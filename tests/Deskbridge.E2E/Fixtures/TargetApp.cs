using System.Diagnostics;
using Deskbridge.E2E.HandsOff;

namespace Deskbridge.E2E.Fixtures;

/// <summary>Starts Deskbridge.E2E.Target and finds its window. Kills it on dispose.</summary>
public sealed class TargetApp : IDisposable
{
    readonly Process process;

    TargetApp(Process process, nint handle)
    {
        this.process = process;
        Handle = handle;
    }

    public nint Handle { get; }

    public int ProcessId => process.Id;

    public static TargetApp Start(string title, params string[] args)
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "Deskbridge.E2E.Target.exe");
        var info = new ProcessStartInfo(exe) { UseShellExecute = false };
        info.ArgumentList.Add("--title");
        info.ArgumentList.Add(title);
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        var process = Process.Start(info) ?? throw new InvalidOperationException("The target app did not start.");
        var hidden = args.Contains("--hidden");
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            // The window exists before WinForms shows it; wait until it is visible unless asked for a hidden one.
            var handle = NativeTest.FindWindowW(null, title);
            if (handle != 0 && (hidden || NativeTest.IsWindowVisible(handle)))
            {
                return new TargetApp(process, handle);
            }

            Thread.Sleep(50);
        }

        process.Kill(entireProcessTree: true);
        throw new TimeoutException($"The target window '{title}' did not appear within 5 s.");
    }

    public void Kill()
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5_000);
        }
    }

    public void Dispose()
    {
        Kill();
        process.Dispose();
    }
}
