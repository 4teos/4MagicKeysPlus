#pragma once

// Client contract: open the control device, send the request and close the handle right away; never keep it open.
// An open handle keeps the device alive after the last keyboard has gone, which blocks unloading the driver and makes
// the next keyboard's attempt to create it fail, so reloading stops working. CreateFile fails with error 2 when there
// is no keyboard (or the driver is too old) and with error 5 when not elevated.

#define MKP_CONTROL_DEVICE_NAME  L"\\Device\\4MagicKeysPlus"
#define MKP_CONTROL_SYMLINK_NAME L"\\DosDevices\\4MagicKeysPlus"

// 0x0022A000, the same constant is used by the C# client.
#define IOCTL_MKP_RELOAD_CONFIG CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800, METHOD_BUFFERED, FILE_WRITE_ACCESS)

typedef struct _MKP_RELOAD_RESULT {
    ULONG RulesRead;
    ULONG RulesAccepted;
    ULONG Devices;
} MKP_RELOAD_RESULT, *PMKP_RELOAD_RESULT;
