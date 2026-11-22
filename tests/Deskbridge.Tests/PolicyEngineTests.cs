using Deskbridge.Core.Apps;
using Deskbridge.Core.Clients;
using Deskbridge.Core.Policy;
using Deskbridge.Core.Tools;
using Deskbridge.Core.Wire;

namespace Deskbridge.Tests;

public sealed class PolicyEngineTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "deskbridge-policy-" + Guid.NewGuid().ToString("N"));
    readonly ConfigStore config;
    readonly ClientRegistry clients;
    readonly PolicyEngine policy;
    static readonly AppId Notepad = AppId.ForExe(@"C:\Windows\notepad.exe", "Notepad");
    static readonly AppId Excel = AppId.ForExe(@"C:\Office\excel.exe", "Excel");
    static readonly ClientContext Cursor = Client(@"C:\Apps\Cursor.exe", "Cursor");
    static readonly ClientContext Claude = Client(@"C:\Apps\Claude.exe", "Claude");

    public PolicyEngineTests()
    {
        config = new ConfigStore(dir);
        clients = new ClientRegistry(config, _ => "Pub");
        policy = new PolicyEngine(config, clients);
        clients.Approve(Cursor.Process);
        clients.Approve(Claude.Process);
    }

    public void Dispose() => Directory.Delete(dir, recursive: true);

    static ClientContext Client(string path, string name) => new(new ClientHello(1, HelloModes.Classic, new ClientProcess(1, path, name)));

    static ToolCall Call(AppId app, ToolEffect effect, bool elevated = false) => new("ui_press", app, effect, "Press Save in Notepad", TargetElevated: elevated);

    void Set(AppId app, PermissionLevel level) => config.Update(c => c.WithLevel(app, level));

    [Fact]
    public void Paused_denies()
    {
        config.Update(c => c with { Paused = true });
        var deny = Assert.IsType<PolicyDecision.Deny>(policy.Decide(Call(Notepad, ToolEffect.Read), Cursor));
        Assert.Contains("paused", deny.Reason);
    }

    [Fact]
    public void Unapproved_client_gets_client_approval_first()
    {
        var stranger = Client(@"C:\Apps\Stranger.exe", "Stranger");
        var confirm = Assert.IsType<PolicyDecision.Confirm>(policy.Decide(Call(Notepad, ToolEffect.Read), stranger));
        Assert.Equal(ConfirmationKind.ClientApproval, confirm.Request.Kind);
        Assert.Contains("Stranger wants to use Deskbridge", confirm.Request.Action);
        Assert.Contains(@"C:\Apps\Stranger.exe", confirm.Request.Content);
    }

    [Fact]
    public void Elevated_window_is_never_available()
    {
        Set(Notepad, PermissionLevel.Act);
        var deny = Assert.IsType<PolicyDecision.Deny>(policy.Decide(Call(Notepad, ToolEffect.Read, elevated: true), Cursor));
        Assert.Contains("administrator", deny.Reason);
    }

    [Fact]
    public void Authenticator_apps_are_never_available_even_at_act()
    {
        var authy = AppId.ForExe(@"C:\Users\me\AppData\Local\authy\Authy Desktop.exe", "Authy Desktop");
        Set(authy, PermissionLevel.Act);
        var deny = Assert.IsType<PolicyDecision.Deny>(policy.Decide(Call(authy, ToolEffect.Read), Cursor));
        Assert.Contains("never touches", deny.Reason);
    }

    [Fact]
    public void Off_app_is_denied_with_its_reason()
    {
        Set(Notepad, PermissionLevel.Off);
        var deny = Assert.IsType<PolicyDecision.Deny>(policy.Decide(Call(Notepad, ToolEffect.Read), Cursor));
        Assert.Equal("Deskbridge is off for Notepad. Change it in Deskbridge settings.", deny.Reason);
    }

    [Fact]
    public void Read_is_allowed_in_a_read_app() =>
        Assert.Equal(new PolicyDecision.Allow(), policy.Decide(Call(Notepad, ToolEffect.Read), Cursor));

    [Fact]
    public void Change_in_an_act_app_asks_and_offers_remember()
    {
        Set(Notepad, PermissionLevel.Act);
        var confirm = Assert.IsType<PolicyDecision.Confirm>(policy.Decide(Call(Notepad, ToolEffect.Change), Cursor));
        Assert.True(confirm.Request.CanRemember);
        Assert.Equal(ConfirmationKind.ToolCall, confirm.Request.Kind);
    }

    [Fact]
    public void Change_in_a_read_app_asks_without_remember_and_says_why()
    {
        var confirm = Assert.IsType<PolicyDecision.Confirm>(policy.Decide(Call(Notepad, ToolEffect.Change), Cursor));
        Assert.False(confirm.Request.CanRemember);
        Assert.Equal("Notepad is read-only in Deskbridge. Allowing lets this one change through.", confirm.Request.Note);
    }

    [Fact]
    public void Remembered_grant_allows_change_until_revoked()
    {
        Set(Notepad, PermissionLevel.Act);
        var call = Call(Notepad, ToolEffect.Change);
        var request = ((PolicyDecision.Confirm)policy.Decide(call, Cursor)).Request;
        policy.Record(call, Cursor, request, ConfirmationAnswer.AllowAndRemember);
        Assert.Equal(new PolicyDecision.Allow(Remembered: true), policy.Decide(call, Cursor));
        config.Update(c => c with { Grants = new() });
        Assert.IsType<PolicyDecision.Confirm>(policy.Decide(call, Cursor));
    }

    [Fact]
    public void Remembered_grant_never_covers_destructive()
    {
        Set(Notepad, PermissionLevel.Act);
        var change = Call(Notepad, ToolEffect.Change);
        policy.Record(change, Cursor, ((PolicyDecision.Confirm)policy.Decide(change, Cursor)).Request, ConfirmationAnswer.AllowAndRemember);
        var confirm = Assert.IsType<PolicyDecision.Confirm>(policy.Decide(Call(Notepad, ToolEffect.Destructive), Cursor));
        Assert.False(confirm.Request.CanRemember);
    }

    [Fact]
    public void Grant_is_per_client_and_per_app()
    {
        Set(Notepad, PermissionLevel.Act);
        Set(Excel, PermissionLevel.Act);
        var call = Call(Notepad, ToolEffect.Change);
        policy.Record(call, Cursor, ((PolicyDecision.Confirm)policy.Decide(call, Cursor)).Request, ConfirmationAnswer.AllowAndRemember);
        Assert.IsType<PolicyDecision.Confirm>(policy.Decide(call, Claude));
        Assert.IsType<PolicyDecision.Confirm>(policy.Decide(Call(Excel, ToolEffect.Change), Cursor));
    }

    [Fact]
    public void Destructive_needs_act()
    {
        var deny = Assert.IsType<PolicyDecision.Deny>(policy.Decide(Call(Notepad, ToolEffect.Destructive), Cursor));
        Assert.Contains("needs Act", deny.Reason);
    }

    [Fact]
    public void Pausing_denies_a_call_allowed_a_moment_before()
    {
        var call = Call(Notepad, ToolEffect.Read);
        Assert.IsType<PolicyDecision.Allow>(policy.Decide(call, Cursor));
        config.Update(c => c with { Paused = true });
        Assert.IsType<PolicyDecision.Deny>(policy.Decide(call, Cursor));
    }

    [Fact]
    public void Allowing_a_client_approval_approves_the_client()
    {
        var stranger = Client(@"C:\Apps\Stranger.exe", "Stranger");
        var call = Call(Notepad, ToolEffect.Read);
        var request = ((PolicyDecision.Confirm)policy.Decide(call, stranger)).Request;
        policy.Record(call, stranger, request, ConfirmationAnswer.Allow);
        Assert.IsType<PolicyDecision.Allow>(policy.Decide(call, stranger));
    }

    [Theory]
    [InlineData("Delete", true)]
    [InlineData("Send message", true)]
    [InlineData("Don't Save", true)]
    [InlineData("Empty Recycle Bin", true)]
    [InlineData("Uninstall", true)]
    [InlineData("Save", false)]
    [InlineData("Notes", false)]
    [InlineData("Sender name", false)]
    [InlineData("Removing old files", true)]
    [InlineData("Payment details", false)]
    public void Destructive_word_on_a_control_makes_the_call_destructive(string controlName, bool destructive) =>
        Assert.Equal(destructive, DestructiveWords.IsDestructive(controlName));

    [Theory]
    [InlineData(null, "DeleteButton", null, true, true)]
    [InlineData("", "btnRemove_Item", null, true, true)]
    [InlineData(null, null, "Deletes the selected rows", true, true)]
    [InlineData(null, null, null, true, true)]
    [InlineData("", " ", "", true, true)]
    [InlineData(null, null, null, false, false)]
    [InlineData(null, "SaveButton", null, true, false)]
    [InlineData("Save", "SaveButton", "Saves the file", true, false)]
    public void A_control_is_judged_by_its_name_id_and_help_text(string? name, string? automationId, string? helpText, bool pressing, bool destructive) =>
        Assert.Equal(destructive, DestructiveWords.IsDestructiveControl(name, automationId, helpText, pressing));
}
