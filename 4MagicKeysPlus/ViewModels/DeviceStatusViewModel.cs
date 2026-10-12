using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.Views;

namespace _4MagicKeysPlus.ViewModels;

public enum BatteryState {
    Unknown,
    Low,
    Medium,
    Good,
}

// Lives for the whole app session (owned by MainWindow), not per-navigation-visit like the old
// per-screen view models - the header needs to show live status regardless of which page is open.
public sealed class DeviceStatusViewModel : INotifyPropertyChanged {
    private readonly DispatcherTimer timer;
    private DetectedKeyboard? detectedKeyboard;
    private string deviceName = Strings.DeviceNotDetected;
    private string connectionGlyph = "";
    private string batteryGlyph = Glyphs.BatteryLevel(0);
    private BatteryState batteryState = BatteryState.Unknown;
    private bool fnLockEnabled;
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

    public BatteryState BatteryState {
        get => batteryState;
        private set => SetField(ref batteryState, value);
    }

    public bool FnLockEnabled {
        get => fnLockEnabled;
        private set => SetField(ref fnLockEnabled, value);
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
        BatteryState = status switch {
            null => BatteryState.Unknown,
            { Percent: <= 20 } => BatteryState.Low,
            { Percent: <= 50 } => BatteryState.Medium,
            _ => BatteryState.Good,
        };

        FnLockEnabled = fnLock;

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
