using Deskbridge.Core.Audit;
using Deskbridge.Core.Clients;
using Deskbridge.Core.Policy;

namespace Deskbridge.Core.Tools;

/// <summary>
/// The one path every tool call takes: the policy decides, the user is asked when needed, the engine
/// acts only on an allow, and exactly one audit line is written.
/// </summary>
public sealed class ToolPipeline(IToolPolicy policy, IConfirmationPresenter presenter, IAuditLog audit, TimeProvider time)
{
    // Client approval can come before the call's own confirmation; anything beyond that is a policy bug.
    const int MaxRounds = 3;

    public async Task<T> RunAsync<T>(ToolCall call, ClientContext client, Func<CancellationToken, Task<Executed<T>>> execute, CancellationToken ct)
    {
        var decision = await DecideAsync(call, client, ct).ConfigureAwait(false);
        var started = time.GetTimestamp();
        try
        {
            var executed = await execute(ct).ConfigureAwait(false);
            Write(call, client, decision, executed.Rung, "ok", started);
            return executed.Value;
        }
        catch
        {
            Write(call, client, decision, null, "error", started);
            throw;
        }
    }

    /// <summary>
    /// For a call that must look at the app before it can be described (a UI action names its control in
    /// the confirmation). The policy first decides the look as a read, so a paused Deskbridge, an unapproved
    /// client or an app that is off refuses before anything is read. Then prepare builds the real call, which
    /// is decided, confirmed and run as usual. Every path writes exactly one audit line.
    /// </summary>
    public async Task<T> RunPreparedAsync<T, TState>(ToolCall look, ClientContext client,
        Func<CancellationToken, Task<(ToolCall Call, TState State)>> prepare,
        Func<TState, CancellationToken, Task<Executed<T>>> execute, CancellationToken ct)
    {
        await DecideAsync(look with { Effect = ToolEffect.Read }, client, ct, audited: look).ConfigureAwait(false);
        ToolCall call;
        TState state;
        var started = time.GetTimestamp();
        try
        {
            (call, state) = await prepare(ct).ConfigureAwait(false);
        }
        catch
        {
            Write(look, client, "not_asked", null, "error", started);
            throw;
        }

        return await RunAsync(call, client, token => execute(state, token), ct).ConfigureAwait(false);
    }

    /// <returns>The audit decision for an allowed call; throws for any refusal.</returns>
    async Task<string> DecideAsync(ToolCall call, ClientContext client, CancellationToken ct, ToolCall? audited = null)
    {
        audited ??= call;
        for (var round = 0; round < MaxRounds; round++)
        {
            switch (policy.Decide(call, client))
            {
                case PolicyDecision.Allow allow:
                    return allow.Remembered ? "remembered" : "allowed";

                case PolicyDecision.Deny deny:
                    Write(audited, client, "denied_by_policy", null, "not_run", time.GetTimestamp());
                    throw new PolicyDeniedException(deny.Reason);

                case PolicyDecision.Confirm confirm:
                    var answer = await presenter.AskAsync(confirm.Request, ct).ConfigureAwait(false);
                    policy.Record(call, client, confirm.Request, answer);
                    if (answer is ConfirmationAnswer.Deny or ConfirmationAnswer.TimedOut)
                    {
                        var timedOut = answer == ConfirmationAnswer.TimedOut;
                        Write(audited, client, timedOut ? "timed_out" : "denied_by_user", null, "not_run", time.GetTimestamp());
                        throw new DeniedByUserException(timedOut);
                    }

                    if (confirm.Request.Kind == ConfirmationKind.ToolCall)
                    {
                        return "allowed_by_user";
                    }

                    continue; // a client approval is not the call's own confirmation: decide again
            }
        }

        throw new InvalidOperationException("The policy kept asking for confirmation without deciding.");
    }

    void Write(ToolCall call, ClientContext client, string decision, Rung? rung, string result, long started) =>
        audit.Append(new AuditEntry(
            time.GetUtcNow(),
            client.Name,
            call.App.DisplayName,
            call.Tool,
            call.Effect,
            decision,
            rung,
            result,
            (long)time.GetElapsedTime(started).TotalMilliseconds,
            call.Content));
}
