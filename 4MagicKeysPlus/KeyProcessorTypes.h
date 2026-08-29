#pragma once

// Pure HID constants/enums shared between the kernel driver build and the
// user-mode KeyProcessor unit tests. Deliberately free of kernel-only headers
// (ntddk.h/wdf.h/vhf.h) - see Driver.h for those, and for DEVICE_CONTEXT /
// VHFHANDLE, which the tests don't need.

// KEYPROCESSOR_KERNEL_BUILD is defined project-wide by 4MagicKeysPlus.vcxproj
// (NOT by KeyProcessor.Tests.vcxproj). This has to be a project-level define
// rather than something deduced from each translation unit's own #include
// history: KeyProcessor.cpp never includes Driver.h/ntddk.h itself, so a
// per-TU guard (e.g. "is _NTDDK_ defined yet") would let it disagree with
// KeyProcessorInterop.cpp about whether BYTE/ULONG/... are the real ntddk.h
// typedefs or fallback ones - same-named types with different underlying
// types means different mangled names, and the two .obj files fail to link
// (this happened once - LNK2019 on KeyProcessor::LoadConfig/Process).
#if defined(KEYPROCESSOR_KERNEL_BUILD)

// BYTE/DWORD/... aren't actually typedef'd by ntddk.h/wdm.h alone - they come
// from further down this same chain (pulled in transitively by the
// Bluetooth/USB/WDF headers). Mirror Driver.h's include list exactly rather
// than guessing which single header is "the" one that provides them; Driver.h
// already includes all of these before reaching this file, so this is a
// no-op there (include guards) and only does real work for KeyProcessor.cpp,
// which doesn't go through Driver.h at all.
#include <ntddk.h>
#include <wdf.h>
#include <vhf.h>
#include <initguid.h>
#include <ntstrsafe.h>
#include <bthdef.h>
#include <ntintsafe.h>
#include <bthguid.h>
#include <bthioctl.h>
#include <sdpnode.h>
#include <bthddi.h>
#include <bthsdpddi.h>
#include <bthsdpdef.h>
#include "usbioctl.h"
#include "usbdi.h"

#define DRIVERNAME "4MagicKeysPlus"

#if defined(DBG)
#define DebugPrint(s, ...) DbgPrintEx(DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, DRIVERNAME ": " s, ##__VA_ARGS__)
#else
#define DebugPrint(...) ((void)0)
#endif

#else // !KEYPROCESSOR_KERNEL_BUILD - KeyProcessor.Tests, plain user-mode C++

#if !defined(_WINDEF_)
#include <cstdint>
#include <cstddef>
typedef std::uint8_t BYTE;
typedef std::uint16_t USHORT;
typedef std::uint32_t ULONG;
typedef std::uint32_t DWORD;
typedef std::uint8_t BOOLEAN;
#ifndef TRUE
#define TRUE 1
#endif
#ifndef FALSE
#define FALSE 0
#endif
#endif

#define DebugPrint(...) ((void)0)

#endif // KEYPROCESSOR_KERNEL_BUILD

#define MAX_KEYMAP_SIZE 128
#define MAX_MODMAP_SIZE 16
#define MAX_SPECIAL_MODMAP_SIZE 4

#define VIRTUAL_EJECT 0xF0
#define VIRTUAL_FN    0xF1

// Consumer Control (usage page 0x0C) usage codes reported in the virtual
// Consumer Control report - see g_ConsumerReportDescriptor (Driver.c), which
// reports the currently pressed key as a single 16-bit usage value.
#define CONSUMER_USAGE_NONE         0x0000
#define CONSUMER_USAGE_NEXT         0x00B5
#define CONSUMER_USAGE_PREV         0x00B6
#define CONSUMER_USAGE_PLAYPAUSE    0x00CD
#define CONSUMER_USAGE_BRIGHT_UP    0x006F
#define CONSUMER_USAGE_BRIGHT_DOWN  0x0070
#define CONSUMER_USAGE_MUTE         0x00E2
#define CONSUMER_VOLUME_UP          0x00E9
#define CONSUMER_VOLUME_DOWN        0x00EA
#define CONSUMER_CALCULATOR         0x0192

enum HidCodes {
    HidKeyNone = 0x0,
    HidKeyErrOvf = 0x1,
    HidEnter = 0x28,

    HidKeyB = 0x5,
    HidKeyP = 0x13,
    HidKeyS = 0x16,
    HidKeyT = 0x17,

    HidF1 = 0x3a,
    HidF2 = 0x3b,
    HidF3 = 0x3c,
    HidF4 = 0x3d,
    HidF5 = 0x3e,
    HidF6 = 0x3f,
    HidF7 = 0x40,
    HidF8 = 0x41,
    HidF9 = 0x42,
    HidF10 = 0x43,
    HidF11 = 0x44,
    HidF12 = 0x45,
    HidF13 = 0x68,
    HidF14 = 0x69,
    HidF15 = 0x6a,
    HidF16 = 0x6b,
    HidF17 = 0x6c,
    HidF18 = 0x6d,
    HidF19 = 0x6e,
    HidF20 = 0x6f,
    HidF21 = 0x70,
    HidF22 = 0x71,
    HidF23 = 0x72,
    HidF24 = 0x73,

    HidLeft = 0x50,
    HidRight = 0x4f,
    HidUp = 0x52,
    HidDown = 0x51,

    HidInsert = 0x49,
    HidDel = 0x4c,
    HidHome = 0x4a,
    HidEnd = 0x4d,
    HidPgUp = 0x4b,
    HidPgDown = 0x4e,

    HidPrtScr = 0x46,
    HidScrLck = 0x47,
    HidPauseBreak = 0x48,
};

enum HidModifierMasks {
    HidLeftCtrlMask = 0x1,
    HidRightCtrlMask = 0x10,

    HidLeftShiftMask = 0x2,
    HidRightShiftMask = 0x20,

    HidLeftAltMask = 0x4,
    HidRightAltMask = 0x40,

    HidLeftCmdMask = 0x8,
    HidRightCmdMask = 0x80
};

static const BYTE g_SpecialKeyCodes[] = { VIRTUAL_EJECT, VIRTUAL_FN };
