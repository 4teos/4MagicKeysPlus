using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlusTests;

public class DriverKeyRemapConfigTests {
    [Fact]
    public void GetRemappedKeys_ContainsEverySourceOfTheUserSetup() {
        IReadOnlySet<PhysicalKey> keys = UserRules.Config(fnLockEnabled: true).GetRemappedKeys();

        HashSet<PhysicalKey> expected = [
            PhysicalKey.Eject, PhysicalKey.Fn,
            PhysicalKey.F13, PhysicalKey.F14, PhysicalKey.F15,
            PhysicalKey.LeftOption, PhysicalKey.LeftCommand, PhysicalKey.RightOption, PhysicalKey.RightCommand,
            PhysicalKey.Left, PhysicalKey.Right, PhysicalKey.Up, PhysicalKey.Down,
            PhysicalKey.Enter, PhysicalKey.P, PhysicalKey.B, PhysicalKey.S,
            PhysicalKey.F1, PhysicalKey.F2, PhysicalKey.F7, PhysicalKey.F8, PhysicalKey.F9,
            PhysicalKey.F10, PhysicalKey.F11, PhysicalKey.F12,
        ];
        Assert.True(expected.SetEquals(keys));
    }

    [Fact]
    public void GetRemappedKeys_IsEmpty_WithoutRules() {
        DriverKeyRemapConfig config = new DriverKeyRemapConfig([], false);

        Assert.Empty(config.GetRemappedKeys());
    }

    [Fact]
    public void DescribeTargets_ShowsFnLayerRuleAsFnCombo() {
        Assert.Equal(["Fn: Home"], UserRules.Config(fnLockEnabled: true).DescribeTargets(PhysicalKey.Left));
    }

    [Fact]
    public void DescribeTargets_IgnoresFnLockForRulesThatAreNotFnLockAware() {
        Assert.Equal(["Fn: Home"], UserRules.Config(fnLockEnabled: false).DescribeTargets(PhysicalKey.Left));
    }

    [Fact]
    public void DescribeTargets_ShowsMediaKeyWithFn_WhileFnLockIsOn() {
        Assert.Equal(["Fn: Play/Pause"], UserRules.Config(fnLockEnabled: true).DescribeTargets(PhysicalKey.F8));
    }

    [Fact]
    public void DescribeTargets_ShowsMediaKeyWithoutFn_WhileFnLockIsOff() {
        Assert.Equal(["Play/Pause"], UserRules.Config(fnLockEnabled: false).DescribeTargets(PhysicalKey.F8));
    }

    [Fact]
    public void DescribeTargets_DescribesVirtualAndModifierTargets() {
        DriverKeyRemapConfig config = UserRules.Config(fnLockEnabled: true);

        Assert.Equal(["Fn"], config.DescribeTargets(PhysicalKey.Eject));
        Assert.Equal(["Insert"], config.DescribeTargets(PhysicalKey.Fn));
        Assert.Equal(["Left Command"], config.DescribeTargets(PhysicalKey.LeftOption));
        Assert.Equal(["Print Screen"], config.DescribeTargets(PhysicalKey.F13));
    }

    [Fact]
    public void DescribeTargets_IsEmptyForAnUnmappedKey() {
        Assert.Empty(UserRules.Config(fnLockEnabled: true).DescribeTargets(PhysicalKey.A));
    }

    [Fact]
    public void DescribeTargets_JoinsKeyAndModifiers() {
        KeyRule rule = _4MagicKeysPlus.Services.DriverConfigParser.ParseRule(0x68, 0x00030046, false)!;

        Assert.Equal("Left Control + Left Shift + Print Screen", rule.DescribeTarget());
    }

    [Fact]
    public void WithRule_ReplacesOnlyTheRuleOfTheSameLayer() {
        DriverKeyRemapConfig config = UserRules.Config(fnLockEnabled: true);
        KeyRule plainLeft = DriverRuleEncoder.CreateRule(PhysicalKey.Left, PhysicalKey.Up, fnLayer: false, fnLockAware: false)!;

        DriverKeyRemapConfig edited = config.WithRule(plainLeft).WithRule(
            DriverRuleEncoder.CreateRule(PhysicalKey.Left, PhysicalKey.Down, fnLayer: false, fnLockAware: false)!);

        Assert.Equal(config.Rules.Count + 1, edited.Rules.Count);
        Assert.Equal(new[] { "Fn: Home", "Down" }, edited.DescribeTargets(PhysicalKey.Left));
    }

    [Fact]
    public void WithoutRules_RemovesEveryLayerOfTheKey() {
        DriverKeyRemapConfig config = UserRules.Config(fnLockEnabled: true);

        DriverKeyRemapConfig edited = config.WithoutRules(PhysicalKey.F8);

        Assert.False(edited.HasRules(PhysicalKey.F8));
        Assert.Equal(config.Rules.Count - 1, edited.Rules.Count);
        Assert.True(edited.HasRules(PhysicalKey.F7));
    }

    [Fact]
    public void Editor_KeepsEditsAndLeavesTheOtherLayerAlone() {
        FakeStore store = new();
        KeyRemapEditor editor = new(UserRules.Config(fnLockEnabled: true).Rules, store, new FakeDriver());

        editor.SetMapping(PhysicalKey.Left, PhysicalKey.F20, fnLayer: false, fnLockAware: false);

        Assert.Equal(26, editor.Rules.Count);
        Assert.Equal(PhysicalKey.F20, KeyRemapRules.GetCurrentTarget(new DriverKeyRemapConfig(editor.Rules, true), PhysicalKey.Left));
        Assert.Contains(editor.Rules, rule => rule.FnLayer && rule.Source.Contains(PhysicalKey.Left));

        editor.RemoveMappings(PhysicalKey.Left);

        Assert.Equal(24, editor.Rules.Count);
    }

    [Fact]
    public void Editor_RefusesAMappingTheDriverWouldDrop() {
        KeyRemapEditor editor = new([], new FakeStore(), new FakeDriver());

        Assert.Throws<InvalidOperationException>(() => editor.SetMapping(PhysicalKey.A, PhysicalKey.Fn, fnLayer: false, fnLockAware: false));
    }
}
