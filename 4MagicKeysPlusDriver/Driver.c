#include "Driver.h"

// Upper bound on subkeys examined under Parameters\Rules, so a damaged key can't keep the enumeration loop running.
#define MAX_RULE_SUBKEYS 1024

///////////////////////////////////////////////////////////////////////////////
// Globals Initialisation
//
DWORD g_dwFnLock = 0;
KEY_RULE g_Rules[MAX_RULES] = {0};
ULONG g_RuleCount = 0;

static WCHAR g_ParametersPathBuffer[512];
static WCHAR g_RulesPathBuffer[512];
static UNICODE_STRING g_ParametersPath;
static UNICODE_STRING g_RulesPath;

// g_StateLock guards the configuration globals above, g_DeviceCollection (every filtered
// keyboard's WDFDEVICE) and g_ControlDevice, so that a reload and a keyboard being added or
// removed never see each other half done.
static WDFWAITLOCK g_StateLock;
static WDFCOLLECTION g_DeviceCollection;
static WDFDEVICE g_ControlDevice;

DECLARE_CONST_UNICODE_STRING(g_ControlDeviceName, MKP_CONTROL_DEVICE_NAME);
DECLARE_CONST_UNICODE_STRING(g_ControlSymbolicLink, MKP_CONTROL_SYMLINK_NAME);
// Only SYSTEM and administrators may open the control device.
DECLARE_CONST_UNICODE_STRING(g_ControlSddl, L"D:P(A;;GA;;;SY)(A;;GA;;;BA)");

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
// (Re)read FnLock and the rules from the registry into the globals. A missing value or
// key means the defaults: FnLock off, no rules. The caller serializes access to the globals.
//
static void LoadConfiguration(void) {
    PAGED_CODE();

    g_dwFnLock = 0;

    HANDLE hParams;
    OBJECT_ATTRIBUTES paramOa;
    InitializeObjectAttributes(&paramOa, &g_ParametersPath, OBJ_CASE_INSENSITIVE | OBJ_KERNEL_HANDLE, NULL, NULL);
    if (NT_SUCCESS(ZwOpenKey(&hParams, KEY_READ, &paramOa))) {
        ReadDwordValue(hParams, L"FnLock", &g_dwFnLock);
        ZwClose(hParams);
    }

    ReadRules(&g_RulesPath);
}

///////////////////////////////////////////////////////////////////////////////
// Hand the current configuration to every filtered keyboard. Returns how many there are;
// *pAccepted is how many rules the processors kept. The caller holds g_StateLock.
//
static ULONG ApplyConfigurationToDevices(ULONG* pAccepted) {
    ULONG count = WdfCollectionGetCount(g_DeviceCollection);
    *pAccepted = 0;

    for (ULONG i = 0; i < count; i++) {
        PDEVICE_CONTEXT context = GetDeviceContext((WDFDEVICE)WdfCollectionGetItem(g_DeviceCollection, i));
        *pAccepted = KeyProcessorLoadConfig(context->KeyProcessorHandle, g_dwFnLock, g_Rules, g_RuleCount);
    }
    return count;
}

///////////////////////////////////////////////////////////////////////////////
// Control device: lets the configuration app tell the driver to reload its rules.
//
static VOID EvtControlIoDeviceControl(IN WDFQUEUE queue, IN WDFREQUEST request, IN size_t outputBufferLength, IN size_t inputBufferLength, IN ULONG ioControlCode) {
    UNREFERENCED_PARAMETER(queue);
    UNREFERENCED_PARAMETER(inputBufferLength);

    if (ioControlCode != IOCTL_MKP_RELOAD_CONFIG) {
        WdfRequestComplete(request, STATUS_INVALID_DEVICE_REQUEST);
        return;
    }

    MKP_RELOAD_RESULT result = {0};

    WdfWaitLockAcquire(g_StateLock, NULL);
    LoadConfiguration();
    result.RulesRead = g_RuleCount;
    result.Devices = ApplyConfigurationToDevices(&result.RulesAccepted);
    WdfWaitLockRelease(g_StateLock);

    DebugPrint("Config reloaded: FnLock=%lu, rules read: %lu, accepted: %lu, keyboards: %lu\n",
        g_dwFnLock, result.RulesRead, result.RulesAccepted, result.Devices);

    size_t bytesWritten = 0;
    NTSTATUS status = STATUS_SUCCESS;
    if (outputBufferLength >= sizeof(result)) {
        PVOID output;
        status = WdfRequestRetrieveOutputBuffer(request, sizeof(result), &output, NULL);
        if (NT_SUCCESS(status)) {
            RtlCopyMemory(output, &result, sizeof(result));
            bytesWritten = sizeof(result);
        }
    }
    WdfRequestCompleteWithInformation(request, status, bytesWritten);
}

