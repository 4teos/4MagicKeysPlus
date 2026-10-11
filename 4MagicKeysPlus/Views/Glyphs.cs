namespace _4MagicKeysPlus.Views;

// Segoe MDL2 Assets codepoints - ships with Windows, no extra font/asset needed.
// Several plausible-looking guesses (e.g. a dedicated "charging level" series) turned out to
// render as unrelated or duplicate shapes, so don't add codepoints here without rendering first.
public static class Glyphs {
    public const string Bluetooth = "";
    public const string Usb = "";
    public const string BatteryCharging = ""; // single "battery with power plug" pictogram
    public const string KeyboardClassic = ""; // keyboard pictogram, used for the Keys nav tab
    public const string Flash = ""; // lightning bolt, used for the Quick Launch nav tab
    public const string Settings = ""; // gear, used for the Advanced nav tab

    // Battery0 (empty) .. Battery10 (full), contiguous codepoint range.
    private static readonly string[] BatteryLevels = ["", "", "", "", "", "", "", "", "", "", ""];

    public static string BatteryLevel(int percent) {
        int index = Math.Clamp(percent / 10, 0, BatteryLevels.Length - 1);
        return BatteryLevels[index];
    }

    // Media targets of the remap editor (checked by rendering them); null for an unknown usage.
    public static string? ForConsumerUsage(int usage) {
        return usage switch {
            0xB6 => "\uE892",
            0xCD => "\uE768\uE769",
            0xB5 => "\uE893",
            0xE2 => "\uE74F",
            0xEA => "\uE993",
            0xE9 => "\uE995",
            0x70 => "\uE706\uE70D",
            0x6F => "\uE706\uE70E",
            _ => null,
        };
    }
}
