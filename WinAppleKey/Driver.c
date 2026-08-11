#include "Driver.h"

///////////////////////////////////////////////////////////////////////////////
// Globals Initialisation
//
DWORD g_dwFnLock = 0;
BYTE g_KeyMap[MAX_KEYMAP_SIZE] = {0};
ULONG g_KeyMapSize = 0;
BYTE g_ModMap[MAX_MODMAP_SIZE] = {0};
ULONG g_ModMapSize = 0;


///////////////////////////////////////////////////////////////////////////////
// Read REG_BINARY value from registry into a buffer
//
static NTSTATUS ReadBinaryRegistryValue(PUNICODE_STRING registryPath, PCWSTR wcszValName, BYTE* destBuf, ULONG destMaxSize, ULONG* pOutSize) {
    PAGED_CODE();

    HANDLE hKey;
    OBJECT_ATTRIBUTES oa;
    InitializeObjectAttributes(&oa, registryPath, OBJ_CASE_INSENSITIVE, NULL, NULL);

    NTSTATUS status = ZwOpenKey(&hKey, KEY_READ, &oa);
    if (!NT_SUCCESS(status))
        return status;

    UNICODE_STRING valname;
    ULONG size = 0;
    RtlInitUnicodeString(&valname, wcszValName);
    status = ZwQueryValueKey(hKey, &valname, KeyValuePartialInformation, NULL, 0, &size);
    if (status != STATUS_OBJECT_NAME_NOT_FOUND && size) {
        PKEY_VALUE_PARTIAL_INFORMATION vp = (PKEY_VALUE_PARTIAL_INFORMATION)ExAllocatePoolWithTag(PagedPool, size, '1gaT');
        if (vp) {
            status = ZwQueryValueKey(hKey, &valname, KeyValuePartialInformation, vp, size, &size);
            if (NT_SUCCESS(status) && vp->Type == REG_BINARY && vp->DataLength <= destMaxSize) {
                RtlCopyMemory(destBuf, vp->Data, vp->DataLength);
                *pOutSize = vp->DataLength;
                DebugPrint("%ws loaded: %lu bytes\n", wcszValName, *pOutSize);
            }
            ExFreePool(vp);
        }
    }

    ZwClose(hKey);
    return STATUS_SUCCESS;
}

///////////////////////////////////////////////////////////////////////////////
// Driver Entry
//
NTSTATUS DriverEntry(IN PDRIVER_OBJECT driverObject, IN PUNICODE_STRING registryPath) {
    DebugPrint("DriverEntry(): driverObject = 0x%p\n", driverObject);

    WDF_DRIVER_CONFIG config;
    WDF_DRIVER_CONFIG_INIT(&config, EvtDriverDeviceAdd);

    NTSTATUS status = WdfDriverCreate(driverObject, registryPath, WDF_NO_OBJECT_ATTRIBUTES, &config, WDF_NO_HANDLE);
    if (!NT_SUCCESS(status)) {
        DebugPrint("DriverEntry(): WdfDriverCreate failed: 0x%x\n", status);
        return status;
    }

    // Build Parameters subkey path
    WCHAR paramPathBuf[512];
    UNICODE_STRING paramPath;
    RtlStringCbPrintfW(paramPathBuf, sizeof(paramPathBuf), L"%wZ\\Parameters", registryPath);
    RtlInitUnicodeString(&paramPath, paramPathBuf);

    ReadDriverRegistryValue(&paramPath, REG_DWORD, L"FnLock", (void*)&g_dwFnLock);
    ReadBinaryRegistryValue(&paramPath, L"KeyMap", g_KeyMap, MAX_KEYMAP_SIZE, &g_KeyMapSize);
    ReadBinaryRegistryValue(&paramPath, L"ModMap", g_ModMap, MAX_MODMAP_SIZE, &g_ModMapSize);

    DebugPrint("FnLock=%d, KeyMapSize=%lu, ModMapSize=%lu\n", g_dwFnLock, g_KeyMapSize, g_ModMapSize);

    return STATUS_SUCCESS;
}

