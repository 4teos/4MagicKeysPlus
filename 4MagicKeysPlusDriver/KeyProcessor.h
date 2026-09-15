#pragma once

// Pure remapping logic - no kernel-only headers (ntddk.h/wdf.h/vhf.h), so this
// file compiles unchanged into both the driver (KeyProcessorInterop.cpp wraps
// it for kernel use) and the user-mode KeyProcessor.Tests project.

#include "KeyProcessorTypes.h"

// Result of Process(): whether the caller should submit a Consumer Control
// usage report, and which usage. Submission itself needs VHFHANDLE/VHF APIs
// (kernel-only), so it happens outside this class - see
// KeyProcessorInterop.cpp's KeyProcessorProcess().
struct ConsumerUsageSubmission {
    bool ShouldSubmit;
    USHORT Usage;
};

// Owns per-device remapping state (KeyMap/ModMap/SpecialModMap config plus the
// last-submitted consumer usage) and turns a raw HID report buffer into its
// remapped form. One instance per physical device - see DEVICE_CONTEXT -
// so that two keyboards plugged in at once don't share consumer-usage
// dedup state with each other.
class KeyProcessor {
public:
    KeyProcessor() = default;

    void LoadConfig(DWORD fnLock,
        const BYTE* keyMap, ULONG keyMapSize,
        const BYTE* modMap, ULONG modMapSize,
        const BYTE* specialModMap, ULONG specialModMapSize);

    ConsumerUsageSubmission Process(BYTE* buf, ULONG size);

private:
    BYTE LookupKeyMap(BYTE source) const;
    static void InjectKey(BYTE* buf, BYTE keyCode);
    static BOOLEAN IsSpecialVirtualKey(BYTE keyCode);
    static USHORT ConsumerUsageForKey(BYTE keyCode);
    void ProcessModifiers(BYTE* pModifier) const;
    void ProcessSpecialModifiers(BYTE* pModifier, BOOLEAN* pFnPressed, BOOLEAN* pEjectPressed) const;
    static void ProcessHardcodedFnBehaviorForKey(BYTE* buf, ULONG pos, BOOLEAN fnPressed);
    USHORT ProcessFKey(BYTE* buf, ULONG pos, BOOLEAN fnPressed, USHORT currentConsumerUsage) const;
    void ProcessNormalKey(BYTE* buf, ULONG pos) const;
    ConsumerUsageSubmission DecideConsumerUsageSubmission(USHORT usage);

    DWORD m_FnLock = 0;
    BYTE m_KeyMap[MAX_KEYMAP_SIZE] = {};
    ULONG m_KeyMapSize = 0;
    BYTE m_ModMap[MAX_MODMAP_SIZE] = {};
    ULONG m_ModMapSize = 0;
    BYTE m_SpecialModMap[MAX_SPECIAL_MODMAP_SIZE] = {};
    ULONG m_SpecialModMapSize = 0;
    USHORT m_LastConsumerUsage = CONSUMER_USAGE_NONE;
};
