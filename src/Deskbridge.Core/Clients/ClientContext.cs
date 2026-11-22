using Deskbridge.Core.Wire;

namespace Deskbridge.Core.Clients;

/// <summary>The MCP client behind one session, as the relay described it.</summary>
public sealed record ClientContext(ClientHello Hello)
{
    public ClientProcess Process => Hello.Client;

    /// <summary>What the user sees: the client's process name.</summary>
    public string Name => string.IsNullOrWhiteSpace(Hello.Client.Name) ? "An unknown client" : Hello.Client.Name;

    /// <summary>What grants and approvals are stored under: the lowercase exe path, or the PID when the path is unknown.</summary>
    public string Key => Hello.Client.Path is { Length: > 0 } path ? path.ToLowerInvariant() : $"pid:{Hello.Client.Pid}";
}
