using System.Diagnostics;
using Deskbridge.E2E.Fixtures;

namespace Deskbridge.E2E;

public class RelayE2ETests
{
    static readonly Dictionary<string, object?> NoArgs = new();

    [Theory]
    [InlineData("2025-11-25")]
    [InlineData("2026-07-28")]
    public async Task A_client_on_either_spec_reaches_the_tools_through_the_relay(string version)
    {
        await using var c = await RelayHarness.ConnectAsync(protocolVersion: version);
        var tools = await c.Client.ListToolsAsync();
        Assert.Contains(tools, t => t.Name == "windows_list");
        Assert.NotEqual(true, (await c.Client.CallToolAsync("windows_list", NoArgs)).IsError);
    }

    [Fact]
    public async Task Two_relays_started_together_share_one_broker()
    {
        var suffix = RelayHarness.NewSuffix();
        var started = DateTime.Now.AddMilliseconds(-50);
        var both = await Task.WhenAll(RelayHarness.ConnectAsync(suffix: suffix), RelayHarness.ConnectAsync(suffix: suffix));
        try
        {
            foreach (var c in both)
            {
                Assert.NotEmpty(await c.Client.ListToolsAsync());
            }

            Assert.Single(RelayHarness.BrokersStartedSince(started));
        }
        finally
        {
            foreach (var c in both)
            {
                await c.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Broker_exits_after_the_last_client_leaves()
    {
        var started = DateTime.Now.AddMilliseconds(-50);
        await (await RelayHarness.ConnectAsync()).DisposeAsync();
        await Task.Delay(TimeSpan.FromSeconds(5)); // idle override is 3 s
        Assert.Empty(RelayHarness.BrokersStartedSince(started));
    }

    [Fact]
    public async Task Warm_start_to_tool_list_is_under_one_second()
    {
        var suffix = RelayHarness.NewSuffix();
        var cold = Stopwatch.StartNew();
        await using var keepBrokerAlive = await RelayHarness.ConnectAsync(suffix: suffix);
        await keepBrokerAlive.Client.ListToolsAsync();
        cold.Stop();

        var warm = Stopwatch.StartNew();
        await using var c = await RelayHarness.ConnectAsync(suffix: suffix);
        await c.Client.ListToolsAsync();
        warm.Stop();

        TestContext.Current.AddAttachment("timings", $"cold {cold.ElapsedMilliseconds} ms, warm {warm.ElapsedMilliseconds} ms");
        Assert.True(warm.Elapsed < TimeSpan.FromSeconds(1), $"warm start took {warm.Elapsed}");
    }
}
