using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace _4MagicKeysPlus.Views;

public partial class KeysView : UserControl {
    private const double KeyCornerRadius = 4;
    private const double UnitSizeInDIP = 8;

    public KeysView() {
        InitializeComponent();

        ModelSelector.Items.Add(KeyboardModel.A1644);
        ModelSelector.Items.Add(KeyboardModel.A1843);

        DetectedKeyboard? detectedKeyboard = KeyboardDetector.DetectConnectedKeyboard();
        ModelSelector.SelectedValue = detectedKeyboard?.Model;
    }

    private void ModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        KeyboardModel? model = e.AddedItems.OfType<KeyboardModel>().FirstOrDefault();
        Render(model);
    }

    private void Render(KeyboardModel? model) {
        KeysCanvas.Children.Clear();

        double rowY = 0.0;
        double keyX = 0.0;
        double maxWidth = 0.0;
        double maxHeight = 0.0;

        if (model == null) {
            return;
        }

        IReadOnlyList<RowLayoutEntry> keysLayout = KeyboardLayout.GetKeys(model);

        foreach (RowLayoutEntry row in keysLayout) {
            for (int i = 0; i < row.RowKeys.Count; i++) {
                KeyLayoutEntry key = row.RowKeys[i];
                KeyBorder border = new(key) {
                    Width = CalcSizeInDIP(key.Width),
                    Height = CalcHeight(key.KeyRowPosition),
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF)),
                    BorderThickness = key.KeyRowPosition == KeyRowPosition.TWO_ROWS_LOWER_ROW ? new Thickness(1, 0, 1, 1) : new Thickness(1),
                    CornerRadius = CalcCornerRadius(key),
                    Child = String.IsNullOrEmpty(key.Label)
                                ? null
                                : new TextBlock {
                                    Text = key.Label,
                                    FontSize = 11,
                                    Padding = new Thickness(6),
                                    HorizontalAlignment = key.LabelHAlignment,
                                    VerticalAlignment = key.LabelVAlignment,
                                    IsHitTestVisible = false
                                },
                };

                Canvas.SetLeft(border, keyX);
                Canvas.SetTop(border, CalcKeyTop(key.KeyRowPosition, rowY));
                KeysCanvas.Children.Add(border);

                keyX += CalcKeyXIncrement(key, i < row.RowKeys.Count - 1 ? row.RowKeys[i + 1] : null);
            }

            rowY += CalcSizeInDIP(SizeUnit.STANDARD_KEY);
            maxWidth = Math.Max(maxWidth, keyX);
            maxHeight = Math.Max(maxHeight, rowY);
            keyX = 0;
            rowY += CalcSizeInDIP(SizeUnit.STANDARD_GAP);
        }

        KeysCanvas.Width = maxWidth;
        KeysCanvas.Height = maxHeight;
    }

    private static CornerRadius CalcCornerRadius(KeyLayoutEntry key) {
        return key.KeyRowPosition switch {
            KeyRowPosition.NORMAL or KeyRowPosition.HALF_ROW_LOWER or KeyRowPosition.HALF_ROW_UPPER => new CornerRadius(KeyCornerRadius),
            KeyRowPosition.TWO_ROWS_UPPER_ROW => new CornerRadius(KeyCornerRadius, KeyCornerRadius, 0, key.Kind == PhysicalKey.Enter ? KeyCornerRadius : 0),
            KeyRowPosition.TWO_ROWS_LOWER_ROW => new CornerRadius(0, 0, KeyCornerRadius, KeyCornerRadius),
            _ => throw new ArgumentOutOfRangeException("Invalid value: " + key.KeyRowPosition),
        };
    }

    private static double CalcHeight(KeyRowPosition rowPosition) {
        return rowPosition switch {
            KeyRowPosition.TWO_ROWS_LOWER_ROW => CalcSizeInDIP(SizeUnit.STANDARD_KEY + SizeUnit.STANDARD_GAP) + 2.0,
            KeyRowPosition.HALF_ROW_UPPER or KeyRowPosition.HALF_ROW_LOWER => CalcSizeInDIP(SizeUnit.STANDARD_KEY / 2),
            _ => CalcSizeInDIP(SizeUnit.STANDARD_KEY),
        };
    }

    private static double CalcKeyTop(KeyRowPosition rowPosition, double rowY) {
        return rowPosition switch {
            KeyRowPosition.TWO_ROWS_LOWER_ROW => rowY - CalcSizeInDIP(SizeUnit.STANDARD_GAP) - 2.0,
            KeyRowPosition.HALF_ROW_LOWER => rowY + CalcSizeInDIP(SizeUnit.STANDARD_KEY / 2),
            _ => rowY,
        };
    }

    private static double CalcKeyXIncrement(KeyLayoutEntry key, KeyLayoutEntry? nextKey) {
        if ((key.KeyRowPosition == KeyRowPosition.HALF_ROW_UPPER && nextKey?.KeyRowPosition == KeyRowPosition.HALF_ROW_LOWER)
            || (key.KeyRowPosition == KeyRowPosition.HALF_ROW_LOWER && nextKey?.KeyRowPosition == KeyRowPosition.HALF_ROW_UPPER)) {
            return 0;
        }

        return CalcSizeInDIP(key.Width) + CalcSizeInDIP(key.GapAfter);
    }

    private static double CalcSizeInDIP(SizeUnit keyWidth) {
        return keyWidth.Value * UnitSizeInDIP;
    }
}
