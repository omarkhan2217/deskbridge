using Deskbridge.Broker;

namespace Deskbridge.Tests;

public class BrokerHostTests
{
    [Fact]
    public async Task Two_clients_are_served_at_once()
    {
        await using var b = await BrokerHarness.StartAsync();
        await using var c1 = await b.ConnectAsync();
        await using var c2 = await b.ConnectAsync();
        Assert.NotEmpty(await c1.ListToolsAsync());
        Assert.NotEmpty(await c2.ListToolsAsync());
        Assert.Equal(2, b.Host.ActiveSessions);
    }

    [Fact]
    public async Task Bad_hello_closes_only_that_connection()
    {
        await using var b = await BrokerHarness.StartAsync();
        await using var good = await b.ConnectAsync();
        await b.SendRawAsync("not a hello\n");
        Assert.NotEmpty(await good.ListToolsAsync());
    }

    [Fact]
    public async Task Abrupt_disconnect_leaves_other_sessions_working()
    {
        await using var b = await BrokerHarness.StartAsync();
        await using var survivor = await b.ConnectAsync();
        var victim = await b.ConnectRawPipeAsync();
        victim.Dispose(); // like a relay being killed mid-session
        await Task.Delay(300);
        Assert.NotEmpty(await survivor.ListToolsAsync());
        Assert.Equal(1, b.Host.ActiveSessions);
    }

    [Fact]
    public async Task Idle_exit_fires_after_the_last_client_leaves()
    {
        await using var b = await BrokerHarness.StartAsync(idleExit: TimeSpan.FromMilliseconds(300));
        await (await b.ConnectAsync()).DisposeAsync();
        var finished = await Task.WhenAny(b.Run, Task.Delay(3_000));
        Assert.Same(b.Run, finished);
    }

    [Fact]
    public async Task Broker_stays_while_a_client_is_connected()
    {
        await using var b = await BrokerHarness.StartAsync(idleExit: TimeSpan.FromMilliseconds(300));
        await using var c = await b.ConnectAsync();
        Assert.NotSame(b.Run, await Task.WhenAny(b.Run, Task.Delay(1_000)));
    }

    [Fact]
    public void Second_broker_cannot_take_the_mutex()
    {
        var name = @"Local\deskbridge-test-" + Guid.NewGuid().ToString("N");
        using var first = SingleInstance.TryAcquire(name);
        Assert.NotNull(first);
        Assert.Null(SingleInstance.TryAcquire(name));
    }
}
