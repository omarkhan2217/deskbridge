using System.Runtime.InteropServices;

namespace Deskbridge.Engines.Os;

/// <summary>
/// The window-message rung: messages sent to one control's own window, for older Win32 apps whose
/// controls offer no UI Automation pattern. Sent with a timeout, so a hung app cannot hang Deskbridge.
/// </summary>
public static class WindowMessages
{
    const uint BmClick = 0x00F5;
    const uint WmSetText = 0x000C;
    const uint SmtoAbortIfHung = 0x0002;
    const uint TimeoutMs = 2_000;

    public static bool Click(nint control) =>
        SendMessageTimeoutW(control, BmClick, 0, 0, SmtoAbortIfHung, TimeoutMs, out _) != 0;

    public static bool SetText(nint control, string text) =>
        SendMessageTimeoutW(control, WmSetText, 0, text, SmtoAbortIfHung, TimeoutMs, out _) != 0;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern nint SendMessageTimeoutW(nint hwnd, uint msg, nint wParam, nint lParam, uint flags, uint timeout, out nint result);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern nint SendMessageTimeoutW(nint hwnd, uint msg, nint wParam, string lParam, uint flags, uint timeout, out nint result);
}
