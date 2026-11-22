using System.Text.Json.Serialization;
using Deskbridge.Core.Apps;

namespace Deskbridge.Core.Tools;

/// <summary>What a tool call does to the user's data. Decides whether the user is asked.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ToolEffect>))]
public enum ToolEffect
{
    [JsonStringEnumMemberName("read")]
    Read,

    [JsonStringEnumMemberName("change")]
    Change,

    [JsonStringEnumMemberName("destructive")]
    Destructive,
}

/// <summary>Which rung of the no-cursor ladder did the work.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Rung>))]
public enum Rung
{
    [JsonStringEnumMemberName("app_interface")]
    AppInterface,

    [JsonStringEnumMemberName("windows_api")]
    WindowsApi,

    [JsonStringEnumMemberName("ui_automation")]
    UiAutomation,

    [JsonStringEnumMemberName("window_message")]
    WindowMessage,

    [JsonStringEnumMemberName("foreground")]
    Foreground,
}

/// <summary>
/// One tool call as the policy and the user see it. <paramref name="Action"/> is the one line a person
/// reads ("Rename notes.txt to plan.txt"); <paramref name="Content"/> is the exact text being written or sent.
/// <paramref name="TargetElevated"/> is set when the target window runs as administrator.
/// </summary>
public sealed record ToolCall(string Tool, AppId App, ToolEffect Effect, string Action, string? Content = null, bool TargetElevated = false);

/// <summary>A tool's result together with the rung that produced it.</summary>
public sealed record Executed<T>(T Value, Rung Rung);
