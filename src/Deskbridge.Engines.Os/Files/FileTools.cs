using System.Runtime.InteropServices;
using System.Text;
using Deskbridge.Core;

namespace Deskbridge.Engines.Os.Files;

public sealed record FileEntry(string Path, long Size, DateTimeOffset Modified);

public sealed record FileText(string Path, string Text, bool Truncated, long Size);

public enum WriteMode
{
    Create,
    Overwrite,
}

/// <summary>
/// Files through the file system and the Shell. Deletes go to the Recycle Bin with no dialog, and nothing
/// here ever overwrites by surprise: create refuses an existing file and move refuses an existing target.
/// </summary>
public static class FileTools
{
    public static IReadOnlyList<FileEntry> Search(string root, string pattern, int maxResults)
    {
        if (!Directory.Exists(root))
        {
            throw new DeskbridgeException($"There is no folder at {root}.");
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
            AttributesToSkip = FileAttributes.System,
        };
        return Directory.EnumerateFiles(root, string.IsNullOrWhiteSpace(pattern) ? "*" : pattern, options)
            .Take(Math.Clamp(maxResults, 1, 1_000))
            .Select(path => new FileInfo(path))
            .Select(info => new FileEntry(info.FullName, info.Length, info.LastWriteTimeUtc))
            .ToList();
    }

    public static FileText Read(string path, int maxBytes)
    {
        path = Full(path);
        RefuseSecret(path);
        if (!File.Exists(path))
        {
            throw new DeskbridgeException($"There is no file at {path}.");
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (RealPath.Final(stream.SafeFileHandle) is { } opened && SecretFiles.IsSecret(opened))
        {
            throw new SecretFileException(); // the file that actually opened, in case a link changed after the check
        }

        var limit = Math.Clamp(maxBytes, 1, 10 * 1024 * 1024);
        var buffer = new byte[Math.Min(limit, stream.Length)];
        var read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        if (buffer.AsSpan(0, Math.Min(read, 8 * 1024)).Contains((byte)0))
        {
            throw new DeskbridgeException("That file is binary, not text, so Deskbridge will not read it as text.");
        }

        using var reader = new StreamReader(new MemoryStream(buffer, 0, read), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return new FileText(path, reader.ReadToEnd(), stream.Length > read, stream.Length);
    }

    public static void Write(string path, string content, WriteMode mode)
    {
        path = Full(path);
        RefuseSecret(path);
        if (mode == WriteMode.Create && File.Exists(path))
        {
            throw new DeskbridgeException($"A file already exists at {path}. Use mode 'overwrite' to replace it.");
        }

        if (mode == WriteMode.Overwrite && !File.Exists(path))
        {
            throw new DeskbridgeException($"There is no file at {path} to overwrite. Use mode 'create'.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static void Move(string from, string to)
    {
        from = Full(from);
        to = Full(to);
        RefuseSecret(from);
        RefuseSecret(to);
        if (File.Exists(to) || Directory.Exists(to))
        {
            throw new DeskbridgeException($"There is already something at {to}. Deskbridge never overwrites when moving.");
        }

        if (File.Exists(from))
        {
            File.Move(from, to);
        }
        else if (Directory.Exists(from))
        {
            Directory.Move(from, to);
        }
        else
        {
            throw new DeskbridgeException($"There is no file or folder at {from}.");
        }
    }

    /// <summary>Sends a file or folder to the Recycle Bin, with no dialog of any kind.</summary>
    public static void Delete(string path)
    {
        path = Full(path);
        RefuseSecret(path);
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new DeskbridgeException($"There is no file or folder at {path}.");
        }

        var op = new ShFileOpStruct
        {
            wFunc = FoDelete,
            pFrom = path + "\0\0",
            fFlags = FofAllowUndo | FofNoConfirmation | FofNoErrorUi | FofSilent,
        };
        var result = SHFileOperationW(ref op);
        if (result != 0 || op.fAnyOperationsAborted)
        {
            throw new DeskbridgeException($"Windows could not move {path} to the Recycle Bin (error {result}).");
        }
    }

    static string Full(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new DeskbridgeException("Give a full path, like C:\\Users\\you\\Documents\\notes.txt.");
        }

        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? "";
        if (full.AsSpan(root.Length).Contains(':'))
        {
            // "notes.txt:hidden" and ".env::$DATA" name streams inside a file; the second one is the file itself.
            throw new DeskbridgeException("Deskbridge does not open alternate data streams: a path has no ':' after the drive.");
        }

        return full;
    }

    static void RefuseSecret(string path)
    {
        if (SecretFiles.IsSecret(path) || SecretFiles.IsSecret(RealPath.Of(path)))
        {
            throw new SecretFileException();
        }
    }

    const uint FoDelete = 3;
    const ushort FofSilent = 0x0004;
    const ushort FofNoConfirmation = 0x0010;
    const ushort FofAllowUndo = 0x0040;
    const ushort FofNoErrorUi = 0x0400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ShFileOpStruct
    {
        public nint hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public nint hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHFileOperationW(ref ShFileOpStruct op);
}
