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

    private static string Get(string key) {
        return ResourceManager.GetString(key, LanguageSource() ?? CultureInfo.CurrentUICulture) ?? key;
    }
}
