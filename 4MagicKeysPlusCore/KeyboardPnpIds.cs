using System.Globalization;
using System.Text.RegularExpressions;

namespace _4MagicKeysPlus.Core;

internal sealed record PnpKeyboard(KeyboardIdentity Identity, KeyboardProduct Product);

// Only two shapes of instance ID carry a keyboard's identity, and they are the ones the driver is attached to or
// right above: the USB device (USB\VID_05AC&PID_026C\<serial>, the parent of the interfaces the driver filters) and the
// paired Bluetooth HID service (BTHENUM\{00001124-...}_VID&0001004C_PID&026C\<...>&<address>_C00000000, which it filters itself).
internal static class KeyboardPnpIds {
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly Regex UsbDevice = new(@"^USB\\VID_05AC&PID_(?<product>[0-9A-F]{4})\\(?<serial>[^\\&]+)$", Options);
    private static readonly Regex BluetoothHidService = new(@"^BTHENUM\\\{00001124-0000-1000-8000-00805F9B34FB\}_VID&0001004C_PID&(?<product>[0-9A-F]{4})\\[^\\]*&(?<address>[0-9A-F]{12})_C[0-9A-F]{8}$", Options);

    public static PnpKeyboard? Parse(string instanceId) {
        Match usb = UsbDevice.Match(instanceId);
        if (usb.Success) {
            return Create(KeyboardIdentity.FromUsbSerial(usb.Groups["serial"].Value), usb.Groups["product"].Value);
        }

        Match bluetooth = BluetoothHidService.Match(instanceId);
        if (bluetooth.Success) {
            return Create(KeyboardIdentity.FromBluetoothAddress(bluetooth.Groups["address"].Value), bluetooth.Groups["product"].Value);
        }

        return null;
    }

    private static PnpKeyboard? Create(KeyboardIdentity? identity, string productHex) {
        ushort productId = ushort.Parse(productHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return identity is not null && KnownKeyboards.TryGetProduct(productId, out KeyboardProduct product) ? new PnpKeyboard(identity, product) : null;
    }
}
