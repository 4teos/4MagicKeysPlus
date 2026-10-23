using Microsoft.Win32;

namespace _4MagicKeysPlus.Services;

public enum DriverState {
    NotInstalled,
    // Installed, but no keyboard is filtered by it at the moment.
    Idle,
    Active,
}

internal interface IDriverProbe {
    DriverState Probe();
}

internal sealed class DriverProbe : IDriverProbe {
    public DriverState Probe() {
        using RegistryKey? service = Registry.LocalMachine.OpenSubKey(DriverStatus.ServiceKeyPath);
        if (service is null) {
            return DriverState.NotInstalled;
        }
        return DriverControl.DeviceExists() ? DriverState.Active : DriverState.Idle;
    }
}
