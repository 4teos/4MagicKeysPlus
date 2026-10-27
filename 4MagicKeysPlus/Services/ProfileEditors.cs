using Microsoft.Win32;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlus.Services;

// The editors of the Keys screen for one keyboard, over the key of its profile. They are made once per keyboard and
// kept for the session, so that going to another keyboard and back leaves what was shown as it was.
internal sealed class ProfileEditors {
    public ProfileEditors(KeyRemapEditor rules, FnLockEditor fnLock, DefaultsEditor defaults) {
        Rules = rules;
        FnLock = fnLock;
        Defaults = defaults;
    }

    public KeyRemapEditor Rules {
        get;
    }

    public FnLockEditor FnLock {
        get;
    }

    public DefaultsEditor Defaults {
        get;
    }

    public static ProfileEditors Open(Guid profileId, KeyboardProduct product, IDriverControl driver, IDefaultSettings defaults) {
        RegistryFnLockStore fnLockStore = new(Registry.LocalMachine, DriverStatus.ProfileKeyPath(profileId));
        string rulesPath = DriverStatus.ProfileRulesKeyPath(profileId);
        KeyRemapEditor rules = new(DriverStatus.ReadRules(Registry.LocalMachine, rulesPath), new RegistryRulesStore(Registry.LocalMachine, rulesPath), driver, fnLockStore);
        return new ProfileEditors(
            rules,
            new FnLockEditor(fnLockStore, driver),
            new DefaultsEditor(profileId, product, defaults, rules, () => DriverStatus.ReadRules(Registry.LocalMachine, rulesPath), driver));
    }
}
