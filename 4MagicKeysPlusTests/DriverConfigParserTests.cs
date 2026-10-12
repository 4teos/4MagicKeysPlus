using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

public class DriverConfigParserTests {
    [Fact]
    public void ParseRule_AcceptsEveryRuleOfTheUserSetup() {
        foreach ((uint source, uint target, bool fnLockAware) in UserRules.Raw) {
            Assert.NotNull(DriverConfigParser.ParseRule(source, target, fnLockAware));
        }
    }

    [Fact]
    public void ParseRule_DecodesKeyToKey() {
        KeyRule rule = DriverConfigParser.ParseRule(0x68, 0x46, false)!;

        Assert.Equal([PhysicalKey.F13], rule.Source);
        Assert.False(rule.FnLayer);
        Assert.Equal(PhysicalKey.PrintScreen, rule.TargetKey);
        Assert.Empty(rule.TargetModifiers);
        Assert.Null(rule.ConsumerUsage);
    }

    [Fact]
    public void ParseRule_DecodesFnLayerKeyRule() {
        KeyRule rule = DriverConfigParser.ParseRule(0x01000050, 0x4A, false)!;

        Assert.Equal([PhysicalKey.Left], rule.Source);
        Assert.True(rule.FnLayer);
        Assert.Equal(PhysicalKey.Home, rule.TargetKey);
    }

    [Fact]
    public void ParseRule_DecodesModifierSwap() {
        KeyRule rule = DriverConfigParser.ParseRule(0x00040000, 0x00080000, false)!;

        Assert.Equal([PhysicalKey.LeftOption], rule.Source);
        Assert.Equal([PhysicalKey.LeftCommand], rule.TargetModifiers);
        Assert.Equal(PhysicalKey.None, rule.TargetKey);
    }

    [Fact]
    public void ParseRule_DecodesModifierToVirtualAndBack() {
        KeyRule toFn = DriverConfigParser.ParseRule(0x00080000, 0xF1, false)!;
        KeyRule fromEject = DriverConfigParser.ParseRule(0xF0, 0x00020000, false)!;

        Assert.Equal(PhysicalKey.Fn, toFn.TargetKey);
        Assert.Equal([PhysicalKey.Eject], fromEject.Source);
        Assert.Equal([PhysicalKey.LeftShift], fromEject.TargetModifiers);
    }

    [Fact]
    public void ParseRule_DecodesFnLayerKeyToModifiers() {
        KeyRule rule = DriverConfigParser.ParseRule(0x01000068, 0x00030000, false)!;

        Assert.Equal(PhysicalKey.None, rule.TargetKey);
        Assert.Equal([PhysicalKey.LeftControl, PhysicalKey.LeftShift], rule.TargetModifiers);
    }

    [Fact]
    public void ParseRule_DecodesKeyPlusModifiersTarget() {
        KeyRule rule = DriverConfigParser.ParseRule(0x68, 0x00010046, false)!;

        Assert.Equal(PhysicalKey.PrintScreen, rule.TargetKey);
        Assert.Equal([PhysicalKey.LeftControl], rule.TargetModifiers);
    }

    [Fact]
    public void ParseRule_DecodesConsumerTarget_WithoutTreatingItAsAKey() {
        KeyRule rule = DriverConfigParser.ParseRule(0x01000041, 0x010000CD, true)!;

        Assert.Equal(0xCD, rule.ConsumerUsage);
        Assert.Equal(PhysicalKey.None, rule.TargetKey);
        Assert.True(rule.FnLockAware);
    }

    [Fact]
    public void ParseRule_KeepsTargetCodeThatHasNoPhysicalKey() {
        // 0xE0 is the usage of Left Control in HID, but PhysicalKey.LeftShift in this app.
        KeyRule rule = DriverConfigParser.ParseRule(0x68, 0xE0, false)!;

        Assert.Equal(PhysicalKey.None, rule.TargetKey);
        Assert.Equal(0xE0, rule.TargetCode);
        Assert.Equal("0xE0", rule.DescribeTarget());
    }

    [Theory]
    [InlineData(0x02000068u, 0x46u, "reserved bit in source")]
    [InlineData(0x68u, 0x02000046u, "reserved bit in target")]
    [InlineData(0x80000068u, 0x46u, "top bit in source")]
    [InlineData(0x168u, 0x46u, "source code above 0xFF")]
    [InlineData(0x1F1u, 0x46u, "source code that would wrap to Fn")]
    [InlineData(0x68u, 0x1F1u, "target code that would wrap to Fn")]
    [InlineData(0x01040000u, 0x00080000u, "Fn flag on a modifier source")]
    [InlineData(0x010000F1u, 0x49u, "Fn flag on a Fn source")]
    [InlineData(0x00040000u, 0x010000CDu, "consumer target on a modifier source")]
    [InlineData(0xF1u, 0x010000CDu, "consumer target on a Fn source")]
    [InlineData(0x00040068u, 0x46u, "modifiers together with a key code in the source")]
    [InlineData(0x00040000u, 0x49u, "modifier source targeting a key")]
    [InlineData(0x000400F1u, 0x00080000u, "Fn source carrying modifiers")]
    [InlineData(0x68u, 0xF1u, "key targeting Fn")]
    [InlineData(0x68u, 0xF0u, "key targeting Eject")]
    [InlineData(0x68u, 0x00000000u, "key with an empty target")]
    [InlineData(0x00040000u, 0x00000000u, "modifier with an empty target")]
    [InlineData(0x00000000u, 0x46u, "empty source")]
    [InlineData(0x68u, 0x01000000u, "consumer target with usage 0")]
    [InlineData(0x68u, 0x01000400u, "consumer usage above 0x3FF")]
    [InlineData(0xE0u, 0x46u, "source key code that is a HID modifier usage")]
    [InlineData(0xF0u, 0x00000000u, "Eject with an empty target")]
    [InlineData(0xF0u, 0x000200F1u, "Fn/Eject target carrying modifiers")]
    public void ParseRule_RejectsWhatTheDriverIgnores(uint source, uint target, string reason) {
        Assert.True(DriverConfigParser.ParseRule(source, target, false) is null, reason);
    }
}
