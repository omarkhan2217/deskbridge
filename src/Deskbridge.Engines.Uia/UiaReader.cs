using System.Runtime.InteropServices;
using Deskbridge.Core;
using Deskbridge.Engines.Os;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Deskbridge.Engines.Uia;

/// <summary>
/// Reads text from a control in any window, in front or not, without focusing it.
/// </summary>
public sealed class UiaReader(UiaWorker worker)
{
    // UIA_E_ELEMENTNOTAVAILABLE: the element (or its window) disappeared during the call.
    const int ElementNotAvailable = unchecked((int)0x80040201);

    public async Task<ControlText> ReadTextAsync(nint window, string? control, int maxChars = 20_000, CancellationToken ct = default)
    {
        if (!WindowHandles.IsAlive(window))
        {
            throw new WindowGoneException();
        }

        // Checked through the window manager, before UI Automation touches the window.
        if (ChromiumGuard.IsChromium(WindowLister.ClassName(window)))
        {
            throw new ChromiumWindowException();
        }

        try
        {
            return await worker.RunAsync(automation => Read(automation.FromHandle(window), control, maxChars), ct)
                .ConfigureAwait(false);
        }
        catch (COMException ex) when (ex.HResult == ElementNotAvailable || !WindowHandles.IsAlive(window))
        {
            throw new WindowGoneException();
        }
        catch (TimeoutException)
        {
            // UI Automation's own TransactionTimeout fired first (FlaUI wraps UIA_E_TIMEOUT this way).
            throw new UiaTimeoutException(worker.Options.TransactionTimeout);
        }
        catch (COMException ex)
        {
            throw new DeskbridgeException(
                $"The app's accessibility interface returned an error (0x{ex.HResult:X8}). Try again; if it repeats, the app may not support reading this control.");
        }
    }

    static ControlText Read(AutomationElement root, string? control, int maxChars)
    {
        var cf = root.ConditionFactory;
        var element = control is null
            ? root.FindFirstDescendant(cf.ByControlType(ControlType.Document).Or(cf.ByControlType(ControlType.Edit)))
            : root.FindFirstDescendant(cf.ByAutomationId(control))
              ?? root.FindFirstDescendant(cf.ByName(control, PropertyConditionFlags.IgnoreCase));

        if (element is null)
        {
            throw new ControlNotFoundException(control);
        }

        if (element.Properties.IsPassword.ValueOrDefault)
        {
            throw new SecretFieldException();
        }

        string text;
        if (element.Patterns.Value.TryGetPattern(out var value))
        {
            text = value.Value.ValueOrDefault ?? "";
        }
        else if (element.Patterns.Text.TryGetPattern(out var textPattern))
        {
            text = textPattern.DocumentRange.GetText(maxChars + 1) ?? "";
        }
        else
        {
            text = element.Properties.Name.ValueOrDefault ?? "";
        }

        var truncated = text.Length > maxChars;
        if (truncated)
        {
            text = text[..maxChars];
        }

        return new ControlText(
            element.Properties.Name.ValueOrDefault,
            element.Properties.AutomationId.ValueOrDefault,
            element.Properties.ControlType.ValueOrDefault.ToString(),
            text,
            truncated,
            element.Patterns.Toggle.TryGetPattern(out var toggle) ? UiaActions.State(toggle.ToggleState.ValueOrDefault) : null);
    }
}
