using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Deskbridge.Core;
using Deskbridge.Core.Tools;
using Deskbridge.Engines.Os;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace Deskbridge.Engines.Uia;

/// <summary>
/// Finds controls and acts on them through UI Automation patterns, then window messages, never the mouse.
/// Every action reads its control again first and refuses if it changed since it was found, so what the
/// user approved is what happens.
/// </summary>
public sealed class UiaActions(UiaWorker worker)
{
    static readonly TimeSpan CacheLife = TimeSpan.FromSeconds(60);
    readonly ConcurrentDictionary<string, (AutomationElement Element, DateTime At)> cache = new();

    public Task<ElementInfo> DescribeAsync(nint window, string element, CancellationToken ct = default)
    {
        UiaGuards.Check(window);
        return Run(automation =>
        {
            var found = Resolve(automation, window, element);
            if (found.Properties.IsPassword.ValueOrDefault)
            {
                throw new SecretFieldException();
            }

            return Remember(window, found) with
            {
                Can = Capabilities(window, found),
                IsOn = found.Patterns.Toggle.TryGetPattern(out var toggle) ? toggle.ToggleState.ValueOrDefault == ToggleState.On : null,
            };
        }, ct);
    }

    public Task<IReadOnlyList<ElementInfo>> FindAsync(nint window, string? name, string? automationId, string? controlType, CancellationToken ct = default)
    {
        UiaGuards.Check(window);
        return Run<IReadOnlyList<ElementInfo>>(automation =>
        {
            var root = automation.FromHandle(window);
            var cf = root.ConditionFactory;
            var all = root.FindAllDescendants();
            return all
                .Where(e => automationId is null || string.Equals(e.Properties.AutomationId.ValueOrDefault, automationId, StringComparison.Ordinal))
                .Where(e => name is null || (e.Properties.Name.ValueOrDefault ?? "").Contains(name, StringComparison.OrdinalIgnoreCase))
                .Where(e => controlType is null || string.Equals(e.Properties.ControlType.ValueOrDefault.ToString(), controlType, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => name is not null && string.Equals(e.Properties.Name.ValueOrDefault, name, StringComparison.OrdinalIgnoreCase) ? 0 : 1) // exact names first; stable
                .Take(20)
                .Select(e => Remember(window, e))
                .ToList();
        }, ct);
    }

    public Task<UiTree> TreeAsync(nint window, int maxDepth = 4, int maxNodes = 300, CancellationToken ct = default)
    {
        UiaGuards.Check(window);
        return Run(automation =>
        {
            var nodes = new List<UiNode>();
            var truncated = false;
            // Breadth first, so a listing cut short by max_nodes keeps the outer controls.
            var queue = new Queue<(AutomationElement Element, int Depth, string? Parent)>();
            foreach (var child in automation.FromHandle(window).FindAllChildren())
            {
                queue.Enqueue((child, 1, null));
            }

            while (queue.Count > 0)
            {
                var (element, depth, parent) = queue.Dequeue();
                if (nodes.Count >= maxNodes)
                {
                    truncated = true;
                    break;
                }

                var info = Remember(window, element);
                nodes.Add(new UiNode(info.ElementId, parent, depth, info.ControlType, info.Name, info.AutomationId, info.Enabled, Patterns(element)));
                if (depth < maxDepth)
                {
                    foreach (var child in element.FindAllChildren())
                    {
                        queue.Enqueue((child, depth + 1, info.ElementId));
                    }
                }
                else if (element.FindFirstChild() is not null)
                {
                    truncated = true;
                }
            }

            return new UiTree(nodes, truncated || queue.Count > 0);
        }, ct);
    }

    public Task<ActionDone> PressAsync(nint window, ElementInfo info, CancellationToken ct = default) =>
        Act(window, info, element =>
        {
            if (element.Patterns.Invoke.TryGetPattern(out var invoke))
            {
                invoke.Invoke();
                return new ActionDone(info, Rung.UiAutomation, "invoke");
            }

            if (element.Patterns.Toggle.TryGetPattern(out var toggle))
            {
                toggle.Toggle();
                return new ActionDone(info, Rung.UiAutomation, "toggle", State(toggle.ToggleState.ValueOrDefault));
            }

            if (element.Patterns.ExpandCollapse.TryGetPattern(out var expand))
            {
                if (expand.ExpandCollapseState.ValueOrDefault == ExpandCollapseState.Collapsed)
                {
                    expand.Expand();
                    return new ActionDone(info, Rung.UiAutomation, "expand", "expanded");
                }

                expand.Collapse();
                return new ActionDone(info, Rung.UiAutomation, "collapse", "collapsed");
            }

            if (element.Patterns.SelectionItem.TryGetPattern(out var select))
            {
                select.Select();
                return new ActionDone(info, Rung.UiAutomation, "select");
            }

            if (element.Patterns.LegacyIAccessible.TryGetPattern(out var legacy) && !string.IsNullOrEmpty(legacy.DefaultAction.ValueOrDefault))
            {
                legacy.DoDefaultAction();
                return new ActionDone(info, Rung.UiAutomation, "default_action");
            }

            var button = MessageTarget(window, element, "button", ControlType.Button);
            if (button != 0 && WindowMessages.Click(button))
            {
                return new ActionDone(info, Rung.WindowMessage, "bm_click");
            }

            throw new NoWayToActException("press");
        }, ct);

    public Task<ActionDone> ToggleAsync(nint window, ElementInfo info, bool? on, CancellationToken ct = default) =>
        Act(window, info, element =>
        {
            if (!element.Patterns.Toggle.TryGetPattern(out var toggle))
            {
                throw new NoWayToActException("switch");
            }

            for (var i = 0; i < 3 && (on is null ? i == 0 : (toggle.ToggleState.ValueOrDefault == ToggleState.On) != on); i++)
            {
                toggle.Toggle();
            }

            return new ActionDone(info, Rung.UiAutomation, "toggle", State(toggle.ToggleState.ValueOrDefault));
        }, ct);

    public Task<ActionDone> SelectAsync(nint window, ElementInfo info, CancellationToken ct = default) =>
        Act(window, info, element =>
        {
            if (!element.Patterns.SelectionItem.TryGetPattern(out var select))
            {
                throw new NoWayToActException("select");
            }

            select.Select();
            return new ActionDone(info, Rung.UiAutomation, "select");
        }, ct);

    public Task<ActionDone> SetTextAsync(nint window, ElementInfo info, string text, CancellationToken ct = default) =>
        Act(window, info, element =>
        {
            if (element.Properties.IsPassword.ValueOrDefault)
            {
                throw new SecretFieldException();
            }

            if (element.Patterns.Value.TryGetPattern(out var value) && !value.IsReadOnly.ValueOrDefault)
            {
                value.SetValue(text);
                return new ActionDone(info, Rung.UiAutomation, "set_value");
            }

            var edit = MessageTarget(window, element, "edit", ControlType.Edit);
            if (edit != 0 && WindowMessages.SetText(edit, text))
            {
                return new ActionDone(info, Rung.WindowMessage, "wm_settext");
            }

            throw new NoWayToActException("type into");
        }, ct);

    public Task<ActionDone> ScrollAsync(nint window, ElementInfo info, string direction, bool page, CancellationToken ct = default) =>
        Act(window, info, element =>
        {
            for (var current = element; current is not null; current = current.Parent)
            {
                if (current.Patterns.Scroll.TryGetPattern(out var scroll))
                {
                    var forward = page ? ScrollAmount.LargeIncrement : ScrollAmount.SmallIncrement;
                    var back = page ? ScrollAmount.LargeDecrement : ScrollAmount.SmallDecrement;
                    var (h, v) = direction switch
                    {
                        "up" => (ScrollAmount.NoAmount, back),
                        "down" => (ScrollAmount.NoAmount, forward),
                        "left" => (back, ScrollAmount.NoAmount),
                        "right" => (forward, ScrollAmount.NoAmount),
                        _ => throw new DeskbridgeException("direction is up, down, left or right."),
                    };
                    scroll.Scroll(h, v);
                    return new ActionDone(info, Rung.UiAutomation, "scroll");
                }
            }

            throw new NoWayToActException("scroll");
        }, ct);

    public Task<IReadOnlyList<string>> SelectedAsync(nint window, string list, CancellationToken ct = default)
    {
        UiaGuards.Check(window);
        return Run<IReadOnlyList<string>>(automation =>
        {
            var element = Resolve(automation, window, list);
            return element.Patterns.Selection.TryGetPattern(out var selection)
                ? (selection.Selection.ValueOrDefault ?? []).Select(e => e.Properties.Name.ValueOrDefault ?? "").ToList()
                : [];
        }, ct);
    }

    async Task<ActionDone> Act(nint window, ElementInfo info, Func<AutomationElement, ActionDone> action, CancellationToken ct)
    {
        UiaGuards.Check(window);
        return await Run(automation =>
        {
            var element = Revalidate(automation, window, info);
            return action(element);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Reads the control again; anything different from what the user approved is refused.</summary>
    AutomationElement Revalidate(UIA3Automation automation, nint window, ElementInfo info)
    {
        if (!cache.TryGetValue(info.ElementId, out var entry))
        {
            throw new ElementUnknownException();
        }

        try
        {
            var element = entry.Element;
            var now = Info(window, element);
            if (now.Name != info.Name || now.ControlType != info.ControlType || now.AutomationId != info.AutomationId)
            {
                throw new ElementChangedException();
            }

            if (!now.Enabled)
            {
                throw new DeskbridgeException("That control is disabled right now.");
            }

            return element;
        }
        catch (COMException)
        {
            throw new ElementChangedException(); // the control is gone
        }
    }

    AutomationElement Resolve(UIA3Automation automation, nint window, string element)
    {
        if (ElementRef.TryParse(element, out var idWindow, out _))
        {
            if (idWindow != window || !cache.TryGetValue(element, out var entry) || DateTime.UtcNow - entry.At > CacheLife)
            {
                throw new ElementUnknownException();
            }

            return entry.Element;
        }

        var root = automation.FromHandle(window);
        var cf = root.ConditionFactory;
        return root.FindFirstDescendant(cf.ByAutomationId(element))
            ?? root.FindFirstDescendant(cf.ByName(element, PropertyConditionFlags.IgnoreCase))
            ?? throw new ControlNotFoundException(element);
    }

    ElementInfo Remember(nint window, AutomationElement element)
    {
        var info = Info(window, element);
        cache[info.ElementId] = (element, DateTime.UtcNow);
        foreach (var stale in cache.Where(kv => DateTime.UtcNow - kv.Value.At > CacheLife).Select(kv => kv.Key).ToList())
        {
            cache.TryRemove(stale, out _);
        }

        return info;
    }

    static ElementInfo Info(nint window, AutomationElement element) => new(
        ElementRef.Format(window, element.Properties.RuntimeId.ValueOrDefault ?? []),
        window,
        element.Properties.Name.ValueOrDefault,
        element.Properties.AutomationId.ValueOrDefault,
        element.Properties.ControlType.ValueOrDefault.ToString(),
        element.Properties.IsEnabled.ValueOrDefault);

    /// <summary>
    /// The control's own window, when a window message can stand in for a pattern: only for buttons (BM_CLICK)
    /// and edit boxes (WM_SETTEXT), recognised by window class or UI Automation type. Otherwise 0.
    /// </summary>
    static nint MessageTarget(nint window, AutomationElement element, string classWord, ControlType type)
    {
        var hwnd = element.Properties.NativeWindowHandle.ValueOrDefault;
        if (hwnd == 0 || hwnd == window)
        {
            return 0;
        }

        return WindowLister.ClassName(hwnd).Contains(classWord, StringComparison.OrdinalIgnoreCase)
            || element.Properties.ControlType.ValueOrDefault == type
            ? hwnd
            : 0;
    }

    /// <summary>What an action could do to this control, checked before anyone is asked to approve it.</summary>
    static IReadOnlyList<string> Capabilities(nint window, AutomationElement element)
    {
        var can = new List<string>();
        var buttonWindow = MessageTarget(window, element, "button", ControlType.Button) != 0;
        var editWindow = MessageTarget(window, element, "edit", ControlType.Edit) != 0;
        var toggle = element.Patterns.Toggle.IsSupported;
        var select = element.Patterns.SelectionItem.IsSupported;
        var defaultAction = element.Patterns.LegacyIAccessible.TryGetPattern(out var legacy) && !string.IsNullOrEmpty(legacy.DefaultAction.ValueOrDefault);
        if (element.Patterns.Invoke.IsSupported || toggle || element.Patterns.ExpandCollapse.IsSupported || select || defaultAction || buttonWindow)
        {
            can.Add("press");
        }

        if (toggle)
        {
            can.Add("toggle");
        }

        if (select)
        {
            can.Add("select");
        }

        if ((element.Patterns.Value.TryGetPattern(out var value) && !value.IsReadOnly.ValueOrDefault) || editWindow)
        {
            can.Add("set_text");
        }

        return can;
    }

    static IReadOnlyList<string> Patterns(AutomationElement element)
    {
        var patterns = new List<string>();
        if (element.Patterns.Invoke.IsSupported) patterns.Add("invoke");
        if (element.Patterns.Toggle.IsSupported) patterns.Add("toggle");
        if (element.Patterns.Value.IsSupported) patterns.Add("value");
        if (element.Patterns.SelectionItem.IsSupported) patterns.Add("select");
        if (element.Patterns.ExpandCollapse.IsSupported) patterns.Add("expand");
        if (element.Patterns.Scroll.IsSupported) patterns.Add("scroll");
        return patterns;
    }

    internal static string State(ToggleState state) => state switch
    {
        ToggleState.On => "on",
        ToggleState.Off => "off",
        _ => "mixed",
    };

    Task<T> Run<T>(Func<UIA3Automation, T> work, CancellationToken ct) => worker.RunAsync(work, ct);
}
