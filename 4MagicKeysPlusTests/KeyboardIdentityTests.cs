using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusTests;

public class KeyboardIdentityTests {
    [Theory]
    [InlineData("C4141104498E")]
    [InlineData("c4141104498e")]
    [InlineData("C4:14:11:04:49:8E")]
    [InlineData("c4-14-11-04-49-8e")]
    public void ABluetoothAddress_IsNormalized(string address) {
        KeyboardIdentity? identity = KeyboardIdentity.FromBluetoothAddress(address);

        Assert.Equal(new KeyboardIdentity(KeyboardConnection.Bluetooth, "C4141104498E"), identity);
        Assert.Equal("bt:C4141104498E", identity!.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("C4141104498")]
    [InlineData("C4141104498EF")]
    [InlineData("C4141104498G")]
    public void NotABluetoothAddress_IsNoIdentity(string? address) {
        Assert.Null(KeyboardIdentity.FromBluetoothAddress(address));
    }

    [Fact]
    public void AUsbSerial_IsUppercased() {
        KeyboardIdentity? identity = KeyboardIdentity.FromUsbSerial(" f0t125100bwjkpgas ");

        Assert.Equal(new KeyboardIdentity(KeyboardConnection.Usb, "F0T125100BWJKPGAS"), identity);
        Assert.Equal("usb:F0T125100BWJKPGAS", identity!.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("5&1A2B3C4D&0&2")]
    [InlineData(@"USB\SERIAL")]
    public void NoUsbSerial_IsNoIdentity(string? serial) {
        Assert.Null(KeyboardIdentity.FromUsbSerial(serial));
    }

    [Theory]
    [InlineData("bt:C4141104498E")]
    [InlineData("usb:F0T125100BWJKPGAS")]
    public void TheTextForm_ParsesBackToTheSameIdentity(string text) {
        KeyboardIdentity? identity = KeyboardIdentity.Parse(text);

        Assert.NotNull(identity);
        Assert.Equal(text, identity.ToString());
    }

    [Fact]
    public void Parsing_IgnoresTheCaseOfThePrefixAndNormalizesTheValue() {
        Assert.Equal("bt:C4141104498E", KeyboardIdentity.Parse("BT:c4:14:11:04:49:8e")!.ToString());
        Assert.Equal("usb:F0T1", KeyboardIdentity.Parse("Usb:f0t1")!.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("C4141104498E")]
    [InlineData("ble:C4141104498E")]
    [InlineData("bt:")]
    [InlineData("bt:not-an-address")]
    [InlineData("usb:")]
    [InlineData("usb:5&1A2B&0&2")]
    public void Garbage_IsNotParsed(string? text) {
        Assert.Null(KeyboardIdentity.Parse(text));
    }

    [Fact]
    public void TheTwoTransportsOfOneKeyboardAreTwoIdentities() {
        Assert.NotEqual(KeyboardIdentity.FromBluetoothAddress("C4141104498E"), KeyboardIdentity.FromUsbSerial("C4141104498E"));
    }
}
