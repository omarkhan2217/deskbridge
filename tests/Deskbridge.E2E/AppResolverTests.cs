using Deskbridge.E2E.Fixtures;
using Deskbridge.Engines.Os;

namespace Deskbridge.E2E;

public class AppResolverTests
{
    [Fact]
    public void Same_app_from_window_and_process_is_equal()
    {
        using var app = TargetApp.Start("dbe2e-app-" + Guid.NewGuid().ToString("N")[..8], "--text", "x");
        var fromWindow = AppResolver.FromWindow(app.Handle);
        Assert.Equal(AppResolver.FromProcess(app.ProcessId), fromWindow);
        Assert.EndsWith(@"\deskbridge.e2e.target.exe", fromWindow.Key);
    }

    [Fact]
    public async Task Calculator_resolves_to_its_own_exe_not_the_frame_host()
    {
        var cover = Path.Combine(Path.GetTempPath(), "deskbridge-e2e-cover-" + Guid.NewGuid().ToString("N")[..8] + ".txt");
        await File.WriteAllTextAsync(cover, "cover");
        try
        {
            using var calc = RealApp.Start("calc.exe", "", titleContains: "Calculator", processName: "CalculatorApp");

            // The frame appears before Windows attaches the app's content window to it.
            var key = AppResolver.FromWindow(calc.Handle).Key;
            for (var waited = 0; key.StartsWith("store:", StringComparison.Ordinal) && waited < 5_000; waited += 200)
            {
                await Task.Delay(200);
                key = AppResolver.FromWindow(calc.Handle).Key;
            }

            Assert.EndsWith(@"\calculatorapp.exe", key);
        }
        finally
        {
            File.Delete(cover);
        }
    }
}
