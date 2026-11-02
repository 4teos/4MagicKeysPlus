using System.Windows;
using System.Windows.Input;

namespace _4MagicKeysPlus.Views;

// No frame of the system, the colors of the theme; Esc and No say no.
internal sealed partial class ConfirmDialog : Window {
    internal ConfirmDialog(string question) {
        InitializeComponent();
        Question.Text = question;
    }

    public static bool Ask(Window? owner, string question) {
        ConfirmDialog dialog = new(question);
        if (owner is not null) {
            dialog.Owner = owner;
        } else {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        return dialog.ShowDialog() == true;
    }

    private void YesButton_Click(object sender, RoutedEventArgs e) {
        DialogResult = true;
    }

    // There is no title bar to drag it by.
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
        DragMove();
    }
}
