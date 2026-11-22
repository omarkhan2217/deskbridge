using System.ComponentModel;
using Deskbridge.Core;
using Deskbridge.Core.Clients;
using Deskbridge.Core.Tools;
using Deskbridge.Engines.Uia;
using ModelContextProtocol.Server;

namespace Deskbridge.Mcp.Tools;

/// <summary>ui_tree, ui_find, ui_press, ui_set_text, ui_select, ui_toggle, ui_scroll (ui_read is in WindowTools).</summary>
public static class UiToolGroup
{
    const string WindowHelp = "The window's handle from windows_list, like 0x1A2B.";
    const string ElementHelp = "An element_id from ui_find or ui_tree, or the control's AutomationId or name.";

    public static IEnumerable<McpServerTool> Create(ToolServices services, ClientContext client)
    {
        yield return McpServerTool.Create(
            ([Description(WindowHelp)] string window,
             [Description("How many levels deep to go. Default 4.")] int max_depth = 4,
             [Description("Most controls to return. Default 300.")] int max_nodes = 300,
             CancellationToken ct = default) =>
                ToolJson.RunAsync(() => ReadAsync(services, client, "ui_tree", window, "List the controls in", async (target, token) =>
                {
                    var tree = await services.Ui.TreeAsync(target.Handle, Math.Clamp(max_depth, 1, 10), Math.Clamp(max_nodes, 1, 1_000), token).ConfigureAwait(false);
                    return new { nodes = tree.Nodes, truncated = tree.Truncated, note = "Names come from the app: " + ToolKit.UntrustedNote };
                }, ct)),
            ToolKit.Options("ui_tree", ToolEffect.Read,
                "Lists a window's controls (type, name, AutomationId, enabled, what each supports), outer controls first, each with an element_id and the element_id of the control that holds it (parent). Works on windows that are not in front. Refuses Chromium and Electron apps."));

        yield return McpServerTool.Create(
            ([Description(WindowHelp)] string window,
             [Description("Optional: all or part of the control's name, matched ignoring case. Exact matches come first.")] string? name = null,
             [Description("Optional: the control's AutomationId.")] string? automation_id = null,
             [Description("Optional: the control type, like button, edit, checkbox, listitem.")] string? control_type = null,
             CancellationToken ct = default) =>
                ToolJson.RunAsync(() => ReadAsync(services, client, "ui_find", window, "Find controls in", async (target, token) =>
                    new
                    {
                        found = (await services.Ui.FindAsync(target.Handle, name, automation_id, control_type, token).ConfigureAwait(false))
                            .Select(e => new { element_id = e.ElementId, window = Deskbridge.Engines.Os.WindowHandles.Format(e.Window), name = e.Name, automation_id = e.AutomationId, control_type = e.ControlType, enabled = e.Enabled }),
                    }, ct)),
            ToolKit.Options("ui_find", ToolEffect.Read,
                "Finds up to 20 controls in a window by name, AutomationId or type, and returns an element_id for each."));

        yield return Action(services, client, "ui_press", "Press",
            "Presses a button or link, or opens a menu, in a window whether it is in front or not. Never uses the mouse. Asks the user first; buttons that delete or send always ask.",
            (actions, target, info, _, token) => actions.PressAsync(target.Handle, info, token));

        yield return McpServerTool.Create(
            ([Description(WindowHelp)] string window,
             [Description(ElementHelp)] string element,
             [Description("The text to put in the control, replacing what is there.")] string text,
             CancellationToken ct = default) =>
                ToolJson.RunAsync(() => ActAsync(services, client, "ui_set_text", "Type into", window, element, text,
                    (actions, target, info, token) => actions.SetTextAsync(target.Handle, info, text, token), ct)),
            ToolKit.Options("ui_set_text", ToolEffect.Change,
                "Replaces the text in a text box without typing keys and without focus. Never writes into password fields. Asks the user first, showing the exact text."));

        yield return Action(services, client, "ui_select", "Select",
            "Selects an item in a list, a tab or a radio button. Asks the user first.",
            (actions, target, info, _, token) => actions.SelectAsync(target.Handle, info, token));

        yield return McpServerTool.Create(
            ([Description(WindowHelp)] string window,
             [Description(ElementHelp)] string element,
             [Description("Optional: true to turn on, false to turn off. Leave out to flip it.")] bool? on = null,
             CancellationToken ct = default) =>
                ToolJson.RunAsync(() => ActAsync(services, client, "ui_toggle", on switch { true => "Turn on", false => "Turn off", _ => "Switch" }, window, element, null,
                    (actions, target, info, token) => actions.ToggleAsync(target.Handle, info, on, token), ct,
                    unchanged: info => on is not null && info.IsOn == on)),
            ToolKit.Options("ui_toggle", ToolEffect.Change,
                "Turns a checkbox or switch on or off. If it is already as asked, it is left alone and the user is not asked. Otherwise asks the user first."));

        yield return McpServerTool.Create(
            ([Description(WindowHelp)] string window,
             [Description(ElementHelp)] string element,
             [Description("up, down, left or right.")] string direction = "down",
             [Description("small or page. Default small.")] string amount = "small",
             CancellationToken ct = default) =>
                ToolJson.RunAsync(() => ActAsync(services, client, "ui_scroll", $"Scroll {direction}", window, element, null,
                    (actions, target, info, token) => actions.ScrollAsync(target.Handle, info, direction.ToLowerInvariant(), amount.Equals("page", StringComparison.OrdinalIgnoreCase), token), ct)),
            ToolKit.Options("ui_scroll", ToolEffect.Change,
                "Scrolls a list or document (or the nearest scrollable area around a control) without the mouse wheel. Asks the user first."));
    }

