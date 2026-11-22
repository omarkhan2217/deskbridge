using System.Diagnostics;
using System.Runtime.InteropServices;
using Deskbridge.Core.Apps;
using Deskbridge.Engines.Os.Native;

namespace Deskbridge.Engines.Os;

/// <summary>
/// Works out which app a window or process belongs to, so permissions attach to the app.
/// A Store app's window is a frame owned by ApplicationFrameHost; its real app is the process of the
/// CoreWindow inside the frame.
/// </summary>
public static unsafe class AppResolver
{
    const string FrameClass = "ApplicationFrameWindow";
    const string CoreWindowClass = "Windows.UI.Core.CoreWindow";

    public static AppId FromProcess(int pid)
    {
        var path = ProcessFacts.ImagePath(pid);
        if (path is not null)
        {
            return FromPath(path);
        }

        var name = ProcessName(pid);
        return new AppId("process:" + name.ToLowerInvariant(), name);
    }

    public static AppId FromPath(string exePath) => AppId.ForExe(exePath, DisplayName(exePath));

    public static AppId FromWindow(nint hwnd)
    {
        if (WindowLister.ClassName(hwnd) == FrameClass)
        {
            var corePid = CoreWindowProcess(hwnd);
            if (corePid is not null)
            {
                return FromProcess(corePid.Value);
            }

            // A minimized Store app detaches its CoreWindow; name it by its title until it comes back.
            var title = WindowLister.Title(hwnd);
            return new AppId("store:" + title.ToLowerInvariant(), title);
        }

        User32.GetWindowThreadProcessId(hwnd, out var pid);
        return FromProcess((int)pid);
    }

    /// <summary>The process that really owns a window: for a Store app's frame, the app inside it.</summary>
    public static int ProcessOf(nint hwnd)
    {
        if (WindowLister.ClassName(hwnd) == FrameClass && CoreWindowProcess(hwnd) is { } corePid)
        {
            return corePid;
        }

        User32.GetWindowThreadProcessId(hwnd, out var pid);
        return (int)pid;
    }

    static int? CoreWindowProcess(nint frame)
    {
        var found = new FrameSearch();
        var gc = GCHandle.Alloc(found);
        try
        {
            User32.EnumChildWindows(frame, &Visit, GCHandle.ToIntPtr(gc));
        }
        finally
        {
            gc.Free();
        }

        return found.ProcessId;
    }

    [UnmanagedCallersOnly]
    static int Visit(nint child, nint state)
    {
        if (WindowLister.ClassName(child) != CoreWindowClass)
        {
            return 1;
        }

        User32.GetWindowThreadProcessId(child, out var pid);
        ((FrameSearch)GCHandle.FromIntPtr(state).Target!).ProcessId = (int)pid;
        return 0;
    }

    static string DisplayName(string exePath)
    {
        try
        {
            var description = FileVersionInfo.GetVersionInfo(exePath).FileDescription;
            if (!string.IsNullOrWhiteSpace(description))
            {
                return description.Trim();
            }
        }
        catch (FileNotFoundException)
        {
        }

        return Path.GetFileNameWithoutExtension(exePath);
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
            return "unknown";
        }
        catch (InvalidOperationException)
        {
            return "unknown";
        }
    }

    sealed class FrameSearch
    {
        public int? ProcessId { get; set; }
    }
}
