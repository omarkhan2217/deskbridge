using System.Text.Json;
using Deskbridge.Core.Apps;

namespace Deskbridge.Core.Policy;

/// <summary>
/// Reads and writes config.json. The file is read again whenever its write time changes (checked on each
/// access, so no watcher thread runs). A file that cannot be read never grants Act: everything falls back
/// to Read until it is fixed.
/// </summary>
public sealed class ConfigStore(string directory)
{
    readonly object gate = new();
    readonly string path = Path.Combine(directory, "config.json");
    DeskbridgeConfig current = new();
    DateTime loadedWriteTime = DateTime.MinValue;
    bool loaded;

    /// <summary>Why config.json could not be read, or null when it is fine (or does not exist yet).</summary>
    public string? ConfigError { get; private set; }

    public string FilePath => path;

    public DeskbridgeConfig Current
    {
        get
        {
            lock (gate)
            {
                RefreshIfChanged();
                return current;
            }
        }
    }

    public PermissionLevel Level(AppId app)
    {
        var config = Current;
        if (config.Apps.TryGetValue(app.Key, out var level))
        {
            return level;
        }

        if (app == AppId.Notifications)
        {
            return PermissionLevel.Act;
        }

        var exeName = Path.GetFileNameWithoutExtension(app.Key);
        return config.OffByDefault.Any(name => string.Equals(name, exeName, StringComparison.OrdinalIgnoreCase))
            ? PermissionLevel.Off
            : PermissionLevel.Read;
    }

    public void Update(Func<DeskbridgeConfig, DeskbridgeConfig> change)
    {
        lock (gate)
        {
            RefreshIfChanged();
            var next = change(current);
            Directory.CreateDirectory(directory);
            if (ConfigError is not null && File.Exists(path))
            {
                // Keep the user's broken file rather than overwrite it.
                File.Move(path, path + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), overwrite: true);
            }

            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(next, ConfigJsonContext.Default.DeskbridgeConfig));
            File.Move(temp, path, overwrite: true);
            current = next;
            ConfigError = null;
            loadedWriteTime = File.GetLastWriteTimeUtc(path);
            loaded = true;
        }
    }

    void RefreshIfChanged()
    {
        if (!File.Exists(path))
        {
            if (!loaded || loadedWriteTime != DateTime.MinValue)
            {
                current = new DeskbridgeConfig();
                ConfigError = null;
                loadedWriteTime = DateTime.MinValue;
                loaded = true;
            }

            return;
        }

        var writeTime = File.GetLastWriteTimeUtc(path);
        if (loaded && writeTime == loadedWriteTime)
        {
            return;
        }

        try
        {
            current = JsonSerializer.Deserialize(File.ReadAllText(path), ConfigJsonContext.Default.DeskbridgeConfig)
                ?? new DeskbridgeConfig();
            ConfigError = null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            current = new DeskbridgeConfig();
            ConfigError = $"Deskbridge could not read {path}: {ex.Message} Every app is treated as Read until the file is fixed.";
        }

        loadedWriteTime = writeTime;
        loaded = true;
    }
}
