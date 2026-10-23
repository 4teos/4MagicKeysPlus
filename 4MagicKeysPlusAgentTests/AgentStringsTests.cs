using System.Globalization;
using System.Reflection;
using _4MagicKeysPlusAgent;

namespace _4MagicKeysPlusAgentTests;

internal static class StringsCollection {
    // The tests that change the language of Strings, which is shared by the whole process.
    public const string Name = "AgentStrings";
}

internal sealed class LanguageScope : IDisposable {
    private readonly Func<CultureInfo?> original = Strings.LanguageSource;

    public LanguageScope(string? cultureName) {
        Strings.LanguageSource = () => cultureName is null ? null : CultureInfo.GetCultureInfo(cultureName);
    }

    public void Dispose() {
        Strings.LanguageSource = original;
    }
}

[Collection(StringsCollection.Name)]
public class AgentStringsTests {
    private static IEnumerable<PropertyInfo> AllStrings => typeof(Strings)
        .GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(property => property.PropertyType == typeof(string));

    private static string Read(PropertyInfo property) {
        return (string)property.GetValue(null)!;
    }

    [Fact]
    public void TheChosenLanguage_IsUsed() {
        using (new LanguageScope("ru")) {
            Assert.Equal("Батарея клавиатуры: {0}%", Strings.LowBatteryMessage);
        }
        using (new LanguageScope("en")) {
            Assert.Equal("Keyboard battery: {0}%", Strings.LowBatteryMessage);
        }
    }

    [Fact]
    public void ALanguageChangedWhileRunning_AppliesToTheNextRead() {
        string? chosen = "en";
        Func<CultureInfo?> original = Strings.LanguageSource;
        Strings.LanguageSource = () => chosen is null ? null : CultureInfo.GetCultureInfo(chosen);
        try {
            string english = Strings.NoticeDismissHint;

            chosen = "ru";

            Assert.NotEqual(english, Strings.NoticeDismissHint);
        } finally {
            Strings.LanguageSource = original;
        }
    }

    [Fact]
    public void WithoutAChoice_TheLanguageOfWindowsIsUsed() {
        CultureInfo original = CultureInfo.CurrentUICulture;
        try {
            using LanguageScope scope = new(null);

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
            Assert.Equal("Нажмите, чтобы закрыть", Strings.NoticeDismissHint);

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("Click to close", Strings.NoticeDismissHint);
        } finally {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void ALanguageWithoutTranslation_FallsBackToEnglish() {
        using (new LanguageScope("de")) {
            Assert.Equal("Click to close", Strings.NoticeDismissHint);
        }
    }

    [Fact]
    public void EveryString_IsTranslatedToRussian() {
        foreach (PropertyInfo property in AllStrings) {
            string english;
            string russian;
            using (new LanguageScope("en")) {
                english = Read(property);
            }
            using (new LanguageScope("ru")) {
                russian = Read(property);
            }

            Assert.NotEqual(property.Name, english);
            Assert.NotEqual(english, russian);
        }
    }
}
