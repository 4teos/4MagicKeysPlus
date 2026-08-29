#include "pch.h"
#include "CppUnitTest.h"

#include "KeyProcessor.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace My4MagicKeysPlusTests
{
    namespace {
        // 9-byte HID report layout Process() expects: [0]=modifiers,
        // [1]=reserved, [2..7]=keycodes, [8]=virtual Fn/Eject bits.
        struct Report {
            BYTE bytes[9] = {};
            Report& Modifier(BYTE mask) { bytes[0] = mask; return *this; }
            Report& Key(int slot, BYTE code) { bytes[2 + slot] = code; return *this; }
        };

        const BYTE kNoBytes[1] = {};
    }

    TEST_CLASS(KeyMapTests)
    {
    public:
        TEST_METHOD(RemapsNormalKey)
        {
            KeyProcessor kp;
            const BYTE keyMap[] = { HidKeyB, HidKeyT };
            kp.LoadConfig(0, keyMap, sizeof(keyMap), kNoBytes, 0, kNoBytes, 0);

            Report report;
            report.Key(0, HidKeyB);
            auto result = kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)HidKeyT, (int)report.bytes[2]);
            Assert::IsFalse(result.ShouldSubmit);
        }

        TEST_METHOD(UnmappedKeyPassesThrough)
        {
            KeyProcessor kp;
            kp.LoadConfig(0, kNoBytes, 0, kNoBytes, 0, kNoBytes, 0);

            Report report;
            report.Key(0, HidKeyB);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)HidKeyB, (int)report.bytes[2]);
        }
    };

    TEST_CLASS(ModMapTests)
    {
    public:
        TEST_METHOD(SwapsCtrlAndAlt)
        {
            KeyProcessor kp;
            const BYTE modMap[] = { HidLeftCtrlMask, HidLeftAltMask, HidLeftAltMask, HidLeftCtrlMask };
            kp.LoadConfig(0, kNoBytes, 0, modMap, sizeof(modMap), kNoBytes, 0);

            Report report;
            report.Modifier(HidLeftCtrlMask);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)HidLeftAltMask, (int)report.bytes[0]);
        }
    };

    // Regression coverage for the two-phase snapshot algorithm described in
    // KeyProcessor's ProcessSpecialModifiers comment: an earlier draft decided
    // from values it had already written earlier in the same pass, which let
    // independently-configured rules silently cancel or chain into each other.
    TEST_CLASS(SpecialModMapTests)
    {
    public:
        TEST_METHOD(ModifierMapsToFn)
        {
            KeyProcessor kp;
            const BYTE specialModMap[] = { HidLeftCmdMask, VIRTUAL_FN };
            kp.LoadConfig(0, kNoBytes, 0, kNoBytes, 0, specialModMap, sizeof(specialModMap));

            // There's no direct "is Fn set" observable, so use the hardcoded
            // Fn+Left->Home combo as a proxy for "Fn actually got set".
            Report report;
            report.Modifier(HidLeftCmdMask);
            report.Key(0, HidLeft);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual(0, report.bytes[0] & HidLeftCmdMask);
            Assert::AreEqual((int)HidHome, (int)report.bytes[2]);
        }

        TEST_METHOD(IndependentRulesDoNotChainInSamePass)
        {
            // LeftCmd->Fn and Fn->LeftAlt configured together. Only LeftCmd is
            // actually pressed, so Fn->LeftAlt must NOT also fire just because
            // LeftCmd->Fn set fnPressed=true earlier in this same call.
            KeyProcessor kp;
            const BYTE specialModMap[] = {
                HidLeftCmdMask, VIRTUAL_FN,
                VIRTUAL_FN, HidLeftAltMask,
            };
            kp.LoadConfig(0, kNoBytes, 0, kNoBytes, 0, specialModMap, sizeof(specialModMap));

            Report report;
            report.Modifier(HidLeftCmdMask);
            report.Key(0, HidLeft);
            kp.Process(report.bytes, sizeof(report.bytes));

            Assert::AreEqual((int)HidHome, (int)report.bytes[2]); // Fn did get set...
            Assert::AreEqual(0, report.bytes[0] & HidLeftAltMask); // ...but didn't also become LeftAlt
        }
    };

    TEST_CLASS(ConsumerUsageTests)
    {
    public:
        TEST_METHOD(SubmitsOnceThenDedupesRepeatedReports)
        {
            KeyProcessor kp;
            kp.LoadConfig(0 /* FnLock off */, kNoBytes, 0, kNoBytes, 0, kNoBytes, 0);

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

        TEST_METHOD(ReleaseSubmitsNoneUsage)
        {
            KeyProcessor kp;
            kp.LoadConfig(0, kNoBytes, 0, kNoBytes, 0, kNoBytes, 0);

            Report held;
            held.Key(0, HidF8);
            kp.Process(held.bytes, sizeof(held.bytes));

            Report released;
            auto result = kp.Process(released.bytes, sizeof(released.bytes));

            Assert::IsTrue(result.ShouldSubmit);
            Assert::AreEqual((int)CONSUMER_USAGE_NONE, (int)result.Usage);
        }

        // Regression test for the original bug this whole KeyProcessor class split
        // was meant to fix: two physical keyboards used to share one global
        // g_LastConsumerUsage, so an identical key press on the second keyboard
        // was silently swallowed. Two independent KeyProcessor instances (one per
        // DEVICE_CONTEXT in the real driver) must not do that.
        TEST_METHOD(TwoDevicesDoNotShareDedupState)
        {
            KeyProcessor deviceA;
            KeyProcessor deviceB;
            deviceA.LoadConfig(0, kNoBytes, 0, kNoBytes, 0, kNoBytes, 0);
            deviceB.LoadConfig(0, kNoBytes, 0, kNoBytes, 0, kNoBytes, 0);

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
}
