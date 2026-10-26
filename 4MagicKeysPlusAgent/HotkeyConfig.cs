using Microsoft.Win32;

namespace _4MagicKeysPlusAgent;

internal sealed record HotkeyEntry(int HotkeyId, string RegistryName, string Name, uint VirtualKey, uint Modifiers, string ExecutablePath);

// Reads the hotkey entries from HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Hotkeys.
// A subkey is a hotkey entry only if it has both
//   VkKey      REG_DWORD  Win32 virtual-key code
//   Path       REG_SZ     what to launch (executable, shortcut, ...)
// plus the optional
//   VkModifier REG_DWORD  Win32 MOD_ALT/MOD_CONTROL/MOD_SHIFT/MOD_WIN bits (default 0)
//   Comment    REG_SZ     label used in logs, the subkey name when absent
// This app has no notion of the driver's HID key codes - by the time a keypress
// reaches here it's already an ordinary Windows keystroke, so the config speaks
// plain Win32 VK codes too. No restriction on which VK/modifier combos are allowed here -
// that's a UI-level concern, not this loader's.
internal static class HotkeyConfig {
    private const string RegistryKeyPath = @"SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Hotkeys";

    private const uint AllowedModifiers = NativeMethods.MOD_ALT | NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_WIN;

    public static List<HotkeyEntry> Load() {
        RegistryKey? rootKey = Registry.LocalMachine.OpenSubKey(RegistryKeyPath);
        using IRegistryKeyReader? reader = rootKey is null ? null : new Win32RegistryKeyReader(rootKey);
        return Load(reader);
    }

    // The actual parsing/validation logic, decoupled from the real registry so it can be
    // unit-tested against a fake IRegistryKeyReader instead of touching HKLM.
    internal static List<HotkeyEntry> Load(IRegistryKeyReader? rootKey) {
        List<HotkeyEntry> entries = new List<HotkeyEntry>();
        if (rootKey is null) {
            Logger.Warn($@"Registry key HKLM\{RegistryKeyPath} does not exist, nothing to load.");
            return entries;
        }

        int nextId = 1;
        foreach (string entryName in rootKey.GetSubKeyNames()) {
            using IRegistryKeyReader? entryKey = rootKey.OpenSubKey(entryName);
            if (entryKey is null) {
                continue;
            }

            object? keyValue = entryKey.GetValue("VkKey");
            object? pathValue = entryKey.GetValue("Path");
            if (keyValue is null && pathValue is null) {
                continue;
            }

            if (pathValue is not string path || string.IsNullOrWhiteSpace(path)) {
                Logger.Warn($@"Hotkeys\{entryName} has no valid 'Path' string value, skipping.");
                continue;
            }

            if (keyValue is not int keyRaw) {
                Logger.Warn($@"Hotkeys\{entryName} has no valid 'VkKey' DWORD value, skipping.");
                continue;
            }

            uint vk = (uint)keyRaw;

            uint mod = entryKey.GetValue("VkModifier") is int modRaw ? (uint)modRaw : 0u;
            if ((mod & ~AllowedModifiers) != 0) {
                Logger.Warn($@"Hotkeys\{entryName} VkModifier 0x{mod:X} has unrecognized bits, ignoring them.");
                mod &= AllowedModifiers;
            }

            string name = entryKey.GetValue("Comment") is string comment && !string.IsNullOrWhiteSpace(comment) ? comment : entryName;
            entries.Add(new HotkeyEntry(nextId++, entryName, name, vk, mod, path));
        }

        return entries;
    }
}
