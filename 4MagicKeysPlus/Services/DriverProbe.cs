using System.Text.RegularExpressions;
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

    // The version of the installed driver package (the DriverVer of its .inf), or null.
    string? InstalledVersion();
}

internal sealed partial class DriverProbe : IDriverProbe {
    internal const string HidClassKeyPath = @"SYSTEM\CurrentControlSet\Control\Class\{745a17a0-74d3-11d0-b6fe-00a0c90f57da}";

    private readonly RegistryKey root;
    private readonly string serviceKeyPath;
    private readonly string classKeyPath;

    public DriverProbe() : this(Registry.LocalMachine, DriverStatus.ServiceKeyPath, HidClassKeyPath) {
    }

    // The root and paths are parameters so that tests can use a scratch key of their own.
    public DriverProbe(RegistryKey root, string serviceKeyPath, string classKeyPath) {
        this.root = root;
        this.serviceKeyPath = serviceKeyPath;
        this.classKeyPath = classKeyPath;
    }

    public DriverState Probe() {
        using RegistryKey? service = root.OpenSubKey(serviceKeyPath);
        if (service is null) {
            return DriverState.NotInstalled;
        }
        return DriverControl.DeviceExists() ? DriverState.Active : DriverState.Idle;
    }

    // Windows installs the service with a display name like "@oem157.inf,%HIDKbFlt.SvcDesc%;4MagicKeys+",
    // which names the installed .inf, and keeps the version of that .inf with every device it is installed on.
    public string? InstalledVersion() {
        using RegistryKey? service = root.OpenSubKey(serviceKeyPath);
        Match inf = InfInDisplayName().Match(service?.GetValue("DisplayName") as string ?? "");
        if (!inf.Success) {
            return null;
        }

        using RegistryKey? devices = root.OpenSubKey(classKeyPath);
        foreach (string name in devices?.GetSubKeyNames() ?? []) {
            using RegistryKey? device = devices!.OpenSubKey(name);
            if (string.Equals(device?.GetValue("InfPath") as string, inf.Groups[1].Value, StringComparison.OrdinalIgnoreCase) && device!.GetValue("DriverVersion") is string version) {
                return version;
            }
        }
        return null;
    }

    [GeneratedRegex(@"^@(oem\d+\.inf),", RegexOptions.IgnoreCase)]
    private static partial Regex InfInDisplayName();
}
