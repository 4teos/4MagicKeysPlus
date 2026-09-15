using System.Windows;
using System.Windows.Input;
using static _4MagicKeysPlus.Models.KeyboardLayout;

namespace _4MagicKeysPlus.Models;

public static class KeyboardLayout {
    public static IReadOnlyList<RowLayoutEntry> GetKeys(KeyboardModel model) {
        if (model == KeyboardModel.A1644) {
            return BuildA1644Layout();
        }
        if (model == KeyboardModel.A1843) {
            return BuildA1843Layout();
        }

        throw new ArgumentOutOfRangeException($"The keyboard model '{model}' is not supported");
    }

    private static IReadOnlyList<RowLayoutEntry> BuildA1843Layout() {
        RowLayoutEntry row1 = new RowLayoutEntry(
            new KeyLayoutEntry(PhysicalKey.Escape, "Esc") {
                Width = new SizeUnit(8),
                LabelHAlignment = HorizontalAlignment.Left,
                LabelVAlignment = VerticalAlignment.Bottom
            },
            new KeyLayoutEntry(PhysicalKey.F1, "F1"),
            new KeyLayoutEntry(PhysicalKey.F2, "F2"),
            new KeyLayoutEntry(PhysicalKey.F3, "F3"),
            new KeyLayoutEntry(PhysicalKey.F4, "F4"),
            new KeyLayoutEntry(PhysicalKey.F5, "F5"),
            new KeyLayoutEntry(PhysicalKey.F6, "F6"),
            new KeyLayoutEntry(PhysicalKey.F7, "F7"),
            new KeyLayoutEntry(PhysicalKey.F8, "F8"),
            new KeyLayoutEntry(PhysicalKey.F9, "F9"),
            new KeyLayoutEntry(PhysicalKey.F10, "F10"),
            new KeyLayoutEntry(PhysicalKey.F11, "F11"),
            new KeyLayoutEntry(PhysicalKey.F12, "F12"),
            new KeyLayoutEntry(PhysicalKey.Eject, "⏏") { GapAfter = SizeUnit.STANDARD_GAP * 2 },
            new KeyLayoutEntry(PhysicalKey.F13, "F13"),
            new KeyLayoutEntry(PhysicalKey.F14, "F14"),
            new KeyLayoutEntry(PhysicalKey.F15, "F15") { GapAfter = SizeUnit.STANDARD_GAP * 2 },
            new KeyLayoutEntry(PhysicalKey.F16, "F16"),
            new KeyLayoutEntry(PhysicalKey.F17, "F17"),
            new KeyLayoutEntry(PhysicalKey.F18, "F18"),
            new KeyLayoutEntry(PhysicalKey.F19, "F19") { GapAfter = SizeUnit.ZERO });

        RowLayoutEntry row2 = new RowLayoutEntry(
            new KeyLayoutEntry(PhysicalKey.Section, "§±"),
            new KeyLayoutEntry(PhysicalKey.D1, "1"),
            new KeyLayoutEntry(PhysicalKey.D2, "2"),
            new KeyLayoutEntry(PhysicalKey.D3, "3"),
            new KeyLayoutEntry(PhysicalKey.D4, "4"),
            new KeyLayoutEntry(PhysicalKey.D5, "5"),
            new KeyLayoutEntry(PhysicalKey.D6, "6"),
            new KeyLayoutEntry(PhysicalKey.D7, "7"),
            new KeyLayoutEntry(PhysicalKey.D8, "8"),
            new KeyLayoutEntry(PhysicalKey.D9, "9"),
            new KeyLayoutEntry(PhysicalKey.D0, "0"),
            new KeyLayoutEntry(PhysicalKey.Minus, "-"),
            new KeyLayoutEntry(PhysicalKey.EqualsKey, "="),
            new KeyLayoutEntry(PhysicalKey.Backspace, "⌫") {
                Width = new SizeUnit(8),
                GapAfter = SizeUnit.STANDARD_GAP * 2,
                LabelHAlignment = HorizontalAlignment.Right,
                LabelVAlignment = VerticalAlignment.Bottom
            },
            new KeyLayoutEntry(PhysicalKey.Function, "Fn"),
            new KeyLayoutEntry(PhysicalKey.Home, "⤒"),
            new KeyLayoutEntry(PhysicalKey.PageUp, "↑") { GapAfter = SizeUnit.STANDARD_GAP * 2 },
            new KeyLayoutEntry(PhysicalKey.NumPadClear, "⌧"),
            new KeyLayoutEntry(PhysicalKey.NumPadEquals, "="),
            new KeyLayoutEntry(PhysicalKey.NumPadDivide, "/"),
            new KeyLayoutEntry(PhysicalKey.NumPadMultiply, "*") { GapAfter = SizeUnit.ZERO });

        RowLayoutEntry row3 = new RowLayoutEntry(
            new KeyLayoutEntry(PhysicalKey.Tab, "⇥") {
                Width = new SizeUnit(8),
                LabelHAlignment = HorizontalAlignment.Left,
                LabelVAlignment = VerticalAlignment.Bottom
            },
            new KeyLayoutEntry(PhysicalKey.Q, "Q"),
            new KeyLayoutEntry(PhysicalKey.W, "W"),
            new KeyLayoutEntry(PhysicalKey.E, "E"),
            new KeyLayoutEntry(PhysicalKey.R, "R"),
            new KeyLayoutEntry(PhysicalKey.T, "T"),
            new KeyLayoutEntry(PhysicalKey.Y, "Y"),
            new KeyLayoutEntry(PhysicalKey.U, "U"),
            new KeyLayoutEntry(PhysicalKey.I, "I"),
            new KeyLayoutEntry(PhysicalKey.O, "O"),
            new KeyLayoutEntry(PhysicalKey.P, "P"),
            new KeyLayoutEntry(PhysicalKey.OpeningBracket, "["),
            new KeyLayoutEntry(PhysicalKey.ClosingBracket, "]"),
            new KeyLayoutEntry(PhysicalKey.Enter, "↩") {
                GapAfter = SizeUnit.STANDARD_GAP * 2,
                KeyRowPosition = KeyRowPosition.TWO_ROWS_UPPER_ROW,
                LabelHAlignment = HorizontalAlignment.Right,
                LabelVAlignment = VerticalAlignment.Bottom
            },
            new KeyLayoutEntry(PhysicalKey.Delete, "⌦"),
            new KeyLayoutEntry(PhysicalKey.End, "⤓"),
            new KeyLayoutEntry(PhysicalKey.PageDown, "↓") { GapAfter = SizeUnit.STANDARD_GAP * 2 },
            new KeyLayoutEntry(PhysicalKey.NumPad7, "7"),
            new KeyLayoutEntry(PhysicalKey.NumPad8, "8"),
            new KeyLayoutEntry(PhysicalKey.NumPad9, "9"),
            new KeyLayoutEntry(PhysicalKey.NumPadMinus, "-") { GapAfter = SizeUnit.ZERO });

        RowLayoutEntry row4 = new RowLayoutEntry(
            new KeyLayoutEntry(PhysicalKey.CapsLock, "⇪") {
                Width = new SizeUnit(9.5),
                LabelHAlignment = HorizontalAlignment.Left,
                LabelVAlignment = VerticalAlignment.Bottom
            },
            new KeyLayoutEntry(PhysicalKey.A, "A"),
            new KeyLayoutEntry(PhysicalKey.S, "S"),
            new KeyLayoutEntry(PhysicalKey.D, "D"),
            new KeyLayoutEntry(PhysicalKey.F, "F"),
            new KeyLayoutEntry(PhysicalKey.G, "G"),
            new KeyLayoutEntry(PhysicalKey.H, "H"),
            new KeyLayoutEntry(PhysicalKey.J, "J"),
            new KeyLayoutEntry(PhysicalKey.K, "K"),
            new KeyLayoutEntry(PhysicalKey.L, "L"),
            new KeyLayoutEntry(PhysicalKey.Semicolon, ";"),
            new KeyLayoutEntry(PhysicalKey.Quote, "'"),
            new KeyLayoutEntry(PhysicalKey.Backslash, "\\"),
            new KeyLayoutEntry(PhysicalKey.Enter, "") {
                Width = new SizeUnit(3.5),
                GapAfter = new SizeUnit(21),
                KeyRowPosition = KeyRowPosition.TWO_ROWS_LOWER_ROW
            },
            new KeyLayoutEntry(PhysicalKey.NumPad4, "4"),
            new KeyLayoutEntry(PhysicalKey.NumPad5, "5"),
            new KeyLayoutEntry(PhysicalKey.NumPad6, "6"),
            new KeyLayoutEntry(PhysicalKey.NumPadPlus, "+") { GapAfter = SizeUnit.ZERO });

        RowLayoutEntry row5 = new RowLayoutEntry(
            new KeyLayoutEntry(PhysicalKey.LeftShift, "⇧") {
                Width = new SizeUnit(6.5),
                LabelHAlignment = HorizontalAlignment.Left,
                LabelVAlignment = VerticalAlignment.Bottom
            },
            new KeyLayoutEntry(PhysicalKey.Tilde, "`"),
            new KeyLayoutEntry(PhysicalKey.Z, "Z"),
            new KeyLayoutEntry(PhysicalKey.X, "X"),
            new KeyLayoutEntry(PhysicalKey.C, "C"),
            new KeyLayoutEntry(PhysicalKey.V, "V"),
            new KeyLayoutEntry(PhysicalKey.B, "B"),
            new KeyLayoutEntry(PhysicalKey.N, "N"),
            new KeyLayoutEntry(PhysicalKey.M, "M"),
            new KeyLayoutEntry(PhysicalKey.Comma, ","),
            new KeyLayoutEntry(PhysicalKey.Period, "."),
            new KeyLayoutEntry(PhysicalKey.Slash, "/"),
            new KeyLayoutEntry(PhysicalKey.RightShift, "⇧") {
                Width = new SizeUnit(12.5),
                GapAfter = new SizeUnit(8),
                LabelHAlignment = HorizontalAlignment.Right,
                LabelVAlignment = VerticalAlignment.Bottom
            },
            new KeyLayoutEntry(PhysicalKey.Up, "▲") { GapAfter = new SizeUnit(8) },
            new KeyLayoutEntry(PhysicalKey.NumPad1, "1"),
            new KeyLayoutEntry(PhysicalKey.NumPad2, "2"),
            new KeyLayoutEntry(PhysicalKey.NumPad3, "3"),
            new KeyLayoutEntry(PhysicalKey.NumPadEnter, "") {
                GapAfter = SizeUnit.ZERO,
                KeyRowPosition = KeyRowPosition.TWO_ROWS_UPPER_ROW
            });

        RowLayoutEntry row6 = new RowLayoutEntry(
            new KeyLayoutEntry(PhysicalKey.LeftControl, "⌃") {
                Width = new SizeUnit(8),
                LabelHAlignment = HorizontalAlignment.Left,
                LabelVAlignment = VerticalAlignment.Top
            },
            new KeyLayoutEntry(PhysicalKey.LeftOption, "⌥") {
                Width = new SizeUnit(6.5),
                LabelHAlignment = HorizontalAlignment.Right,
                LabelVAlignment = VerticalAlignment.Top
            },
            new KeyLayoutEntry(PhysicalKey.LeftCommand, "⌘") {
                Width = new SizeUnit(8),
                LabelHAlignment = HorizontalAlignment.Right,
                LabelVAlignment = VerticalAlignment.Top
            },
            new KeyLayoutEntry(PhysicalKey.Space, "") { Width = new SizeUnit(35) },
            new KeyLayoutEntry(PhysicalKey.RightCommand, "⌘") {
                Width = new SizeUnit(8),
                LabelHAlignment = HorizontalAlignment.Left,
                LabelVAlignment = VerticalAlignment.Top
            },
            new KeyLayoutEntry(PhysicalKey.RightOption, "⌥") {
                Width = new SizeUnit(6.5),
                LabelHAlignment = HorizontalAlignment.Left,
                LabelVAlignment = VerticalAlignment.Top
            },
            new KeyLayoutEntry(PhysicalKey.RightControl, "⌃") {
                Width = new SizeUnit(8),
                GapAfter = SizeUnit.STANDARD_GAP * 2,
                LabelHAlignment = HorizontalAlignment.Right,
                LabelVAlignment = VerticalAlignment.Top
            },
            new KeyLayoutEntry(PhysicalKey.Left, "◀"),
            new KeyLayoutEntry(PhysicalKey.Down, "▼"),
            new KeyLayoutEntry(PhysicalKey.Right, "▶") { GapAfter = SizeUnit.STANDARD_GAP * 2 },
            new KeyLayoutEntry(PhysicalKey.NumPad0, "0") { Width = new SizeUnit(11) },
            new KeyLayoutEntry(PhysicalKey.NumPadDecimal, "."),
            new KeyLayoutEntry(PhysicalKey.NumPadEnter, "⌅") {
                GapAfter = SizeUnit.ZERO,
                KeyRowPosition = KeyRowPosition.TWO_ROWS_LOWER_ROW,
                LabelVAlignment = VerticalAlignment.Bottom
            });

        return [row1, row2, row3, row4, row5, row6];
    }

