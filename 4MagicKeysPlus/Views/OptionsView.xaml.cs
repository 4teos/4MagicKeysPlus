using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlus.Views;

public partial class OptionsView : UserControl {
    private readonly OptionsViewModel viewModel;
    private readonly DispatcherTimer timer = new() {
        Interval = TimeSpan.FromSeconds(3)
    };

    public OptionsView(OptionsViewModel viewModel) {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;

        timer.Tick += async (_, _) => await viewModel.RefreshAsync();
        // The screen is built anew on every visit, so its timer must not outlive it.
        Loaded += async (_, _) => {
            timer.Start();
            await viewModel.RefreshAsync();
        };
        Unloaded += (_, _) => timer.Stop();
    }

    private void Restart_Click(object sender, RoutedEventArgs e) {
        viewModel.Appearance.Restart();
    }

    private async void StartAgent_Click(object sender, RoutedEventArgs e) {
        await viewModel.Agent.StartAsync();
    }

    private void OpenForNewKeyboard_Click(object sender, RoutedEventArgs e) {
        viewModel.Agent.SetOpenAppForNewKeyboard(OpenForNewKeyboardToggle.IsChecked == true);
        OpenForNewKeyboardToggle.SetCurrentValue(ToggleButton.IsCheckedProperty, viewModel.Agent.OpenAppForNewKeyboard);
    }

    private void Autostart_Click(object sender, RoutedEventArgs e) {
        viewModel.Agent.SetAutostart(AutostartToggle.IsChecked == true);
        // Assigning IsChecked would replace its binding, which keeps the switch in step with the registry.
        AutostartToggle.SetCurrentValue(ToggleButton.IsCheckedProperty, viewModel.Agent.AutostartEnabled);
    }
}
