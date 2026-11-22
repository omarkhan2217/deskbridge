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

    /// <summary>
    /// Judges a control by everything the app says about it: its name, its AutomationId read as words (so
    /// "btnRemove_Item" reads "btn Remove Item") and its help text. A control being pressed that says nothing
    /// about itself counts as destructive, because nobody can tell what pressing it does.
    /// </summary>
    public static bool IsDestructiveControl(string? name, string? automationId, string? helpText, bool pressing)
    {
        string?[] parts = [name, Words(automationId), helpText];
        return parts.All(string.IsNullOrWhiteSpace) ? pressing : parts.Any(IsDestructive);
    }

    static string? Words(string? id) =>
        id is null ? null : LowerToUpper().Replace(AcronymToWord().Replace(id, "$1 $2"), "$1 $2").Replace('_', ' ').Replace('-', ' ');

    [GeneratedRegex("([a-z0-9])([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex LowerToUpper();

    [GeneratedRegex("([A-Z])([A-Z][a-z])", RegexOptions.CultureInvariant)]
    private static partial Regex AcronymToWord();

    // Verb forms count too ("Deletes the selected rows", "Removing"), so help text is read like a name.
    // "Sender" and "Payment" do not match: the word has to end where the verb form ends.
    [GeneratedRegex(
        @"\b(?:(?:delet|remov|purchas|eras)(?:e|es|ed|ing)|(?:send|publish|post|pay|buy|uninstall|format|discard)(?:s|es|ed|ing)?|empt(?:y|ies|ied|ying)|don'?t\s+save|close\s+without\s+saving)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
