using Deskbridge.Core;
using Deskbridge.Core.Apps;
using Deskbridge.Core.Audit;
using Deskbridge.Core.Clients;
using Deskbridge.Core.Policy;
using Deskbridge.Core.Tools;
using Deskbridge.Core.Wire;

namespace Deskbridge.Tests;

public class ToolPipelineTests
{
    static readonly ClientContext Client = new(new ClientHello(1, HelloModes.Classic, new ClientProcess(7, @"C:\Apps\Cursor.exe", "Cursor")));
    static readonly AppId Notepad = new(@"c:\windows\notepad.exe", "Notepad");
    static readonly ToolCall Call = new("ui_set_text", Notepad, ToolEffect.Change, "Type into Notes in Notepad", Content: "hello");

    sealed class FakePolicy(params PolicyDecision[] decisions) : IToolPolicy
    {
        readonly Queue<PolicyDecision> queue = new(decisions);
        public List<ConfirmationAnswer> Recorded { get; } = new();
        public List<ToolCall> Seen { get; } = new();
        public PolicyDecision Decide(ToolCall call, ClientContext client)
        {
            Seen.Add(call);
            return queue.Count > 1 ? queue.Dequeue() : queue.Peek();
        }

        public void Record(ToolCall call, ClientContext client, ConfirmationRequest request, ConfirmationAnswer answer) => Recorded.Add(answer);
    }

    sealed class FakePresenter(params ConfirmationAnswer[] answers) : IConfirmationPresenter
    {
        readonly Queue<ConfirmationAnswer> queue = new(answers);
        public List<ConfirmationRequest> Asked { get; } = new();
        public Task<ConfirmationAnswer> AskAsync(ConfirmationRequest request, CancellationToken ct)
        {
            Asked.Add(request);
            return Task.FromResult(queue.Dequeue());
        }
    }

