namespace _4MagicKeysPlus.Models;

// RegistryName is null while the entry is not saved; VirtualKey is a Win32 VK code, Modifiers are MOD_ALT/MOD_CONTROL/MOD_SHIFT/MOD_WIN bits.
// Order is the position in the list, saved with the entry because the registry lists subkeys by name.
public sealed record HotkeyEntry(string? RegistryName, string Name, string Path, uint VirtualKey, uint Modifiers, int? Order = null) {
    public bool SameCombination(uint virtualKey, uint modifiers) {
        return VirtualKey == virtualKey && Modifiers == modifiers;
    }
}
