using Microsoft.Win32;

namespace _4MagicKeysPlusAppLauncher;

internal sealed record AppLaunchEntry(int HotkeyId, string Name, uint VirtualKey, uint Modifiers, string ExecutablePath);

// Reads HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\AppLaunchMap.
// One subkey per app (named after the exe, e.g. "Notepad" - purely a label, never
// parsed), each with:
//   Key      REG_DWORD  Win32 virtual-key code
//   Modifier REG_DWORD  Win32 MOD_ALT/MOD_CONTROL/MOD_SHIFT/MOD_WIN bits, optional (default 0)
//   Path     REG_SZ     full path to the executable to launch
// This app has no notion of the driver's HID key codes - by the time a keypress
// reaches here it's already an ordinary Windows keystroke, so the config speaks
// plain Win32 VK codes too. No restriction on which VK/modifier combos are allowed here -
// that's a UI-level concern, not this loader's.
internal static class AppLaunchConfig {
    private const string RegistryKeyPath = @"SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\AppLaunchMap";

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
        foreach (string appName in rootKey.GetSubKeyNames()) {
            using IRegistryKeyReader? appKey = rootKey.OpenSubKey(appName);
            if (appKey is null) {
                continue;
            }

            if (appKey.GetValue("Path") is not string path || string.IsNullOrWhiteSpace(path)) {
                Logger.Warn($@"AppLaunchMap\{appName} has no valid 'Path' string value, skipping.");
                continue;
            }

            if (appKey.GetValue("Key") is not int keyRaw) {
                Logger.Warn($@"AppLaunchMap\{appName} has no valid 'Key' DWORD value, skipping.");
                continue;
            }

            uint vk = (uint)keyRaw;

            uint mod = appKey.GetValue("Modifier") is int modRaw ? (uint)modRaw : 0u;
            if ((mod & ~AllowedModifiers) != 0) {
                Logger.Warn($@"AppLaunchMap\{appName} Modifier 0x{mod:X} has unrecognized bits, ignoring them.");
                mod &= AllowedModifiers;
            }

            entries.Add(new AppLaunchEntry(nextId++, appName, vk, mod, path));
        }

        return entries;
    }
}
