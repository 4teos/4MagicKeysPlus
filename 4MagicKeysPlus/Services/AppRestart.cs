using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using _4MagicKeysPlus.Properties;

namespace _4MagicKeysPlus.Services;

internal static class AppRestart {
    // The copy needs administrator rights like this one, and gets them without a prompt because a process that has them starts it.
    // Null on success, else what went wrong.
    public static string? TryRestart() {
        try {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("The path of the app is unknown")));
        } catch (Exception exception) when (exception is Win32Exception or InvalidOperationException) {
            return string.Format(Strings.OptionsRestartFailed, exception.Message);
        }

        Application.Current.Shutdown();
        return null;
    }
}
