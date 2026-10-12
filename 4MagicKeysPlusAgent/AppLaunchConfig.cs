using Microsoft.Win32;

namespace _4MagicKeysPlusAgent;

internal sealed record AppLaunchEntry(int HotkeyId, string Name, uint VirtualKey, uint Modifiers, string ExecutablePath);

// Reads the launch entries from HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Rules.
// The same subkeys also hold the driver's key rules (Source/Target), which are not this
// loader's business: a subkey is a launch entry only if it has both
//   VkKey      REG_DWORD  Win32 virtual-key code
//   Path       REG_SZ     what to launch (executable, shortcut, ...)
// plus the optional
//   VkModifier REG_DWORD  Win32 MOD_ALT/MOD_CONTROL/MOD_SHIFT/MOD_WIN bits (default 0)
//   Comment    REG_SZ     label used in logs, the subkey name when absent
// This app has no notion of the driver's HID key codes - by the time a keypress
// reaches here it's already an ordinary Windows keystroke, so the config speaks
// plain Win32 VK codes too. No restriction on which VK/modifier combos are allowed here -
// that's a UI-level concern, not this loader's.
internal static class AppLaunchConfig {
    private const string RegistryKeyPath = @"SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Rules";

    private const uint AllowedModifiers = NativeMethods.MOD_ALT | NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_WIN;

    public static List<AppLaunchEntry> Load() {
        RegistryKey? rootKey = Registry.LocalMachine.OpenSubKey(RegistryKeyPath);
        using IRegistryKeyReader? reader = rootKey is null ? null : new Win32RegistryKeyReader(rootKey);
        return Load(reader);
    }

    // The actual parsing/validation logic, decoupled from the real registry so it can be
    // unit-tested against a fake IRegistryKeyReader instead of touching HKLM.
    internal static List<AppLaunchEntry> Load(IRegistryKeyReader? rootKey) {
        List<AppLaunchEntry> entries = new List<AppLaunchEntry>();
        if (rootKey is null) {
            Logger.Warn($@"Registry key HKLM\{RegistryKeyPath} does not exist, nothing to load.");
            return entries;
        }

        int nextId = 1;
        foreach (string ruleName in rootKey.GetSubKeyNames()) {
            using IRegistryKeyReader? ruleKey = rootKey.OpenSubKey(ruleName);
            if (ruleKey is null) {
                continue;
            }

            object? keyValue = ruleKey.GetValue("VkKey");
            object? pathValue = ruleKey.GetValue("Path");
            if (keyValue is null && pathValue is null) {
                continue;
            }

            if (pathValue is not string path || string.IsNullOrWhiteSpace(path)) {
                Logger.Warn($@"Rules\{ruleName} has no valid 'Path' string value, skipping.");
                continue;
            }

            if (keyValue is not int keyRaw) {
                Logger.Warn($@"Rules\{ruleName} has no valid 'VkKey' DWORD value, skipping.");
                continue;
            }

            uint vk = (uint)keyRaw;

            uint mod = ruleKey.GetValue("VkModifier") is int modRaw ? (uint)modRaw : 0u;
            if ((mod & ~AllowedModifiers) != 0) {
                Logger.Warn($@"Rules\{ruleName} VkModifier 0x{mod:X} has unrecognized bits, ignoring them.");
                mod &= AllowedModifiers;
            }

            string name = ruleKey.GetValue("Comment") is string comment && !string.IsNullOrWhiteSpace(comment) ? comment : ruleName;
            entries.Add(new AppLaunchEntry(nextId++, name, vk, mod, path));
        }

        return entries;
    }
}
