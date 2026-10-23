using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

public class ThemeManagerTests {
    [Theory]
    [InlineData(ThemeChoice.Light, false, true)]
    [InlineData(ThemeChoice.Light, true, true)]
    [InlineData(ThemeChoice.Dark, false, false)]
    [InlineData(ThemeChoice.Dark, true, false)]
    [InlineData(ThemeChoice.System, false, false)]
    [InlineData(ThemeChoice.System, true, true)]
    public void OnlySystemFollowsWindows(ThemeChoice choice, bool systemUsesLight, bool expected) {
        Assert.Equal(expected, ThemeManager.UseLight(choice, systemUsesLight));
    }
}
