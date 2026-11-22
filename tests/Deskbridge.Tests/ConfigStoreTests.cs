using Deskbridge.Core.Apps;
using Deskbridge.Core.Policy;

namespace Deskbridge.Tests;

public sealed class ConfigStoreTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "deskbridge-config-" + Guid.NewGuid().ToString("N"));
    static readonly AppId Notepad = AppId.ForExe(@"C:\Windows\notepad.exe", "Notepad");

    public ConfigStoreTests() => Directory.CreateDirectory(dir);

    public void Dispose() => Directory.Delete(dir, recursive: true);

    string ConfigFile => Path.Combine(dir, "config.json");

    [Fact]
    public void New_app_defaults_to_read() => Assert.Equal(PermissionLevel.Read, new ConfigStore(dir).Level(Notepad));

    [Theory]
    [InlineData(@"C:\Program Files\Bitwarden\Bitwarden.exe")]
    [InlineData(@"C:\Program Files\KeePassXC\KeePassXC.exe")]
    [InlineData(@"C:\Users\me\AppData\Local\1Password\app\8\1Password.exe")]
    public void Password_manager_defaults_to_off(string exe) =>
        Assert.Equal(PermissionLevel.Off, new ConfigStore(dir).Level(AppId.ForExe(exe, Path.GetFileNameWithoutExtension(exe))));

    [Fact]
    public void Notifications_default_to_act() => Assert.Equal(PermissionLevel.Act, new ConfigStore(dir).Level(AppId.Notifications));

    [Fact]
    public void Set_level_survives_a_new_store()
    {
        new ConfigStore(dir).Update(c => c.WithLevel(Notepad, PermissionLevel.Act));
        Assert.Equal(PermissionLevel.Act, new ConfigStore(dir).Level(Notepad));
    }

    [Fact]
    public void Config_file_uses_snake_case_and_word_levels()
    {
        new ConfigStore(dir).Update(c => c.WithLevel(Notepad, PermissionLevel.Act) with { StartAtSignIn = true });
        var json = File.ReadAllText(ConfigFile);
        Assert.Contains("\"start_at_sign_in\": true", json);
        Assert.Contains("\"c:\\\\windows\\\\notepad.exe\": \"act\"", json);
    }

    [Fact]
    public void Hand_edit_is_picked_up_without_restart()
    {
        var store = new ConfigStore(dir);
        Assert.Equal(PermissionLevel.Read, store.Level(Notepad));
        File.WriteAllText(ConfigFile, "{\"version\":1,\"apps\":{\"c:\\\\windows\\\\notepad.exe\":\"off\"}}");
        File.SetLastWriteTimeUtc(ConfigFile, DateTime.UtcNow.AddSeconds(5));
        Assert.Equal(PermissionLevel.Off, store.Level(Notepad));
    }

    [Fact]
    public void Malformed_config_never_grants_act()
    {
        var store = new ConfigStore(dir);
        store.Update(c => c.WithLevel(Notepad, PermissionLevel.Act));
        File.WriteAllText(ConfigFile, "{ not json");
        File.SetLastWriteTimeUtc(ConfigFile, DateTime.UtcNow.AddSeconds(5));
        Assert.Equal(PermissionLevel.Read, store.Level(Notepad));
        Assert.Contains("config.json", store.ConfigError);
    }

    [Fact]
    public void Broken_file_is_kept_aside_before_an_update()
    {
        File.WriteAllText(ConfigFile, "{ not json");
        var store = new ConfigStore(dir);
        store.Update(c => c with { Paused = true });
        Assert.True(store.Current.Paused);
        Assert.Single(Directory.GetFiles(dir, "config.json.broken-*"));
        Assert.Null(store.ConfigError);
    }

    [Fact]
    public void Update_is_atomic_and_leaves_no_temp_file()
    {
        new ConfigStore(dir).Update(c => c with { Pinned = true });
        Assert.Equal(["config.json"], Directory.GetFiles(dir).Select(Path.GetFileName));
    }
}
