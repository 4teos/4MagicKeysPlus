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

// Owns per-device remapping state (the rule list plus the last-submitted
// consumer usage) and turns a raw HID report buffer into its remapped form.
// One instance per physical device - see DEVICE_CONTEXT - so that two
// keyboards plugged in at once don't share consumer-usage dedup state with
// each other.
class KeyProcessor {
public:
    KeyProcessor() = default;

    // Rules that can't be interpreted (see ClassifyRule) are dropped here.
    void LoadConfig(DWORD fnLock, const KEY_RULE* rules, ULONG ruleCount);

    ConsumerUsageSubmission Process(BYTE* buf, ULONG size);

private:
    enum RuleKind : BYTE {
        RuleInvalid,
        RuleModToMod,
        RuleModToVirtual,
        RuleVirtualToMod,
        RuleVirtualToVirtual,
        RuleVirtualToKey,
        RuleKeyToOutput,
    };

    struct ParsedRule {
        DWORD Source;
        DWORD Target;
        RuleKind Kind;
        BOOLEAN FnLockAware;
    };

    static RuleKind ClassifyRule(DWORD source, DWORD target);
    static BOOLEAN IsSpecialVirtualKey(DWORD code);
    static void InjectKey(BYTE* buf, BYTE keyCode);
    const ParsedRule* FindKeyRule(BYTE key, BOOLEAN fnLayer) const;
    BOOLEAN IsFnLayerActive(const ParsedRule& rule, BOOLEAN fnPressed) const;
    void ProcessModifiersAndVirtualKeys(BYTE* buf, BOOLEAN* pFnPressed, BOOLEAN ejectPressed, BYTE* injectedKeys, ULONG* pInjectedCount) const;
    USHORT ProcessKeys(BYTE* buf, BOOLEAN fnPressed) const;
    ConsumerUsageSubmission DecideConsumerUsageSubmission(USHORT usage);

    DWORD m_FnLock = 0;
    ParsedRule m_Rules[MAX_RULES] = {};
    ULONG m_RuleCount = 0;
    USHORT m_LastConsumerUsage = CONSUMER_USAGE_NONE;
};
