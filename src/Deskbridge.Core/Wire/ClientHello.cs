namespace Deskbridge.Core.Wire;

/// <summary>The process that started the relay: the MCP client.</summary>
public sealed record ClientProcess(int Pid, string? Path, string? Name);

/// <summary>The relay's first message to the broker. Raw MCP bytes follow it on the same pipe.</summary>
public sealed record ClientHello(int V, string Mode, ClientProcess Client, string? PairingToken = null);

public static class HelloModes
{
    public const string Classic = "classic";
    public const string Code = "code";

    public static bool IsKnown(string? mode) => mode is Classic or Code;
}
