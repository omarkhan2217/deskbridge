namespace Deskbridge.Engines.Uia;

/// <summary>
/// Chromium and Electron windows switch into a slower accessibility mode as soon as UI Automation
/// walks them. The class name comes from the window manager, so checking it costs the app nothing.
/// </summary>
public static class ChromiumGuard
{
    public static bool IsChromium(string windowClass) =>
        windowClass is "Chrome_WidgetWin_0" or "Chrome_WidgetWin_1";
}
