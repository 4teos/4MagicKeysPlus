#include "KeyProcessor.h"

namespace {
    const ULONG kMaxInjectedKeys = 3;

    BYTE ModsOf(DWORD value) {
        return (BYTE)((value & RULE_MODS_MASK) >> RULE_MODS_SHIFT);
    }

    DWORD CodeOf(DWORD value) {
        return value & RULE_CODE_MASK;
    }

    BYTE SpecialKeyBit(DWORD code) {
        switch (code) {
            case VIRTUAL_EJECT:
                return SPECIAL_KEY_EJECT;
            case VIRTUAL_FN:
                return SPECIAL_KEY_FN;
            case VIRTUAL_LOCK:
                return SPECIAL_KEY_LOCK;
            default:
                return 0;
        }
    }
}

BOOLEAN KeyProcessor::IsSpecialVirtualKey(DWORD code) {
    return code == VIRTUAL_EJECT || code == VIRTUAL_FN || code == VIRTUAL_LOCK;
}

KeyProcessor::RuleKind KeyProcessor::ClassifyRule(DWORD source, DWORD target) {
    DWORD sourceFlags = source & RULE_FLAGS_MASK;
    DWORD targetFlags = target & RULE_FLAGS_MASK;
    if ((sourceFlags & ~RULE_SOURCE_FN) != 0 || (targetFlags & ~RULE_TARGET_CONSUMER) != 0) {
        return RuleInvalid;
    }

    BOOLEAN sourceFn = sourceFlags != 0;
    BOOLEAN targetConsumer = targetFlags != 0;
    DWORD sourceCode = CodeOf(source);
    DWORD targetCode = CodeOf(target);
    BYTE sourceMods = ModsOf(source);
    BYTE targetMods = ModsOf(target);

    if (sourceCode > 0xFF) {
        return RuleInvalid;
    }

    BOOLEAN sourceIsMod = sourceCode == 0 && sourceMods != 0;
    BOOLEAN sourceIsVirtual = IsSpecialVirtualKey(sourceCode);
    BOOLEAN sourceIsKey = sourceCode != 0 && !sourceIsVirtual && sourceMods == 0;

    if (sourceIsMod || sourceIsVirtual) {
        if (sourceFn || targetConsumer || (sourceIsVirtual && sourceMods != 0)) {
            return RuleInvalid;
        }
        if (targetCode == 0) {
            if (targetMods == 0) {
                return RuleInvalid;
            }
            return sourceIsMod ? RuleModToMod : RuleVirtualToMod;
        }
        if (IsSpecialVirtualKey(targetCode)) {
            if (targetMods != 0 || targetCode == VIRTUAL_LOCK) {
                return RuleInvalid;
            }
            return sourceIsMod ? RuleModToVirtual : RuleVirtualToVirtual;
        }
        if (sourceIsVirtual && targetCode <= 0xFF) {
            return RuleVirtualToKey;
        }
        return RuleInvalid;
    }

    if (sourceIsKey) {
        if (targetConsumer) {
            return (targetCode != 0 && targetCode <= CONSUMER_USAGE_MAX) ? RuleKeyToOutput : RuleInvalid;
        }
        if (targetCode > 0xFF || IsSpecialVirtualKey(targetCode) || (targetCode == 0 && targetMods == 0)) {
            return RuleInvalid;
        }
        return RuleKeyToOutput;
    }

    return RuleInvalid;
}

ULONG KeyProcessor::LoadConfig(DWORD fnLock, const KEY_RULE* rules, ULONG ruleCount) {
    m_FnLock = fnLock;
    m_RuleCount = 0;

    for (ULONG i = 0; i < ruleCount && m_RuleCount < MAX_RULES; i++) {
        RuleKind kind = ClassifyRule(rules[i].Source, rules[i].Target);
        if (kind == RuleInvalid) {
            DebugPrint("Rule ignored: source 0x%08X target 0x%08X\n", rules[i].Source, rules[i].Target);
            continue;
        }
        m_Rules[m_RuleCount++] = {rules[i].Source, rules[i].Target, kind, rules[i].FnLockAware};
    }
    return m_RuleCount;
}

void KeyProcessor::InjectKey(BYTE* buf, BYTE keyCode) {
    for (int i = 2; i <= 7; i++) {
        if (!buf[i]) {
            buf[i] = keyCode;
            return;
        }
    }
}

