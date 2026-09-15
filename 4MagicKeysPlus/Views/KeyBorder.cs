using _4MagicKeysPlus.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace _4MagicKeysPlus.Views;

public class KeyBorder : Border {
    private static readonly BrushConverter BrushConverter = new();

    private Brush? originalBackground;
    private Brush? originalBorderBrush;
    private List<KeyBorder>? mergedGroup;
    
    public KeyBorder() : base() {
        KeyLayoutEntry = new KeyLayoutEntry(PhysicalKey.None, "");
    }

    public KeyBorder(KeyLayoutEntry keyLayoutEntry) : base() {
        KeyLayoutEntry = keyLayoutEntry;
    }

    public KeyLayoutEntry KeyLayoutEntry {
        get;
    }

    //public Brush HighlightBackground { get; set; } = (Brush)BrushConverter.ConvertFrom("#CCFBF1")!;
    //public Brush HighlightBorderBrush { get; set; } = (Brush)BrushConverter.ConvertFrom("#14B8A6")!;
    public Brush HighlightBackground { get; set; } = (Brush)BrushConverter.ConvertFrom("#D1FAE5")!;
    public Brush HighlightBorderBrush { get; set; } = (Brush)BrushConverter.ConvertFrom("#10B981")!;
    //public Brush HighlightBackground { get; set; } = (Brush)BrushConverter.ConvertFrom("#E0F2FE")!;
    //public Brush HighlightBorderBrush { get; set; } = (Brush)BrushConverter.ConvertFrom("#0EA5E9")!;
    //public Brush HighlightBackground { get; set; } = (Brush)BrushConverter.ConvertFrom("#E5F1FB")!;
    //public Brush HighlightBorderBrush { get; set; } = (Brush)BrushConverter.ConvertFrom("#0078D4")!;

    protected override void OnMouseEnter(MouseEventArgs e) {
        base.OnMouseEnter(e);

        List<KeyBorder> group = GetMergedGroup();
        group.ForEach(border => border.ApplyHighlight(true));
    }

    protected override void OnMouseLeave(MouseEventArgs e) {
        base.OnMouseLeave(e);

        List<KeyBorder> group = GetMergedGroup();
        if (group.Any(border => border.IsMouseOver)) {
            return;
        }

        group.ForEach(border => border.ApplyHighlight(false));
    }

    private List<KeyBorder> GetMergedGroup() {
        if (mergedGroup != null) {
            return mergedGroup;
        }

        mergedGroup = [this];
        if (Parent is Canvas parentCanvas) {
            foreach (KeyBorder otherBorder in parentCanvas.Children.OfType<KeyBorder>()) {
                if (otherBorder != this && AreMerged(KeyLayoutEntry, otherBorder.KeyLayoutEntry)) {
                    mergedGroup.Add(otherBorder);
                }
            }
        }
        return mergedGroup;
    }

    private void ApplyHighlight(bool isHighlighted) {
        if (isHighlighted) {
            if (Background != HighlightBackground && BorderBrush != HighlightBorderBrush) {
                originalBackground = Background;
                originalBorderBrush = BorderBrush;
            }
            Background = HighlightBackground;
            BorderBrush = HighlightBorderBrush;
        } else {
            Background = originalBackground;
            BorderBrush = originalBorderBrush;
        }
    }

    private static bool AreMerged(KeyLayoutEntry entry1, KeyLayoutEntry entry2) {
        return entry1.Kind == entry2.Kind
            && ((entry1.KeyRowPosition == KeyRowPosition.TWO_ROWS_UPPER_ROW && entry2.KeyRowPosition == KeyRowPosition.TWO_ROWS_LOWER_ROW)
                || (entry1.KeyRowPosition == KeyRowPosition.TWO_ROWS_LOWER_ROW && entry2.KeyRowPosition == KeyRowPosition.TWO_ROWS_UPPER_ROW));
    }
}
