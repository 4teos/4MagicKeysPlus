using System.Text;

namespace _4MagicKeysPlus.Core;

// Win32 virtual-key code and MOD_* bits as a person reads them: "Ctrl+Alt+F16".
public static class HotkeyText {
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    private static readonly Dictionary<uint, string> Names = new() {
        [0x08] = "Backspace", [0x09] = "Tab", [0x0D] = "Enter", [0x13] = "Pause", [0x14] = "Caps Lock",
        [0x1B] = "Esc", [0x20] = "Space", [0x21] = "Page Up", [0x22] = "Page Down", [0x23] = "End",
        [0x24] = "Home", [0x25] = "Left", [0x26] = "Up", [0x27] = "Right", [0x28] = "Down",
        [0x2C] = "Print Screen", [0x2D] = "Insert", [0x2E] = "Delete",
        [0x6A] = "Num *", [0x6B] = "Num +", [0x6D] = "Num -", [0x6E] = "Num .", [0x6F] = "Num /",
        [0x90] = "Num Lock", [0x91] = "Scroll Lock",
        [0xBA] = ";", [0xBB] = "=", [0xBC] = ",", [0xBD] = "-", [0xBE] = ".", [0xBF] = "/",
        [0xC0] = "`", [0xDB] = "[", [0xDC] = "\\", [0xDD] = "]", [0xDE] = "'",
    };

    public static string Format(uint virtualKey, uint modifiers) {
        StringBuilder text = new();
        if ((modifiers & ModControl) != 0) {
            text.Append("Ctrl+");
        }
        if ((modifiers & ModAlt) != 0) {
            text.Append("Alt+");
        }
        if ((modifiers & ModShift) != 0) {
            text.Append("Shift+");
        }
        if ((modifiers & ModWin) != 0) {
            text.Append("Win+");
        }
        return text.Append(KeyName(virtualKey)).ToString();
    }

    public static string KeyName(uint virtualKey) {
        return virtualKey switch {
            >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
            >= 0x60 and <= 0x69 => $"Num {virtualKey - 0x60}",
            >= 0x70 and <= 0x87 => $"F{virtualKey - 0x6F}",
            _ => Names.TryGetValue(virtualKey, out string? name) ? name : $"VK 0x{virtualKey:X2}",
        };
    }
}
