using Deskbridge.Core;
using Deskbridge.Mcp;
using ModelContextProtocol.Protocol;

namespace Deskbridge.Tests;

public class ToolJsonTests
{
    static string Text(CallToolResult r) => string.Concat(r.Content.OfType<TextContentBlock>().Select(t => t.Text));

    [Fact]
    public async Task Deskbridge_errors_reach_the_agent_word_for_word()
    {
        var result = await ToolJson.RunAsync(() => throw new DeskbridgeException("That window is gone."));
        Assert.True(result.IsError);
        Assert.Equal("That window is gone.", Text(result));
    }

    [Fact]
    public async Task Unexpected_errors_become_plain_tool_errors_that_say_what_happened()
    {
        var result = await ToolJson.RunAsync(() => throw new InvalidOperationException("the list changed while it was read"));
        Assert.True(result.IsError);
        Assert.Contains("Deskbridge hit an unexpected problem", Text(result));
        Assert.Contains("the list changed while it was read", Text(result));
    }
}
