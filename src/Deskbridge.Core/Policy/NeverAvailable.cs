using Deskbridge.Core.Tools;

namespace Deskbridge.Core.Policy;

/// <summary>
/// Calls Deskbridge refuses whatever the permissions say. UI-level checks (password fields, secret
/// windows, CAPTCHAs) happen in the UI Automation engine, which can see the controls.
/// </summary>
public static class NeverAvailable
{
    static readonly string[] ProtectedApps =
    [
        "sechealthui",     // Windows Security
        "securityhealthhost",
        "consent",         // UAC prompt
        "credentialuibroker",
        "authy desktop",   // one-time codes
        "authy",
        "winauth",
        "2fast",
    ];

    public static string? Reason(ToolCall call)
    {
        if (call.TargetElevated)
        {
            return "That window runs as administrator. Windows does not let other programs touch it, and Deskbridge never asks to.";
        }

        var exeName = Path.GetFileNameWithoutExtension(call.App.Key);
        return ProtectedApps.Contains(exeName, StringComparer.OrdinalIgnoreCase)
            ? $"Deskbridge never touches {call.App.DisplayName}: it guards your PC's security, sign-ins or one-time codes."
            : null;
    }
}
