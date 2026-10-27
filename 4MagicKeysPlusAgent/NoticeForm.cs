namespace _4MagicKeysPlusAgent;

// A small always-on-top notice that never takes focus, so it can't swallow keystrokes
// the user is typing while it appears.
internal abstract class NoticeForm : Form {
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private const int GapDip = 8;

    // The notices that are on the screen, so that a new one is not put over them (all of them live on the UI thread).
    private static readonly List<NoticeForm> OpenNotices = [];

    protected static readonly Color Background = Color.FromArgb(0x2B, 0x2B, 0x2B);
    protected static readonly Color CriticalColor = Color.FromArgb(0xE8, 0x11, 0x23);

    protected NoticeForm() {
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Padding = new Padding(1);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams {
        get {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    // Zero: as wide as the content is. Notices that can be on the screen together are better of one width, so that they
    // make a column. The form grows to its content, so the width is that of the content, the one control the form holds.
    protected virtual int FixedWidthDip => 0;

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
}
