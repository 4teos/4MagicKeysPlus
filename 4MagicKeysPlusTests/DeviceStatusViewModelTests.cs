using Microsoft.Win32;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.ViewModels;
using _4MagicKeysPlus.Views;

namespace _4MagicKeysPlusTests;

public sealed class DeviceStatusViewModelTests {
    private const string Bluetooth = "bt:C4141104498E";
    private const string Usb = "usb:F0T125100BWJKPGAS";

    private readonly FakeProfileStore store = new();
    private readonly HashSet<Guid> fnLocked = [];
    private readonly List<DetectedKeyboard?> batteryReads = [];
    private List<KeyboardEndpoint> endpoints = [];
    private KeyboardBatteryStatus? battery = new(80, false);

    private (KeyboardSession Session, DeviceStatusViewModel Status) Open() {
        KeyboardSession session = new(store, () => endpoints, new FakeDriver());
        session.Refresh();
        DeviceStatusViewModel status = new(session, fnLocked.Contains, detected => {
            batteryReads.Add(detected);
            return detected is null ? null : battery;
        });
        return (session, status);
    }

    [Fact]
    public void WithoutAConnectedKeyboard_NothingIsDetected() {
        store.Profiles.Add(KeyboardTestData.Profile(Bluetooth));

        (_, DeviceStatusViewModel status) = Open();

        Assert.False(status.IsKeyboardDetected);
        Assert.Equal(Strings.DeviceNotDetected, status.DeviceName);
        Assert.Equal(Strings.DeviceNotDetected, status.ConnectionText);
        Assert.Equal("", status.ConnectionGlyph);
        Assert.Equal(BatteryState.Unknown, status.BatteryState);
        Assert.False(status.FnLockEnabled);
        Assert.Null(batteryReads.Single());
    }

    [Fact]
    public void TheSelectedKeyboard_IsShownByItsNameAndItsConnection() {
        store.Profiles.Add(KeyboardTestData.Profile(Bluetooth, Usb));
        endpoints = [KeyboardTestData.Endpoint(Usb)];

        (_, DeviceStatusViewModel status) = Open();

        Assert.True(status.IsKeyboardDetected);
        Assert.Equal(KeyboardTestData.Shown(), status.DeviceName);
        Assert.Equal(Glyphs.Usb, status.ConnectionGlyph);
        Assert.Equal(Strings.ConnectionUsb, status.ConnectionText);
        Assert.Contains(Strings.ConnectionUsb, status.StatusTooltip);
    }

    [Fact]
    public void ABluetoothKeyboard_IsShownAsSuch() {
        store.Profiles.Add(KeyboardTestData.Profile(Bluetooth, Usb));
        endpoints = [KeyboardTestData.Endpoint(Bluetooth)];

        (_, DeviceStatusViewModel status) = Open();

        Assert.Equal(Glyphs.Bluetooth, status.ConnectionGlyph);
        Assert.Equal(Strings.ConnectionBluetooth, status.ConnectionText);
    }

    [Fact]
    public void TheBatteryIsReadFromTheEndpointTheKeyboardIsConnectedOver() {
        store.Profiles.Add(KeyboardTestData.Profile(KeyboardProduct.A1644, Bluetooth, Usb));
        endpoints = [KeyboardTestData.Endpoint(Usb, product: KeyboardProduct.A1644)];

        Open();

        DetectedKeyboard read = Assert.IsType<DetectedKeyboard>(batteryReads.Single());
        Assert.Equal(KeyboardProduct.A1644, read.Product);
        Assert.Equal(KeyboardConnection.Usb, read.Connection);
        Assert.Equal(endpoints[0].DevicePath, read.DevicePath);
    }

    [Theory]
    [InlineData(10, false, BatteryState.Low)]
    [InlineData(20, false, BatteryState.Low)]
    [InlineData(21, false, BatteryState.Medium)]
    [InlineData(50, false, BatteryState.Medium)]
    [InlineData(51, false, BatteryState.Good)]
    [InlineData(100, true, BatteryState.Good)]
    public void TheBatteryLevel_IsShownAsItsState(int percent, bool charging, BatteryState expected) {
        store.Profiles.Add(KeyboardTestData.Profile(Bluetooth));
        endpoints = [KeyboardTestData.Endpoint(Bluetooth)];
        battery = new KeyboardBatteryStatus(percent, charging);

        (_, DeviceStatusViewModel status) = Open();

        Assert.Equal(expected, status.BatteryState);
        Assert.Equal(Glyphs.BatteryLevel(percent, charging), status.BatteryGlyph);
        Assert.Contains($"{percent}%", status.StatusTooltip);
    }

    [Fact]
    public void ABatteryThatCannotBeRead_IsUnknown() {
        store.Profiles.Add(KeyboardTestData.Profile(Bluetooth));
        endpoints = [KeyboardTestData.Endpoint(Bluetooth)];
        battery = null;

        (_, DeviceStatusViewModel status) = Open();

        Assert.Equal(BatteryState.Unknown, status.BatteryState);
        Assert.Contains("—", status.StatusTooltip);
    }

