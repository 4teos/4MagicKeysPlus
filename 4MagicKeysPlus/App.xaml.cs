using System.Windows;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlus;

public partial class App : Application {
    private ThemeManager? themes;

    // The language is read once by the strings and the theme is what the first window is drawn
    // with, so both are applied before it is created (which is why App.xaml has no StartupUri).
    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        if (DefaultsCommand.Asks(e.Args)) {
            Shutdown(DefaultsCommand.RunFromConsole(e.Args));
            return;
        }

        RegistryAppSettingsStore store = new();
        AppSettings settings = store.Read();
        Languages.Apply(settings.Language);
        themes = new ThemeManager(this);
        themes.Apply(settings.Theme);

        new MainWindow(store, settings, themes).Show();
    }

    protected override void OnExit(ExitEventArgs e) {
        themes?.Dispose();
        base.OnExit(e);
    }
}
