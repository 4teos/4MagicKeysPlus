using System.IO;
using System.Security;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlus.Services;

// Everything that reads or writes the profiles goes through here one at a time, so that two refreshes never both make a profile for the same keyboard.
internal sealed class KeyboardSession {
    private readonly IKeyboardProfileStore store;
    private readonly Func<IReadOnlyList<KeyboardEndpoint>> enumerate;
    private readonly IDriverControl driver;
    private readonly IDefaultSettings? defaults;
    private readonly object access = new();
    private int refreshing;

    public KeyboardSession(IKeyboardProfileStore store, Func<IReadOnlyList<KeyboardEndpoint>> enumerate, IDriverControl driver, IDefaultSettings? defaults = null) {
        this.store = store;
        this.enumerate = enumerate;
        this.driver = driver;
        this.defaults = defaults;
    }

    // Also raised by a refresh that found nothing new, on the thread that asked.
    public event Action? Changed;

    public KeyboardCatalog Catalog {
        get; private set;
    } = KeyboardCatalog.Empty;

    // Always a connected one, null when none is. It is not remembered: the one that is shown stays while it is connected, else the first connected one is.
    public Guid? SelectedId {
        get; private set;
    }

    public KeyboardEntry? Selected => Catalog.Keyboards.FirstOrDefault(keyboard => keyboard.Profile.Id == SelectedId);

    public void Refresh() {
        Apply(Gather(), SelectedId);
    }

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

    // Tells the driver, which finds the profile of a keyboard only when the keyboard connects or when it is told.
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

    internal static Guid? ChooseSelection(Guid? current, IReadOnlyList<KeyboardEntry> keyboards) {
        KeyboardEntry? keep = keyboards.FirstOrDefault(keyboard => keyboard.Profile.Id == current && keyboard.IsConnected);
        return (keep ?? keyboards.FirstOrDefault(keyboard => keyboard.IsConnected))?.Profile.Id;
    }

    // A keyboard that is not the same as any profile's keyboard gets a profile at once; the ones that may be the same wait for the answer of the user.
    // Creating one profile may change the answer for the next keyboard (the two ways of one new keyboard), so each is looked at again afterwards.
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
