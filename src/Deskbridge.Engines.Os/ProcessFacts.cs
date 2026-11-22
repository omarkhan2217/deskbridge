using System.Runtime.InteropServices;
using Deskbridge.Engines.Os.Native;

namespace Deskbridge.Engines.Os;

/// <summary>Facts about a process that permissions need: its exe path and whether it runs as administrator.</summary>
public static unsafe class ProcessFacts
{
    public static string? ImagePath(int pid)
    {
        if (pid <= 0)
        {
            return null;
        }

        var process = Kernel32.OpenProcess(Kernel32.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (process == 0)
        {
            return null;
        }

        try
        {
            var buffer = stackalloc char[1024];
            uint size = 1024;
            return Kernel32.QueryFullProcessImageName(process, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : null;
        }
        finally
        {
            Kernel32.CloseHandle(process);
        }
    }

    /// <summary>
    /// True when the process runs elevated. A process Windows will not even let us query counts as
    /// elevated too: Deskbridge cannot act on it either way.
    /// </summary>
    public static bool IsElevated(int pid)
    {
        if (pid <= 0)
        {
            return false;
        }

        var process = Kernel32.OpenProcess(Kernel32.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (process == 0)
        {
            return Marshal.GetLastPInvokeError() == Kernel32.ERROR_ACCESS_DENIED;
        }

        try
        {
            if (!Kernel32.OpenProcessToken(process, Kernel32.TOKEN_QUERY, out var token))
            {
                return Marshal.GetLastPInvokeError() == Kernel32.ERROR_ACCESS_DENIED;
            }

            try
            {
                return Kernel32.GetTokenInformation(token, Kernel32.TokenElevation, out var elevated, sizeof(int), out _) && elevated != 0;
            }
            finally
            {
                Kernel32.CloseHandle(token);
            }
        }
        finally
        {
            Kernel32.CloseHandle(process);
        }
    }
}
