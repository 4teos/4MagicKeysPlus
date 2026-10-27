using System.IO;
using System.Security;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlus.Services;

// The keyboards of the running app: what is stored, what is connected, and which keyboard the screens show. Lives for the
// whole app session (owned by MainWindow). Everything that reads or writes the profiles goes through here one at a time, so
// that two refreshes never both make a profile for the same keyboard.
internal sealed class KeyboardSession {
    private readonly IKeyboardProfileStore store;
    private readonly Func<IReadOnlyList<KeyboardEndpoint>> enumerate;
    private readonly IDriverControl driver;
    private readonly IDefaultSettings? defaults;
    private readonly object access = new();
    private int refreshing;

    // A new profile begins as a copy of the default settings of its model, when there are any.
    public KeyboardSession(IKeyboardProfileStore store, Func<IReadOnlyList<KeyboardEndpoint>> enumerate, IDriverControl driver, IDefaultSettings? defaults = null) {
        this.store = store;
        this.enumerate = enumerate;
        this.driver = driver;
        this.defaults = defaults;
    }

    // Raised after the keyboards or the selection changed (also by a refresh that found nothing new), on the thread that asked.
    public event Action? Changed;

    public KeyboardCatalog Catalog {
        get; private set;
    } = KeyboardCatalog.Empty;

    // The profile shown on the screens, always one that is connected; null when none is. It is not remembered: the one that
    // is shown stays while it is connected, else the first connected one is.
    public Guid? SelectedId {
        get; private set;
    }

    public KeyboardEntry? Selected => Catalog.Keyboards.FirstOrDefault(keyboard => keyboard.Profile.Id == SelectedId);

    // Looks at the keyboards at once, on this thread.
    public void Refresh() {
        Apply(Gather(), SelectedId);
    }

    // The same without holding the caller up, for the timer; a refresh that is still going on is not started again.
    public async Task RefreshAsync() {
        if (Interlocked.Exchange(ref refreshing, 1) != 0) {
            return;
        }

        try {
            Apply(await Task.Run(Gather), SelectedId);
        } finally {
            Volatile.Write(ref refreshing, 0);
        }
    }

    // False for a keyboard that is not connected (or not known): its settings cannot be opened.
    public bool Select(Guid profileId) {
        if (!Catalog.Keyboards.Any(keyboard => keyboard.Profile.Id == profileId && keyboard.IsConnected)) {
            return false;
        }

        Apply(Catalog, profileId);
        return true;
    }

    // The connected keyboard is the one of the profile: adds its way of connecting to the profile and tells the driver, which
    // finds the profile of a keyboard only when the keyboard connects or when it is told.
    public ApplyResult Link(UnownedKeyboard keyboard, KeyboardProfile profile) {
        try {
            lock (access) {
                KeyboardProfile stored = store.Load().FirstOrDefault(candidate => candidate.Id == profile.Id) ?? profile;
                store.Save(stored with {
                    Identities = [.. stored.Identities, keyboard.Endpoint.Identity]
                });
            }
        } catch (Exception exception) when (IsStoreFailure(exception)) {
            return new ApplyResult(ApplyOutcome.Failed, string.Format(Strings.ApplySaveFailed, exception.Message));
        }

        ApplyResult result = KeyRemapEditor.ReloadDriver(driver);
        Apply(Gather(), profile.Id);
        return result;
    }

    // The connected keyboard is another one than those that are known: it gets a profile of its own.
    public ApplyResult StartNew(UnownedKeyboard keyboard) {
        Guid? created;
        try {
            lock (access) {
                created = Create(keyboard.Endpoint, store.Load());
            }
        } catch (Exception exception) when (IsStoreFailure(exception)) {
            return new ApplyResult(ApplyOutcome.Failed, string.Format(Strings.ApplySaveFailed, exception.Message));
        }

        Apply(Gather(), created);
        return new ApplyResult(ApplyOutcome.Applied, Strings.ApplyApplied);
    }

    // The one a keyboard is shown under: the one that was shown if it is still connected, else the first one that is.
    internal static Guid? ChooseSelection(Guid? current, IReadOnlyList<KeyboardEntry> keyboards) {
        KeyboardEntry? keep = keyboards.FirstOrDefault(keyboard => keyboard.Profile.Id == current && keyboard.IsConnected);
        return (keep ?? keyboards.FirstOrDefault(keyboard => keyboard.IsConnected))?.Profile.Id;
    }

    // A connected keyboard that is not the same as any of the profiles' keyboards gets a profile at once. The ones that may be
    // the same as one of them wait for the answer of the user. Creating one profile may change the answer for the next
    // keyboard (the two ways of one new keyboard), so each is looked at again afterwards.
    private KeyboardCatalog Gather() {
        lock (access) {
            KeyboardCatalog catalog;
            for (int attempts = 0; ; attempts++) {
                IReadOnlyList<KeyboardProfile> profiles = store.Load();
                catalog = KeyboardCatalog.Build(profiles, enumerate());
                UnownedKeyboard? fresh = catalog.Unowned.FirstOrDefault(keyboard => keyboard.Candidates.Count == 0);
                if (fresh is null || attempts >= catalog.Unowned.Count || !TryCreate(fresh.Endpoint, profiles)) {
                    return catalog;
                }
            }
        }
    }

    private bool TryCreate(KeyboardEndpoint endpoint, IReadOnlyList<KeyboardProfile> profiles) {
        try {
            Create(endpoint, profiles);
            return true;
        } catch (Exception exception) when (IsStoreFailure(exception)) {
            return false;
        }
    }

    private Guid Create(KeyboardEndpoint endpoint, IReadOnlyList<KeyboardProfile> profiles) {
        KeyboardProfile profile = new(
            Guid.NewGuid(),
            endpoint.Product,
            [endpoint.Identity],
            KeyboardCatalog.NewIndex(endpoint.Product, profiles));
        store.Save(profile);
        defaults?.CopyToProfile(profile.Id, endpoint.Product);
        return profile.Id;
    }

    // The store refuses an identity that another profile lists, which only a second writer can bring about.
    private static bool IsStoreFailure(Exception exception) {
        return exception is UnauthorizedAccessException or IOException or SecurityException or InvalidOperationException;
    }

    private void Apply(KeyboardCatalog catalog, Guid? wanted) {
        Catalog = catalog;
        SelectedId = ChooseSelection(wanted, catalog.Keyboards);
        Changed?.Invoke();
    }
}
