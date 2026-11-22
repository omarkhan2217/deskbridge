using System.Diagnostics;
using System.Text.Json;
using Deskbridge.Core.Apps;
using Deskbridge.Core.Policy;
using Deskbridge.E2E.Fixtures;
using Deskbridge.E2E.HandsOff;
using Deskbridge.Engines.Os;
using Deskbridge.Engines.Uia;
using Microsoft.Win32;
using ModelContextProtocol.Protocol;

namespace Deskbridge.E2E.Gates;

/// <summary>
/// The Phase 1 gate (docs/phases/phase-1-core.md, Task 12), through the published relay and broker:
/// rename a file, read Notepad, and turn "Show seconds in system tray clock" on and off in Settings (D37).
/// Every agent call, with its confirmation answered through UI Automation, changes nothing the user owns.
/// </summary>
public sealed class Phase1GateTests : IDisposable
{
    const string AdvancedKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    readonly UiaWorker answerer = new();
    readonly string suffix = RelayHarness.NewSuffix();
    readonly string dir = Path.Combine(Path.GetTempPath(), "deskbridge-gate-" + Guid.NewGuid().ToString("N")[..8]);

    public Phase1GateTests() => Directory.CreateDirectory(dir);

    public void Dispose()
    {
        answerer.Dispose();
        Directory.Delete(dir, recursive: true);
    }

    static string Text(CallToolResult r) => string.Concat(r.Content.OfType<TextContentBlock>().Select(t => t.Text));

    /// <summary>Starts a call, answers Deskbridge's confirmation through UI Automation, and returns the result.</summary>
    async Task<CallToolResult> CallAndAllowAsync(RelayHarness relay, string tool, Dictionary<string, object?> args)
    {
        var call = relay.Client.CallToolAsync(tool, args).AsTask();
        await ConfirmationAnswerer.AnswerAsync(answerer, "AllowButton", TimeSpan.FromSeconds(20));
        var result = await call;
        Assert.True(result.IsError != true, $"{tool} failed: {Text(result)}");
        return result;
    }

    [Fact]
    public async Task Rename_a_file_with_a_confirmation_and_no_cursor_or_focus_change()
    {
        new ConfigStore(RelayHarness.ConfigFor(suffix)).Update(c => c.WithLevel(AppId.Files, PermissionLevel.Act));
        await using var relay = await RelayHarness.ConnectAsync(suffix: suffix);
        var from = Path.Combine(dir, "notes.txt");
        var to = Path.Combine(dir, "plan.txt");
        await File.WriteAllTextAsync(from, "gate");

        await HandsOffProbe.AssertUntouchedOnceAsync(() =>
            CallAndAllowAsync(relay, "files_move", new() { ["from"] = from, ["to"] = to }));

        Assert.False(File.Exists(from));
        Assert.Equal("gate", await File.ReadAllTextAsync(to));
    }

    [Fact]
    public async Task Read_a_notepad_document_with_no_cursor_or_focus_change()
    {
        var marker = "deskbridge-gate-" + Guid.NewGuid().ToString("N")[..8];
        var file = Path.Combine(dir, marker + ".txt");
        await File.WriteAllTextAsync(file, marker);
        using var notepad = RealApp.Start("notepad.exe", $"\"{file}\"", titleContains: marker, processName: "Notepad");
        notepad.MinimizeAndActivateNext();
        await using var relay = await RelayHarness.ConnectAsync(suffix: suffix);

        var result = await HandsOffProbe.AssertUntouchedAsync(() =>
            relay.Client.CallToolAsync("ui_read", new Dictionary<string, object?> { ["window"] = WindowHandles.Format(notepad.Handle) }).AsTask());

        Assert.True(result.IsError != true, Text(result));
        Assert.Contains(marker, Text(result));
    }

