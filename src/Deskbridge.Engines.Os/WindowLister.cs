using System.Diagnostics;
using System.Runtime.InteropServices;
using Deskbridge.Engines.Os.Native;

namespace Deskbridge.Engines.Os;

/// <summary>
/// Lists the top-level windows a user would see in the taskbar or Alt+Tab, in Z-order.
/// Uses only window-manager calls, never UI Automation, so it costs nothing in the target apps.
/// </summary>
public static unsafe class WindowLister
{
    public static IReadOnlyList<WindowInfo> List()
    {
        var handles = new List<nint>();
        var gc = GCHandle.Alloc(handles);
        try
        {
            User32.EnumWindows(&Collect, GCHandle.ToIntPtr(gc));
        }
        finally
        {
            gc.Free();
        }

        var processNames = new Dictionary<int, string>();
        var windows = new List<WindowInfo>();
        foreach (var hwnd in handles)
        {
            if (!IsUserWindow(hwnd, out var title))
            {
                continue;
            }

            User32.GetWindowThreadProcessId(hwnd, out var pid);
            var processId = (int)pid;
            if (!processNames.TryGetValue(processId, out var processName))
            {
                processName = ProcessName(processId);
                processNames[processId] = processName;
            }

            windows.Add(new WindowInfo(hwnd, title, processName, processId, ClassName(hwnd)));
        }

        return windows;
    }

    public static string ClassName(nint hwnd)
    {
        var buffer = stackalloc char[256];
        var length = User32.GetClassName(hwnd, buffer, 256);
        return length > 0 ? new string(buffer, 0, length) : "";
    }

    [UnmanagedCallersOnly]
    static int Collect(nint hwnd, nint state)
    {
        ((List<nint>)GCHandle.FromIntPtr(state).Target!).Add(hwnd);
        return 1;
    }

    static bool IsUserWindow(nint hwnd, out string title)
    {
        title = "";
        if (!User32.IsWindowVisible(hwnd))
        {
            return false;
        }

        if (User32.DwmGetWindowAttribute(hwnd, User32.DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
        {
            return false;
        }

        var exStyle = (long)User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE);
        if ((exStyle & User32.WS_EX_TOOLWINDOW) != 0 && (exStyle & User32.WS_EX_APPWINDOW) == 0)
        {
            return false;
        }

        title = Title(hwnd);
        return title.Length > 0;
    }

    public static string Title(nint hwnd)
    {
        var length = User32.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return "";
        }

        var buffer = new char[length + 1];
        fixed (char* p = buffer)
        {
            var copied = User32.GetWindowText(hwnd, p, buffer.Length);
            return new string(p, 0, copied);
        }
    }

    static string ProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return ""; // the process exited while we looked
        }
        catch (InvalidOperationException)
        {
            return "";
        }
    }
}
