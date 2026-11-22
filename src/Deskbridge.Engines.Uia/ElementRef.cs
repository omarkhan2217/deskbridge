using System.Globalization;
using Deskbridge.Core.Tools;
using Deskbridge.Engines.Os;

namespace Deskbridge.Engines.Uia;

/// <summary>An element ID agents pass between calls: "&lt;window&gt;:&lt;runtime id&gt;", like 0x1A2B:42.7.9.</summary>
public static class ElementRef
{
    public static string Format(nint window, int[] runtimeId) =>
        WindowHandles.Format(window) + ":" + string.Join(".", runtimeId.Select(i => i.ToString(CultureInfo.InvariantCulture)));

    public static bool TryParse(string text, out nint window, out int[] runtimeId)
    {
        window = 0;
        runtimeId = [];
        var colon = text.IndexOf(':');
        if (colon <= 0 || colon == text.Length - 1 || !WindowHandles.TryParse(text[..colon], out window))
        {
            return false;
        }

        var parts = text[(colon + 1)..].Split('.');
        var ids = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out ids[i]))
            {
                window = 0;
                return false;
            }
        }

        runtimeId = ids;
        return true;
    }
}

/// <summary>A control as found: what the user is asked about, and what is checked again before acting.</summary>
public sealed record ElementInfo(string ElementId, nint Window, string? Name, string? AutomationId, string ControlType, bool Enabled)
{
    /// <summary>What can be done to it without the mouse: press, toggle, select, set_text. Filled in by DescribeAsync.</summary>
    public IReadOnlyList<string> Can { get; init; } = [];

    /// <summary>For a checkbox or switch, whether it was on when found. Otherwise null. Filled in by DescribeAsync.</summary>
    public bool? IsOn { get; init; }
}

/// <summary>What an action did and which rung did it.</summary>
public sealed record ActionDone(ElementInfo Element, Rung Rung, string How, string? State = null);

/// <summary>One control in a tree listing. Parent is the element_id of the control that holds it, or null at the top.</summary>
public sealed record UiNode(string ElementId, string? Parent, int Depth, string ControlType, string? Name, string? AutomationId, bool Enabled, IReadOnlyList<string> Patterns);

public sealed record UiTree(IReadOnlyList<UiNode> Nodes, bool Truncated)
{
    public int Count => Nodes.Count;
}
