#pragma once

// Declares wchar_t in the kernel build, where it is not a built-in type (the project has no /Zc:wchar_t).
#include "KeyProcessorTypes.h"

// The identity of a keyboard as the configuration app writes it into the registry (see KeyboardIdentity.cs in
// 4MagicKeysPlusCore): bt:C4141104498E for a Bluetooth pairing, usb:F0T125100BWJKPGAS for a USB cable, in upper
// case. A plain C interface without kernel types, so that Driver.c and the user-mode unit tests use it alike.

#ifdef __cplusplus
extern "C" {
#endif

// The longest identity text with its terminator. A USB serial number that does not fit gives no identity.
#define MKP_IDENTITY_MAX_CHARS 64

// The identity from the instance ID of the paired Bluetooth HID service the driver is attached to, e.g.
// BTHENUM\{00001124-...}_VID&0001004C_PID&026C\D&15B70467&0&C4141104498E_C00000000 -> bt:C4141104498E.
// Returns 1 and writes the identity, or returns 0 and writes an empty string.
    int MkpIdentityFromBluetoothDeviceId(const wchar_t* deviceId, wchar_t* identity);

// The identity from the instance ID of the USB device that is the parent of the interface the driver is attached to,
// e.g. USB\VID_05AC&PID_026C\F0T125100BWJKPGAS -> usb:F0T125100BWJKPGAS. A device without a serial number has an ID
// made up from its port, with an '&' in it, that changes with the port: no identity. Same results as above.
    int MkpIdentityFromUsbDeviceId(const wchar_t* deviceId, wchar_t* identity);

// The USB product ID of the model (0x0267 A1644, 0x026C A1843) from the same instance IDs as the two above, e.g. 0x026C
// from both examples; the factory defaults of the model are looked up by it. 0 when the ID does not carry one. The
// Bluetooth and the USB product IDs of a model are the same.
    unsigned int MkpProductIdFromBluetoothDeviceId(const wchar_t* deviceId);
    unsigned int MkpProductIdFromUsbDeviceId(const wchar_t* deviceId);

// Whether the identity is one of the strings of a REG_MULTI_SZ list. 'chars' is the size of the data in characters:
// the list may lack its final terminators. Letters are compared without regard to case.
    int MkpIdentityListContains(const wchar_t* list, unsigned int chars, const wchar_t* identity);

#ifdef __cplusplus
}
#endif
