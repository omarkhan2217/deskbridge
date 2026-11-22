using Deskbridge.E2E.HandsOff;
using Deskbridge.Engines.Uia;
using FlaUI.Core.AutomationElements;

namespace Deskbridge.E2E.Fixtures;

/// <summary>
/// Answers Deskbridge's confirmation window the way a person would, but through UI Automation:
/// no mouse, no focus. Returns the action text the window showed.
/// </summary>
public static class ConfirmationAnswerer
{
    public const string WindowTitle = "Deskbridge";

    public static async Task<nint> WaitForWindowAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var handle = NativeTest.FindWindowW(null, WindowTitle);
            if (handle != 0 && NativeTest.IsWindowVisible(handle))
            {
                return handle;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Deskbridge's confirmation window did not appear.");
    }

    public static async Task<string> AnswerAsync(UiaWorker worker, string buttonId, TimeSpan? timeout = null)
    {
        var window = await WaitForWindowAsync(timeout ?? TimeSpan.FromSeconds(10));
        return await worker.RunAsync(automation =>
        {
            var root = automation.FromHandle(window);
            var cf = root.ConditionFactory;
            var action = root.FindFirstDescendant(cf.ByAutomationId("ActionText"))?.Name ?? "";
            var button = root.FindFirstDescendant(cf.ByAutomationId(buttonId))
                ?? throw new InvalidOperationException($"No {buttonId} in the confirmation window.");
            button.AsButton().Invoke();
            return action;
        });
    }

    public static Task<bool> HasButtonAsync(UiaWorker worker, nint window, string buttonId) =>
        worker.RunAsync(automation =>
        {
            var root = automation.FromHandle(window);
            return root.FindFirstDescendant(root.ConditionFactory.ByAutomationId(buttonId)) is not null;
        });
}
