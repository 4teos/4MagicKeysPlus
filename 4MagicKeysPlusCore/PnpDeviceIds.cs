using System.Runtime.InteropServices;

namespace _4MagicKeysPlus.Core;

// The instance IDs of the devices that are present now under one enumerator ("USB", "BTHENUM"), from the configuration manager.
internal static class PnpDeviceIds {
    private const uint FilterEnumerator = 0x1;
    private const uint FilterPresent = 0x100;
    private const int Success = 0;
    private const int BufferSmall = 0x1A;

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_ID_List_SizeW(out uint length, string filter, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_ID_ListW(string filter, [Out] char[] buffer, uint bufferLength, uint flags);

    public static IReadOnlyList<string> Present(string enumerator) {
        const uint flags = FilterEnumerator | FilterPresent;

        // The list can grow between asking for its size and reading it, which is reported as a small buffer.
        for (int attempt = 0; attempt < 3; attempt++) {
            if (CM_Get_Device_ID_List_SizeW(out uint length, enumerator, flags) != Success) {
                return [];
            }

            char[] buffer = new char[length];
            int result = CM_Get_Device_ID_ListW(enumerator, buffer, length, flags);
            if (result == Success) {
                return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
            }
            if (result != BufferSmall) {
                return [];
            }
        }
        return [];
    }
}
