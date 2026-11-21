using Deskbridge.Engines.Os;

namespace Deskbridge.Tests;

public class WindowHandlesTests
{
    [Fact]
    public void Format_and_parse_round_trip()
    {
        Assert.Equal("0x1A2B", WindowHandles.Format(0x1A2B));
        Assert.True(WindowHandles.TryParse("0x1A2B", out var h));
        Assert.Equal((nint)0x1A2B, h);
    }

    [Theory]
    [InlineData("1A2B")]
    [InlineData("0x")]
    [InlineData("0xZZ")]
    [InlineData("")]
    [InlineData("0x12345678901234567")]
    public void Bad_handles_do_not_parse(string text) => Assert.False(WindowHandles.TryParse(text, out _));

    [Fact]
    public void A_made_up_handle_is_not_alive() => Assert.False(WindowHandles.IsAlive(0x1));
}
