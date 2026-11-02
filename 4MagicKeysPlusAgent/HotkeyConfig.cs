using Microsoft.Win32;

namespace _4MagicKeysPlusAgent;

internal sealed record HotkeyEntry(int HotkeyId, string RegistryName, string Name, uint VirtualKey, uint Modifiers, string ExecutablePath);

// Hotkeys speak plain Win32 VK codes, not the driver's HID codes: by the time a keypress reaches the agent it is an ordinary Windows keystroke.
internal static class HotkeyConfig {
    private const string RegistryKeyPath = @"SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Hotkeys";

    private const uint AllowedModifiers = NativeMethods.MOD_ALT | NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_WIN;

    public static List<HotkeyEntry> Load() {
        RegistryKey? rootKey = Registry.LocalMachine.OpenSubKey(RegistryKeyPath);
        using IRegistryKeyReader? reader = rootKey is null ? null : new Win32RegistryKeyReader(rootKey);
        return Load(reader);
    }

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
