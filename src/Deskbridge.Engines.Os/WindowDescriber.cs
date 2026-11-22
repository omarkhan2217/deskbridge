using System.Diagnostics;
using Deskbridge.Core;
using Deskbridge.Engines.Os.Native;

namespace Deskbridge.Engines.Os;

public sealed record Bounds(int X, int Y, int Width, int Height);

public sealed record WindowDescription(
    string Handle,
    string Title,
    string Process,
    int Pid,
    string? Path,
    string ClassName,
    Bounds Bounds,
    string State,
    bool Foreground,
    bool Elevated,
    bool StoreApp,
    bool Chromium);

/// <summary>Facts about one window from the window manager only: no UI Automation, so it costs the app nothing.</summary>
public static class WindowDescriber
{
    public static WindowDescription Describe(nint hwnd)
    {
        if (!WindowHandles.IsAlive(hwnd))
        {
            throw new DeskbridgeException("That window is gone. List windows again.");
        }

        var className = WindowLister.ClassName(hwnd);
        var pid = AppResolver.ProcessOf(hwnd);
        User32.GetWindowRect(hwnd, out var rect);
        var state = User32.IsIconic(hwnd) ? "minimized" : User32.IsZoomed(hwnd) ? "maximized" : "normal";
        return new WindowDescription(
            WindowHandles.Format(hwnd),
            WindowLister.Title(hwnd),
            ProcessName(pid),
            pid,
            ProcessFacts.ImagePath(pid),
            className,
            new Bounds(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top),
            state,
            User32.GetForegroundWindow() == hwnd,
            ProcessFacts.IsElevated(pid),
            className == "ApplicationFrameWindow",
            className.StartsWith("Chrome_WidgetWin_", StringComparison.Ordinal));
    }

    static string ProcessName(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return "";
        }
        catch (InvalidOperationException)
        {
            return "";
        }
    }
}
