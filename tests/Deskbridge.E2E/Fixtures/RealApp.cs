using System.Diagnostics;
using Deskbridge.E2E.HandsOff;
using Deskbridge.Engines.Os;

namespace Deskbridge.E2E.Fixtures;

/// <summary>
/// Starts a real Windows app (Notepad, Calculator) and finds its window by title through WindowLister,
/// because these apps start through app aliases and the window belongs to another process.
/// Refuses to run if the app is already open, so a test never closes the user's own windows.
/// </summary>
public sealed class RealApp : IDisposable
{
    const int SwMinimize = 6;
    const uint WmClose = 0x0010;

    readonly string processName;
    readonly DateTime startedAt;

    RealApp(nint handle, string processName, DateTime startedAt)
    {
        Handle = handle;
        this.processName = processName;
        this.startedAt = startedAt;
    }

    public nint Handle { get; }

    public static RealApp Start(string exe, string arguments, string titleContains, string processName)
    {
        if (Process.GetProcessesByName(processName).Length > 0)
        {
            throw new InvalidOperationException(
                $"{processName} is already open. Close it before running this test, so the test never touches your own window.");
        }

        var startedAt = DateTime.Now.AddSeconds(-1);
        Process.Start(new ProcessStartInfo(exe, arguments) { UseShellExecute = true })?.Dispose();
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var window = WindowLister.List().FirstOrDefault(w => w.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase));
            if (window is not null)
            {
                return new RealApp(window.Handle, processName, startedAt);
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"No window with '{titleContains}' in its title appeared within 15 s.");
    }

    /// <summary>Minimizes and lets Windows activate the next window (test setup), so the app is not in front when the test reads it.</summary>
    public void MinimizeAndActivateNext()
    {
        NativeTest.ShowWindow(Handle, SwMinimize);
        Thread.Sleep(500);
    }

    public bool IsForeground => NativeTest.GetForegroundWindow() == Handle;

    /// <summary>
    /// Asks the window to close, then ends any copy of the app this test started (Store apps can stay
    /// alive and hidden after WM_CLOSE). Start refused to run if the app was already open, so every
    /// process ended here was started by the test.
    /// </summary>
    public void Dispose()
    {
        NativeTest.PostMessageW(Handle, WmClose, 0, 0);
        Thread.Sleep(500);
        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    if (process.StartTime >= startedAt && !process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(5_000);
                    }
                }
                catch (InvalidOperationException)
                {
                    // already gone
                }
            }
        }
    }
}
