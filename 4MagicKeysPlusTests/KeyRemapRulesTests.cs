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

    // What the editor offers must be what the driver accepts: a rule it would drop is a mapping
    // that silently does nothing.
    [Theory]
    [MemberData(nameof(Layers))]
    public void EveryOfferedMapping_IsAcceptedByTheDriverParser(bool fnLayer) {
        int checkedPairs = 0;
        foreach (PhysicalKey source in Enum.GetValues<PhysicalKey>()) {
            foreach (RemapTarget target in KeyRemapRules.GetAllowedTargets(source, fnLayer)) {
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
        IReadOnlyList<RemapTarget> targets = KeyRemapRules.GetAllowedTargets(lockKey);

        Assert.Contains((RemapTarget)PhysicalKey.LeftControl, targets);
        Assert.Contains((RemapTarget)PhysicalKey.Fn, targets);
        Assert.Contains((RemapTarget)PhysicalKey.Eject, targets);
        Assert.Contains((RemapTarget)PhysicalKey.F13, targets);
    }

    [Theory]
    [MemberData(nameof(Layers))]
    public void TheLockKeys_AreNoTargetOfAnything(bool fnLayer) {
        foreach (PhysicalKey source in Enum.GetValues<PhysicalKey>()) {
            IReadOnlyList<RemapTarget> targets = KeyRemapRules.GetAllowedTargets(source, fnLayer);

            Assert.DoesNotContain((RemapTarget)PhysicalKey.Lock, targets);
            Assert.DoesNotContain((RemapTarget)PhysicalKey.TouchId, targets);
        }
    }

    [Fact]
    public void TheMenuKey_IsRemappedLikeTheExtendedKeys() {
        Assert.Contains((RemapTarget)PhysicalKey.PrintScreen, KeyRemapRules.GetAllowedTargets(PhysicalKey.Menu));
        Assert.Contains(RemapTarget.Media(0xE9), KeyRemapRules.GetAllowedTargets(PhysicalKey.Menu));
        Assert.True(KeyRemapRules.CanRemap(PhysicalKey.Menu, fnLayer: true));
    }

    [Fact]
    public void TheApplicationKey_IsATargetOfTheExtendedKeysTheLockKeysAndTheFnLayer() {
        RemapTarget application = PhysicalKey.Application;

        Assert.Contains(application, KeyRemapRules.GetAllowedTargets(PhysicalKey.Menu));
        Assert.Contains(application, KeyRemapRules.GetAllowedTargets(PhysicalKey.F13));
        Assert.Contains(application, KeyRemapRules.GetAllowedTargets(PhysicalKey.Eject));
        Assert.Contains(application, KeyRemapRules.GetAllowedTargets(PhysicalKey.TouchId));
        Assert.Contains(application, KeyRemapRules.GetAllowedTargets(PhysicalKey.A, fnLayer: true));
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
        Assert.Contains(RemapTarget.Media(0xE9), KeyRemapRules.GetAllowedTargets(PhysicalKey.F13, fnLayer: fnLayer));
        Assert.DoesNotContain(KeyRemapRules.GetAllowedTargets(PhysicalKey.LeftShift, fnLayer: fnLayer), target => target.IsMedia);
        Assert.DoesNotContain(KeyRemapRules.GetAllowedTargets(PhysicalKey.Eject, fnLayer: fnLayer), target => target.IsMedia);
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

    private static RemapTarget TargetOf(KeyRule rule) {
        if (rule.ConsumerUsage is int usage) {
            return RemapTarget.Media(usage);
        }
        return rule.TargetKey != PhysicalKey.None ? rule.TargetKey : rule.TargetModifiers.Single();
    }
}
