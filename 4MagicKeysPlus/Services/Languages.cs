using System.Globalization;
using System.IO;
using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlus.Services;

public sealed record LanguageOption(string? CultureName, string DisplayName);

internal static class Languages {
    // The neutral resources are English; every other language is a satellite assembly in a folder
    // named after it, so a new Strings.<language>.resx shows up here without any code. Only the
    // languages themselves count ("ru", "de"): a folder of a regional variant ("ru-RU") is not
    // offered. English comes first, the rest in the order of their names.
    private const string NeutralCulture = "en";

    public static IReadOnlyList<LanguageOption> Available(string directory) {
        string satellite = $"{typeof(Languages).Assembly.GetName().Name}.resources.dll";
        List<CultureInfo> cultures = [CultureInfo.GetCultureInfo(NeutralCulture)];
        foreach (string folder in Directory.EnumerateDirectories(directory).OrderBy(folder => folder, StringComparer.OrdinalIgnoreCase)) {
            if (!File.Exists(Path.Combine(folder, satellite))) {
                continue;
            }
            try {
                CultureInfo culture = CultureInfo.GetCultureInfo(Path.GetFileName(folder));
                if (AppSettingsRegistry.GeneralLanguage(culture).Name == culture.Name && !cultures.Contains(culture)) {
                    cultures.Add(culture);
                }
            } catch (CultureNotFoundException) {
                // Not a culture folder.
            }
        }
        return [.. cultures.Select(culture => new LanguageOption(culture.Name, DisplayName(culture)))];
    }

    // A language that is not installed any more (or a name that never was one) leaves Windows' own.
    public static void Apply(string? cultureName) {
        LanguageOption? option = Available(AppContext.BaseDirectory)
            .FirstOrDefault(language => string.Equals(language.CultureName, cultureName, StringComparison.OrdinalIgnoreCase));
        if (option?.CultureName is null) {
            return;
        }

        CultureInfo culture = CultureInfo.GetCultureInfo(option.CultureName);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    // "Русский": the name of the language in itself, which the culture gives in lower case.
    private static string DisplayName(CultureInfo culture) {
        string name = culture.NativeName;
        return char.ToUpper(name[0], culture) + name[1..];
    }
}
