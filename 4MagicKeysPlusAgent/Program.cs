using System.Diagnostics;

namespace _4MagicKeysPlusAgent;

internal static class Program {
    [STAThread]
    private static void Main(string[] args) {
        ApplicationConfiguration.Initialize();

#if DEBUG
        // Test switches, Debug builds only: --demo-battery <percent> [delay seconds] shows the
        // notice once, after the delay, and exits.
        int? demoPercent = IntArgument(args, "--demo-battery");
        if (demoPercent is not null) {
            using LowBatteryForm demo = new LowBatteryForm(demoPercent.Value);
            System.Windows.Forms.Timer diagnosticsTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            diagnosticsTimer.Tick += (_, _) => {
                diagnosticsTimer.Stop();
                ZOrderDiagnostics.Log(demo.Handle);
            };
            demo.Shown += (_, _) => diagnosticsTimer.Start();
            Thread.Sleep(TimeSpan.FromSeconds(IntArgument(args, "--demo-battery", valueOffset: 2) ?? 0));
            Application.Run(demo);
            return;
        }
#endif

        List<AppLaunchEntry> entries = AppLaunchConfig.Load();

        using HotkeyWindow window = new HotkeyWindow();
        Dictionary<int, string> pathById = entries.ToDictionary(e => e.HotkeyId, e => e.ExecutablePath);

        foreach (AppLaunchEntry entry in entries) {
            if (!window.RegisterHotKey(entry, out int win32Error)) {
                Logger.Warn($"Failed to register hotkey '{entry.Name}' (vk=0x{entry.VirtualKey:X2}, mod=0x{entry.Modifiers:X2}), Win32 error {win32Error}.");
            }
        }

        using BatteryMonitor batteryMonitor = CreateBatteryMonitor(args);
        batteryMonitor.Start();

        window.HotKeyPressed += id => {
            if (pathById.TryGetValue(id, out string? path)) {
                LaunchApp(path);
            }
        };

        Application.Run();
    }

    private static BatteryMonitor CreateBatteryMonitor(string[] args) {
#if DEBUG
        // --battery-interval <seconds>: check that often instead, each reading logged.
        int? intervalSeconds = IntArgument(args, "--battery-interval");
        if (intervalSeconds is not null) {
            return new BatteryMonitor(TimeSpan.FromSeconds(intervalSeconds.Value), verbose: true);
        }
#endif
        return new BatteryMonitor(BatteryMonitor.DefaultInterval, verbose: false);
    }

#if DEBUG
    private static int? IntArgument(string[] args, string name, int valueOffset = 1) {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + valueOffset < args.Length && int.TryParse(args[index + valueOffset], out int value) ? value : null;
    }
#endif

    private static void LaunchApp(string path) {
        try {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        } catch (Exception ex) {
            Logger.Error($"Failed to launch '{path}': {ex.Message}");
        }
    }
}
