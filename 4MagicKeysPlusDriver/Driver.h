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

#include "KeyProcessorTypes.h"
#include "DriverInterface.h"
#include "KeyboardIdentity.h"
#include "UsbDescriptorFix.h"

#if defined(DBG)
#define DebugPrintBuffer(text, buffer, length) KdPrintBuffer(text, buffer, length)
#else
#define DebugPrintBuffer(...) ((void)0)
#endif

    typedef struct _DEVICE_CONTEXT {
        VHFHANDLE VhfHandle;
        PVOID KeyProcessorHandle;
        BOOLEAN InDeviceCollection;
        WCHAR Identity[MKP_IDENTITY_MAX_CHARS];
        ULONG ProductId;
        BOOLEAN DescriptorFixOnly;
    } DEVICE_CONTEXT, *PDEVICE_CONTEXT;

    WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(DEVICE_CONTEXT, GetDeviceContext)

    // A single 16-bit selector instead of a per-usage bitmap: the legacy media-key translation of Windows does not recognize bitmaps.
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

    NTSTATUS DriverEntry(IN PDRIVER_OBJECT driverObject, IN PUNICODE_STRING registryPath);
    NTSTATUS EvtDriverDeviceAdd(IN WDFDRIVER driver, IN PWDFDEVICE_INIT deviceInit);
    void EvtDeviceContextCleanup(IN WDFOBJECT object);
    void EvtIoInternalDeviceControl(IN WDFQUEUE queue, IN WDFREQUEST request, IN size_t outputBufferLength, IN size_t inputBufferLength, IN ULONG ioControlCode);
    void InternalIoctlRequestCompletion(IN WDFREQUEST request, IN WDFIOTARGET target, IN PWDF_REQUEST_COMPLETION_PARAMS params, IN WDFCONTEXT context);
    void KdPrintBuffer(PCHAR text, PUCHAR buffer, ULONG length);

    VOID EvtVhfReadyForNextReadReport(IN PVOID VhfClientContext);

    PVOID KeyProcessorCreate(void);
    void KeyProcessorDestroy(PVOID handle);
    ULONG KeyProcessorLoadConfig(PVOID handle, DWORD fnLock, const KEY_RULE* rules, ULONG ruleCount);
    void KeyProcessorProcess(PVOID handle, BYTE* buf, ULONG size, VHFHANDLE vhfHandle);

#ifdef __cplusplus
}
#endif
