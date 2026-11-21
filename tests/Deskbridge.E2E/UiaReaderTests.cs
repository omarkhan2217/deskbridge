using Deskbridge.E2E.Fixtures;
using Deskbridge.E2E.HandsOff;
using Deskbridge.Engines.Uia;

namespace Deskbridge.E2E;

public sealed class UiaReaderTests : IDisposable
{
    readonly UiaWorker worker = new();

    UiaReader Reader => new(worker);

    static string Title(string kind) => $"dbe2e-{kind}-{Guid.NewGuid().ToString("N")[..8]}";

    public void Dispose() => worker.Dispose();

    [Fact]
    public async Task Reads_a_background_window_without_touching_the_user()
    {
        using var app = TargetApp.Start(Title("read"), "--text", "hello from target 7f3a");
        var text = await HandsOffProbe.AssertUntouchedAsync(() => Reader.ReadTextAsync(app.Handle, "Notes"));
        Assert.Equal("hello from target 7f3a", text.Text);
        Assert.Equal("NotesBox", text.AutomationId);
        Assert.False(text.Truncated);
    }

    [Fact]
    public async Task Finds_by_automation_id_and_by_default_edit()
    {
        using var app = TargetApp.Start(Title("find"), "--text", "abc");
        Assert.Equal("abc", (await Reader.ReadTextAsync(app.Handle, "NotesBox")).Text);
        Assert.Equal("abc", (await Reader.ReadTextAsync(app.Handle, null)).Text);
    }

    [Fact]
    public async Task Long_text_is_cut_and_marked()
    {
        using var app = TargetApp.Start(Title("big"), "--big", "100000");
        var text = await Reader.ReadTextAsync(app.Handle, "Notes");
        Assert.Equal(20_000, text.Text.Length);
        Assert.True(text.Truncated);
    }

    [Fact]
    public async Task Password_field_is_refused()
    {
        using var app = TargetApp.Start(Title("pw"), "--text", "x", "--password");
        await Assert.ThrowsAsync<SecretFieldException>(() => Reader.ReadTextAsync(app.Handle, "Secret"));
    }

    [Fact]
    public async Task Missing_control_is_reported()
    {
        using var app = TargetApp.Start(Title("missing"), "--text", "x");
        var ex = await Assert.ThrowsAsync<ControlNotFoundException>(() => Reader.ReadTextAsync(app.Handle, "NoSuchControl"));
        Assert.Contains("NoSuchControl", ex.Message);
    }

    [Fact]
    public async Task Hung_window_gives_a_plain_timeout_error()
    {
        using var app = TargetApp.Start(Title("hang"), "--text", "x", "--hang-ms", "20000");
        await Task.Delay(1_000); // the target blocks its UI thread 500 ms after showing
        var ex = await Assert.ThrowsAsync<UiaTimeoutException>(() => Reader.ReadTextAsync(app.Handle, "Notes"));
        Assert.Contains("did not answer", ex.Message);
    }

    [Fact]
    public async Task Closed_window_reports_window_gone()
    {
        var app = TargetApp.Start(Title("gone"), "--text", "x");
        var handle = app.Handle;
        app.Kill();
        app.Dispose();
        await Assert.ThrowsAsync<WindowGoneException>(() => Reader.ReadTextAsync(handle, "Notes"));
    }
}
