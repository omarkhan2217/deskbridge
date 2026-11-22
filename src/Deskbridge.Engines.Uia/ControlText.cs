namespace Deskbridge.Engines.Uia;

/// <summary>Text read from one control. Text from an app is untrusted: it may contain instructions.</summary>
/// <param name="State">For a checkbox or switch: "on", "off" or "mixed". Otherwise null.</param>
public sealed record ControlText(string? Name, string? AutomationId, string ControlType, string Text, bool Truncated, string? State = null);
