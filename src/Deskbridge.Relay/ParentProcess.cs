using System.Runtime.InteropServices;
using Deskbridge.Core.Wire;

namespace Deskbridge.Relay;

/// <summary>
/// Finds the MCP client that started the relay, which the broker asks the user about. Many clients start
/// servers through a shell ("cmd /c deskbridge mcp"), so shells between the client and the relay are skipped.
/// </summary>
internal static unsafe class ParentProcess
{
    static readonly string[] Shells = ["cmd.exe", "powershell.exe", "pwsh.exe", "bash.exe", "sh.exe"];
    const int MaxShellsSkipped = 4;

    public static ClientProcess? Get(int childPid)
    {
        var processes = Snapshot();
        if (!processes.TryGetValue(childPid, out var child) || !processes.TryGetValue(child.ParentPid, out var parent))
        {
            return null;
        }

        for (var skipped = 0; skipped < MaxShellsSkipped && Shells.Contains(parent.ExeName, StringComparer.OrdinalIgnoreCase); skipped++)
        {
            if (!processes.TryGetValue(parent.ParentPid, out var above))
            {
                break; // the shell's own parent is gone: the shell is the best we know
            }

            parent = above;
        }

        var path = ImagePath(parent.Pid);
        return new ClientProcess(parent.Pid, path, Path.GetFileNameWithoutExtension(path ?? parent.ExeName));
    }

    public static IReadOnlyList<int> ChildrenOf(int pid) =>
        Snapshot().Values.Where(p => p.ParentPid == pid && p.Pid != pid).Select(p => p.Pid).ToList();

    sealed record ProcessEntry(int Pid, int ParentPid, string ExeName);

    static Dictionary<int, ProcessEntry> Snapshot()
    {
        var result = new Dictionary<int, ProcessEntry>();
        var snapshot = Native.CreateToolhelp32Snapshot(Native.TH32CS_SNAPPROCESS, 0);
        if (snapshot == Native.InvalidHandle)
        {
            return result;
        }

        try
        {
            var entry = new Native.ProcessEntry32 { dwSize = (uint)Marshal.SizeOf<Native.ProcessEntry32>() };
            for (var ok = Native.Process32First(snapshot, ref entry); ok; ok = Native.Process32Next(snapshot, ref entry))
            {
                var name = new string(entry.szExeFile);
                result[(int)entry.th32ProcessID] = new ProcessEntry((int)entry.th32ProcessID, (int)entry.th32ParentProcessID, name);
            }

            return result;
        }
        finally
        {
            Native.CloseHandle(snapshot);
        }
    }

    static string? ImagePath(int pid)
    {
        var process = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (process == 0)
        {
            return null; // for example an elevated client: the broker will show it without a path
        }

        try
        {
            var buffer = stackalloc char[1024];
            uint size = 1024;
            return Native.QueryFullProcessImageName(process, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : null;
        }
        finally
        {
            Native.CloseHandle(process);
        }
    }
}
