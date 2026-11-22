using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Deskbridge.Core;
using Windows.Media.Control;

namespace Deskbridge.Engines.Os.Desktop;

/// <summary>Plain text on the clipboard, through the Win32 clipboard. Other formats are left alone.</summary>
public static class ClipboardText
{
    const uint CfUnicodeText = 13;
    const uint GmemMoveable = 0x0002;

    public static string? Read()
    {
        using var open = Open();
        var handle = GetClipboardData(CfUnicodeText);
        if (handle == 0)
        {
            return null;
        }

        var pointer = GlobalLock(handle);
        try
        {
            return Marshal.PtrToStringUni(pointer);
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    public static void Write(string text)
    {
        using var open = Open();
        EmptyClipboard();
        var bytes = (text.Length + 1) * 2;
        var memory = GlobalAlloc(GmemMoveable, (nuint)bytes);
        var pointer = GlobalLock(memory);
        Marshal.Copy((text + "\0").ToCharArray(), 0, pointer, text.Length + 1);
        GlobalUnlock(memory);
        if (SetClipboardData(CfUnicodeText, memory) == 0)
        {
            throw new DeskbridgeException("Windows refused to put the text on the clipboard.");
        }
    }

    static Session Open()
    {
        // Another program may hold the clipboard for a moment.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (OpenClipboard(0))
            {
                return new Session();
            }

            Thread.Sleep(50);
        }

        throw new DeskbridgeException("Another program is holding the clipboard. Try again in a moment.");
    }

    sealed class Session : IDisposable
    {
        public void Dispose() => CloseClipboard();
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool OpenClipboard(nint owner);

    [DllImport("user32.dll")]
    static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    static extern nint GetClipboardData(uint format);

    [DllImport("user32.dll")]
    static extern nint SetClipboardData(uint format, nint memory);

    [DllImport("kernel32.dll")]
    static extern nint GlobalAlloc(uint flags, nuint bytes);

    [DllImport("kernel32.dll")]
    static extern nint GlobalLock(nint memory);

    [DllImport("kernel32.dll")]
    static extern bool GlobalUnlock(nint memory);
}

public sealed record NowPlaying(string App, string? Title, string? Artist, string Status);

public enum MediaAction
{
    Play,
    Pause,
    Toggle,
    Next,
    Previous,
}

/// <summary>What is playing, and play controls, through the Windows media session (any player, including Spotify).</summary>
public static class MediaSession
{
    public static async Task<NowPlaying?> NowPlayingAsync()
    {
        var session = (await GlobalSystemMediaTransportControlsSessionManager.RequestAsync()).GetCurrentSession();
        if (session is null)
        {
            return null;
        }

        var properties = await session.TryGetMediaPropertiesAsync();
        var status = session.GetPlaybackInfo()?.PlaybackStatus.ToString().ToLowerInvariant() ?? "unknown";
        return new NowPlaying(session.SourceAppUserModelId, properties?.Title, properties?.Artist, status);
    }

    /// <returns>False when nothing is playing or paused anywhere.</returns>
    public static async Task<bool> ControlAsync(MediaAction action)
    {
        var session = (await GlobalSystemMediaTransportControlsSessionManager.RequestAsync()).GetCurrentSession();
        if (session is null)
        {
            return false;
        }

        return action switch
        {
            MediaAction.Play => await session.TryPlayAsync(),
            MediaAction.Pause => await session.TryPauseAsync(),
            MediaAction.Toggle => await session.TryTogglePlayPauseAsync(),
            MediaAction.Next => await session.TrySkipNextAsync(),
            MediaAction.Previous => await session.TrySkipPreviousAsync(),
            _ => false,
        };
    }
}

/// <summary>The default output device's master volume, through Core Audio.</summary>
public static class Volume
{
    public static int Get() => Use(endpoint =>
    {
        Check(endpoint.GetMasterVolumeLevelScalar(out var level));
        return (int)Math.Round(level * 100);
    });

    public static (int Before, int After) Set(int percent)
    {
        var before = Get();
        Use(endpoint =>
        {
            var context = Guid.Empty;
            Check(endpoint.SetMasterVolumeLevelScalar(Math.Clamp(percent, 0, 100) / 100f, ref context));
            return 0;
        });
        return (before, Get());
    }

    static T Use<T>(Func<IAudioEndpointVolume, T> work)
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        try
        {
            Check(enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out var device));
            var iid = typeof(IAudioEndpointVolume).GUID;
            Check(device.Activate(ref iid, 23 /* CLSCTX_ALL */, 0, out var activated));
            return work((IAudioEndpointVolume)activated);
        }
        catch (COMException ex)
        {
            throw new DeskbridgeException($"Windows has no audio output to control (error 0x{ex.HResult:X8}).");
        }
    }

    static void Check(int hr)
    {
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumerator
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out nint devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int clsCtx, nint activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        [PreserveSig]
        int RegisterControlChangeNotify(nint notify);

        [PreserveSig]
        int UnregisterControlChangeNotify(nint notify);

        [PreserveSig]
        int GetChannelCount(out uint count);

        [PreserveSig]
        int SetMasterVolumeLevel(float levelDb, ref Guid context);

        [PreserveSig]
        int SetMasterVolumeLevelScalar(float level, ref Guid context);

        [PreserveSig]
        int GetMasterVolumeLevel(out float levelDb);

        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float level);
    }
}

/// <summary>Settings pages as ms-settings: links.</summary>
public static partial class SettingsPages
{
    public static string Uri(string page)
    {
        if (!PagePattern().IsMatch(page))
        {
            throw new DeskbridgeException("A Settings page is a short name like 'taskbar', 'display' or 'bluetooth'.");
        }

        return "ms-settings:" + page;
    }

    [GeneratedRegex("^[a-z0-9-]{1,60}$")]
    private static partial Regex PagePattern();
}
