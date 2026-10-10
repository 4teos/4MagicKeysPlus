#include "Driver.h"

// Upper bound on subkeys examined under Parameters\Rules, so a damaged key can't keep the enumeration loop running.
#define MAX_RULE_SUBKEYS 1024

///////////////////////////////////////////////////////////////////////////////
// Globals Initialisation
//
DWORD g_dwFnLock = 0;
KEY_RULE g_Rules[MAX_RULES] = {0};
ULONG g_RuleCount = 0;

///////////////////////////////////////////////////////////////////////////////
// Read a REG_DWORD value from an open key. *pValue is only written on success.
//
static NTSTATUS ReadDwordValue(HANDLE hKey, PCWSTR wcszValName, DWORD* pValue) {
    PAGED_CODE();

    UNICODE_STRING valname;
    RtlInitUnicodeString(&valname, wcszValName);

    ULONGLONG storage[(sizeof(KEY_VALUE_PARTIAL_INFORMATION) + sizeof(DWORD) + sizeof(ULONGLONG) - 1) / sizeof(ULONGLONG)];
    PKEY_VALUE_PARTIAL_INFORMATION info = (PKEY_VALUE_PARTIAL_INFORMATION)storage;
    ULONG resultLength = 0;
    NTSTATUS status = ZwQueryValueKey(hKey, &valname, KeyValuePartialInformation, info, sizeof(storage), &resultLength);
    if (!NT_SUCCESS(status)) {
        return status;
    }
    if (info->Type != REG_DWORD || info->DataLength != sizeof(DWORD)) {
        return STATUS_OBJECT_TYPE_MISMATCH;
    }

    RtlCopyMemory(pValue, info->Data, sizeof(DWORD));
    return STATUS_SUCCESS;
}

///////////////////////////////////////////////////////////////////////////////
// Load every rule subkey of Parameters\Rules into g_Rules. A subkey is a rule
// only if it has REG_DWORD Source and Target; the subkey name itself is
// irrelevant, and subkeys without a Target (launch-only entries) are skipped.
//
static void ReadRules(PUNICODE_STRING rulesPath) {
    PAGED_CODE();

    g_RuleCount = 0;

    HANDLE hRules;
    OBJECT_ATTRIBUTES oa;
    InitializeObjectAttributes(&oa, rulesPath, OBJ_CASE_INSENSITIVE | OBJ_KERNEL_HANDLE, NULL, NULL);
    if (!NT_SUCCESS(ZwOpenKey(&hRules, KEY_READ, &oa))) {
        return;
    }

    // Registry key names are at most 255 characters.
    ULONG infoSize = sizeof(KEY_BASIC_INFORMATION) + 256 * sizeof(WCHAR);
    PKEY_BASIC_INFORMATION info = (PKEY_BASIC_INFORMATION)ExAllocatePool2(POOL_FLAG_PAGED, infoSize, 'uRyK');
    if (!info) {
        ZwClose(hRules);
        return;
    }

    for (ULONG index = 0; index < MAX_RULE_SUBKEYS && g_RuleCount < MAX_RULES; index++) {
        ULONG resultLength = 0;
        NTSTATUS status = ZwEnumerateKey(hRules, index, KeyBasicInformation, info, infoSize, &resultLength);
        if (status == STATUS_NO_MORE_ENTRIES) {
            break;
        }
        if (!NT_SUCCESS(status)) {
            continue;
        }

        UNICODE_STRING name;
        name.Buffer = info->Name;
        name.Length = (USHORT)info->NameLength;
        name.MaximumLength = (USHORT)info->NameLength;

        OBJECT_ATTRIBUTES ruleOa;
        InitializeObjectAttributes(&ruleOa, &name, OBJ_CASE_INSENSITIVE | OBJ_KERNEL_HANDLE, hRules, NULL);
        HANDLE hRule;
        if (!NT_SUCCESS(ZwOpenKey(&hRule, KEY_READ, &ruleOa))) {
            continue;
        }

        KEY_RULE rule = {0};
        DWORD fnLockAware = 0;
        if (NT_SUCCESS(ReadDwordValue(hRule, L"Source", &rule.Source))
            && NT_SUCCESS(ReadDwordValue(hRule, L"Target", &rule.Target))) {
            ReadDwordValue(hRule, L"FnLockAware", &fnLockAware);
            rule.FnLockAware = fnLockAware != 0;
            g_Rules[g_RuleCount++] = rule;
        }
        ZwClose(hRule);
    }

    ExFreePool(info);
    ZwClose(hRules);
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

    HANDLE hParams;
    OBJECT_ATTRIBUTES paramOa;
    InitializeObjectAttributes(&paramOa, &paramPath, OBJ_CASE_INSENSITIVE | OBJ_KERNEL_HANDLE, NULL, NULL);
    if (NT_SUCCESS(ZwOpenKey(&hParams, KEY_READ, &paramOa))) {
        ReadDwordValue(hParams, L"FnLock", &g_dwFnLock);
        ZwClose(hParams);
    }

    WCHAR rulesPathBuf[512];
    UNICODE_STRING rulesPath;
    RtlStringCbPrintfW(rulesPathBuf, sizeof(rulesPathBuf), L"%wZ\\Rules", &paramPath);
    RtlInitUnicodeString(&rulesPath, rulesPathBuf);
    ReadRules(&rulesPath);

    DebugPrint("FnLock=%lu, rules loaded: %lu\n", g_dwFnLock, g_RuleCount);

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

    deviceContext->KeyProcessorHandle = KeyProcessorCreate();
    if (!deviceContext->KeyProcessorHandle) {
        DebugPrint("EvtDriverDeviceAdd(): KeyProcessorCreate failed\n");
        return STATUS_INSUFFICIENT_RESOURCES;
    }
    KeyProcessorLoadConfig(deviceContext->KeyProcessorHandle, g_dwFnLock, g_Rules, g_RuleCount);

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
// and this device's KeyProcessor instance
//
void EvtDeviceContextCleanup(IN WDFOBJECT object) {
    PDEVICE_CONTEXT deviceContext = GetDeviceContext(object);
    if (deviceContext->VhfHandle) {
        DebugPrint("EvtDeviceContextCleanup(): deleting virtual HID device\n");
        VhfDelete(deviceContext->VhfHandle, TRUE);
        deviceContext->VhfHandle = NULL;
    }
    if (deviceContext->KeyProcessorHandle) {
        KeyProcessorDestroy(deviceContext->KeyProcessorHandle);
        deviceContext->KeyProcessorHandle = NULL;
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
                        KeyProcessorProcess(deviceContext->KeyProcessorHandle, buf + 2, 9, deviceContext->VhfHandle);
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
                        KeyProcessorProcess(deviceContext->KeyProcessorHandle, buf + 1, 9, deviceContext->VhfHandle);
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
