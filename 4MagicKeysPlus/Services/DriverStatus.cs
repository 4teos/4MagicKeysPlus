using Microsoft.Win32;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlus.Services;

internal static class DriverStatus {
    internal const string ServiceKeyPath = @"SYSTEM\CurrentControlSet\Services\4MagicKeysPlus";
    internal const string ParametersKeyPath = $@"{ServiceKeyPath}\Parameters";
    private const string HotkeysKeyName = "Hotkeys";

    public const string HotkeysKeyPath = $@"{ParametersKeyPath}\{HotkeysKeyName}";

    // The key of a keyboard profile, which holds its FnLock and its Rules.
    public static string ProfileKeyPath(Guid profileId) {
        return $@"{RegistryKeyboardProfileStore.KeyboardsKeyPath}\{RegistryKeyboardProfileStore.ProfileKeyName(profileId)}";
    }

    public static string ProfileRulesKeyPath(Guid profileId) {
        return $@"{ProfileKeyPath(profileId)}\Rules";
    }

    // Subkeys without both Source and Target as DWORDs are not driver rules.
    public static List<KeyRule> ReadRules(RegistryKey root, string rulesKeyPath) {
        List<KeyRule> rules = [];

        using RegistryKey? rulesKey = root.OpenSubKey(rulesKeyPath);
        if (rulesKey is null) {
            return rules;
        }

        foreach (string name in rulesKey.GetSubKeyNames()) {
            using RegistryKey? ruleKey = rulesKey.OpenSubKey(name);
            if (ruleKey?.GetValue("Source") is not int source || ruleKey.GetValue("Target") is not int target) {
                continue;
            }

            bool fnLockAware = ruleKey.GetValue("FnLockAware") is int aware && aware != 0;
            KeyRule? rule = DriverConfigParser.ParseRule(unchecked((uint)source), unchecked((uint)target), fnLockAware);
            if (rule is not null) {
                rules.Add(rule with {
                    RegistryName = name
                });
            }
        }
        return rules;
    }
}
