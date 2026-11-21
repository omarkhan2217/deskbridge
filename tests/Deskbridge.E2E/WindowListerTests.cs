using Deskbridge.E2E.Fixtures;
using Deskbridge.E2E.HandsOff;
using Deskbridge.Engines.Os;

namespace Deskbridge.E2E;

public class WindowListerTests
{
    [Fact]
    public void Lists_a_visible_window_with_its_process()
    {
        var title = "dbe2e-list-" + Guid.NewGuid().ToString("N")[..8];
        using var app = TargetApp.Start(title, "--text", "x");
        var w = Assert.Single(WindowLister.List(), w => w.Title == title);
        Assert.Equal(app.Handle, w.Handle);
        Assert.Equal(app.ProcessId, w.ProcessId);
        Assert.Equal("Deskbridge.E2E.Target", w.ProcessName);
    }

    [Fact]
    public void Hidden_windows_are_not_listed()
    {
        var title = "dbe2e-hidden-" + Guid.NewGuid().ToString("N")[..8];
        using var app = TargetApp.Start(title, "--hidden");
        Assert.DoesNotContain(WindowLister.List(), w => w.Title == title);
    }

    [Fact]
    public async Task Listing_does_not_touch_the_user() =>
        await HandsOffProbe.AssertUntouchedAsync(() => Task.FromResult(WindowLister.List()));
}
