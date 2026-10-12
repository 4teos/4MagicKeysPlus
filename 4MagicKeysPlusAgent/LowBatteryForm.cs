namespace _4MagicKeysPlusAgent;

// A small always-on-top notice that never takes focus, so it can't swallow keystrokes
// the user is typing while it appears. Closes by itself or on a click anywhere.
internal sealed class LowBatteryForm : Form {
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int DismissDelayMs = 8000;

    private static readonly Color Background = Color.FromArgb(0x2B, 0x2B, 0x2B);
    private static readonly Color WarningColor = Color.FromArgb(0xFF, 0x8C, 0x00);
    private static readonly Color CriticalColor = Color.FromArgb(0xE8, 0x11, 0x23);

    private readonly System.Windows.Forms.Timer dismissTimer = new();

    public LowBatteryForm(int percent) {
        Color accent = percent <= BatteryAlertPolicy.CriticalPercent ? CriticalColor : WarningColor;

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = accent;
        Padding = new Padding(1);

        Label glyph = new Label {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font("Segoe MDL2 Assets", 20f),
            ForeColor = accent,
            Margin = new Padding(14, 10, 8, 10),
            Text = BatteryGlyph(percent),
        };
        Label message = new Label {
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = new Font("Segoe UI", 11f),
            ForeColor = Color.White,
            Margin = new Padding(0, 10, 18, 10),
            Text = string.Format(Strings.LowBatteryMessage, percent),
        };

        TableLayoutPanel layout = new TableLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Background,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            RowCount = 1,
        };
        layout.Controls.Add(glyph, 0, 0);
        layout.Controls.Add(message, 1, 0);
        Controls.Add(layout);

        foreach (Control control in new Control[] { this, layout, glyph, message }) {
            control.Click += (_, _) => Close();
        }

        dismissTimer.Interval = DismissDelayMs;
        dismissTimer.Tick += (_, _) => Close();
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
        dismissTimer.Start();
    }

    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        TopmostWindow.Result result = TopmostWindow.Ensure(Handle);
#if DEBUG
        Logger.Info($"Popup topmost: {result}");
#endif
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            dismissTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    // Segoe MDL2 Assets Battery0 (U+E850, empty) .. Battery10 (U+E85A, full) - the same
    // verified range the config UI's status icon uses.
    private static string BatteryGlyph(int percent) {
        return ((char)(0xE850 + Math.Clamp(percent / 10, 0, 10))).ToString();
    }
}
