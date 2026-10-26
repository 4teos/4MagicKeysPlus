using Microsoft.Win32;
using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlus.Services;

internal static class DriverStatus {
    internal const string ServiceKeyPath = @"SYSTEM\CurrentControlSet\Services\4MagicKeysPlus";
    internal const string ParametersKeyPath = $@"{ServiceKeyPath}\Parameters";
    private const string RulesKeyName = "Rules";
    private const string HotkeysKeyName = "Hotkeys";
    private const string KeyboardsKeyName = "Keyboards";

    public const string RulesKeyPath = $@"{ParametersKeyPath}\{RulesKeyName}";
    public const string HotkeysKeyPath = $@"{ParametersKeyPath}\{HotkeysKeyName}";
    public const string KeyboardsKeyPath = $@"{ParametersKeyPath}\{KeyboardsKeyName}";

    // The subkey of a keyboard profile under Keyboards: its GUID in braces, upper case.
    public static string ProfileKeyName(Guid profileId) {
        return profileId.ToString("B").ToUpperInvariant();
    }

    public static bool ReadFnLockEnabled() {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(ParametersKeyPath);
        return key?.GetValue("FnLock") is int fnLock && fnLock != 0;
    }

    public static DriverKeyRemapConfig ReadKeyRemapConfig() {
        return new DriverKeyRemapConfig(ReadRules(), ReadFnLockEnabled());
    }

    // Subkeys without both Source and Target as DWORDs are not driver rules.
    private static List<KeyRule> ReadRules() {
        List<KeyRule> rules = [];

        using RegistryKey? rulesKey = Registry.LocalMachine.OpenSubKey(RulesKeyPath);
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
