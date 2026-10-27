using _4MagicKeysPlus.Core;
using _4MagicKeysPlusAgent;

namespace _4MagicKeysPlusAgentTests;

public class BatteryAlertsTests {
    private static BatteryTarget Target(string key, string? name = null) {
        return new BatteryTarget(key, name, new DetectedKeyboard(KeyboardProduct.A1843, KeyboardConnection.Bluetooth, $@"\\?\hid#{key}"));
    }

    private static BatteryReading Reading(string key, int percent, bool charging = false, string? name = null) {
        return new BatteryReading(Target(key, name), new KeyboardBatteryStatus(percent, charging));
    }

    [Fact]
    public void AKeyboardThatIsFine_HasNoNotice() {
        BatteryAlerts alerts = new();

        Assert.Empty(alerts.Evaluate([Reading("a", 80)]));
    }

    [Fact]
    public void AKeyboardThatRunsLow_HasANoticeWithItsNameAndItsCharge() {
        BatteryAlerts alerts = new();

        BatteryNotice notice = Assert.Single(alerts.Evaluate([Reading("a", 18, name: "Desk")]));

        Assert.Equal(new BatteryNotice("a", "Desk", 18), notice);
    }

    [Fact]
    public void SeveralKeyboardsLowAtOnce_HaveANoticeEach() {
        BatteryAlerts alerts = new();

        IReadOnlyList<BatteryNotice> notices = alerts.Evaluate([Reading("a", 18, name: "First"), Reading("b", 4, name: "Second"), Reading("c", 70)]);

        Assert.Equal([new BatteryNotice("a", "First", 18), new BatteryNotice("b", "Second", 4)], notices);
    }

    [Fact]
    public void AKeyboardIsWarnedAboutOnce_AndTheOthersAreWarnedAboutTheirOwn() {
        BatteryAlerts alerts = new();
        Assert.Single(alerts.Evaluate([Reading("a", 18)]));

        IReadOnlyList<BatteryNotice> notices = alerts.Evaluate([Reading("a", 17), Reading("b", 19)]);

        Assert.Equal("b", Assert.Single(notices).Key);
    }

    [Fact]
    public void TheNextThreshold_IsAWarningOfItsOwn_ForThatKeyboardOnly() {
        BatteryAlerts alerts = new();
        alerts.Evaluate([Reading("a", 18), Reading("b", 18)]);

        IReadOnlyList<BatteryNotice> notices = alerts.Evaluate([Reading("a", 5), Reading("b", 12)]);

        Assert.Equal(new BatteryNotice("a", null, 5), Assert.Single(notices));
    }

    [Fact]
    public void ChargingOneKeyboard_RearmsThatKeyboardOnly() {
        BatteryAlerts alerts = new();
        alerts.Evaluate([Reading("a", 18), Reading("b", 18)]);
        alerts.Evaluate([Reading("a", 60, charging: true), Reading("b", 17)]);

        IReadOnlyList<BatteryNotice> notices = alerts.Evaluate([Reading("a", 19), Reading("b", 16)]);

        Assert.Equal("a", Assert.Single(notices).Key);
    }

    [Fact]
    public void AKeyboardThatCannotBeRead_IsSkipped_AndKeepsWhatItWasWarnedAbout() {
        BatteryAlerts alerts = new();
        alerts.Evaluate([Reading("a", 18)]);

        Assert.Empty(alerts.Evaluate([new BatteryReading(Target("a"), null)]));
        Assert.Empty(alerts.Evaluate([Reading("a", 17)]));
    }

    [Fact]
    public void TheSameKey_IsTheSameKeyboard_WhicheverWayItWasRead() {
        BatteryAlerts alerts = new();
        BatteryTarget overBluetooth = Target("a");
        BatteryTarget overUsb = new("a", null, new DetectedKeyboard(KeyboardProduct.A1843, KeyboardConnection.Usb, @"\\?\hid#usb"));
        Assert.Single(alerts.Evaluate([new BatteryReading(overBluetooth, new KeyboardBatteryStatus(19, false))]));

        Assert.Empty(alerts.Evaluate([new BatteryReading(overUsb, new KeyboardBatteryStatus(18, false))]));
    }
}
