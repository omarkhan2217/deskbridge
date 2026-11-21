using Deskbridge.Engines.Uia;

namespace Deskbridge.Tests;

public class ChromiumGuardTests
{
    [Theory]
    [InlineData("Chrome_WidgetWin_1", true)]
    [InlineData("Chrome_WidgetWin_0", true)]
    [InlineData("Notepad", false)]
    [InlineData("WindowsForms10.Window.8.app.0.1", false)]
    public void Recognises_chromium_windows(string windowClass, bool expected) =>
        Assert.Equal(expected, ChromiumGuard.IsChromium(windowClass));
}
