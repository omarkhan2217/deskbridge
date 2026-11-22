using System.Globalization;
using Deskbridge.Engines.Os.Native;

namespace Deskbridge.Engines.Os;

/// <summary>Window handles as agents see them: "0x" plus uppercase hex, like 0x1A2B.</summary>
public static class WindowHandles
{
    public static string Format(nint handle) => "0x" + ((long)handle).ToString("X", CultureInfo.InvariantCulture);

    public static bool TryParse(string? text, out nint handle)
    {
        handle = 0;
        if (text is null || text.Length < 3 || text.Length > 18
            || !(text.StartsWith("0x", StringComparison.Ordinal) || text.StartsWith("0X", StringComparison.Ordinal)))
        {
            return false;
        }

        if (!long.TryParse(text.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        handle = (nint)value;
        return true;
    }

    public static bool IsAlive(nint handle) => handle != 0 && User32.IsWindow(handle);

    public static bool IsMinimized(nint handle) => User32.IsIconic(handle);
}
