using System.Windows;
using Microsoft.Win32;

namespace _4MagicKeysPlus.Services;

internal interface IThemeApplier {
    void Apply(ThemeChoice choice);
}

// Swaps the theme dictionary of the application (the first of its merged dictionaries, see App.xaml).
// Every color is a DynamicResource, so the open windows follow at once.
internal sealed class ThemeManager : IThemeApplier, IDisposable {
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private readonly Application application;
    private ThemeChoice choice;
    private bool? lightLoaded;

    public ThemeManager(Application application) {
        this.application = application;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Apply(ThemeChoice choice) {
        this.choice = choice;
        Load(UseLight(choice, SystemUsesLightTheme()));
    }

    public void Dispose() {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    internal static bool UseLight(ThemeChoice choice, bool systemUsesLight) {
        return choice switch {
            ThemeChoice.Light => true,
            ThemeChoice.Dark => false,
            _ => systemUsesLight,
        };
    }

    // Windows has no value until the user first picks a mode, and then it is light.
    internal static bool SystemUsesLightTheme() {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }

    private void Load(bool light) {
        string name = light ? "Light" : "Dark";
        string assembly = typeof(ThemeManager).Assembly.GetName().Name!;
        application.Resources.MergedDictionaries[0] = new ResourceDictionary {
            Source = new Uri($"pack://application:,,,/{assembly};component/Themes/{name}.xaml")
        };
        lightLoaded = light;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle)) {
            return;
        }

        application.Dispatcher.BeginInvoke(() => {
            if (choice != ThemeChoice.System) {
                return;
            }
            bool light = SystemUsesLightTheme();
            if (light != lightLoaded) {
                Load(light);
            }
        });
    }
}
