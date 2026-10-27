namespace _4MagicKeysPlus.Views;

// Segoe MDL2 Assets codepoints - ships with Windows, no extra font/asset needed.
// Some plausible-looking guesses (a codepoint where a series seems to go on) turned out to
// render as unrelated or duplicate shapes, so don't add codepoints here without rendering first.
public static class Glyphs {
    public const string Bluetooth = "";
    public const string Usb = "";
    public const string KeyboardClassic = ""; // keyboard pictogram, used for the Keys nav tab
    public const string Flash = ""; // lightning bolt, used for the Quick Launch nav tab
    public const string Settings = ""; // gear, used for the Settings nav tab

    // A glyph for every 10% of the charge, of the battery and of the battery with a plug (charging). Battery0 (E850, empty) ..
    // Battery9 (E859) are contiguous, and so are BatteryCharging0 (E85A) .. BatteryCharging8 (E862); the 90% charging glyph (E83E)
    // and both 100% ones (E83F, EA93) lie elsewhere. Checked by rendering both series.
    private static readonly string[] BatteryLevels = ["\uE850", "\uE851", "\uE852", "\uE853", "\uE854", "\uE855", "\uE856", "\uE857", "\uE858", "\uE859", "\uE83F"];
    private static readonly string[] BatteryChargingLevels = ["\uE85A", "\uE85B", "\uE85C", "\uE85D", "\uE85E", "\uE85F", "\uE860", "\uE861", "\uE862", "\uE83E", "\uEA93"];

    // The level is rounded down to a step of 10.
    public static string BatteryLevel(int percent, bool charging = false) {
        string[] levels = charging ? BatteryChargingLevels : BatteryLevels;
        return levels[Math.Clamp(percent / 10, 0, levels.Length - 1)];
    }

    // Quick launch cards: edit (pencil), done (check), delete (cross), add (plus), and the stand-in
    // for a file that has no icon of its own (all-apps grid).
    public const string Edit = "";
    public const string Accept = "";
    public const string Delete = "";
    public const string Add = "";
    public const string AppFallback = "";

    // Drag handle of a Quick launch card.
    public const string Grip = "\uE76F";

    // Outcome of applying a mapping (filled circle with a check, warning triangle, filled circle with a cross).
    public const string ResultSuccess = "\uEC61";
    public const string ResultWarning = "\uE814";
    public const string ResultError = "\uEB90";

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
