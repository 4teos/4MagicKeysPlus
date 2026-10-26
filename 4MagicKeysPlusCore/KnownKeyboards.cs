namespace _4MagicKeysPlus.Core;

// The IDs 4MagicKeysPlusDriver\4MagicKeysPlus.inf matches on: Apple's USB-IF vendor ID for the cable, its Bluetooth
// SIG company ID for the Bluetooth pairing, and the product ID of each model.
internal static class KnownKeyboards {
    public static bool TryGetConnection(ushort vendorId, out KeyboardConnection connection) {
        switch (vendorId) {
            case 0x05AC:
                connection = KeyboardConnection.Usb;
                return true;
            case 0x004C:
                connection = KeyboardConnection.Bluetooth;
                return true;
            default:
                connection = default;
                return false;
        }
    }

    public static bool TryGetProduct(ushort productId, out KeyboardProduct product) {
        switch (productId) {
            case 0x0267:
                product = KeyboardProduct.A1644;
                return true;
            case 0x026C:
                product = KeyboardProduct.A1843;
                return true;
            default:
                product = default;
                return false;
        }
    }
}
