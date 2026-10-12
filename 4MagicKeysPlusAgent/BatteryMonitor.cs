using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusAgent;

// Polls the keyboard battery and shows a notice when it runs low. The first check comes one
// interval after start, so a freshly started session isn't greeted by a popup.
internal sealed class BatteryMonitor : IDisposable {
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(15);

    private readonly System.Windows.Forms.Timer timer = new();
    private readonly BatteryAlertPolicy policy = new();
    private readonly bool verbose;
    private DetectedKeyboard? detectedKeyboard;
    private bool checking;
    private LowBatteryForm? form;

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
            KeyboardBatteryStatus? status = await Task.Run(ReadStatus);
            if (verbose) {
                Logger.Info(status == null ? "Battery: keyboard not found." : $"Battery: {status.Percent}%, charging={status.IsCharging}.");
            }

            if (status != null && policy.Evaluate(status) is not null) {
                Show(status.Percent);
            }
        } catch (Exception ex) {
            Logger.Error($"Battery check failed: {ex.Message}");
        } finally {
            checking = false;
        }
    }

    // The path found earlier goes stale on sleep or a USB/Bluetooth switch, so a failed read
    // triggers one fresh detection instead of retrying the dead path forever.
    private KeyboardBatteryStatus? ReadStatus() {
        detectedKeyboard ??= KeyboardDetector.DetectConnectedKeyboard();

        KeyboardBatteryStatus? status = KeyboardBattery.ReadStatus(detectedKeyboard);
        if (status == null && detectedKeyboard != null) {
            detectedKeyboard = KeyboardDetector.DetectConnectedKeyboard();
            status = KeyboardBattery.ReadStatus(detectedKeyboard);
        }

        return status;
    }

    private void Show(int percent) {
        form?.Close();
        form = new LowBatteryForm(percent);
        form.FormClosed += (_, _) => form = null;
        form.Show();
    }

    public void Dispose() {
        timer.Dispose();
        form?.Close();
    }
}