    private static IReadOnlyList<RowLayoutEntry> BuildA1644Layout() {
        RowLayoutEntry row1 = new RowLayoutEntry(
            new KeyLayoutEntry(PhysicalKey.Escape, "Esc") {
                Width = new SizeUnit(8),
                LabelHAlignment = HorizontalAlignment.Left,
                LabelVAlignment = VerticalAlignment.Bottom
            },
            new KeyLayoutEntry(PhysicalKey.F1, "F1"),
            new KeyLayoutEntry(PhysicalKey.F2, "F2"),
            new KeyLayoutEntry(PhysicalKey.F3, "F3"),
            new KeyLayoutEntry(PhysicalKey.F4, "F4"),
            new KeyLayoutEntry(PhysicalKey.F5, "F5"),
            new KeyLayoutEntry(PhysicalKey.F6, "F6"),
            new KeyLayoutEntry(PhysicalKey.F7, "F7"),
            new KeyLayoutEntry(PhysicalKey.F8, "F8"),
            new KeyLayoutEntry(PhysicalKey.F9, "F9"),
            new KeyLayoutEntry(PhysicalKey.F10, "F10"),
            new KeyLayoutEntry(PhysicalKey.F11, "F11"),
            new KeyLayoutEntry(PhysicalKey.F12, "F12"),
            new KeyLayoutEntry(PhysicalKey.Eject, "⏏") { GapAfter = SizeUnit.ZERO });

        RowLayoutEntry row2 = new RowLayoutEntry(
            new KeyLayoutEntry(PhysicalKey.Section, "§±"),
            new KeyLayoutEntry(PhysicalKey.D1, "1"),
            new KeyLayoutEntry(PhysicalKey.D2, "2"),
            new KeyLayoutEntry(PhysicalKey.D3, "3"),
            new KeyLayoutEntry(PhysicalKey.D4, "4"),
            new KeyLayoutEntry(PhysicalKey.D5, "5"),
            new KeyLayoutEntry(PhysicalKey.D6, "6"),
            new KeyLayoutEntry(PhysicalKey.D7, "7"),
            new KeyLayoutEntry(PhysicalKey.D8, "8"),
            new KeyLayoutEntry(PhysicalKey.D9, "9"),
            new KeyLayoutEntry(PhysicalKey.D0, "0"),
            new KeyLayoutEntry(PhysicalKey.Minus, "-"),
            new KeyLayoutEntry(PhysicalKey.EqualsKey, "="),
            new KeyLayoutEntry(PhysicalKey.Backspace, "⌫") {
                Width = new SizeUnit(8),
                GapAfter = SizeUnit.ZERO,
                LabelHAlignment = HorizontalAlignment.Right,
                LabelVAlignment = VerticalAlignment.Bottom
            });

        RowLayoutEntry row3 = new RowLayoutEntry(
            new KeyLayoutEntry(PhysicalKey.Tab, "⇥") {
                Width = new SizeUnit(8),
                LabelHAlignment = HorizontalAlignment.Left,
                LabelVAlignment = VerticalAlignment.Bottom
            },
            new KeyLayoutEntry(PhysicalKey.Q, "Q"),
            new KeyLayoutEntry(PhysicalKey.W, "W"),
            new KeyLayoutEntry(PhysicalKey.E, "E"),
            new KeyLayoutEntry(PhysicalKey.R, "R"),
            new KeyLayoutEntry(PhysicalKey.T, "T"),
            new KeyLayoutEntry(PhysicalKey.Y, "Y"),
            new KeyLayoutEntry(PhysicalKey.U, "U"),
            new KeyLayoutEntry(PhysicalKey.I, "I"),
            new KeyLayoutEntry(PhysicalKey.O, "O"),
            new KeyLayoutEntry(PhysicalKey.P, "P"),
            new KeyLayoutEntry(PhysicalKey.OpeningBracket, "["),
            new KeyLayoutEntry(PhysicalKey.ClosingBracket, "]"),
            new KeyLayoutEntry(PhysicalKey.Enter, "↩") {
                GapAfter = SizeUnit.ZERO,
                KeyRowPosition = KeyRowPosition.TWO_ROWS_UPPER_ROW,
                LabelHAlignment = HorizontalAlignment.Right,
                LabelVAlignment = VerticalAlignment.Bottom
            });

        RowLayoutEntry row4 = new RowLayoutEntry(
            new KeyLayoutEntry(PhysicalKey.CapsLock, "⇪") {
                Width = new SizeUnit(9.5),
                LabelHAlignment = HorizontalAlignment.Left,
                LabelVAlignment = VerticalAlignment.Bottom
            },
            new KeyLayoutEntry(PhysicalKey.A, "A"),
            new KeyLayoutEntry(PhysicalKey.S, "S"),
            new KeyLayoutEntry(PhysicalKey.D, "D"),
            new KeyLayoutEntry(PhysicalKey.F, "F"),
            new KeyLayoutEntry(PhysicalKey.G, "G"),
            new KeyLayoutEntry(PhysicalKey.H, "H"),
            new KeyLayoutEntry(PhysicalKey.J, "J"),
            new KeyLayoutEntry(PhysicalKey.K, "K"),
            new KeyLayoutEntry(PhysicalKey.L, "L"),
            new KeyLayoutEntry(PhysicalKey.Semicolon, ";"),
            new KeyLayoutEntry(PhysicalKey.Quote, "'"),
            new KeyLayoutEntry(PhysicalKey.Backslash, "\\"),
            new KeyLayoutEntry(PhysicalKey.Enter, "") {
                Width = new SizeUnit(3.5),
                GapAfter = SizeUnit.ZERO,
                KeyRowPosition = KeyRowPosition.TWO_ROWS_LOWER_ROW
            });

        RowLayoutEntry row5 = new RowLayoutEntry(
            new KeyLayoutEntry(PhysicalKey.LeftShift, "⇧") {
                Width = new SizeUnit(6.5),
                LabelHAlignment = HorizontalAlignment.Left,
                LabelVAlignment = VerticalAlignment.Bottom
            },
            new KeyLayoutEntry(PhysicalKey.Tilde, "`"),
            new KeyLayoutEntry(PhysicalKey.Z, "Z"),
            new KeyLayoutEntry(PhysicalKey.X, "X"),
            new KeyLayoutEntry(PhysicalKey.C, "C"),
            new KeyLayoutEntry(PhysicalKey.V, "V"),
            new KeyLayoutEntry(PhysicalKey.B, "B"),
            new KeyLayoutEntry(PhysicalKey.N, "N"),
            new KeyLayoutEntry(PhysicalKey.M, "M"),
            new KeyLayoutEntry(PhysicalKey.Comma, ","),
            new KeyLayoutEntry(PhysicalKey.Period, "."),
            new KeyLayoutEntry(PhysicalKey.Slash, "/"),
            new KeyLayoutEntry(PhysicalKey.RightShift, "⇧") {
                Width = new SizeUnit(12.5),
                GapAfter = SizeUnit.ZERO,
                LabelHAlignment = HorizontalAlignment.Right,
                LabelVAlignment = VerticalAlignment.Bottom
            });

        RowLayoutEntry row6 = new RowLayoutEntry(
            new KeyLayoutEntry(PhysicalKey.Function, "Fn") {
                LabelHAlignment = HorizontalAlignment.Left,
                LabelVAlignment = VerticalAlignment.Bottom
            },
            new KeyLayoutEntry(PhysicalKey.LeftControl, "⌃") {
                LabelHAlignment = HorizontalAlignment.Right,
                LabelVAlignment = VerticalAlignment.Top
            },
            new KeyLayoutEntry(PhysicalKey.LeftOption, "⌥") {
                LabelHAlignment = HorizontalAlignment.Right,
                LabelVAlignment = VerticalAlignment.Top
            },
            new KeyLayoutEntry(PhysicalKey.LeftCommand, "⌘") {
                Width = new SizeUnit(6.5),
                LabelHAlignment = HorizontalAlignment.Right,
                LabelVAlignment = VerticalAlignment.Top
            },
            new KeyLayoutEntry(PhysicalKey.Space, "") { Width = new SizeUnit(29) },
            new KeyLayoutEntry(PhysicalKey.RightCommand, "⌘") {
                Width = new SizeUnit(6.5),
                LabelHAlignment = HorizontalAlignment.Left,
                LabelVAlignment = VerticalAlignment.Top
            },
            new KeyLayoutEntry(PhysicalKey.RightOption, "⌥") {
                LabelHAlignment = HorizontalAlignment.Left,
                LabelVAlignment = VerticalAlignment.Top
            },
            new KeyLayoutEntry(PhysicalKey.Left, "◀"),
            new KeyLayoutEntry(PhysicalKey.Up, "▲") {
                KeyRowPosition = KeyRowPosition.HALF_ROW_UPPER
            },
            new KeyLayoutEntry(PhysicalKey.Down, "▼") {
                KeyRowPosition = KeyRowPosition.HALF_ROW_LOWER
            },
            new KeyLayoutEntry(PhysicalKey.Right, "▶") { GapAfter = SizeUnit.ZERO });

        return [row1, row2, row3, row4, row5, row6];
    }
}

