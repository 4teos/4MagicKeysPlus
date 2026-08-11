#pragma once

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

#ifdef __cplusplus
extern "C" {
#endif

#define DRIVERNAME "WinAppleKey"
#define MAX_KEYMAP_SIZE 128
#define VIRTUAL_EJECT 0xF0
#define VIRTUAL_FN    0xF1

#if defined(DBG)
#define DebugPrint(s, ...) DbgPrintEx(DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, DRIVERNAME ": " s, ##__VA_ARGS__)
#define DebugPrintBuffer(text, buffer, length) KdPrintBuffer(text, buffer, length)
#else
#define DebugPrint(...) ((void)0)
#define DebugPrintBuffer(...) ((void)0)
#endif

#define MAX_MODMAP_SIZE 16

// Consumer Control (usage page 0x0C) usage codes reported in the virtual
// Consumer Control report - see g_ConsumerReportDescriptor (Driver.c),
// which reports the currently pressed key as a single 16-bit usage value.
#define CONSUMER_USAGE_NONE         0x0000
#define CONSUMER_USAGE_NEXT         0x00B5
#define CONSUMER_USAGE_PREV         0x00B6
#define CONSUMER_USAGE_PLAYPAUSE    0x00CD
#define CONSUMER_USAGE_BRIGHT_UP    0x006F
#define CONSUMER_USAGE_BRIGHT_DOWN  0x0070

// Consumer volume control.
#define CONSUMER_USAGE_MUTE         0x00E2
#define CONSUMER_VOLUME_UP          0x00E9
#define CONSUMER_VOLUME_DOWN        0x00EA


    ///////////////////////////////////////////////////////////////////////////////
    // Globals

    extern DWORD g_dwFnLock;
    extern BYTE g_KeyMap[MAX_KEYMAP_SIZE];
    extern ULONG g_KeyMapSize;
    extern BYTE g_ModMap[MAX_MODMAP_SIZE];
    extern ULONG g_ModMapSize;

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

    ///////////////////////////////////////////////////////////////////////////////
    // Per-device context - holds the handle to our virtual Consumer Control HID device
    //
    typedef struct _DEVICE_CONTEXT {
        VHFHANDLE VhfHandle;
    } DEVICE_CONTEXT, * PDEVICE_CONTEXT;

    WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(DEVICE_CONTEXT, GetDeviceContext)

    ///////////////////////////////////////////////////////////////////////////////
    // Report Descriptor for the virtual Consumer Control device (media/brightness
    // keys that cannot be expressed on the physical keyboard's own report).
    //
    // Array/selector style: a single 16-bit field reports the usage code of
    // whichever Consumer Control key is currently pressed (0 = none). This
    // matches how real multimedia keyboards report Consumer Control usages -
    // Windows' legacy media-key translation (WM_APPCOMMAND / VK_MEDIA_*) does
    // not appear to recognize a per-usage bitmap encoding.
    static const UCHAR g_ConsumerReportDescriptor[] = {
        0x05, 0x0C,        // USAGE_PAGE (Consumer Devices)
        0x09, 0x01,        // USAGE (Consumer Control)
        0xA1, 0x01,        // COLLECTION (Application)
        0x85, 0x02,        //   REPORT_ID (2)
        0x15, 0x00,        //   LOGICAL_MINIMUM (0)
        0x26, 0xFF, 0x03,  //   LOGICAL_MAXIMUM (1023)
        0x19, 0x00,        //   USAGE_MINIMUM (0)
        0x2A, 0xFF, 0x03,  //   USAGE_MAXIMUM (1023)
        0x75, 0x10,        //   REPORT_SIZE (16)
        0x95, 0x01,        //   REPORT_COUNT (1)
        0x81, 0x00,        //   INPUT (Data,Ary,Abs)
        0xC0                // END_COLLECTION
    };


    ///////////////////////////////////////////////////////////////////////////////
    // Global functions

    NTSTATUS DriverEntry(IN PDRIVER_OBJECT driverObject, IN PUNICODE_STRING registryPath);
    NTSTATUS EvtDriverDeviceAdd(IN WDFDRIVER driver, IN PWDFDEVICE_INIT deviceInit);
    void EvtDeviceContextCleanup(IN WDFOBJECT object);
    void EvtIoInternalDeviceControl(IN WDFQUEUE queue, IN WDFREQUEST request, IN size_t outputBufferLength, IN size_t inputBufferLength, IN ULONG ioControlCode);
    void InternalIoctlRequestCompletion(IN WDFREQUEST request, IN WDFIOTARGET target, IN PWDF_REQUEST_COMPLETION_PARAMS params, IN WDFCONTEXT context);
    void KdPrintBuffer(PCHAR text, PUCHAR buffer, ULONG length);
    NTSTATUS ReadDriverRegistryValue(PUNICODE_STRING registryPath, DWORD dwRegValeType, PCWSTR wcszValName, PVOID* pValue);

    void ProcessKeyBuffer(BYTE* pbuf, ULONG size, VHFHANDLE vhfHandle);
    VOID EvtVhfReadyForNextReadReport(IN PVOID VhfClientContext);

#ifdef __cplusplus
}
#endif
