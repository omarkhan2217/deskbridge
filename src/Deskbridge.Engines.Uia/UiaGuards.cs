using System.Text.RegularExpressions;
using Deskbridge.Core;
using Deskbridge.Engines.Os;

namespace Deskbridge.Engines.Uia;

/// <summary>
/// Checks made before UI Automation touches a window, using only the window manager: it must be alive,
/// not a Chromium or Electron app, not running as administrator, not showing a secret file, and not a
/// minimized Store app (whose controls are hidden).
/// </summary>
public static partial class UiaGuards
{
    public static void Check(nint window)
    {
        if (!WindowHandles.IsAlive(window))
        {
            throw new WindowGoneException();
        }

        var className = WindowLister.ClassName(window);
        if (ChromiumGuard.IsChromium(className))
        {
            throw new ChromiumWindowException();
        }

        if (ProcessFacts.IsElevated(AppResolver.ProcessOf(window)))
        {
            throw new ElevatedWindowException();
        }

        var title = WindowLister.Title(window);
        if (SecretTitle().IsMatch(title))
        {
            throw new SecretWindowException();
        }

        if (className == "ApplicationFrameWindow" && WindowHandles.IsMinimized(window))
        {
            throw new MinimizedStoreAppException(title);
        }
    }

    [GeneratedRegex(
        @"(^|[\s\\/""'(])\.env(\.[\w-]+)?\b|\bid_(rsa|dsa|ecdsa|ed25519)\b|\.(pem|key|pfx|p12|kdbx)\b|\bcredentials?\.\w+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretTitle();
}

public sealed class ElevatedWindowException()
    : DeskbridgeException("That window runs as administrator. Windows does not let other programs touch it, and Deskbridge never asks to.");

public sealed class SecretWindowException()
    : DeskbridgeException("That window shows a secret file (an .env file, a key, a certificate or a password database), so Deskbridge will not read or act in it.");

public sealed class MinimizedStoreAppException(string app)
    : DeskbridgeException($"{app} is minimized, and minimized Store apps hide their controls. Ask the user to restore it, then try again.");

public sealed class ElementChangedException()
    : DeskbridgeException("The control changed since it was found (its name, type or state is different). Find it again before acting.");

public sealed class ElementUnknownException()
    : DeskbridgeException("That element ID is no longer known: it was found too long ago or in another session. Find it again with ui_find.");

public sealed class NoWayToActException(string what)
    : DeskbridgeException($"There is no way to {what} this control without the mouse, so Deskbridge will not.");
