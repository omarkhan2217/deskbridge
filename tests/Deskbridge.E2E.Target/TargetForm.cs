namespace Deskbridge.E2E.Target;

sealed class TargetForm : Form
{
    readonly TargetOptions options;
    readonly Button save;

    public TargetForm(TargetOptions options)
    {
        this.options = options;
        Text = options.Title;
        Width = 460;
        Height = 420;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(40, 40);

        var notes = new TextBox
        {
            Name = "NotesBox",
            AccessibleName = "Notes",
            Multiline = true,
            MaxLength = int.MaxValue,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(10, 10),
            Size = new Size(420, 100),
            Text = options.Big > 0 ? new string('a', options.Big) : options.Text,
        };
        Controls.Add(notes);

        var status = new Label { Name = "StatusLabel", Text = "Not saved", Location = new Point(10, 120), AutoSize = true };
        Controls.Add(status);

        save = new Button { Name = "SaveButton", Text = "Save", Location = new Point(10, 145), AutoSize = true };
        save.Click += (_, _) => status.Text = "Saved";
        Controls.Add(save);

        Controls.Add(new CheckBox { Name = "BoldCheck", Text = "Bold", Location = new Point(120, 148), AutoSize = true });

        var delete = new Button { Name = "DeleteButton", Text = "Delete", Location = new Point(220, 145), AutoSize = true };
        delete.Click += (_, _) => status.Text = "Deleted";
        Controls.Add(delete);

        // An icon-only button: no text, so UI Automation sees no name, only its AutomationId.
        var remove = new Button { Name = "RemoveItemButton", Text = "", Location = new Point(300, 145), Size = new Size(24, 24) };
        remove.Click += (_, _) => status.Text = "Removed";
        Controls.Add(remove);

        var sizes = new ListBox { Name = "SizeList", AccessibleName = "Size", Location = new Point(10, 185), Size = new Size(150, 60) };
        sizes.Items.AddRange(["Small", "Medium", "Large"]);
        Controls.Add(sizes);

        if (options.Password)
        {
            Controls.Add(new TextBox
            {
                Name = "SecretBox",
                AccessibleName = "Secret",
                UseSystemPasswordChar = true,
                Location = new Point(10, 255),
                Size = new Size(200, 24),
                Text = "hunter2",
            });
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (options.NativeButton)
        {
            Controls.Add(new LegacyButton { Name = "LegacyButton", Text = "Legacy Save", Location = new Point(220, 185), Size = new Size(150, 30) });
        }

        if (options.RenameAfterMs > 0)
        {
            After(options.RenameAfterMs, () => save.Text = "Save As");
        }

        if (options.HangMs > 0)
        {
            After(500, () => Thread.Sleep(options.HangMs)); // blocks the UI thread: the app stops answering
        }
    }

    void After(int ms, Action action)
    {
        var timer = new System.Windows.Forms.Timer { Interval = ms };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
    }

    /// <summary>
    /// Like an old owner-drawn Win32 button: a BUTTON window that UI Automation finds by name but that offers no
    /// pattern and no default action. It answers BM_CLICK sent to its own window by changing its text to "Legacy clicked".
    /// </summary>
    sealed class LegacyButton : Control
    {
        const int BmClick = 0x00F5;

        protected override AccessibleObject CreateAccessibilityInstance() => new Plain(this);

        // A real Win32 BUTTON window underneath, as owner-drawn buttons in old apps are.
        protected override CreateParams CreateParams
        {
            get
            {
                var p = base.CreateParams;
                p.ClassName = "Button"; // the spelling WinForms Button uses, so the class is registered once
                return p;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.DrawRectangle(Pens.Gray, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == BmClick)
            {
                Text = "Legacy clicked";
                Invalidate();
                m.Result = 0;
                return;
            }

            base.WndProc(ref m);
        }

        sealed class Plain(Control owner) : ControlAccessibleObject(owner)
        {
            public override AccessibleRole Role => AccessibleRole.StaticText;

            public override string DefaultAction => "";
        }
    }
}
