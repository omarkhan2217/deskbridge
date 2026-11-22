using System.Runtime.InteropServices;
using Deskbridge.Engines.Os.Native;

namespace Deskbridge.Engines.Os.Files;

/// <summary>
/// A path as the file system resolves it: long names in place of 8.3 short names, and links and junctions
/// followed to where they point. Secret-file checks run on this as well as on the path as given, so
/// "CREDEN~1.JSO" or a junction into a secrets folder is caught like the real name.
/// </summary>
public static unsafe class RealPath
{
    /// <summary>For a path that does not exist yet, its nearest existing folder is resolved and the rest added back.</summary>
    public static string Of(string fullPath)
    {
        var existing = fullPath;
        var missing = new Stack<string>();
        while (!File.Exists(existing) && !Directory.Exists(existing))
        {
            var parent = Path.GetDirectoryName(existing);
            if (parent is null)
            {
                return fullPath;
            }

            missing.Push(Path.GetFileName(existing));
            existing = parent;
        }

        // Access 0 asks for no rights: enough to learn the path, without reading anything.
        using var handle = Kernel32.CreateFile(existing, 0, Kernel32.FILE_SHARE_ALL, 0, Kernel32.OPEN_EXISTING, Kernel32.FILE_FLAG_BACKUP_SEMANTICS, 0);
        var resolved = handle.IsInvalid ? existing : Final(handle) ?? existing;
        while (missing.Count > 0)
        {
            resolved = Path.Combine(resolved, missing.Pop());
        }

        return resolved;
    }

    /// <summary>The final path of an open file or folder, without the \\?\ prefix, or null when Windows cannot say.</summary>
    public static string? Final(SafeHandle handle)
    {
        var size = 512u;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var buffer = new char[size];
            uint length;
            fixed (char* p = buffer)
            {
                length = Kernel32.GetFinalPathNameByHandle(handle, p, size, 0);
            }

            if (length == 0)
            {
                return null;
            }

            if (length < size)
            {
                return WithoutPrefix(new string(buffer, 0, (int)length));
            }

            size = length + 1;
        }

        return null;
    }

    static string WithoutPrefix(string path) =>
        path.StartsWith(@"\\?\UNC\", StringComparison.Ordinal) ? @"\\" + path[8..]
        : path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..]
        : path;
}
