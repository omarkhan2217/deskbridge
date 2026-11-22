using Deskbridge.Core.Apps;
using Deskbridge.Core.Policy;
using Deskbridge.Core.Tools;
using Deskbridge.E2E.Fixtures;
using Deskbridge.E2E.HandsOff;
using Deskbridge.Engines.Uia;
using Deskbridge.Ui;

namespace Deskbridge.E2E;

public sealed class ConfirmationWindowTests : IDisposable
{
    readonly UiThread ui = new();
    readonly UiaWorker worker = new();

    public void Dispose()
    {
        worker.Dispose();
        ui.Dispose();
    }

    static ConfirmationRequest Request(string action, bool canRemember = true) =>
        new(ConfirmationKind.ToolCall, "Cursor", AppId.ForExe(@"C:\Windows\notepad.exe", "Notepad"),
            action, "hello world", ToolEffect.Change, canRemember, Note: null);

    [Fact]
    public async Task Showing_the_window_does_not_touch_the_user()
    {
        var presenter = new ConfirmationPresenter(ui, TimeSpan.FromMinutes(2));

        // Each attempt shows one window and answers it through UI Automation, so a retry (when the
        // user moved the mouse meanwhile) leaves nothing queued.
        var answer = await HandsOffProbe.AssertUntouchedAsync(async () =>
        {
            var asked = presenter.AskAsync(Request("Type into Notes in Notepad"), CancellationToken.None);
            await ConfirmationAnswerer.AnswerAsync(worker, "DenyButton");
            return await asked;
        });
        Assert.Equal(ConfirmationAnswer.Deny, answer);
    }

    [Fact]
    public async Task Allow_through_ui_automation_returns_allow()
    {
        var presenter = new ConfirmationPresenter(ui, TimeSpan.FromMinutes(2));
        var answer = presenter.AskAsync(Request("Rename notes.txt to plan.txt"), CancellationToken.None);
        var shown = await ConfirmationAnswerer.AnswerAsync(worker, "AllowButton");
        Assert.Equal(ConfirmationAnswer.Allow, await answer);
        Assert.Contains("Rename notes.txt to plan.txt", shown);
    }

    [Fact]
    public async Task Remember_button_only_when_allowed()
    {
        var presenter = new ConfirmationPresenter(ui, TimeSpan.FromMinutes(2));
        var answer = presenter.AskAsync(Request("Delete plan.txt", canRemember: false), CancellationToken.None);
        var window = await ConfirmationAnswerer.WaitForWindowAsync(TimeSpan.FromSeconds(10));
        Assert.False(await ConfirmationAnswerer.HasButtonAsync(worker, window, "RememberButton"));
        await ConfirmationAnswerer.AnswerAsync(worker, "DenyButton");
        await answer;

        var second = presenter.AskAsync(Request("Type into Notes in Notepad"), CancellationToken.None);
        window = await ConfirmationAnswerer.WaitForWindowAsync(TimeSpan.FromSeconds(10));
        Assert.True(await ConfirmationAnswerer.HasButtonAsync(worker, window, "RememberButton"));
        await ConfirmationAnswerer.AnswerAsync(worker, "RememberButton");
        Assert.Equal(ConfirmationAnswer.AllowAndRemember, await second);
    }

    [Fact]
    public async Task No_answer_times_out()
    {
        var presenter = new ConfirmationPresenter(ui, TimeSpan.FromMilliseconds(300));
        Assert.Equal(ConfirmationAnswer.TimedOut, await presenter.AskAsync(Request("Anything"), CancellationToken.None));
    }

    [Fact]
    public async Task Two_requests_queue_and_each_gets_its_answer()
    {
        var presenter = new ConfirmationPresenter(ui, TimeSpan.FromMinutes(2));
        var first = presenter.AskAsync(Request("First request"), CancellationToken.None);
        var second = presenter.AskAsync(Request("Second request"), CancellationToken.None);

        Assert.Contains("First request", await ConfirmationAnswerer.AnswerAsync(worker, "DenyButton"));
        Assert.Equal(ConfirmationAnswer.Deny, await first);
        Assert.False(second.IsCompleted);

        Assert.Contains("Second request", await ConfirmationAnswerer.AnswerAsync(worker, "AllowButton"));
        Assert.Equal(ConfirmationAnswer.Allow, await second);
    }
}
