using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusTests;

public class KeyboardPnpIdsTests {
    // The instance IDs are the ones of the A1843 on the development machine.
    private const string BluetoothHid = @"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&026C\D&15B70467&0&C4141104498E_C00000000";
    private const string UsbDevice = @"USB\VID_05AC&PID_026C\F0T125100BWJKPGAS";

    [Fact]
    public void ThePairedBluetoothHidService_IsTheBluetoothIdentity() {
        PnpKeyboard? keyboard = KeyboardPnpIds.Parse(BluetoothHid);

        Assert.NotNull(keyboard);
        Assert.Equal("bt:C4141104498E", keyboard.Identity.ToString());
        Assert.Equal(KeyboardProduct.A1843, keyboard.Product);
    }

    [Fact]
    public void TheCaseOfTheInstanceIdDoesNotMatter() {
        PnpKeyboard? keyboard = KeyboardPnpIds.Parse(@"bthenum\{00001124-0000-1000-8000-00805f9b34fb}_vid&0001004c_pid&0267\d&15b70467&0&c4141104498e_c00000000");

        Assert.NotNull(keyboard);
        Assert.Equal("bt:C4141104498E", keyboard.Identity.ToString());
        Assert.Equal(KeyboardProduct.A1644, keyboard.Product);
    }

    [Fact]
    public void TheUsbDevice_IsTheUsbIdentity() {
        PnpKeyboard? keyboard = KeyboardPnpIds.Parse(UsbDevice);

        Assert.NotNull(keyboard);
        Assert.Equal("usb:F0T125100BWJKPGAS", keyboard.Identity.ToString());
        Assert.Equal(KeyboardProduct.A1843, keyboard.Product);
    }

    [Theory]
    [InlineData(@"BTHENUM\{00001200-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&026C\D&15B70467&0&C4141104498E_C00000000")]
    [InlineData(@"BTHENUM\DEV_C4141104498E\D&15B70467&0&BLUETOOTHDEVICE_C4141104498E")]
    [InlineData(@"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&0250\D&15B70467&0&C4141104498E_C00000000")]
    [InlineData(@"USB\VID_05AC&PID_026C&MI_01\9&286A66C3&1&0001")]
    [InlineData(@"USB\VID_05AC&PID_026C\5&1A2B3C4D&0&2")]
    [InlineData(@"USB\VID_05AC&PID_0250\F0T125100BWJKPGAS")]
    [InlineData(@"USB\VID_046D&PID_C52B\5&2A8F1B2&0&3")]
    [InlineData(@"HID\VID_05AC&PID_026C&MI_00&COL02\A&27E58948&0&0001")]
    [InlineData("")]
    public void EverythingElse_IsNotAKeyboardIdentity(string instanceId) {
        Assert.Null(KeyboardPnpIds.Parse(instanceId));
    }
}

public class KeyboardEnumeratorTests {
    private const string BluetoothHid = @"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&026C\D&15B70467&0&C4141104498E_C00000000";
    private const string BluetoothService = @"BTHENUM\{00001200-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&026C\D&15B70467&0&C4141104498E_C00000000";
    private const string BluetoothDevice = @"BTHENUM\DEV_C4141104498E\D&15B70467&0&BLUETOOTHDEVICE_C4141104498E";
    private const string UsbDevice = @"USB\VID_05AC&PID_026C\F0T125100BWJKPGAS";
    private const string UsbInterface = @"USB\VID_05AC&PID_026C&MI_01\9&286A66C3&1&0001";

    private static KeyboardIdentity Bluetooth(string address) {
        return KeyboardIdentity.FromBluetoothAddress(address)!;
    }

    private static KeyboardIdentity Usb(string serial) {
        return KeyboardIdentity.FromUsbSerial(serial)!;
    }

    private static LiveKeyboardInterface Live(KeyboardIdentity? identity, string path, KeyboardProduct product = KeyboardProduct.A1843) {
        return new LiveKeyboardInterface(product, identity?.Connection ?? KeyboardConnection.Usb, path, identity);
    }

    [Fact]
    public void ABluetoothPairingWithItsLinkDown_IsKnownButNotLive() {
        IReadOnlyList<KeyboardEndpoint> endpoints = KeyboardEnumerator.Combine([BluetoothService, BluetoothHid, BluetoothDevice], []);

        KeyboardEndpoint endpoint = Assert.Single(endpoints);
        Assert.Equal(Bluetooth("C4141104498E"), endpoint.Identity);
        Assert.Equal(KeyboardProduct.A1843, endpoint.Product);
        Assert.False(endpoint.IsLive);
        Assert.Null(endpoint.DevicePath);
    }

