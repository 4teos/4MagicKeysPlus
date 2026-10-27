using System.Globalization;
using System.Security;
using Microsoft.Win32;

namespace _4MagicKeysPlus.Core;

// Where the configuration app keeps its settings (they are per user). The agent follows the language and whether to
// open the app for a keyboard that has no profile.
public static class AppSettingsRegistry {
    public const string KeyPath = @"Software\4MagicKeysPlus";
    public const string LanguageValue = "Language";
    public const string OpenAppForNewKeyboardValue = "OpenAppForNewKeyboard";

    public static CultureInfo? ReadLanguage() {
        return ReadLanguage(Registry.CurrentUser, KeyPath);
    }

    // The language chosen in the configuration app, or null when it follows Windows: there is no
    // value, it is no culture, or it cannot be read (a notice must not fail because of that).
    public static CultureInfo? ReadLanguage(RegistryKey root, string keyPath) {
        try {
            using RegistryKey? key = root.OpenSubKey(keyPath);
            return ParseLanguage(key?.GetValue(LanguageValue) as string);
        } catch (Exception exception) when (exception is IOException or SecurityException or UnauthorizedAccessException) {
            return null;
        }
    }

    public static bool ReadOpenAppForNewKeyboard() {
        return ReadOpenAppForNewKeyboard(Registry.CurrentUser, KeyPath);
    }

    // On unless the value is a DWORD of 0: that is the default, and what an unreadable setting falls back to.
    public static bool ReadOpenAppForNewKeyboard(RegistryKey root, string keyPath) {
        try {
            using RegistryKey? key = root.OpenSubKey(keyPath);
            return key?.GetValue(OpenAppForNewKeyboardValue) is not int value || value != 0;
        } catch (Exception exception) when (exception is IOException or SecurityException or UnauthorizedAccessException) {
            return true;
        }
    }

    // A culture name as the language alone (see GeneralLanguage); null for a blank or unknown name.
    public static CultureInfo? ParseLanguage(string? name) {
        if (string.IsNullOrWhiteSpace(name)) {
            return null;
        }

        try {
            return GeneralLanguage(CultureInfo.GetCultureInfo(name.Trim()));
        } catch (CultureNotFoundException) {
            return null;
        }
    }

    // The apps have the languages themselves ("en", "de"), no regional variants of them: en-US is en.
    public static CultureInfo GeneralLanguage(CultureInfo culture) {
        while (culture.Parent.Name.Length > 0) {
            culture = culture.Parent;
        }
        return culture;
    }
}
