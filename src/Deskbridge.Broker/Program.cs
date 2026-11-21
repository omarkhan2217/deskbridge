using System.Globalization;
using Deskbridge.Broker;
using Deskbridge.Core;
using Deskbridge.Engines.Uia;
using Deskbridge.Mcp;

// deskbridge-broker.exe: one per Windows user. Relays start it; it exits after IdleExit with no clients.
var sid = Names.CurrentUserSid();
var suffix = Environment.GetEnvironmentVariable(Names.PipeSuffixVariable);
using var instance = SingleInstance.TryAcquire(Names.BrokerMutex(sid, suffix));
if (instance is null)
{
    return 0; // another broker already serves this user
}

var idleExit = int.TryParse(Environment.GetEnvironmentVariable(Names.IdleExitVariable), NumberStyles.None, CultureInfo.InvariantCulture, out var ms)
    ? TimeSpan.FromMilliseconds(ms)
    : BrokerOptions.DefaultIdleExit;

using var worker = new UiaWorker();
var host = new BrokerHost(
    new BrokerOptions(Names.PipeName(sid, suffix), idleExit, BrokerOptions.DefaultHelloTimeout),
    new ToolServices(new UiaReader(worker)));
await using (host)
{
    await host.RunAsync(CancellationToken.None);
}

return 0;
