using Deskbridge.Core.Policy;
using Deskbridge.Core.Tools;

namespace Deskbridge.Ui;

/// <summary>
/// The confirmation window. It appears near the tray, stays on top and never takes focus: clicking its
/// buttons works without activating it, so the user's window keeps the keyboard.
/// </summary>
internal sealed class ConfirmationForm : Form
{
    const int WsExNoActivate = 0x08000000;
    const int WsExTopmost = 0x00000008;
    const int WsExToolWindow = 0x00000080;

    readonly Action<ConfirmationAnswer> answer;
    readonly System.Windows.Forms.Timer timeout;
    readonly TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    bool answered;

    /// <summary>Completes once the window's handle is destroyed.</summary>
    public Task Gone => closed.Task;

    public ConfirmationForm(ConfirmationRequest request, TimeSpan limit, Action<ConfirmationAnswer> answer)
    {
        this.answer = answer;
        Text = "Deskbridge";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(440, 250);
        Font = new Font("Segoe UI", 9.5f);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12), AutoSize = false };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Name = "HeaderText",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Text = Header(request),
            MaximumSize = new Size(410, 0),
        });
        layout.Controls.Add(new Label
        {
            Name = "ActionText",
            AccessibleName = request.Action,
            AutoSize = true,
            Text = request.Action,
            MaximumSize = new Size(410, 0),
        });
        layout.Controls.Add(new TextBox
        {
            Name = "ContentBox",
            AccessibleName = "Exact content",
            ReadOnly = true,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9.5f),
            Text = request.Content ?? "",
            Visible = !string.IsNullOrEmpty(request.Content),
        });
        layout.Controls.Add(new Label
        {
            Name = "NoteText",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Text = request.Note ?? "",
            Visible = !string.IsNullOrEmpty(request.Note),
            MaximumSize = new Size(410, 0),
        });

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.Add(Button("DenyButton", "Deny", ConfirmationAnswer.Deny));
        if (request.CanRemember)
        {
            buttons.Controls.Add(Button("RememberButton", "Allow and remember", ConfirmationAnswer.AllowAndRemember));
        }

        buttons.Controls.Add(Button("AllowButton", "Allow", ConfirmationAnswer.Allow));
        layout.Controls.Add(buttons);
        Controls.Add(layout);

        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);

        timeout = new System.Windows.Forms.Timer { Interval = (int)Math.Max(1, limit.TotalMilliseconds) };
        timeout.Tick += (_, _) => Finish(ConfirmationAnswer.TimedOut);
        timeout.Start();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExNoActivate | WsExTopmost | WsExToolWindow;
            return cp;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        Finish(ConfirmationAnswer.Deny); // closing the window is a no
        base.OnFormClosing(e);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        base.OnHandleDestroyed(e);
        closed.TrySetResult(); // the window no longer exists, so the next one can never be mistaken for it
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timeout.Dispose();
        }

        base.Dispose(disposing);
    }

    static string Header(ConfirmationRequest request) => request.Kind switch
    {
        ConfirmationKind.ClientApproval => "A program wants to use Deskbridge",
        _ when request.Effect == ToolEffect.Destructive => $"{request.ClientName} wants to delete or send in {request.App.DisplayName}",
        _ => $"{request.ClientName} wants to change {request.App.DisplayName}",
    };

    Button Button(string name, string text, ConfirmationAnswer result)
    {
        var button = new Button { Name = name, Text = text, AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
        button.Click += (_, _) => Finish(result);
        return button;
    }

    void Finish(ConfirmationAnswer result)
    {
        if (answered)
        {
            return;
        }

        answered = true;
        timeout.Stop();
        answer(result);
        if (!IsDisposed)
        {
            BeginInvoke(Close);
        }
    }
}
