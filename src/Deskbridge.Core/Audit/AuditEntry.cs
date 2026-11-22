using Deskbridge.Core.Tools;

namespace Deskbridge.Core.Audit;

/// <summary>
/// One line of the audit log. Decision: allowed, allowed_by_user, remembered, denied_by_policy,
/// denied_by_user or timed_out. Result: ok, error or not_run. Content is kept only when the user turns it on.
/// </summary>
public sealed record AuditEntry(
    DateTimeOffset Time,
    string Client,
    string App,
    string Tool,
    ToolEffect Effect,
    string Decision,
    Rung? Rung,
    string Result,
    long DurationMs,
    string? Content);

public interface IAuditLog
{
    void Append(AuditEntry entry);
}
