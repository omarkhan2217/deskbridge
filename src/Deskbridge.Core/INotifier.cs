namespace Deskbridge.Core;

/// <summary>Shows a notification to the user (the tray icon's balloon, in the broker).</summary>
public interface INotifier
{
    void Show(string title, string text);
}

/// <summary>Used until the tray is running: refuses plainly rather than pretend a notification was shown.</summary>
public sealed class NoNotifier : INotifier
{
    public void Show(string title, string text) =>
        throw new DeskbridgeException("Deskbridge cannot show notifications right now because its tray icon is not running.");
}
