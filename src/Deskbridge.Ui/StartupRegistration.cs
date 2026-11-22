using Microsoft.Win32;

namespace Deskbridge.Ui;

/// <summary>"Start at sign-in": one value under HKCU\Software\Microsoft\Windows\CurrentVersion\Run.</summary>
public sealed class StartupRegistration(string valueName)
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsOn => Command is not null;

    public string? Command
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(valueName) as string;
        }
    }

    public void Set(bool on, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on)
        {
            key.SetValue(valueName, $"\"{exePath}\"");
        }
        else if (key.GetValue(valueName) is not null)
        {
            key.DeleteValue(valueName);
        }
    }
}
