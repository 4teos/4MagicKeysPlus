namespace _4MagicKeysPlusAgent;

// There is no console (WinExe), so this file is the only way to see why a hotkey did not fire.
internal static class Logger {
    private static readonly string LogPath =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "4MagicKeysPlus", "agent.log");

    public static void Info(string message) {
        Write("INFO", message);
    }

    public static void Warn(string message) {
        Write("WARN", message);
    }

    public static void Error(string message) {
        Write("ERROR", message);
    }

    private static void Write(string level, string message) {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
        } catch {
            // Never let a logging failure take down the launcher.
        }
    }
}
