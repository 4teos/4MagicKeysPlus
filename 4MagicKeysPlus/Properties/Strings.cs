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

    public static string KeyActionSetMapping {
        get {
            return Get(nameof(KeyActionSetMapping));
        }
    }

    public static string KeyActionRemoveMapping {
        get {
            return Get(nameof(KeyActionRemoveMapping));
        }
    }

    public static string KeyActionCancel {
        get {
            return Get(nameof(KeyActionCancel));
        }
    }

    public static string KeyOptionWithFn {
        get {
            return Get(nameof(KeyOptionWithFn));
        }
    }

    public static string KeyOptionFollowFnLock {
        get {
            return Get(nameof(KeyOptionFollowFnLock));
        }
    }

    public static string KeyOptionFollowFnLockHint {
        get {
            return Get(nameof(KeyOptionFollowFnLockHint));
        }
    }

    public static string KeyNotRemapped {
        get {
            return Get(nameof(KeyNotRemapped));
        }
    }

    public static string KeyCannotRemap {
        get {
            return Get(nameof(KeyCannotRemap));
        }
    }

    public static string KeyPickPrompt {
        get {
            return Get(nameof(KeyPickPrompt));
        }
    }

    public static string KeyRuleWithFn {
        get {
            return Get(nameof(KeyRuleWithFn));
        }
    }

    public static string KeyRuleFollowsFnLock {
        get {
            return Get(nameof(KeyRuleFollowsFnLock));
        }
    }

    public static string ApplyApplied {
        get {
            return Get(nameof(ApplyApplied));
        }
    }

    public static string ApplyIgnored {
        get {
            return Get(nameof(ApplyIgnored));
        }
    }

    public static string ApplySavedNotApplied {
        get {
            return Get(nameof(ApplySavedNotApplied));
        }
    }

    public static string ApplyDriverError {
        get {
            return Get(nameof(ApplyDriverError));
        }
    }

    public static string ApplySaveFailed {
        get {
            return Get(nameof(ApplySaveFailed));
        }
    }

    public static string HotkeyAgentNotRunning {
        get {
            return Get(nameof(HotkeyAgentNotRunning));
        }
    }

    public static string HotkeyNotRegistered {
        get {
            return Get(nameof(HotkeyNotRegistered));
        }
    }

    private static string Get(string key) {
        return ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }
}
