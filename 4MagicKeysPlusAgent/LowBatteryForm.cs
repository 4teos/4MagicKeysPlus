namespace _4MagicKeysPlusAgent;

internal sealed class LowBatteryForm : NoticeForm {
    private static readonly Color WarningColor = Color.FromArgb(0xFF, 0x8C, 0x00);

    public LowBatteryForm(int percent, string? keyboardName = null)
        : base(percent <= BatteryAlertPolicy.CriticalPercent ? CriticalColor : WarningColor, BatteryGlyph(percent), Content(percent, keyboardName)) {
    }

    // Wide enough for the longest name of a keyboard (its model and an index) beside the glyph.
    protected override int FixedWidthDip => 420;

    private static Control Content(int percent, string? keyboardName) {
        List<Control> rows = [];
        if (keyboardName is not null) {
            rows.Add(new Label {
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.Gainsboro,
                Margin = Padding.Empty,
                Text = keyboardName,
            });
        }
        rows.Add(new Label {
            AutoSize = true,
            Font = new Font("Segoe UI", 11f),
            ForeColor = Color.White,
            Margin = Padding.Empty,
            Text = string.Format(Strings.LowBatteryMessage, percent),
        });
        return Column(rows);
    }

    // Battery0 (U+E850, empty) .. Battery9 (U+E859) are contiguous; the levels above are never asked for, a notice is only due for a low charge.
    private static string BatteryGlyph(int percent) {
        return ((char)(0xE850 + Math.Clamp(percent / 10, 0, 10))).ToString();
    }
}
