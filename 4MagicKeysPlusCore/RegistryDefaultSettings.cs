using Microsoft.Win32;

namespace _4MagicKeysPlus.Core;

public interface IDefaultSettings {
    bool Has(KeyboardProduct model);

    // False, with the profile left as it is, when the model has no defaults.
    bool CopyToProfile(Guid profileId, KeyboardProduct model);
}

// A profile gets a copy, so changing the defaults later leaves the keyboards that are set up as they are.
public sealed class RegistryDefaultSettings : IDefaultSettings {
    public const string DefaultsKeyPath = @"SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Defaults";

    private readonly RegistryKey root;
    private readonly string defaultsPath;
    private readonly string keyboardsPath;

    public RegistryDefaultSettings() : this(Registry.LocalMachine, DefaultsKeyPath, RegistryKeyboardProfileStore.KeyboardsKeyPath) {
    }

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

    // A rule without both Source and Target is not one, as for the driver.
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