    [Fact]
    public async Task Turn_clock_seconds_on_and_off_in_settings_with_no_cursor_or_focus_change()
    {
        var original = Registry.GetValue(AdvancedKey, "ShowSecondsInSystemClock", null);
        Assert.Empty(Process.GetProcessesByName("Notepad")); // the cover window must be the test's own
        var cover = Path.Combine(dir, "cover.txt");
        await File.WriteAllTextAsync(cover, "in front of Settings");
        try
        {
            // Setup: open Settings at Date & time (where Windows 11 25H2 keeps the clock seconds switch, D42), then put Notepad in front of it.
            Process.Start(new ProcessStartInfo("ms-settings:dateandtime") { UseShellExecute = true })?.Dispose();
            var settings = await WaitForWindowAsync("Settings");
            using var notepad = RealApp.Start("notepad.exe", $"\"{cover}\"", titleContains: "cover", processName: "Notepad");
            await Task.Delay(1_500);
            new ConfigStore(RelayHarness.ConfigFor(suffix)).Update(c => c.WithLevel(AppResolver.FromWindow(settings), PermissionLevel.Act));
            await using var relay = await RelayHarness.ConnectAsync(suffix: suffix);
            var window = WindowHandles.Format(settings);

            var toggle = await FindSecondsToggleAsync(relay, window);
            if (toggle is null)
            {
                // The switch is inside the collapsed "Show more settings" of the System tray time and date group.
                var expander = await FindExpanderAsync(relay, window, "Show time and date in the System tray")
                    ?? throw new InvalidOperationException("No 'Show more settings' under 'Show time and date in the System tray' in Settings.");
                await HandsOffProbe.AssertUntouchedOnceAsync(() =>
                    CallAndAllowAsync(relay, "ui_press", new() { ["window"] = window, ["element"] = expander }));
                await Task.Delay(1_000);
                toggle = await FindSecondsToggleAsync(relay, window);
            }

            Assert.NotNull(toggle);
            await HandsOffProbe.AssertUntouchedOnceAsync(() =>
                CallAndAllowAsync(relay, "ui_toggle", new() { ["window"] = window, ["element"] = toggle, ["on"] = true }));
            Assert.Equal(1, await RegistryValueAsync(expected: 1));

            await HandsOffProbe.AssertUntouchedOnceAsync(() =>
                CallAndAllowAsync(relay, "ui_toggle", new() { ["window"] = window, ["element"] = toggle, ["on"] = false }));
            Assert.Equal(0, await RegistryValueAsync(expected: 0));

            NativeTest.PostMessageW(settings, 0x0010 /* WM_CLOSE */, 0, 0);
        }
        finally
        {
            if (original is int value)
            {
                Registry.SetValue(AdvancedKey, "ShowSecondsInSystemClock", value, RegistryValueKind.DWord);
            }
        }
    }

    static async Task<nint> WaitForWindowAsync(string title)
    {
        for (var i = 0; i < 100; i++)
        {
            var match = WindowLister.List().FirstOrDefault(w => w.Title == title);
            if (match is not null)
            {
                return match.Handle;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"No window titled '{title}' appeared.");
    }

    static Task<string?> FindSecondsToggleAsync(RelayHarness relay, string window) => FindInTreeAsync(relay, window, "Show seconds", "toggle");

    static async Task<string?> FindInTreeAsync(RelayHarness relay, string window, string nameContains, string pattern)
    {
        var nodes = await TreeAsync(relay, window);
        return nodes
            .Where(n => n.GetProperty("name").GetString()?.Contains(nameContains, StringComparison.OrdinalIgnoreCase) == true)
            .Where(n => HasPattern(n, pattern))
            .Select(n => n.GetProperty("element_id").GetString())
            .FirstOrDefault();
    }

    /// <summary>The expandable control held by the group with this name, found as an agent would: through each node's parent.</summary>
    static async Task<string?> FindExpanderAsync(RelayHarness relay, string window, string groupName)
    {
        var nodes = await TreeAsync(relay, window);
        var groups = nodes.Where(n => n.GetProperty("control_type").GetString() == "Group" && n.GetProperty("name").GetString() == groupName)
            .Select(n => n.GetProperty("element_id").GetString())
            .ToHashSet();
        return nodes
            .Where(n => n.GetProperty("parent").GetString() is { } parent && groups.Contains(parent) && HasPattern(n, "expand"))
            .Select(n => n.GetProperty("element_id").GetString())
            .FirstOrDefault();
    }

    static bool HasPattern(JsonElement node, string pattern) => node.GetProperty("patterns").EnumerateArray().Any(p => p.GetString() == pattern);

    static async Task<List<JsonElement>> TreeAsync(RelayHarness relay, string window)
    {
        var tree = await relay.Client.CallToolAsync("ui_tree", new Dictionary<string, object?> { ["window"] = window, ["max_depth"] = 30, ["max_nodes"] = 1000 });
        Assert.True(tree.IsError != true, "ui_tree failed: " + Text(tree));
        using var doc = JsonDocument.Parse(Text(tree));
        return doc.RootElement.GetProperty("nodes").EnumerateArray().Select(n => n.Clone()).ToList();
    }

    static async Task<string> FindByNameAsync(RelayHarness relay, string window, string name)
    {
        var found = await relay.Client.CallToolAsync("ui_find", new Dictionary<string, object?> { ["window"] = window, ["name"] = name });
        Assert.True(found.IsError != true, "ui_find failed: " + Text(found));
        using var doc = JsonDocument.Parse(Text(found));
        return doc.RootElement.GetProperty("found")[0].GetProperty("element_id").GetString()!;
    }

    static async Task<int?> RegistryValueAsync(int expected)
    {
        int? value = null;
        for (var i = 0; i < 30 && value != expected; i++)
        {
            await Task.Delay(100);
            value = Registry.GetValue(AdvancedKey, "ShowSecondsInSystemClock", null) as int?;
        }

        return value;
    }
}