///////////////////////////////////////////////////////////////////////////////
// EvtDriverDeviceAdd - creates the KMDF filter device object and its I/O queue
//
NTSTATUS EvtDriverDeviceAdd(IN WDFDRIVER driver, IN PWDFDEVICE_INIT deviceInit) {
    UNREFERENCED_PARAMETER(driver);

    DebugPrint("EvtDriverDeviceAdd()\n");

    WdfFdoInitSetFilter(deviceInit);

    WDF_OBJECT_ATTRIBUTES deviceAttributes;
    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&deviceAttributes, DEVICE_CONTEXT);
    deviceAttributes.EvtCleanupCallback = EvtDeviceContextCleanup;

    WDFDEVICE hDevice;
    NTSTATUS status = WdfDeviceCreate(&deviceInit, &deviceAttributes, &hDevice);
    if (!NT_SUCCESS(status)) {
        DebugPrint("EvtDriverDeviceAdd(): WdfDeviceCreate failed: 0x%x\n", status);
        return status;
    }

    WDF_IO_QUEUE_CONFIG queueConfig;
    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&queueConfig, WdfIoQueueDispatchParallel);
    queueConfig.EvtIoInternalDeviceControl = EvtIoInternalDeviceControl;

    WDFQUEUE queue;
    status = WdfIoQueueCreate(hDevice, &queueConfig, WDF_NO_OBJECT_ATTRIBUTES, &queue);
    if (!NT_SUCCESS(status)) {
        DebugPrint("EvtDriverDeviceAdd(): WdfIoQueueCreate failed: 0x%x\n", status);
        return status;
    }

    PDEVICE_CONTEXT deviceContext = GetDeviceContext(hDevice);
    deviceContext->VhfHandle = NULL;

    VHF_CONFIG vhfConfig;
    VHF_CONFIG_INIT(&vhfConfig, WdfDeviceWdmGetDeviceObject(hDevice), sizeof(g_ConsumerReportDescriptor), (PUCHAR)g_ConsumerReportDescriptor);
    vhfConfig.VendorID = 0x0001;
    vhfConfig.ProductID = 0x0002;
    vhfConfig.VersionNumber = 0x0001;
    vhfConfig.EvtVhfReadyForNextReadReport = EvtVhfReadyForNextReadReport;

    status = VhfCreate(&vhfConfig, &deviceContext->VhfHandle);
    if (!NT_SUCCESS(status)) {
        DebugPrint("EvtDriverDeviceAdd(): VhfCreate failed: 0x%x\n", status);
        return status;
    }

    status = VhfStart(deviceContext->VhfHandle);
    if (!NT_SUCCESS(status)) {
        DebugPrint("EvtDriverDeviceAdd(): VhfStart failed: 0x%x\n", status);
        return status;
    }

    DebugPrint("EvtDriverDeviceAdd(): virtual consumer-control HID device started\n");

    return STATUS_SUCCESS;
}

///////////////////////////////////////////////////////////////////////////////
// Diagnostic: fires whenever hidclass issues a new IOCTL_HID_READ_REPORT to
// our virtual device and VHF is ready to accept another VhfReadReportSubmit.
//
VOID EvtVhfReadyForNextReadReport(IN PVOID VhfClientContext) {
    UNREFERENCED_PARAMETER(VhfClientContext);
    DebugPrint("EvtVhfReadyForNextReadReport()\n");
}

///////////////////////////////////////////////////////////////////////////////
// Called when the filter device object is torn down - releases the VHF device
//
void EvtDeviceContextCleanup(IN WDFOBJECT object) {
    PDEVICE_CONTEXT deviceContext = GetDeviceContext(object);
    if (deviceContext->VhfHandle) {
        DebugPrint("EvtDeviceContextCleanup(): deleting virtual HID device\n");
        VhfDelete(deviceContext->VhfHandle, TRUE);
        deviceContext->VhfHandle = NULL;
    }
}

