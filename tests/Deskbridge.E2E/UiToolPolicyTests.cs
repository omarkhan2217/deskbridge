using System.IO.Pipes;
using Deskbridge.Core.Audit;
using Deskbridge.Core.Clients;
using Deskbridge.Core.Policy;
using Deskbridge.Core.Tools;
using Deskbridge.Core.Wire;
using Deskbridge.E2E.Fixtures;
using Deskbridge.Engines.Os;
using Deskbridge.Engines.Uia;
using Deskbridge.Mcp;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Deskbridge.E2E;

/// <summary>The UI tools through a real MCP session, the real policy, and a presenter that answers like a user.</summary>
public sealed class UiToolPolicyTests : IAsyncDisposable
{
    readonly string configDir = Path.Combine(Path.GetTempPath(), "deskbridge-e2e-uipolicy-" + Guid.NewGuid().ToString("N"));
    readonly Answers presenter = new();
    readonly ConfigStore config;
    readonly ToolServices services;
    NamedPipeServerStream? serverPipe;
    NamedPipeClientStream? clientPipe;
    McpClient? client;

    public UiToolPolicyTests()
    {
        config = new ConfigStore(configDir);
        var clients = new ClientRegistry(config, _ => null);
        var hello = new ClientProcess(Environment.ProcessId, null, "e2e");
        clients.Approve(hello);
        services = new ToolServices(new ToolPipeline(new PolicyEngine(config, clients), presenter, new NoAudit(), TimeProvider.System), config, () => new UiaWorker());
    }

    sealed class Answers : IConfirmationPresenter
    {
        public Queue<ConfirmationAnswer> Next { get; } = new();

        public List<ConfirmationRequest> Asked { get; } = new();

        public Task<ConfirmationAnswer> AskAsync(ConfirmationRequest request, CancellationToken ct)
        {
            Asked.Add(request);
            return Task.FromResult(Next.Dequeue());
        }
    }

    sealed class NoAudit : IAuditLog
    {
        public void Append(AuditEntry entry)
        {
        }
    }

    async Task<McpClient> ConnectAsync()
    {
        var name = "deskbridge-e2e-" + Guid.NewGuid().ToString("N");
        serverPipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        clientPipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await Task.WhenAll(serverPipe.WaitForConnectionAsync(), clientPipe.ConnectAsync());
        var server = McpSessionFactory.Create(serverPipe, new ClientHello(1, HelloModes.Classic, new ClientProcess(Environment.ProcessId, null, "e2e")), services);
        _ = server.RunAsync();
        client = await McpClient.CreateAsync(new StreamClientTransport(clientPipe, clientPipe));
        return client;
    }

    static string Text(CallToolResult r) => string.Concat(r.Content.OfType<TextContentBlock>().Select(t => t.Text));

    [Fact]
    public async Task A_delete_button_always_asks_and_offers_no_remember()
    {
        using var app = TargetApp.Start("dbe2e-del-" + Guid.NewGuid().ToString("N")[..8], "--text", "x");
        config.Update(c => c.WithLevel(AppResolver.FromWindow(app.Handle), PermissionLevel.Act));
        var mcp = await ConnectAsync();
        presenter.Next.Enqueue(ConfirmationAnswer.Deny);

        var result = await mcp.CallToolAsync("ui_press", new Dictionary<string, object?> { ["window"] = WindowHandles.Format(app.Handle), ["element"] = "DeleteButton" });

        Assert.True(result.IsError);
        Assert.Contains("denied", Text(result));
        var asked = Assert.Single(presenter.Asked);
        Assert.Equal((ToolEffect.Destructive, false), (asked.Effect, asked.CanRemember));
        Assert.Contains("'Delete'", asked.Action);
    }

    [Fact]
    public async Task Allow_and_remember_stops_the_next_question_for_the_same_app()
    {
        using var app = TargetApp.Start("dbe2e-rem-" + Guid.NewGuid().ToString("N")[..8], "--text", "x");
        config.Update(c => c.WithLevel(AppResolver.FromWindow(app.Handle), PermissionLevel.Act));
        var mcp = await ConnectAsync();
        var args = new Dictionary<string, object?> { ["window"] = WindowHandles.Format(app.Handle), ["element"] = "SaveButton" };

        presenter.Next.Enqueue(ConfirmationAnswer.AllowAndRemember);
        Assert.NotEqual(true, (await mcp.CallToolAsync("ui_press", args)).IsError);
        Assert.NotEqual(true, (await mcp.CallToolAsync("ui_press", args)).IsError);

        Assert.Single(presenter.Asked);
    }

