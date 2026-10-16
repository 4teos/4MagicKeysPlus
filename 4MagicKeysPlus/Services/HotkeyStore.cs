using Microsoft.Win32;
using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlus.Services;

// Where the hotkeys are saved. Save gets the entries as last saved and the entries wanted now,
// and returns the latter with the subkey name each one has been saved under.
internal interface IHotkeyStore {
    IReadOnlyList<HotkeyEntry> Load();

    IReadOnlyList<HotkeyEntry> Save(IReadOnlyList<HotkeyEntry> saved, IReadOnlyList<HotkeyEntry> wanted);
}

// What saving changes under Parameters\Rules, worked out without touching the registry. An entry
// keeps its subkey (whatever it is called) as long as its key combination stays the same, so a
// new name or path is written in place. A new entry, or one whose combination changed, goes
// under Hotkey_<modifiers>_<key>, and the subkey it left is released.
internal sealed record HotkeysPlan(IReadOnlyList<string> Release, IReadOnlyList<HotkeyEntry> Write, IReadOnlyList<HotkeyEntry> Result) {
    public static HotkeysPlan Create(IReadOnlyList<HotkeyEntry> saved, IReadOnlyList<HotkeyEntry> wanted) {
        Dictionary<string, HotkeyEntry> savedByName = [];
        foreach (HotkeyEntry entry in saved.Where(entry => entry.RegistryName is not null)) {
            savedByName[entry.RegistryName!] = entry;
        }
        HashSet<string> taken = new(StringComparer.OrdinalIgnoreCase);
        foreach (HotkeyEntry entry in wanted) {
            if (KeepsItsSubkey(entry, savedByName)) {
                taken.Add(entry.RegistryName!);
            }
        }

        List<HotkeyEntry> result = [];
        List<HotkeyEntry> write = [];
        foreach (HotkeyEntry entry in wanted) {
            if (KeepsItsSubkey(entry, savedByName)) {
                result.Add(entry);
                if (savedByName[entry.RegistryName!] != entry) {
                    write.Add(entry);
                }
                continue;
            }

            HotkeyEntry placed = entry with { RegistryName = FreeName(entry, taken) };
            taken.Add(placed.RegistryName!);
            result.Add(placed);
            write.Add(placed);
        }

        List<string> release = [.. saved
            .Select(entry => entry.RegistryName)
            .OfType<string>()
            .Where(name => !taken.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        return new HotkeysPlan(release, write, result);
    }

    private static bool KeepsItsSubkey(HotkeyEntry entry, Dictionary<string, HotkeyEntry> savedByName) {
        return entry.RegistryName is not null
            && savedByName.TryGetValue(entry.RegistryName, out HotkeyEntry? before)
            && before.SameCombination(entry.VirtualKey, entry.Modifiers);
    }

    private static string FreeName(HotkeyEntry entry, HashSet<string> taken) {
        string name = $"Hotkey_{entry.Modifiers:X2}_{entry.VirtualKey:X2}";
        string candidate = name;
        for (int suffix = 2; taken.Contains(candidate); suffix++) {
            candidate = $"{name}_{suffix}";
        }
        return candidate;
    }
}

internal sealed class RegistryHotkeyStore : IHotkeyStore {
    private const uint AllowedModifiers = 0x000F;

    private readonly RegistryKey root;
    private readonly string rulesPath;

    public RegistryHotkeyStore() : this(Registry.LocalMachine, DriverStatus.RulesKeyPath) {
    }

    // The root and path are parameters so that tests can use a scratch key of their own.
    public RegistryHotkeyStore(RegistryKey root, string rulesPath) {
        this.root = root;
        this.rulesPath = rulesPath;
    }

    // A subkey is a hotkey if it has a VkKey DWORD and a non-empty Path, like the agent reads it.
    public IReadOnlyList<HotkeyEntry> Load() {
        List<HotkeyEntry> entries = [];
        using RegistryKey? rulesKey = root.OpenSubKey(rulesPath);
        if (rulesKey is null) {
            return entries;
        }

        foreach (string name in rulesKey.GetSubKeyNames()) {
            using RegistryKey? key = rulesKey.OpenSubKey(name);
            if (key?.GetValue("VkKey") is not int virtualKey || key.GetValue("Path") is not string path || string.IsNullOrWhiteSpace(path)) {
                continue;
            }

            uint modifiers = key.GetValue("VkModifier") is int raw ? (uint)raw & AllowedModifiers : 0;
            string label = key.GetValue("Comment") is string comment && !string.IsNullOrWhiteSpace(comment) ? comment : HotkeyNames.FromPath(path);
            entries.Add(new HotkeyEntry(name, label, path, (uint)virtualKey, modifiers));
        }
        return entries;
    }

    public IReadOnlyList<HotkeyEntry> Save(IReadOnlyList<HotkeyEntry> saved, IReadOnlyList<HotkeyEntry> wanted) {
        HotkeysPlan plan = HotkeysPlan.Create(saved, wanted);
        if (plan.Release.Count == 0 && plan.Write.Count == 0) {
            return plan.Result;
        }

        using RegistryKey rulesKey = root.CreateSubKey(rulesPath, writable: true);
        foreach (string name in plan.Release) {
            Release(rulesKey, name);
        }
        foreach (HotkeyEntry entry in plan.Write) {
            using RegistryKey key = rulesKey.CreateSubKey(entry.RegistryName!, writable: true);
            key.SetValue("VkKey", unchecked((int)entry.VirtualKey), RegistryValueKind.DWord);
            if (entry.Modifiers != 0) {
                key.SetValue("VkModifier", unchecked((int)entry.Modifiers), RegistryValueKind.DWord);
            } else {
                key.DeleteValue("VkModifier", throwOnMissingValue: false);
            }
            key.SetValue("Path", entry.Path, RegistryValueKind.String);
            key.SetValue("Comment", entry.Name, RegistryValueKind.String);
        }
        return plan.Result;
    }

    // A subkey that is also a driver rule (Source/Target) keeps those values: only its part as
    // a hotkey is removed.
    private static void Release(RegistryKey rulesKey, string name) {
        using (RegistryKey? key = rulesKey.OpenSubKey(name, writable: true)) {
            if (key is null) {
                return;
            }
            if (key.GetValue("Source") is not null || key.GetValue("Target") is not null) {
                foreach (string value in new[] { "VkKey", "VkModifier", "Path" }) {
                    key.DeleteValue(value, throwOnMissingValue: false);
                }
                return;
            }
        }
        rulesKey.DeleteSubKey(name, throwOnMissingSubKey: false);
    }
}
