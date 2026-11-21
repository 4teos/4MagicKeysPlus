using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

public class KeyRemapRulesTests {
    [Fact]
    public void GetCurrentTarget_ReturnsBaseLayerKeyTarget() {
        DriverKeyRemapConfig config = UserRules.Config(fnLockEnabled: true);

        Assert.Equal(PhysicalKey.PrintScreen, KeyRemapRules.GetCurrentTarget(config, PhysicalKey.F13));
        Assert.Equal(PhysicalKey.Fn, KeyRemapRules.GetCurrentTarget(config, PhysicalKey.Eject));
        Assert.Equal(PhysicalKey.Insert, KeyRemapRules.GetCurrentTarget(config, PhysicalKey.Fn));
    }

    [Fact]
    public void GetCurrentTarget_ReturnsSingleModifierTarget() {
        DriverKeyRemapConfig config = UserRules.Config(fnLockEnabled: true);

        Assert.Equal(PhysicalKey.LeftCommand, KeyRemapRules.GetCurrentTarget(config, PhysicalKey.LeftOption));
    }

    [Fact]
    public void GetCurrentTarget_IgnoresFnLayerRules() {
        DriverKeyRemapConfig config = UserRules.Config(fnLockEnabled: true);

        Assert.Null(KeyRemapRules.GetCurrentTarget(config, PhysicalKey.Left));
        Assert.Null(KeyRemapRules.GetCurrentTarget(config, PhysicalKey.F8));
    }

    [Fact]
    public void GetCurrentTarget_IsNullForAnUnmappedKey() {
        Assert.Null(KeyRemapRules.GetCurrentTarget(UserRules.Config(fnLockEnabled: true), PhysicalKey.A));
    }

    public static TheoryData<bool> Layers => [false, true];

    private static readonly KeyboardModel FullSize = KeyboardModel.A1843;

    private static IEnumerable<KeyboardModel> AllModels => Enum.GetValues<KeyboardProduct>().Select(KeyboardModel.From);

    // What the editor offers must be what the driver accepts: a rule it would drop is a mapping
    // that silently does nothing.
    [Theory]
    [MemberData(nameof(Layers))]
    public void EveryOfferedMapping_IsAcceptedByTheDriverParser(bool fnLayer) {
        int checkedPairs = 0;
        foreach (PhysicalKey source in Enum.GetValues<PhysicalKey>()) {
            foreach (RemapTarget target in KeyRemapRules.GetAllowedTargets(source, FullSize, fnLayer)) {
                KeyRule? rule = DriverRuleEncoder.CreateRule(source, target, fnLayer, fnLockAware: false);

                Assert.True(rule is not null, $"{source} -> {target} (Fn layer: {fnLayer}) is dropped by the driver");
                Assert.True(rule.Source.SetEquals([source]));
                Assert.Equal(fnLayer, rule.FnLayer);
                Assert.Equal(target, TargetOf(rule));
                checkedPairs++;
            }
        }
        Assert.True(checkedPairs > 100);
    }

    [Fact]
    public void CanRemap_FnLayerCoversKeysThatHaveNoPlainRemap() {
        Assert.False(KeyRemapRules.CanRemap(PhysicalKey.Enter));
        Assert.True(KeyRemapRules.CanRemap(PhysicalKey.Enter, fnLayer: true));
        Assert.True(KeyRemapRules.CanRemap(PhysicalKey.F8, fnLayer: true));
    }

    [Fact]
    public void CanRemap_FnLayerExcludesModifiersAndVirtualKeys() {
        Assert.False(KeyRemapRules.CanRemap(PhysicalKey.LeftShift, fnLayer: true));
        Assert.False(KeyRemapRules.CanRemap(PhysicalKey.Fn, fnLayer: true));
        Assert.False(KeyRemapRules.CanRemap(PhysicalKey.Eject, fnLayer: true));
        Assert.False(KeyRemapRules.CanRemap(PhysicalKey.Lock, fnLayer: true));
        Assert.False(KeyRemapRules.CanRemap(PhysicalKey.TouchId, fnLayer: true));
        Assert.False(KeyRemapRules.CanRemap(PhysicalKey.None, fnLayer: true));
    }

    [Theory]
    [InlineData(PhysicalKey.Lock)]
    [InlineData(PhysicalKey.TouchId)]
    public void TheLockKeys_AreRemappedLikeEject(PhysicalKey lockKey) {
        IReadOnlyList<RemapTarget> targets = KeyRemapRules.GetAllowedTargets(lockKey, FullSize);

        Assert.Contains((RemapTarget)PhysicalKey.LeftControl, targets);
        Assert.Contains((RemapTarget)PhysicalKey.Fn, targets);
        Assert.Contains((RemapTarget)PhysicalKey.Eject, targets);
        Assert.Contains((RemapTarget)PhysicalKey.F13, targets);
    }

