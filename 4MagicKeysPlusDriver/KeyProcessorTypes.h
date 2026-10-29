#pragma once

// Defined project-wide by the driver project, never by the tests: every file must agree on the types it sees.
#if defined(KEYPROCESSOR_KERNEL_BUILD)

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

#else

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

#endif

#define MAX_RULES 128

// Rule encoding (Parameters\Rules\<name>\Source / Target, both REG_DWORD):
//   bits  0-15  code: HID keyboard usage, VIRTUAL_EJECT/VIRTUAL_FN/VIRTUAL_LOCK (the last one only in a
//               source), or - in a target with RULE_TARGET_CONSUMER - a Consumer Control usage.
//               0 means "no key" (a pure modifier rule).
//   bits 16-23  modifier mask (HidModifierMasks bits).
//   bit  24     source: Fn must be held (RULE_SOURCE_FN);
//               target: the code is a Consumer Control usage (RULE_TARGET_CONSUMER).
//   bits 25-31  reserved, a rule with any of them set is ignored.
#define RULE_CODE_MASK       0x0000FFFFUL
#define RULE_MODS_SHIFT      16
#define RULE_MODS_MASK       0x00FF0000UL
#define RULE_SOURCE_FN       0x01000000UL
#define RULE_TARGET_CONSUMER 0x01000000UL
#define RULE_FLAGS_MASK      0xFF000000UL

typedef struct _KEY_RULE {
    DWORD Source;
    DWORD Target;
    BOOLEAN FnLockAware;
} KEY_RULE, *PKEY_RULE;

#define VIRTUAL_EJECT 0xF0
#define VIRTUAL_FN    0xF1
#define VIRTUAL_LOCK  0xF2

// Bits of the last byte of the keyboard report. Apple reports the Lock key (Touch ID on the models that have it) as the consumer usage Menu.
#define SPECIAL_KEY_EJECT 0x01
#define SPECIAL_KEY_FN    0x02
#define SPECIAL_KEY_LOCK  0x04

#define CONSUMER_USAGE_NONE        0x0000
#define CONSUMER_USAGE_NEXT        0x00B5
#define CONSUMER_USAGE_PREV        0x00B6
#define CONSUMER_USAGE_PLAYPAUSE   0x00CD
#define CONSUMER_USAGE_BRIGHT_UP   0x006F
#define CONSUMER_USAGE_BRIGHT_DOWN 0x0070
#define CONSUMER_USAGE_MUTE        0x00E2
#define CONSUMER_VOLUME_UP         0x00E9
#define CONSUMER_VOLUME_DOWN       0x00EA
#define CONSUMER_CALCULATOR        0x0192
// The maximum of g_ConsumerReportDescriptor.
#define CONSUMER_USAGE_MAX 0x03FF

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
