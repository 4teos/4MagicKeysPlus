namespace _4MagicKeysPlusAgent;

internal sealed class HotkeyErrorsForm : NoticeForm {
    private const int MaxTextWidth = 560;
    private const string WarningGlyph = "";

    public HotkeyErrorsForm(IReadOnlyList<string> failures) : base(CriticalColor, WarningGlyph, Content(failures)) {
    }

    private static Control Content(IReadOnlyList<string> failures) {
        return Column([TextLabel(Strings.HotkeyErrorsTitle, Padding.Empty), .. failures.Select(FailureRow)]);
    }

    private static Control FailureRow(string text) {
        TableLayoutPanel row = new TableLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Background,
            ColumnCount = 2,
            Margin = new Padding(0, 5, 0, 0),
            RowCount = 1,
        };
        row.Controls.Add(TextLabel("•", new Padding(0, 0, 8, 0)), 0, 0);
        row.Controls.Add(TextLabel(text, Padding.Empty), 1, 0);
        return row;
    }

    private static Label TextLabel(string text, Padding margin) {
        return new Label {
            AutoSize = true,
            MaximumSize = new Size(MaxTextWidth, 0),
            Font = new Font("Segoe UI", 11f),
            ForeColor = Color.White,
            Margin = margin,
            Text = text,
        };
    }
}
