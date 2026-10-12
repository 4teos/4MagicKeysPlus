using Microsoft.Win32.SafeHandles;

namespace _4MagicKeysPlus.Core;

public static class KeyboardBattery {
    // The keyboard's vendor-defined HID collection (usage page 0xFF00) reports battery as a
    // 3-byte INPUT report: {0x90, status flags, percentage}. Confirmed on real hardware that
    // HidD_GetFeature does not work here - HidP_GetCaps shows this collection declares zero
    // feature reports; only HidD_GetInputReport reaches it.
    // Status byte layout confirmed against the equivalent Linux HID driver for Apple Magic
    // accessories (same report on Magic Mouse/Trackpad): bit 0x02 = external power connected.
    private const byte BatteryReportId = 0x90;
    private const int BatteryReportLength = 3;
    private const byte ExternalPowerFlag = 0x02;

    // Re-opens the exact device KeyboardDetector already confirmed works, instead of
    // re-scanning every HID interface on the system again.
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

    // Also used by KeyboardDetector as its liveness probe: among the several HID collections a
    // composite keyboard exposes under the same VID/PID, only this vendor one actually answers.
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