    [Fact]
    public void FnLockIsThatOfTheProfileOfTheKeyboard() {
        KeyboardProfile desk = KeyboardTestData.Profile("bt:AAAAAAAAAAAA");
        KeyboardProfile office = KeyboardTestData.Profile(2, "bt:BBBBBBBBBBBB");
        store.Profiles.AddRange([desk, office]);
        fnLocked.Add(office.Id);
        endpoints = [KeyboardTestData.Endpoint("bt:AAAAAAAAAAAA"), KeyboardTestData.Endpoint("bt:BBBBBBBBBBBB")];
        (KeyboardSession session, DeviceStatusViewModel status) = Open();
        Assert.False(status.FnLockEnabled);

        session.Select(office.Id);

        Assert.True(status.FnLockEnabled);
        Assert.Equal(KeyboardTestData.Shown(2), status.DeviceName);
        Assert.Contains(Strings.FnLockEnabled, status.StatusTooltip);
    }

    [Fact]
    public void ARefresh_PicksUpAChangeOfFnLock() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth);
        store.Profiles.Add(desk);
        endpoints = [KeyboardTestData.Endpoint(Bluetooth)];
        (_, DeviceStatusViewModel status) = Open();

        fnLocked.Add(desk.Id);
        status.Refresh();

        Assert.True(status.FnLockEnabled);
    }

    [Fact]
    public void TheStatusFollowsTheKeyboardsThatComeAndGo() {
        store.Profiles.Add(KeyboardTestData.Profile(Bluetooth));
        (KeyboardSession session, DeviceStatusViewModel status) = Open();
        List<string?> changed = [];
        status.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.False(status.IsKeyboardDetected);

        endpoints = [KeyboardTestData.Endpoint(Bluetooth)];
        session.Refresh();

        Assert.True(status.IsKeyboardDetected);
        Assert.Equal(KeyboardTestData.Shown(), status.DeviceName);
        Assert.Contains(nameof(DeviceStatusViewModel.IsKeyboardDetected), changed);
        Assert.Contains(nameof(DeviceStatusViewModel.DeviceName), changed);
    }
}

[Collection(TestCollections.Registry)]
public sealed class DriverStatusTests : IDisposable {
    private readonly ScratchKey scratch = new();

    public void Dispose() {
        scratch.Dispose();
    }

    [Fact]
    public void TheKeyOfAProfile_IsUnderKeyboardsByItsGuidInBraces() {
        Guid id = Guid.Parse("6f1d2c3a-8b4e-4f70-9a21-5c3e7d8b0a11");

        Assert.Equal(@"SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Keyboards\{6F1D2C3A-8B4E-4F70-9A21-5C3E7D8B0A11}", DriverStatus.ProfileKeyPath(id));
        Assert.Equal(@$"{DriverStatus.ProfileKeyPath(id)}\Rules", DriverStatus.ProfileRulesKeyPath(id));
    }

    [Fact]
    public void WithoutTheKey_ThereAreNoRules() {
        Assert.Empty(DriverStatus.ReadRules(Registry.CurrentUser, scratch.Path("Rules")));
    }

    [Fact]
    public void TheRules_AreReadWithTheNamesOfTheirSubkeys() {
        string rulesPath = scratch.Path("Rules");
        using (RegistryKey rule = Registry.CurrentUser.CreateSubKey($@"{rulesPath}\01000041")) {
            rule.SetValue("Source", 0x01000041, RegistryValueKind.DWord);
            rule.SetValue("Target", 0x010000CD, RegistryValueKind.DWord);
            rule.SetValue("FnLockAware", 1, RegistryValueKind.DWord);
        }
        using (RegistryKey rule = Registry.CurrentUser.CreateSubKey($@"{rulesPath}\00040000")) {
            rule.SetValue("Source", 0x00040000, RegistryValueKind.DWord);
            rule.SetValue("Target", 0x00080000, RegistryValueKind.DWord);
        }

        List<KeyRule> rules = DriverStatus.ReadRules(Registry.CurrentUser, rulesPath);

        Assert.Equal(["00040000", "01000041"], rules.Select(rule => rule.RegistryName).Order());
        Assert.True(rules.Single(rule => rule.RegistryName == "01000041").FnLockAware);
        Assert.False(rules.Single(rule => rule.RegistryName == "00040000").FnLockAware);
    }

    [Fact]
    public void ASubkeyThatIsNoRule_IsSkipped() {
        string rulesPath = scratch.Path("Rules");
        using (RegistryKey noTarget = Registry.CurrentUser.CreateSubKey($@"{rulesPath}\A")) {
            noTarget.SetValue("Source", 0x01000041, RegistryValueKind.DWord);
        }
        using (RegistryKey wrongType = Registry.CurrentUser.CreateSubKey($@"{rulesPath}\B")) {
            wrongType.SetValue("Source", "text");
            wrongType.SetValue("Target", 1, RegistryValueKind.DWord);
        }

        Assert.Empty(DriverStatus.ReadRules(Registry.CurrentUser, rulesPath));
    }
}
