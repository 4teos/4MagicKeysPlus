using System.Text.RegularExpressions;
using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlusTests;

public class PhysicalKeyNamesTests {
    [Theory]
    [InlineData(PhysicalKey.LeftControl, "Left Control")]
    [InlineData(PhysicalKey.RightCommand, "Right Command")]
    [InlineData(PhysicalKey.PrintScreen, "Print Screen")]
    [InlineData(PhysicalKey.PauseBreak, "Pause Break")]
    [InlineData(PhysicalKey.CapsLock, "Caps Lock")]
    [InlineData(PhysicalKey.OpeningBracket, "Opening Bracket")]
    [InlineData(PhysicalKey.NumPadEnter, "Num Pad Enter")]
    [InlineData(PhysicalKey.NumPad1, "Num Pad 1")]
    [InlineData(PhysicalKey.F13, "F13")]
    [InlineData(PhysicalKey.Fn, "Fn")]
    [InlineData(PhysicalKey.A, "A")]
    [InlineData(PhysicalKey.Home, "Home")]
    public void GetDisplayName_SplitsIdentifierIntoWords(PhysicalKey key, string expected) {
        Assert.Equal(expected, PhysicalKeyNames.GetDisplayName(key));
    }

    [Theory]
    [InlineData(PhysicalKey.D1, "1")]
    [InlineData(PhysicalKey.D0, "0")]
    [InlineData(PhysicalKey.EqualsKey, "Equals")]
    public void GetDisplayName_UsesOverridesForIdentifiersThatDoNotReadAsWords(PhysicalKey key, string expected) {
        Assert.Equal(expected, PhysicalKeyNames.GetDisplayName(key));
    }

    [Fact]
    public void GetDisplayName_ProducesCleanNamesForEveryKey() {
        foreach (PhysicalKey key in Enum.GetValues<PhysicalKey>()) {
            string name = PhysicalKeyNames.GetDisplayName(key);

            Assert.False(string.IsNullOrWhiteSpace(name), key.ToString());
            Assert.DoesNotContain("  ", name);
            Assert.False(Regex.IsMatch(name, @"^D\d$"), $"{key} reads as {name}");
        }
    }
}
