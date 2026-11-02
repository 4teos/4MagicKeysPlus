using Microsoft.Win32;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlus.Services;

internal sealed class ProfileEditors {
    public ProfileEditors(KeyRemapEditor rules, FnLockEditor fnLock, DefaultsEditor defaults, LayoutEditor layout) {
        Rules = rules;
        FnLock = fnLock;
        Defaults = defaults;
        Layout = layout;
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

    public LayoutEditor Layout {
        get;
    }

    public static ProfileEditors Open(Guid profileId, KeyboardProduct product, IDriverControl driver, IDefaultSettings defaults) {
        RegistryFnLockStore fnLockStore = new(Registry.LocalMachine, DriverStatus.ProfileKeyPath(profileId));
        string rulesPath = DriverStatus.ProfileRulesKeyPath(profileId);
        bool touchId = KeyboardLayout.GetKeys(KeyboardModel.From(product), LayoutStandard.Iso).SelectMany(row => row.RowKeys).Any(key => key.Kind == PhysicalKey.TouchId);
        KeyRemapEditor rules = new(DriverStatus.ReadRules(Registry.LocalMachine, rulesPath, touchId), new RegistryRulesStore(Registry.LocalMachine, rulesPath), driver, fnLockStore);
        return new ProfileEditors(
            rules,
            new FnLockEditor(fnLockStore, driver),
            new DefaultsEditor(profileId, product, defaults, rules, () => DriverStatus.ReadRules(Registry.LocalMachine, rulesPath, touchId), driver),
            new LayoutEditor(new RegistryLayoutStore(Registry.LocalMachine, DriverStatus.ProfileKeyPath(profileId))));
    }
}
