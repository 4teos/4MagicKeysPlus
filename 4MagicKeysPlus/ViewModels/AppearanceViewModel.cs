using System.IO;
using System.Security;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlus.ViewModels;

// Theme and language. The theme is applied at once; the language is read once at start-up (the
// strings of the screens are not rebuilt), so a change of it waits for a restart.
public sealed class AppearanceViewModel : ObservableObject {
    private readonly IAppSettingsStore store;
    private readonly IThemeApplier themes;
    private readonly Func<string?> restart;
    private readonly string? launchLanguage;
    private AppSettings settings;
    private string? problem;

    // restart closes the app on success and answers what went wrong, or null.
    internal AppearanceViewModel(IAppSettingsStore store, IThemeApplier themes, IReadOnlyList<LanguageOption> available, AppSettings launched, Func<string?> restart) {
        this.store = store;
        this.themes = themes;
        this.restart = restart;
        Languages = [new LanguageOption(null, Strings.OptionsLanguageSystem), .. available];
        settings = launched;
        launchLanguage = Find(launched.Language).CultureName;
    }

    public IReadOnlyList<LanguageOption> Languages {
        get;
    }

    public bool ThemeIsSystem {
        get => settings.Theme == ThemeChoice.System;
        set => SelectTheme(value, ThemeChoice.System);
    }

    public bool ThemeIsDark {
        get => settings.Theme == ThemeChoice.Dark;
        set => SelectTheme(value, ThemeChoice.Dark);
    }

    public bool ThemeIsLight {
        get => settings.Theme == ThemeChoice.Light;
        set => SelectTheme(value, ThemeChoice.Light);
    }

    public LanguageOption SelectedLanguage {
        get => Find(settings.Language);
        set {
            if (value is not null && value.CultureName != SelectedLanguage.CultureName) {
                AppSettings wanted = settings with {
                    Language = value.CultureName
                };
                Save(wanted);
            }
        }
    }

    public bool LanguageRestartRequired => SelectedLanguage.CultureName != launchLanguage;

    public string? Problem => problem;

    public bool HasProblem => problem is not null;

    public void Restart() {
        problem = restart();
        Notify(nameof(Problem), nameof(HasProblem));
    }

    private LanguageOption Find(string? cultureName) {
        return Languages.FirstOrDefault(language => language.CultureName is not null && string.Equals(language.CultureName, cultureName, StringComparison.OrdinalIgnoreCase)) ?? Languages[0];
    }

    private void SelectTheme(bool selected, ThemeChoice choice) {
        if (!selected || settings.Theme == choice) {
            return;
        }

        AppSettings wanted = settings with {
            Theme = choice
        };
        if (Save(wanted)) {
            themes.Apply(choice);
        }
    }

    // The screen shows what is saved, so it is told again when saving failed too.
    private bool Save(AppSettings wanted) {
        bool saved = false;
        try {
            store.Write(wanted);
            settings = wanted;
            problem = null;
            saved = true;
        } catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or SecurityException) {
            problem = string.Format(Strings.OptionsSaveFailed, exception.Message);
        }

        Notify(nameof(ThemeIsSystem), nameof(ThemeIsDark), nameof(ThemeIsLight), nameof(SelectedLanguage), nameof(LanguageRestartRequired), nameof(Problem), nameof(HasProblem));
        return saved;
    }
}
