using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace _4MagicKeysPlus.Services;

internal sealed record DriverReloadResult(uint RulesRead, uint RulesAccepted, uint Devices);

internal interface IDriverControl {
    // Null when the driver has no control device: no keyboard is connected, or the driver is
    // older than live reload. Anything else that goes wrong is a Win32Exception.
    DriverReloadResult? Reload();
}

// Talks to the driver's control device (DriverInterface.h in the driver project). The handle is
// closed right after the request: an open handle keeps the device alive after the last
// keyboard has gone, which blocks unloading the driver.
internal sealed class DriverControl : IDriverControl {
    private const string DevicePath = @"\\.\4MagicKeysPlus";
    private const uint GenericWrite = 0x40000000;
    private const uint ShareReadWrite = 3;
    private const uint OpenExisting = 3;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;

    // CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800, METHOD_BUFFERED, FILE_WRITE_ACCESS)
    private const uint IoctlReloadConfig = 0x0022A000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint code, IntPtr input, uint inputSize, uint[] output, uint outputSize, out uint returned, IntPtr overlapped);

    public DriverReloadResult? Reload() {
        using SafeFileHandle device = CreateFile(DevicePath, GenericWrite, ShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (device.IsInvalid) {
            int error = Marshal.GetLastWin32Error();
            return error is ErrorFileNotFound or ErrorPathNotFound ? null : throw new Win32Exception(error);
        }

        uint[] result = new uint[3];
        if (!DeviceIoControl(device, IoctlReloadConfig, IntPtr.Zero, 0, result, (uint)(result.Length * sizeof(uint)), out _, IntPtr.Zero)) {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        return new DriverReloadResult(result[0], result[1], result[2]);
    }
}
