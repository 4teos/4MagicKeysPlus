using System.Windows;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.ViewModels;
using _4MagicKeysPlus.Views;

namespace _4MagicKeysPlus;

public partial class MainWindow : Window {
    // Shared by the screens, so unsaved edits survive switching between them.
    private readonly KeyRemapEditor keyRemapEditor = new();

    public MainWindow() {
        InitializeComponent();
        DataContext = new DeviceStatusViewModel();
        MainContent.Content = new KeysView(keyRemapEditor);
    }

    private void NavKeys_Checked(object sender, RoutedEventArgs e) {
        MainContent?.Content = new KeysView(keyRemapEditor);
    }

    private void NavApps_Checked(object sender, RoutedEventArgs e) {
        MainContent?.Content = new KeysView(keyRemapEditor, Strings.QuickLaunchPlaceholderNotice);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) {
        SystemCommands.MinimizeWindow(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) {
        SystemCommands.CloseWindow(this);
    }
}
