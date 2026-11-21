using System.IO.Pipes;
using System.Text;
using Deskbridge.Broker;
using Deskbridge.Core.Wire;
using Deskbridge.Engines.Uia;
using Deskbridge.Mcp;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Deskbridge.Tests;

/// <summary>A BrokerHost on a private pipe name, run in this process, with helpers to connect clients.</summary>
sealed class BrokerHarness : IAsyncDisposable
{
    readonly string pipeName;
    readonly CancellationTokenSource stop = new();
    readonly UiaWorker worker;
    readonly List<IDisposable> raw = new();

    BrokerHarness(string pipeName, BrokerHost host, UiaWorker worker)
    {
        this.pipeName = pipeName;
        this.worker = worker;
        Host = host;
        Run = host.RunAsync(stop.Token);
    }

    public BrokerHost Host { get; }

    public Task Run { get; }

    public static Task<BrokerHarness> StartAsync(TimeSpan? idleExit = null)
    {
        var name = "deskbridge-test-" + Guid.NewGuid().ToString("N");
        var worker = new UiaWorker();
        var host = new BrokerHost(
            new BrokerOptions(name, idleExit ?? TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(5)),
            new ToolServices(new UiaReader(worker)));
        return Task.FromResult(new BrokerHarness(name, host, worker));
    }

    public async Task<ConnectedClient> ConnectAsync()
    {
        var pipe = await ConnectRawPipeAsync(track: false);
        HelloFrame.Write(pipe, new ClientHello(1, HelloModes.Classic, new ClientProcess(Environment.ProcessId, null, "tests")));
        var client = await McpClient.CreateAsync(new StreamClientTransport(pipe, pipe));
        return new ConnectedClient(client, pipe);
    }

    public async Task SendRawAsync(string text)
    {
        var pipe = await ConnectRawPipeAsync();
        await pipe.WriteAsync(Encoding.UTF8.GetBytes(text));
        await pipe.FlushAsync();
    }

    public Task<NamedPipeClientStream> ConnectRawPipeAsync() => ConnectRawPipeAsync(track: true);

    async Task<NamedPipeClientStream> ConnectRawPipeAsync(bool track)
    {
        var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5_000);
        if (track)
        {
            raw.Add(pipe);
        }

        return pipe;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var r in raw)
        {
            r.Dispose();
        }

        await stop.CancelAsync();
        try
        {
            await Run.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
        }

        await Host.DisposeAsync();
        worker.Dispose();
        stop.Dispose();
    }

    public sealed class ConnectedClient(McpClient client, NamedPipeClientStream pipe) : IAsyncDisposable
    {
        public async Task<IList<McpClientTool>> ListToolsAsync() => await client.ListToolsAsync();

        public async Task<CallToolResult> CallToolAsync(string name, Dictionary<string, object?> args) =>
            await client.CallToolAsync(name, args);

        public async ValueTask DisposeAsync()
        {
            await client.DisposeAsync();
            await pipe.DisposeAsync();
        }
    }
}
