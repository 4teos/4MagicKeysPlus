using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using _4MagicKeysPlus.Core;
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
// It shows the keyboard that is selected, and follows the selection.
public sealed class DeviceStatusViewModel : INotifyPropertyChanged {
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

    private readonly KeyboardSession session;
    private readonly Func<Guid, bool> readFnLock;
    private readonly Func<DetectedKeyboard?, KeyboardBatteryStatus?> readBattery;
    private DispatcherTimer? timer;
    private bool isKeyboardDetected;
    private string deviceName = Strings.DeviceNotDetected;
    private string connectionText = Strings.DeviceNotDetected;
    private string connectionGlyph = "";
    private string batteryGlyph = Glyphs.BatteryLevel(0);
    private BatteryState batteryState = BatteryState.Unknown;
    private bool fnLockEnabled;
    private string statusTooltip = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    // readFnLock tells the Fn Lock of a profile, readBattery the battery of the keyboard on the way it is connected.
    internal DeviceStatusViewModel(KeyboardSession session, Func<Guid, bool> readFnLock, Func<DetectedKeyboard?, KeyboardBatteryStatus?> readBattery) {
        this.session = session;
        this.readFnLock = readFnLock;
        this.readBattery = readBattery;
        session.Changed += Refresh;
        Refresh();
    }

    public string DeviceName {
        get => deviceName;
        private set => SetField(ref deviceName, value);
    }

    public bool IsKeyboardDetected => isKeyboardDetected;

    public string ConnectionText {
        get => connectionText;
        private set => SetField(ref connectionText, value);
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

    // Looks at the keyboards every few seconds: the session tells of what it found, and the battery goes with it.
    public void Start() {
        timer = new DispatcherTimer { Interval = RefreshInterval };
        timer.Tick += async (_, _) => {
            try {
                await session.RefreshAsync();
            } catch (Exception) {
                // Looking at the keyboards is no reason to bring the app down: the next tick looks again.
            }
        };
        timer.Start();
    }

    public void Refresh() {
        KeyboardEntry? selected = session.Selected;
        KeyboardEndpoint? live = selected?.Live;
        DetectedKeyboard? detectedKeyboard = live is { DevicePath: { } devicePath }
            ? new DetectedKeyboard(live.Product, live.Identity.Connection, devicePath)
            : null;
        KeyboardBatteryStatus? status = readBattery(detectedKeyboard);
        bool fnLock = selected is not null && readFnLock(selected.Profile.Id);

        SetField(ref isKeyboardDetected, selected is not null, nameof(IsKeyboardDetected));
        DeviceName = selected?.DisplayName ?? Strings.DeviceNotDetected;

        ConnectionGlyph = live?.Identity.Connection switch {
            KeyboardConnection.Usb => Glyphs.Usb,
            KeyboardConnection.Bluetooth => Glyphs.Bluetooth,
            _ => "",
        };

        BatteryGlyph = Glyphs.BatteryLevel(status?.Percent ?? 0, status?.IsCharging ?? false);
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
        string connectionValue = live?.Identity.Connection switch {
            KeyboardConnection.Usb => Strings.ConnectionUsb,
            KeyboardConnection.Bluetooth => Strings.ConnectionBluetooth,
            _ => Strings.DeviceNotDetected,
        };
        ConnectionText = connectionValue;
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