    [Theory]
    [MemberData(nameof(Layers))]
    public void TheLockKeys_AreNoTargetOfAnything(bool fnLayer) {
        foreach (KeyboardModel model in AllModels) {
            foreach (PhysicalKey source in Enum.GetValues<PhysicalKey>()) {
                IReadOnlyList<RemapTarget> targets = KeyRemapRules.GetAllowedTargets(source, model, fnLayer);

                Assert.DoesNotContain((RemapTarget)PhysicalKey.Lock, targets);
                Assert.DoesNotContain((RemapTarget)PhysicalKey.TouchId, targets);
            }
        }
    }

    [Fact]
    public void F13ToF24_AreOfferedOnlyByTheKeyboardsThatHaveF13() {
        foreach (KeyboardModel model in AllModels) {
            Assert.Equal(HasF13(model), KeyRemapRules.GetAllowedTargets(PhysicalKey.Eject, model).Contains((RemapTarget)PhysicalKey.F13));
        }
    }

    [Theory]
    [MemberData(nameof(Layers))]
    public void CompactKeyboards_GetNoF13ToF24ButStillCanRemapEveryKey(bool fnLayer) {
        foreach (KeyboardModel model in AllModels.Where(model => !HasF13(model))) {
            foreach (PhysicalKey source in Enum.GetValues<PhysicalKey>()) {
                IReadOnlyList<RemapTarget> targets = KeyRemapRules.GetAllowedTargets(source, model, fnLayer);

                Assert.DoesNotContain(targets, target => !target.IsMedia && target.Key >= PhysicalKey.F13 && target.Key <= PhysicalKey.F24);
                if (KeyRemapRules.GetAllowedTargets(source, FullSize, fnLayer).Count > 0) {
                    Assert.NotEmpty(targets);
                }
            }
        }
    }

    [Fact]
    public void CompactKeyboards_KeepTheOtherExtendedTargets() {
        IReadOnlyList<RemapTarget> fnLayer = KeyRemapRules.GetAllowedTargets(PhysicalKey.F3, KeyboardModel.A3118, fnLayer: true);
        IReadOnlyList<RemapTarget> touchId = KeyRemapRules.GetAllowedTargets(PhysicalKey.TouchId, KeyboardModel.A3118);

        Assert.DoesNotContain((RemapTarget)PhysicalKey.F16, fnLayer);
        Assert.Contains((RemapTarget)PhysicalKey.PrintScreen, fnLayer);
        Assert.Contains((RemapTarget)PhysicalKey.Application, fnLayer);
        Assert.Contains((RemapTarget)PhysicalKey.Home, fnLayer);
        Assert.Contains(RemapTarget.Media(0xE9), fnLayer);
        Assert.DoesNotContain((RemapTarget)PhysicalKey.F13, touchId);
        Assert.Contains((RemapTarget)PhysicalKey.Insert, touchId);
        Assert.Contains((RemapTarget)PhysicalKey.LeftControl, touchId);
    }

    [Fact]
    public void FullSizeKeyboards_GetF13ToF24() {
        Assert.Contains((RemapTarget)PhysicalKey.F16, KeyRemapRules.GetAllowedTargets(PhysicalKey.F3, FullSize, fnLayer: true));
        Assert.Contains((RemapTarget)PhysicalKey.F24, KeyRemapRules.GetAllowedTargets(PhysicalKey.F13, FullSize));
        Assert.Contains((RemapTarget)PhysicalKey.F20, KeyRemapRules.GetAllowedTargets(PhysicalKey.Eject, FullSize));
    }

    [Fact]
    public void TheMenuKey_IsRemappedLikeTheExtendedKeys() {
        Assert.Contains((RemapTarget)PhysicalKey.PrintScreen, KeyRemapRules.GetAllowedTargets(PhysicalKey.Menu, FullSize));
        Assert.Contains(RemapTarget.Media(0xE9), KeyRemapRules.GetAllowedTargets(PhysicalKey.Menu, FullSize));
        Assert.True(KeyRemapRules.CanRemap(PhysicalKey.Menu, fnLayer: true));
    }

    [Fact]
    public void TheApplicationKey_IsATargetOfTheExtendedKeysTheLockKeysAndTheFnLayer() {
        RemapTarget application = PhysicalKey.Application;

        Assert.Contains(application, KeyRemapRules.GetAllowedTargets(PhysicalKey.Menu, FullSize));
        Assert.Contains(application, KeyRemapRules.GetAllowedTargets(PhysicalKey.F13, FullSize));
        Assert.Contains(application, KeyRemapRules.GetAllowedTargets(PhysicalKey.Eject, FullSize));
        Assert.Contains(application, KeyRemapRules.GetAllowedTargets(PhysicalKey.TouchId, FullSize));
        Assert.Contains(application, KeyRemapRules.GetAllowedTargets(PhysicalKey.A, FullSize, fnLayer: true));
    }

