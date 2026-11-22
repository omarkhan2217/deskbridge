using System.IO.Pipes;
using Deskbridge.Core.Wire;
using Deskbridge.Engines.Uia;
using Deskbridge.Mcp;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Deskbridge.Tests;

/// <summary>A real SDK client talking to a server from McpSessionFactory over a private named pipe.</summary>
sealed class SessionHarness : IAsyncDisposable
{
    readonly NamedPipeServerStream serverPipe;
    readonly NamedPipeClientStream clientPipe;
    readonly ToolServices services;
    readonly McpServer server;
    readonly Task run;

    SessionHarness(NamedPipeServerStream serverPipe, NamedPipeClientStream clientPipe, ToolServices services, McpServer server, Task run, McpClient client)
    {
        this.serverPipe = serverPipe;
        this.clientPipe = clientPipe;
        this.services = services;
        this.server = server;
        this.run = run;
        Client = client;
    }

    public McpClient Client { get; }

    public static async Task<SessionHarness> StartAsync(string? protocolVersion = null, ToolServices? services = null)
    {
        var name = "deskbridge-test-" + Guid.NewGuid().ToString("N");
        var serverPipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var clientPipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await Task.WhenAll(serverPipe.WaitForConnectionAsync(), clientPipe.ConnectAsync());

        services ??= TestServices.AllowAll();
        var hello = new ClientHello(1, HelloModes.Classic, TestServices.TestClient);
        var server = McpSessionFactory.Create(serverPipe, hello, services);
        var run = server.RunAsync();
        var client = await McpClient.CreateAsync(
            new StreamClientTransport(clientPipe, clientPipe),
            new McpClientOptions { ProtocolVersion = protocolVersion });
        return new SessionHarness(serverPipe, clientPipe, services, server, run, client);
    }

    public static string Text(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(t => t.Text));

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await clientPipe.DisposeAsync();
        await server.DisposeAsync();
        await serverPipe.DisposeAsync();
        try
        {
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            // the session ends with the pipe; how it ends does not matter here
        }

        services.Dispose();
    }
}
