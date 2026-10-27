#pragma once

// What a user-mode client (the configuration app) needs to talk to the driver. The driver
// creates this control device while at least one keyboard it filters is present.
// From user mode: CreateFile("\\\\.\\4MagicKeysPlus", GENERIC_WRITE, ...) - administrators and
// SYSTEM only - then DeviceIoControl.
//
// Client contract: open the device, send the request and close the handle right away; never keep
// it open. An open handle keeps the control device alive after the last keyboard has gone, which
// blocks unloading the driver (an update needs a reboot) and makes the next keyboard's attempt to
// create the device fail, so reloading stops working. CreateFile fails with error 2 when there is
// no keyboard or the driver is too old for this interface, and with error 5 when not elevated.

#define MKP_CONTROL_DEVICE_NAME  L"\\Device\\4MagicKeysPlus"
#define MKP_CONTROL_SYMLINK_NAME L"\\DosDevices\\4MagicKeysPlus"

// Looks up the profile of every keyboard again (Parameters\Keyboards\<profile>: FnLock and the Rules
// subkey, found by the keyboard's identity in the profile's Identities) and applies it to that keyboard,
// all at once. Input: none. Output (optional): MKP_RELOAD_RESULT.
// The value is 0x0022A000: the same constant is used by the C# client.
#define IOCTL_MKP_RELOAD_CONFIG CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800, METHOD_BUFFERED, FILE_WRITE_ACCESS)

typedef struct _MKP_RELOAD_RESULT {
    ULONG RulesRead;      // rule subkeys found in the profiles of the keyboards, summed over the keyboards
    ULONG RulesAccepted;  // those of them the driver can interpret; the rest are ignored
    ULONG Devices;        // keyboards the configuration was applied to
} MKP_RELOAD_RESULT, *PMKP_RELOAD_RESULT;