///////////////////////////////////////////////////////////////////////////////
// All internal device control requests (including raw BT/USB transfers)
// pass through here. We forward everything unchanged; requests carrying a
// raw HID input report transfer get a completion routine attached so we can
// peek (and, later, modify) the report bytes in transit.
//
void EvtIoInternalDeviceControl(IN WDFQUEUE queue, IN WDFREQUEST request, IN size_t outputBufferLength, IN size_t inputBufferLength, IN ULONG ioControlCode) {
    UNREFERENCED_PARAMETER(outputBufferLength);
    UNREFERENCED_PARAMETER(inputBufferLength);

    BOOLEAN bWatch = FALSE;
    PIRP irp = WdfRequestWdmGetIrp(request);
    PIO_STACK_LOCATION irpSp = IoGetCurrentIrpStackLocation(irp);

    if (ioControlCode == IOCTL_INTERNAL_BTH_SUBMIT_BRB) {
        PBRB pbrb = (PBRB)irpSp->Parameters.Others.Argument1;
        if (pbrb && pbrb->BrbHeader.Type == BRB_L2CA_ACL_TRANSFER)
            bWatch = TRUE;
    } else if (ioControlCode == IOCTL_INTERNAL_USB_SUBMIT_URB) {
        PURB purb = (PURB)irpSp->Parameters.Others.Argument1;
        if (purb && purb->UrbHeader.Function == URB_FUNCTION_BULK_OR_INTERRUPT_TRANSFER)
            bWatch = TRUE;
    }

    NTSTATUS status;

    if (bWatch) {
        WdfRequestFormatRequestUsingCurrentType(request);
        WdfRequestSetCompletionRoutine(request, InternalIoctlRequestCompletion, WdfIoQueueGetDevice(queue));

        if (!WdfRequestSend(request, WdfDeviceGetIoTarget(WdfIoQueueGetDevice(queue)), WDF_NO_SEND_OPTIONS)) {
            status = WdfRequestGetStatus(request);
            DebugPrint("EvtIoInternalDeviceControl(): WdfRequestSend failed: 0x%x\n", status);
            WdfRequestComplete(request, status);
        }
        return;
    }

    WDF_REQUEST_SEND_OPTIONS options;
    WDF_REQUEST_SEND_OPTIONS_INIT(&options, WDF_REQUEST_SEND_OPTION_SEND_AND_FORGET);

    if (!WdfRequestSend(request, WdfDeviceGetIoTarget(WdfIoQueueGetDevice(queue)), &options)) {
        status = WdfRequestGetStatus(request);
        DebugPrint("EvtIoInternalDeviceControl(): WdfRequestSend (forget) failed: 0x%x\n", status);
        WdfRequestComplete(request, status);
    }
}

///////////////////////////////////////////////////////////////////////////////
// Completion routine for watched requests - inspects the raw report bytes
//
void InternalIoctlRequestCompletion(IN WDFREQUEST request, IN WDFIOTARGET target, IN PWDF_REQUEST_COMPLETION_PARAMS params, IN WDFCONTEXT context) {
    UNREFERENCED_PARAMETER(target);

    PDEVICE_CONTEXT deviceContext = GetDeviceContext((WDFDEVICE)context);

    DebugPrint("InternalIoctlRequestCompletion()\n");

    if (NT_SUCCESS(params->IoStatus.Status)) {
        PIRP irp = WdfRequestWdmGetIrp(request);
        PIO_STACK_LOCATION irpSp = IoGetCurrentIrpStackLocation(irp);
        ULONG dwControlCode = irpSp->Parameters.DeviceIoControl.IoControlCode;

        if (dwControlCode == IOCTL_INTERNAL_BTH_SUBMIT_BRB) {
            PBRB pbrb = (PBRB)irpSp->Parameters.Others.Argument1;

            if (pbrb && pbrb->BrbHeader.Type == BRB_L2CA_ACL_TRANSFER) {
                BYTE* buf = (BYTE*)pbrb->BrbL2caAclTransfer.Buffer;
                ULONG size = pbrb->BrbL2caAclTransfer.BufferSize;
                DebugPrint("InternalIoctlRequestCompletion BRB_L2CA_ACL_TRANSFER: Buffer = 0x%p, BufferSize = %lu\n", buf, size);

                if (buf) {
                    DebugPrintBuffer("ACL <= ", buf, size);
                    if (size == 11) {
                        ProcessKeyBuffer(buf + 2, 9, deviceContext->VhfHandle);
                    }
                }
            }
        } else if (dwControlCode == IOCTL_INTERNAL_USB_SUBMIT_URB) {
            PURB purb = (PURB)irpSp->Parameters.Others.Argument1;
            if (purb && purb->UrbHeader.Function == URB_FUNCTION_BULK_OR_INTERRUPT_TRANSFER) {
                BYTE* buf = (BYTE*)purb->UrbBulkOrInterruptTransfer.TransferBuffer;
                ULONG size = purb->UrbBulkOrInterruptTransfer.TransferBufferLength;
                DebugPrint("InternalIoctlRequestCompletion URB_FUNCTION_BULK_OR_INTERRUPT_TRANSFER: TransferBuffer = 0x%p, TransferBufferLength = %lu\n", buf, size);

                if (buf) {
                    DebugPrintBuffer("INT <= ", buf, size);
                    if (size == 10) {
                        ProcessKeyBuffer(buf + 1, 9, deviceContext->VhfHandle);
                    }
                }
            }
        }
    }

    WdfRequestComplete(request, params->IoStatus.Status);
}

