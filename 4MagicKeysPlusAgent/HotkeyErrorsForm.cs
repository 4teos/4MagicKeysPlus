namespace _4MagicKeysPlusAgent;

// Stays until clicked: unlike a battery level it needs reading, and the configuration app may not be open to show it.
internal sealed class HotkeyErrorsForm : NoticeForm {
    private const int MaxTextWidth = 560;

    public HotkeyErrorsForm(IReadOnlyList<string> failures) {
        BackColor = CriticalColor;

        Label glyph = new Label {
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Font = new Font("Segoe MDL2 Assets", 20f),
            ForeColor = CriticalColor,
            Margin = new Padding(14, 10, 8, 10),
            Text = "",
        };

        FlowLayoutPanel messages = new FlowLayoutPanel {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Left,
            BackColor = Background,
            FlowDirection = FlowDirection.TopDown,
            Margin = new Padding(0, 10, 18, 4),
            WrapContents = false,
        };
        messages.Controls.Add(TextLabel(Strings.HotkeyErrorsTitle, Padding.Empty));
        foreach (string failure in failures) {
            messages.Controls.Add(FailureRow(failure));
        }

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
        layout.Controls.Add(glyph, 0, 0);
        layout.SetRowSpan(glyph, 2);
        layout.Controls.Add(messages, 1, 0);
        layout.Controls.Add(dismiss, 1, 1);
        Controls.Add(layout);

        CloseOnClick(this);
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

    private void CloseOnClick(Control control) {
        control.Click += (_, _) => Close();
        foreach (Control child in control.Controls) {
            CloseOnClick(child);
        }
    }
}
