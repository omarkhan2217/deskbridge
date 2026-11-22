namespace Deskbridge.Core.Apps;

/// <summary>
/// An app as permissions see it. <paramref name="Key"/> is the lowercase full path of its exe, or a
/// "system.*" name for parts of Windows that are not one app.
/// </summary>
public sealed record AppId(string Key, string DisplayName)
{
    public static AppId Files { get; } = new("system.files", "Files");

    public static AppId Clipboard { get; } = new("system.clipboard", "Clipboard");

    public static AppId Media { get; } = new("system.media", "Media and volume");

    public static AppId Settings { get; } = new("system.settings", "Windows Settings");

    public static AppId Windows { get; } = new("system.windows", "Windows and apps list");

    public static AppId Notifications { get; } = new("system.notifications", "Notifications");

    public static AppId ForExe(string exePath, string displayName) =>
        new(Path.GetFullPath(exePath).ToLowerInvariant(), displayName);
}
