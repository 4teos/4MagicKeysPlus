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

// DRIVERNAME and DebugPrint come from KeyProcessorTypes.h (its
// KEYPROCESSOR_KERNEL_BUILD branch - defined project-wide by
// 4MagicKeysPlus.vcxproj). It also carries the HID codes/enums, MAX_*_SIZE
// limits, VIRTUAL_EJECT/FN and CONSUMER_USAGE_* constants, shared as-is with
// the portable KeyProcessor class and its user-mode unit tests.
#include "KeyProcessorTypes.h"

#if defined(DBG)
#define DebugPrintBuffer(text, buffer, length) KdPrintBuffer(text, buffer, length)
#else
#define DebugPrintBuffer(...) ((void)0)
#endif

    ///////////////////////////////////////////////////////////////////////////////
    // Globals

    extern DWORD g_dwFnLock;
    extern BYTE g_KeyMap[MAX_KEYMAP_SIZE];
    extern ULONG g_KeyMapSize;
    extern BYTE g_ModMap[MAX_MODMAP_SIZE];
    extern ULONG g_ModMapSize;
    extern BYTE g_SpecialModMap[MAX_SPECIAL_MODMAP_SIZE];
    extern ULONG g_SpecialModMapSize;

    ///////////////////////////////////////////////////////////////////////////////
    // Per-device context - holds the handle to our virtual Consumer Control HID
    // device and to this device's own KeyProcessor instance (opaque handle -
    // KeyProcessor is a C++ class defined in KeyProcessor.h/.cpp; kept as PVOID
    // here so this header and Driver.c can stay plain C).
    //
    typedef struct _DEVICE_CONTEXT {
        VHFHANDLE VhfHandle;
        PVOID KeyProcessorHandle;
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

    VOID EvtVhfReadyForNextReadReport(IN PVOID VhfClientContext);

    ///////////////////////////////////////////////////////////////////////////////
    // KeyProcessor - opaque C wrapper around the KeyProcessor C++ class
    // (KeyProcessor.h/.cpp, wrapped for kernel use in KeyProcessorInterop.cpp).
    // One instance per physical device, owned by that device's DEVICE_CONTEXT.
    //
    PVOID KeyProcessorCreate(void);
    void KeyProcessorDestroy(PVOID handle);
    void KeyProcessorLoadConfig(PVOID handle, DWORD fnLock,
        const BYTE* keyMap, ULONG keyMapSize,
        const BYTE* modMap, ULONG modMapSize,
        const BYTE* specialModMap, ULONG specialModMapSize);
    void KeyProcessorProcess(PVOID handle, BYTE* buf, ULONG size, VHFHANDLE vhfHandle);

#ifdef __cplusplus
}
#endif
