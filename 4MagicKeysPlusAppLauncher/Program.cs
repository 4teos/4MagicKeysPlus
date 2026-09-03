using System.Diagnostics;

namespace _4MagicKeysPlusAppLauncher;

internal static class Program {
    [STAThread]
    private static void Main() {
        List<AppLaunchEntry> entries = AppLaunchConfig.Load();
        if (entries.Count == 0) {
            return;
        }

        using HotkeyWindow window = new HotkeyWindow();
        Dictionary<int, string> pathById = entries.ToDictionary(e => e.HotkeyId, e => e.ExecutablePath);

        foreach (AppLaunchEntry entry in entries) {
            if (!window.RegisterHotKey(entry, out int win32Error)) {
                Logger.Warn($"Failed to register hotkey '{entry.Name}' (vk=0x{entry.VirtualKey:X2}, mod=0x{entry.Modifiers:X2}), Win32 error {win32Error}.");
            }
        }

        window.HotKeyPressed += id => {
            if (pathById.TryGetValue(id, out string? path)) {
                LaunchApp(path);
            }
        };

        Application.Run();
    }

    private static void LaunchApp(string path) {
        try {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        } catch (Exception ex) {
            Logger.Error($"Failed to launch '{path}': {ex.Message}");
        }
    }
}
