using Microsoft.Win32;

namespace _4MagicKeysPlus.Services;

internal static class DriverStatus {
    private const string ParametersKeyPath = @"SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters";

    public static bool ReadFnLockEnabled() {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(ParametersKeyPath);
        return key?.GetValue("FnLock") as int? == 1;
    }
}
