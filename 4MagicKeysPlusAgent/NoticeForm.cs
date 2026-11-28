namespace _4MagicKeysPlusAgent;

// A small always-on-top notice that never takes focus, so it can't swallow keystrokes the user is typing while it appears.
// It stays until clicked: the user may not be looking at the screen when it shows up.
internal abstract class NoticeForm : Form {
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private const int GapDip = 8;

    // Only the UI thread touches it.
    private static readonly List<NoticeForm> OpenNotices = [];

    protected static readonly Color Background = Color.FromArgb(0x2B, 0x2B, 0x2B);
    protected static readonly Color CriticalColor = Color.FromArgb(0xE8, 0x11, 0x23);

    protected NoticeForm(Color accent, string glyph, Control content) {
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Padding = new Padding(1);
        BackColor = accent;

        Label glyphLabel = new Label {
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Font = new Font("Segoe MDL2 Assets", 20f),
            ForeColor = accent,
            Margin = new Padding(14, 10, 8, 10),
            Text = glyph,
        };

        content.Anchor = AnchorStyles.Left;
        content.BackColor = Background;
        content.Margin = new Padding(0, 10, 18, 4);

        Label dismiss = new Label {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.Gray,
            Margin = new Padding(0, 0, 18, 10),
            Text = Strings.NoticeDismissHint,
        };

        TableLayoutPanel layout = new TableLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Background,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            RowCount = 2,
        };
        layout.Controls.Add(glyphLabel, 0, 0);
        layout.SetRowSpan(glyphLabel, 2);
        layout.Controls.Add(content, 1, 0);
        layout.Controls.Add(dismiss, 1, 1);
        Controls.Add(layout);

        CloseOnClick(this);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams {
        get {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    // Zero: as wide as the content. Notices that can be on the screen together are better of one width, so that they make a column.
    protected virtual int FixedWidthDip => 0;

    protected static FlowLayoutPanel Column(IEnumerable<Control> rows) {
        FlowLayoutPanel column = new FlowLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        column.Controls.AddRange([.. rows]);
        return column;
    }

    protected override void OnLoad(EventArgs e) {
        base.OnLoad(e);
        if (FixedWidthDip > 0 && Controls.Count > 0) {
            Controls[0].MinimumSize = new Size(FixedWidthDip * DeviceDpi / 96 - Padding.Horizontal, 0);
        }
        PerformLayout();
        Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = NoticeLayout.Place(area, Size, [.. OpenNotices.Select(notice => notice.Bounds)], GapDip * DeviceDpi / 96);
        OpenNotices.Add(this);
    }

    protected override void OnFormClosed(FormClosedEventArgs e) {
        OpenNotices.Remove(this);
        base.OnFormClosed(e);
    }

    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        TopmostWindow.Result result = TopmostWindow.Ensure(Handle);
#if DEBUG
        Logger.Info($"Popup topmost: {result}");
#endif
    }

    private void CloseOnClick(Control control) {
        control.Click += (_, _) => Close();
        foreach (Control child in control.Controls) {
            CloseOnClick(child);
        }
    }
}
