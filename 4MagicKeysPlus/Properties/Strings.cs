using System.Globalization;
using System.Resources;

namespace _4MagicKeysPlus.Properties;

public static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new("_4MagicKeysPlus.Properties.Strings", typeof(Strings).Assembly);

    public static string NavKeys {
        get {
            return Get(nameof(NavKeys));
        }
    }

    public static string NavQuickLaunch {
        get {
            return Get(nameof(NavQuickLaunch));
        }
    }

    public static string NavAdvanced {
        get {
            return Get(nameof(NavAdvanced));
        }
    }

    public static string DeviceNotDetected {
        get {
            return Get(nameof(DeviceNotDetected));
        }
    }

    public static string ConnectionUsb {
        get {
            return Get(nameof(ConnectionUsb));
        }
    }

    public static string ConnectionBluetooth {
        get {
            return Get(nameof(ConnectionBluetooth));
        }
    }

    public static string BatteryCharging {
        get {
            return Get(nameof(BatteryCharging));
        }
    }

    public static string FnLockEnabled {
        get {
            return Get(nameof(FnLockEnabled));
        }
    }

    public static string FnLockDisabled {
        get {
            return Get(nameof(FnLockDisabled));
        }
    }

    public static string StatusBatteryLabel {
        get {
            return Get(nameof(StatusBatteryLabel));
        }
    }

    public static string StatusConnectionLabel {
        get {
            return Get(nameof(StatusConnectionLabel));
        }
    }

    public static string StatusFnLockLabel {
        get {
            return Get(nameof(StatusFnLockLabel));
        }
    }

    public static string QuickLaunchPlaceholderNotice {
        get {
            return Get(nameof(QuickLaunchPlaceholderNotice));
        }
    }

    public static string KeysModelLabel {
        get {
            return Get(nameof(KeysModelLabel));
        }
    }

    public static string KeysModel_MagicKeyboard {
        get {
            return Get(nameof(KeysModel_MagicKeyboard));
        }
    }

    public static string KeysModel_MagicKeyboardWithNumpad {
        get {
            return Get(nameof(KeysModel_MagicKeyboardWithNumpad));
        }
    }

    private static string Get(string key) {
        return ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
