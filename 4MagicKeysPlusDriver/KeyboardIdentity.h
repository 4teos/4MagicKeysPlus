#pragma once

#include "KeyProcessorTypes.h"

// The strings must be the same as KeyboardIdentity.cs of 4MagicKeysPlusCore writes: bt:AABBCCDDEEFF, usb:<serial>, upper case.

#ifdef __cplusplus
extern "C" {
#endif

#define MKP_IDENTITY_MAX_CHARS 64

// BTHENUM\{00001124-...}_VID&0001004C_PID&026C\D&15B70467&0&C4141104498E_C00000000 -> bt:C4141104498E
    int MkpIdentityFromBluetoothDeviceId(const wchar_t* deviceId, wchar_t* identity);

// USB\VID_05AC&PID_026C\F0T125100BWJKPGAS -> usb:F0T125100BWJKPGAS; a device without a serial has an ID made up from its port (with an '&'): no identity
    int MkpIdentityFromUsbDeviceId(const wchar_t* deviceId, wchar_t* identity);

// The Bluetooth and the USB product IDs of a model are the same.
    unsigned int MkpProductIdFromBluetoothDeviceId(const wchar_t* deviceId);
    unsigned int MkpProductIdFromUsbDeviceId(const wchar_t* deviceId);

    int MkpIsUsbInterface0(const wchar_t* deviceId);

// 'chars' is the size of the data in characters: the list may lack its final terminators.
    int MkpIdentityListContains(const wchar_t* list, unsigned int chars, const wchar_t* identity);

#ifdef __cplusplus
}
#endif