/* Contains size in an artificial size unit.
 *      - Standard key is a square with the side of 5 units
 *      - Standard gap between keys and between key rows is 1 unit */
public struct SizeUnit {
    public static readonly SizeUnit ZERO = new(0);
    public static readonly SizeUnit STANDARD_KEY = new(5);
    public static readonly SizeUnit STANDARD_GAP = new(1);

    public static SizeUnit operator +(SizeUnit sizeUnit1, SizeUnit sizeUnit2) {
        return new SizeUnit(sizeUnit1.Value + sizeUnit2.Value);
    }
    public static SizeUnit operator *(SizeUnit sizeUnit, double value) {
        return new SizeUnit(sizeUnit.Value * value);
    }
    public static SizeUnit operator /(SizeUnit sizeUnit, double value) {
        return new SizeUnit(sizeUnit.Value / value);
    }

    public SizeUnit(double value) {
        Value = value;
    }

    public double Value {
        get; private set;
    }
}

public enum KeyRowSpan {
    UPPER_HALF,
    LOWER_HALF,
    ONE,
    TWO
}

public enum KeyRowPosition {
    NORMAL,
    TWO_ROWS_UPPER_ROW,
    TWO_ROWS_LOWER_ROW,
    HALF_ROW_UPPER,
    HALF_ROW_LOWER
}

public record KeyLayoutEntry(PhysicalKey Kind, string Label) {
    public SizeUnit Width {
        get; init;
    } = SizeUnit.STANDARD_KEY;
    public SizeUnit GapAfter {
        get; init;
    } = SizeUnit.STANDARD_GAP;
    public KeyRowPosition KeyRowPosition {
        get; init;
    } = KeyRowPosition.NORMAL;
    public HorizontalAlignment LabelHAlignment {
        get; init;
    } = HorizontalAlignment.Center;
    public VerticalAlignment LabelVAlignment {
        get; init;
    } = VerticalAlignment.Center;
}

public record RowLayoutEntry {
    private List<KeyLayoutEntry> rowKeys = new List<KeyLayoutEntry>();

    public RowLayoutEntry(params KeyLayoutEntry[] keyLayoutEntries) {
        rowKeys.AddRange(keyLayoutEntries);
    }

    public IReadOnlyList<KeyLayoutEntry> RowKeys {
        get {
            return rowKeys;
        }
    }
}
