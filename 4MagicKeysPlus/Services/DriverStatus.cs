using Microsoft.Win32;
using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlus.Services;

internal static class DriverStatus {
    private const string ParametersKeyPath = @"SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters";
    private const string RulesKeyName = "Rules";

    public const string RulesKeyPath = $@"{ParametersKeyPath}\{RulesKeyName}";

    public static bool ReadFnLockEnabled() {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(ParametersKeyPath);
        return key?.GetValue("FnLock") is int fnLock && fnLock != 0;
    }

    public static DriverKeyRemapConfig ReadKeyRemapConfig() {
        return new DriverKeyRemapConfig(ReadRules(), ReadFnLockEnabled());
    }

    // Subkeys without both Source and Target as DWORDs (e.g. launch-only entries) are not driver rules.
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
                rules.Add(rule with { RegistryName = name });
            }
        }
        return rules;
    }
}
