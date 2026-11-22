using System.Text.Json;
using Deskbridge.Core.Audit;
using Deskbridge.Core.Clients;
using Deskbridge.Core.Policy;
using Deskbridge.Core.Tools;
using Deskbridge.Core.Wire;
using Deskbridge.Engines.Os;

namespace Deskbridge.Tests;

sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

public sealed class AuditLogTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "deskbridge-audit-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    static AuditEntry Entry(DateTimeOffset at) =>
        new(at, "Cursor", "Notepad", "ui_set_text", ToolEffect.Change, "allowed_by_user", Rung.UiAutomation, "ok", 42, "secret words");

    [Fact]
    public void Entry_fields_are_exact()
    {
        var at = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
        new AuditLog(dir, () => false, new FixedTime(at)).Append(Entry(at));
        var line = Assert.Single(File.ReadAllLines(Path.Combine(dir, "audit-2026-11-22.jsonl")));
        using var doc = JsonDocument.Parse(line);
        var names = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(["time", "client", "app", "tool", "effect", "decision", "rung", "result", "duration_ms"], names);
        Assert.Equal("change", doc.RootElement.GetProperty("effect").GetString());
        Assert.Equal("ui_automation", doc.RootElement.GetProperty("rung").GetString());
    }

    [Fact]
    public void Content_only_when_logging_is_on()
    {
        var at = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
        var on = false;
        var log = new AuditLog(dir, () => on, new FixedTime(at));
        log.Append(Entry(at));
        on = true;
        log.Append(Entry(at));
        var lines = File.ReadAllLines(Path.Combine(dir, "audit-2026-11-22.jsonl"));
        Assert.DoesNotContain("secret words", lines[0]);
        Assert.Contains("\"content\":\"secret words\"", lines[1]);
    }

    [Fact]
    public void Midnight_starts_a_new_file()
    {
        var time = new FixedTime(new DateTimeOffset(2026, 11, 22, 23, 59, 59, TimeSpan.Zero));
        var log = new AuditLog(dir, () => false, time);
        log.Append(Entry(time.Now));
        time.Now = time.Now.AddSeconds(2);
        log.Append(Entry(time.Now));
        Assert.Equal(["audit-2026-11-22.jsonl", "audit-2026-11-23.jsonl"], Directory.GetFiles(dir).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void Locked_file_never_fails_the_call()
    {
        var at = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
        Directory.CreateDirectory(dir);
        using var locked = new FileStream(Path.Combine(dir, "audit-2026-11-22.jsonl"), FileMode.Create, FileAccess.Write, FileShare.None);
        var log = new AuditLog(dir, () => false, new FixedTime(at));
        log.Append(Entry(at));
        Assert.Equal(1, log.FailedWrites);
    }
}

public sealed class ClientRegistryTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "deskbridge-clients-" + Guid.NewGuid().ToString("N"));
    static readonly ClientProcess Cursor = new(10, @"C:\Apps\Cursor.exe", "Cursor");

    public void Dispose()
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Approved_client_is_not_asked_again()
    {
        var registry = new ClientRegistry(new ConfigStore(dir), _ => "Anysphere");
        Assert.False(registry.IsApproved(Cursor));
        registry.Approve(Cursor);
        Assert.True(new ClientRegistry(new ConfigStore(dir), _ => "Anysphere").IsApproved(Cursor));
    }

    [Fact]
    public void Changed_publisher_is_asked_again()
    {
        new ClientRegistry(new ConfigStore(dir), _ => "Anysphere").Approve(Cursor);
        Assert.False(new ClientRegistry(new ConfigStore(dir), _ => "Someone Else").IsApproved(Cursor));
        Assert.False(new ClientRegistry(new ConfigStore(dir), _ => null).IsApproved(Cursor));
    }

    [Fact]
    public void Client_without_a_path_is_approved_for_this_run_only()
    {
        var unknown = new ClientProcess(77, null, null);
        var registry = new ClientRegistry(new ConfigStore(dir), _ => null);
        registry.Approve(unknown);
        Assert.True(registry.IsApproved(unknown));
        Assert.False(new ClientRegistry(new ConfigStore(dir), _ => null).IsApproved(unknown));
    }

    [Fact]
    public void Remove_forgets_the_client()
    {
        var registry = new ClientRegistry(new ConfigStore(dir), _ => "Anysphere");
        registry.Approve(Cursor);
        registry.Remove(Cursor.Path!);
        Assert.False(registry.IsApproved(Cursor));
    }

    [Fact]
    public void Signed_exe_has_its_publisher_and_unsigned_has_none()
    {
        var dotnet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
        Assert.False(string.IsNullOrEmpty(Publisher.Of(dotnet))); // the .NET signer's common name is ".NET"
        Assert.Null(Publisher.Of(Path.Combine(AppContext.BaseDirectory, "Deskbridge.Tests.exe")));
    }
}
