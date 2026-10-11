using _4MagicKeysPlus.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace _4MagicKeysPlus.Views;

public class KeyBorder : Border {
    private const double HoverThicknessMultiplier = 2;

    private Thickness normalBorderThickness = new(1);
    private bool isHovered;
    private bool looksDisabled;
    private List<KeyBorder>? mergedGroup;

    public KeyBorder() : this(new KeyLayoutEntry(PhysicalKey.None, "")) {
    }

    public KeyBorder(KeyLayoutEntry keyLayoutEntry) : base() {
        KeyLayoutEntry = keyLayoutEntry;
        ApplyVisualState();
    }

    public event EventHandler? Clicked;

    public KeyLayoutEntry KeyLayoutEntry {
        get;
    }

    // A key that is not interactive reacts neither to the mouse nor to clicks.
    public bool IsInteractive {
        get; set;
    } = true;

    public bool LooksDisabled {
        get => looksDisabled;
        set {
            looksDisabled = value;
            ApplyVisualState();
        }
    }

    public bool IsSelected {
        get; private set;
    }

    public void SetSelected(bool selected) {
        IsSelected = selected;
        ApplyVisualState();
    }

    public Thickness NormalBorderThickness {
        get => normalBorderThickness;
        set {
            normalBorderThickness = value;
            ApplyVisualState();
        }
    }

    protected override void OnMouseEnter(MouseEventArgs e) {
        base.OnMouseEnter(e);
        if (!IsInteractive) {
            return;
        }
        GetMergedGroup().ForEach(border => border.SetHovered(true));
    }

    protected override void OnMouseLeave(MouseEventArgs e) {
        base.OnMouseLeave(e);
        if (!IsInteractive) {
            return;
        }

        List<KeyBorder> group = GetMergedGroup();
        if (group.Any(border => border.IsMouseOver)) {
            return;
        }

        group.ForEach(border => border.SetHovered(false));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
        base.OnMouseLeftButtonDown(e);
        if (IsInteractive) {
            Clicked?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetHovered(bool hovered) {
        isHovered = hovered;
        ApplyVisualState();
    }

    private void ApplyVisualState() {
        bool emphasized = false;

        if (looksDisabled) {
            SetBrushes(ThemeKeys.KeyDisabledBackground, ThemeKeys.KeyDisabledBorder, ThemeKeys.KeyDisabledForeground);
        } else {
            emphasized = IsSelected || isHovered;
            SetBrushes(
                IsSelected ? ThemeKeys.KeySelectedBackground : ThemeKeys.KeyBackground,
                emphasized ? ThemeKeys.KeyAccentBorder : ThemeKeys.KeyBorder,
                ThemeKeys.KeyForeground);
        }

        // The thicker border grows inward: the padding gives up exactly what the border takes, so
        // the key and its content neither resize nor move.
        Thickness thickest = Scale(normalBorderThickness, HoverThicknessMultiplier);
        BorderThickness = emphasized ? thickest : normalBorderThickness;
        Padding = new Thickness(
            thickest.Left - BorderThickness.Left,
            thickest.Top - BorderThickness.Top,
            thickest.Right - BorderThickness.Right,
            thickest.Bottom - BorderThickness.Bottom);
    }

    // Resource references, not brushes, so that the key follows a theme switch.
    private void SetBrushes(string background, string border, string foreground) {
        SetResourceReference(BackgroundProperty, background);
        SetResourceReference(BorderBrushProperty, border);
        SetResourceReference(TextElement.ForegroundProperty, foreground);
    }

    private static Thickness Scale(Thickness thickness, double factor) {
        return new Thickness(thickness.Left * factor, thickness.Top * factor, thickness.Right * factor, thickness.Bottom * factor);
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

    private static bool AreMerged(KeyLayoutEntry entry1, KeyLayoutEntry entry2) {
        return entry1.Kind == entry2.Kind
            && ((entry1.KeyRowPosition == KeyRowPosition.TWO_ROWS_UPPER_ROW && entry2.KeyRowPosition == KeyRowPosition.TWO_ROWS_LOWER_ROW)
                || (entry1.KeyRowPosition == KeyRowPosition.TWO_ROWS_LOWER_ROW && entry2.KeyRowPosition == KeyRowPosition.TWO_ROWS_UPPER_ROW));
    }
}
