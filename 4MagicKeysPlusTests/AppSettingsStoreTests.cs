using Microsoft.Win32;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

[Collection(TestCollections.Registry)]
public sealed class AppSettingsStoreTests : IDisposable {
    private readonly ScratchKey scratch = new();
    private readonly RegistryAppSettingsStore store;

    public AppSettingsStoreTests() {
        store = new RegistryAppSettingsStore(Registry.CurrentUser, scratch.Path("Settings"));
    }

    public void Dispose() {
        scratch.Dispose();
    }

    [Fact]
    public void WithoutAKey_TheDefaultsApply() {
        AppSettings settings = store.Read();

        Assert.Equal(ThemeChoice.System, settings.Theme);
        Assert.Null(settings.Language);
    }

    [Theory]
    [InlineData(ThemeChoice.System, null)]
    [InlineData(ThemeChoice.Dark, "ru")]
    [InlineData(ThemeChoice.Light, "en")]
    public void WhatIsWritten_IsRead(ThemeChoice theme, string? language) {
        store.Write(new AppSettings(theme, language));

        AppSettings settings = store.Read();

        Assert.Equal(theme, settings.Theme);
        Assert.Equal(language, settings.Language);
    }

    [Fact]
    public void WritingNoLanguage_RemovesTheValue() {
        store.Write(new AppSettings(ThemeChoice.Dark, "ru"));

        store.Write(new AppSettings(ThemeChoice.Dark, null));

        using RegistryKey key = scratch.Open("Settings")!;
        Assert.Null(key.GetValue("Language"));
    }

    [Theory]
    [InlineData("purple")]
    [InlineData("7")]
    [InlineData("")]
    public void ATheme_ThatIsNotOneOfTheThree_FallsBackToSystem(string stored) {
        using (RegistryKey key = scratch.Create("Settings")) {
            key.SetValue("Theme", stored);
        }

        Assert.Equal(ThemeChoice.System, store.Read().Theme);
    }

    [Theory]
    [InlineData("ru-RU", "ru")]
    [InlineData("en-US", "en")]
    [InlineData(" de-AT ", "de")]
    public void ARegionalLanguage_IsReadAsTheLanguageAlone(string stored, string expected) {
        using (RegistryKey key = scratch.Create("Settings")) {
            key.SetValue("Language", stored);
        }

        Assert.Equal(expected, store.Read().Language);
    }

    [Fact]
    public void ALanguage_ThatIsNoCulture_MeansSystem() {
        using (RegistryKey key = scratch.Create("Settings")) {
            key.SetValue("Language", "not a culture!");
        }

        Assert.Null(store.Read().Language);
    }

    [Fact]
    public void ABlankLanguage_MeansSystem() {
        using (RegistryKey key = scratch.Create("Settings")) {
            key.SetValue("Language", "  ");
        }

        Assert.Null(store.Read().Language);
    }
}
