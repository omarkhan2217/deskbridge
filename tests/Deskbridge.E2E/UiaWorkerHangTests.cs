using System.Diagnostics;
using Deskbridge.E2E.Fixtures;
using Deskbridge.Engines.Uia;

namespace Deskbridge.E2E;

public class UiaWorkerHangTests
{
    [Fact]
    public async Task Hung_target_times_out_and_the_next_call_works()
    {
        using var hung = TargetApp.Start("dbe2e-hang-" + Guid.NewGuid().ToString("N")[..8], "--text", "x", "--hang-ms", "20000");
        using var healthy = TargetApp.Start("dbe2e-ok-" + Guid.NewGuid().ToString("N")[..8], "--text", "fine");
        await Task.Delay(1_000); // the hung target blocks its UI thread 500 ms after showing
        using var worker = new UiaWorker();

        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<Exception>(() => worker.RunAsync(a => a.FromHandle(hung.Handle).FindAllDescendants().Length));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(9), $"took {sw.Elapsed}");
        TestContext.Current.SendDiagnosticMessage($"hung call failed after {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        var name = await worker.RunAsync(a => a.FromHandle(healthy.Handle).Name);
        Assert.Contains("dbe2e-ok-", name);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"took {sw.Elapsed}");
    }
}
