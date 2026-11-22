using System.Security.Principal;
using System.Text.RegularExpressions;

namespace Deskbridge.Core;

/// <summary>
/// Every product, file, pipe and mutex name in one place, so a rename touches one file.
/// </summary>
public static partial class Names
{
    public const string Product = "Deskbridge";
    public const string ServerName = "deskbridge";
    public const string RelayExe = "deskbridge.exe";
    public const string BrokerExe = "deskbridge-broker.exe";

    public const string PipeSuffixVariable = "DESKBRIDGE_PIPE_SUFFIX";
    public const string IdleExitVariable = "DESKBRIDGE_IDLE_EXIT_MS";
    public const string BrokerPathVariable = "DESKBRIDGE_BROKER_PATH";
    public const string ConfigDirVariable = "DESKBRIDGE_CONFIG_DIR";

    /// <summary>The value name under HKCU\...\Run when "start at sign-in" is on.</summary>
    public const string RunValueName = "Deskbridge";

    /// <summary>Where config.json lives: the override when given (tests), else %APPDATA%\Deskbridge.</summary>
    public static string ConfigDirectory(string? overrideDir) =>
        overrideDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Product);

    /// <summary>Where audit logs live: under the override when given (tests), else %LOCALAPPDATA%\Deskbridge\logs.</summary>
    public static string LogDirectory(string? overrideDir) =>
        overrideDir is not null
            ? Path.Combine(overrideDir, "logs")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Product, "logs");

    /// <summary>The config directory override from the environment, or null.</summary>
    public static string? ConfigOverride() => Environment.GetEnvironmentVariable(ConfigDirVariable);

    public static string CurrentUserSid()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User?.Value
            ?? throw new InvalidOperationException("The current Windows account has no SID.");
    }

    public static string PipeName(string userSid, string? suffix) =>
        $"deskbridge-{userSid}{FormatSuffix(suffix)}";

    public static string BrokerMutex(string userSid, string? suffix) =>
        $@"Local\deskbridge-broker-{userSid}{FormatSuffix(suffix)}";

    static string FormatSuffix(string? suffix)
    {
        if (suffix is null)
        {
            return "";
        }

        if (!SafeSuffix().IsMatch(suffix))
        {
            throw new ArgumentException(
                "A pipe suffix uses only letters, digits, '_' and '-', 1 to 32 characters.", nameof(suffix));
        }

        return "-" + suffix;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,32}$")]
    private static partial Regex SafeSuffix();
}