void KdPrintBuffer(PCHAR text, PUCHAR buffer, ULONG length) {
    if (!buffer || length == 0) return;
    DbgPrintEx(DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, DRIVERNAME ": %s", text ? text : "");
    for (ULONG i = 0; i < length; ++i)
        DbgPrintEx(DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "%02X ", buffer[i]);
    DbgPrintEx(DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "\n");
}

NTSTATUS ReadDriverRegistryValue(PUNICODE_STRING registryPath, DWORD dwRegValeType, PCWSTR wcszValName, PVOID* pValue) {
    PAGED_CODE();

    HANDLE hKey;
    OBJECT_ATTRIBUTES oa;
    InitializeObjectAttributes(&oa, registryPath, OBJ_CASE_INSENSITIVE, NULL, NULL);

    NTSTATUS status = ZwOpenKey(&hKey, KEY_READ, &oa);
    if (!NT_SUCCESS(status)) {
        DebugPrint("Can't open key %wZ - %X\n", registryPath, status);
        return status;
    }

    UNICODE_STRING valname;
    ULONG size = 0;
    RtlInitUnicodeString(&valname, wcszValName);
    status = ZwQueryValueKey(hKey, &valname, KeyValuePartialInformation, NULL, 0, &size);
    if (status != STATUS_OBJECT_NAME_NOT_FOUND && size) {
        PKEY_VALUE_PARTIAL_INFORMATION vp = (PKEY_VALUE_PARTIAL_INFORMATION)ExAllocatePoolWithTag(PagedPool, size, '1gaT');
        if (vp) {
            status = ZwQueryValueKey(hKey, &valname, KeyValuePartialInformation, vp, size, &size);
            if (NT_SUCCESS(status)) {
                if (dwRegValeType == REG_SZ) {
                    PUNICODE_STRING pDst = (PUNICODE_STRING)pValue;
                    UNICODE_STRING src;
                    src.Buffer = (PWSTR)vp->Data;
                    src.Length = (USHORT)vp->DataLength;
                    src.MaximumLength = (USHORT)vp->DataLength;
                    RtlCopyUnicodeString(pDst, &src);
                } else if (dwRegValeType == REG_DWORD)
                    *pValue = (void*)(ULONG_PTR)(*(DWORD*)vp->Data);
                else {
                    DebugPrint("Unsupported registry value type.");
                    status = STATUS_INVALID_PARAMETER;
                }
            } else
                DebugPrint("ZwQueryValueKey(%ws) failed - %X\n", valname.Buffer, status);

            ExFreePool(vp);
        } else {
            DebugPrint("Can't allocate %d bytes for reading registry\n", size);
            status = STATUS_INSUFFICIENT_RESOURCES;
        }
    }

    ZwClose(hKey);
    return status;
}
