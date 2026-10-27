using _4MagicKeysPlus.Views;

namespace _4MagicKeysPlusTests;

public class GlyphsTests {
    [Theory]
    [InlineData(0, "\uE850")]
    [InlineData(9, "\uE850")]
    [InlineData(10, "\uE851")]
    [InlineData(55, "\uE855")]
    [InlineData(99, "\uE859")]
    [InlineData(100, "\uE83F")]
    [InlineData(-5, "\uE850")]
    [InlineData(150, "\uE83F")]
    public void TheBatteryGlyph_FollowsTheLevel(int percent, string expected) {
        Assert.Equal(expected, Glyphs.BatteryLevel(percent));
    }

    [Theory]
    [InlineData(0, "\uE85A")]
    [InlineData(9, "\uE85A")]
    [InlineData(10, "\uE85B")]
    [InlineData(55, "\uE85F")]
    [InlineData(80, "\uE862")]
    [InlineData(90, "\uE83E")]
    [InlineData(99, "\uE83E")]
    [InlineData(100, "\uEA93")]
    [InlineData(-5, "\uE85A")]
    [InlineData(150, "\uEA93")]
    public void TheChargingGlyph_FollowsTheLevelToo(int percent, string expected) {
        Assert.Equal(expected, Glyphs.BatteryLevel(percent, charging: true));
    }

    // The two series are not told apart by the shape alone: a glyph of one that is also in the other would show a battery that is
    // charging as one that is not, or the other way round.
    [Fact]
    public void EachSeriesHasAGlyphForEveryTenPercent_AndTheSeriesDoNotShareOne() {
        string[] plain = [.. Enumerable.Range(0, 11).Select(step => Glyphs.BatteryLevel(step * 10))];
        string[] charging = [.. Enumerable.Range(0, 11).Select(step => Glyphs.BatteryLevel(step * 10, charging: true))];

        Assert.Equal(11, plain.Distinct().Count());
        Assert.Equal(11, charging.Distinct().Count());
        Assert.Empty(plain.Intersect(charging));
    }
}
