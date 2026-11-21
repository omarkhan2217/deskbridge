namespace Deskbridge.Engines.Uia;

/// <summary>
/// How long UI Automation may wait. TransactionTimeout and ConnectionTimeout are UI Automation's own
/// limits (set through IUIAutomation2); CallTimeout is Deskbridge's backstop for a call that still hangs.
/// </summary>
public sealed record UiaWorkerOptions(TimeSpan CallTimeout, TimeSpan TransactionTimeout, TimeSpan ConnectionTimeout)
{
    public static UiaWorkerOptions Default { get; } =
        new(TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2));
}
