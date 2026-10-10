using System.Runtime.InteropServices;

namespace _4MagicKeysPlus.Services;

internal static class HidDeviceEnumerator {
    public static IEnumerable<string> EnumeratePaths() {
        NativeMethods.HidD_GetHidGuid(out Guid hidGuid);

        IntPtr deviceInfoSet = NativeMethods.SetupDiGetClassDevs(
            ref hidGuid, IntPtr.Zero, IntPtr.Zero,
            NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);

        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1)) {
            yield break;
        }

        try {
            NativeMethods.SP_DEVICE_INTERFACE_DATA interfaceData = new() {
                cbSize = Marshal.SizeOf<NativeMethods.SP_DEVICE_INTERFACE_DATA>()
            };

            for (int i = 0; NativeMethods.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, i, ref interfaceData); i++) {
                string? path = TryGetDevicePath(deviceInfoSet, ref interfaceData);
                if (path != null) {
                    yield return path;
                }
            }
        } finally {
            NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static string? TryGetDevicePath(IntPtr deviceInfoSet, ref NativeMethods.SP_DEVICE_INTERFACE_DATA interfaceData) {
        NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out int requiredSize, IntPtr.Zero);

        IntPtr detailBuffer = Marshal.AllocHGlobal(requiredSize);
        try {
            // SP_DEVICE_INTERFACE_DETAIL_DATA.cbSize must be this platform-dependent constant
            // (not the variable-length struct's real size), or the call fails with ERROR_INVALID_PARAMETER.
            Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 4 + Marshal.SystemDefaultCharSize);

            if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailBuffer, requiredSize, out _, IntPtr.Zero)) {
                return null;
            }

            IntPtr pathPtr = IntPtr.Add(detailBuffer, 4);
            return Marshal.PtrToStringAuto(pathPtr);
        } finally {
            Marshal.FreeHGlobal(detailBuffer);
        }
    }
}
