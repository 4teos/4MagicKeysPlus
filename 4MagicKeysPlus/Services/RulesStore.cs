using Microsoft.Win32;
using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlus.Services;

// Where the rules are saved. Save gets the rules as last saved and the rules wanted now, and
// returns the latter with the subkey name each one has been saved under.
internal interface IRulesStore {
    IReadOnlyList<KeyRule> Save(IReadOnlyList<KeyRule> saved, IReadOnlyList<KeyRule> wanted);
}

internal sealed record RuleWrite(string Name, uint Source, uint Target, bool FnLockAware);

// What saving changes under Parameters\Rules, worked out without touching the registry. A rule
// that has no subkey yet (RegistryName is null) is written under the hex of its Source, the
// convention the driver's registry layout follows. Rules are never edited in place: a changed
// rule is a new one, so the subkey of the old one goes unless the new one takes its name.
internal sealed record RulesPlan(IReadOnlyList<string> Delete, IReadOnlyList<RuleWrite> Write, IReadOnlyList<KeyRule> Result) {
    public static RulesPlan Create(IReadOnlyList<KeyRule> saved, IReadOnlyList<KeyRule> wanted) {
        List<KeyRule> result = [];
        List<RuleWrite> write = [];
        foreach (KeyRule rule in wanted) {
            if (rule.RegistryName is not null) {
                result.Add(rule);
                continue;
            }

            string name = rule.RawSource.ToString("X8");
            write.Add(new RuleWrite(name, rule.RawSource, rule.RawTarget, rule.FnLockAware));
            result.Add(rule with {
                RegistryName = name
            });
        }

        HashSet<string> kept = new(result.Select(rule => rule.RegistryName!), StringComparer.OrdinalIgnoreCase);
        List<string> delete = [.. saved
            .Select(rule => rule.RegistryName)
            .OfType<string>()
            .Where(name => !kept.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        return new RulesPlan(delete, write, result);
    }
}

internal sealed class RegistryRulesStore : IRulesStore {
    private readonly RegistryKey root;
    private readonly string rulesPath;

    public RegistryRulesStore() : this(Registry.LocalMachine, DriverStatus.RulesKeyPath) {
    }

    // The root and path are parameters so that tests can use a scratch key of their own.
    public RegistryRulesStore(RegistryKey root, string rulesPath) {
        this.root = root;
        this.rulesPath = rulesPath;
    }

    public IReadOnlyList<KeyRule> Save(IReadOnlyList<KeyRule> saved, IReadOnlyList<KeyRule> wanted) {
        RulesPlan plan = RulesPlan.Create(saved, wanted);
        if (plan.Delete.Count == 0 && plan.Write.Count == 0) {
            return plan.Result;
        }

        using RegistryKey rulesKey = root.CreateSubKey(rulesPath, writable: true);
        foreach (string name in plan.Delete) {
            Delete(rulesKey, name);
        }
        foreach (RuleWrite rule in plan.Write) {
            using RegistryKey ruleKey = rulesKey.CreateSubKey(rule.Name, writable: true);
            ruleKey.SetValue("Source", unchecked((int)rule.Source), RegistryValueKind.DWord);
            ruleKey.SetValue("Target", unchecked((int)rule.Target), RegistryValueKind.DWord);
            if (rule.FnLockAware) {
                ruleKey.SetValue("FnLockAware", 1, RegistryValueKind.DWord);
            } else {
                ruleKey.DeleteValue("FnLockAware", throwOnMissingValue: false);
            }
        }
        return plan.Result;
    }

    // A subkey that is also a hotkey entry (the agent's VkKey/Path) keeps those values: only
    // its part as a driver rule is removed.
    private static void Delete(RegistryKey rulesKey, string name) {
        using (RegistryKey? ruleKey = rulesKey.OpenSubKey(name, writable: true)) {
            if (ruleKey is null) {
                return;
            }
            if (ruleKey.GetValue("VkKey") is not null || ruleKey.GetValue("Path") is not null) {
                foreach (string value in new[] { "Source", "Target", "FnLockAware" }) {
                    ruleKey.DeleteValue(value, throwOnMissingValue: false);
                }
                return;
            }
        }
        rulesKey.DeleteSubKey(name, throwOnMissingSubKey: false);
    }
}
