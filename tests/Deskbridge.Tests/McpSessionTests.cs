using System.Text.Json;

namespace Deskbridge.Tests;

public class McpSessionTests
{
    static readonly Dictionary<string, object?> NoArgs = new();

    [Fact]
    public async Task Lists_exactly_the_phase_0_tools()
    {
        await using var h = await SessionHarness.StartAsync();
        var names = (await h.Client.ListToolsAsync()).Select(t => t.Name).Order().ToArray();
        Assert.Equal(new[] { "ui_read", "windows_list" }, names);
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
    public async Task Tools_say_they_are_read_only()
    {
        await using var h = await SessionHarness.StartAsync();
        foreach (var t in await h.Client.ListToolsAsync())
        {
            Assert.True(t.ProtocolTool.Annotations?.ReadOnlyHint, t.Name);
        }
    }
}
