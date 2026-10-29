#include "pch.h"
#include "CppUnitTest.h"

#include "KeyProcessor.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace My4MagicKeysPlusDriverTests {
    namespace {
        // [0]=modifiers, [1]=reserved, [2..7]=keycodes, [8]=special key bits
        struct Report {
            BYTE bytes[9] = {};
            Report& Modifier(BYTE mask) {
                bytes[0] = mask;
                return *this;
            }
            Report& Key(int slot, BYTE code) {
                bytes[2 + slot] = code;
                return *this;
            }
            Report& Fn() {
                bytes[8] |= SPECIAL_KEY_FN;
                return *this;
            }
            Report& Eject() {
                bytes[8] |= SPECIAL_KEY_EJECT;
                return *this;
            }
            Report& Lock() {
                bytes[8] |= SPECIAL_KEY_LOCK;
                return *this;
            }
        };

        const DWORD kFn = RULE_SOURCE_FN;

        DWORD Mods(BYTE mask) {
            return (DWORD)mask << RULE_MODS_SHIFT;
        }
        DWORD Consumer(USHORT usage) {
            return RULE_TARGET_CONSUMER | usage;
        }

        template <size_t N>
        void Load(KeyProcessor& kp, DWORD fnLock, const KEY_RULE (&rules)[N]) {
            kp.LoadConfig(fnLock, rules, (ULONG)N);
        }

        void LoadNone(KeyProcessor& kp, DWORD fnLock = 0) {
            kp.LoadConfig(fnLock, nullptr, 0);
        }

        const KEY_RULE kUserRules[] = {
            {0x000000F0, 0x000000F1, false}, // Eject -> Fn
            {0x000000F1, 0x00000049, false}, // Fn -> Insert
            {0x00000068, 0x00000046, false}, // F13 -> PrintScreen
            {0x00000069, 0x00000047, false}, // F14 -> ScrollLock
            {0x0000006A, 0x00000048, false}, // F15 -> Pause
            {0x00040000, 0x00080000, false}, // LeftOption -> LeftCommand
            {0x00080000, 0x00040000, false}, // LeftCommand -> LeftOption
            {0x00400000, 0x00800000, false}, // RightOption -> RightCommand
            {0x00800000, 0x00400000, false}, // RightCommand -> RightOption
            {0x01000050, 0x0000004A, false}, // Fn+Left -> Home
            {0x0100004F, 0x0000004D, false}, // Fn+Right -> End
            {0x01000052, 0x0000004B, false}, // Fn+Up -> PageUp
            {0x01000051, 0x0000004E, false}, // Fn+Down -> PageDown
            {0x01000028, 0x00000049, false}, // Fn+Enter -> Insert
            {0x01000013, 0x00000046, false}, // Fn+P -> PrintScreen
            {0x01000005, 0x00000048, false}, // Fn+B -> Pause
            {0x01000016, 0x00000047, false}, // Fn+S -> ScrollLock
            {0x0100003A, 0x01000070, true}, // Fn+F1 -> Brightness Down
            {0x0100003B, 0x0100006F, true}, // Fn+F2 -> Brightness Up
            {0x01000040, 0x010000B6, true}, // Fn+F7 -> Previous Track
            {0x01000041, 0x010000CD, true}, // Fn+F8 -> Play/Pause
            {0x01000042, 0x010000B5, true}, // Fn+F9 -> Next Track
            {0x01000043, 0x010000E2, true}, // Fn+F10 -> Mute
            {0x01000044, 0x010000EA, true}, // Fn+F11 -> Volume Down
            {0x01000045, 0x010000E9, true}, // Fn+F12 -> Volume Up
        };
    }

    TEST_CLASS(KeyRuleTests) {
    public:
        TEST_METHOD(RemapsNormalKey) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {{HidKeyB, HidKeyT, false}};
            Load(kp, 0, rules);

            Report report;
            report.Key(0, HidKeyB);
            auto result = kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)HidKeyT, (int)report.bytes[2]);
            Assert::IsFalse(result.ShouldSubmit);
        }

        TEST_METHOD(UnmappedKeyPassesThrough) {
            KeyProcessor kp;
            LoadNone(kp);

            Report report;
            report.Key(0, HidKeyB);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)HidKeyB, (int)report.bytes[2]);
        }

        TEST_METHOD(PlainRuleStillAppliesWhileFnIsHeld) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {{HidF13, HidPrtScr, false}};
            Load(kp, 0, rules);

            Report report;
            report.Fn().Key(0, HidF13);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)HidPrtScr, (int)report.bytes[2]);
        }

        TEST_METHOD(KeyCanBecomeModifiers) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {{kFn | HidF13, Mods(HidLeftCtrlMask | HidLeftShiftMask), false}};
            Load(kp, 0, rules);

            Report report;
            report.Fn().Key(0, HidF13);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)(HidLeftCtrlMask | HidLeftShiftMask), (int)report.bytes[0]);
            Assert::AreEqual(0, (int)report.bytes[2]);
        }

        TEST_METHOD(UninterpretableRulesAreIgnored) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {
                {HidKeyB, VIRTUAL_FN, false},              // key -> Fn is not a supported shape
                {0x02000000UL | HidKeyP, HidKeyT, false},  // reserved flag bit set
            };
            Load(kp, 0, rules);

            Report report;
            report.Key(0, HidKeyB).Key(1, HidKeyP);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)HidKeyB, (int)report.bytes[2]);
            Assert::AreEqual((int)HidKeyP, (int)report.bytes[3]);
        }
    };

    TEST_CLASS(ModifierRuleTests) {
    public:
        TEST_METHOD(SwapsCtrlAndAlt) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {
                {Mods(HidLeftCtrlMask), Mods(HidLeftAltMask), false},
                {Mods(HidLeftAltMask), Mods(HidLeftCtrlMask), false},
            };
            Load(kp, 0, rules);

            Report report;
            report.Modifier(HidLeftCtrlMask);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)HidLeftAltMask, (int)report.bytes[0]);
        }

        TEST_METHOD(SwapKeepsBothWhenBothHeld) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {
                {Mods(HidLeftCtrlMask), Mods(HidLeftAltMask), false},
                {Mods(HidLeftAltMask), Mods(HidLeftCtrlMask), false},
            };
            Load(kp, 0, rules);

            Report report;
            report.Modifier(HidLeftCtrlMask | HidLeftAltMask);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)(HidLeftCtrlMask | HidLeftAltMask), (int)report.bytes[0]);
        }
    };

    TEST_CLASS(VirtualKeyRuleTests) {
    public:
        TEST_METHOD(ModifierMapsToFn) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {
                {Mods(HidLeftCmdMask), VIRTUAL_FN, false},
                {kFn | HidLeft, HidHome, false},
            };
            Load(kp, 0, rules);

            Report report;
            report.Modifier(HidLeftCmdMask);
            report.Key(0, HidLeft);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual(0, report.bytes[0] & HidLeftCmdMask);
            Assert::AreEqual((int)HidHome, (int)report.bytes[2]);
        }

        TEST_METHOD(IndependentRulesDoNotChainInSamePass) {
            // Only LeftCmd is pressed: Fn->LeftAlt must not fire because LeftCmd->Fn produced Fn.
            KeyProcessor kp;
            const KEY_RULE rules[] = {
                {Mods(HidLeftCmdMask), VIRTUAL_FN, false},
                {VIRTUAL_FN, Mods(HidLeftAltMask), false},
                {kFn | HidLeft, HidHome, false},
            };
            Load(kp, 0, rules);

            Report report;
            report.Modifier(HidLeftCmdMask);
            report.Key(0, HidLeft);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)HidHome, (int)report.bytes[2]);
            Assert::AreEqual(0, report.bytes[0] & HidLeftAltMask);
        }

        TEST_METHOD(FnMapsToModifierAndIsNoLongerFn) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {
                {VIRTUAL_FN, Mods(HidLeftCtrlMask), false},
                {kFn | HidLeft, HidHome, false},
            };
            Load(kp, 0, rules);

            Report report;
            report.Fn().Key(0, HidLeft);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)HidLeftCtrlMask, (int)report.bytes[0]);
            Assert::AreEqual((int)HidLeft, (int)report.bytes[2]);
        }

        TEST_METHOD(EjectActsAsFnAndFnTypesInsert) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {
                {VIRTUAL_EJECT, VIRTUAL_FN, false},
                {VIRTUAL_FN, HidInsert, false},
                {kFn | HidLeft, HidHome, false},
            };
            Load(kp, 0, rules);

            Report eject;
            eject.Eject().Key(0, HidLeft);
            kp.Process(eject.bytes, sizeof(eject.bytes));
            Assert::AreEqual((int)HidHome, (int)eject.bytes[2]);
            Assert::AreEqual(0, (int)eject.bytes[3]);

            Report fn;
            fn.Fn();
            kp.Process(fn.bytes, sizeof(fn.bytes));
            Assert::AreEqual((int)HidInsert, (int)fn.bytes[2]);

            Report fnWithLeft;
            fnWithLeft.Fn().Key(0, HidLeft);
            kp.Process(fnWithLeft.bytes, sizeof(fnWithLeft.bytes));
            Assert::AreEqual((int)HidLeft, (int)fnWithLeft.bytes[2]);
            Assert::AreEqual((int)HidInsert, (int)fnWithLeft.bytes[3]);
        }

        TEST_METHOD(FnAndEjectSwapWithoutChaining) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {
                {VIRTUAL_FN, VIRTUAL_EJECT, false},
                {VIRTUAL_EJECT, VIRTUAL_FN, false},
                {kFn | HidLeft, HidHome, false},
            };
            Load(kp, 0, rules);

            Report eject;
            eject.Eject().Key(0, HidLeft);
            kp.Process(eject.bytes, sizeof(eject.bytes));
            Assert::AreEqual((int)HidHome, (int)eject.bytes[2]);

            Report fn;
            fn.Fn().Key(0, HidLeft);
            kp.Process(fn.bytes, sizeof(fn.bytes));
            Assert::AreEqual((int)HidLeft, (int)fn.bytes[2]);
        }
    };

    TEST_CLASS(FnLayerTests) {
    public:
        TEST_METHOD(FnLeftBecomesHomeOnlyWithFn) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {{kFn | HidLeft, HidHome, false}};
            Load(kp, 0, rules);

            Report withFn;
            withFn.Fn().Key(0, HidLeft);
            kp.Process(withFn.bytes, sizeof(withFn.bytes));
            Assert::AreEqual((int)HidHome, (int)withFn.bytes[2]);

            Report withoutFn;
            withoutFn.Key(0, HidLeft);
            kp.Process(withoutFn.bytes, sizeof(withoutFn.bytes));
            Assert::AreEqual((int)HidLeft, (int)withoutFn.bytes[2]);
        }

        TEST_METHOD(NoRulesMeansNoFnBehavior) {
            KeyProcessor kp;
            LoadNone(kp);

            Report arrow;
            arrow.Fn().Key(0, HidLeft);
            kp.Process(arrow.bytes, sizeof(arrow.bytes));
            Assert::AreEqual((int)HidLeft, (int)arrow.bytes[2]);

            Report f8;
            f8.Key(0, HidF8);
            auto result = kp.Process(f8.bytes, sizeof(f8.bytes));
            Assert::AreEqual((int)HidF8, (int)f8.bytes[2]);
            Assert::IsFalse(result.ShouldSubmit);
        }

        TEST_METHOD(OtherBitsOfTheSpecialByteArePassedThrough) {
            KeyProcessor kp;
            LoadNone(kp);

            Report report;
            report.Fn().Eject();
            report.bytes[8] |= 0xF8;
            kp.Process(report.bytes, sizeof(report.bytes));
            Assert::AreEqual(0xF8, (int)report.bytes[8]);
        }

        TEST_METHOD(LockPassesThroughWithoutARule) {
            KeyProcessor kp;
            LoadNone(kp);

            Report report;
            report.Lock();
            kp.Process(report.bytes, sizeof(report.bytes));
            Assert::AreEqual((int)SPECIAL_KEY_LOCK, (int)report.bytes[8]);
        }

        TEST_METHOD(LockBecomesAKey) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {{VIRTUAL_LOCK, HidInsert, false}};
            Load(kp, 0, rules);

            Report report;
            report.Lock();
            kp.Process(report.bytes, sizeof(report.bytes));
            Assert::AreEqual((int)HidInsert, (int)report.bytes[2]);
            Assert::AreEqual(0, (int)report.bytes[8]);
        }

        TEST_METHOD(LockBecomesAModifier) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {{VIRTUAL_LOCK, Mods(HidLeftCtrlMask), false}};
            Load(kp, 0, rules);

            Report report;
            report.Lock();
            kp.Process(report.bytes, sizeof(report.bytes));
            Assert::AreEqual((int)HidLeftCtrlMask, (int)report.bytes[0]);
            Assert::AreEqual(0, (int)report.bytes[8]);
        }

        TEST_METHOD(LockActsAsFn) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {
                {VIRTUAL_LOCK, VIRTUAL_FN, false},
                {kFn | HidLeft, HidHome, false},
            };
            Load(kp, 0, rules);

            Report report;
            report.Lock().Key(0, HidLeft);
            kp.Process(report.bytes, sizeof(report.bytes));
            Assert::AreEqual((int)HidHome, (int)report.bytes[2]);
            Assert::AreEqual(0, (int)report.bytes[8]);
        }

        TEST_METHOD(LockRuleDoesNothingWhileLockIsNotPressed) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {{VIRTUAL_LOCK, HidInsert, false}};
            Load(kp, 0, rules);

            Report report;
            report.Key(0, HidKeyB);
            kp.Process(report.bytes, sizeof(report.bytes));
            Assert::AreEqual((int)HidKeyB, (int)report.bytes[2]);
            Assert::AreEqual(0, (int)report.bytes[3]);
        }

        TEST_METHOD(LockCannotBeATarget) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {
                {HidKeyB, VIRTUAL_LOCK, false},
                {VIRTUAL_EJECT, VIRTUAL_LOCK, false},
                {Mods(HidLeftCtrlMask), VIRTUAL_LOCK, false},
            };
            Assert::AreEqual(0, (int)kp.LoadConfig(0, rules, 3));
        }

        TEST_METHOD(EjectFnAndLockEachInjectTheirKey) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {
                {VIRTUAL_EJECT, HidEnd, false},
                {VIRTUAL_FN, HidInsert, false},
                {VIRTUAL_LOCK, HidHome, false},
            };
            Load(kp, 0, rules);

            Report report;
            report.Eject().Fn().Lock();
            kp.Process(report.bytes, sizeof(report.bytes));
            Assert::AreEqual((int)HidEnd, (int)report.bytes[2]);
            Assert::AreEqual((int)HidInsert, (int)report.bytes[3]);
            Assert::AreEqual((int)HidHome, (int)report.bytes[4]);
            Assert::AreEqual(0, (int)report.bytes[8]);
        }

        TEST_METHOD(FnLockDoesNotAffectNonAwareRules) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {{kFn | HidLeft, HidHome, false}};
            Load(kp, 1, rules);

            Report withoutFn;
            withoutFn.Key(0, HidLeft);
            kp.Process(withoutFn.bytes, sizeof(withoutFn.bytes));
            Assert::AreEqual((int)HidLeft, (int)withoutFn.bytes[2]);

            Report withFn;
            withFn.Fn().Key(0, HidLeft);
            kp.Process(withFn.bytes, sizeof(withFn.bytes));
            Assert::AreEqual((int)HidHome, (int)withFn.bytes[2]);
        }
    };

    TEST_CLASS(FnLockAwareTests) {
    public:
        TEST_METHOD(FnLockOffMediaWithoutFn) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {{kFn | HidF8, Consumer(CONSUMER_USAGE_PLAYPAUSE), true}};
            Load(kp, 0, rules);

            Report report;
            report.Key(0, HidF8);
            auto result = kp.Process(report.bytes, sizeof(report.bytes));

            Assert::IsTrue(result.ShouldSubmit);
            Assert::AreEqual((int)CONSUMER_USAGE_PLAYPAUSE, (int)result.Usage);
            Assert::AreEqual(0, (int)report.bytes[2]);
        }

        TEST_METHOD(FnLockOffPlainF8WithFn) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {{kFn | HidF8, Consumer(CONSUMER_USAGE_PLAYPAUSE), true}};
            Load(kp, 0, rules);

            Report report;
            report.Fn().Key(0, HidF8);
            auto result = kp.Process(report.bytes, sizeof(report.bytes));

            Assert::IsFalse(result.ShouldSubmit);
            Assert::AreEqual((int)HidF8, (int)report.bytes[2]);
        }

        TEST_METHOD(FnLockOnPlainF8WithoutFn) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {{kFn | HidF8, Consumer(CONSUMER_USAGE_PLAYPAUSE), true}};
            Load(kp, 1, rules);

            Report report;
            report.Key(0, HidF8);
            auto result = kp.Process(report.bytes, sizeof(report.bytes));

            Assert::IsFalse(result.ShouldSubmit);
            Assert::AreEqual((int)HidF8, (int)report.bytes[2]);
        }

        TEST_METHOD(FnLockOnMediaWithFn) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {{kFn | HidF8, Consumer(CONSUMER_USAGE_PLAYPAUSE), true}};
            Load(kp, 1, rules);

            Report report;
            report.Fn().Key(0, HidF8);
            auto result = kp.Process(report.bytes, sizeof(report.bytes));

            Assert::IsTrue(result.ShouldSubmit);
            Assert::AreEqual((int)CONSUMER_USAGE_PLAYPAUSE, (int)result.Usage);
            Assert::AreEqual(0, (int)report.bytes[2]);
        }
    };

    TEST_CLASS(ConsumerUsageTests) {
    public:
        TEST_METHOD(SubmitsOnceThenDedupesRepeatedReports) {
            KeyProcessor kp;
            Load(kp, 0, kUserRules);

            Report held1;
            held1.Key(0, HidF8); // Play/Pause
            auto first = kp.Process(held1.bytes, sizeof(held1.bytes));

            Report held2;
            held2.Key(0, HidF8);
            auto second = kp.Process(held2.bytes, sizeof(held2.bytes));

            Assert::IsTrue(first.ShouldSubmit);
            Assert::AreEqual((int)CONSUMER_USAGE_PLAYPAUSE, (int)first.Usage);
            Assert::IsFalse(second.ShouldSubmit);
        }

        TEST_METHOD(ReleaseSubmitsNoneUsage) {
            KeyProcessor kp;
            Load(kp, 0, kUserRules);

            Report held;
            held.Key(0, HidF8);
            kp.Process(held.bytes, sizeof(held.bytes));

            Report released;
            auto result = kp.Process(released.bytes, sizeof(released.bytes));

            Assert::IsTrue(result.ShouldSubmit);
            Assert::AreEqual((int)CONSUMER_USAGE_NONE, (int)result.Usage);
        }

        TEST_METHOD(TwoDevicesDoNotShareDedupState) {
            KeyProcessor deviceA;
            KeyProcessor deviceB;
            Load(deviceA, 0, kUserRules);
            Load(deviceB, 0, kUserRules);

            Report pressedA;
            pressedA.Key(0, HidF8);
            Report pressedB;
            pressedB.Key(0, HidF8);

            auto resultA = deviceA.Process(pressedA.bytes, sizeof(pressedA.bytes));
            auto resultB = deviceB.Process(pressedB.bytes, sizeof(pressedB.bytes));

            Assert::IsTrue(resultA.ShouldSubmit);
            Assert::IsTrue(resultB.ShouldSubmit);
        }
    };

    TEST_CLASS(UserRulesTests) {
    public:
        TEST_METHOD(EjectPlusLeftIsHome) {
            KeyProcessor kp;
            Load(kp, 1, kUserRules);

            Report report;
            report.Eject().Key(0, HidLeft);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)HidHome, (int)report.bytes[2]);
            Assert::AreEqual(0, (int)report.bytes[3]);
        }

        TEST_METHOD(F13IsPrintScreenEvenWithEjectHeld) {
            KeyProcessor kp;
            Load(kp, 1, kUserRules);

            Report report;
            report.Eject().Key(0, HidF13);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)HidPrtScr, (int)report.bytes[2]);
        }

        TEST_METHOD(OptionAndCommandAreSwapped) {
            KeyProcessor kp;
            Load(kp, 1, kUserRules);

            Report report;
            report.Modifier(HidLeftAltMask | HidRightCmdMask);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)(HidLeftCmdMask | HidRightAltMask), (int)report.bytes[0]);
        }

        TEST_METHOD(FnLockOnF8IsPlainAndEjectF8IsMedia) {
            KeyProcessor kp;
            Load(kp, 1, kUserRules);

            Report plain;
            plain.Key(0, HidF8);
            auto plainResult = kp.Process(plain.bytes, sizeof(plain.bytes));
            Assert::IsFalse(plainResult.ShouldSubmit);
            Assert::AreEqual((int)HidF8, (int)plain.bytes[2]);

            Report media;
            media.Eject().Key(0, HidF8);
            auto mediaResult = kp.Process(media.bytes, sizeof(media.bytes));
            Assert::IsTrue(mediaResult.ShouldSubmit);
            Assert::AreEqual((int)CONSUMER_USAGE_PLAYPAUSE, (int)mediaResult.Usage);
        }
    };

    TEST_CLASS(ReloadTests) {
    public:
        TEST_METHOD(LoadConfigReturnsHowManyRulesWereKept) {
            KeyProcessor kp;
            const KEY_RULE rules[] = {
                {HidKeyB, HidKeyT, false},
                {HidKeyB, VIRTUAL_FN, false},  // key -> Fn is not a supported shape
                {HidKeyP, HidKeyT, false},
            };

            Assert::AreEqual(2, (int)kp.LoadConfig(0, rules, 3));
            Assert::AreEqual(0, (int)kp.LoadConfig(0, nullptr, 0));
        }

        TEST_METHOD(LoadingAgainReplacesTheWholeConfiguration) {
            KeyProcessor kp;
            const KEY_RULE first[] = {{HidKeyB, HidKeyT, false}};
            const KEY_RULE second[] = {{HidKeyP, HidKeyT, false}};
            Load(kp, 0, first);
            Load(kp, 0, second);

            Report report;
            report.Key(0, HidKeyB).Key(1, HidKeyP);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)HidKeyB, (int)report.bytes[2]);
            Assert::AreEqual((int)HidKeyT, (int)report.bytes[3]);
        }

        TEST_METHOD(RemovingAllRulesRestoresPlainKeys) {
            KeyProcessor kp;
            Load(kp, 0, kUserRules);
            LoadNone(kp);

            Report report;
            report.Key(0, HidKeyB).Modifier(HidLeftAltMask);
            auto result = kp.Process(report.bytes, sizeof(report.bytes));

            Assert::IsFalse(result.ShouldSubmit);
            Assert::AreEqual((int)HidKeyB, (int)report.bytes[2]);
            Assert::AreEqual((int)HidLeftAltMask, (int)report.bytes[0]);
        }

        TEST_METHOD(FnLockChangeAppliesFromTheNextReport) {
            KeyProcessor kp;
            Load(kp, 1, kUserRules);
            Load(kp, 0, kUserRules);

            Report report;
            report.Key(0, HidF8);
            auto result = kp.Process(report.bytes, sizeof(report.bytes));

            Assert::IsTrue(result.ShouldSubmit);
            Assert::AreEqual((int)CONSUMER_USAGE_PLAYPAUSE, (int)result.Usage);
        }

        TEST_METHOD(MediaKeyHeldAcrossAReloadIsStillReleased) {
            KeyProcessor kp;
            Load(kp, 0, kUserRules);

            Report pressed;
            pressed.Key(0, HidF8);
            kp.Process(pressed.bytes, sizeof(pressed.bytes));

            LoadNone(kp);

            Report released;
            auto result = kp.Process(released.bytes, sizeof(released.bytes));

            Assert::IsTrue(result.ShouldSubmit);
            Assert::AreEqual((int)CONSUMER_USAGE_NONE, (int)result.Usage);
        }
    };
}
