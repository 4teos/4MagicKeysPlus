using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlus.ViewModels;

// What the app sees of the driver and the keyboard.
public sealed class SystemStatusViewModel : ObservableObject {
    private readonly IDriverProbe driver;
    private readonly Func<string?> keyboard;
    private string driverText = "";
    private StatusLevel driverLevel;
    private string keyboardText = "";
    private StatusLevel keyboardLevel;

    // keyboard describes the connected keyboard, or answers null when there is none.
    internal SystemStatusViewModel(IDriverProbe driver, Func<string?> keyboard) {
        this.driver = driver;
        this.keyboard = keyboard;
        Refresh();
    }

    public string DriverText {
        get => driverText;
        private set => SetField(ref driverText, value);
    }

    public StatusLevel DriverLevel {
        get => driverLevel;
        private set => SetField(ref driverLevel, value);
    }

    public string KeyboardText {
        get => keyboardText;
        private set => SetField(ref keyboardText, value);
    }

    public StatusLevel KeyboardLevel {
        get => keyboardLevel;
        private set => SetField(ref keyboardLevel, value);
    }

    public void Refresh() {
        (DriverText, DriverLevel) = driver.Probe() switch {
            DriverState.Active => (Strings.OptionsDriverActive, StatusLevel.Good),
            DriverState.Idle => (Strings.OptionsDriverIdle, StatusLevel.Neutral),
            _ => (Strings.OptionsDriverMissing, StatusLevel.Bad),
        };

        string? connected = keyboard();
        KeyboardText = connected ?? Strings.DeviceNotDetected;
        KeyboardLevel = connected is null ? StatusLevel.Neutral : StatusLevel.Good;
    }
}
