using System.Runtime.InteropServices;
using Deskbridge.Core;
using Deskbridge.Engines.Os.Native;

namespace Deskbridge.Engines.Os.Apps;

/// <summary>Runs one piece of Shell work on its own STA thread, which Shell COM objects need.</summary>
public static class StaThread
{
    public static Task<T> RunAsync<T>(Func<T> work)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                result.SetResult(work());
            }
            catch (Exception ex)
            {
                result.SetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Deskbridge Shell",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return result.Task;
    }
}

public sealed record InstalledApp(string Name, string AppId);

/// <summary>The apps in the Start menu's "All apps" list, from the shell:AppsFolder namespace.</summary>
public static class AppsFolder
{
    public static Task<IReadOnlyList<InstalledApp>> ListAsync() => StaThread.RunAsync<IReadOnlyList<InstalledApp>>(() =>
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application")
            ?? throw new DeskbridgeException("The Windows Shell is not available.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            var apps = new List<InstalledApp>();
            foreach (var item in shell.NameSpace("shell:AppsFolder").Items())
            {
                apps.Add(new InstalledApp((string)item.Name, (string)item.Path));
            }

            return apps.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    });
}

/// <summary>
/// Whether a launched app came to the front, what is in front now, and the new window if one appeared
/// (its handle works in every window tool).
/// </summary>
public sealed record LaunchResult(string Target, bool TookFocus, string? ForegroundTitle, string? NewWindow, string? NewWindowTitle);

/// <summary>
/// Starts an app (an app ID from apps_list_installed, an exe path, or an ms-settings: page) without
/// activating it. Some apps come to the front anyway, so the result says whether that happened (D39).
/// </summary>
public static class Launcher
{
    static readonly TimeSpan WaitForWindow = TimeSpan.FromSeconds(8);
    static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(500);

    public static async Task<LaunchResult> LaunchAsync(string target, CancellationToken ct)
    {
        var file = Resolve(target);
        var before = User32.GetForegroundWindow();
        var existing = WindowLister.List().Select(w => w.Handle).ToHashSet();
        await StaThread.RunAsync(() =>
        {
            Execute(file);
            return 0;
        }).ConfigureAwait(false);

        // A launch is a moment the user asked for; watching for its window briefly is not idle polling.
        // Stop at the first new window or the first change of foreground, then let activation settle.
        WindowInfo? fresh = null;
        var deadline = DateTime.UtcNow + WaitForWindow;
        while (DateTime.UtcNow < deadline && fresh is null && User32.GetForegroundWindow() == before)
        {
            await Task.Delay(150, ct).ConfigureAwait(false);
            fresh = WindowLister.List().FirstOrDefault(w => !existing.Contains(w.Handle));
        }

        await Task.Delay(Settle, ct).ConfigureAwait(false);
        fresh ??= WindowLister.List().FirstOrDefault(w => !existing.Contains(w.Handle));
        var now = User32.GetForegroundWindow();
        return new LaunchResult(
            target,
            now != before,
            now != before ? WindowLister.Title(now) : null,
            fresh is null ? null : WindowHandles.Format(fresh.Handle),
            fresh?.Title);
    }

    static string Resolve(string target)
    {
        if (target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase))
        {
            return target;
        }

        if (Path.IsPathFullyQualified(target) && File.Exists(target) && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return target;
        }

        if (!string.IsNullOrWhiteSpace(target) && !target.Contains('"') && !Path.IsPathFullyQualified(target))
        {
            return @"shell:AppsFolder\" + target; // any app ID from apps_list_installed; the Shell checks it
        }

        throw new DeskbridgeException("Name an app ID from apps_list_installed, or the full path of an .exe that exists.");
    }

    static unsafe void Execute(string file)
    {
        fixed (char* verb = "open")
        fixed (char* path = file)
        {
            var info = new ShellExecuteInfo
            {
                cbSize = (uint)sizeof(ShellExecuteInfo),
                fMask = SeeMaskNoAsync | SeeMaskFlagNoUi,
                lpVerb = verb,
                lpFile = path,
                nShow = SwShowNoActivate,
            };
            if (!ShellExecuteExW(&info))
            {
                throw new DeskbridgeException($"Windows could not start {file} (error {Marshal.GetLastPInvokeError()}).");
            }
        }
    }

    const uint SeeMaskNoAsync = 0x00000100;
    const uint SeeMaskFlagNoUi = 0x00000400;
    const int SwShowNoActivate = 4;

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ShellExecuteInfo
    {
        public uint cbSize;
        public uint fMask;
        public nint hwnd;
        public char* lpVerb;
        public char* lpFile;
        public char* lpParameters;
        public char* lpDirectory;
        public int nShow;
        public nint hInstApp;
        public nint lpIDList;
        public char* lpClass;
        public nint hkeyClass;
        public uint dwHotKey;
        public nint hIconOrMonitor;
        public nint hProcess;
    }

    [DllImport("shell32.dll", SetLastError = true)]
    static extern unsafe bool ShellExecuteExW(ShellExecuteInfo* info);
}
