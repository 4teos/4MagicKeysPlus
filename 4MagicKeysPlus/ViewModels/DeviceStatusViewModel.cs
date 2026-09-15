using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Threading;
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

    private readonly DispatcherTimer timer;
    private DetectedKeyboard? detectedKeyboard;
    private KeyboardBatteryStatus? batteryStatus;
    private bool fnLockEnabled;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DeviceStatusViewModel() {
        Refresh();

        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
    }

    public string DeviceName => detectedKeyboard?.Model.DeviceDescription ?? Strings.DeviceNotDetected;

    public string ConnectionGlyph => detectedKeyboard?.Connection switch {
        KeyboardConnection.Usb => Glyphs.Usb,
        KeyboardConnection.Bluetooth => Glyphs.Bluetooth,
        _ => "",
    };

    public string ConnectionTooltip => detectedKeyboard?.Connection switch {
        KeyboardConnection.Usb => Strings.ConnectionUsb,
        KeyboardConnection.Bluetooth => Strings.ConnectionBluetooth,
        _ => Strings.DeviceNotDetected,
    };

    public string BatteryGlyph => batteryStatus == null
        ? Glyphs.BatteryLevel(0)
        : batteryStatus.IsCharging ? Glyphs.BatteryCharging : Glyphs.BatteryLevel(batteryStatus.Percent);

    public Brush BatteryColor => batteryStatus switch {
        null => UnknownBatteryBrush,
        { Percent: <= 20 } => LowBatteryBrush,
        { Percent: <= 50 } => MediumBatteryBrush,
        _ => GoodBatteryBrush,
    };

    public string BatteryTooltip => batteryStatus == null
        ? "—"
        : batteryStatus.IsCharging ? $"{batteryStatus.Percent}% ({Strings.BatteryCharging})" : $"{batteryStatus.Percent}%";

    public bool FnLockEnabled => fnLockEnabled;
    public Brush FnLockBackground => fnLockEnabled ? FnLockOnBackground : FnLockOffBackground;
    public Brush FnLockForeground => fnLockEnabled ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51));
    public string FnLockTooltip => fnLockEnabled ? Strings.FnLockEnabled : Strings.FnLockDisabled;

    private void Refresh() {
        detectedKeyboard ??= KeyboardDetector.DetectConnectedKeyboard();

        KeyboardBatteryStatus? status = KeyboardBattery.ReadStatus(detectedKeyboard);
        if (status == null && detectedKeyboard != null) {
            // The known device path died - unplugged, or switched transport (e.g. USB -> Bluetooth).
            // Re-detect immediately instead of waiting for the device to reappear on its old path.
            detectedKeyboard = KeyboardDetector.DetectConnectedKeyboard();
            status = KeyboardBattery.ReadStatus(detectedKeyboard);
        }

        batteryStatus = status;
        fnLockEnabled = DriverStatus.ReadFnLockEnabled();

        OnPropertyChanged(nameof(DeviceName));
        OnPropertyChanged(nameof(ConnectionGlyph));
        OnPropertyChanged(nameof(ConnectionTooltip));
        OnPropertyChanged(nameof(BatteryGlyph));
        OnPropertyChanged(nameof(BatteryColor));
        OnPropertyChanged(nameof(BatteryTooltip));
        OnPropertyChanged(nameof(FnLockEnabled));
        OnPropertyChanged(nameof(FnLockBackground));
        OnPropertyChanged(nameof(FnLockForeground));
        OnPropertyChanged(nameof(FnLockTooltip));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
