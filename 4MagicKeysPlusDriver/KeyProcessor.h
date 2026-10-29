#pragma once

#include "KeyProcessorTypes.h"

struct ConsumerUsageSubmission {
    bool ShouldSubmit;
    USHORT Usage;
};

class KeyProcessor {
public:
    KeyProcessor() = default;

    // Keeps the consumer-usage state, so a media key held across a reload is still released.
    ULONG LoadConfig(DWORD fnLock, const KEY_RULE* rules, ULONG ruleCount);

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
    void ProcessModifiersAndVirtualKeys(BYTE* buf, BOOLEAN* pFnPressed, BYTE specialKeys, BYTE* injectedKeys, ULONG* pInjectedCount) const;
    USHORT ProcessKeys(BYTE* buf, BOOLEAN fnPressed) const;
    ConsumerUsageSubmission DecideConsumerUsageSubmission(USHORT usage);

    DWORD m_FnLock = 0;
    ParsedRule m_Rules[MAX_RULES] = {};
    ULONG m_RuleCount = 0;
    USHORT m_LastConsumerUsage = CONSUMER_USAGE_NONE;
};
