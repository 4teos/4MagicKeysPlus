using Microsoft.Win32;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

[Collection(TestCollections.Registry)]
public sealed class DriverProbeTests : IDisposable {
    private const string DisplayName = "@oem157.inf,%HIDKbFlt.SvcDesc%;4MagicKeys+";
    private readonly ScratchKey scratch = new();
    private readonly DriverProbe probe;

    public DriverProbeTests() {
        probe = new DriverProbe(Registry.CurrentUser, scratch.Path("Service"), scratch.Path("Class"));
    }

    public void Dispose() {
        scratch.Dispose();
    }

    private void Service(string displayName) {
        using RegistryKey service = scratch.Create("Service");
        service.SetValue("DisplayName", displayName);
    }

    private void Device(string name, string infPath, string? version) {
        using RegistryKey device = scratch.Create($@"Class\{name}");
        device.SetValue("InfPath", infPath);
        if (version is not null) {
            device.SetValue("DriverVersion", version);
        }
    }

    [Fact]
    public void TheVersion_IsThatOfTheInfTheServiceWasInstalledFrom() {
        Service(DisplayName);
        Device("0105", "oem12.inf", "9.9.9.9");
        Device("0106", "oem157.inf", "2.2.0.34");

        Assert.Equal("2.2.0.34", probe.InstalledVersion());
    }

    [Fact]
    public void TheInfIsFoundWhateverTheCase() {
        Service("@OEM157.INF,%HIDKbFlt.SvcDesc%;4MagicKeys+");
        Device("0106", "oem157.inf", "2.2.0.34");

        Assert.Equal("2.2.0.34", probe.InstalledVersion());
    }

    [Fact]
    public void WhenNoDeviceUsesThatInf_ThereIsNoVersion() {
        Service(DisplayName);
        Device("0105", "oem12.inf", "9.9.9.9");

        Assert.Null(probe.InstalledVersion());
    }

    [Fact]
    public void ADeviceWithoutAVersion_GivesNone() {
        Service(DisplayName);
        Device("0106", "oem157.inf", version: null);

        Assert.Null(probe.InstalledVersion());
    }

    [Fact]
    public void AServiceThatIsNotFromAnInf_GivesNone() {
        Service("4MagicKeys+");
        Device("0106", "oem157.inf", "2.2.0.34");

        Assert.Null(probe.InstalledVersion());
    }

    [Fact]
    public void WithoutTheClassKey_ThereIsNoVersion() {
        Service(DisplayName);

        Assert.Null(probe.InstalledVersion());
    }

    [Fact]
    public void WithoutTheService_TheDriverIsNotInstalled_AndHasNoVersion() {
        Device("0106", "oem157.inf", "2.2.0.34");

        Assert.Null(probe.InstalledVersion());
        Assert.Equal(DriverState.NotInstalled, probe.Probe());
    }
}
