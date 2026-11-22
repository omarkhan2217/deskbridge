using Deskbridge.Core.Audit;
using Deskbridge.Core.Clients;
using Deskbridge.Core.Policy;
using Deskbridge.Core.Tools;
using Deskbridge.Core.Wire;
using Deskbridge.Engines.Uia;
using Deskbridge.Mcp;

namespace Deskbridge.Tests;

/// <summary>Builds ToolServices for tests, each with its own config folder and an in-memory audit log.</summary>
static class TestServices
{
    /// <summary>The client every harness says hello as.</summary>
    public static readonly ClientProcess TestClient = new(Environment.ProcessId, null, "tests");

    public sealed class MemoryAudit : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = new();

        public void Append(AuditEntry entry)
        {
            lock (Entries)
            {
                Entries.Add(entry);
            }
        }
    }

    sealed class AllowEverything : IToolPolicy
    {
        public PolicyDecision Decide(ToolCall call, ClientContext client) => new PolicyDecision.Allow();

        public void Record(ToolCall call, ClientContext client, ConfirmationRequest request, ConfirmationAnswer answer)
        {
        }
    }

    sealed class NeverAsked : IConfirmationPresenter
    {
        public Task<ConfirmationAnswer> AskAsync(ConfirmationRequest request, CancellationToken ct) =>
            throw new InvalidOperationException("This test expected no confirmation: " + request.Action);
    }

    static string NewConfigDir() => Path.Combine(Path.GetTempPath(), "deskbridge-test-" + Guid.NewGuid().ToString("N"));

    /// <summary>Every call allowed, nothing asked: for tests about tools, not policy.</summary>
    public static ToolServices AllowAll(MemoryAudit? audit = null) =>
        new(new ToolPipeline(new AllowEverything(), new NeverAsked(), audit ?? new MemoryAudit(), TimeProvider.System),
            new ConfigStore(NewConfigDir()),
            () => new UiaWorker());

    /// <summary>The real policy with a fresh config; the test client is already approved.</summary>
    public static ToolServices RealPolicy(out ConfigStore config, MemoryAudit? audit = null)
    {
        config = new ConfigStore(NewConfigDir());
        var clients = new ClientRegistry(config, _ => null);
        clients.Approve(TestClient);
        return new ToolServices(
            new ToolPipeline(new PolicyEngine(config, clients), new NeverAsked(), audit ?? new MemoryAudit(), TimeProvider.System),
            config,
            () => new UiaWorker());
    }
}
