using System.Windows;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.ViewModels;
using _4MagicKeysPlus.Views;

namespace _4MagicKeysPlus;

public partial class MainWindow : Window {
    // Shared by the screens, so unsaved edits survive switching between them.
    private readonly KeyRemapEditor keyRemapEditor = new();
    private readonly HotkeyEditor hotkeyEditor = new();
    private readonly FnLockEditor fnLockEditor = new();
    private readonly OptionsViewModel options;

    // launched is what the app was started with: a language chosen since then waits for a restart.
    internal MainWindow(IAppSettingsStore settings, AppSettings launched, IThemeApplier themes) {
        InitializeComponent();
        DeviceStatusViewModel deviceStatus = new();
        DataContext = deviceStatus;
        fnLockEditor.Changed += deviceStatus.Refresh;
        options = new OptionsViewModel(
            new AppearanceViewModel(settings, themes, Languages.Available(AppContext.BaseDirectory), launched, AppRestart.TryRestart),
            new AgentOptionsViewModel(new AgentLink(), new RegistryAgentAutostart(), AgentLauncher.TryStart),
            new SystemStatusViewModel(new DriverProbe(), () => deviceStatus.IsKeyboardDetected ? $"{deviceStatus.DeviceName}, {deviceStatus.ConnectionText}" : null));
        MainContent.Content = new KeysView(keyRemapEditor, fnLockEditor);
    }

    private void NavKeys_Checked(object sender, RoutedEventArgs e) {
        MainContent?.Content = new KeysView(keyRemapEditor, fnLockEditor);
    }

    private void NavApps_Checked(object sender, RoutedEventArgs e) {
        MainContent?.Content = new HotkeyListView(hotkeyEditor);
    }

    private void NavOptions_Checked(object sender, RoutedEventArgs e) {
        MainContent?.Content = new OptionsView(options);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) {
        SystemCommands.MinimizeWindow(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) {
        SystemCommands.CloseWindow(this);
    }
}
