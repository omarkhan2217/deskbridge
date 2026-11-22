using Deskbridge.Core.Tools;
using Deskbridge.E2E.Fixtures;
using Deskbridge.E2E.HandsOff;
using Deskbridge.Engines.Uia;

namespace Deskbridge.E2E;

public sealed class UiActionsTests : IDisposable
{
    readonly UiaWorker worker = new();
    readonly UiaActions ui;

    public UiActionsTests() => ui = new UiaActions(worker);

    public void Dispose() => worker.Dispose();

    static string Title(string kind) => $"dbe2e-{kind}-{Guid.NewGuid().ToString("N")[..8]}";

    async Task<string> ReadAsync(nint window, string control) => (await new UiaReader(worker).ReadTextAsync(window, control)).Text;

    [Fact]
    public async Task Press_invokes_a_button_in_a_background_window()
    {
        using var app = TargetApp.Start(Title("press"), "--text", "x");
        var save = await ui.DescribeAsync(app.Handle, "SaveButton");
        var done = await HandsOffProbe.AssertUntouchedAsync(() => ui.PressAsync(app.Handle, save));
        Assert.Equal(Rung.UiAutomation, done.Rung);
        Assert.Equal("Saved", await ReadAsync(app.Handle, "StatusLabel"));
    }

    [Fact]
    public async Task Toggle_sets_a_checkbox_and_leaves_it_when_already_set()
    {
        using var app = TargetApp.Start(Title("toggle"), "--text", "x");
        var bold = await ui.DescribeAsync(app.Handle, "BoldCheck");
        var first = await HandsOffProbe.AssertUntouchedAsync(() => ui.ToggleAsync(app.Handle, bold, on: true));
        Assert.Equal("on", first.State);
        var second = await ui.ToggleAsync(app.Handle, await ui.DescribeAsync(app.Handle, "BoldCheck"), on: true);
        Assert.Equal("on", second.State);
    }

    [Fact]
    public async Task Select_picks_a_list_item()
    {
        using var app = TargetApp.Start(Title("select"), "--text", "x");
        var large = await ui.DescribeAsync(app.Handle, "Large");
        await HandsOffProbe.AssertUntouchedAsync(() => ui.SelectAsync(app.Handle, large));
        Assert.Equal(["Large"], await ui.SelectedAsync(app.Handle, "SizeList"));
    }

    [Fact]
    public async Task Set_text_writes_a_text_box()
    {
        using var app = TargetApp.Start(Title("settext"), "--text", "old");
        var notes = await ui.DescribeAsync(app.Handle, "Notes");
        await HandsOffProbe.AssertUntouchedAsync(() => ui.SetTextAsync(app.Handle, notes, "typed by deskbridge"));
        Assert.Equal("typed by deskbridge", await ReadAsync(app.Handle, "Notes"));
    }

    [Fact]
    public async Task Set_text_refuses_a_password_field()
    {
        using var app = TargetApp.Start(Title("pwset"), "--text", "x", "--password");
        await Assert.ThrowsAsync<SecretFieldException>(() => ui.DescribeAsync(app.Handle, "Secret"));
    }

    [Fact]
    public async Task Press_refuses_a_control_that_changed()
    {
        using var app = TargetApp.Start(Title("changed"), "--text", "x", "--rename-after-ms", "1500");
        var save = await ui.DescribeAsync(app.Handle, "SaveButton");
        await Task.Delay(2_500); // the app renames the button to "Save As"
        var ex = await Assert.ThrowsAsync<ElementChangedException>(() => ui.PressAsync(app.Handle, save));
        Assert.Contains("changed since it was found", ex.Message);
        Assert.Equal("Not saved", await ReadAsync(app.Handle, "StatusLabel"));
    }

    [Fact]
    public async Task Legacy_button_falls_back_to_a_window_message()
    {
        using var app = TargetApp.Start(Title("legacy"), "--text", "x", "--native-button");
        await Task.Delay(500);
        var legacy = await ui.DescribeAsync(app.Handle, "Legacy Save");
        var done = await HandsOffProbe.AssertUntouchedAsync(() => ui.PressAsync(app.Handle, legacy));
        Assert.Equal(Rung.WindowMessage, done.Rung);
        Assert.Equal("Legacy clicked", await ReadAsync(app.Handle, "Legacy clicked"));
    }

    [Fact]
    public async Task Tree_stays_within_limits()
    {
        using var app = TargetApp.Start(Title("tree"), "--text", "x");
        var tree = await HandsOffProbe.AssertUntouchedAsync(() => ui.TreeAsync(app.Handle, maxDepth: 3, maxNodes: 5));
        Assert.True(tree.Count <= 5, $"{tree.Count} nodes");
        Assert.True(tree.Truncated);
        var all = await ui.TreeAsync(app.Handle, maxDepth: 4, maxNodes: 300);
        Assert.Contains(all.Nodes, n => n.AutomationId == "SaveButton" && n.Patterns.Contains("invoke"));
    }

    [Fact]
    public async Task Tree_nodes_name_their_parent()
    {
        using var app = TargetApp.Start(Title("parent"), "--text", "x");
        var tree = await ui.TreeAsync(app.Handle, maxDepth: 4, maxNodes: 300);
        var list = Assert.Single(tree.Nodes, n => n.AutomationId == "SizeList");
        var medium = Assert.Single(tree.Nodes, n => n.Name == "Medium");
        Assert.Equal(list.ElementId, medium.Parent);
        Assert.Null(list.Parent); // a direct child of the window
    }

    [Fact]
    public async Task Find_matches_part_of_a_name_and_lists_exact_matches_first()
    {
        using var app = TargetApp.Start(Title("partial"), "--text", "x");
        var found = await ui.FindAsync(app.Handle, name: "save", automationId: null, controlType: null);
        Assert.Equal("SaveButton", found[0].AutomationId);
        Assert.Contains(found, e => e.Name == "Not saved");
    }

    [Fact]
    public async Task Find_returns_element_ids_that_actions_accept()
    {
        using var app = TargetApp.Start(Title("find"), "--text", "x");
        var found = Assert.Single(await ui.FindAsync(app.Handle, name: "Save", automationId: null, controlType: "button"));
        await ui.PressAsync(app.Handle, await ui.DescribeAsync(app.Handle, found.ElementId));
        Assert.Equal("Saved", await ReadAsync(app.Handle, "StatusLabel"));
    }

    [Fact]
    public async Task Minimized_store_app_gives_a_plain_message()
    {
        using var calc = RealApp.Start("calc.exe", "", titleContains: "Calculator", processName: "CalculatorApp");
        await Task.Delay(1_500);
        calc.MinimizeAndActivateNext();
        var ex = await Assert.ThrowsAsync<MinimizedStoreAppException>(() => ui.DescribeAsync(calc.Handle, "CalculatorResults"));
        Assert.Contains("minimized", ex.Message);
    }
}