    static McpServerTool Action(ToolServices services, ClientContext client, string tool, string verb, string description,
        Func<UiaActions, WindowTarget, ElementInfo, string?, CancellationToken, Task<ActionDone>> act) =>
        McpServerTool.Create(
            ([Description(WindowHelp)] string window,
             [Description(ElementHelp)] string element,
             CancellationToken ct = default) =>
                ToolJson.RunAsync(() => ActAsync(services, client, tool, verb, window, element, null,
                    (actions, target, info, token) => act(actions, target, info, null, token), ct)),
            ToolKit.Options(tool, ToolEffect.Change, description));

    static async Task<object> ReadAsync(ToolServices services, ClientContext client, string tool, string window, string verb,
        Func<WindowTarget, CancellationToken, Task<object>> read, CancellationToken ct)
    {
        var target = ToolKit.Window(window);
        return await services.Pipeline.RunAsync(
            new ToolCall(tool, target.App, ToolEffect.Read, $"{verb} {target.App.DisplayName}", TargetElevated: target.Elevated),
            client,
            async token => new Executed<object>(await read(target, token).ConfigureAwait(false), Rung.UiAutomation),
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Finds the control first, so the confirmation names it and a control named like "Delete" or "Send"
    /// makes the call destructive. The policy decides that look as a read first, so a paused Deskbridge, an
    /// unapproved client, an elevated window or an app that is off is refused before any control is read.
    /// The action then checks the control again before acting.
    /// </summary>
    static async Task<object> ActAsync(ToolServices services, ClientContext client, string tool, string verb, string window, string element, string? content,
        Func<UiaActions, WindowTarget, ElementInfo, CancellationToken, Task<ActionDone>> act, CancellationToken ct,
        Func<ElementInfo, bool>? unchanged = null)
    {
        var target = ToolKit.Window(window);
        var look = new ToolCall(tool, target.App, ToolEffect.Change, $"{verb} in {target.App.DisplayName}", content, TargetElevated: target.Elevated);
        return await services.Pipeline.RunPreparedAsync(look, client,
            async token =>
            {
                var info = await services.Ui.DescribeAsync(target.Handle, element, token).ConfigureAwait(false);
                var need = tool switch { "ui_press" => "press", "ui_toggle" => "toggle", "ui_select" => "select", "ui_set_text" => "set_text", _ => null };
                if (need is not null && !info.Can.Contains(need))
                {
                    // Refuse before anyone is asked to approve something that cannot happen.
                    throw new NoWayToActException(need.Replace('_', ' '));
                }

                var name = new[] { info.Name, info.AutomationId }.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? info.ControlType;
                if (unchanged?.Invoke(info) == true)
                {
                    // Already as asked: nothing changes, so this is a read and nobody is asked.
                    return (new ToolCall(tool, target.App, ToolEffect.Read, $"Check '{name}' in {target.App.DisplayName}", TargetElevated: target.Elevated), (info, Unchanged: true));
                }

                // A nameless button is destructive too: the user could not tell what they would be approving.
                var effect = DestructiveWords.IsDestructiveControl(info.Name, info.AutomationId, info.HelpText, pressing: tool == "ui_press")
                    ? ToolEffect.Destructive
                    : ToolEffect.Change;
                return (new ToolCall(tool, target.App, effect, $"{verb} '{name}' in {target.App.DisplayName}", content, TargetElevated: target.Elevated), (info, Unchanged: false));
            },
            async (state, token) =>
            {
                var (info, same) = state;
                if (same)
                {
                    return new Executed<object>(
                        new { element = info.ElementId, name = info.Name, how = "unchanged", state = info.IsOn == true ? "on" : "off", rung = Rung.UiAutomation },
                        Rung.UiAutomation);
                }

                var done = await act(services.Ui, target, info, token).ConfigureAwait(false);
                return new Executed<object>(
                    new { element = done.Element.ElementId, name = done.Element.Name, how = done.How, state = done.State, rung = done.Rung },
                    done.Rung);
            },
            ct).ConfigureAwait(false);
    }
}
