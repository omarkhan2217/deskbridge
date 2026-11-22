using System.Diagnostics;
using Deskbridge.Core;
using Deskbridge.Core.Clients;
using Deskbridge.Core.Policy;
using ModelContextProtocol.Client;

namespace Deskbridge.E2E.Fixtures;

/// <summary>
/// An SDK client that starts the published AOT relay (artifacts/relay/deskbridge.exe) the way a real
/// MCP client does. Each harness uses its own pipe suffix and a 3 s idle exit, so tests never touch
/// the user's real broker and leave nothing running.
/// </summary>
public sealed class RelayHarness : IAsyncDisposable
{
    RelayHarness(McpClient client) => Client = client;

    public McpClient Client { get; }

    public static string NewSuffix() => "e2e-" + Guid.NewGuid().ToString("N")[..8];

    public static async Task<RelayHarness> ConnectAsync(string? protocolVersion = null, string? suffix = null)
    {
        suffix ??= NewSuffix();
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "deskbridge",
            Command = RelayPath(),
            Arguments = ["mcp"],
            EnvironmentVariables = new Dictionary<string, string?>
            {
                [Names.PipeSuffixVariable] = suffix,
                [Names.ConfigDirVariable] = ConfigFor(suffix),
                [Names.IdleExitVariable] = "3000",
                [Names.BrokerPathVariable] = Path.Combine(AppContext.BaseDirectory, Names.BrokerExe),
            },
        });
        var client = await McpClient.CreateAsync(transport, new McpClientOptions { ProtocolVersion = protocolVersion });
        return new RelayHarness(client);
    }

    public static IReadOnlyList<int> BrokersStartedSince(DateTime started) =>
        Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Names.BrokerExe))
            .Where(p =>
            {
                try
                {
                    return p.StartTime >= started;
                }
                catch (InvalidOperationException)
                {
                    return false; // exited while we looked
                }
            })
            .Select(p => p.Id)
            .ToList();

    /// <summary>
    /// A config folder per broker, with this test process already approved as a client, so the broker never
    /// asks and never reads the user's real permissions (D40).
    /// </summary>
    public static string ConfigFor(string suffix)
    {
        var dir = Path.Combine(Path.GetTempPath(), "deskbridge-e2e-config-" + suffix);
        var store = new ConfigStore(dir);
        if (store.Current.Clients.Count == 0)
        {
            var self = Environment.ProcessPath!;
            store.Update(c => c with
            {
                Clients = [new ClientRecord(self, Deskbridge.Engines.Os.Publisher.Of(self), Path.GetFileNameWithoutExtension(self), DateTimeOffset.Now)],
            });
        }

        return dir;
    }

    static string RelayPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Deskbridge.slnx")))
        {
            dir = dir.Parent;
        }

        var relay = dir is null ? null : Path.Combine(dir.FullName, "artifacts", "relay", Names.RelayExe);
        if (relay is null || !File.Exists(relay))
        {
            throw new FileNotFoundException(
                "Publish the relay first: dotnet publish src/Deskbridge.Relay -c Release -r win-x64 -o artifacts/relay");
        }

        return relay;
    }

    public async ValueTask DisposeAsync() => await Client.DisposeAsync();
}