// Decides from the original state only, never from what an earlier rule of the same pass produced: independent rules
// must not chain into each other. Only the source side of a rule is cleared, the target bits are only added.
void KeyProcessor::ProcessModifiersAndVirtualKeys(BYTE* buf, BOOLEAN* pFnPressed, BYTE specialKeys, BYTE* injectedKeys, ULONG* pInjectedCount) const {
    const BYTE originalMods = buf[0];
    const BOOLEAN originalFn = *pFnPressed;

    BYTE consumedMods = 0;
    BYTE producedMods = 0;
    BOOLEAN consumedFn = FALSE;
    BOOLEAN producedFn = FALSE;
    BYTE appliedSpecialKeys = 0;

    for (ULONG i = 0; i < m_RuleCount; i++) {
        const ParsedRule& rule = m_Rules[i];
        BYTE sourceMods = ModsOf(rule.Source);
        BYTE targetMods = ModsOf(rule.Target);
        DWORD targetCode = CodeOf(rule.Target);

        switch (rule.Kind) {
            case RuleModToMod:
                consumedMods |= sourceMods;
                if (originalMods & sourceMods) {
                    producedMods |= targetMods;
                }
                break;
            case RuleModToVirtual:
                consumedMods |= sourceMods;
                if ((originalMods & sourceMods) && targetCode == VIRTUAL_FN) {
                    producedFn = TRUE;
                }
                break;
            case RuleVirtualToMod:
            case RuleVirtualToVirtual:
            case RuleVirtualToKey: {
                DWORD sourceCode = CodeOf(rule.Source);
                BYTE sourceBit = SpecialKeyBit(sourceCode);
                if (!(specialKeys & sourceBit) || (appliedSpecialKeys & sourceBit)) {
                    break;
                }
                appliedSpecialKeys |= sourceBit;
                buf[8] &= (BYTE)~sourceBit;
                if (sourceCode == VIRTUAL_FN) {
                    consumedFn = TRUE;
                }
                producedMods |= targetMods;
                if (rule.Kind == RuleVirtualToVirtual) {
                    if (targetCode == VIRTUAL_FN) {
                        producedFn = TRUE;
                    }
                } else if (rule.Kind == RuleVirtualToKey && *pInjectedCount < kMaxInjectedKeys) {
                    injectedKeys[(*pInjectedCount)++] = (BYTE)targetCode;
                }
                break;
            }
            default:
                break;
        }
    }

    buf[0] = (originalMods & ~consumedMods) | producedMods;
    *pFnPressed = (originalFn && !consumedFn) || producedFn;

    if (originalMods != buf[0] || originalFn != *pFnPressed) {
        DebugPrint("Modifier rules applied: mod 0x%02X -> 0x%02X, Fn %d -> %d\n",
            originalMods, buf[0], originalFn, *pFnPressed);
    }
}

BOOLEAN KeyProcessor::IsFnLayerActive(const ParsedRule& rule, BOOLEAN fnPressed) const {
    BOOLEAN active = fnPressed != FALSE;
    if (rule.FnLockAware && m_FnLock == 0) {
        active = !active;
    }
    return active;
}

const KeyProcessor::ParsedRule* KeyProcessor::FindKeyRule(BYTE key, BOOLEAN fnLayer) const {
    for (ULONG i = 0; i < m_RuleCount; i++) {
        const ParsedRule& rule = m_Rules[i];
        if (rule.Kind == RuleKeyToOutput
            && CodeOf(rule.Source) == key
            && ((rule.Source & RULE_SOURCE_FN) != 0) == (fnLayer != FALSE)) {
            return &rule;
        }
    }
    return nullptr;
}

USHORT KeyProcessor::ProcessKeys(BYTE* buf, BOOLEAN fnPressed) const {
    USHORT consumerUsage = CONSUMER_USAGE_NONE;
    BYTE extraMods = 0;

    for (int i = 2; i <= 7; i++) {
        BYTE key = buf[i];
        if (!key) {
            continue;
        }

        const ParsedRule* rule = FindKeyRule(key, TRUE);
        if (!rule || !IsFnLayerActive(*rule, fnPressed)) {
            rule = FindKeyRule(key, FALSE);
        }
        if (!rule) {
            continue;
        }

        DWORD targetCode = CodeOf(rule->Target);
        if (rule->Target & RULE_TARGET_CONSUMER) {
            buf[i] = HidKeyNone;
            if (consumerUsage == CONSUMER_USAGE_NONE) {
                consumerUsage = (USHORT)targetCode;
            }
            DebugPrint("Key 0x%02X -> consumer usage 0x%04X\n", key, targetCode);
        } else {
            buf[i] = (BYTE)targetCode;
            extraMods |= ModsOf(rule->Target);
            DebugPrint("Key 0x%02X -> 0x%02X\n", key, targetCode);
        }
    }

    buf[0] |= extraMods;
    return consumerUsage;
}

// Submits only on a transition, including the one back to NONE: Volume and Brightness are re-trigger controls,
// the host repeats them until it sees a report with 0.
ConsumerUsageSubmission KeyProcessor::DecideConsumerUsageSubmission(USHORT usage) {
    if (usage == m_LastConsumerUsage) {
        return {false, usage};
    }
    m_LastConsumerUsage = usage;
    return {true, usage};
}

ConsumerUsageSubmission KeyProcessor::Process(BYTE* buf, ULONG size) {
    if (!buf || size < 9) {
        return {false, CONSUMER_USAGE_NONE};
    }

    for (int i = 2; i <= 7; i++) {
        if (buf[i]) {
            DebugPrint("Raw key: 0x%02X\n", buf[i]);
        }
    }
    for (int bit = 0; bit < 8; bit++) {
        if (buf[0] & (1 << bit)) {
            DebugPrint("Raw modifier: bit %d (mask 0x%02X)\n", bit, 1 << bit);
        }
    }

    const BYTE specialKeys = buf[8];
    BOOLEAN fnPressed = (specialKeys & SPECIAL_KEY_FN) != 0;
    buf[8] &= (BYTE) ~(SPECIAL_KEY_EJECT | SPECIAL_KEY_FN);

    // Keys produced by special key rules are injected after the key pass so they are not remapped a second time.
    BYTE injectedKeys[kMaxInjectedKeys] = {};
    ULONG injectedCount = 0;
    ProcessModifiersAndVirtualKeys(buf, &fnPressed, specialKeys, injectedKeys, &injectedCount);

    USHORT consumerUsage = ProcessKeys(buf, fnPressed);

    for (ULONG i = 0; i < injectedCount; i++) {
        InjectKey(buf, injectedKeys[i]);
    }

    return DecideConsumerUsageSubmission(consumerUsage);
}
