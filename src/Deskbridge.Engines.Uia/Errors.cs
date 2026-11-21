using Deskbridge.Core;

namespace Deskbridge.Engines.Uia;

public sealed class UiaTimeoutException(TimeSpan limit)
    : DeskbridgeException($"The app did not answer within {limit.TotalSeconds:0.#} seconds. It may be busy or not responding.");

public sealed class WindowGoneException()
    : DeskbridgeException("That window is gone. List windows again.");

public sealed class ControlNotFoundException(string? control)
    : DeskbridgeException(control is null
        ? "That window has no text box or document to read. Name a control."
        : $"No control named '{control}' in that window.");

public sealed class SecretFieldException()
    : DeskbridgeException("That field holds a password or secret, so Deskbridge will not read it.");

public sealed class ChromiumWindowException()
    : DeskbridgeException("That window is a Chromium or Electron app. Reading it needs an adapter that allows it, which does not exist yet.");
