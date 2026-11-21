using Deskbridge.Engines.Uia;

namespace Deskbridge.Tests;

public class UiaWorkerTests
{
    [Fact]
    public async Task Every_call_runs_on_one_mta_thread()
    {
        using var worker = new UiaWorker();
        var a = await worker.RunAsync(_ => (Environment.CurrentManagedThreadId, Thread.CurrentThread.GetApartmentState()));
        var b = await worker.RunAsync(_ => Environment.CurrentManagedThreadId);
        Assert.Equal(a.Item1, b);
        Assert.Equal(ApartmentState.MTA, a.Item2);
    }

    [Fact]
    public async Task Auto_set_focus_is_off_and_timeouts_are_set()
    {
        using var worker = new UiaWorker();
        var (autoSetFocus, transaction, connection) = await worker.RunAsync(a =>
            (a.NativeAutomation2.AutoSetFocus, a.TransactionTimeout, a.ConnectionTimeout));
        Assert.Equal(0, autoSetFocus);
        Assert.Equal(TimeSpan.FromSeconds(5), transaction);
        Assert.Equal(TimeSpan.FromSeconds(2), connection);
    }

    [Fact]
    public async Task A_stuck_call_times_out_and_a_new_thread_takes_over()
    {
        using var worker = new UiaWorker(new UiaWorkerOptions(TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2)));
        var first = worker.ThreadId;
        await Assert.ThrowsAsync<UiaTimeoutException>(() => worker.RunAsync(_ => { Thread.Sleep(5_000); return 0; }));
        Assert.Equal(1, await worker.RunAsync(_ => 1));
        Assert.NotEqual(first, worker.ThreadId);
    }

    [Fact]
    public async Task Exceptions_from_work_reach_the_caller()
    {
        using var worker = new UiaWorker();
        await Assert.ThrowsAsync<InvalidOperationException>(() => worker.RunAsync<int>(_ => throw new InvalidOperationException()));
    }
}
