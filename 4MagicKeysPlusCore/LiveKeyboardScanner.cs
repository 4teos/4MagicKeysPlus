using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace _4MagicKeysPlus.Core;

internal sealed record LiveKeyboardInterface(KeyboardProduct Product, KeyboardConnection Connection, string DevicePath, KeyboardIdentity? Identity);

// The HID interfaces of the keyboards that are connected right now, one per keyboard and transport.
internal static class LiveKeyboardScanner {
    public static IEnumerable<LiveKeyboardInterface> Scan() {
        foreach (string devicePath in HidDeviceEnumerator.EnumeratePaths()) {
            LiveKeyboardInterface? live = Probe(devicePath);
            if (live != null) {
                yield return live;
            }
        }
    }

    private static LiveKeyboardInterface? Probe(string devicePath) {
        using SafeFileHandle handle = HidNativeMethods.OpenHidDevice(devicePath);

        // A composite keyboard exposes several HID collections under the same VID/PID; the
        // keyboard's own keyboard-usage collection is access-denied by Windows' anti-keylogger
        // restriction (handle.IsInvalid here), so only the vendor collection actually opens
        // and answers the TryRead probe below - that's what confirms "this is the real device"
        // rather than just some unrelated sibling interface with a matching VID/PID.
        if (handle.IsInvalid || !TryGetIds(handle, out ushort vendorId, out ushort productId)) {
            return null;
        }

        if (!KnownKeyboards.TryGetConnection(vendorId, out KeyboardConnection connection)
            || !KnownKeyboards.TryGetProduct(productId, out KeyboardProduct product)
            || !KeyboardBattery.TryRead(handle, out _)) {
            return null;
        }

        return new LiveKeyboardInterface(product, connection, devicePath, ReadIdentity(handle, connection));
    }

    // Over Bluetooth Windows answers the HID serial string with the address of the keyboard, over USB with its serial number.
    private static KeyboardIdentity? ReadIdentity(SafeFileHandle handle, KeyboardConnection connection) {
        StringBuilder serial = new(256);
        if (!HidNativeMethods.HidD_GetSerialNumberString(handle, serial, serial.Capacity * sizeof(char))) {
            return null;
        }

        return connection == KeyboardConnection.Bluetooth
            ? KeyboardIdentity.FromBluetoothAddress(serial.ToString())
            : KeyboardIdentity.FromUsbSerial(serial.ToString());
    }

    private static bool TryGetIds(SafeFileHandle handle, out ushort vendorId, out ushort productId) {
        HidNativeMethods.HIDD_ATTRIBUTES attributes = new() {
            Size = Marshal.SizeOf<HidNativeMethods.HIDD_ATTRIBUTES>()
        };
        if (HidNativeMethods.HidD_GetAttributes(handle, ref attributes)) {
            vendorId = attributes.VendorID;
            productId = attributes.ProductID;
            return true;
        }

        vendorId = productId = 0;
        return false;
    }
}
