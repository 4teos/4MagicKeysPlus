using _4MagicKeysPlus.Models;

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
}
