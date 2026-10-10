using _4MagicKeysPlus.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace _4MagicKeysPlus.Views;

public class KeyBorder : Border {
    private static readonly BrushConverter BrushConverter = new();

    private static readonly Brush NormalBackground = Brushes.White;
    private static readonly Brush NormalBorderBrush = (Brush)BrushConverter.ConvertFrom("#9CA3AF")!;
    private static readonly Brush AccentBorderBrush = (Brush)BrushConverter.ConvertFrom("#10B981")!;
    private static readonly Brush SelectedBackground = (Brush)BrushConverter.ConvertFrom("#D1FAE5")!;

    private const double HoverThicknessMultiplier = 2;

    private Thickness normalBorderThickness = new(1);
    private bool isHovered;
    private List<KeyBorder>? mergedGroup;

    public KeyBorder() : this(new KeyLayoutEntry(PhysicalKey.None, "")) {
    }

    public KeyBorder(KeyLayoutEntry keyLayoutEntry) : base() {
        KeyLayoutEntry = keyLayoutEntry;
        Background = NormalBackground;
        BorderBrush = NormalBorderBrush;
        BorderThickness = normalBorderThickness;
    }

    public KeyLayoutEntry KeyLayoutEntry {
        get;
    }

    public bool IsSelected {
        get; private set;
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
        GetMergedGroup().ForEach(border => border.SetHovered(true));
    }

    protected override void OnMouseLeave(MouseEventArgs e) {
        base.OnMouseLeave(e);

        List<KeyBorder> group = GetMergedGroup();
        if (group.Any(border => border.IsMouseOver)) {
            return;
        }

        group.ForEach(border => border.SetHovered(false));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
        base.OnMouseLeftButtonDown(e);
        ToggleSelected();
    }

    private void SetHovered(bool hovered) {
        isHovered = hovered;
        ApplyVisualState();
    }

    private void ToggleSelected() {
        bool newState = !IsSelected;

        if (Parent is Canvas canvas) {
            foreach (KeyBorder otherBorder in canvas.Children.OfType<KeyBorder>()) {
                if (otherBorder != this && !AreMerged(KeyLayoutEntry, otherBorder.KeyLayoutEntry)) {
                    otherBorder.SetSelected(false);
                }
            }
        }

        GetMergedGroup().ForEach(border => border.SetSelected(newState));
    }

    private void SetSelected(bool selected) {
        IsSelected = selected;
        ApplyVisualState();
    }

    private void ApplyVisualState() {
        if (IsSelected) {
            Background = SelectedBackground;
            BorderBrush = AccentBorderBrush;
            BorderThickness = Scale(normalBorderThickness, HoverThicknessMultiplier);
        } else if (isHovered) {
            Background = NormalBackground;
            BorderBrush = AccentBorderBrush;
            BorderThickness = Scale(normalBorderThickness, HoverThicknessMultiplier);
        } else {
            Background = NormalBackground;
            BorderBrush = NormalBorderBrush;
            BorderThickness = normalBorderThickness;
        }
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
