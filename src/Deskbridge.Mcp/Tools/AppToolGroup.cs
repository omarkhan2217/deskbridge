using System.ComponentModel;
using Deskbridge.Core.Apps;
using Deskbridge.Core.Clients;
using Deskbridge.Core.Tools;
using Deskbridge.Engines.Os;
using Deskbridge.Engines.Os.Apps;
using ModelContextProtocol.Server;

namespace Deskbridge.Mcp.Tools;

/// <summary>apps_list_installed, apps_list_running, apps_launch, windows_describe.</summary>
public static class AppToolGroup
{
    public static IEnumerable<McpServerTool> Create(ToolServices services, ClientContext client)
    {
        yield return McpServerTool.Create(
            (CancellationToken ct) => ToolJson.RunAsync(() => services.Pipeline.RunAsync(
                new ToolCall("apps_list_installed", AppId.Windows, ToolEffect.Read, "List the installed apps"), client,
                async _ => new Executed<object>(
                    new { apps = (await AppsFolder.ListAsync().ConfigureAwait(false)).Select(a => new { name = a.Name, app_id = a.AppId }) },
                    Rung.WindowsApi),
                ct)),
            ToolKit.Options("apps_list_installed", ToolEffect.Read,
                "Lists the apps in the Start menu with the app ID that apps_launch takes."));

        yield return McpServerTool.Create(
            (CancellationToken ct) => ToolJson.RunAsync(() => services.Pipeline.RunAsync(
                new ToolCall("apps_list_running", AppId.Windows, ToolEffect.Read, "List the running apps"), client,
                _ => Task.FromResult(new Executed<object>(
                    new
                    {
                        apps = WindowLister.List()
                            .GroupBy(w => w.ProcessId)
                            .Select(g => new { process = g.First().ProcessName, pid = g.Key, path = ProcessFacts.ImagePath(g.Key), windows = g.Count() }),
                    },
                    Rung.WindowsApi)),
                ct)),
            ToolKit.Options("apps_list_running", ToolEffect.Read,
                "Lists the running apps that have visible windows, with how many windows each has."));

        yield return McpServerTool.Create(
            ([Description("An app ID from apps_list_installed, or the full path of an .exe.")] string app,
             CancellationToken ct = default) =>
                ToolJson.RunAsync(() =>
                {
                    var target = Path.IsPathFullyQualified(app) ? AppResolver.FromPath(app) : new AppId("app:" + app.ToLowerInvariant(), AppName(app));
                    return services.Pipeline.RunAsync(
                        new ToolCall("apps_launch", target, ToolEffect.Change, $"Open {target.DisplayName}. It may come to the front."), client,
                        async token =>
                        {
                            var result = await Launcher.LaunchAsync(app, token).ConfigureAwait(false);
                            return new Executed<object>(
                                new { app, took_focus = result.TookFocus, foreground_title = result.ForegroundTitle, window = result.NewWindow, window_title = result.NewWindowTitle },
                                Rung.WindowsApi);
                        },
                        ct);
                }),
            ToolKit.Options("apps_launch", ToolEffect.Change,
                "Opens an app without activating it. Some apps come to the front anyway; the result says whether it did. Asks the user first."));

        yield return McpServerTool.Create(
            ([Description("The window's handle from windows_list, like 0x1A2B.")] string window,
             CancellationToken ct = default) =>
                ToolJson.RunAsync(() =>
                {
                    var target = ToolKit.Window(window);
                    return services.Pipeline.RunAsync(
                        new ToolCall("windows_describe", target.App, ToolEffect.Read, $"Describe a window of {target.App.DisplayName}", TargetElevated: target.Elevated),
                        client,
                        _ => Task.FromResult(new Executed<object>(WindowDescriber.Describe(target.Handle), Rung.WindowsApi)),
                        ct);
                }),
            ToolKit.Options("windows_describe", ToolEffect.Read,
                "Describes a window from the window manager alone: title, process, position and size, minimized or not, in front or not, run as administrator or not."));
    }

    /// <summary>A readable name from an app ID: "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App" gives "WindowsNotepad".</summary>
    static string AppName(string appId)
    {
        var name = appId.Split('!')[0].Split('_')[0];
        var dot = name.LastIndexOf('.');
        return dot >= 0 && dot < name.Length - 1 ? name[(dot + 1)..] : name;
    }
}
