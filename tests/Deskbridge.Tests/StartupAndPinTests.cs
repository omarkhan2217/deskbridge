using Deskbridge.Broker;
using Deskbridge.Ui;

namespace Deskbridge.Tests;

public class StartupRegistrationTests
{
    [Fact]
    public void Startup_value_is_written_and_removed()
    {
        var registration = new StartupRegistration("Deskbridge-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Assert.False(registration.IsOn);
            registration.Set(true, @"C:\Apps\Deskbridge\deskbridge-broker.exe");
            Assert.True(registration.IsOn);
            Assert.Equal("\"C:\\Apps\\Deskbridge\\deskbridge-broker.exe\"", registration.Command);
            registration.Set(false, "");
            Assert.False(registration.IsOn);
        }
        finally
        {
            registration.Set(false, "");
        }
    }
}

public class PinnedBrokerTests
{
    [Fact]
    public async Task Pinned_broker_does_not_idle_exit_until_unpinned()
    {
        var pinned = true;
        using var services = TestServices.AllowAll();
        await using var host = new BrokerHost(
            new BrokerOptions("deskbridge-test-" + Guid.NewGuid().ToString("N"), TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(5)),
            services,
            keepRunning: () => pinned);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = host.RunAsync(stop.Token);

        Assert.NotSame(run, await Task.WhenAny(run, Task.Delay(1_000)));
        pinned = false;
        Assert.Same(run, await Task.WhenAny(run, Task.Delay(3_000)));
    }
}
