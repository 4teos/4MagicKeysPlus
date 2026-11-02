using System.Globalization;
using System.Security;
using Microsoft.Win32;

namespace _4MagicKeysPlus.Core;

public static class AppSettingsRegistry {
    public const string KeyPath = @"Software\4MagicKeysPlus";
    public const string LanguageValue = "Language";
    public const string OpenAppForNewKeyboardValue = "OpenAppForNewKeyboard";

    public static CultureInfo? ReadLanguage() {
        return ReadLanguage(Registry.CurrentUser, KeyPath);
    }

    // Null when the app follows Windows, also when the value cannot be read: a notice must not fail because of that.
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

    public static bool ReadOpenAppForNewKeyboard(RegistryKey root, string keyPath) {
        try {
            using RegistryKey? key = root.OpenSubKey(keyPath);
            return key?.GetValue(OpenAppForNewKeyboardValue) is not int value || value != 0;
        } catch (Exception exception) when (exception is IOException or SecurityException or UnauthorizedAccessException) {
            return true;
        }
    }

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

    public static CultureInfo GeneralLanguage(CultureInfo culture) {
        while (culture.Parent.Name.Length > 0) {
            culture = culture.Parent;
        }
        return culture;
    }
}
