namespace _4MagicKeysPlus.Models;

// One rule of the driver's Parameters\Rules list. Source is a single key, a single Fn/Eject, or
// the modifiers of a modifier source; FnLayer is the "only with Fn" flag of a key source.
// The target is a key (TargetKey, or only TargetCode when that has no PhysicalKey), modifiers,
// or a consumer-control usage.
public sealed record KeyRule(
    IReadOnlySet<PhysicalKey> Source,
    bool FnLayer,
    PhysicalKey TargetKey,
    int TargetCode,
    IReadOnlySet<PhysicalKey> TargetModifiers,
    int? ConsumerUsage,
    bool FnLockAware) {

    // The rule's subkey under Parameters\Rules, when it was read from the registry. Saving has to
    // delete that very subkey, whatever it is called.
    public string? RegistryName {
        get; init;
    }

    public string DescribeTarget() {
        if (ConsumerUsage is int usage) {
            return ConsumerUsageNames.Get(usage);
        }

        List<string> parts = [.. TargetModifiers.Select(PhysicalKeyNames.GetDisplayName)];
        if (TargetKey != PhysicalKey.None) {
            parts.Add(PhysicalKeyNames.GetDisplayName(TargetKey));
        } else if (TargetCode != 0) {
            parts.Add($"0x{TargetCode:X2}");
        }
        return string.Join(" + ", parts);
    }
}

public sealed record DriverKeyRemapConfig(IReadOnlyList<KeyRule> Rules, bool FnLockEnabled) {
    public IReadOnlySet<PhysicalKey> GetRemappedKeys() {
        HashSet<PhysicalKey> keys = [];
        foreach (KeyRule rule in Rules) {
            keys.UnionWith(rule.Source);
        }
        return keys;
    }

    public bool HasRules(PhysicalKey key) {
        return Rules.Any(rule => rule.Source.Contains(key));
    }

    // Replaces the rule of the same source and layer, leaving the key's rules of the other layer alone.
    public DriverKeyRemapConfig WithRule(KeyRule rule) {
        List<KeyRule> rules = [.. Rules.Where(existing => existing.FnLayer != rule.FnLayer || !existing.Source.SetEquals(rule.Source)), rule];
        return this with { Rules = rules };
    }

    public DriverKeyRemapConfig WithoutRules(PhysicalKey key) {
        return this with { Rules = [.. Rules.Where(rule => !rule.Source.Contains(key))] };
    }

    // Gets tooltip text with the rule target for the key. A Fn-layer rule is marked "Fn: "
    // only while it really needs Fn held - a FnLockAware rule needs no Fn once FnLock is off.
    public IReadOnlyList<string> DescribeTargets(PhysicalKey key) {
        List<string> targets = [];
        foreach (KeyRule rule in Rules) {
            if (!rule.Source.Contains(key)) {
                continue;
            }

            string target = rule.DescribeTarget();
            targets.Add(NeedsFnHeld(rule) ? $"Fn: {target}" : target);
        }
        return targets;
    }

    private bool NeedsFnHeld(KeyRule rule) {
        return rule.FnLayer && !(rule.FnLockAware && !FnLockEnabled);
    }
}
