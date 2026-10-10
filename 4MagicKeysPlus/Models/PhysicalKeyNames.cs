using System.Text;

namespace _4MagicKeysPlus.Models;

// Display names are the enum identifiers split into words ("LeftControl" -> "Left Control"),
// not localized: the keys are labelled in Latin on every keyboard layout this app supports.
public static class PhysicalKeyNames {
    // Identifiers that don't read as words: D1 is the digit row, EqualsKey avoids the C# keyword clash.
    private static readonly IReadOnlyDictionary<PhysicalKey, string> Overrides = new Dictionary<PhysicalKey, string> {
        [PhysicalKey.D1] = "1",
        [PhysicalKey.D2] = "2",
        [PhysicalKey.D3] = "3",
        [PhysicalKey.D4] = "4",
        [PhysicalKey.D5] = "5",
        [PhysicalKey.D6] = "6",
        [PhysicalKey.D7] = "7",
        [PhysicalKey.D8] = "8",
        [PhysicalKey.D9] = "9",
        [PhysicalKey.D0] = "0",
        [PhysicalKey.EqualsKey] = "Equals",
    };

    public static string GetDisplayName(PhysicalKey key) {
        return Overrides.TryGetValue(key, out string? name) ? name : SplitWords(key.ToString());
    }

    internal static string SplitWords(string identifier) {
        StringBuilder words = new StringBuilder(identifier.Length + 3);
        for (int i = 0; i < identifier.Length; i++) {
            if (i > 0 && char.IsLower(identifier[i - 1]) && (char.IsUpper(identifier[i]) || char.IsDigit(identifier[i]))) {
                words.Append(' ');
            }
            words.Append(identifier[i]);
        }
        return words.ToString();
    }
}
