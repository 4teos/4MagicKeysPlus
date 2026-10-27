using Microsoft.Win32;

namespace _4MagicKeysPlus.Core;

// Where a new profile gets its settings from.
public interface IDefaultSettings {
    bool Has(KeyboardProduct model);

    // Makes the settings of the profile (its Fn Lock and its rules) a copy of the default settings of the model. False,
    // with the profile left as it is, when the model has none.
    bool CopyToProfile(Guid profileId, KeyboardProduct model);
}

// The default settings of the models as the driver reads them: a subkey of Parameters\Defaults for each model, named by its
// USB product ID as four hex digits (026C), laid out like a profile: FnLock (DWORD) and the Rules subkey, one subkey for each rule
// named by the hex of its Source, with the DWORDs Source and Target, FnLockAware only when set, and a Comment. The installer
// writes them from the shipped files (see DefaultSettingsFile); a profile gets a copy, so changing the defaults later
// leaves the keyboards that are set up as they are.
public sealed class RegistryDefaultSettings : IDefaultSettings {
    public const string DefaultsKeyPath = @"SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Defaults";

    private readonly RegistryKey root;
    private readonly string defaultsPath;
    private readonly string keyboardsPath;

    public RegistryDefaultSettings() : this(Registry.LocalMachine, DefaultsKeyPath, RegistryKeyboardProfileStore.KeyboardsKeyPath) {
    }

    // The root and paths are parameters so that tests can use a scratch key of their own.
    public RegistryDefaultSettings(RegistryKey root, string defaultsPath, string keyboardsPath) {
        this.root = root;
        this.defaultsPath = defaultsPath;
        this.keyboardsPath = keyboardsPath;
    }

    public static string ModelKeyName(KeyboardProduct model) {
        return KnownKeyboards.GetProductId(model).ToString("X4");
    }

    public bool Has(KeyboardProduct model) {
        using RegistryKey? key = root.OpenSubKey($@"{defaultsPath}\{ModelKeyName(model)}");
        return key is not null;
    }

    // Null when the model has no defaults. A rule without both Source and Target is not one, as for the driver.
    public DefaultSettings? Read(KeyboardProduct model) {
        using RegistryKey? key = root.OpenSubKey($@"{defaultsPath}\{ModelKeyName(model)}");
        if (key is null) {
            return null;
        }

        List<DefaultRule> rules = [];
        using (RegistryKey? rulesKey = key.OpenSubKey("Rules")) {
            foreach (string name in rulesKey?.GetSubKeyNames() ?? []) {
                using RegistryKey? ruleKey = rulesKey!.OpenSubKey(name);
                if (ruleKey?.GetValue("Source") is int source && ruleKey.GetValue("Target") is int target) {
                    rules.Add(new DefaultRule(
                        unchecked((uint)source),
                        unchecked((uint)target),
                        ruleKey.GetValue("FnLockAware") is int fnLockAware && fnLockAware != 0,
                        ruleKey.GetValue("Comment") as string));
                }
            }
        }

        return new DefaultSettings(model, key.GetValue("FnLock") is int fnLock && fnLock != 0, [.. rules.OrderBy(rule => rule.Source)]);
    }

    public void Write(DefaultSettings defaults) {
        string path = $@"{defaultsPath}\{ModelKeyName(defaults.Model)}";
        root.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        using RegistryKey key = root.CreateSubKey(path, writable: true);
        WriteSettings(key, defaults);
    }

    public bool CopyToProfile(Guid profileId, KeyboardProduct model) {
        DefaultSettings? defaults = Read(model);
        if (defaults is null) {
            return false;
        }

        string name = RegistryKeyboardProfileStore.ProfileKeyName(profileId);
        using RegistryKey key = root.OpenSubKey($@"{keyboardsPath}\{name}", writable: true)
            ?? throw new InvalidOperationException($"The profile {name} does not exist.");
        WriteSettings(key, defaults);
        return true;
    }

    // The Fn Lock and the whole Rules subkey of the key become those of the defaults.
    private static void WriteSettings(RegistryKey key, DefaultSettings defaults) {
        key.SetValue("FnLock", defaults.FnLock ? 1 : 0, RegistryValueKind.DWord);
        key.DeleteSubKeyTree("Rules", throwOnMissingSubKey: false);
        using RegistryKey rulesKey = key.CreateSubKey("Rules", writable: true);
        foreach (DefaultRule rule in defaults.Rules) {
            using RegistryKey ruleKey = rulesKey.CreateSubKey(rule.Source.ToString("X8"), writable: true);
            ruleKey.SetValue("Source", unchecked((int)rule.Source), RegistryValueKind.DWord);
            ruleKey.SetValue("Target", unchecked((int)rule.Target), RegistryValueKind.DWord);
            if (rule.FnLockAware) {
                ruleKey.SetValue("FnLockAware", 1, RegistryValueKind.DWord);
            }
            if (rule.Comment is not null) {
                ruleKey.SetValue("Comment", rule.Comment, RegistryValueKind.String);
            }
        }
    }
}
