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

    // GitHub's Windows runners run elevated and a developer's PC usually does not, so the answer is
    // checked against .NET's own view of this process rather than assumed.
    [Fact]
    public void Elevation_of_this_process_matches_what_dotnet_reports() =>
        Assert.Equal(Environment.IsPrivilegedProcess, ProcessFacts.IsElevated(Environment.ProcessId));

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
