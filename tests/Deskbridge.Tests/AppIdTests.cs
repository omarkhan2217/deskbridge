using Deskbridge.Core.Apps;
using Deskbridge.Engines.Os;

namespace Deskbridge.Tests;

public class AppIdTests
{
    [Fact]
    public void Keys_are_lowercase_full_paths() =>
        Assert.Equal(@"c:\windows\notepad.exe", AppId.ForExe(@"C:\Windows\.\Notepad.EXE", "Notepad").Key);

    [Fact]
    public void System_parts_have_fixed_keys() =>
        Assert.Equal(
            ["system.files", "system.clipboard", "system.media", "system.settings", "system.windows", "system.notifications"],
            new[] { AppId.Files, AppId.Clipboard, AppId.Media, AppId.Settings, AppId.Windows, AppId.Notifications }.Select(a => a.Key));

    [Fact]
    public void This_process_is_not_elevated() => Assert.False(ProcessFacts.IsElevated(Environment.ProcessId));

    [Fact]
    public void This_process_has_an_image_path() =>
        Assert.EndsWith(".exe", ProcessFacts.ImagePath(Environment.ProcessId), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void A_missing_process_has_no_path_and_is_not_elevated()
    {
        Assert.Null(ProcessFacts.ImagePath(-1));
        Assert.False(ProcessFacts.IsElevated(-1));
    }
}
