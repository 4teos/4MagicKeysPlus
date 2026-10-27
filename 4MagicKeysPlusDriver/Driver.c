#include "Driver.h"
#include <devpkey.h>

// Upper bounds on the profiles and on the rule subkeys examined, so a damaged key can't keep an enumeration loop running.
#define MAX_PROFILE_SUBKEYS 256
#define MAX_RULE_SUBKEYS    1024

// Room for the instance ID of a device, and for the Identities list of a profile (bytes).
#define DEVICE_ID_BYTES      1024
#define IDENTITIES_MAX_BYTES 4096

///////////////////////////////////////////////////////////////////////////////
// The configuration of the keyboard being loaded: its FnLock and its rules. They are filled from the
// profile of one keyboard and handed to that keyboard at once, so they are only scratch space.
//
static DWORD g_dwFnLock = 0;
static KEY_RULE g_Rules[MAX_RULES] = {0};
static ULONG g_RuleCount = 0;

static WCHAR g_KeyboardsPathBuffer[512];
static UNICODE_STRING g_KeyboardsPath;
static WCHAR g_DefaultsPathBuffer[512];
static UNICODE_STRING g_DefaultsPath;

// Where the configuration of a keyboard came from, for the log.
typedef enum _CONFIGURATION_SOURCE {
    ConfigurationNone,
    ConfigurationProfile,
    ConfigurationDefaults
} CONFIGURATION_SOURCE;

