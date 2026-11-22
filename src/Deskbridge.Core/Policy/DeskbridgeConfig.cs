using System.Text.Json.Serialization;
using Deskbridge.Core.Apps;
using Deskbridge.Core.Clients;

namespace Deskbridge.Core.Policy;

/// <summary>Off: Deskbridge does nothing in the app. Read: it may read. Act: it may also change things, after asking.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PermissionLevel>))]
public enum PermissionLevel
{
    [JsonStringEnumMemberName("off")]
    Off,

    [JsonStringEnumMemberName("read")]
    Read,

    [JsonStringEnumMemberName("act")]
    Act,
}

/// <summary>A remembered "allow" for non-destructive changes by one client in one app (D36).</summary>
public sealed record Grant(string Client, string App, DateTimeOffset GrantedAt);

/// <summary>Everything in config.json. The user can edit the file by hand; Deskbridge picks changes up on the next call.</summary>
public sealed record DeskbridgeConfig
{
    public static IReadOnlyList<string> DefaultOffApps { get; } =
    [
        "1password", "bitwarden", "keepass", "keepassxc", "dashlane", "lastpass", "enpass", "authy", "microsoftauthenticator",
    ];

    public int Version { get; init; } = 1;

    public bool Paused { get; init; }

    public bool Pinned { get; init; }

    public bool StartAtSignIn { get; init; }

    public bool ContentLogging { get; init; }

    public Dictionary<string, PermissionLevel> Apps { get; init; } = new();

    public List<ClientRecord> Clients { get; init; } = new();

    public List<Grant> Grants { get; init; } = new();

    /// <summary>Exe names (without .exe) that start at Off for reading and acting.</summary>
    public List<string> OffByDefault { get; init; } = DefaultOffApps.ToList();

    public DeskbridgeConfig WithLevel(AppId app, PermissionLevel level) =>
        this with { Apps = new Dictionary<string, PermissionLevel>(Apps) { [app.Key] = level } };
}
