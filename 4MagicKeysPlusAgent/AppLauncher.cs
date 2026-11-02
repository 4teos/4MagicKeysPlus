using System.Diagnostics;

namespace _4MagicKeysPlusAgent;

internal static class AppLauncher {
    private const string AppName = "4MagicKeysPlus";

    public static string AppPath => Path.Combine(AppContext.BaseDirectory, $"{AppName}.exe");

    public static bool IsRunning() {
        Process[] running = Process.GetProcessesByName(AppName);
        foreach (Process process in running) {
            process.Dispose();
        }
        return running.Length > 0;
    }

    // Declining the consent prompt (the manifest of the app asks for administrator rights) is no error worth more than a line in the log.
    public static void Open() {
        string path = AppPath;
        if (!File.Exists(path)) {
            Logger.Warn($"Cannot open the app, '{path}' does not exist.");
            return;
        }

        try {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = AppContext.BaseDirectory });
            Logger.Info("Opened the app for a keyboard that has no profile.");
        } catch (Exception exception) {
            Logger.Error($"Failed to open the app '{path}': {exception.Message}");
        }
    }
}
