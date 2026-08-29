#include "KeyProcessor.h"

// ntddk.h (pulled in by KeyProcessorTypes.h for KEYPROCESSOR_KERNEL_BUILD)
// already makes memcpy available as a compiler intrinsic - <cstring> would
// pull in vcruntime headers that clash with the kernel CRT headers next to it
// on the include path.
#if !defined(KEYPROCESSOR_KERNEL_BUILD)
#include <cstring>
#endif

namespace {
    ULONG ClampSize(ULONG size, ULONG maxSize) {
        return size > maxSize ? maxSize : size;
    }
}

void KeyProcessor::LoadConfig(DWORD fnLock,
        const BYTE* keyMap, ULONG keyMapSize,
        const BYTE* modMap, ULONG modMapSize,
        const BYTE* specialModMap, ULONG specialModMapSize) {
    m_FnLock = fnLock;

    m_KeyMapSize = ClampSize(keyMapSize, MAX_KEYMAP_SIZE);
    memcpy(m_KeyMap, keyMap, m_KeyMapSize);

    m_ModMapSize = ClampSize(modMapSize, MAX_MODMAP_SIZE);
    memcpy(m_ModMap, modMap, m_ModMapSize);

    m_SpecialModMapSize = ClampSize(specialModMapSize, MAX_SPECIAL_MODMAP_SIZE);
    memcpy(m_SpecialModMap, specialModMap, m_SpecialModMapSize);
}

BYTE KeyProcessor::LookupKeyMap(BYTE source) const {
    for (ULONG i = 0; i + 1 < m_KeyMapSize; i += 2) {
        if (m_KeyMap[i] == source) {
            return m_KeyMap[i + 1];
        }
    }
    return 0;
}

void KeyProcessor::InjectKey(BYTE* buf, BYTE keyCode) {
    for (int i = 2; i <= 7; i++) {
        if (!buf[i]) {
            buf[i] = keyCode;
            return;
        }
    }
}

BOOLEAN KeyProcessor::IsSpecialVirtualKey(BYTE keyCode) {
    char keyIndex = keyCode - VIRTUAL_EJECT;
    size_t arraySize = sizeof(g_SpecialKeyCodes) / sizeof(g_SpecialKeyCodes[0]);
    return keyIndex >= 0 && (size_t)keyIndex < arraySize;
}

USHORT KeyProcessor::ConsumerUsageForKey(BYTE keyCode) {
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
void KeyProcessor::ProcessModifiers(BYTE* pModifier) const {
    if (m_ModMapSize < 2) {
        return;
    }

    BYTE original = *pModifier;
    BYTE mappedMask = 0;
    for (ULONG i = 0; i + 1 < m_ModMapSize; i += 2)
        mappedMask |= m_ModMap[i];

    *pModifier = original & ~mappedMask;
    for (ULONG i = 0; i + 1 < m_ModMapSize; i += 2) {
        if (original & m_ModMap[i])
            *pModifier |= m_ModMap[i + 1];
    }

    if (original != *pModifier) {
        DebugPrint("ModMap applied: 0x%02X -> 0x%02X\n", original, *pModifier);
    }
}

// Apply remapping between real modifiers and the virtual Fn/Eject keys, in either
// direction. Each m_SpecialModMap pair has exactly one real-modifier side and one
// VIRTUAL_FN/VIRTUAL_EJECT side; IsSpecialVirtualKey tells them apart per pair.
void KeyProcessor::ProcessSpecialModifiers(BYTE* pModifier, BOOLEAN* pFnPressed, BOOLEAN* pEjectPressed) const {
    if (m_SpecialModMapSize < 2) {
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
    for (ULONG i = 0; i + 1 < m_SpecialModMapSize; i += 2) {
        if (!IsSpecialVirtualKey(m_SpecialModMap[i]) && IsSpecialVirtualKey(m_SpecialModMap[i + 1])) {
            consumedMask |= m_SpecialModMap[i];
        }
    }

    BYTE result = original & ~consumedMask;
    BOOLEAN newFn = originalFn;
    BOOLEAN newEject = originalEject;

    for (ULONG i = 0; i + 1 < m_SpecialModMapSize; i += 2) {
        BYTE source = m_SpecialModMap[i];
        BYTE target = m_SpecialModMap[i + 1];

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
void KeyProcessor::ProcessHardcodedFnBehaviorForKey(BYTE* buf, ULONG pos, BOOLEAN fnPressed) {
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
USHORT KeyProcessor::ProcessFKey(BYTE* buf, ULONG pos, BOOLEAN fnPressed, USHORT currentConsumerUsage) const {
    BYTE key = buf[pos];

    if (key < HidF1 || key > HidF12 || (m_FnLock != 0) != (fnPressed != FALSE)) {
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
void KeyProcessor::ProcessNormalKey(BYTE* buf, ULONG pos) const {
    BYTE key = buf[pos];
    BYTE targetKey = LookupKeyMap(key);
    if (targetKey) {
        DebugPrint("Key mapping: 0x%02X -> 0x%02X\n", key, targetKey);
        buf[pos] = targetKey;
    }
}

// Decides whether the caller should submit a Consumer Control usage report.
// Submits only on a state transition (including the transition back to
// CONSUMER_USAGE_NONE) - Volume/Brightness/Scan Next/Previous are Re-Trigger
// Controls, so the host keeps repeating the action for as long as the reported
// value stays non-zero, and will only stop once it sees a report with 0. Without
// ever sending that release report the action would repeat indefinitely, which
// happened to be masked for Play/Pause only because it is a One-Shot Control
// that fires once per transition regardless of how long the value stays set.
ConsumerUsageSubmission KeyProcessor::DecideConsumerUsageSubmission(USHORT usage) {
    if (usage == m_LastConsumerUsage) {
        return { false, usage };
    }
    m_LastConsumerUsage = usage;
    return { true, usage };
}

ConsumerUsageSubmission KeyProcessor::Process(BYTE* buf, ULONG size) {
    if (!buf || size < 9) {
        return { false, CONSUMER_USAGE_NONE };
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
        consumerUsage = ProcessFKey(buf, i, fnPressed, consumerUsage);
        ProcessNormalKey(buf, i);
    }

    return DecideConsumerUsageSubmission(consumerUsage);
}
