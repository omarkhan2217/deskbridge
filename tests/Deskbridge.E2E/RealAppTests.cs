using Deskbridge.E2E.Fixtures;
using Deskbridge.E2E.HandsOff;
using Deskbridge.Engines.Uia;

namespace Deskbridge.E2E;

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
            notepad.MinimizeAndActivateNext(); // test setup, before the snapshot
            Assert.False(notepad.IsForeground);
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
            Assert.False(calc.IsForeground);
            using var worker = new UiaWorker();
            var text = await HandsOffProbe.AssertUntouchedAsync(() => new UiaReader(worker).ReadTextAsync(calc.Handle, "CalculatorResults"));
            Assert.StartsWith("Display is", text.Text);
        }
        finally
        {
            File.Delete(cover);
        }
    }
}
