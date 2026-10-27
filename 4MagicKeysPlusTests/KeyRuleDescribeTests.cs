using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

public sealed class KeyRuleDescribeTests {
    private static string Describe(PhysicalKey source, RemapTarget target, bool fnLayer = false) {
        return DriverRuleEncoder.CreateRule(source, target, fnLayer, fnLockAware: false)!.Describe();
    }

    [Fact]
    public void AKeyThatBecomesAnotherKey_IsSourceArrowTarget() {
        Assert.Equal("F13 -> Print Screen", Describe(PhysicalKey.F13, PhysicalKey.PrintScreen));
    }

    [Fact]
    public void ARuleOfTheFnLayer_StartsWithFn() {
        Assert.Equal("Fn+Left -> Home", Describe(PhysicalKey.Left, PhysicalKey.Home, fnLayer: true));
    }

    [Fact]
    public void AMediaTarget_IsNamedByItsUsage() {
        Assert.Equal("Fn+F8 -> Play/Pause", Describe(PhysicalKey.F8, RemapTarget.Media(0xCD), fnLayer: true));
    }

    [Fact]
    public void AModifierThatBecomesAnotherModifier_IsNamedByBoth() {
        Assert.Equal("Left Option -> Left Command", Describe(PhysicalKey.LeftOption, PhysicalKey.LeftCommand));
    }

    [Fact]
    public void ATargetOfModifiersAndAKey_JoinsThemWithPlus() {
        // F13 -> Left Control + C: a combination, which only the registry can have.
        KeyRule rule = DriverConfigParser.ParseRule(0x00000068, 0x00010006, fnLockAware: false)!;

        Assert.Equal("F13 -> Left Control + C", rule.Describe());
    }

    [Fact]
    public void EjectAndFn_AreNamedAsTheyAre() {
        KeyRule rule = DriverConfigParser.ParseRule(0x000000F0, 0x000000F1, fnLockAware: false)!;

        Assert.Equal($"{PhysicalKeyNames.GetDisplayName(PhysicalKey.Eject)} -> {PhysicalKeyNames.GetDisplayName(PhysicalKey.Fn)}", rule.Describe());
    }
}
