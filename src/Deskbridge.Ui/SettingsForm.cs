using System.Diagnostics;
using Deskbridge.Core.Apps;
using Deskbridge.Core.Policy;

namespace Deskbridge.Ui;

/// <summary>
/// Deskbridge settings: each app's level, approved clients, remembered grants (D36) and general options.
/// Every change is written to config.json at once.
/// </summary>
internal sealed class SettingsForm : Form
{
    readonly ConfigStore config;
    readonly StartupRegistration startup;
    readonly ListBox apps = new() { Name = "AppList", AccessibleName = "Apps", Dock = DockStyle.Fill, IntegralHeight = false };
    readonly ListBox clients = new() { Name = "ClientList", AccessibleName = "Clients", Dock = DockStyle.Fill, IntegralHeight = false };
    readonly ListBox grants = new() { Name = "GrantList", AccessibleName = "Remembered", Dock = DockStyle.Fill, IntegralHeight = false };
    readonly CheckBox startAtSignIn = new() { Name = "StartAtSignInBox", Text = "Start Deskbridge when I sign in", AutoSize = true };
    readonly CheckBox keepRunning = new() { Name = "KeepRunningBox", Text = "Keep running when no agent is connected", AutoSize = true };
    readonly CheckBox contentLogging = new() { Name = "ContentLoggingBox", Text = "Keep the content of each action in the audit log", AutoSize = true };
    readonly Button pause = new() { Name = "PauseButton", AutoSize = true };
    readonly Label configError = new() { Name = "ConfigErrorLabel", AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(460, 0) };
    bool loading;

    public SettingsForm(ConfigStore config, StartupRegistration startup, string logDirectory, bool hotkeyRegistered)
    {
        this.config = config;
        this.startup = startup;
        Text = "Deskbridge settings";
        ClientSize = new Size(520, 420);
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterScreen;

        var tabs = new TabControl { Name = "Tabs", Dock = DockStyle.Fill };
        tabs.TabPages.Add(Page("Apps", "What Deskbridge may do in each app you have set. Apps not listed are Read.", apps,
            Button("OffButton", "Off", () => SetLevel(PermissionLevel.Off)),
            Button("ReadButton", "Read", () => SetLevel(PermissionLevel.Read)),
            Button("ActButton", "Act", () => SetLevel(PermissionLevel.Act))));
        tabs.TabPages.Add(Page("Clients", "Programs you allowed to use Deskbridge.", clients,
            Button("RemoveClientButton", "Remove", RemoveClient)));
        tabs.TabPages.Add(Page("Remembered", "Changes you allowed and asked Deskbridge to remember. Deletes and sends always ask.", grants,
            Button("RevokeButton", "Revoke", RevokeGrant)));

        var general = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12), WrapContents = false };
        general.Controls.Add(startAtSignIn);
        general.Controls.Add(keepRunning);
        general.Controls.Add(contentLogging);
        general.Controls.Add(pause);
        general.Controls.Add(Button("OpenLogsButton", "Open log folder", () =>
        {
            Directory.CreateDirectory(logDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{logDirectory}\"") { UseShellExecute = true })?.Dispose();
        }));
        general.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(460, 0),
            Text = hotkeyRegistered
                ? "Ctrl+Alt+Shift+P pauses or resumes Deskbridge from anywhere."
                : "Another program already uses Ctrl+Alt+Shift+P, so the pause hotkey is off. Use the tray icon to pause.",
        });
        general.Controls.Add(configError);
        var generalPage = new TabPage("General");
        generalPage.Controls.Add(general);
        tabs.TabPages.Add(generalPage);
        Controls.Add(tabs);

        startAtSignIn.CheckedChanged += (_, _) => { if (!loading) startup.Set(startAtSignIn.Checked, Environment.ProcessPath ?? ""); };
        keepRunning.CheckedChanged += (_, _) => { if (!loading) config.Update(c => c with { Pinned = keepRunning.Checked }); };
        contentLogging.CheckedChanged += (_, _) => { if (!loading) config.Update(c => c with { ContentLogging = contentLogging.Checked }); };
        pause.Click += (_, _) =>
        {
            config.Update(c => c with { Paused = !c.Paused });
            Reload();
        };
        Reload();
    }

    static TabPage Page(string title, string help, ListBox list, params Button[] buttons)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = help, AutoSize = true, MaximumSize = new Size(480, 0) });
        layout.Controls.Add(list);
        var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        row.Controls.AddRange(buttons);
        layout.Controls.Add(row);
        var page = new TabPage(title);
        page.Controls.Add(layout);
        return page;
    }

    static Button Button(string name, string text, Action click)
    {
        var button = new Button { Name = name, Text = text, AutoSize = true };
        button.Click += (_, _) => click();
        return button;
    }

    void Reload()
    {
        loading = true;
        var current = config.Current;
        apps.DataSource = current.Apps.Select(a => new Row(a.Key, $"{a.Key}: {a.Value.ToString().ToLowerInvariant()}")).ToList();
        clients.DataSource = current.Clients.Select(c => new Row(c.Path, $"{c.Name}  ({c.Path}, {c.Publisher ?? "not signed"})")).ToList();
        grants.DataSource = current.Grants.Select(g => new Row(g.Client + "|" + g.App, $"{Path.GetFileName(g.Client)} may change {Path.GetFileName(g.App)}")).ToList();
        startAtSignIn.Checked = startup.IsOn;
        keepRunning.Checked = current.Pinned;
        contentLogging.Checked = current.ContentLogging;
        pause.Text = current.Paused ? "Resume Deskbridge" : "Pause Deskbridge";
        configError.Text = config.ConfigError ?? "";
        loading = false;
    }

    void SetLevel(PermissionLevel level)
    {
        if (apps.SelectedItem is Row row)
        {
            config.Update(c => c.WithLevel(new AppId(row.Key, row.Key), level));
            Reload();
        }
    }

    void RemoveClient()
    {
        if (clients.SelectedItem is Row row)
        {
            config.Update(c => c with { Clients = c.Clients.Where(x => x.Path != row.Key).ToList() });
            Reload();
        }
    }

    void RevokeGrant()
    {
        if (grants.SelectedItem is Row row)
        {
            config.Update(c => c with { Grants = c.Grants.Where(g => g.Client + "|" + g.App != row.Key).ToList() });
            Reload();
        }
    }

    sealed record Row(string Key, string Text)
    {
        public override string ToString() => Text;
    }
}
