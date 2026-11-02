using Microsoft.Win32;
using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlus.Services;

public enum ThemeChoice {
    System,
    Dark,
    Light,
}

// Language is a language name ("en", "de"), or null to follow Windows. The agent reads OpenAppForNewKeyboard.
internal sealed record AppSettings(ThemeChoice Theme = ThemeChoice.System, string? Language = null, bool OpenAppForNewKeyboard = true);

internal interface IAppSettingsStore {
    AppSettings Read();

    void Write(AppSettings settings);
}

internal sealed class RegistryAppSettingsStore : IAppSettingsStore {
    internal const string KeyPath = AppSettingsRegistry.KeyPath;
    private const string ThemeValue = "Theme";
    private const string LanguageValue = AppSettingsRegistry.LanguageValue;

    private readonly RegistryKey root;
    private readonly string keyPath;

    public RegistryAppSettingsStore() : this(Registry.CurrentUser, KeyPath) {
    }

    public RegistryAppSettingsStore(RegistryKey root, string keyPath) {
        this.root = root;
        this.keyPath = keyPath;
    }

    public AppSettings Read() {
        using RegistryKey? key = root.OpenSubKey(keyPath);
        ThemeChoice theme = Enum.TryParse(key?.GetValue(ThemeValue) as string, ignoreCase: true, out ThemeChoice parsed) && Enum.IsDefined(parsed)
            ? parsed
            : ThemeChoice.System;
        string? language = AppSettingsRegistry.ParseLanguage(key?.GetValue(LanguageValue) as string)?.Name;
        return new AppSettings(theme, language, AppSettingsRegistry.ReadOpenAppForNewKeyboard(root, keyPath));
    }

    public void Write(AppSettings settings) {
        using RegistryKey key = root.CreateSubKey(keyPath, writable: true);
        key.SetValue(ThemeValue, settings.Theme.ToString(), RegistryValueKind.String);
        key.SetValue(AppSettingsRegistry.OpenAppForNewKeyboardValue, settings.OpenAppForNewKeyboard ? 1 : 0, RegistryValueKind.DWord);
        if (settings.Language is null) {
            key.DeleteValue(LanguageValue, throwOnMissingValue: false);
        } else {
            key.SetValue(LanguageValue, settings.Language, RegistryValueKind.String);
        }
    }
}
