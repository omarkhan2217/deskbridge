using System.Security.Principal;
using System.Text;
using Deskbridge.Core;
using Deskbridge.E2E.Fixtures;
using Deskbridge.E2E.HandsOff;
using Deskbridge.Engines.Os;
using Deskbridge.Engines.Os.Apps;
using Deskbridge.Engines.Os.Desktop;
using Deskbridge.Engines.Os.Files;

namespace Deskbridge.E2E;

public sealed class OsToolsTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "deskbridge-e2e-files-" + Guid.NewGuid().ToString("N"));

    public OsToolsTests() => Directory.CreateDirectory(dir);

    public void Dispose()
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Files_round_trip_without_touching_the_user()
    {
        var notes = Path.Combine(dir, "notes.txt");
        var plan = Path.Combine(dir, "plan.txt");
        await HandsOffProbe.AssertUntouchedAsync(() =>
        {
            if (File.Exists(notes))
            {
                File.Delete(notes);
            }

            FileTools.Write(notes, "first line\nsecond line", WriteMode.Create);
            return Task.FromResult(0);
        });
        Assert.Equal("first line\nsecond line", FileTools.Read(notes, 1_048_576).Text);
        Assert.Contains(FileTools.Search(dir, "*.txt", 200), f => f.Path == notes && f.Size == 22);

        await HandsOffProbe.AssertUntouchedAsync(() => { FileTools.Move(notes, plan); return Task.FromResult(0); });
        Assert.False(File.Exists(notes));
        Assert.True(File.Exists(plan));

        await HandsOffProbe.AssertUntouchedAsync(() => { FileTools.Delete(plan); return Task.FromResult(0); });
        Assert.False(File.Exists(plan));
        Assert.True(InRecycleBin(plan), "the deleted file should be in the Recycle Bin");
    }

    [Fact]
    public void Read_cuts_long_files_and_marks_them()
    {
        var big = Path.Combine(dir, "big.txt");
        File.WriteAllText(big, new string('x', 5_000));
        var text = FileTools.Read(big, 1_000);
        Assert.Equal(1_000, text.Text.Length);
        Assert.True(text.Truncated);
    }

    [Fact]
    public void Secret_and_binary_files_are_refused()
    {
        var env = Path.Combine(dir, ".env");
        File.WriteAllText(env, "API_KEY=abc");
        Assert.Contains("secret", Assert.Throws<SecretFileException>(() => FileTools.Read(env, 1_000)).Message);
        Assert.Throws<SecretFileException>(() => FileTools.Write(Path.Combine(dir, "id_rsa"), "x", WriteMode.Create));

        var binary = Path.Combine(dir, "image.bin");
        File.WriteAllBytes(binary, [1, 2, 0, 3]);
        Assert.Contains("binary", Assert.Throws<DeskbridgeException>(() => FileTools.Read(binary, 1_000)).Message);
    }

    [Fact]
    public void Create_and_move_never_overwrite()
    {
        var a = Path.Combine(dir, "a.txt");
        var b = Path.Combine(dir, "b.txt");
        File.WriteAllText(a, "a");
        File.WriteAllText(b, "b");
        Assert.Contains("already exists", Assert.Throws<DeskbridgeException>(() => FileTools.Write(a, "new", WriteMode.Create)).Message);
        Assert.Contains("never overwrites", Assert.Throws<DeskbridgeException>(() => FileTools.Move(a, b)).Message);
        FileTools.Write(a, "new", WriteMode.Overwrite);
        Assert.Equal("new", File.ReadAllText(a));
        Assert.Contains("There is no file", Assert.Throws<DeskbridgeException>(() => FileTools.Read(Path.Combine(dir, "missing.txt"), 10)).Message);
    }

    [Fact]
    public void Clipboard_round_trip_puts_the_users_text_back()
    {
        var saved = ClipboardText.Read();
        try
        {
            ClipboardText.Write("deskbridge clipboard test");
            Assert.Equal("deskbridge clipboard test", ClipboardText.Read());
        }
        finally
        {
            if (saved is not null)
            {
                ClipboardText.Write(saved);
            }
        }
    }

    [Fact]
    public void Describe_a_background_window()
    {
        var title = "dbe2e-describe-" + Guid.NewGuid().ToString("N")[..8];
        using var app = TargetApp.Start(title, "--text", "x");
        var d = WindowDescriber.Describe(app.Handle);
        Assert.Equal(title, d.Title);
        Assert.Equal(app.ProcessId, d.Pid);
        Assert.Equal("normal", d.State);
        Assert.False(d.Foreground);
        Assert.False(d.Elevated);
        Assert.False(d.StoreApp);
        Assert.False(d.Chromium);
        Assert.True(d.Bounds.Width > 0);
    }

    [Fact]
    public async Task Installed_apps_include_notepad()
    {
        var apps = await AppsFolder.ListAsync();
        Assert.Contains(apps, a => a.AppId == "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App");
    }

    [Fact]
    public async Task Now_playing_answers_whether_or_not_something_plays()
    {
        var playing = await MediaSession.NowPlayingAsync();
        Assert.True(playing is null || !string.IsNullOrEmpty(playing.Status));
    }

    [Fact]
    public void Setting_the_volume_to_its_current_level_changes_nothing()
    {
        var current = Volume.Get();
        var (before, after) = Volume.Set(current);
        Assert.Equal(current, before);
        Assert.Equal(current, after);
    }

    [Fact]
    public async Task Launching_notepad_reports_focus_truthfully()
    {
        Assert.Empty(System.Diagnostics.Process.GetProcessesByName("Notepad")); // never touch the user's own Notepad
        var result = await Launcher.LaunchAsync("Microsoft.WindowsNotepad_8wekyb3d8bbwe!App", CancellationToken.None);
        try
        {
            Assert.NotNull(result.NewWindow);
            Assert.True(WindowHandles.TryParse(result.NewWindow, out var notepad));
            Assert.Equal(NativeTest.GetForegroundWindow() == notepad, result.TookFocus);
        }
        finally
        {
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("Notepad"))
            {
                p.Kill();
            }
        }
    }

    static bool InRecycleBin(string originalPath)
    {
        // Each deleted item has a $I file in the user's Recycle Bin folder that records its original path.
        var sid = WindowsIdentity.GetCurrent().User!.Value;
        var bin = Path.Combine(Path.GetPathRoot(originalPath)!, "$Recycle.Bin", sid);
        var needle = Encoding.Unicode.GetBytes(originalPath);
        return Directory.EnumerateFiles(bin, "$I*").Any(f => File.ReadAllBytes(f).AsSpan().IndexOf(needle) >= 0);
    }
}
