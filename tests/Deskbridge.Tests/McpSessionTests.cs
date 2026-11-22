using System.Text.Json;

namespace Deskbridge.Tests;

public class McpSessionTests
{
    static readonly Dictionary<string, object?> NoArgs = new();

    public static readonly string[] CoreTools =
    [
        "apps_launch", "apps_list_installed", "apps_list_running",
        "clipboard_read", "clipboard_write",
        "files_delete", "files_move", "files_read", "files_search", "files_write",
        "media_control", "media_now_playing", "notify_show", "settings_open",
        "ui_find", "ui_press", "ui_read", "ui_scroll", "ui_select", "ui_set_text", "ui_toggle", "ui_tree",
        "volume_set", "windows_describe", "windows_list",
    ];

    [Fact]
    public async Task Lists_the_core_tools()
    {
        await using var h = await SessionHarness.StartAsync();
        var names = (await h.Client.ListToolsAsync()).Select(t => t.Name).Order().ToArray();
        Assert.Equal(CoreTools.Order(), names);
    }

    [Fact]
    public async Task Each_tool_declares_its_effect()
    {
        await using var h = await SessionHarness.StartAsync();
        var tools = (await h.Client.ListToolsAsync()).ToDictionary(t => t.Name, t => t.ProtocolTool.Annotations);
        Assert.True(tools["files_read"]?.ReadOnlyHint);
        Assert.False(tools["files_write"]?.ReadOnlyHint);
        Assert.True(tools["files_delete"]?.DestructiveHint);
    }

    [Fact]
    public async Task Tool_names_follow_the_naming_rule()
    {
        await using var h = await SessionHarness.StartAsync();
        foreach (var t in await h.Client.ListToolsAsync())
        {
            Assert.Matches("^[a-z][a-z0-9_]*$", t.Name);
        }
    }

    [Theory]
    [InlineData("2025-11-25")]
    [InlineData("2026-07-28")]
    public async Task Old_and_new_protocol_versions_both_work(string version)
    {
        await using var h = await SessionHarness.StartAsync(protocolVersion: version);
        Assert.Equal("deskbridge", h.Client.ServerInfo?.Name);
        Assert.Equal(version, h.Client.NegotiatedProtocolVersion);
        var result = await h.Client.CallToolAsync("windows_list", NoArgs);
        Assert.NotEqual(true, result.IsError);
    }

    [Fact]
    public async Task Windows_list_uses_snake_case_fields()
    {
        await using var h = await SessionHarness.StartAsync();
        var json = SessionHarness.Text(await h.Client.CallToolAsync("windows_list", NoArgs));
        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.GetProperty("windows")[0];
        foreach (var field in new[] { "handle", "title", "process", "pid" })
        {
            Assert.True(first.TryGetProperty(field, out _), field);
        }
    }

    [Fact]
    public async Task Ui_read_error_is_a_plain_tool_error()
    {
        await using var h = await SessionHarness.StartAsync();
        var result = await h.Client.CallToolAsync("ui_read", new Dictionary<string, object?> { ["window"] = "0x1" });
        Assert.True(result.IsError);
        Assert.Equal("That window is gone. List windows again.", SessionHarness.Text(result));
    }

    [Fact]
    public async Task Bad_handle_text_is_explained()
    {
        await using var h = await SessionHarness.StartAsync();
        var result = await h.Client.CallToolAsync("ui_read", new Dictionary<string, object?> { ["window"] = "notepad" });
        Assert.True(result.IsError);
        Assert.Contains("not a window handle", SessionHarness.Text(result));
    }

    [Fact]
    public async Task Off_app_is_refused_with_its_reason()
    {
        var services = TestServices.RealPolicy(out var config);
        config.Update(c => c.WithLevel(Deskbridge.Core.Apps.AppId.Windows, Deskbridge.Core.Policy.PermissionLevel.Off));
        await using var h = await SessionHarness.StartAsync(services: services);
        var result = await h.Client.CallToolAsync("windows_list", NoArgs);
        Assert.True(result.IsError);
        Assert.Equal("Deskbridge is off for Windows and apps list. Change it in Deskbridge settings.", SessionHarness.Text(result));
    }

    [Fact]
    public async Task Every_call_writes_one_audit_line()
    {
        var audit = new TestServices.MemoryAudit();
        await using var h = await SessionHarness.StartAsync(services: TestServices.RealPolicy(out _, audit));
        await h.Client.CallToolAsync("windows_list", NoArgs);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(("windows_list", "allowed", "ok", "tests"), (entry.Tool, entry.Decision, entry.Result, entry.Client));
    }


}
