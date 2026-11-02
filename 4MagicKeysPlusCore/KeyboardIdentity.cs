namespace _4MagicKeysPlus.Core;

// bt:AABBCCDDEEFF (the address) or usb:<serial>: the text the registry holds, built the same way by the driver.
// The cable and the pairing of one keyboard have nothing in common, so they are two identities.
public sealed record KeyboardIdentity(KeyboardConnection Connection, string Id) {
    // MKP_IDENTITY_MAX_CHARS of the driver (64) less "usb:" and the terminator.
    private const int MaxUsbSerialLength = 59;

    public static KeyboardIdentity? FromBluetoothAddress(string? address) {
        string id = (address ?? "").Replace(":", "").Replace("-", "").ToUpperInvariant();
        return id.Length == 12 && id.All(char.IsAsciiHexDigit) ? new KeyboardIdentity(KeyboardConnection.Bluetooth, id) : null;
    }

    // A USB device without a serial number gets an ID made of its port (it has '&' in it), which changes with the port: no identity.
    // Neither is a serial that the driver's identity buffer cannot hold: it would never match there.
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