    [Fact]
    public void Encode_SendsTheApplicationKeyAsItsHidUsage() {
        Assert.Equal((0x00000076u, 0x00000065u), DriverRuleEncoder.Encode(PhysicalKey.Menu, PhysicalKey.Application, fnLayer: false));
    }

    [Fact]
    public void FnLockAware_IsDroppedForAPlainRule() {
        KeyRule? plain = DriverRuleEncoder.CreateRule(PhysicalKey.F13, PhysicalKey.PrintScreen, fnLayer: false, fnLockAware: true);
        KeyRule? withFn = DriverRuleEncoder.CreateRule(PhysicalKey.F13, PhysicalKey.PrintScreen, fnLayer: true, fnLockAware: true);

        Assert.False(plain!.FnLockAware);
        Assert.True(withFn!.FnLockAware);
    }

    [Fact]
    public void Encode_PutsModifiersIntoTheMaskAndFnIntoTheSourceFlag() {
        Assert.Equal((0x00040000u, 0x00080000u), DriverRuleEncoder.Encode(PhysicalKey.LeftOption, PhysicalKey.LeftCommand, fnLayer: false));
        Assert.Equal((0x000000F0u, 0x000000F1u), DriverRuleEncoder.Encode(PhysicalKey.Eject, PhysicalKey.Fn, fnLayer: false));
        Assert.Equal((0x01000050u, 0x0000004Au), DriverRuleEncoder.Encode(PhysicalKey.Left, PhysicalKey.Home, fnLayer: true));
    }

    [Fact]
    public void Encode_SendsTheLockAndTheTouchIdKeysAsTheLockCode() {
        Assert.Equal((0x000000F2u, 0x00000049u), DriverRuleEncoder.Encode(PhysicalKey.Lock, PhysicalKey.Insert, fnLayer: false));
        Assert.Equal((0x000000F2u, 0x00000049u), DriverRuleEncoder.Encode(PhysicalKey.TouchId, PhysicalKey.Insert, fnLayer: false));
    }

    [Fact]
    public void CreateRule_KeepsTheTouchIdKeyAsTheSource() {
        KeyRule rule = DriverRuleEncoder.CreateRule(PhysicalKey.TouchId, PhysicalKey.Insert, fnLayer: false, fnLockAware: false)!;

        Assert.Equal([PhysicalKey.TouchId], rule.Source);
        Assert.Equal(0xF2u, rule.RawSource);
        Assert.Equal("Touch ID -> Insert", rule.Describe());
    }

    [Theory]
    [MemberData(nameof(Layers))]
    public void MediaTargets_AreOfferedToKeysOnly(bool fnLayer) {
        Assert.Contains(RemapTarget.Media(0xE9), KeyRemapRules.GetAllowedTargets(PhysicalKey.F13, FullSize, fnLayer: fnLayer));
        Assert.DoesNotContain(KeyRemapRules.GetAllowedTargets(PhysicalKey.LeftShift, FullSize, fnLayer: fnLayer), target => target.IsMedia);
        Assert.DoesNotContain(KeyRemapRules.GetAllowedTargets(PhysicalKey.Eject, FullSize, fnLayer: fnLayer), target => target.IsMedia);
    }

    [Fact]
    public void Encode_MarksAMediaTargetAsConsumer() {
        Assert.Equal((0x01000041u, 0x010000CDu), DriverRuleEncoder.Encode(PhysicalKey.F8, RemapTarget.Media(0xCD), fnLayer: true));
        Assert.Equal((0x00000068u, 0x010000E9u), DriverRuleEncoder.Encode(PhysicalKey.F13, RemapTarget.Media(0xE9), fnLayer: false));
    }

    [Fact]
    public void EveryMediaUsage_HasAGlyphAndAName() {
        foreach (int usage in ConsumerUsageNames.Usages) {
            Assert.NotNull(_4MagicKeysPlus.Views.Glyphs.ForConsumerUsage(usage));
            Assert.DoesNotContain("Consumer 0x", ConsumerUsageNames.Get(usage));
        }
    }

    private static bool HasF13(KeyboardModel model) {
        return KeyboardLayout.GetKeys(model, LayoutStandard.Ansi).SelectMany(row => row.RowKeys).Any(key => key.Kind == PhysicalKey.F13);
    }

    private static RemapTarget TargetOf(KeyRule rule) {
        if (rule.ConsumerUsage is int usage) {
            return RemapTarget.Media(usage);
        }
        return rule.TargetKey != PhysicalKey.None ? rule.TargetKey : rule.TargetModifiers.Single();
    }
}
