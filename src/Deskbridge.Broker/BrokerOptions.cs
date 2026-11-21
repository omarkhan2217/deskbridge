namespace Deskbridge.Broker;

/// <summary>
/// PipeName: where relays connect. IdleExit: how long the broker waits with no clients before exiting
/// (30 minutes by default). HelloTimeout: how long a new connection has to send its hello.
/// </summary>
public sealed record BrokerOptions(string PipeName, TimeSpan IdleExit, TimeSpan HelloTimeout)
{
    public static readonly TimeSpan DefaultIdleExit = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan DefaultHelloTimeout = TimeSpan.FromSeconds(5);
}
