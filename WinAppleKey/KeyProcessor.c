#ifdef TEST_BUILD
#include "test_stubs.h"
#else
#include "driver.h"
#endif

BOOLEAN g_FakeFnActive = 0;

static BYTE LookupKeyMap(BYTE source) {
    for (ULONG i = 0; i + 1 < g_KeyMapSize; i += 2) {
        if (g_KeyMap[i] == source) {
            return g_KeyMap[i + 1];
        }
    }
    return 0;
}

static void InjectKey(BYTE* buf, BYTE keyCode) {
    for (int i = 2; i <= 7; i++) {
        if (!buf[i]) {
            buf[i] = keyCode;
            return;
        }
    }
}

void ProcessKeyBuffer(BYTE* buf, ULONG size) {
    if (!buf || size < 9) {
        return;
    }

   
    BYTE* pSpecialKey = &buf[8];
    BOOLEAN fnPressed = (*pSpecialKey & 0x2) != 0;
    BOOLEAN ejectPressed = (*pSpecialKey & 0x1) != 0;

    if (fnPressed) {
        BYTE fnKeyTarget = LookupKeyMap(VIRTUAL_FN);
        if (fnKeyTarget) {
            fnPressed = FALSE;
            *pSpecialKey &= ~0x2;
            if (fnKeyTarget == VIRTUAL_EJECT) {
                DebugPrint("Fn pressed -> mapped to Eject key");
                ejectPressed = TRUE;
                *pSpecialKey |= 0x1;
            } else {
                DebugPrint("Fn pressed -> mapped to normal key 0x%02X\n", fnKeyTarget);
                InjectKey(buf, fnKeyTarget);
            }
        }
    }

    if (ejectPressed) {
        BYTE ejectKeyTarget = LookupKeyMap(VIRTUAL_EJECT);
        if (ejectKeyTarget) {
            ejectPressed = FALSE;
            *pSpecialKey &= ~0x1;
            if (ejectKeyTarget == VIRTUAL_FN) {
                DebugPrint("Eject pressed -> mapped to Fn key");
                fnPressed = TRUE;
                *pSpecialKey |= 0x2;
            } else {
                DebugPrint("Eject pressed -> mapped to normal key 0x%02X\n", ejectKeyTarget);
                InjectKey(buf, ejectKeyTarget);
            }
        }
    }

    BYTE* pModifier = &buf[0];

    if (fnPressed || ejectPressed || *pModifier || buf[2]) {
        DebugPrint("Input Report: mod=0x%02X, fn=%d, eject=%d, key1=0x%02X\n",
            *pModifier, fnPressed, ejectPressed, buf[2]);
    }

    // Apply modifier remapping
    if (g_ModMapSize >= 2) {
        BYTE original = *pModifier;
        BYTE mappedMask = 0;
        for (ULONG i = 0; i + 1 < g_ModMapSize; i += 2)
            mappedMask |= g_ModMap[i];

        *pModifier = original & ~mappedMask;
        for (ULONG i = 0; i + 1 < g_ModMapSize; i += 2) {
            if (original & g_ModMap[i])
                *pModifier |= g_ModMap[i + 1];
        }
        if (original != *pModifier) {
            DebugPrint("ModMap applied: 0x%02X -> 0x%02X\n", original, *pModifier);
        }
    }

//  FnLock behavior:
//  FnLock == 1 : F1‑F12 act as standard function keys. Holding physical Fn (or a key remapped to Fn) gives extra‑functions (like multimedia control)
//  FnLock == 0 : F1‑F12 act as extra‑function by default. Holding physical Fn reverts them to standard function keys.
    if (g_dwFnLock) {
        for (int i = 2; i <= 7; i++) {
            if (buf[i] >= HidF1 && buf[i] <= HidF12) {
                DebugPrint("FnLock behavior applied for key: 0x%02X\n", buf[i]);
                fnPressed = !fnPressed;
                *pSpecialKey ^= 0x2;
                break;
            }
        }
    }
    
    // Process hardcoded Fn+[key] combinations for navigation/editing keys
    if (fnPressed) {
        BYTE oldKey = buf[2];
        switch (buf[2]) {
            case HidLeft: 
                buf[2] = HidHome; 
                break;
            case HidRight: 
                buf[2] = HidEnd; 
                break;
            case HidUp: 
                buf[2] = HidPgUp; 
                break;
            case HidDown: 
                buf[2] = HidPgDown; 
                break;
            case HidEnter: 
                buf[2] = HidInsert; 
                break;
            case HidKeyP: 
                buf[2] = HidPrtScr; 
                break;
            case HidKeyB: 
                buf[2] = HidPauseBreak; 
                break;
            case HidKeyS: 
                buf[2] = HidScrLck; 
                break;
        }

        if (oldKey != buf[2]) {
            DebugPrint("Fn combo: 0x%02X -> 0x%02X\n", oldKey, buf[2]);
        }
    }

    // Apply KeyMap to regular keys
    for (int i = 2; i <= 7; i++) {
        if (buf[i] && buf[i] < 0xF0) {
            BYTE target = LookupKeyMap(buf[i]);
            if (target) {
                DebugPrint("KeyMap slot %d: 0x%02X -> 0x%02X\n", i - 2, buf[i], target);
                buf[i] = target;
            }
        }
    }
}
