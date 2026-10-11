using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace _4MagicKeysPlus.Views;

public partial class KeysView : UserControl {
    private const double KeyCornerRadius = 4;
    private const double UnitSizeInDIP = 8;

    private const double ChipHeight = 28;
    private const double ChipMargin = 2;

    private readonly KeyRemapEditor editor;
    private KeyboardModel? model;
    private PhysicalKey? selectedKey;
    private bool isPicking;
    private bool withFn;
    private bool followFnLock;
    private Window? hostWindow;

    public KeysView(KeyRemapEditor editor, string? placeholderNotice = null) {
        InitializeComponent();
        this.editor = editor;

        if (!string.IsNullOrEmpty(placeholderNotice)) {
            PlaceholderNotice.Text = placeholderNotice;
            PlaceholderNotice.Visibility = Visibility.Visible;
        }

        Loaded += (_, _) => {
            hostWindow = Window.GetWindow(this);
            hostWindow?.PreviewKeyDown += HostWindow_PreviewKeyDown;
        };
        Unloaded += (_, _) => {
            hostWindow?.PreviewKeyDown -= HostWindow_PreviewKeyDown;
            hostWindow = null;
        };

        ModelSelector.Items.Add(KeyboardModel.A1644);
        ModelSelector.Items.Add(KeyboardModel.A1843);

        DetectedKeyboard? detectedKeyboard = KeyboardDetector.DetectConnectedKeyboard();
        ModelSelector.SelectedValue = detectedKeyboard == null ? null : KeyboardModel.From(detectedKeyboard.Product);
    }

