namespace _4MagicKeysPlus.Models;

public enum SpecialModMapDirection {
    KeyToModifiers,
    ModifiersToKey
}

public readonly record struct KeyMapEntry(PhysicalKey Source, PhysicalKey Target);

public readonly record struct ModMapEntry(IReadOnlySet<PhysicalKey> Source, IReadOnlySet<PhysicalKey> Target);

public readonly record struct SpecialModMapEntry(PhysicalKey Key, IReadOnlySet<PhysicalKey> Modifiers, SpecialModMapDirection Direction);

public sealed record DriverKeyRemapConfig(
    IReadOnlyList<KeyMapEntry> KeyMap,
    IReadOnlyList<ModMapEntry> ModMap,
    IReadOnlyList<SpecialModMapEntry> SpecialModMap) {

    public IReadOnlySet<PhysicalKey> GetRemappedKeys() {
        HashSet<PhysicalKey> keys = [];

        foreach (KeyMapEntry entry in KeyMap) {
            keys.Add(entry.Source);
        }
        foreach (ModMapEntry entry in ModMap) {
            keys.UnionWith(entry.Source);
        }
        foreach (SpecialModMapEntry entry in SpecialModMap) {
            if (entry.Direction == SpecialModMapDirection.KeyToModifiers) {
                keys.Add(entry.Key);
            } else {
                keys.UnionWith(entry.Modifiers);
            }            
        }
        return keys;
    }

    // Gets tooltip text with the map target key
    public IReadOnlyList<string> DescribeTargets(PhysicalKey key) {
        List<string> targets = [];
        foreach (KeyMapEntry entry in KeyMap) {
            if (entry.Source == key) {
                targets.Add(entry.Target.ToString());
            }
        }
        foreach (ModMapEntry entry in ModMap) {
            if (entry.Source.Contains(key)) {
                targets.Add(DescribeKeySet(entry.Target));
            }
        }
        foreach (SpecialModMapEntry entry in SpecialModMap) {
            if (entry.Direction == SpecialModMapDirection.KeyToModifiers && entry.Key == key) {
                targets.Add(DescribeKeySet(entry.Modifiers));
            }
            if (entry.Direction == SpecialModMapDirection.ModifiersToKey && entry.Modifiers.Contains(key)) {
                targets.Add(entry.Key.ToString());
            }
        }
        return targets;
    }

    private static string DescribeKeySet(IReadOnlySet<PhysicalKey> keys) {
        return string.Join(" + ", keys);
    }
}
