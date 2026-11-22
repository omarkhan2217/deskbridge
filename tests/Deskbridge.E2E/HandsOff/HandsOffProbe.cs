using System.Runtime.InteropServices;
using Xunit.Sdk;

namespace Deskbridge.E2E.HandsOff;

/// <summary>
/// The proof behind "hands off": records the cursor, foreground window and keyboard focus
/// before and after an action and fails on any change Deskbridge could have caused.
/// </summary>
public static class HandsOffProbe
{
    const int Attempts = 5;
    static readonly TimeSpan QuietBefore = TimeSpan.FromMilliseconds(250);
    static readonly TimeSpan QuietWaitLimit = TimeSpan.FromSeconds(15);

    public static HandsOffSnapshot Capture()
    {
        var captureTick = (uint)Environment.TickCount;
        NativeTest.GetCursorPos(out var point);
        var foreground = NativeTest.GetForegroundWindow();
        nint focus = 0;
        if (foreground != 0)
        {
            var thread = NativeTest.GetWindowThreadProcessId(foreground, out _);
            var info = new NativeTest.GuiThreadInfo { cbSize = Marshal.SizeOf<NativeTest.GuiThreadInfo>() };
            if (NativeTest.GetGUIThreadInfo(thread, ref info))
            {
                focus = info.hwndFocus;
            }
        }

        return new HandsOffSnapshot(point.X, point.Y, foreground, focus, LastInputTick(), captureTick);
    }

    public static HandsOffResult Compare(HandsOffSnapshot before, HandsOffSnapshot after)
    {
        var changes = new List<string>();
        if (before.CursorX != after.CursorX || before.CursorY != after.CursorY)
        {
            changes.Add($"cursor moved from ({before.CursorX}, {before.CursorY}) to ({after.CursorX}, {after.CursorY})");
        }

        if (before.Foreground != after.Foreground)
        {
            changes.Add($"foreground changed from 0x{before.Foreground:X} to 0x{after.Foreground:X}");
        }

        if (before.Focus != after.Focus)
        {
            changes.Add($"focus changed from 0x{before.Focus:X} to 0x{after.Focus:X}");
        }

        // Input stamped at or after the first snapshot happened during the action. The tick has a
        // 15.6 ms resolution, so input just before the snapshot can also count: that only causes a retry.
        var inputDuringAction = unchecked((int)(after.LastInputTick - before.CaptureTick)) >= -16;
        return new HandsOffResult(changes, inputDuringAction);
    }

    /// <summary>
    /// Runs a read-only action between two snapshots, at a moment when the user is idle. If input
    /// arrives during the action, the result cannot be blamed on Deskbridge, so the action is repeated
    /// (up to 5 attempts). Injected input would also count as input, so a Deskbridge bug that injects
    /// input ends as "inconclusive", which still fails the test.
    /// </summary>
    public static async Task<T> AssertUntouchedAsync<T>(Func<Task<T>> action)
    {
        for (var attempt = 1; ; attempt++)
        {
            WaitForQuiet();
            var before = Capture();
            var result = await action();
            var after = Capture();
            var comparison = Compare(before, after);
            if (comparison.UserInterfered)
            {
                if (attempt < Attempts)
                {
                    continue;
                }

                throw new XunitException(
                    $"Inconclusive: input arrived during all {Attempts} attempts (the user, or injected input). Last changes: "
                    + (comparison.Changes.Count == 0 ? "none" : string.Join("; ", comparison.Changes)));
            }

            if (comparison.Changes.Count > 0)
            {
                throw new XunitException("The action touched the user: " + string.Join("; ", comparison.Changes));
            }

            return result;
        }
    }

    /// <summary>
    /// For actions that must not run twice (a press, a rename): waits for a quiet moment, runs once, and fails
    /// as inconclusive if the user gave input meanwhile instead of repeating the action.
    /// </summary>
    public static async Task<T> AssertUntouchedOnceAsync<T>(Func<Task<T>> action)
    {
        WaitForQuiet();
        var before = Capture();
        var result = await action();
        var comparison = Compare(before, Capture());
        if (comparison.UserInterfered)
        {
            throw new XunitException("Inconclusive: input arrived during a one-time action (the user, or injected input). Run the test again with hands off.");
        }

        if (comparison.Changes.Count > 0)
        {
            throw new XunitException("The action touched the user: " + string.Join("; ", comparison.Changes));
        }

        return result;
    }

    static void WaitForQuiet()
    {
        var deadline = DateTime.UtcNow + QuietWaitLimit;
        while (DateTime.UtcNow < deadline)
        {
            var idleMs = unchecked((uint)Environment.TickCount - LastInputTick());
            if (idleMs >= QuietBefore.TotalMilliseconds)
            {
                return;
            }

            Thread.Sleep(50);
        }
    }

    static uint LastInputTick()
    {
        var input = new NativeTest.LastInputInfo { cbSize = (uint)Marshal.SizeOf<NativeTest.LastInputInfo>() };
        NativeTest.GetLastInputInfo(ref input);
        return input.dwTime;
    }
}
