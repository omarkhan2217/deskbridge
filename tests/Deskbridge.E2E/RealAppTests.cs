using Deskbridge.E2E.Fixtures;
using Deskbridge.E2E.HandsOff;
using Deskbridge.Engines.Uia;

namespace Deskbridge.E2E;

/// <summary>
/// Real-app compatibility: Deskbridge reads Notepad and Calculator without touching the user. The strict
/// "window not in front" proof is the target app's (D29). Here Windows sometimes refuses to move the
/// foreground while the owner is using the PC, so a setup that cannot put the app behind is noted in the
/// test output rather than failing the read it does not affect.
/// </summary>
public class RealAppTests
{
    [Fact]
    public async Task Notepad_text_is_read_while_notepad_is_minimized()
    {
        var marker = "deskbridge-e2e-" + Guid.NewGuid().ToString("N")[..8];
        var file = Path.Combine(Path.GetTempPath(), marker + ".txt");
        await File.WriteAllTextAsync(file, marker);
        try
        {
            using var notepad = RealApp.Start("notepad.exe", $"\"{file}\"", titleContains: marker, processName: "Notepad");
            for (var attempt = 0; attempt < 3 && (attempt == 0 || notepad.IsForeground); attempt++)
            {
                notepad.MinimizeAndActivateNext(); // test setup, before the snapshot
            }

            NoteIfInFront(notepad, "Notepad");
            using var worker = new UiaWorker();
            var text = await HandsOffProbe.AssertUntouchedAsync(() => new UiaReader(worker).ReadTextAsync(notepad.Handle, null));
            Assert.Contains(marker, text.Text);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Calculator_display_is_read_while_calculator_is_behind_another_window()
    {
        // A minimized Store app has no controls in the tree, and restoring it takes activation back,
        // so the test opens Notepad on top of Calculator instead.
        var cover = Path.Combine(Path.GetTempPath(), "deskbridge-e2e-cover-" + Guid.NewGuid().ToString("N")[..8] + ".txt");
        await File.WriteAllTextAsync(cover, "in front of Calculator");
        try
        {
            using var calc = RealApp.Start("calc.exe", "", titleContains: "Calculator", processName: "CalculatorApp");
            using var notepad = RealApp.Start("notepad.exe", $"\"{cover}\"", titleContains: Path.GetFileNameWithoutExtension(cover), processName: "Notepad");
            await Task.Delay(1_000);
            NoteIfInFront(calc, "Calculator");
            using var worker = new UiaWorker();
            var text = await HandsOffProbe.AssertUntouchedAsync(() => new UiaReader(worker).ReadTextAsync(calc.Handle, "CalculatorResults"));
            Assert.StartsWith("Display is", text.Text);
        }
        finally
        {
            File.Delete(cover);
        }
    }

    static void NoteIfInFront(RealApp app, string name)
    {
        if (app.IsForeground)
        {
            TestContext.Current.SendDiagnosticMessage(
                $"{name} stayed in front: Windows did not move the foreground during setup. The read below still runs inside the hands-off probe.");
        }
    }
}
