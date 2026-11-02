using System.Globalization;
using System.Resources;
using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusAgent;

internal static class Strings {
    private static readonly ResourceManager ResourceManager =
        new("_4MagicKeysPlusAgent.Strings", typeof(Strings).Assembly);

    // Read at each use: the agent keeps running when the language is changed, and the next notice has to follow it.
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

    public static string KeyboardModelA2449 {
        get {
            return Get(nameof(KeyboardModelA2449));
        }
    }

    public static string KeyboardModelA2450 {
        get {
            return Get(nameof(KeyboardModelA2450));
        }
    }

    public static string KeyboardModelA2520 {
        get {
            return Get(nameof(KeyboardModelA2520));
        }
    }

    public static string KeyboardModelA3118 {
        get {
            return Get(nameof(KeyboardModelA3118));
        }
    }

    public static string KeyboardModelA3119 {
        get {
            return Get(nameof(KeyboardModelA3119));
        }
    }

    public static string KeyboardModelA3203 {
        get {
            return Get(nameof(KeyboardModelA3203));
        }
    }

    public static string ModelName(KeyboardProduct product) {
        return product switch {
            KeyboardProduct.A1644 => KeyboardModelA1644,
            KeyboardProduct.A1843 => KeyboardModelA1843,
            KeyboardProduct.A2449 => KeyboardModelA2449,
            KeyboardProduct.A2450 => KeyboardModelA2450,
            KeyboardProduct.A2520 => KeyboardModelA2520,
            KeyboardProduct.A3118 => KeyboardModelA3118,
            KeyboardProduct.A3119 => KeyboardModelA3119,
            KeyboardProduct.A3203 => KeyboardModelA3203,
            _ => product.ToString(),
        };
    }

    private static string Get(string key) {
        return ResourceManager.GetString(key, LanguageSource() ?? CultureInfo.CurrentUICulture) ?? key;
    }
}
