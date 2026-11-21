namespace Deskbridge.Engines.Os;

/// <summary>One top-level window the user can see.</summary>
public sealed record WindowInfo(nint Handle, string Title, string ProcessName, int ProcessId, string ClassName);
