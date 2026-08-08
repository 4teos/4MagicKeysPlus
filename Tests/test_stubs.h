#pragma once

#include <string.h>

typedef unsigned char BYTE;
typedef unsigned long ULONG;
typedef unsigned long DWORD;
typedef int BOOLEAN;
#define TRUE 1
#define FALSE 0
#define RtlZeroMemory(dst, len) memset(dst, 0, len)

#define MAX_KEYMAP_SIZE 128
#define MAX_MODMAP_SIZE 16
#define VIRTUAL_EJECT 0xF0
#define VIRTUAL_FN    0xF1
#define DebugPrint(...) ((void)0)
#define DebugPrintBuffer(...) ((void)0)

extern DWORD g_dwFnLock;
extern BYTE g_KeyMap[MAX_KEYMAP_SIZE];
extern ULONG g_KeyMapSize;
extern BYTE g_ModMap[MAX_MODMAP_SIZE];
extern ULONG g_ModMapSize;

enum HidCodes
{
	HidKeyNone = 0x0, HidKeyErrOvf = 0x1,
	HidKeyB = 0x5, HidKeyP = 0x13, HidKeyS = 0x16, HidKeyT = 0x17,
	HidF1 = 0x3a, HidF2 = 0x3b, HidF3 = 0x3c, HidF4 = 0x3d,
	HidF5 = 0x3e, HidF6 = 0x3f, HidF7 = 0x40, HidF8 = 0x41,
	HidF9 = 0x42, HidF10 = 0x43, HidF11 = 0x44, HidF12 = 0x45,
	HidF13 = 0x68, HidF14 = 0x69, HidF15 = 0x6a,
	HidF16 = 0x6b, HidF17 = 0x6c, HidF18 = 0x6d, HidF19 = 0x6e,
	HidF20 = 0x6f, HidF21 = 0x70, HidF22 = 0x71, HidF23 = 0x72, HidF24 = 0x73,
	HidDel = 0x4c,
	HidLeft = 0x50, HidHome = 0x4a, HidRight = 0x4f, HidEnd = 0x4d,
	HidUp = 0x52, HidPgUp = 0x4b, HidDown = 0x51, HidPgDown = 0x4e,
	HidEnter = 0x28, HidPrtScr = 0x46, HidScrLck = 0x47,
	HidPauseBreak = 0x48, HidInsert = 0x49,
	HidLCtrlMask = 0x1, HidRCtrlMask = 0x10,
	HidLAltMask = 0x4, HidRAltMask = 0x40,
	HidLCmdMask = 0x8, HidRCmdMask = 0x80,
	HidLShiftMask = 0x2, HidRShiftMask = 0x20
};

void ProcessKeyBuffer(BYTE* pbuf, ULONG size);
