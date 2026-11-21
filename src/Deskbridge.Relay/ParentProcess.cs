using System.Runtime.InteropServices;
using Deskbridge.Core.Wire;

namespace Deskbridge.Relay;

/// <summary>Finds the process that started the relay: the MCP client, which the broker asks the user about.</summary>
internal static unsafe class ParentProcess
{
    public static ClientProcess? Get(int childPid)
    {
        var parentPid = FindParentPid(childPid);
        if (parentPid is null || !Exists(parentPid.Value))
        {
            return null;
        }

        var path = ImagePath(parentPid.Value);
        return new ClientProcess((int)parentPid.Value, path, path is null ? null : Path.GetFileNameWithoutExtension(path));
    }

    static uint? FindParentPid(int childPid)
    {
        if (childPid <= 0)
        {
            return null;
        }

        var snapshot = Native.CreateToolhelp32Snapshot(Native.TH32CS_SNAPPROCESS, 0);
        if (snapshot == Native.InvalidHandle)
        {
            return null;
        }

        try
        {
            var entry = new Native.ProcessEntry32 { dwSize = (uint)Marshal.SizeOf<Native.ProcessEntry32>() };
            for (var ok = Native.Process32First(snapshot, ref entry); ok; ok = Native.Process32Next(snapshot, ref entry))
            {
                if (entry.th32ProcessID == (uint)childPid)
                {
                    return entry.th32ParentProcessID;
                }
            }

            return null;
        }
        finally
        {
            Native.CloseHandle(snapshot);
        }
    }

    static bool Exists(uint pid)
    {
        var snapshot = Native.CreateToolhelp32Snapshot(Native.TH32CS_SNAPPROCESS, 0);
        if (snapshot == Native.InvalidHandle)
        {
            return false;
        }

        try
        {
            var entry = new Native.ProcessEntry32 { dwSize = (uint)Marshal.SizeOf<Native.ProcessEntry32>() };
            for (var ok = Native.Process32First(snapshot, ref entry); ok; ok = Native.Process32Next(snapshot, ref entry))
            {
                if (entry.th32ProcessID == pid)
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            Native.CloseHandle(snapshot);
        }
    }

    static string? ImagePath(uint pid)
    {
        var process = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
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
