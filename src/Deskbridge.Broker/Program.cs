using System.Globalization;
using Deskbridge.Broker;
using Deskbridge.Core;
using Deskbridge.Core.Audit;
using Deskbridge.Core.Clients;
using Deskbridge.Core.Policy;
using Deskbridge.Core.Tools;
using Deskbridge.Engines.Os;
using Deskbridge.Engines.Uia;
using Deskbridge.Mcp;
using Deskbridge.Ui;

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

var configOverride = Names.ConfigOverride();
var config = new ConfigStore(Names.ConfigDirectory(configOverride));
var clients = new ClientRegistry(config, Publisher.Of);
using var ui = new UiThread();
var pipeline = new ToolPipeline(
    new PolicyEngine(config, clients),
    new ConfirmationPresenter(ui, ConfirmationPresenter.DefaultLimit),
    new AuditLog(Names.LogDirectory(configOverride), () => config.Current.ContentLogging, TimeProvider.System),
    TimeProvider.System);

using var quit = new CancellationTokenSource();
using var tray = await Tray.StartAsync(ui, config, Names.LogDirectory(configOverride), onQuit: quit.Cancel);
using var services = new ToolServices(pipeline, config, () => new UiaWorker(), notifier: tray);
var host = new BrokerHost(
    new BrokerOptions(Names.PipeName(sid, suffix), idleExit, BrokerOptions.DefaultHelloTimeout),
    services,
    keepRunning: () => config.Current.Pinned);
await using (host)
{
    try
    {
        await host.RunAsync(quit.Token);
    }
    catch (OperationCanceledException)
    {
        // Quit from the tray.
    }
}

return 0;
