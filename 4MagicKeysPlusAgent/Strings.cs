using System.Globalization;
using System.Resources;
using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusAgent;

internal static class Strings {
    private static readonly ResourceManager ResourceManager =
        new("_4MagicKeysPlusAgent.Strings", typeof(Strings).Assembly);

    // The language of the configuration app, read at each use: the agent keeps running when the
    // language is changed, and the next notice has to follow it. Without a choice, Windows' own.
    internal static Func<CultureInfo?> LanguageSource = AppSettingsRegistry.ReadLanguage;

    public static string LowBatteryMessage {
        get {
            return Get(nameof(LowBatteryMessage));
        }
    }

    public static string HotkeyErrorsTitle {
        get {
            return Get(nameof(HotkeyErrorsTitle));
        }
    }

    public static string HotkeyAlreadyRegistered {
        get {
            return Get(nameof(HotkeyAlreadyRegistered));
        }
    }

    public static string NoticeDismissHint {
        get {
            return Get(nameof(NoticeDismissHint));
        }
    }

    public static string KeyboardModelA1644 {
        get {
            return Get(nameof(KeyboardModelA1644));
        }
    }

    public static string KeyboardModelA1843 {
        get {
            return Get(nameof(KeyboardModelA1843));
        }
    }

    public static string ModelName(KeyboardProduct product) {
        return product switch {
            KeyboardProduct.A1644 => KeyboardModelA1644,
            KeyboardProduct.A1843 => KeyboardModelA1843,
            _ => product.ToString(),
        };
    }

    private static string Get(string key) {
        return ResourceManager.GetString(key, LanguageSource() ?? CultureInfo.CurrentUICulture) ?? key;
    }
}