    private void ModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        model = e.AddedItems.OfType<KeyboardModel>().FirstOrDefault();
        selectedKey = null;
        isPicking = false;
        Refresh();
    }

    // The strip of extra keys is as wide as the keyboard.
    private void KeyboardViewbox_SizeChanged(object sender, SizeChangedEventArgs e) {
        VirtualKeysHost.Width = e.NewSize.Width;
    }

    private void HostWindow_PreviewKeyDown(object sender, KeyEventArgs e) {
        if (e.Key == Key.Escape && isPicking) {
            CancelPicking();
            e.Handled = true;
        }
    }

    private void KeyBorder_Clicked(object? sender, EventArgs e) {
        KeyBorder border = (KeyBorder)sender!;

        if (isPicking) {
            CompletePicking(border.Tag is RemapTarget target ? target : border.KeyLayoutEntry.Kind);
        } else {
            PhysicalKey key = border.KeyLayoutEntry.Kind;
            SelectKey(selectedKey == key ? null : key);
        }
    }

    private void SelectKey(PhysicalKey? key) {
        selectedKey = key;
        ResetOptions();

        foreach (KeyBorder border in KeysCanvas.Children.OfType<KeyBorder>()) {
            border.SetSelected(border.KeyLayoutEntry.Kind == selectedKey);
        }
        UpdatePanel();
    }

    private void SetButton_Click(object sender, RoutedEventArgs e) {
        isPicking = true;
        Refresh();
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e) {
        if (selectedKey is PhysicalKey key) {
            editor.RemoveMappings(key);
            Refresh();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) {
        CancelPicking();
    }

    private void WithFnCheck_Click(object sender, RoutedEventArgs e) {
        withFn = WithFnCheck.IsChecked == true;
        Refresh();
    }

    private void FollowFnLockCheck_Click(object sender, RoutedEventArgs e) {
        followFnLock = FollowFnLockCheck.IsChecked == true;
    }

    private void CancelPicking() {
        isPicking = false;
        Refresh();
    }

    private void CompletePicking(RemapTarget target) {
        if (selectedKey is PhysicalKey source) {
            editor.SetMapping(source, target, withFn, followFnLock);
        }
        isPicking = false;
        Refresh();
    }

    // Defaults of the options for the selected key: the layer is fixed when the key can be
    // remapped in one layer only, FnLockAware starts as the key's current Fn-layer rule has it.
    private void ResetOptions() {
        if (selectedKey is not PhysicalKey key) {
            return;
        }

        withFn = KeyRemapRules.CanRemap(key, fnLayer: true) && !KeyRemapRules.CanRemap(key);
        followFnLock = editor.Rules.FirstOrDefault(rule => rule.FnLayer && rule.Source.Count == 1 && rule.Source.Contains(key))?.FnLockAware ?? false;
    }

    private void Refresh() {
        Render();
        UpdatePanel();
    }

    private void UpdatePanel() {
        if (model == null || selectedKey is not PhysicalKey key) {
            StatusPanel.Visibility = Visibility.Collapsed;
            return;
        }

        DriverKeyRemapConfig config = editor.Config;
        bool canRemapPlain = KeyRemapRules.CanRemap(key);
        bool canRemapWithFn = KeyRemapRules.CanRemap(key, fnLayer: true);

        StatusPanel.Visibility = Visibility.Visible;
        PanelKeyHost.Content = BuildKeyTile(model, key);
        PanelKeyName.Text = PhysicalKeyNames.GetDisplayName(key);

        if (isPicking) {
            PanelMapping.Text = Strings.KeyPickPrompt;
        } else {
            List<string> lines = [.. config.Rules.Where(rule => rule.Source.Contains(key)).Select(DescribeRule)];
            PanelMapping.Text = lines.Count > 0
                ? string.Join(Environment.NewLine, lines)
                : canRemapPlain || canRemapWithFn ? Strings.KeyNotRemapped : Strings.KeyCannotRemap;
        }

        WithFnCheck.IsChecked = withFn;
        WithFnCheck.IsEnabled = canRemapPlain && canRemapWithFn;
        FollowFnLockCheck.IsChecked = withFn && followFnLock;
        FollowFnLockCheck.IsEnabled = withFn;

        SetButton.Visibility = isPicking ? Visibility.Collapsed : Visibility.Visible;
        SetButton.IsEnabled = canRemapPlain || canRemapWithFn;
        RemoveButton.Visibility = isPicking ? Visibility.Collapsed : Visibility.Visible;
        RemoveButton.IsEnabled = config.HasRules(key);
        CancelButton.Visibility = isPicking ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string DescribeRule(KeyRule rule) {
        string description = $"\u2192 {rule.DescribeTarget()}";
        if (!rule.FnLayer) {
            return description;
        }

        description = $"{Strings.KeyRuleWithFn} {description}";
        return rule.FnLockAware ? $"{description} ({Strings.KeyRuleFollowsFnLock})" : description;
    }

    private static KeyBorder BuildKeyTile(KeyboardModel model, PhysicalKey key) {
        KeyLayoutEntry? entry = KeyboardLayout.GetKeys(model).SelectMany(row => row.RowKeys).FirstOrDefault(candidate => candidate.Kind == key);
        string text = string.IsNullOrEmpty(entry?.Label) ? PhysicalKeyNames.GetDisplayName(key) : entry.Label;
        KeyBorder tile = BuildKeyChip(key, text, ChipHeight + 8, 0);
        tile.IsInteractive = false;
        return tile;
    }

    private static KeyBorder BuildKeyChip(RemapTarget target, string text, double height, double margin) {
        KeyBorder chip = new(new KeyLayoutEntry(target.Key, text)) {
            Tag = target,
            Height = height,
            MinWidth = height,
            Margin = new Thickness(margin),
            CornerRadius = new CornerRadius(KeyCornerRadius),
            Child = new TextBlock {
                Text = text,
                FontSize = 11,
                Padding = new Thickness(6, 0, 6, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
            },
        };
        return chip;
    }

    // A media target is drawn as its glyph; the name is in the tooltip.
    private static KeyBorder BuildTargetChip(RemapTarget target) {
        string? glyph = target.IsMedia ? Glyphs.ForConsumerUsage(target.ConsumerUsage) : null;
        KeyBorder chip = BuildKeyChip(target, glyph ?? target.DisplayName, ChipHeight, ChipMargin);
        if (glyph != null) {
            ((TextBlock)chip.Child).FontFamily = new FontFamily("Segoe MDL2 Assets");
            ((TextBlock)chip.Child).FontSize = 14;
        }
        if (target.IsMedia) {
            chip.ToolTip = new ToolTip {
                Content = target.DisplayName,
                Style = (Style)Application.Current.Resources["TooltipBelow"],
            };
            ToolTipService.SetInitialShowDelay(chip, 0);
            ToolTipService.SetBetweenShowDelay(chip, 0);
        }
        return chip;
    }

    private void Render() {
        KeysCanvas.Children.Clear();
        VirtualKeysPanel.Children.Clear();
        VirtualKeysHost.Visibility = Visibility.Collapsed;

        double rowY = 0.0;
        double keyX = 0.0;
        double maxWidth = 0.0;
        double maxHeight = 0.0;

        if (model == null) {
            return;
        }

        IReadOnlyList<RowLayoutEntry> keysLayout = KeyboardLayout.GetKeys(model);
        DriverKeyRemapConfig remapConfig = editor.Config;

        // While a target is being picked, the current remaps are hidden and the keys that cannot
        // be the target are switched off.
        IReadOnlySet<PhysicalKey> remappedKeys = isPicking ? new HashSet<PhysicalKey>() : remapConfig.GetRemappedKeys();
        IReadOnlyList<RemapTarget> allowedTargets = isPicking && selectedKey is PhysicalKey source
            ? KeyRemapRules.GetAllowedTargets(source, withFn)
            : [];

        foreach (RowLayoutEntry row in keysLayout) {
            for (int i = 0; i < row.RowKeys.Count; i++) {
                KeyLayoutEntry key = row.RowKeys[i];
                bool isRemapped = remappedKeys.Contains(key.Kind);
                KeyBorder border = new(key) {
                    Width = CalcSizeInDIP(key.Width),
                    Height = CalcHeight(key.KeyRowPosition),
                    NormalBorderThickness = key.KeyRowPosition == KeyRowPosition.TWO_ROWS_LOWER_ROW ? new Thickness(1, 0, 1, 1) : new Thickness(1),
                    CornerRadius = CalcCornerRadius(key),
                    Child = BuildKeyLabel(key, isRemapped),
                };
                if (isPicking) {
                    bool isTarget = allowedTargets.Contains((RemapTarget)key.Kind);
                    border.IsInteractive = isTarget;
                    border.LooksDisabled = !isTarget;
                } else {
                    border.SetSelected(key.Kind == selectedKey);
                }
                border.Clicked += KeyBorder_Clicked;
                if (isRemapped) {
                    border.ToolTip = BuildKeyToolTip(remapConfig, key.Kind);
                    ToolTipService.SetInitialShowDelay(border, 0);
                    ToolTipService.SetBetweenShowDelay(border, 0);
                }

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

        RenderVirtualKeys(keysLayout, allowedTargets);
    }

    // The targets this keyboard has no key for (F20, Print Screen, media keys, ...) are offered as extra keys.
    private void RenderVirtualKeys(IReadOnlyList<RowLayoutEntry> keysLayout, IReadOnlyList<RemapTarget> allowedTargets) {
        HashSet<PhysicalKey> drawnKeys = [.. keysLayout.SelectMany(row => row.RowKeys).Select(key => key.Kind)];

        foreach (RemapTarget target in allowedTargets.Where(target => target.IsMedia || !drawnKeys.Contains(target.Key))) {
            KeyBorder chip = BuildTargetChip(target);
            chip.Clicked += KeyBorder_Clicked;
            VirtualKeysPanel.Children.Add(chip);
        }
        VirtualKeysHost.Visibility = VirtualKeysPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static TextBlock? BuildKeyLabel(KeyLayoutEntry key, bool isRemapped) {
        if (string.IsNullOrEmpty(key.Label)) {
            return null;
        }

        TextBlock label = new() {
            Text = key.Label,
            FontSize = 11,
            Padding = new Thickness(6),
            HorizontalAlignment = key.LabelHAlignment,
            VerticalAlignment = key.LabelVAlignment,
            IsHitTestVisible = false,
        };
        if (isRemapped) {
            label.FontWeight = FontWeights.Bold;
            label.SetResourceReference(TextBlock.ForegroundProperty, ThemeKeys.KeyRemappedForeground);
        }
        return label;
    }

    private static ToolTip BuildKeyToolTip(DriverKeyRemapConfig remapConfig, PhysicalKey key) {
        return new ToolTip {
            Content = string.Join(Environment.NewLine, remapConfig.DescribeTargets(key)),
            Style = (Style)Application.Current.Resources["TooltipAbove"],
        };
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
