using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusAgent;

// Polls the battery of every connected keyboard and shows a notice, a window of its own, for each one that runs low. The
// first check comes one interval after start, so a freshly started session isn't greeted by a popup.
internal sealed class BatteryMonitor : IDisposable {
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(15);

    private readonly System.Windows.Forms.Timer timer = new();
    private readonly BatteryAlerts alerts = new();
    private readonly Dictionary<string, LowBatteryForm> forms = [];
    private readonly bool verbose;
    private bool checking;

    public BatteryMonitor(TimeSpan interval, bool verbose) {
        this.verbose = verbose;
        timer.Interval = (int)interval.TotalMilliseconds;
        timer.Tick += OnTick;
    }

    public void Start() {
        timer.Start();
    }

    private async void OnTick(object? sender, EventArgs e) {
        if (checking) {
            return;
        }

        checking = true;
        try {
            IReadOnlyList<BatteryReading> readings = await Task.Run(ReadAll);
            if (verbose) {
                Log(readings);
            }

            foreach (BatteryNotice notice in alerts.Evaluate(readings)) {
                Show(notice);
            }
        } catch (Exception ex) {
            Logger.Error($"Battery check failed: {ex.Message}");
        } finally {
            checking = false;
        }
    }

    // The keyboards are looked for afresh each time: the paths found earlier go stale on sleep or a USB/Bluetooth switch,
    // and a keyboard may have been connected or taken away since.
    private static IReadOnlyList<BatteryReading> ReadAll() {
        IReadOnlyList<BatteryTarget> targets = BatteryTargets.Select(KeyboardEnumerator.Enumerate(), new RegistryKeyboardProfileStore().Load(), Strings.ModelName);
        return [.. targets.Select(target => new BatteryReading(target, KeyboardBattery.ReadStatus(target.Keyboard)))];
    }

    private static void Log(IReadOnlyList<BatteryReading> readings) {
        if (readings.Count == 0) {
            Logger.Info("Battery: keyboard not found.");
        }

        foreach (BatteryReading reading in readings) {
            string name = reading.Target.Name ?? reading.Target.Key;
            Logger.Info(reading.Status == null ? $"Battery {name}: not readable." : $"Battery {name}: {reading.Status.Percent}%, charging={reading.Status.IsCharging}.");
        }
    }

    // A keyboard that is warned about again (the next threshold) replaces its own notice; the notices of the other keyboards stay.
    private void Show(BatteryNotice notice) {
        if (forms.TryGetValue(notice.Key, out LowBatteryForm? previous)) {
            previous.Close();
        }

        LowBatteryForm form = new(notice.Percent, notice.KeyboardName);
        forms[notice.Key] = form;
        form.FormClosed += (_, _) => {
            if (forms.TryGetValue(notice.Key, out LowBatteryForm? current) && current == form) {
                forms.Remove(notice.Key);
            }
        };
        form.Show();
    }

    public void Dispose() {
        timer.Dispose();
        foreach (LowBatteryForm form in forms.Values.ToList()) {
            form.Close();
        }
    }
}