    sealed class FakeAudit : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = new();
        public void Append(AuditEntry entry) => Entries.Add(entry);
    }

    static ConfirmationRequest Ask(ConfirmationKind kind = ConfirmationKind.ToolCall) =>
        new(kind, "Cursor", Notepad, Call.Action, Call.Content, Call.Effect, CanRemember: true, Note: null);

    static (ToolPipeline Pipeline, FakeAudit Audit, FakePresenter Presenter, FakePolicy Policy) Make(PolicyDecision[] decisions, params ConfirmationAnswer[] answers)
    {
        var policy = new FakePolicy(decisions);
        var presenter = new FakePresenter(answers);
        var audit = new FakeAudit();
        return (new ToolPipeline(policy, presenter, audit, TimeProvider.System), audit, presenter, policy);
    }

    static Func<CancellationToken, Task<Executed<string>>> Execute(List<string> ran) => _ =>
    {
        ran.Add("ran");
        return Task.FromResult(new Executed<string>("done", Rung.UiAutomation));
    };

    static readonly ToolCall Look = new("ui_press", Notepad, ToolEffect.Change, "Press in Notepad");

    static Func<CancellationToken, Task<(ToolCall, string)>> Prepare(List<string> ran, Exception? fail = null) => _ =>
    {
        ran.Add("prepared");
        return fail is null
            ? Task.FromResult((Look with { Action = "Press 'Save' in Notepad" }, "Save"))
            : Task.FromException<(ToolCall, string)>(fail);
    };

    static Func<string, CancellationToken, Task<Executed<string>>> ExecutePrepared(List<string> ran) => (state, _) =>
    {
        ran.Add("ran " + state);
        return Task.FromResult(new Executed<string>("done", Rung.UiAutomation));
    };

    [Fact]
    public async Task Nothing_is_prepared_when_the_policy_refuses_to_look()
    {
        var (pipeline, audit, presenter, _) = Make([new PolicyDecision.Deny("Deskbridge is off for Notepad.")]);
        var ran = new List<string>();
        var ex = await Assert.ThrowsAsync<PolicyDeniedException>(() =>
            pipeline.RunPreparedAsync(Look, Client, Prepare(ran), ExecutePrepared(ran), CancellationToken.None));
        Assert.Equal("Deskbridge is off for Notepad.", ex.Message);
        Assert.Empty(ran);
        Assert.Empty(presenter.Asked);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(("ui_press", "denied_by_policy", "not_run"), (entry.Tool, entry.Decision, entry.Result));
    }

    [Fact]
    public async Task The_look_is_decided_as_a_read_and_the_prepared_call_as_itself()
    {
        var (pipeline, audit, _, policy) = Make([new PolicyDecision.Allow()]);
        var ran = new List<string>();
        await pipeline.RunPreparedAsync(Look, Client, Prepare(ran), ExecutePrepared(ran), CancellationToken.None);
        Assert.Equal([ToolEffect.Read, ToolEffect.Change], policy.Seen.Select(c => c.Effect));
        Assert.Equal("Press 'Save' in Notepad", policy.Seen[1].Action);
        Assert.Equal(["prepared", "ran Save"], ran);
        Assert.Single(audit.Entries);
    }

    [Fact]
    public async Task A_prepare_failure_asks_nothing_and_writes_one_audit_line()
    {
        // The look is allowed; the prepared call would ask, but preparing fails first.
        var (pipeline, audit, presenter, _) = Make([new PolicyDecision.Allow(), new PolicyDecision.Confirm(Ask())], ConfirmationAnswer.Allow);
        var ran = new List<string>();
        var ex = await Assert.ThrowsAsync<DeskbridgeException>(() =>
            pipeline.RunPreparedAsync(Look, Client, Prepare(ran, new DeskbridgeException("There is no way to press this control.")), ExecutePrepared(ran), CancellationToken.None));
        Assert.Equal("There is no way to press this control.", ex.Message);
        Assert.Equal(["prepared"], ran);
        Assert.Empty(presenter.Asked);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(("not_asked", "error"), (entry.Decision, entry.Result));
    }

    [Fact]
    public async Task A_prepared_call_is_confirmed_and_audited_once()
    {
        var (pipeline, audit, presenter, _) = Make([new PolicyDecision.Allow(), new PolicyDecision.Confirm(Ask())], ConfirmationAnswer.Allow);
        var ran = new List<string>();
        Assert.Equal("done", await pipeline.RunPreparedAsync(Look, Client, Prepare(ran), ExecutePrepared(ran), CancellationToken.None));
        Assert.Single(presenter.Asked);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(("allowed_by_user", "ok"), (entry.Decision, entry.Result));
    }

    [Fact]
    public async Task Denied_call_never_runs()
    {
        var (pipeline, audit, _, _) = Make([new PolicyDecision.Deny("Deskbridge is off for Notepad.")]);
        var ran = new List<string>();
        var ex = await Assert.ThrowsAsync<PolicyDeniedException>(() => pipeline.RunAsync(Call, Client, Execute(ran), CancellationToken.None));
        Assert.Equal("Deskbridge is off for Notepad.", ex.Message);
        Assert.Empty(ran);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal("denied_by_policy", entry.Decision);
        Assert.Equal("not_run", entry.Result);
    }

    [Fact]
    public async Task Allowed_call_runs_and_is_audited_once()
    {
        var (pipeline, audit, presenter, _) = Make([new PolicyDecision.Allow()]);
        var ran = new List<string>();
        Assert.Equal("done", await pipeline.RunAsync(Call, Client, Execute(ran), CancellationToken.None));
        Assert.Single(ran);
        Assert.Empty(presenter.Asked);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(("allowed", Rung.UiAutomation, "ok", "Cursor", "ui_set_text"), (entry.Decision, entry.Rung, entry.Result, entry.Client, entry.Tool));
    }

    [Fact]
    public async Task Remembered_allow_is_audited_as_remembered()
    {
        var (pipeline, audit, _, _) = Make([new PolicyDecision.Allow(Remembered: true)]);
        await pipeline.RunAsync(Call, Client, Execute(new()), CancellationToken.None);
        Assert.Equal("remembered", Assert.Single(audit.Entries).Decision);
    }

    [Fact]
    public async Task Confirmed_call_runs_after_allow()
    {
        var (pipeline, audit, presenter, policy) = Make([new PolicyDecision.Confirm(Ask())], ConfirmationAnswer.Allow);
        var ran = new List<string>();
        await pipeline.RunAsync(Call, Client, Execute(ran), CancellationToken.None);
        Assert.Single(ran);
        Assert.Single(presenter.Asked);
        Assert.Equal([ConfirmationAnswer.Allow], policy.Recorded);
        Assert.Equal("allowed_by_user", Assert.Single(audit.Entries).Decision);
    }

    [Fact]
    public async Task User_deny_never_runs()
    {
        var (pipeline, audit, _, _) = Make([new PolicyDecision.Confirm(Ask())], ConfirmationAnswer.Deny);
        var ran = new List<string>();
        await Assert.ThrowsAsync<DeniedByUserException>(() => pipeline.RunAsync(Call, Client, Execute(ran), CancellationToken.None));
        Assert.Empty(ran);
        Assert.Equal(("denied_by_user", "not_run"), (audit.Entries[0].Decision, audit.Entries[0].Result));
    }

    [Fact]
    public async Task Timeout_counts_as_deny()
    {
        var (pipeline, audit, _, _) = Make([new PolicyDecision.Confirm(Ask())], ConfirmationAnswer.TimedOut);
        var ex = await Assert.ThrowsAsync<DeniedByUserException>(() => pipeline.RunAsync(Call, Client, Execute(new()), CancellationToken.None));
        Assert.Contains("No one answered", ex.Message);
        Assert.Equal("timed_out", Assert.Single(audit.Entries).Decision);
    }

    [Fact]
    public async Task Client_approval_comes_first_then_the_call_is_decided_again()
    {
        var (pipeline, audit, presenter, _) = Make(
            [new PolicyDecision.Confirm(Ask(ConfirmationKind.ClientApproval)), new PolicyDecision.Confirm(Ask()), new PolicyDecision.Allow()],
            ConfirmationAnswer.Allow, ConfirmationAnswer.Allow);
        var ran = new List<string>();
        await pipeline.RunAsync(Call, Client, Execute(ran), CancellationToken.None);
        Assert.Equal([ConfirmationKind.ClientApproval, ConfirmationKind.ToolCall], presenter.Asked.Select(a => a.Kind));
        Assert.Single(ran);
        Assert.Single(audit.Entries);
    }

    [Fact]
    public async Task Execute_failure_is_audited_as_error()
    {
        var (pipeline, audit, _, _) = Make([new PolicyDecision.Allow()]);
        await Assert.ThrowsAsync<DeskbridgeException>(() =>
            pipeline.RunAsync<string>(Call, Client, _ => throw new DeskbridgeException("That window is gone."), CancellationToken.None));
        Assert.Equal(("allowed", "error"), (audit.Entries[0].Decision, audit.Entries[0].Result));
    }

    [Fact]
    public async Task Content_reaches_the_audit_log_which_decides_whether_to_keep_it()
    {
        var (pipeline, audit, _, _) = Make([new PolicyDecision.Allow()]);
        await pipeline.RunAsync(Call, Client, Execute(new()), CancellationToken.None);
        Assert.Equal("hello", Assert.Single(audit.Entries).Content);
    }

    [Fact]
    public void Client_context_names_and_keys()
    {
        Assert.Equal(("Cursor", @"c:\apps\cursor.exe"), (Client.Name, Client.Key));
        var unknown = new ClientContext(new ClientHello(1, HelloModes.Classic, new ClientProcess(42, null, null)));
        Assert.Equal(("An unknown client", "pid:42"), (unknown.Name, unknown.Key));
    }
}
