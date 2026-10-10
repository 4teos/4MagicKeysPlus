using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Threading;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.Views;

namespace _4MagicKeysPlus.ViewModels;

// Lives for the whole app session (owned by MainWindow), not per-navigation-visit like the old
// per-screen view models - the header needs to show live status regardless of which page is open.
public sealed class DeviceStatusViewModel : INotifyPropertyChanged {
    private static readonly SolidColorBrush LowBatteryBrush = new(Color.FromRgb(0xEF, 0x44, 0x44));
    private static readonly SolidColorBrush MediumBatteryBrush = new(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly SolidColorBrush GoodBatteryBrush = new(Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly SolidColorBrush UnknownBatteryBrush = new(Color.FromRgb(0x9C, 0xA3, 0xAF));
    private static readonly SolidColorBrush FnLockOnBackground = new(Color.FromRgb(0x37, 0x41, 0x51));
    private static readonly SolidColorBrush FnLockOffBackground = Brushes.Transparent;
    private static readonly SolidColorBrush FnLockOnForeground = Brushes.White;
    private static readonly SolidColorBrush FnLockOffForeground = new(Color.FromRgb(0x37, 0x41, 0x51));

    private readonly DispatcherTimer timer;
    private DetectedKeyboard? detectedKeyboard;
    private string deviceName = Strings.DeviceNotDetected;
    private string connectionGlyph = "";
    private string batteryGlyph = Glyphs.BatteryLevel(0);
    private Brush batteryColor = UnknownBatteryBrush;
    private bool fnLockEnabled;
    private Brush fnLockBackground = FnLockOffBackground;
    private Brush fnLockForeground = FnLockOffForeground;
    private string statusTooltip = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeviceStatusViewModel() {
        Refresh();

        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
    }

    public string DeviceName {
        get => deviceName;
        private set => SetField(ref deviceName, value);
    }

    public string ConnectionGlyph {
        get => connectionGlyph;
        private set => SetField(ref connectionGlyph, value);
    }

    public string BatteryGlyph {
        get => batteryGlyph;
        private set => SetField(ref batteryGlyph, value);
    }

    public Brush BatteryColor {
        get => batteryColor;
        private set => SetField(ref batteryColor, value);
    }

    public bool FnLockEnabled {
        get => fnLockEnabled;
        private set => SetField(ref fnLockEnabled, value);
    }

    public Brush FnLockBackground {
        get => fnLockBackground;
        private set => SetField(ref fnLockBackground, value);
    }

    public Brush FnLockForeground {
        get => fnLockForeground;
        private set => SetField(ref fnLockForeground, value);
    }

    public string StatusTooltip {
        get => statusTooltip;
        private set => SetField(ref statusTooltip, value);
    }

    private void Refresh() {
        detectedKeyboard ??= KeyboardDetector.DetectConnectedKeyboard();

        KeyboardBatteryStatus? status = KeyboardBattery.ReadStatus(detectedKeyboard);
        if (status == null && detectedKeyboard != null) {
            detectedKeyboard = KeyboardDetector.DetectConnectedKeyboard();
            status = KeyboardBattery.ReadStatus(detectedKeyboard);
        }

        bool fnLock = DriverStatus.ReadFnLockEnabled();

        DeviceName = detectedKeyboard == null ? Strings.DeviceNotDetected : KeyboardModel.From(detectedKeyboard.Product).DeviceDescription;

        ConnectionGlyph = detectedKeyboard?.Connection switch {
            KeyboardConnection.Usb => Glyphs.Usb,
            KeyboardConnection.Bluetooth => Glyphs.Bluetooth,
            _ => "",
        };

        BatteryGlyph = status == null
            ? Glyphs.BatteryLevel(0)
            : status.IsCharging ? Glyphs.BatteryCharging : Glyphs.BatteryLevel(status.Percent);
        BatteryColor = status switch {
            null => UnknownBatteryBrush,
            { Percent: <= 20 } => LowBatteryBrush,
            { Percent: <= 50 } => MediumBatteryBrush,
            _ => GoodBatteryBrush,
        };

        FnLockEnabled = fnLock;
        FnLockBackground = fnLock ? FnLockOnBackground : FnLockOffBackground;
        FnLockForeground = fnLock ? FnLockOnForeground : FnLockOffForeground;

        string batteryValue = status == null
            ? "—"
            : status.IsCharging ? $"{status.Percent}% ({Strings.BatteryCharging})" : $"{status.Percent}%";
        string connectionValue = detectedKeyboard?.Connection switch {
            KeyboardConnection.Usb => Strings.ConnectionUsb,
            KeyboardConnection.Bluetooth => Strings.ConnectionBluetooth,
            _ => Strings.DeviceNotDetected,
        };
        string fnLockValue = fnLock ? Strings.FnLockEnabled : Strings.FnLockDisabled;

        StatusTooltip =
            $"{Strings.StatusBatteryLabel}: {batteryValue}\n" +
            $"{Strings.StatusConnectionLabel}: {connectionValue}\n" +
            $"{Strings.StatusFnLockLabel}: {fnLockValue}";
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
        if (EqualityComparer<T>.Default.Equals(field, value)) {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
