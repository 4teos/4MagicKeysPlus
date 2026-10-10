using _4MagicKeysPlus.Core;
using _4MagicKeysPlusAgent;

namespace _4MagicKeysPlusAgentTests;

public class BatteryAlertPolicyTests {
    private static KeyboardBatteryStatus Level(int percent, bool charging = false) {
        return new KeyboardBatteryStatus(percent, charging);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(50)]
    [InlineData(21)]
    public void Evaluate_ReturnsNull_AboveWarningThreshold(int percent) {
        BatteryAlertPolicy policy = new BatteryAlertPolicy();

        Assert.Null(policy.Evaluate(Level(percent)));
    }

    [Theory]
    [InlineData(20)]
    [InlineData(19)]
    public void Evaluate_ReturnsWarning_AtOrBelowTwentyPercent(int percent) {
        BatteryAlertPolicy policy = new BatteryAlertPolicy();

        Assert.Equal(20, policy.Evaluate(Level(percent)));
    }

    [Fact]
    public void Evaluate_WarnsOnlyOnce_UntilBatteryDropsFurther() {
        BatteryAlertPolicy policy = new BatteryAlertPolicy();

        Assert.Equal(20, policy.Evaluate(Level(20)));
        Assert.Null(policy.Evaluate(Level(15)));
        Assert.Null(policy.Evaluate(Level(6)));
        Assert.Equal(5, policy.Evaluate(Level(5)));
        Assert.Null(policy.Evaluate(Level(3)));
    }

    [Fact]
    public void Evaluate_ReturnsOnlyCritical_WhenBothThresholdsAreCrossedAtOnce() {
        BatteryAlertPolicy policy = new BatteryAlertPolicy();

        Assert.Equal(5, policy.Evaluate(Level(5)));
        Assert.Null(policy.Evaluate(Level(4)));
    }

    [Fact]
    public void Evaluate_StaysSilent_WhileCharging() {
        BatteryAlertPolicy policy = new BatteryAlertPolicy();

        Assert.Null(policy.Evaluate(Level(3, charging: true)));
    }

    [Fact]
    public void Evaluate_WarnsAgain_AfterChargingRearmsTheThresholds() {
        BatteryAlertPolicy policy = new BatteryAlertPolicy();

        Assert.Equal(20, policy.Evaluate(Level(19)));
        Assert.Null(policy.Evaluate(Level(60, charging: true)));
        Assert.Equal(20, policy.Evaluate(Level(18)));
    }
}
