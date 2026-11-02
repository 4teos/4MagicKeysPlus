using Microsoft.Win32.SafeHandles;

namespace _4MagicKeysPlus.Core;

public static class KeyboardBattery {
    // The vendor collection (usage page 0xFF00) reports the battery as a 3-byte INPUT report {0x90, flags, percent}. On real hardware
    // HidD_GetFeature does not work (the collection declares no feature reports), only HidD_GetInputReport does.
    // Flags bit 0x02 = external power connected (as in the Linux HID driver for Apple accessories).
    private const byte BatteryReportId = 0x90;
    private const int BatteryReportLength = 3;
    private const byte ExternalPowerFlag = 0x02;

    public static KeyboardBatteryStatus? ReadStatus(DetectedKeyboard? detectedKeyboard) {
        if (detectedKeyboard == null) {
            return null;
        }

        using SafeFileHandle handle = HidNativeMethods.OpenHidDevice(detectedKeyboard.DevicePath);
        if (handle.IsInvalid) {
            return null;
        }

        return TryRead(handle, out KeyboardBatteryStatus status) ? status : null;
    }

    // Also the liveness probe of KeyboardDetector: of the HID collections of a composite keyboard only this vendor one answers.
    internal static bool TryRead(SafeFileHandle handle, out KeyboardBatteryStatus status) {
        byte[] report = new byte[BatteryReportLength];
        report[0] = BatteryReportId;
        if (HidNativeMethods.HidD_GetInputReport(handle, report, report.Length)) {
            status = new KeyboardBatteryStatus(report[2], (report[1] & ExternalPowerFlag) != 0);
            return true;
        }

        status = null!;
        return false;
    }
}
