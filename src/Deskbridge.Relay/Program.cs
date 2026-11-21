using System.Reflection;
using Deskbridge.Core;
using Deskbridge.Core.Wire;

namespace Deskbridge.Relay;

/// <summary>
/// deskbridge.exe: what every MCP client starts. "deskbridge mcp" connects stdio to the user's broker.
/// Only MCP bytes go to stdout; messages for people go to stderr.
/// </summary>
internal static class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args is ["--version"])
        {
            Console.Out.WriteLine(Version());
            return 0;
        }

        if (args.Length == 0 || args[0] != "mcp")
        {
            Console.Error.WriteLine("Usage: deskbridge mcp [--code-mode]");
            return 2;
        }

        var mode = args.Contains("--code-mode") ? HelloModes.Code : HelloModes.Classic;
        var sid = Names.CurrentUserSid();
        var pipeName = Names.PipeName(sid, Environment.GetEnvironmentVariable(Names.PipeSuffixVariable));
        var brokerPath = Environment.GetEnvironmentVariable(Names.BrokerPathVariable)
            ?? Path.Combine(AppContext.BaseDirectory, Names.BrokerExe);

        System.IO.Pipes.NamedPipeClientStream pipe;
        try
        {
            pipe = await BrokerLauncher.ConnectOrStartAsync(pipeName, brokerPath, TimeSpan.FromSeconds(5), CancellationToken.None);
        }
        catch (Exception ex) when (ex is TimeoutException or FileNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Deskbridge could not reach its broker: " + ex.Message);
            return 1;
        }

        await using (pipe)
        {
            var client = ParentProcess.Get(Environment.ProcessId) ?? new ClientProcess(0, null, null);
            HelloFrame.Write(pipe, new ClientHello(HelloFrame.CurrentVersion, mode, client));
            await using var stdin = Console.OpenStandardInput();
            await using var stdout = Console.OpenStandardOutput();
            await Pump.RunAsync(stdin, stdout, pipe, CancellationToken.None);
        }

        return 0;
    }

    static string Version() =>
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";
}
