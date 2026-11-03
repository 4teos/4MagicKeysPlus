using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace _4MagicKeysPlus.Core;

public static class DriverControlDevice {
    public const string DevicePath = @"\\.\4MagicKeysPlus";

    private const uint ShareReadWrite = 3;
    private const uint OpenExisting = 3;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    // The driver makes the device with the first keyboard it takes and deletes it with the last, so it exists while a keyboard runs on the driver.
    // Opening it without any access asks nothing of the driver; a device that cannot be opened is still there.
    public static bool Exists() {
        using SafeFileHandle device = CreateFile(DevicePath, 0, ShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        return !device.IsInvalid || Marshal.GetLastWin32Error() is not (ErrorFileNotFound or ErrorPathNotFound);
    }
}
