namespace _4MagicKeysPlus.Models;

// A hotkey that launches something, as the agent reads it from a subkey of Parameters\Rules.
// RegistryName is that subkey's name, null while the entry has not been saved. VirtualKey is
// a Win32 VK code and Modifiers are MOD_ALT/MOD_CONTROL/MOD_SHIFT/MOD_WIN bits. Order is the
// position in the list, saved with the entry because the registry lists subkeys by name.
public sealed record HotkeyEntry(string? RegistryName, string Name, string Path, uint VirtualKey, uint Modifiers, int? Order = null) {
    public bool SameCombination(uint virtualKey, uint modifiers) {
        return VirtualKey == virtualKey && Modifiers == modifiers;
    }
}
