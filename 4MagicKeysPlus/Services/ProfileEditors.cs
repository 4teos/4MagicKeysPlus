using Microsoft.Win32;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlus.Services;

// The two editors of the Keys screen for one keyboard, over the key of its profile. They are made once per keyboard and
// kept for the session, so that going to another keyboard and back leaves what was shown as it was.
internal sealed class ProfileEditors {
    public ProfileEditors(KeyRemapEditor rules, FnLockEditor fnLock) {
        Rules = rules;
        FnLock = fnLock;
    }

    public KeyRemapEditor Rules {
        get;
    }

    public FnLockEditor FnLock {
        get;
    }

    public static ProfileEditors Open(Guid profileId, IDriverControl driver) {
        RegistryFnLockStore fnLockStore = new(Registry.LocalMachine, DriverStatus.ProfileKeyPath(profileId));
        string rulesPath = DriverStatus.ProfileRulesKeyPath(profileId);
        return new ProfileEditors(
            new KeyRemapEditor(DriverStatus.ReadRules(Registry.LocalMachine, rulesPath), new RegistryRulesStore(Registry.LocalMachine, rulesPath), driver, fnLockStore),
            new FnLockEditor(fnLockStore, driver));
    }
}