    [Fact]
    public async Task A_control_that_cannot_be_pressed_is_refused_before_anyone_is_asked()
    {
        using var app = TargetApp.Start("dbe2e-nopress-" + Guid.NewGuid().ToString("N")[..8], "--text", "x");
        config.Update(c => c.WithLevel(AppResolver.FromWindow(app.Handle), PermissionLevel.Act));
        var mcp = await ConnectAsync();

        var result = await mcp.CallToolAsync("ui_press", new Dictionary<string, object?> { ["window"] = WindowHandles.Format(app.Handle), ["element"] = "StatusLabel" });

        Assert.True(result.IsError);
        Assert.Contains("no way to press", Text(result));
        Assert.Empty(presenter.Asked);
    }

    [Fact]
    public async Task A_switch_already_as_asked_is_left_alone_without_asking()
    {
        using var app = TargetApp.Start("dbe2e-same-" + Guid.NewGuid().ToString("N")[..8], "--text", "x");
        config.Update(c => c.WithLevel(AppResolver.FromWindow(app.Handle), PermissionLevel.Act));
        var mcp = await ConnectAsync();

        var result = await mcp.CallToolAsync("ui_toggle", new Dictionary<string, object?> { ["window"] = WindowHandles.Format(app.Handle), ["element"] = "BoldCheck", ["on"] = false });

        Assert.True(result.IsError != true, Text(result));
        Assert.Contains("\"state\":\"off\"", Text(result));
        Assert.Contains("\"how\":\"unchanged\"", Text(result));
        Assert.Empty(presenter.Asked);
    }

    [Fact]
    public async Task An_app_that_is_off_is_refused_before_its_controls_are_read()
    {
        using var app = TargetApp.Start("dbe2e-off-" + Guid.NewGuid().ToString("N")[..8], "--text", "x");
        config.Update(c => c.WithLevel(AppResolver.FromWindow(app.Handle), PermissionLevel.Off));
        var mcp = await ConnectAsync();

        // StatusLabel cannot be pressed; if Deskbridge looked at it first, the answer would say so.
        var result = await mcp.CallToolAsync("ui_press", new Dictionary<string, object?> { ["window"] = WindowHandles.Format(app.Handle), ["element"] = "StatusLabel" });

        Assert.True(result.IsError);
        Assert.Contains("Deskbridge is off for", Text(result));
        Assert.Empty(presenter.Asked);
    }

    [Fact]
    public async Task Ui_find_and_ui_tree_answer_in_json_with_element_ids()
    {
        using var app = TargetApp.Start("dbe2e-json-" + Guid.NewGuid().ToString("N")[..8], "--text", "x");
        var mcp = await ConnectAsync();
        var window = WindowHandles.Format(app.Handle);

        var found = await mcp.CallToolAsync("ui_find", new Dictionary<string, object?> { ["window"] = window, ["name"] = "Save" });
        Assert.True(found.IsError != true, Text(found));
        using (var doc = System.Text.Json.JsonDocument.Parse(Text(found)))
        {
            var first = doc.RootElement.GetProperty("found")[0];
            Assert.StartsWith(window + ":", first.GetProperty("element_id").GetString());
            Assert.Equal(window, first.GetProperty("window").GetString());
        }

        var tree = await mcp.CallToolAsync("ui_tree", new Dictionary<string, object?> { ["window"] = window });
        Assert.True(tree.IsError != true, Text(tree));
        using (var doc = System.Text.Json.JsonDocument.Parse(Text(tree)))
        {
            Assert.Contains(doc.RootElement.GetProperty("nodes").EnumerateArray(), n => n.GetProperty("automation_id").GetString() == "SaveButton");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (client is not null)
        {
            await client.DisposeAsync();
        }

        clientPipe?.Dispose();
        serverPipe?.Dispose();
        services.Dispose();
        if (Directory.Exists(configDir))
        {
            Directory.Delete(configDir, recursive: true);
        }
    }
}
