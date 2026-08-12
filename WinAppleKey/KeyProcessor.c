#include "driver.h"

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

static BOOLEAN IsSpecialVirtualKey(BYTE keyCode) {
    switch (keyCode) {
        case VIRTUAL_FN:
        case VIRTUAL_EJECT:
            return TRUE;
        default:
            return FALSE;
    }
}

static USHORT ConsumerUsageForKey(BYTE keyCode) {
    switch (keyCode) {
        case HidF1:
            return CONSUMER_USAGE_BRIGHT_DOWN;
        case HidF2:
            return CONSUMER_USAGE_BRIGHT_UP;
        case HidF7:
            return CONSUMER_USAGE_PREV;
        case HidF8:
            return CONSUMER_USAGE_PLAYPAUSE;
        case HidF9:
            return CONSUMER_USAGE_NEXT;
        case HidF10:
            return CONSUMER_USAGE_MUTE;
        case HidF11:
            return CONSUMER_VOLUME_DOWN;
        case HidF12:
            return CONSUMER_VOLUME_UP;
        default:
            return CONSUMER_USAGE_NONE;
    }
}

// Apply modifiers remapping
static void ProcessModifiers(BYTE* pModifier) {
    if (g_ModMapSize < 2) {
        return;
    }

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

// Apply remapping between real modifiers and the virtual Fn/Eject keys, in either
// direction. Each g_SpecialModMap pair has exactly one real-modifier side and one
// VIRTUAL_FN/VIRTUAL_EJECT side; IsSpecialVirtualKey tells them apart per pair.
static void ProcessSpecialModifiers(BYTE* pModifier, BOOLEAN* pFnPressed, BOOLEAN* pEjectPressed) {
    if (g_SpecialModMapSize < 2) {
        return;
    }

    BYTE original = *pModifier;
    BOOLEAN originalFn = *pFnPressed;
    BOOLEAN originalEject = *pEjectPressed;

    // Only the SOURCE side of a modifier->special rule gets cleared up front. A
    // special->modifier TARGET bit must stay untouched here, otherwise an
    // independent press of that same physical key would be wiped even when the
    // Fn/Eject source isn't held.
    BYTE consumedMask = 0;
    for (ULONG i = 0; i + 1 < g_SpecialModMapSize; i += 2) {
        if (!IsSpecialVirtualKey(g_SpecialModMap[i]) && IsSpecialVirtualKey(g_SpecialModMap[i + 1])) {
            consumedMask |= g_SpecialModMap[i];
        }
    }

    BYTE result = original & ~consumedMask;
    BOOLEAN newFn = originalFn;
    BOOLEAN newEject = originalEject;

    for (ULONG i = 0; i + 1 < g_SpecialModMapSize; i += 2) {
        BYTE source = g_SpecialModMap[i];
        BYTE target = g_SpecialModMap[i + 1];

        if (!IsSpecialVirtualKey(source) && IsSpecialVirtualKey(target)) {
            // modifier -> Fn/Eject
            if (original & source) {
                if (target == VIRTUAL_FN) {
                    newFn = TRUE;
                } else {
                    newEject = TRUE;
                }
            }
        } else if (IsSpecialVirtualKey(source) && !IsSpecialVirtualKey(target)) {
            // Fn/Eject -> modifier
            BOOLEAN sourcePressed = (source == VIRTUAL_FN) ? originalFn : originalEject;
            if (sourcePressed) {
                result |= target;
                if (source == VIRTUAL_FN) {
                    newFn = FALSE;
                } else {
                    newEject = FALSE;
                }
            }
        }
    }

    *pModifier = result;
    *pFnPressed = newFn;
    *pEjectPressed = newEject;

    if (original != *pModifier || originalFn != *pFnPressed || originalEject != *pEjectPressed) {
        DebugPrint("SpecialModMap applied: mod 0x%02X -> 0x%02X, Fn %d -> %d, Eject %d -> %d\n",
            original, *pModifier, originalFn, *pFnPressed, originalEject, *pEjectPressed);
    }
}

// Apply hardcoded remappings with the Fn key pressed
static void ProcessHardcodedFnBehaviorForKey(BYTE* buf, ULONG pos, BOOLEAN fnPressed) {
    if (!fnPressed) {
        return;
    }

    BYTE key = buf[pos];
    switch (key) {
        case HidLeft:
            buf[pos] = HidHome;
            break;
        case HidRight:
            buf[pos] = HidEnd;
            break;
        case HidUp:
            buf[pos] = HidPgUp;
            break;
        case HidDown:
            buf[pos] = HidPgDown;
            break;
        case HidEnter:
            buf[pos] = HidInsert;
            break;
        case HidKeyP:
            buf[pos] = HidPrtScr;
            break;
        case HidKeyB:
            buf[pos] = HidPauseBreak;
            break;
        case HidKeyS:
            buf[pos] = HidScrLck;
            break;
    }

    if (key != buf[pos]) {
        DebugPrint("Hardcoded Fn combo: 0x%02X -> 0x%02X\n", key, buf[pos]);
    }
}

// Apply F key behaviors. F keys contain extra functions (volume control, multimedia control, etc.)
// and behave depending on the Fn key and FnLock behavior.
static USHORT ProcessFKey(BYTE* buf, ULONG pos, BOOLEAN fnPressed, BOOLEAN fnLocked, USHORT currentConsumerUsage) {
    BYTE key = buf[pos];

    if (key < HidF1 || key > HidF12 || fnLocked != fnPressed) {
        // nothing to do because it is not an F key or it should act like a regular F key
        return CONSUMER_USAGE_NONE;
    }

    if (currentConsumerUsage != CONSUMER_USAGE_NONE) {
        return currentConsumerUsage;
    }

    USHORT consumerUsage = ConsumerUsageForKey(key);
    if (consumerUsage != CONSUMER_USAGE_NONE) {
        buf[pos] = HidKeyNone;
        DebugPrint("Extra function for F key: 0x%02X -> 0x%04X\n", key, consumerUsage);
    }

    return consumerUsage;
}

// Apply normal key remappings
static void ProcessNormalKey(BYTE* buf, ULONG pos) {
    BYTE key = buf[pos];
    BYTE targetKey = LookupKeyMap(key);
    if (targetKey) {
        DebugPrint("Key mapping: 0x%02X -> 0x%02X\n", key, targetKey);
        buf[pos] = targetKey;
    }
}

static USHORT g_LastConsumerUsage = CONSUMER_USAGE_NONE;

// Submits the consumer usage action through the virtual Consumer Control device.
// Submits only on a state transition (including the transition back to
// CONSUMER_USAGE_NONE) - Volume/Brightness/Scan Next/Previous are Re-Trigger
// Controls, so the host keeps repeating the action for as long as the reported
// value stays non-zero, and will only stop once it sees a report with 0. Without
// ever sending that release report the action would repeat indefinitely, which
// happened to be masked for Play/Pause only because it is a One-Shot Control
// that fires once per transition regardless of how long the value stays set.
static void SubmitConsumerUsageForMediaKey(USHORT usage, VHFHANDLE vhfHandle) {
    if (!vhfHandle || usage == g_LastConsumerUsage) {
        return;
    }
    g_LastConsumerUsage = usage;

    // reportBuffer must start with the report ID byte itself, followed by the report data.
    UCHAR reportBuffer[3] = {2, (UCHAR)(usage & 0xFF), (UCHAR)(usage >> 8)};
    HID_XFER_PACKET packet;
    packet.reportBuffer = reportBuffer;
    packet.reportBufferLen = sizeof(reportBuffer);
    packet.reportId = 2;

    NTSTATUS status = VhfReadReportSubmit(vhfHandle, &packet);
    if (!NT_SUCCESS(status)) {
        DebugPrint("Submit consumer usage: VhfReadReportSubmit failed: 0x%x\n", status);
    } else {
        DebugPrint("Submit consumer usage: Success. Usage 0x%04X\n", usage);
    }
}

void ProcessKeyBuffer(BYTE* buf, ULONG size, VHFHANDLE vhfHandle) {
    if (!buf || size < 9) {
        return;
    }

    BYTE* pSpecialKey = &buf[8];
    BOOLEAN fnPressed = (*pSpecialKey & 0x2) != 0;
    BOOLEAN ejectPressed = (*pSpecialKey & 0x1) != 0;
    *pSpecialKey = 0;

    if (fnPressed) {
        BYTE fnKeyTarget = LookupKeyMap(VIRTUAL_FN);
        if (fnKeyTarget) {
            fnPressed = FALSE;
            if (fnKeyTarget == VIRTUAL_EJECT) {
                DebugPrint("Fn pressed -> mapped to Eject key");
                ejectPressed = TRUE;
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
            if (ejectKeyTarget == VIRTUAL_FN) {
                DebugPrint("Eject pressed -> mapped to Fn key");
                fnPressed = TRUE;
            } else {
                DebugPrint("Eject pressed -> mapped to normal key 0x%02X\n", ejectKeyTarget);
                InjectKey(buf, ejectKeyTarget);
            }
        }
    }

    ProcessModifiers(&buf[0]);
    ProcessSpecialModifiers(&buf[0], &fnPressed, &ejectPressed);

    USHORT consumerUsage = CONSUMER_USAGE_NONE;
    for (int i = 2; i <= 7; i++) {
        if (!buf[i]) {
            continue;
        }

        ProcessHardcodedFnBehaviorForKey(buf, i, fnPressed);
        consumerUsage = ProcessFKey(buf, i, fnPressed, g_dwFnLock != 0, consumerUsage);
        ProcessNormalKey(buf, i);
    }

    SubmitConsumerUsageForMediaKey(consumerUsage, vhfHandle);
}
