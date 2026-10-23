using Microsoft.Win32;
using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlus.Services;

public enum ThemeChoice {
    System,
    Dark,
    Light,
}

// Language is the language name ("en", "de"), or null to follow Windows.
internal sealed record AppSettings(ThemeChoice Theme = ThemeChoice.System, string? Language = null);

internal interface IAppSettingsStore {
    AppSettings Read();

    void Write(AppSettings settings);
}

// The settings of the app itself (not of the driver) are per user, in a key the agent reads the language from.
internal sealed class RegistryAppSettingsStore : IAppSettingsStore {
    internal const string KeyPath = AppSettingsRegistry.KeyPath;
    private const string ThemeValue = "Theme";
    private const string LanguageValue = AppSettingsRegistry.LanguageValue;

    private readonly RegistryKey root;
    private readonly string keyPath;

    public RegistryAppSettingsStore() : this(Registry.CurrentUser, KeyPath) {
    }

    // The root and path are parameters so that tests can use a scratch key of their own.
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
        return new AppSettings(theme, language);
    }

    public void Write(AppSettings settings) {
        using RegistryKey key = root.CreateSubKey(keyPath, writable: true);
        key.SetValue(ThemeValue, settings.Theme.ToString(), RegistryValueKind.String);
        if (settings.Language is null) {
            key.DeleteValue(LanguageValue, throwOnMissingValue: false);
        } else {
            key.SetValue(LanguageValue, settings.Language, RegistryValueKind.String);
        }
    }
}
