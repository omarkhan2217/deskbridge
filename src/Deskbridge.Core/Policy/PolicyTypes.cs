using Deskbridge.Core.Apps;
using Deskbridge.Core.Clients;
using Deskbridge.Core.Tools;

namespace Deskbridge.Core.Policy;

/// <summary>What the policy says about one call.</summary>
public abstract record PolicyDecision
{
    PolicyDecision()
    {
    }

    /// <summary>Run it. <paramref name="Remembered"/> is true when a remembered grant allowed it.</summary>
    public sealed record Allow(bool Remembered = false) : PolicyDecision;

    public sealed record Deny(string Reason) : PolicyDecision;

    public sealed record Confirm(ConfirmationRequest Request) : PolicyDecision;
}

public enum ConfirmationKind
{
    ToolCall,
    ClientApproval,
}

public enum ConfirmationAnswer
{
    Allow,
    AllowAndRemember,
    Deny,
    TimedOut,
}

/// <summary>What the confirmation window shows.</summary>
public sealed record ConfirmationRequest(
    ConfirmationKind Kind,
    string ClientName,
    AppId App,
    string Action,
    string? Content,
    ToolEffect Effect,
    bool CanRemember,
    string? Note);

public interface IConfirmationPresenter
{
    Task<ConfirmationAnswer> AskAsync(ConfirmationRequest request, CancellationToken ct);
}

public interface IToolPolicy
{
    PolicyDecision Decide(ToolCall call, ClientContext client);

    /// <summary>Stores what an answer implies: an approved client, or a remembered grant.</summary>
    void Record(ToolCall call, ClientContext client, ConfirmationRequest request, ConfirmationAnswer answer);
}

/// <summary>The policy refused the call. The message says why and what the user can change.</summary>
public sealed class PolicyDeniedException(string reason) : DeskbridgeException(reason);

/// <summary>The user said no, or did not answer in time.</summary>
public sealed class DeniedByUserException(bool timedOut)
    : DeskbridgeException(timedOut
        ? "No one answered Deskbridge's confirmation in time, so this was not done."
        : "The user denied this in Deskbridge, so it was not done.")
{
    public bool TimedOut { get; } = timedOut;
}
