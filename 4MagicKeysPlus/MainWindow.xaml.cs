using System.Windows;
using _4MagicKeysPlus.ViewModels;
using _4MagicKeysPlus.Views;

namespace _4MagicKeysPlus;

public partial class MainWindow : Window {
    public MainWindow() {
        InitializeComponent();
        DataContext = new DeviceStatusViewModel();
        MainContent.Content = new KeysView();
    }

    private void NavKeys_Checked(object sender, RoutedEventArgs e) {
        MainContent?.Content = new KeysView();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void CloseButton_Click(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);
}
