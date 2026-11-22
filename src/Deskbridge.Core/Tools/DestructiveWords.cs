using System.Text.RegularExpressions;

namespace Deskbridge.Core.Tools;

/// <summary>
/// Control names that mean deleting, sending, publishing or paying. Pressing such a control is a
/// destructive call: it always asks, and a remembered grant never covers it.
/// </summary>
public static partial class DestructiveWords
{
    public static bool IsDestructive(string? controlName) =>
        controlName is not null && Pattern().IsMatch(controlName);

    [GeneratedRegex(
        @"\b(delete|remove|send|publish|post|pay|buy|purchase|uninstall|format|discard|erase|empty|don'?t\s+save|close\s+without\s+saving)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
