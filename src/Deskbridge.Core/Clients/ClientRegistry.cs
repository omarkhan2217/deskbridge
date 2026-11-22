using System.Collections.Concurrent;
using Deskbridge.Core.Policy;
using Deskbridge.Core.Wire;

namespace Deskbridge.Core.Clients;

/// <summary>
/// The MCP clients the user approved. A client is matched on its exe path and its signer, so an exe that
/// changed publisher (or lost its signature) is asked about again. A client whose path is unknown can be
/// approved only for this broker run.
/// </summary>
public sealed class ClientRegistry(ConfigStore config, Func<string, string?> publisherOf)
{
    readonly ConcurrentDictionary<int, bool> approvedThisRun = new();

    public bool IsApproved(ClientProcess client)
    {
        if (client.Path is null)
        {
            return approvedThisRun.ContainsKey(client.Pid);
        }

        var record = config.Current.Clients.FirstOrDefault(c => SamePath(c.Path, client.Path));
        return record is not null && record.Publisher == publisherOf(client.Path);
    }

    public void Approve(ClientProcess client)
    {
        if (client.Path is null)
        {
            approvedThisRun[client.Pid] = true;
            return;
        }

        var record = new ClientRecord(client.Path, publisherOf(client.Path), client.Name ?? Path.GetFileNameWithoutExtension(client.Path), DateTimeOffset.Now);
        config.Update(c => c with
        {
            Clients = c.Clients.Where(existing => !SamePath(existing.Path, client.Path)).Append(record).ToList(),
        });
    }

    public string? PublisherOf(string path) => publisherOf(path);

    public void Remove(string path) =>
        config.Update(c => c with { Clients = c.Clients.Where(existing => !SamePath(existing.Path, path)).ToList() });

    static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
