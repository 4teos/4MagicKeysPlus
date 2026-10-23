using System.Globalization;
using Microsoft.Win32;
using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusAgentTests;

public sealed class AppSettingsRegistryTests : IDisposable {
    private const string ScratchRoot = @"Software\4MagicKeysPlusAgentTests";
    private readonly string folder = $@"{ScratchRoot}\{Guid.NewGuid():N}";

    public void Dispose() {
        Registry.CurrentUser.DeleteSubKeyTree(folder, throwOnMissingSubKey: false);
        using RegistryKey? root = Registry.CurrentUser.OpenSubKey(ScratchRoot);
        if (root?.SubKeyCount == 0) {
            Registry.CurrentUser.DeleteSubKey(ScratchRoot, throwOnMissingSubKey: false);
        }
    }

    private void Store(object value) {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(folder, writable: true);
        key.SetValue(AppSettingsRegistry.LanguageValue, value);
    }

    [Fact]
    public void WithoutAKey_TheLanguageIsThatOfWindows() {
        Assert.Null(AppSettingsRegistry.ReadLanguage(Registry.CurrentUser, folder));
    }

    [Fact]
    public void WithoutTheValue_TheLanguageIsThatOfWindows() {
        using (Registry.CurrentUser.CreateSubKey(folder)) {
        }

        Assert.Null(AppSettingsRegistry.ReadLanguage(Registry.CurrentUser, folder));
    }

    [Theory]
    [InlineData("ru", "ru")]
    [InlineData(" en ", "en")]
    [InlineData("ru-RU", "ru")]
    [InlineData("en-US", "en")]
    [InlineData("de-AT", "de")]
    public void TheChosenLanguage_IsRead_AsTheLanguageAlone(string stored, string expected) {
        Store(stored);

        Assert.Equal(expected, AppSettingsRegistry.ReadLanguage(Registry.CurrentUser, folder)?.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a culture!")]
    public void ABlankValue_OrOneThatIsNoCulture_MeansWindows(string stored) {
        Store(stored);

        Assert.Null(AppSettingsRegistry.ReadLanguage(Registry.CurrentUser, folder));
    }

    [Fact]
    public void AValueOfAnotherType_MeansWindows() {
        Store(5);

        Assert.Null(AppSettingsRegistry.ReadLanguage(Registry.CurrentUser, folder));
    }

    [Theory]
    [InlineData("ru-RU", "ru")]
    [InlineData("pt-BR", "pt")]
    [InlineData("en", "en")]
    [InlineData("zh-Hans-CN", "zh")]
    public void ARegionalVariant_IsItsLanguage(string name, string expected) {
        Assert.Equal(expected, AppSettingsRegistry.GeneralLanguage(CultureInfo.GetCultureInfo(name)).Name);
    }
}
