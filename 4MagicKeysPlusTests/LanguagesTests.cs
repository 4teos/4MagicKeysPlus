using System.Globalization;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

public sealed class LanguagesTests : IDisposable {
    private const string Satellite = "4MagicKeysPlus.resources.dll";
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"4MagicKeysPlusTests-{Guid.NewGuid():N}");

    public LanguagesTests() {
        Directory.CreateDirectory(directory);
    }

    public void Dispose() {
        Directory.Delete(directory, recursive: true);
    }

    private void Folder(string name, string? file = Satellite) {
        string folder = Path.Combine(directory, name);
        Directory.CreateDirectory(folder);
        if (file is not null) {
            File.WriteAllBytes(Path.Combine(folder, file), []);
        }
    }

    [Fact]
    public void TheNeutralEnglishIsAlwaysThere() {
        LanguageOption english = Assert.Single(Languages.Available(directory));

        Assert.Equal(("en", "English"), (english.CultureName, english.DisplayName));
    }

    [Fact]
    public void EveryFolderOfALanguageWithTheSatellite_IsOffered_AfterEnglish_ByName() {
        Folder("ru");
        Folder("es");
        Folder("de");

        IReadOnlyList<LanguageOption> languages = Languages.Available(directory);

        Assert.Equal([("en", "English"), ("de", "Deutsch"), ("es", "Español"), ("ru", "Русский")], languages.Select(language => (language.CultureName, language.DisplayName)));
    }

    [Fact]
    public void TheFolderOfARegionalVariant_IsNotOffered() {
        Folder("ru-RU");
        Folder("pt-BR");
        Folder("pt-PT");
        Folder("ru");

        Assert.Equal(["en", "ru"], Languages.Available(directory).Select(language => language.CultureName));
    }

    [Fact]
    public void AFolder_WithoutTheSatellite_OrThatIsNoCulture_IsNotALanguage() {
        Folder("fr", file: null);
        Folder("es", file: "other.resources.dll");
        Folder("not a culture!");

        Assert.Equal(["en"], Languages.Available(directory).Select(language => language.CultureName));
    }

    [Fact]
    public void TheLanguagesOfTheApp_AreEnglishAndRussian() {
        IReadOnlyList<LanguageOption> languages = Languages.Available(AppContext.BaseDirectory);

        Assert.Equal(["en", "ru"], languages.Select(language => language.CultureName));
    }

    // The resources are the languages themselves, and a Windows in a regional variant finds its language.
    [Theory]
    [InlineData("ru", "Настройки")]
    [InlineData("ru-RU", "Настройки")]
    [InlineData("ru-UA", "Настройки")]
    [InlineData("en-GB", "Settings")]
    [InlineData("de-DE", "Settings")]
    public void TheStrings_FollowTheLanguageOfAnyCultureOfIt(string culture, string expected) {
        CultureInfo original = CultureInfo.CurrentUICulture;
        try {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

            Assert.Equal(expected, Strings.NavSettings);
        } finally {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