    [Fact]
    public void APluggedInUsbKeyboard_IsLive() {
        IReadOnlyList<KeyboardEndpoint> endpoints = KeyboardEnumerator.Combine([UsbInterface, UsbDevice], []);

        KeyboardEndpoint endpoint = Assert.Single(endpoints);
        Assert.Equal(Usb("F0T125100BWJKPGAS"), endpoint.Identity);
        Assert.True(endpoint.IsLive);
    }

    [Fact]
    public void ALiveBluetoothInterface_MakesThePairingLiveAndGivesItsDevicePath() {
        KeyboardIdentity identity = Bluetooth("C4141104498E");

        IReadOnlyList<KeyboardEndpoint> endpoints = KeyboardEnumerator.Combine([BluetoothHid], [Live(identity, @"\\?\hid#bt")]);

        KeyboardEndpoint endpoint = Assert.Single(endpoints);
        Assert.True(endpoint.IsLive);
        Assert.Equal(@"\\?\hid#bt", endpoint.DevicePath);
    }

    [Fact]
    public void OneKeyboardOnBothTransports_IsTwoEndpoints_OnlyTheOneInUseIsLive() {
        IReadOnlyList<KeyboardEndpoint> endpoints = KeyboardEnumerator.Combine(
            [BluetoothHid, UsbDevice],
            [Live(Usb("F0T125100BWJKPGAS"), @"\\?\hid#usb")]);

        Assert.Equal(2, endpoints.Count);
        KeyboardEndpoint bluetooth = endpoints.Single(endpoint => endpoint.Identity.Connection == KeyboardConnection.Bluetooth);
        KeyboardEndpoint usb = endpoints.Single(endpoint => endpoint.Identity.Connection == KeyboardConnection.Usb);
        Assert.False(bluetooth.IsLive);
        Assert.True(usb.IsLive);
        Assert.Equal(@"\\?\hid#usb", usb.DevicePath);
    }

    [Fact]
    public void ALiveInterfaceThatNoDeviceIdExplains_IsAddedAnyway() {
        IReadOnlyList<KeyboardEndpoint> endpoints = KeyboardEnumerator.Combine([], [Live(Bluetooth("C4141104498E"), @"\\?\hid#bt", KeyboardProduct.A1644)]);

        KeyboardEndpoint endpoint = Assert.Single(endpoints);
        Assert.Equal(KeyboardProduct.A1644, endpoint.Product);
        Assert.True(endpoint.IsLive);
    }

    [Fact]
    public void ALiveInterfaceWithoutAnIdentity_IsLeftOut() {
        IReadOnlyList<KeyboardEndpoint> endpoints = KeyboardEnumerator.Combine([UsbDevice], [Live(null, @"\\?\hid#nameless")]);

        KeyboardEndpoint endpoint = Assert.Single(endpoints);
        Assert.Null(endpoint.DevicePath);
    }

    [Fact]
    public void TheFirstLiveInterfaceOfAKeyboard_KeepsItsDevicePath() {
        KeyboardIdentity identity = Usb("F0T125100BWJKPGAS");

        IReadOnlyList<KeyboardEndpoint> endpoints = KeyboardEnumerator.Combine([UsbDevice], [Live(identity, @"\\?\hid#first"), Live(identity, @"\\?\hid#second")]);

        Assert.Equal(@"\\?\hid#first", Assert.Single(endpoints).DevicePath);
    }

    [Fact]
    public void TheSameDeviceIdTwice_IsOneEndpoint() {
        IReadOnlyList<KeyboardEndpoint> endpoints = KeyboardEnumerator.Combine([BluetoothHid, BluetoothHid.ToLowerInvariant()], []);

        Assert.Single(endpoints);
    }

    [Fact]
    public void TwoKeyboardsOfOneModel_AreTwoEndpoints_InAStableOrder() {
        string other = BluetoothHid.Replace("C4141104498E", "AABBCCDDEEFF");

        IReadOnlyList<KeyboardEndpoint> endpoints = KeyboardEnumerator.Combine([BluetoothHid, other], []);

        Assert.Equal(["bt:AABBCCDDEEFF", "bt:C4141104498E"], endpoints.Select(endpoint => endpoint.Identity.ToString()));
    }

    [Fact]
    public void TheOrder_IsModelThenTransportThenId() {
        string a1644 = UsbDevice.Replace("PID_026C", "PID_0267").Replace("F0T125100BWJKPGAS", "ZZZ");

        IReadOnlyList<KeyboardEndpoint> endpoints = KeyboardEnumerator.Combine([UsbDevice, BluetoothHid, a1644], []);

        Assert.Equal(["usb:ZZZ", "usb:F0T125100BWJKPGAS", "bt:C4141104498E"], endpoints.Select(endpoint => endpoint.Identity.ToString()));
    }
}
