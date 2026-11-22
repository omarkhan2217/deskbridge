using System.Text.RegularExpressions;
using Deskbridge.Core;

namespace Deskbridge.Engines.Os.Files;

/// <summary>Files Deskbridge never reads or writes: environment files, keys, certificates, password databases.</summary>
public static partial class SecretFiles
{
    public static bool IsSecret(string path) => Pattern().IsMatch(Path.GetFileName(path));

    [GeneratedRegex(
        @"^(\.env(\..*)?|id_(rsa|dsa|ecdsa|ed25519).*|.*\.(pem|key|pfx|p12|kdbx|keystore|jks)|.*credential.*|.*secret.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}

public sealed class SecretFileException()
    : DeskbridgeException("Deskbridge does not read or write secret files such as .env files, keys, certificates and password databases.");
