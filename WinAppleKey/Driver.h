#pragma once

#include <ntddk.h>
#include <wdm.h>
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

	///////////////////////////////////////////////////////////////////////////////
	// Globals

	#define MAX_MODMAP_SIZE 16

	extern DWORD g_dwFnLock;
	extern BYTE g_KeyMap[MAX_KEYMAP_SIZE];
	extern ULONG g_KeyMapSize;
	extern BYTE g_ModMap[MAX_MODMAP_SIZE];
	extern ULONG g_ModMapSize;

	enum HidCodes
	{
		HidKeyNone = 0x0,
		HidKeyErrOvf = 0x1,
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
		HidDel = 0x4c,
		HidLeft = 0x50,
		HidHome = 0x4a,
		HidRight = 0x4f,
		HidEnd = 0x4d,
		HidUp = 0x52,
		HidPgUp = 0x4b,
		HidDown = 0x51,
		HidPgDown = 0x4e,
		HidEnter = 0x28,
		HidPrtScr = 0x46,
		HidScrLck = 0x47,
		HidPauseBreak = 0x48,
		HidInsert = 0x49,
		HidLCtrlMask = 0x1,
		HidRCtrlMask = 0x10,
		HidLAltMask = 0x4,
		HidRAltMask = 0x40,
		HidLCmdMask = 0x8,
		HidRCmdMask = 0x80,
		HidLShiftMask = 0x2,
		HidRShiftMask = 0x20
	};

	typedef struct tagDEVICE_EXTENSION
	{
		PDEVICE_OBJECT DeviceObject;
		PDEVICE_OBJECT LowerDeviceObject;
		PDEVICE_OBJECT Pdo;
		IO_REMOVE_LOCK RemoveLock;
	} DEVICE_EXTENSION, *PDEVICE_EXTENSION;

	///////////////////////////////////////////////////////////////////////////////
	// Global functions

	NTSTATUS DriverEntry(IN PDRIVER_OBJECT driverObject, IN PUNICODE_STRING registryPath);
	void DriverUnload(IN PDRIVER_OBJECT fido);
	void RemoveDevice(IN PDEVICE_OBJECT fdo);
	NTSTATUS CompleteRequest(IN PIRP Irp, IN NTSTATUS status, IN ULONG_PTR info);
	NTSTATUS AddDevice(IN PDRIVER_OBJECT driver, IN PDEVICE_OBJECT pdo);
	NTSTATUS DispatchAny(IN PDEVICE_OBJECT fido, IN PIRP irp);
	NTSTATUS DispatchPower(IN PDEVICE_OBJECT fido, IN PIRP irp);
	NTSTATUS DispatchPnp(IN PDEVICE_OBJECT fido, IN PIRP irp);
	ULONG GetLowerDeviceType(PDEVICE_OBJECT pdo);
	NTSTATUS StartDeviceCompletionRoutine(PDEVICE_OBJECT fido, PIRP irp, PDEVICE_EXTENSION pdx);
	NTSTATUS UsageNotificationCompletionRoutine(PDEVICE_OBJECT fido, PIRP irp, PDEVICE_EXTENSION pdx);
	NTSTATUS InternalIoctlComplete(IN PDEVICE_OBJECT fido, IN PIRP irp, IN PVOID context);
	NTSTATUS DispatchInternalIoctl(IN PDEVICE_OBJECT fido, IN PIRP irp);
	void KdPrintBuffer(PCHAR text, PUCHAR buffer, ULONG length);
	NTSTATUS ReadDriverRegistryValue(PUNICODE_STRING registryPath, DWORD dwRegValeType, PCWSTR wcszValName, PVOID* pValue);

	void ProcessKeyBuffer(BYTE* pbuf, ULONG size);

#ifdef __cplusplus
}
#endif
