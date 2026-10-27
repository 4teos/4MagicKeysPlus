using System.Windows;
using Microsoft.Win32;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.ViewModels;
using _4MagicKeysPlus.Views;

namespace _4MagicKeysPlus;

public partial class MainWindow : Window {
    private readonly IDriverControl driver;
    private readonly KeyboardSession keyboards;
    private readonly DeviceStatusViewModel deviceStatus;
    private readonly KeyboardSelectorViewModel selector;
    // The editors of each keyboard are made on first use and kept, so the screens share them and unsaved edits survive
    // switching between the screens and between the keyboards.
    private readonly Dictionary<Guid, ProfileEditors> editors = [];
    private readonly HotkeyEditor hotkeyEditor = new();
    private readonly OptionsViewModel options;
    private Guid? shownKeyboard;

    // launched is what the app was started with: a language chosen since then waits for a restart.
    internal MainWindow(IAppSettingsStore settings, AppSettings launched, IThemeApplier themes) : this(settings, launched, themes, new DriverControl()) {
    }

    private MainWindow(IAppSettingsStore settings, AppSettings launched, IThemeApplier themes, IDriverControl driver)
        : this(settings, launched, themes, driver, new KeyboardSession(new RegistryKeyboardProfileStore(), KeyboardEnumerator.Enumerate, driver)) {
    }

    // The driver and the keyboards are parameters so that tests can use fakes.
    internal MainWindow(IAppSettingsStore settings, AppSettings launched, IThemeApplier themes, IDriverControl driver, KeyboardSession keyboards) {
        InitializeComponent();

        this.driver = driver;
        this.keyboards = keyboards;
        // The app is often opened because a keyboard has no profile yet: that is dealt with before the window shows.
        keyboards.Refresh();
        deviceStatus = new DeviceStatusViewModel(keyboards, ReadFnLock, KeyboardBattery.ReadStatus);
        selector = new KeyboardSelectorViewModel(keyboards);
        DataContext = deviceStatus;
        KeyboardSelector.DataContext = selector;
        LinkBanner.DataContext = selector;
        keyboards.Changed += OnKeyboardsChanged;

        options = new OptionsViewModel(
            new AppearanceViewModel(settings, themes, Languages.Available(AppContext.BaseDirectory), launched, AppRestart.TryRestart),
            new AgentOptionsViewModel(new AgentLink(), new RegistryAgentAutostart(), settings, AgentLauncher.TryStart),
            new SystemStatusViewModel(new DriverProbe(), () => deviceStatus.IsKeyboardDetected ? $"{deviceStatus.DeviceName}, {deviceStatus.ConnectionText}" : null),
            typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "—");
        ShowKeys();
        deviceStatus.Start();
    }

    private static bool ReadFnLock(Guid profileId) {
        return new RegistryFnLockStore(Registry.LocalMachine, DriverStatus.ProfileKeyPath(profileId)).Read();
    }

    private ProfileEditors EditorsOf(Guid profileId) {
        if (!editors.TryGetValue(profileId, out ProfileEditors? profileEditors)) {
            profileEditors = ProfileEditors.Open(profileId, driver);
            profileEditors.FnLock.Changed += deviceStatus.Refresh;
            editors[profileId] = profileEditors;
        }
        return profileEditors;
    }

    // The Keys screen belongs to the selected keyboard; with none connected there is nothing to show on it.
    private void ShowKeys() {
        KeyboardEntry? selected = keyboards.Selected;
        shownKeyboard = selected?.Profile.Id;
        MainContent.Content = selected is null ? new NoKeyboardView() : new KeysView(EditorsOf(selected.Profile.Id), selected.Profile.Product);
    }

    // The screen is built anew only for another keyboard: a refresh that changes nothing must not undo what is being edited.
    private void OnKeyboardsChanged() {
        if (NavKeys.IsChecked == true && keyboards.SelectedId != shownKeyboard) {
            ShowKeys();
        }
    }

    private void NavKeys_Checked(object sender, RoutedEventArgs e) {
        if (MainContent is not null) {
            ShowKeys();
        }
    }

    private void NavApps_Checked(object sender, RoutedEventArgs e) {
        MainContent?.Content = new HotkeyListView(hotkeyEditor);
    }

    private void NavOptions_Checked(object sender, RoutedEventArgs e) {
        MainContent?.Content = new OptionsView(options);
    }

    private void LinkYes_Click(object sender, RoutedEventArgs e) {
        selector.LinkToCandidate();
    }

    private void LinkNo_Click(object sender, RoutedEventArgs e) {
        selector.StartNewKeyboard();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) {
        SystemCommands.MinimizeWindow(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) {
        SystemCommands.CloseWindow(this);
    }
}
