#include "Driver.h"

#ifdef ALLOC_PRAGMA
	#pragma alloc_text (INIT, DriverEntry)
	#pragma alloc_text (PAGE, DriverUnload)
	#pragma alloc_text (PAGE, AddDevice)
	#pragma alloc_text (PAGE, RemoveDevice)
	#pragma alloc_text (PAGE, GetLowerDeviceType)
#endif // ALLOC_PRAGMA

///////////////////////////////////////////////////////////////////////////////
// Globals Initialisation
//
DWORD g_dwFnLock = 0;
BYTE g_KeyMap[MAX_KEYMAP_SIZE] = { 0 };
ULONG g_KeyMapSize = 0;
BYTE g_ModMap[MAX_MODMAP_SIZE] = { 0 };
ULONG g_ModMapSize = 0;

///////////////////////////////////////////////////////////////////////////////
// Read REG_BINARY value from registry into a buffer
//
static NTSTATUS ReadBinaryRegistryValue(PUNICODE_STRING registryPath, PCWSTR wcszValName, BYTE* destBuf, ULONG destMaxSize, ULONG* pOutSize)
{
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
	if (status != STATUS_OBJECT_NAME_NOT_FOUND && size)
	{
		PKEY_VALUE_PARTIAL_INFORMATION vp = (PKEY_VALUE_PARTIAL_INFORMATION)ExAllocatePoolWithTag(PagedPool, size, '1gaT');
		if (vp)
		{
			status = ZwQueryValueKey(hKey, &valname, KeyValuePartialInformation, vp, size, &size);
			if (NT_SUCCESS(status) && vp->Type == REG_BINARY && vp->DataLength <= destMaxSize)
			{
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
NTSTATUS DriverEntry(IN PDRIVER_OBJECT driverObject, IN PUNICODE_STRING registryPath)
{
	DebugPrint("DriverEntry(): driverObject = 0x%x\n", driverObject);

	driverObject->DriverUnload = DriverUnload;
	driverObject->DriverExtension->AddDevice = AddDevice;

	for (int i = 0; i < IRP_MJ_MAXIMUM_FUNCTION; i++)
		driverObject->MajorFunction[i] = DispatchAny;

	driverObject->MajorFunction[IRP_MJ_POWER] = DispatchPower;
	driverObject->MajorFunction[IRP_MJ_PNP] = DispatchPnp;

	driverObject->MajorFunction[IRP_MJ_INTERNAL_DEVICE_CONTROL] = DispatchInternalIoctl;

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

void DriverUnload(IN PDRIVER_OBJECT driverObject)
{
	PAGED_CODE();

	DebugPrint("DriverUnload(): driverObject = 0x%x\n", driverObject);
}

NTSTATUS AddDevice(IN PDRIVER_OBJECT driverObject, IN PDEVICE_OBJECT pdo)
{
	PAGED_CODE();

	DebugPrint("AddDevice(): driverObject: 0x%x, pdo: 0x%x\n", driverObject, pdo);

	PDEVICE_OBJECT fido;
	ULONG type = GetLowerDeviceType(pdo);
	NTSTATUS status = IoCreateDevice(driverObject, sizeof(DEVICE_EXTENSION), NULL, type, 0, FALSE, &fido);
	if (!NT_SUCCESS(status))
	{
		DebugPrint("AddDevice(): IoCreateDevice failed: 0x%x\n", status);
		return status;
	}

	PDEVICE_EXTENSION fidoExt = (PDEVICE_EXTENSION)fido->DeviceExtension;
	do
	{
		IoInitializeRemoveLock(&fidoExt->RemoveLock, 0, 0, 0);
		fidoExt->DeviceObject = fido;
		fidoExt->Pdo = pdo;

		PDEVICE_OBJECT fdo = IoAttachDeviceToDeviceStack(fido, pdo);
		if (!fdo)
		{
			DebugPrint("AddDevice(): IoAttachDeviceToDeviceStack failed\n");
			status = STATUS_DEVICE_REMOVED;
			break;
		}
		fidoExt->LowerDeviceObject = fdo;

		fido->Flags |= fdo->Flags & (DO_DIRECT_IO | DO_BUFFERED_IO | DO_POWER_PAGABLE);
		fido->Flags &= ~DO_DEVICE_INITIALIZING;

	} while (FALSE);

	if (!NT_SUCCESS(status))
	{
		if (fidoExt->LowerDeviceObject)
			IoDetachDevice(fidoExt->LowerDeviceObject);

		IoDeleteDevice(fido);
	}

	return status;
}

void RemoveDevice(IN PDEVICE_OBJECT fido)
{
	PAGED_CODE();

	PDEVICE_EXTENSION fidoExt = (PDEVICE_EXTENSION)fido->DeviceExtension;
	if (fidoExt->LowerDeviceObject)
		IoDetachDevice(fidoExt->LowerDeviceObject);

	IoDeleteDevice(fido);
}

ULONG GetLowerDeviceType(PDEVICE_OBJECT pdo)
{
	PDEVICE_OBJECT lowerPdo = IoGetAttachedDeviceReference(pdo);
	if (!lowerPdo)
		return FILE_DEVICE_UNKNOWN;

	ULONG devtype = lowerPdo->DeviceType;
	ObDereferenceObject(lowerPdo);

	return devtype;
}

NTSTATUS CompleteRequest(IN PIRP irp, IN NTSTATUS status, IN ULONG_PTR info)
{
	irp->IoStatus.Status = status;
	irp->IoStatus.Information = info;
	IoCompleteRequest(irp, IO_NO_INCREMENT);

	return status;
}

NTSTATUS DispatchPower(IN PDEVICE_OBJECT fido, IN PIRP irp)
{
	PDEVICE_EXTENSION pdx = (PDEVICE_EXTENSION)fido->DeviceExtension;
	PoStartNextPowerIrp(irp);

	NTSTATUS status = IoAcquireRemoveLock(&pdx->RemoveLock, irp);
	if (!NT_SUCCESS(status))
		return CompleteRequest(irp, status, 0);

	IoSkipCurrentIrpStackLocation(irp);

	status = PoCallDriver(pdx->LowerDeviceObject, irp);

	IoReleaseRemoveLock(&pdx->RemoveLock, irp);

	return status;
}

NTSTATUS DispatchPnp(IN PDEVICE_OBJECT fido, IN PIRP irp)
{
	PIO_STACK_LOCATION stack = IoGetCurrentIrpStackLocation(irp);
	ULONG fcn = stack->MinorFunction;
	PDEVICE_EXTENSION pdx = (PDEVICE_EXTENSION)fido->DeviceExtension;

	NTSTATUS status = IoAcquireRemoveLock(&pdx->RemoveLock, irp);
	if (!NT_SUCCESS(status))
		return CompleteRequest(irp, status, 0);

	if (fcn == IRP_MN_DEVICE_USAGE_NOTIFICATION)
	{
		if (!fido->AttachedDevice || (fido->AttachedDevice->Flags & DO_POWER_PAGABLE))
			fido->Flags |= DO_POWER_PAGABLE;

		IoCopyCurrentIrpStackLocationToNext(irp);
		IoSetCompletionRoutine(irp, (PIO_COMPLETION_ROUTINE)UsageNotificationCompletionRoutine, (PVOID)pdx, TRUE, TRUE, TRUE);

		return IoCallDriver(pdx->LowerDeviceObject, irp);
	}

	if (fcn == IRP_MN_START_DEVICE)
	{
		IoCopyCurrentIrpStackLocationToNext(irp);

		IoSetCompletionRoutine(irp, (PIO_COMPLETION_ROUTINE)StartDeviceCompletionRoutine, (PVOID)pdx, TRUE, TRUE, TRUE);

		return IoCallDriver(pdx->LowerDeviceObject, irp);
	}

	if (fcn == IRP_MN_REMOVE_DEVICE)
	{
		IoSkipCurrentIrpStackLocation(irp);

		status = IoCallDriver(pdx->LowerDeviceObject, irp);

		IoReleaseRemoveLockAndWait(&pdx->RemoveLock, irp);

		RemoveDevice(fido);

		return status;
	}

	IoSkipCurrentIrpStackLocation(irp);
	status = IoCallDriver(pdx->LowerDeviceObject, irp);

	IoReleaseRemoveLock(&pdx->RemoveLock, irp);

	return status;
}

NTSTATUS StartDeviceCompletionRoutine(PDEVICE_OBJECT fido, PIRP irp, PDEVICE_EXTENSION pdx)
{
	if (irp->PendingReturned)
		IoMarkIrpPending(irp);

	if (pdx->LowerDeviceObject->Characteristics & FILE_REMOVABLE_MEDIA)
		fido->Characteristics |= FILE_REMOVABLE_MEDIA;

	IoReleaseRemoveLock(&pdx->RemoveLock, irp);
	return STATUS_SUCCESS;
}

NTSTATUS UsageNotificationCompletionRoutine(PDEVICE_OBJECT fido, PIRP irp, PDEVICE_EXTENSION pdx)
{
	if (irp->PendingReturned)
		IoMarkIrpPending(irp);

	if (!(pdx->LowerDeviceObject->Flags & DO_POWER_PAGABLE))
		fido->Flags &= ~DO_POWER_PAGABLE;

	IoReleaseRemoveLock(&pdx->RemoveLock, irp);

	return STATUS_SUCCESS;
}

NTSTATUS DispatchAny(IN PDEVICE_OBJECT fido, IN PIRP irp)
{
	PDEVICE_EXTENSION devExt = (PDEVICE_EXTENSION)fido->DeviceExtension;
	NTSTATUS status;

	status = IoAcquireRemoveLock(&devExt->RemoveLock, irp);
	if (!NT_SUCCESS(status))
		return CompleteRequest(irp, status, 0);

	IoSkipCurrentIrpStackLocation(irp);

	status = IoCallDriver(devExt->LowerDeviceObject, irp);

	IoReleaseRemoveLock(&devExt->RemoveLock, irp);

	return status;
}


NTSTATUS InternalIoctlComplete(IN PDEVICE_OBJECT fido, IN PIRP irp, IN PVOID context)
{
	UNREFERENCED_PARAMETER(fido);
	UNREFERENCED_PARAMETER(context);
	DebugPrint("InternalIoctlComplete()\n");

	PIO_STACK_LOCATION irpSp = IoGetCurrentIrpStackLocation(irp);
	if (NT_SUCCESS(irp->IoStatus.Status))
	{
		ULONG dwControlCode = irpSp->Parameters.DeviceIoControl.IoControlCode;
		if (dwControlCode == IOCTL_INTERNAL_BTH_SUBMIT_BRB)
		{
			PBRB pbrb = (PBRB)irpSp->Parameters.Others.Argument1;

			if (pbrb && pbrb->BrbHeader.Type == BRB_L2CA_ACL_TRANSFER)
			{
				BYTE* buf = (BYTE*)pbrb->BrbL2caAclTransfer.Buffer;
				ULONG size = pbrb->BrbL2caAclTransfer.BufferSize;
				DebugPrint("InternalIoctlComplete BRB_L2CA_ACL_TRANSFER: Buffer = 0x%x, BufferSize = %lu\n", buf, size);

				if (buf && size == 11)
				{
					DebugPrintBuffer("ProcessKeyBuffer(): <= ", buf, size);
					//ProcessKeyBuffer(buf + 2, 9);
					DebugPrintBuffer("ProcessKeyBuffer(): => ", buf, size);
				}
			}
		}
		else if (dwControlCode == IOCTL_INTERNAL_USB_SUBMIT_URB)
		{
			PURB purb = (PURB)irpSp->Parameters.Others.Argument1;
			if (purb && purb->UrbHeader.Function == URB_FUNCTION_BULK_OR_INTERRUPT_TRANSFER)
			{
				BYTE* buf = (BYTE*)purb->UrbBulkOrInterruptTransfer.TransferBuffer;
				ULONG size = purb->UrbBulkOrInterruptTransfer.TransferBufferLength;
				DebugPrint("InternalIoctlComplete URB_FUNCTION_BULK_OR_INTERRUPT_TRANSFER: TransferBuffer = 0x%x, TransferBufferLength = %lu\n", buf, size);

				if (buf && size == 10)
				{
					DebugPrintBuffer("ProcessKeyBuffer(): <= ", buf, size);
					//ProcessKeyBuffer(buf + 1, 9);
					DebugPrintBuffer("ProcessKeyBuffer(): => ", buf, size);
				}
			}
		}
	}

	if (irp->PendingReturned)
		IoMarkIrpPending(irp);

	PDEVICE_EXTENSION devExt = (PDEVICE_EXTENSION)fido->DeviceExtension;
	IoReleaseRemoveLock(&devExt->RemoveLock, irp);

	return irp->IoStatus.Status;
}

NTSTATUS DispatchInternalIoctl(IN PDEVICE_OBJECT fido, IN PIRP irp)
{
	PDEVICE_EXTENSION devExt = (PDEVICE_EXTENSION)fido->DeviceExtension;
	PIO_STACK_LOCATION irpSp = IoGetCurrentIrpStackLocation(irp);
	ULONG dwControlCode = irpSp->Parameters.DeviceIoControl.IoControlCode;
	BOOLEAN bCompletionSet = FALSE;

	NTSTATUS status = IoAcquireRemoveLock(&devExt->RemoveLock, irp);
	if (!NT_SUCCESS(status))
		return CompleteRequest(irp, status, 0);

	if (dwControlCode == IOCTL_INTERNAL_BTH_SUBMIT_BRB)
	{
		PBRB pbrb = (PBRB)irpSp->Parameters.Others.Argument1;
		if (pbrb && pbrb->BrbHeader.Type == BRB_L2CA_ACL_TRANSFER)
		{
			IoCopyCurrentIrpStackLocationToNext(irp);
			IoSetCompletionRoutine(irp, InternalIoctlComplete, fido, TRUE, TRUE, TRUE);
			bCompletionSet = TRUE;
		}
		else
			IoSkipCurrentIrpStackLocation(irp);
	}
	else if (dwControlCode == IOCTL_INTERNAL_USB_SUBMIT_URB)
	{
		PURB purb = (PURB)irpSp->Parameters.Others.Argument1;
		if (purb && purb->UrbHeader.Function == URB_FUNCTION_BULK_OR_INTERRUPT_TRANSFER)
		{
			IoCopyCurrentIrpStackLocationToNext(irp);
			IoSetCompletionRoutine(irp, InternalIoctlComplete, fido, TRUE, TRUE, TRUE);
			bCompletionSet = TRUE;
		}
		else
			IoSkipCurrentIrpStackLocation(irp);
	}
	else
		IoSkipCurrentIrpStackLocation(irp);

	status = IoCallDriver(devExt->LowerDeviceObject, irp);

	if (!bCompletionSet)
	{
		IoReleaseRemoveLock(&devExt->RemoveLock, irp);
	}

	return status;
}

void KdPrintBuffer(PCHAR text, PUCHAR buffer, ULONG length)
{
	if (!buffer || length == 0) return;
	DbgPrintEx(DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, DRIVERNAME ": %s", text ? text : "");
	for (ULONG i = 0; i < length; ++i)
		DbgPrintEx(DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "%02X ", buffer[i]);
	DbgPrintEx(DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "\n");
}

NTSTATUS ReadDriverRegistryValue(PUNICODE_STRING registryPath, DWORD dwRegValeType, PCWSTR wcszValName, PVOID* pValue)
{
	PAGED_CODE();

	HANDLE hKey;
	OBJECT_ATTRIBUTES oa;
	InitializeObjectAttributes(&oa, registryPath, OBJ_CASE_INSENSITIVE, NULL, NULL);

	NTSTATUS status = ZwOpenKey(&hKey, KEY_READ, &oa);
	if (!NT_SUCCESS(status))
	{
		DebugPrint("Can't open key %wZ - %X\n", registryPath, status);
		return status;
	}

	UNICODE_STRING valname;
	ULONG size = 0;
	RtlInitUnicodeString(&valname, wcszValName);
	status = ZwQueryValueKey(hKey, &valname, KeyValuePartialInformation, NULL, 0, &size);
	if (status != STATUS_OBJECT_NAME_NOT_FOUND && size)
	{
		PKEY_VALUE_PARTIAL_INFORMATION vp = (PKEY_VALUE_PARTIAL_INFORMATION)ExAllocatePoolWithTag(PagedPool, size, '1gaT');
		if (vp)
		{
			status = ZwQueryValueKey(hKey, &valname, KeyValuePartialInformation, vp, size, &size);
			if (NT_SUCCESS(status))
			{
				if (dwRegValeType == REG_SZ)
				{
					PUNICODE_STRING pDst = (PUNICODE_STRING)pValue;
					UNICODE_STRING src;
					src.Buffer = (PWSTR)vp->Data;
					src.Length = (USHORT)vp->DataLength;
					src.MaximumLength = (USHORT)vp->DataLength;
					RtlCopyUnicodeString(pDst, &src);
				}
				else if (dwRegValeType == REG_DWORD)
					*pValue = (void*)(ULONG_PTR)(*(DWORD*)vp->Data);
				else
				{
					DebugPrint("Unsupported registry value type.");
					status = STATUS_INVALID_PARAMETER;
				}
			}
			else
				DebugPrint("ZwQueryValueKey(%ws) failed - %X\n", valname.Buffer, status);

			ExFreePool(vp);
		}
		else
		{
			DebugPrint("Can't allocate %d bytes for reading registry\n", size);
			status = STATUS_INSUFFICIENT_RESOURCES;
		}
	}

	ZwClose(hKey);
	return status;
}
