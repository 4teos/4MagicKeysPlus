using System.Globalization;
using System.Resources;

namespace _4MagicKeysPlus.Properties;

public static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new("_4MagicKeysPlus.Properties.Strings", typeof(Strings).Assembly);

    public static string NavKeys => Get(nameof(NavKeys));
    public static string NavQuickLaunch => Get(nameof(NavQuickLaunch));
    public static string NavAdvanced => Get(nameof(NavAdvanced));
    public static string DeviceNotDetected => Get(nameof(DeviceNotDetected));
    public static string ConnectionUsb => Get(nameof(ConnectionUsb));
    public static string ConnectionBluetooth => Get(nameof(ConnectionBluetooth));
    public static string BatteryCharging => Get(nameof(BatteryCharging));
    public static string FnLockEnabled => Get(nameof(FnLockEnabled));
    public static string FnLockDisabled => Get(nameof(FnLockDisabled));
    public static string KeysModelLabel => Get(nameof(KeysModelLabel));
    public static string KeysModel_MagicKeyboard => Get(nameof(KeysModel_MagicKeyboard));
    public static string KeysModel_MagicKeyboardWithNumpad => Get(nameof(KeysModel_MagicKeyboardWithNumpad));

    private static string Get(string key) =>
        ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