// g_StateLock guards the scratch configuration above, g_DeviceCollection (every filtered
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
// Load every rule subkey of an open Rules key into g_Rules. A subkey is a rule
// only if it has REG_DWORD Source and Target; the subkey name itself is
// irrelevant, and subkeys without a Target are skipped.
//
static void ReadRules(HANDLE hRules) {
    PAGED_CODE();

    g_RuleCount = 0;

    // Registry key names are at most 255 characters.
    ULONG infoSize = sizeof(KEY_BASIC_INFORMATION) + 256 * sizeof(WCHAR);
    PKEY_BASIC_INFORMATION info = (PKEY_BASIC_INFORMATION)ExAllocatePool2(POOL_FLAG_PAGED, infoSize, 'uRyK');
    if (!info) {
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
}

///////////////////////////////////////////////////////////////////////////////
// Whether the Identities list (REG_MULTI_SZ) of an open profile key has this identity.
//
static BOOLEAN ProfileHasIdentity(HANDLE hProfile, const WCHAR* identity) {
    PAGED_CODE();

    ULONG size = sizeof(KEY_VALUE_PARTIAL_INFORMATION) + IDENTITIES_MAX_BYTES;
    PKEY_VALUE_PARTIAL_INFORMATION info = (PKEY_VALUE_PARTIAL_INFORMATION)ExAllocatePool2(POOL_FLAG_PAGED, size, 'dIyK');
    if (!info) {
        return FALSE;
    }

    UNICODE_STRING valueName;
    RtlInitUnicodeString(&valueName, L"Identities");
    ULONG resultLength = 0;
    BOOLEAN found = FALSE;
    NTSTATUS status = ZwQueryValueKey(hProfile, &valueName, KeyValuePartialInformation, info, size, &resultLength);
    if (NT_SUCCESS(status) && info->Type == REG_MULTI_SZ) {
        found = MkpIdentityListContains((const WCHAR*)info->Data, info->DataLength / sizeof(WCHAR), identity) != 0;
    }

    ExFreePool(info);
    return found;
}

///////////////////////////////////////////////////////////////////////////////
// Load the settings under an open key of a profile (or of the factory defaults of a model, which are
// laid out the same way) into g_dwFnLock and g_Rules: its FnLock value and its Rules subkey.
//
static void ReadSettings(HANDLE hSettings) {
    PAGED_CODE();

    ReadDwordValue(hSettings, L"FnLock", &g_dwFnLock);

    UNICODE_STRING rulesName;
    RtlInitUnicodeString(&rulesName, L"Rules");
    OBJECT_ATTRIBUTES rulesOa;
    InitializeObjectAttributes(&rulesOa, &rulesName, OBJ_CASE_INSENSITIVE | OBJ_KERNEL_HANDLE, hSettings, NULL);
    HANDLE hRules;
    if (NT_SUCCESS(ZwOpenKey(&hRules, KEY_READ, &rulesOa))) {
        ReadRules(hRules);
        ZwClose(hRules);
    }
}

///////////////////////////////////////////////////////////////////////////////
// Load the configuration of the keyboard with this identity into g_dwFnLock and g_Rules: that of the
// first profile under Parameters\Keyboards whose Identities has it. Fn Lock is off and there are no
// rules when no profile has it. The caller holds g_StateLock.
//
static BOOLEAN LoadProfile(const WCHAR* identity) {
    PAGED_CODE();

    g_dwFnLock = 0;
    g_RuleCount = 0;
    if (identity[0] == L'\0') {
        return FALSE;
    }

    HANDLE hKeyboards;
    OBJECT_ATTRIBUTES oa;
    InitializeObjectAttributes(&oa, &g_KeyboardsPath, OBJ_CASE_INSENSITIVE | OBJ_KERNEL_HANDLE, NULL, NULL);
    if (!NT_SUCCESS(ZwOpenKey(&hKeyboards, KEY_READ, &oa))) {
        return FALSE;
    }

    // Registry key names are at most 255 characters.
    ULONG infoSize = sizeof(KEY_BASIC_INFORMATION) + 256 * sizeof(WCHAR);
    PKEY_BASIC_INFORMATION info = (PKEY_BASIC_INFORMATION)ExAllocatePool2(POOL_FLAG_PAGED, infoSize, 'uRyK');
    if (!info) {
        ZwClose(hKeyboards);
        return FALSE;
    }

    BOOLEAN found = FALSE;
    for (ULONG index = 0; index < MAX_PROFILE_SUBKEYS && !found; index++) {
        ULONG resultLength = 0;
        NTSTATUS status = ZwEnumerateKey(hKeyboards, index, KeyBasicInformation, info, infoSize, &resultLength);
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

        OBJECT_ATTRIBUTES profileOa;
        InitializeObjectAttributes(&profileOa, &name, OBJ_CASE_INSENSITIVE | OBJ_KERNEL_HANDLE, hKeyboards, NULL);
        HANDLE hProfile;
        if (!NT_SUCCESS(ZwOpenKey(&hProfile, KEY_READ, &profileOa))) {
            continue;
        }

        if (ProfileHasIdentity(hProfile, identity)) {
            found = TRUE;
            ReadSettings(hProfile);
        }
        ZwClose(hProfile);
    }

    ExFreePool(info);
    ZwClose(hKeyboards);
    return found;
}

///////////////////////////////////////////////////////////////////////////////
// Load the factory defaults of a model (its USB product ID) into g_dwFnLock and g_Rules: the subkey
// of Parameters\Defaults named by the product ID as four hex digits. Fn Lock is off and there are no
// rules when the model has none. The caller holds g_StateLock.
//
static BOOLEAN LoadDefaults(ULONG productId) {
    PAGED_CODE();

    g_dwFnLock = 0;
    g_RuleCount = 0;
    if (productId == 0) {
        return FALSE;
    }

    WCHAR path[512];
    if (!NT_SUCCESS(RtlStringCbPrintfW(path, sizeof(path), L"%wZ\\%04lX", &g_DefaultsPath, productId))) {
        return FALSE;
    }

    UNICODE_STRING pathName;
    RtlInitUnicodeString(&pathName, path);
    OBJECT_ATTRIBUTES oa;
    InitializeObjectAttributes(&oa, &pathName, OBJ_CASE_INSENSITIVE | OBJ_KERNEL_HANDLE, NULL, NULL);
    HANDLE hDefaults;
    if (!NT_SUCCESS(ZwOpenKey(&hDefaults, KEY_READ, &oa))) {
        return FALSE;
    }

    ReadSettings(hDefaults);
    ZwClose(hDefaults);
    return TRUE;
}

///////////////////////////////////////////////////////////////////////////////
// Load the configuration of a keyboard into g_dwFnLock and g_Rules: that of its profile, or, when no
// profile has its identity (or it has none), the factory defaults of its model. A profile that is
// there is never mixed with the defaults, even if it has no rules. The caller holds g_StateLock.
//
static CONFIGURATION_SOURCE LoadConfiguration(const WCHAR* identity, ULONG productId) {
    PAGED_CODE();

    if (LoadProfile(identity)) {
        return ConfigurationProfile;
    }
    return LoadDefaults(productId) ? ConfigurationDefaults : ConfigurationNone;
}

///////////////////////////////////////////////////////////////////////////////
// Look up the configuration of every filtered keyboard again and hand it to that keyboard. Returns
// how many keyboards there are; *pRulesRead and *pRulesAccepted are the rules found for them and the
// ones the processors kept, summed over the keyboards. The caller holds g_StateLock.
//
static ULONG ReloadDevices(ULONG* pRulesRead, ULONG* pRulesAccepted) {
    ULONG count = WdfCollectionGetCount(g_DeviceCollection);
    *pRulesRead = 0;
    *pRulesAccepted = 0;

    for (ULONG i = 0; i < count; i++) {
        PDEVICE_CONTEXT context = GetDeviceContext((WDFDEVICE)WdfCollectionGetItem(g_DeviceCollection, i));
        CONFIGURATION_SOURCE source = LoadConfiguration(context->Identity, context->ProductId);
        ULONG accepted = KeyProcessorLoadConfig(context->KeyProcessorHandle, g_dwFnLock, g_Rules, g_RuleCount);
        DebugPrint("Reload: keyboard '%ws': configuration from: %d (0 none, 1 profile, 2 defaults), FnLock=%lu, rules read: %lu, accepted: %lu\n",
            context->Identity, source, g_dwFnLock, g_RuleCount, accepted);
        UNREFERENCED_PARAMETER(source);

        *pRulesRead += g_RuleCount;
        *pRulesAccepted += accepted;
    }
    return count;
}

///////////////////////////////////////////////////////////////////////////////
// Reads a string property of the device's own devnode (an instance ID here) into the buffer,
// which is left empty when the property is not there.
//
static BOOLEAN QueryDeviceString(WDFDEVICE device, const DEVPROPKEY* key, PWCHAR buffer, ULONG bufferBytes) {
    PAGED_CODE();

    RtlZeroMemory(buffer, bufferBytes);

    WDF_DEVICE_PROPERTY_DATA data;
    WDF_DEVICE_PROPERTY_DATA_INIT(&data, key);

    ULONG resultLength = 0;
    DEVPROPTYPE type = 0;
    NTSTATUS status = WdfDeviceQueryPropertyEx(device, &data, bufferBytes - sizeof(WCHAR), buffer, &resultLength, &type);
    if (!NT_SUCCESS(status) || type != DEVPROP_TYPE_STRING) {
        DebugPrint("QueryDeviceString(): the property is not available: 0x%x (type %lu)\n", status, type);
        RtlZeroMemory(buffer, bufferBytes);
        return FALSE;
    }
    return TRUE;
}

///////////////////////////////////////////////////////////////////////////////
// The identity of the keyboard this device is, written to 'identity' (empty when there is none): the
// Bluetooth address, taken from the instance ID of the paired HID service the driver is attached to,
// or the serial number, taken from the instance ID of the USB device that is the parent of the
// interface it is attached to (USB\VID_05AC&PID_026C\<serial>). The same texts as the configuration app's.
// The USB product ID of the model comes from the same instance IDs (*pProductId, 0 when they have none).
//
static void QueryDeviceIdentity(WDFDEVICE device, WCHAR* identity, ULONG* pProductId) {
    PAGED_CODE();

    identity[0] = L'\0';
    *pProductId = 0;

    PWCHAR deviceId = (PWCHAR)ExAllocatePool2(POOL_FLAG_PAGED, DEVICE_ID_BYTES, 'dIyK');
    if (!deviceId) {
        return;
    }

    if (QueryDeviceString(device, &DEVPKEY_Device_InstanceId, deviceId, DEVICE_ID_BYTES)) {
        DebugPrint("QueryDeviceIdentity(): instance ID '%ws'\n", deviceId);
        if (MkpIdentityFromBluetoothDeviceId(deviceId, identity)) {
            *pProductId = MkpProductIdFromBluetoothDeviceId(deviceId);
        } else if (QueryDeviceString(device, &DEVPKEY_Device_Parent, deviceId, DEVICE_ID_BYTES)) {
            DebugPrint("QueryDeviceIdentity(): parent '%ws'\n", deviceId);
            MkpIdentityFromUsbDeviceId(deviceId, identity);
            *pProductId = MkpProductIdFromUsbDeviceId(deviceId);
        }
    }

    ExFreePool(deviceId);
    DebugPrint("QueryDeviceIdentity(): the identity of the keyboard is '%ws', the product ID is 0x%04lX\n", identity, *pProductId);
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
    result.Devices = ReloadDevices(&result.RulesRead, &result.RulesAccepted);
    WdfWaitLockRelease(g_StateLock);

    DebugPrint("Config reloaded: keyboards: %lu, rules read: %lu, accepted: %lu\n",
        result.Devices, result.RulesRead, result.RulesAccepted);

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

    RtlStringCbPrintfW(g_KeyboardsPathBuffer, sizeof(g_KeyboardsPathBuffer), L"%wZ\\Parameters\\Keyboards", registryPath);
    RtlInitUnicodeString(&g_KeyboardsPath, g_KeyboardsPathBuffer);

    RtlStringCbPrintfW(g_DefaultsPathBuffer, sizeof(g_DefaultsPathBuffer), L"%wZ\\Parameters\\Defaults", registryPath);
    RtlInitUnicodeString(&g_DefaultsPath, g_DefaultsPathBuffer);

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

    QueryDeviceIdentity(hDevice, deviceContext->Identity, &deviceContext->ProductId);

    // From here on the device takes part in config reloads. The first keyboard also brings the
    // control device along. The configuration is read afresh: settings saved while no keyboard was
    // connected (so there was no control device to tell) apply to it now.
    WdfWaitLockAcquire(g_StateLock, NULL);
    CONFIGURATION_SOURCE source = LoadConfiguration(deviceContext->Identity, deviceContext->ProductId);
    ULONG rulesAccepted = KeyProcessorLoadConfig(deviceContext->KeyProcessorHandle, g_dwFnLock, g_Rules, g_RuleCount);
    DebugPrint("EvtDriverDeviceAdd(): keyboard '%ws': configuration from: %d (0 none, 1 profile, 2 defaults), FnLock=%lu, rules read: %lu, accepted: %lu\n",
        deviceContext->Identity, source, g_dwFnLock, g_RuleCount, rulesAccepted);
    UNREFERENCED_PARAMETER(source);
    UNREFERENCED_PARAMETER(rulesAccepted);
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
