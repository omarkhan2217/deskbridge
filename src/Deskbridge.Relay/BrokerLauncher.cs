using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace Deskbridge.Relay;

/// <summary>
/// Connects to the user's broker, starting it if it is not running.
/// The broker is started without inheriting any handle, so it never holds the client's stdio open,
/// and outside the client's job object where Windows allows, so closing the client does not kill it.
/// </summary>
internal static class BrokerLauncher
{
    static readonly TimeSpan FirstTry = TimeSpan.FromMilliseconds(300);
    static readonly TimeSpan RetryStep = TimeSpan.FromMilliseconds(250);

    public static async Task<NamedPipeClientStream> ConnectOrStartAsync(string pipeName, string brokerPath, TimeSpan timeout, CancellationToken ct)
    {
        var pipe = NewPipe(pipeName);
        if (await TryConnectAsync(pipe, FirstTry, ct).ConfigureAwait(false))
        {
            return pipe;
        }

        if (!File.Exists(brokerPath))
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw new FileNotFoundException($"The broker is not at {brokerPath}.");
        }

        var broker = Start(brokerPath);
        var restarted = false;
        var deadline = DateTime.UtcNow + timeout;
        try
        {
            while (DateTime.UtcNow < deadline)
            {
                if (await TryConnectAsync(pipe, RetryStep, ct).ConfigureAwait(false))
                {
                    return pipe;
                }

                // A broker that was shutting down may still have held the single-instance lock, so the one
                // we started exited at once. Start one more.
                if (!restarted && HasExited(broker))
                {
                    Native.CloseHandle(broker);
                    broker = Start(brokerPath);
                    restarted = true;
                }
            }
        }
        finally
        {
            Native.CloseHandle(broker);
        }

        await pipe.DisposeAsync().ConfigureAwait(false);
        throw new TimeoutException($"The broker did not answer within {timeout.TotalSeconds:0} seconds.");
    }

    static NamedPipeClientStream NewPipe(string pipeName) =>
        new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    static async Task<bool> TryConnectAsync(NamedPipeClientStream pipe, TimeSpan wait, CancellationToken ct)
    {
        try
        {
            await pipe.ConnectAsync(wait, ct).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    static bool HasExited(nint process) => Native.WaitForSingleObject(process, 0) == Native.WAIT_OBJECT_0;

    /// <returns>The broker's process handle; the caller closes it.</returns>
    static nint Start(string brokerPath)
    {
        var flags = Native.DETACHED_PROCESS | Native.CREATE_UNICODE_ENVIRONMENT | Native.CREATE_BREAKAWAY_FROM_JOB;
        if (TryCreate(brokerPath, flags, out var process))
        {
            return process;
        }

        // Some job objects forbid breakaway; then the broker lives inside the client's job.
        if (Marshal.GetLastPInvokeError() == Native.ERROR_ACCESS_DENIED
            && TryCreate(brokerPath, flags & ~Native.CREATE_BREAKAWAY_FROM_JOB, out process))
        {
            return process;
        }

        throw new InvalidOperationException($"Windows refused to start the broker (error {Marshal.GetLastPInvokeError()}).");
    }

    static unsafe bool TryCreate(string brokerPath, uint flags, out nint process)
    {
        var commandLine = ("\"" + brokerPath + "\"\0").ToCharArray();
        var directory = (Path.GetDirectoryName(brokerPath) + "\0").ToCharArray();
        var startup = new Native.StartupInfo { cb = (uint)Marshal.SizeOf<Native.StartupInfo>() };
        fixed (char* app = brokerPath + "\0")
        fixed (char* cmd = commandLine)
        fixed (char* dir = directory)
        {
            if (!Native.CreateProcess(app, cmd, 0, 0, inheritHandles: false, flags, 0, dir, ref startup, out var info))
            {
                process = 0;
                return false;
            }

            Native.CloseHandle(info.hThread);
            process = info.hProcess;
            return true;
        }
    }
}
