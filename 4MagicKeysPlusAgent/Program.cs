using System.Diagnostics;
using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusAgent;

internal static class Program {
    private static readonly TimeSpan PauseTimeout = TimeSpan.FromSeconds(30);

    [STAThread]
    private static void Main(string[] args) {
        ApplicationConfiguration.Initialize();

#if DEBUG
        // Test switches, Debug builds only: --demo-battery <percent> [delay seconds] shows the
        // notice once, after the delay, and exits; --demo-hotkey-errors shows the registration
        // errors notice with sample lines until it is clicked.
        if (args.Contains("--demo-hotkey-errors")) {
            using HotkeyErrorsForm errorsDemo = new HotkeyErrorsForm([
                "Windows Terminal (Ctrl+Alt+T): Hot key is already registered.",
                "Notepad (Ctrl+Shift+N): Hot key is already registered.",
                "Visual Studio Code with a deliberately long name to see how the window wraps it (Ctrl+Alt+F9): Hot key is already registered.",
            ]);
            Application.Run(errorsDemo);
            return;
        }

        // --demo-batteries: the notices of three keyboards at once, to see that they do not overlap.
        if (args.Contains("--demo-batteries")) {
            ApplicationContext demoContext = new ApplicationContext();
            int shown = 0;
            foreach ((int percent, string name) in new[] {
                (18, Strings.ModelName(KeyboardProduct.A1644)),
                (4, $"{Strings.ModelName(KeyboardProduct.A1644)} (2)"),
                (15, Strings.ModelName(KeyboardProduct.A1843)),
            }) {
                LowBatteryForm form = new LowBatteryForm(percent, name);
                shown++;
                form.FormClosed += (_, _) => {
                    if (--shown == 0) {
                        demoContext.ExitThread();
                    }
                };
                form.Show();
            }
            Application.Run(demoContext);
            return;
        }

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

        // Two agents would both answer on the pipe and both fight for the same hotkeys.
        using Mutex singleInstance = new Mutex(true, @"Local\4MagicKeysPlusAgent", out bool firstInstance);
        if (!firstInstance) {
            return;
        }

        using HotkeyWindow window = new HotkeyWindow();
        // Hands work from the pipe's threads to this one, which owns the hotkey window.
        using Control uiThread = new Control();
        _ = uiThread.Handle;

        HotkeyManager hotkeys = new HotkeyManager(window, () => HotkeyConfig.Load());
        window.HotKeyPressed += id => {
            if (hotkeys.FindPath(id) is { } path) {
                LaunchApp(path);
            }
        };

        IReadOnlyList<HotkeyRegistration> registrations = hotkeys.Reload();
        IReadOnlyList<string> failures = hotkeys.DescribeFailures(registrations);
        if (failures.Count > 0) {
            uiThread.BeginInvoke(() => ShowHotkeyFailures(failures));
        }

        using BatteryMonitor batteryMonitor = CreateBatteryMonitor(args);
        batteryMonitor.Start();

        using NewKeyboardWatcher newKeyboards = new NewKeyboardWatcher(
            KeyboardEnumerator.Enumerate,
            () => new RegistryKeyboardProfileStore().Load(),
            AppSettingsRegistry.ReadOpenAppForNewKeyboard,
            AppLauncher.IsRunning,
            AppLauncher.Open,
            NewKeyboardWatcher.DefaultInterval);
        newKeyboards.Start();

        AgentCommandHandler commands = new AgentCommandHandler(hotkeys, work => (AgentResponse)uiThread.Invoke(work));
        using AgentPipeServer pipeServer = new AgentPipeServer(commands, PauseTimeout);
        pipeServer.Start();

        Application.Run();
    }

    private static void ShowHotkeyFailures(IReadOnlyList<string> failures) {
        HotkeyErrorsForm form = new HotkeyErrorsForm(failures);
        form.FormClosed += (_, _) => form.Dispose();
        form.Show();
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
