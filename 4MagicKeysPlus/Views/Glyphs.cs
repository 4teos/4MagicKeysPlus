namespace _4MagicKeysPlus.Views;

// Segoe MDL2 Assets codepoints - ships with Windows, no extra font/asset needed.
// Verified by rendering each glyph on real hardware before relying on it (fe-pc, 2026-09-15) -
// several plausible-looking guesses (e.g. a dedicated "charging level" series) turned out to
// render as unrelated or duplicate shapes, so don't add codepoints here without rendering first.
internal static class Glyphs {
    public const string Bluetooth = "";
    public const string Usb = "";
    public const string BatteryCharging = ""; // single "battery with power plug" pictogram

    // Battery0 (empty) .. Battery10 (full), contiguous codepoint range.
    private static readonly string[] BatteryLevels = [
        "", "", "", "", "",
        "", "", "", "", "", "",
    ];

    public static string BatteryLevel(int percent) {
        int index = Math.Clamp(percent / 10, 0, BatteryLevels.Length - 1);
        return BatteryLevels[index];
    }
}
