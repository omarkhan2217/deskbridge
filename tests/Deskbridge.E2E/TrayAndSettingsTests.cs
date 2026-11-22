using Deskbridge.Core.Apps;
using Deskbridge.Core.Policy;
using Deskbridge.E2E.HandsOff;
using Deskbridge.Engines.Uia;
using Deskbridge.Ui;
using FlaUI.Core.AutomationElements;

namespace Deskbridge.E2E;

public sealed class TrayAndSettingsTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "deskbridge-e2e-tray-" + Guid.NewGuid().ToString("N"));
    readonly UiThread ui = new();
    readonly ConfigStore config;
    readonly UiaWorker worker = new();

    public TrayAndSettingsTests() => config = new ConfigStore(dir);

    public void Dispose()
    {
        worker.Dispose();
        ui.Dispose();
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Pause_from_the_tray_pauses_and_resumes()
    {
        using var tray = await Tray.StartAsync(ui, config, dir, onQuit: () => { });
        await tray.TogglePauseAsync();
        Assert.True(config.Current.Paused);
        await tray.TogglePauseAsync();
        Assert.False(config.Current.Paused);
    }

    [Fact]
    public async Task The_hotkey_message_toggles_pause()
    {
        using var tray = await Tray.StartAsync(ui, config, dir, onQuit: () => { });
        Assert.NotEqual(0, tray.HotkeyWindow);
        NativeTest.PostMessageW(tray.HotkeyWindow, 0x0312 /* WM_HOTKEY */, Tray.HotkeyId, 0);
        for (var i = 0; i < 20 && !config.Current.Paused; i++)
        {
            await Task.Delay(100);
        }

        Assert.True(config.Current.Paused);
    }

    [Fact]
    public async Task Revoking_a_grant_in_settings_removes_it()
    {
        config.Update(c => c with { Grants = [new Grant(@"c:\apps\cursor.exe", @"c:\windows\notepad.exe", DateTimeOffset.Now)] });
        using var tray = await Tray.StartAsync(ui, config, dir, onQuit: () => { });
        var window = await tray.OpenSettingsAsync();
        await worker.RunAsync(automation =>
        {
            var root = automation.FromHandle(window);
            var cf = root.ConditionFactory;
            root.FindFirstDescendant(cf.ByName("Remembered"))!.Patterns.SelectionItem.Pattern.Select();
            root.FindFirstDescendant(cf.ByAutomationId("GrantList"))!
                .FindFirstDescendant(cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem))!
                .Patterns.SelectionItem.Pattern.Select();
            root.FindFirstDescendant(cf.ByAutomationId("RevokeButton"))!.AsButton().Invoke();
            return 0;
        });
        for (var i = 0; i < 20 && config.Current.Grants.Count > 0; i++)
        {
            await Task.Delay(100);
        }

        Assert.Empty(config.Current.Grants);
        await tray.CloseSettingsAsync();
    }

    [Fact]
    public async Task Setting_an_app_level_in_settings_is_saved()
    {
        config.Update(c => c.WithLevel(AppId.ForExe(@"C:\Windows\notepad.exe", "Notepad"), PermissionLevel.Read));
        using var tray = await Tray.StartAsync(ui, config, dir, onQuit: () => { });
        var window = await tray.OpenSettingsAsync();
        await worker.RunAsync(automation =>
        {
            var root = automation.FromHandle(window);
            var cf = root.ConditionFactory;
            root.FindFirstDescendant(cf.ByAutomationId("AppList"))!
                .FindFirstDescendant(cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem))!
                .Patterns.SelectionItem.Pattern.Select();
            root.FindFirstDescendant(cf.ByAutomationId("ActButton"))!.AsButton().Invoke();
            return 0;
        });
        for (var i = 0; i < 20 && config.Level(AppId.ForExe(@"C:\Windows\notepad.exe", "Notepad")) != PermissionLevel.Act; i++)
        {
            await Task.Delay(100);
        }

        Assert.Equal(PermissionLevel.Act, config.Level(AppId.ForExe(@"C:\Windows\notepad.exe", "Notepad")));
        await tray.CloseSettingsAsync();
    }
}
