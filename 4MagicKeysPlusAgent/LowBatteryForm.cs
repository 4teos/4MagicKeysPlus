namespace _4MagicKeysPlusAgent;

// Closes by itself or on a click anywhere.
internal sealed class LowBatteryForm : NoticeForm {
    private const int DismissDelayMs = 8000;

    private static readonly Color WarningColor = Color.FromArgb(0xFF, 0x8C, 0x00);

    private readonly System.Windows.Forms.Timer dismissTimer = new();

    // keyboardName is given when there are several keyboards, to tell the notices apart.
    public LowBatteryForm(int percent, string? keyboardName = null) {
        Color accent = percent <= BatteryAlertPolicy.CriticalPercent ? CriticalColor : WarningColor;

        BackColor = accent;

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
            Margin = Padding.Empty,
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
        FlowLayoutPanel text = new FlowLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Left,
            BackColor = Background,
            FlowDirection = FlowDirection.TopDown,
            Margin = new Padding(0, 10, 18, 10),
            WrapContents = false,
        };
        List<Control> clickable = [this, layout, glyph, text, message];
        if (keyboardName is not null) {
            Label name = new Label {
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.Gainsboro,
                Margin = Padding.Empty,
                Text = keyboardName,
            };
            text.Controls.Add(name);
            clickable.Add(name);
        }
        text.Controls.Add(message);
        layout.Controls.Add(text, 1, 0);
        Controls.Add(layout);

        foreach (Control control in clickable) {
            control.Click += (_, _) => Close();
        }

        dismissTimer.Interval = DismissDelayMs;
        dismissTimer.Tick += (_, _) => Close();
    }

    // Wide enough for the longest name of a keyboard (its model and an index) beside the glyph.
    protected override int FixedWidthDip => 420;

    protected override void OnLoad(EventArgs e) {
        base.OnLoad(e);
        dismissTimer.Start();
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            dismissTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    // Segoe MDL2 Assets Battery0 (U+E850, empty) .. Battery9 (U+E859) are contiguous; a notice is only
    // due for a low charge, so the levels above, where the range does not go on, are never asked for.
    private static string BatteryGlyph(int percent) {
        return ((char)(0xE850 + Math.Clamp(percent / 10, 0, 10))).ToString();
    }
}