///////////////////////////////////////////////////////////////////////////////
// Creates the control device. Called with g_StateLock held, when the first keyboard appears;
// a failure only costs live reload, so the caller carries on without it.
//
static NTSTATUS CreateControlDevice(WDFDRIVER driver) {
    PAGED_CODE();

    PWDFDEVICE_INIT init = WdfControlDeviceInitAllocate(driver, &g_ControlSddl);
    if (!init) {
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    WdfDeviceInitSetExclusive(init, FALSE);
    NTSTATUS status = WdfDeviceInitAssignName(init, &g_ControlDeviceName);
    if (!NT_SUCCESS(status)) {
        WdfDeviceInitFree(init);
        return status;
    }

    WDF_OBJECT_ATTRIBUTES attributes;
    WDF_OBJECT_ATTRIBUTES_INIT(&attributes);
    attributes.ExecutionLevel = WdfExecutionLevelPassive;

    WDFDEVICE controlDevice;
    status = WdfDeviceCreate(&init, &attributes, &controlDevice);
    if (!NT_SUCCESS(status)) {
        if (init) {
            WdfDeviceInitFree(init);
        }
        return status;
    }

    WDF_IO_QUEUE_CONFIG queueConfig;
    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&queueConfig, WdfIoQueueDispatchSequential);
    queueConfig.EvtIoDeviceControl = EvtControlIoDeviceControl;

    status = WdfIoQueueCreate(controlDevice, &queueConfig, WDF_NO_OBJECT_ATTRIBUTES, WDF_NO_HANDLE);
    if (NT_SUCCESS(status)) {
        status = WdfDeviceCreateSymbolicLink(controlDevice, &g_ControlSymbolicLink);
    }
    if (!NT_SUCCESS(status)) {
        WdfObjectDelete(controlDevice);
        return status;
    }

    WdfControlFinishInitializing(controlDevice);
    g_ControlDevice = controlDevice;
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

    status = WdfWaitLockCreate(WDF_NO_OBJECT_ATTRIBUTES, &g_StateLock);
    if (NT_SUCCESS(status)) {
        status = WdfCollectionCreate(WDF_NO_OBJECT_ATTRIBUTES, &g_DeviceCollection);
    }
    if (!NT_SUCCESS(status)) {
        DebugPrint("DriverEntry(): creating the device list failed: 0x%x\n", status);
        return status;
    }

    RtlStringCbPrintfW(g_ParametersPathBuffer, sizeof(g_ParametersPathBuffer), L"%wZ\\Parameters", registryPath);
    RtlInitUnicodeString(&g_ParametersPath, g_ParametersPathBuffer);
    RtlStringCbPrintfW(g_RulesPathBuffer, sizeof(g_RulesPathBuffer), L"%wZ\\Rules", &g_ParametersPath);
    RtlInitUnicodeString(&g_RulesPath, g_RulesPathBuffer);

    LoadConfiguration();

    DebugPrint("FnLock=%lu, rules loaded: %lu\n", g_dwFnLock, g_RuleCount);

    return STATUS_SUCCESS;
}

///////////////////////////////////////////////////////////////////////////////
// EvtDriverDeviceAdd - creates the KMDF filter device object and its I/O queue
//
NTSTATUS EvtDriverDeviceAdd(IN WDFDRIVER driver, IN PWDFDEVICE_INIT deviceInit) {
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

    // From here on the device takes part in config reloads. The first keyboard also brings the
    // control device along. The registry is read afresh: rules saved while no keyboard was
    // connected (so there was no control device to tell) apply to it now.
    WdfWaitLockAcquire(g_StateLock, NULL);
    LoadConfiguration();
    KeyProcessorLoadConfig(deviceContext->KeyProcessorHandle, g_dwFnLock, g_Rules, g_RuleCount);
    status = WdfCollectionAdd(g_DeviceCollection, hDevice);
    if (NT_SUCCESS(status)) {
        deviceContext->InDeviceCollection = TRUE;
        if (!g_ControlDevice) {
            NTSTATUS controlStatus = CreateControlDevice(driver);
            if (!NT_SUCCESS(controlStatus)) {
                DebugPrint("EvtDriverDeviceAdd(): the control device was not created: 0x%x\n", controlStatus);
            }
        }
    }
    WdfWaitLockRelease(g_StateLock);
    if (!NT_SUCCESS(status)) {
        DebugPrint("EvtDriverDeviceAdd(): WdfCollectionAdd failed: 0x%x\n", status);
        return status;
    }

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

    // The last keyboard takes the control device with it. It is deleted outside the lock: that
    // waits for a running IOCTL, which may itself be waiting for the lock.
    WDFDEVICE controlDevice = NULL;
    WdfWaitLockAcquire(g_StateLock, NULL);
    if (deviceContext->InDeviceCollection) {
        WdfCollectionRemove(g_DeviceCollection, object);
        deviceContext->InDeviceCollection = FALSE;
        if (WdfCollectionGetCount(g_DeviceCollection) == 0) {
            controlDevice = g_ControlDevice;
            g_ControlDevice = NULL;
        }
    }
    WdfWaitLockRelease(g_StateLock);
    if (controlDevice) {
        WdfObjectDelete(controlDevice);
    }

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
        if (pbrb && pbrb->BrbHeader.Type == BRB_L2CA_ACL_TRANSFER) {
            bWatch = TRUE;
        }
    } else if (ioControlCode == IOCTL_INTERNAL_USB_SUBMIT_URB) {
        PURB purb = (PURB)irpSp->Parameters.Others.Argument1;
        if (purb && purb->UrbHeader.Function == URB_FUNCTION_BULK_OR_INTERRUPT_TRANSFER) {
            bWatch = TRUE;
        }
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
    if (!buffer || length == 0) {
        return;
    }
    DbgPrintEx(DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, DRIVERNAME ": %s", text ? text : "");
    for (ULONG i = 0; i < length; ++i) {
        DbgPrintEx(DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "%02X ", buffer[i]);
    }
    DbgPrintEx(DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "\n");
}
