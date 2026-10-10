using Microsoft.Win32;
using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlus.Services;

internal static class DriverStatus {
    private const string ParametersKeyPath = @"SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters";

    public static bool ReadFnLockEnabled() {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(ParametersKeyPath);
        return key?.GetValue("FnLock") as int? == 1;
    }

    public static DriverKeyRemapConfig ReadKeyRemapConfig() {
        using RegistryKey? regKey = Registry.LocalMachine.OpenSubKey(ParametersKeyPath);
        return new DriverKeyRemapConfig(
            KeyMap: DriverConfigParser.ParseKeyMap(regKey?.GetValue("KeyMap") as byte[]),
            ModMap: DriverConfigParser.ParseModMap(regKey?.GetValue("ModMap") as byte[]),
            SpecialModMap: DriverConfigParser.ParseSpecialModMap(regKey?.GetValue("SpecialModMap") as byte[]));
    }
}
