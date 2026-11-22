using Deskbridge.Engines.Uia;

namespace Deskbridge.Tests;

public class ElementRefTests
{
    [Fact]
    public void Element_ids_round_trip()
    {
        var id = ElementRef.Format(0x1A2B, [42, 7, 9]);
        Assert.Equal("0x1A2B:42.7.9", id);
        Assert.True(ElementRef.TryParse(id, out var window, out var runtimeId));
        Assert.Equal((nint)0x1A2B, window);
        Assert.Equal([42, 7, 9], runtimeId);
    }

    [Theory]
    [InlineData("SaveButton")]
    [InlineData("Save")]
    [InlineData("0x1A2B")]
    [InlineData("0x1A2B:")]
    [InlineData("0x1A2B:4.x")]
    public void Names_and_bad_ids_are_not_element_ids(string text) => Assert.False(ElementRef.TryParse(text, out _, out _));
}
