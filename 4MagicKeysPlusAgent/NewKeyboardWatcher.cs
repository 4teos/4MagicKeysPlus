using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusAgent;

// Opens the configuration app when a keyboard is connected that no profile has: the driver gives such a keyboard no
// rules, so somebody has to give it a profile (the app does, or asks whether it is the same keyboard as one it knows).
// The app is almost never running, so the agent is the one that notices. Every keyboard is brought up once per agent
// session: a prompt that was dismissed is not repeated until the agent starts again.
internal sealed class NewKeyboardWatcher : IDisposable {
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(10);

    private readonly Func<IReadOnlyList<KeyboardEndpoint>> enumerate;
    private readonly Func<IReadOnlyList<KeyboardProfile>> loadProfiles;
    private readonly Func<bool> isEnabled;
    private readonly Func<bool> isAppRunning;
    private readonly Action openApp;
    private readonly TimeSpan interval;
    private readonly HashSet<KeyboardIdentity> handled = [];
    private System.Threading.Timer? timer;
    private int checking;

    public NewKeyboardWatcher(
        Func<IReadOnlyList<KeyboardEndpoint>> enumerate,
        Func<IReadOnlyList<KeyboardProfile>> loadProfiles,
        Func<bool> isEnabled,
        Func<bool> isAppRunning,
        Action openApp,
        TimeSpan interval) {
        this.enumerate = enumerate;
        this.loadProfiles = loadProfiles;
        this.isEnabled = isEnabled;
        this.isAppRunning = isAppRunning;
        this.openApp = openApp;
        this.interval = interval;
    }

    // The first check comes one interval after start, when the session has settled.
    public void Start() {
        timer = new System.Threading.Timer(_ => OnTimer(), null, interval, interval);
    }

    // A running app looks after the keyboards that it sees itself, so the keyboards found while it runs count as brought up.
    // When the setting is off they are left alone, so that switching it on later still opens the app for what has no profile.
    public void Check() {
        if (!isEnabled()) {
            return;
        }

        List<KeyboardIdentity> found = [.. KeyboardOwnership.UnownedLiveEndpoints(enumerate(), loadProfiles()).Select(endpoint => endpoint.Identity)];
        List<KeyboardIdentity> fresh = [.. found.Where(identity => !handled.Contains(identity))];
        if (fresh.Count == 0) {
            return;
        }

        handled.UnionWith(fresh);
        if (!isAppRunning()) {
            openApp();
        }
    }

    // A check that is still running (the enumeration can be slow) is not started a second time.
    private void OnTimer() {
        if (Interlocked.Exchange(ref checking, 1) != 0) {
            return;
        }

        try {
            Check();
        } catch (Exception exception) {
            Logger.Error($"Checking for new keyboards failed: {exception.Message}");
        } finally {
            Volatile.Write(ref checking, 0);
        }
    }

    public void Dispose() {
        timer?.Dispose();
    }
}
