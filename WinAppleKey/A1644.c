#include "driver.h"

BOOLEAN g_FakeFnActive = 0;

static BYTE LookupKeyMap(BYTE source)
{
	for (ULONG i = 0; i + 1 < g_KeyMapSize; i += 2)
	{
		if (g_KeyMap[i] == source)
			return g_KeyMap[i + 1];
	}
	return 0;
}

static void InjectKey(BYTE* buf, BYTE keyCode)
{
	for (int i = 2; i <= 7; i++)
	{
		if (!buf[i]) { buf[i] = keyCode; return; }
	}
}

void ProcessKeyBuffer(BYTE* buf, ULONG size)
{
	BYTE* pModifier = &buf[0];
	BYTE* pSpecialKey = &buf[8];

	BOOLEAN fnPressed = (*pSpecialKey & 0x2) != 0;
	BOOLEAN ejectPressed = (*pSpecialKey & 0x1) != 0;
	*pSpecialKey = 0;

	BYTE fnTarget = LookupKeyMap(VIRTUAL_FN);
	if (fnTarget)
		g_FakeFnActive = g_dwFnLock;
	else
		g_FakeFnActive = fnPressed || g_dwFnLock;

	// Optionally process Alt-Cmd swap
	if (g_dwSwapAltCmd)
	{
		if (*pModifier & HidLAltMask)
		{
			*pModifier &= ~HidLAltMask;
			*pModifier |= HidLCmdMask;
		}
		else if (*pModifier & HidLCmdMask)
		{
			*pModifier &= ~HidLCmdMask;
			*pModifier |= HidLAltMask;
		}

		if (*pModifier & HidRAltMask)
		{
			*pModifier &= ~HidRAltMask;
			*pModifier |= HidRCmdMask;
		}
		else if (*pModifier & HidRCmdMask)
		{
			*pModifier &= ~HidRCmdMask;
			*pModifier |= HidRAltMask;
		}
	}

	// Process Fn+[key] combinations
	if (g_FakeFnActive && (buf[2] || *pModifier))
	{
		switch (buf[2])
		{
		case HidLeft: buf[2] = HidHome; break;
		case HidRight: buf[2] = HidEnd; break;
		case HidUp: buf[2] = HidPgUp; break;
		case HidDown: buf[2] = HidPgDown; break;
		case HidEnter: buf[2] = HidInsert; break;
		case HidF1: buf[2] = HidF13; break;
		case HidF2: buf[2] = HidF14; break;
		case HidF3: buf[2] = HidF15; break;
		case HidF4: buf[2] = HidF16; break;
		case HidF5: buf[2] = HidF17; break;
		case HidF6: buf[2] = HidF18; break;
		case HidF7: buf[2] = HidF19; break;
		case HidF8: buf[2] = HidF20; break;
		case HidF9: buf[2] = HidF21; break;
		case HidF10: buf[2] = HidF22; break;
		case HidF11: buf[2] = HidF23; break;
		case HidF12: buf[2] = HidF24; break;
		case HidKeyP: buf[2] = HidPrtScr; break;
		case HidKeyB: buf[2] = HidPauseBreak; break;
		case HidKeyS: buf[2] = HidScrLck; break;
		default:
			if (*pModifier & HidLCtrlMask)
			{
				*pModifier &= ~HidLCtrlMask;
				*pModifier |= HidRCtrlMask;
			}
			else if (fnPressed && !fnTarget)
				RtlZeroMemory(buf, size);
			break;
		}
	}

	// Apply KeyMap to regular keys
	for (int i = 2; i <= 7; i++)
	{
		if (buf[i] && buf[i] < 0xF0)
		{
			BYTE target = LookupKeyMap(buf[i]);
			if (target)
				buf[i] = target;
		}
	}

	// Inject Eject target
	if (ejectPressed)
	{
		BYTE target = LookupKeyMap(VIRTUAL_EJECT);
		if (target)
			InjectKey(buf, target);
	}

	// Inject Fn target
	if (fnPressed && fnTarget)
		InjectKey(buf, fnTarget);
}
