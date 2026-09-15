using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlus.Services;

internal static class KeyboardDetector {
    // VID identifies the transport (Apple's USB-IF vendor ID vs its Bluetooth SIG company ID),
    // PID the model - same pairs 4MagicKeysPlusDriver\4MagicKeysPlus.inf matches on, just read
    // here via HidD_GetAttributes instead of a literal hardware-ID string.
    private static readonly (ushort VendorId, ushort ProductId, KeyboardModel Model, KeyboardConnection Connection)[] KnownDevices = [
        (0x05AC, 0x0267, KeyboardModel.A1644, KeyboardConnection.Usb),
        (0x05AC, 0x026C, KeyboardModel.A1843, KeyboardConnection.Usb),
        (0x004C, 0x0267, KeyboardModel.A1644, KeyboardConnection.Bluetooth),
        (0x004C, 0x026C, KeyboardModel.A1843, KeyboardConnection.Bluetooth),
    ];

    public static DetectedKeyboard? DetectConnectedKeyboard() {
        foreach (string devicePath in HidDeviceEnumerator.EnumeratePaths()) {
            using SafeFileHandle handle = NativeMethods.OpenHidDevice(devicePath);

            // A composite keyboard exposes several HID collections under the same VID/PID; the
            // keyboard's own keyboard-usage collection is access-denied by Windows' anti-keylogger
            // restriction (handle.IsInvalid here), so only the vendor collection actually opens
            // and answers the TryRead probe below - that's what confirms "this is the real device"
            // rather than just some unrelated sibling interface with a matching VID/PID.
            if (handle.IsInvalid || !TryGetIds(handle, out ushort vendorId, out ushort productId)) {
                continue;
            }

            foreach ((ushort knownVendorId, ushort knownProductId, KeyboardModel model, KeyboardConnection connection) in KnownDevices) {
                if (vendorId == knownVendorId && productId == knownProductId && KeyboardBattery.TryRead(handle, out _)) {
                    return new DetectedKeyboard(model, connection, devicePath);
                }
            }
        }

        return null;
    }

    private static bool TryGetIds(SafeFileHandle handle, out ushort vendorId, out ushort productId) {
        NativeMethods.HIDD_ATTRIBUTES attributes = new() { Size = Marshal.SizeOf<NativeMethods.HIDD_ATTRIBUTES>() };
        if (NativeMethods.HidD_GetAttributes(handle, ref attributes)) {
            vendorId = attributes.VendorID;
            productId = attributes.ProductID;
            return true;
        }

        vendorId = productId = 0;
        return false;
    }
}
