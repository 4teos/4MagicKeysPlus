namespace _4MagicKeysPlusAgent;

// A small always-on-top notice that never takes focus, so it can't swallow keystrokes
// the user is typing while it appears.
internal abstract class NoticeForm : Form {
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

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

    protected override void OnLoad(EventArgs e) {
        base.OnLoad(e);
        PerformLayout();
        Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + area.Height * 3 / 4 - Height / 2);
    }

    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        TopmostWindow.Result result = TopmostWindow.Ensure(Handle);
#if DEBUG
        Logger.Info($"Popup topmost: {result}");
#endif
    }
}
