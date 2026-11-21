using Deskbridge.E2E.Fixtures;

namespace Deskbridge.E2E.HandsOff;

public class HandsOffProbeTests
{
    [Fact]
    public void Catches_deliberate_mouse_move()
    {
        // The only place in the repo that moves the cursor. It puts it back.
        var before = HandsOffProbe.Capture();
        NativeTest.SetCursorPos(before.CursorX + 50, before.CursorY + 50);
        var after = HandsOffProbe.Capture();
        NativeTest.SetCursorPos(before.CursorX, before.CursorY);

        var result = HandsOffProbe.Compare(before, after);
        Assert.Contains(result.Changes, c => c.StartsWith("cursor moved"));
    }

    [Fact]
    public void Input_stamped_during_the_action_counts_as_the_user()
    {
        // The user's last input carries the same tick both times (15.6 ms resolution) but is later than the first snapshot.
        var before = new HandsOffSnapshot(10, 10, 1, 1, LastInputTick: 1_000, CaptureTick: 1_000);
        var after = new HandsOffSnapshot(10, 14, 1, 1, LastInputTick: 1_000, CaptureTick: 1_014);
        Assert.True(HandsOffProbe.Compare(before, after).UserInterfered);

        var idleBefore = before with { LastInputTick = 500 };
        var idleAfter = after with { LastInputTick = 500 };
        Assert.False(HandsOffProbe.Compare(idleBefore, idleAfter).UserInterfered);
    }

    [Fact]
    public async Task Quiet_action_passes() =>
        await HandsOffProbe.AssertUntouchedAsync(async () => { await Task.Delay(50); return 0; });

    [Fact]
    public async Task Target_app_opens_without_taking_focus() =>
        await HandsOffProbe.AssertUntouchedAsync(() =>
        {
            using var app = TargetApp.Start("dbe2e-focus-" + Guid.NewGuid().ToString("N")[..8], "--text", "x");
            Assert.NotEqual(app.Handle, NativeTest.GetForegroundWindow());
            return Task.FromResult(0);
        });
}
