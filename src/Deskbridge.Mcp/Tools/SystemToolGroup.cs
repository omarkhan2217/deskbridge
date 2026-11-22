using System.ComponentModel;
using Deskbridge.Core;
using Deskbridge.Core.Apps;
using Deskbridge.Core.Clients;
using Deskbridge.Core.Tools;
using Deskbridge.Engines.Os.Apps;
using Deskbridge.Engines.Os.Desktop;
using ModelContextProtocol.Server;

namespace Deskbridge.Mcp.Tools;

/// <summary>clipboard_read, clipboard_write, media_now_playing, media_control, volume_set, notify_show, settings_open.</summary>
public static class SystemToolGroup
{
    public static IEnumerable<McpServerTool> Create(ToolServices services, ClientContext client)
    {
        Task<object> Run(string tool, AppId app, ToolEffect effect, string action, string? content, Func<CancellationToken, Task<object>> work, CancellationToken ct) =>
            services.Pipeline.RunAsync(new ToolCall(tool, app, effect, action, content), client,
                async token => new Executed<object>(await work(token).ConfigureAwait(false), Rung.WindowsApi), ct);

        yield return McpServerTool.Create(
            (CancellationToken ct) => ToolJson.RunAsync(() => Run("clipboard_read", AppId.Clipboard, ToolEffect.Read, "Read the clipboard's text", null,
                _ => Task.FromResult<object>(ClipboardText.Read() is { } text
                    ? new { has_text = true, untrusted_text = text, note = ToolKit.UntrustedNote }
                    : new { has_text = false, untrusted_text = "", note = "The clipboard holds no text." }),
                ct)),
            ToolKit.Options("clipboard_read", ToolEffect.Read, "Reads the text on the clipboard, returned in 'untrusted_text'."));

        yield return McpServerTool.Create(
            ([Description("The text to put on the clipboard.")] string text, CancellationToken ct = default) =>
                ToolJson.RunAsync(() => Run("clipboard_write", AppId.Clipboard, ToolEffect.Change, "Put text on the clipboard", text, _ =>
                {
                    ClipboardText.Write(text);
                    return Task.FromResult<object>(new { written_chars = text.Length });
                }, ct)),
            ToolKit.Options("clipboard_write", ToolEffect.Change, "Puts text on the clipboard, replacing what is there. Asks the user first."));

        yield return McpServerTool.Create(
            (CancellationToken ct) => ToolJson.RunAsync(() => Run("media_now_playing", AppId.Media, ToolEffect.Read, "See what is playing", null, async _ =>
                await MediaSession.NowPlayingAsync().ConfigureAwait(false) is { } playing
                    ? new { playing = true, app = playing.App, title = playing.Title, artist = playing.Artist, status = playing.Status }
                    : (object)new { playing = false, message = "Nothing is playing." },
                ct)),
            ToolKit.Options("media_now_playing", ToolEffect.Read, "Says what is playing in any media app (Spotify, a browser, a video player), and whether it is playing or paused."));

        yield return McpServerTool.Create(
            ([Description("play, pause, toggle, next or previous.")] string action, CancellationToken ct = default) =>
                ToolJson.RunAsync(() =>
                {
                    var media = ParseAction(action);
                    return Run("media_control", AppId.Media, ToolEffect.Change, $"Media: {action.ToLowerInvariant()}", null, async _ =>
                        await MediaSession.ControlAsync(media).ConfigureAwait(false)
                            ? new { done = true, action = action.ToLowerInvariant() }
                            : (object)new { done = false, message = "Nothing is playing or paused, so there is nothing to control." },
                        ct);
                }),
            ToolKit.Options("media_control", ToolEffect.Change, "Plays, pauses or skips in whichever media app is current, like the media keys. Asks the user first."));

        yield return McpServerTool.Create(
            ([Description("The new volume, 0 to 100.")] int percent, CancellationToken ct = default) =>
                ToolJson.RunAsync(() => Run("volume_set", AppId.Media, ToolEffect.Change, $"Set the volume to {Math.Clamp(percent, 0, 100)}%", null, _ =>
                {
                    var (before, after) = Volume.Set(percent);
                    return Task.FromResult<object>(new { before, after });
                }, ct)),
            ToolKit.Options("volume_set", ToolEffect.Change, "Sets the main speaker volume. Asks the user first."));

        yield return McpServerTool.Create(
            ([Description("A short title.")] string title,
             [Description("The message.")] string text,
             CancellationToken ct = default) =>
                ToolJson.RunAsync(() => Run("notify_show", AppId.Notifications, ToolEffect.Change, $"Show a notification: {title}", text, _ =>
                {
                    services.Notifier.Show(title, text);
                    return Task.FromResult<object>(new { shown = true });
                }, ct)),
            ToolKit.Options("notify_show", ToolEffect.Change, "Shows a Windows notification from Deskbridge's tray icon."));

        yield return McpServerTool.Create(
            ([Description("A Settings page name such as 'taskbar', 'display', 'bluetooth' or 'sound'.")] string page,
             CancellationToken ct = default) =>
                ToolJson.RunAsync(() =>
                {
                    var uri = SettingsPages.Uri(page);
                    return Run("settings_open", AppId.Settings, ToolEffect.Change, $"Open Settings at '{page}'. Settings may come to the front.", null, async token =>
                    {
                        var result = await Launcher.LaunchAsync(uri, token).ConfigureAwait(false);
                        return new { page, took_focus = result.TookFocus, foreground_title = result.ForegroundTitle, window = result.NewWindow };
                    }, ct);
                }),
            ToolKit.Options("settings_open", ToolEffect.Change, "Opens a page of the Windows Settings app. Settings usually comes to the front; the result says whether it did. Asks the user first."));
    }

    static MediaAction ParseAction(string action) => action.ToLowerInvariant() switch
    {
        "play" => MediaAction.Play,
        "pause" => MediaAction.Pause,
        "toggle" => MediaAction.Toggle,
        "next" => MediaAction.Next,
        "previous" => MediaAction.Previous,
        _ => throw new DeskbridgeException("action is play, pause, toggle, next or previous."),
    };
}
