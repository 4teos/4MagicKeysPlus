using System.Diagnostics;

namespace _4MagicKeysPlusAgent;

// The configuration app, which is installed next to the agent.
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

    // The app asks for administrator rights in its manifest, so Windows shows the consent prompt. Declining it is no error
    // worth more than a line in the log.
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
