namespace _4MagicKeysPlus.Core;

// What makes a keyboard the same physical keyboard from one connection to the next: a Bluetooth keyboard is its
// address, a USB one its serial number (Windows puts it into the instance ID of the USB device and the HID serial
// string). The text form, bt:AABBCCDDEEFF or usb:AABBCCDDEEFFGGHHJ, is what the registry holds; the driver builds the
// same strings. The USB cable and the Bluetooth pairing of one keyboard have nothing in common, so they are two identities.
public sealed record KeyboardIdentity(KeyboardConnection Connection, string Id) {
    // MKP_IDENTITY_MAX_CHARS of the driver (64) less "usb:" and the terminator.
    private const int MaxUsbSerialLength = 59;

    public static KeyboardIdentity? FromBluetoothAddress(string? address) {
        string id = (address ?? "").Replace(":", "").Replace("-", "").ToUpperInvariant();
        return id.Length == 12 && id.All(char.IsAsciiHexDigit) ? new KeyboardIdentity(KeyboardConnection.Bluetooth, id) : null;
    }

    // Windows makes up the ID of a USB device without a serial number from its port (it has '&' in it), so
    // that one would change with the port and is no identity. A serial the driver's identity buffer cannot
    // hold would never match there, so it is no identity either.
    public static KeyboardIdentity? FromUsbSerial(string? serial) {
        string id = (serial ?? "").Trim().ToUpperInvariant();
        return id.Length is 0 or > MaxUsbSerialLength || id.Contains('&') || id.Contains('\\') ? null : new KeyboardIdentity(KeyboardConnection.Usb, id);
    }

    public static KeyboardIdentity? Parse(string? text) {
        int colon = text?.IndexOf(':') ?? -1;
        if (text is null || colon < 0) {
            return null;
        }

        string value = text[(colon + 1)..];
        return text[..colon].ToLowerInvariant() switch {
            "bt" => FromBluetoothAddress(value),
            "usb" => FromUsbSerial(value),
            _ => null
        };
    }

    public override string ToString() {
        return $"{(Connection == KeyboardConnection.Bluetooth ? "bt" : "usb")}:{Id}";
    }
}
