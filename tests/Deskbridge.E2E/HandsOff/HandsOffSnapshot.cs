namespace Deskbridge.E2E.HandsOff;

/// <summary>
/// What the user owns: the cursor, the foreground window and the focused control. Also when the user
/// last gave input and when this snapshot was taken, both as Windows tick counts (milliseconds).
/// </summary>
public sealed record HandsOffSnapshot(int CursorX, int CursorY, nint Foreground, nint Focus, uint LastInputTick, uint CaptureTick);

public sealed record HandsOffResult(IReadOnlyList<string> Changes, bool UserInterfered);
