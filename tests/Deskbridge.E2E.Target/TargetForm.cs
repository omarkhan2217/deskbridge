namespace Deskbridge.E2E.Target;

sealed class TargetForm : Form
{
    readonly TargetOptions options;

    public TargetForm(TargetOptions options)
    {
        this.options = options;
        Text = options.Title;
        Width = 420;
        Height = 260;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(40, 40);

        var notes = new TextBox
        {
            Name = "NotesBox",
            AccessibleName = "Notes",
            Multiline = true,
            MaxLength = int.MaxValue,
            Dock = DockStyle.Top,
            Height = 120,
            Text = options.Big > 0 ? new string('a', options.Big) : options.Text,
        };
        Controls.Add(notes);

        if (options.Password)
        {
            Controls.Add(new TextBox
            {
                Name = "SecretBox",
                AccessibleName = "Secret",
                UseSystemPasswordChar = true,
                Dock = DockStyle.Bottom,
                Text = "hunter2",
            });
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (options.HangMs > 0)
        {
            var timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Thread.Sleep(options.HangMs); // blocks the UI thread: the app stops answering
            };
            timer.Start();
        }
    }
}
