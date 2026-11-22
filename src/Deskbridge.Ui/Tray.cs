using System.Diagnostics;
using System.Runtime.InteropServices;
using Deskbridge.Core;
using Deskbridge.Core.Policy;

namespace Deskbridge.Ui;

/// <summary>
/// The tray icon: Pause all or Resume, Settings, Open log folder, Quit. It also shows notifications
/// (notify_show) and owns the pause hotkey, Ctrl+Alt+Shift+P.
/// </summary>
public sealed class Tray : IDisposable, INotifier
{
    public const int HotkeyId = 0xDB01;

    readonly UiThread ui;
    readonly ConfigStore config;
    readonly string logDirectory;
    readonly Action onQuit;
    readonly StartupRegistration startup;
    NotifyIcon? icon;
    HotkeyReceiver? hotkey;
    SettingsForm? settings;

    Tray(UiThread ui, ConfigStore config, string logDirectory, Action onQuit, StartupRegistration startup)
    {
        this.ui = ui;
        this.config = config;
        this.logDirectory = logDirectory;
        this.onQuit = onQuit;
        this.startup = startup;
    }

    /// <summary>The hidden window that receives the hotkey (0 when not created).</summary>
    public nint HotkeyWindow { get; private set; }

    /// <summary>False when another program already owns Ctrl+Alt+Shift+P.</summary>
    public bool HotkeyRegistered { get; private set; }

    public static async Task<Tray> StartAsync(UiThread ui, ConfigStore config, string logDirectory, Action onQuit, StartupRegistration? startup = null)
    {
        var tray = new Tray(ui, config, logDirectory, onQuit, startup ?? new StartupRegistration(Names.RunValueName));
        await ui.InvokeAsync(() =>
        {
            tray.Create();
            return 0;
        }).ConfigureAwait(false);
        return tray;
    }

    public Task TogglePauseAsync()
    {
        config.Update(c => c with { Paused = !c.Paused });
        return ui.InvokeAsync(() =>
        {
            UpdateTooltip();
            return 0;
        });
    }

    public Task<nint> OpenSettingsAsync() => ui.InvokeAsync(() =>
    {
        if (settings is null || settings.IsDisposed)
        {
            settings = new SettingsForm(config, startup, logDirectory, HotkeyRegistered);
        }

        settings.Show(); // the user asked for this window, so it may come to the front
        return settings.Handle;
    });

    public Task CloseSettingsAsync() => ui.InvokeAsync(() =>
    {
        settings?.Close();
        return 0;
    });

    public void Show(string title, string text) =>
        ui.Post(() => icon?.ShowBalloonTip(5_000, title, text, ToolTipIcon.Info));

    public void Dispose() => ui.Post(() =>
    {
        hotkey?.Release();
        settings?.Close();
        if (icon is not null)
        {
            icon.Visible = false;
            icon.Dispose();
        }
    });

    void Create()
    {
        var menu = new ContextMenuStrip();
        var pause = new ToolStripMenuItem("Pause all", null, async (_, _) => await TogglePauseAsync().ConfigureAwait(false));
        menu.Items.Add(pause);
        menu.Items.Add(new ToolStripMenuItem("Settings", null, async (_, _) => await OpenSettingsAsync().ConfigureAwait(false)));
        menu.Items.Add(new ToolStripMenuItem("Open log folder", null, (_, _) => OpenLogFolder()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Quit Deskbridge", null, (_, _) => onQuit()));
        menu.Opening += (_, _) => pause.Text = config.Current.Paused ? "Resume" : "Pause all";

        icon = new NotifyIcon { Icon = TrayIcons.Make(), ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += async (_, _) => await OpenSettingsAsync().ConfigureAwait(false);
        UpdateTooltip();

        hotkey = new HotkeyReceiver(() => _ = TogglePauseAsync());
        HotkeyWindow = hotkey.Handle;
        HotkeyRegistered = hotkey.Registered;
    }

    void UpdateTooltip()
    {
        if (icon is not null)
        {
            icon.Text = config.Current.Paused ? "Deskbridge: paused" : "Deskbridge";
        }
    }

    void OpenLogFolder()
    {
        Directory.CreateDirectory(logDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{logDirectory}\"") { UseShellExecute = true })?.Dispose();
    }

    /// <summary>A message-only window that owns the global hotkey.</summary>
    sealed class HotkeyReceiver : NativeWindow
    {
        const int WmHotkey = 0x0312;
        const uint ModAlt = 0x1;
        const uint ModControl = 0x2;
        const uint ModShift = 0x4;
        const uint ModNoRepeat = 0x4000;
        readonly Action toggle;

        public HotkeyReceiver(Action toggle)
        {
            this.toggle = toggle;
            CreateHandle(new CreateParams { Parent = new nint(-3) }); // HWND_MESSAGE
            Registered = RegisterHotKey(Handle, HotkeyId, ModAlt | ModControl | ModShift | ModNoRepeat, (uint)Keys.P);
        }

        public bool Registered { get; }

        public void Release()
        {
            if (Registered)
            {
                UnregisterHotKey(Handle, HotkeyId);
            }

            DestroyHandle();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && m.WParam == HotkeyId)
            {
                toggle();
                return;
            }

            base.WndProc(ref m);
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);

        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(nint hwnd, int id);
    }
}

/// <summary>Draws the tray icon: a "D" on a blue square, so no image file has to ship.</summary>
static class TrayIcons
{
    public static Icon Make()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var fill = new SolidBrush(Color.FromArgb(28, 92, 165));
            g.FillRectangle(fill, 2, 2, 28, 28);
            using var font = new Font("Segoe UI", 16, FontStyle.Bold, GraphicsUnit.Pixel);
            TextRenderer.DrawText(g, "D", font, new Rectangle(2, 2, 28, 28), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }
}
