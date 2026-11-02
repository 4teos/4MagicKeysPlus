using System.Windows;
using System.Windows.Input;
using _4MagicKeysPlus.Core;
using static _4MagicKeysPlus.Core.KeyboardProduct;
using static _4MagicKeysPlus.Models.KeyboardLayout;

namespace _4MagicKeysPlus.Models;

public static class KeyboardLayout {
    private static readonly KeyLayoutEntry EjectKey = new(PhysicalKey.Eject, "⏏");
    private static readonly KeyLayoutEntry TouchIdKey = new(PhysicalKey.TouchId, "");
    private static readonly KeyLayoutEntry LockKey = new(PhysicalKey.Lock, "🔒");

    public static IReadOnlyList<RowLayoutEntry> GetKeys(KeyboardModel model, LayoutStandard standard) {
        return [BuildRow1(model), BuildRow2(model, standard), BuildRow3(model, standard), BuildRow4(model, standard), BuildRow5(model, standard), BuildRow6(model)];
    }

    private static RowLayoutEntry BuildRow1(KeyboardModel model) {
        KeyLayoutEntry cornerKey;
        SizeUnit gap;
        KeyLayoutEntry[] tail;
        switch (model.Product) {
            case A1644:
                cornerKey = EjectKey;
                gap = SizeUnit.ZERO;
                tail = [];
                break;
            case A1843:
                cornerKey = EjectKey;
                gap = SizeUnit.STANDARD_GAP * 2;
                tail = BuildExtendedFunctionKeys();
                break;
            case A2449 or A3118:
                cornerKey = TouchIdKey;
                gap = SizeUnit.ZERO;
                tail = [];
                break;
            case A2520 or A3119:
                cornerKey = TouchIdKey;
                gap = SizeUnit.STANDARD_GAP * 2;
                tail = BuildExtendedFunctionKeys();
                break;
            case A2450 or A3203:
                cornerKey = LockKey;
                gap = SizeUnit.ZERO;
                tail = [];
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(model), model, null);
        }

        return new RowLayoutEntry([
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
            cornerKey with { GapAfter = gap },
            .. tail]);
    }

    private static KeyLayoutEntry[] BuildExtendedFunctionKeys() {
        return [
            new KeyLayoutEntry(PhysicalKey.F13, "F13"),
            new KeyLayoutEntry(PhysicalKey.F14, "F14"),
            new KeyLayoutEntry(PhysicalKey.F15, "F15") { GapAfter = SizeUnit.STANDARD_GAP * 2 },
            new KeyLayoutEntry(PhysicalKey.F16, "F16"),
            new KeyLayoutEntry(PhysicalKey.F17, "F17"),
            new KeyLayoutEntry(PhysicalKey.F18, "F18"),
            new KeyLayoutEntry(PhysicalKey.F19, "F19") { GapAfter = SizeUnit.ZERO }
        ];
    }

    private static RowLayoutEntry BuildRow2(KeyboardModel model, LayoutStandard standard) {
        KeyLayoutEntry[] navigationAndNumpad = [
            new KeyLayoutEntry(PhysicalKey.Home, "⤒"),
            new KeyLayoutEntry(PhysicalKey.PageUp, "↑") { GapAfter = SizeUnit.STANDARD_GAP * 2 },
            new KeyLayoutEntry(PhysicalKey.NumPadClear, "⌧"),
            new KeyLayoutEntry(PhysicalKey.NumPadEquals, "="),
            new KeyLayoutEntry(PhysicalKey.NumPadDivide, "/"),
            new KeyLayoutEntry(PhysicalKey.NumPadMultiply, "*") { GapAfter = SizeUnit.ZERO }
        ];
        SizeUnit gap;
        KeyLayoutEntry[] tail;
        switch (model.Product) {
            case A1843 or A2520:
                gap = SizeUnit.STANDARD_GAP * 2;
                tail = [new KeyLayoutEntry(PhysicalKey.Fn, "Fn"), .. navigationAndNumpad];
                break;
            case A3119:
                gap = SizeUnit.STANDARD_GAP * 2;
                tail = [new KeyLayoutEntry(PhysicalKey.Menu, "☰"), .. navigationAndNumpad];
                break;
            default:
                gap = SizeUnit.ZERO;
                tail = [];
                break;
        }

        return new RowLayoutEntry([
            standard == LayoutStandard.Ansi ? new KeyLayoutEntry(PhysicalKey.Tilde, "`") : new KeyLayoutEntry(PhysicalKey.Section, "§±"),
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
                GapAfter = gap,
                LabelHAlignment = HorizontalAlignment.Right,
                LabelVAlignment = VerticalAlignment.Bottom
            },
            .. tail]);
    }

    private static RowLayoutEntry BuildRow3(KeyboardModel model, LayoutStandard standard) {
        SizeUnit gap;
        KeyLayoutEntry[] tail;
        switch (model.Product) {
            case A1843 or A2520 or A3119:
                gap = SizeUnit.STANDARD_GAP * 2;
                tail = [
                    new KeyLayoutEntry(PhysicalKey.Delete, "⌦"),
                    new KeyLayoutEntry(PhysicalKey.End, "⤓"),
                    new KeyLayoutEntry(PhysicalKey.PageDown, "↓") { GapAfter = SizeUnit.STANDARD_GAP * 2 },
                    new KeyLayoutEntry(PhysicalKey.NumPad7, "7"),
                    new KeyLayoutEntry(PhysicalKey.NumPad8, "8"),
                    new KeyLayoutEntry(PhysicalKey.NumPad9, "9"),
                    new KeyLayoutEntry(PhysicalKey.NumPadMinus, "-") { GapAfter = SizeUnit.ZERO }
                ];
                break;
            default:
                gap = SizeUnit.ZERO;
                tail = [];
                break;
        }

        KeyLayoutEntry lastKey = standard == LayoutStandard.Ansi
            ? new KeyLayoutEntry(PhysicalKey.Backslash, "\\") { GapAfter = gap }
            : new KeyLayoutEntry(PhysicalKey.Enter, "↩") {
                GapAfter = gap,
                KeyRowPosition = KeyRowPosition.TWO_ROWS_UPPER_ROW,
                LabelHAlignment = HorizontalAlignment.Right,
                LabelVAlignment = VerticalAlignment.Bottom
            };

        return new RowLayoutEntry([
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
            lastKey,
            .. tail]);
    }

    private static RowLayoutEntry BuildRow4(KeyboardModel model, LayoutStandard standard) {
        SizeUnit gap;
        KeyLayoutEntry[] tail;
        switch (model.Product) {
            case A1843 or A2520 or A3119:
                gap = new SizeUnit(21);
                tail = [
                    new KeyLayoutEntry(PhysicalKey.NumPad4, "4"),
                    new KeyLayoutEntry(PhysicalKey.NumPad5, "5"),
                    new KeyLayoutEntry(PhysicalKey.NumPad6, "6"),
                    new KeyLayoutEntry(PhysicalKey.NumPadPlus, "+") { GapAfter = SizeUnit.ZERO }
                ];
                break;
            default:
                gap = SizeUnit.ZERO;
                tail = [];
                break;
        }

        KeyLayoutEntry[] lastKeys = standard == LayoutStandard.Ansi
            ? [
                new KeyLayoutEntry(PhysicalKey.Enter, "↩") {
                    Width = new SizeUnit(9.5),
                    GapAfter = gap,
                    LabelHAlignment = HorizontalAlignment.Right,
                    LabelVAlignment = VerticalAlignment.Bottom
                }
            ]
            : [
                new KeyLayoutEntry(PhysicalKey.Backslash, "\\"),
                new KeyLayoutEntry(PhysicalKey.Enter, "") {
                    Width = new SizeUnit(3.5),
                    GapAfter = gap,
                    KeyRowPosition = KeyRowPosition.TWO_ROWS_LOWER_ROW
                }
            ];

        return new RowLayoutEntry([
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
            .. lastKeys,
            .. tail]);
    }

    private static RowLayoutEntry BuildRow5(KeyboardModel model, LayoutStandard standard) {
        SizeUnit gap;
        KeyLayoutEntry[] tail;
        switch (model.Product) {
            case A1843 or A2520 or A3119:
                gap = new SizeUnit(8);
                tail = [
                    new KeyLayoutEntry(PhysicalKey.Up, "▲") { GapAfter = new SizeUnit(8) },
                    new KeyLayoutEntry(PhysicalKey.NumPad1, "1"),
                    new KeyLayoutEntry(PhysicalKey.NumPad2, "2"),
                    new KeyLayoutEntry(PhysicalKey.NumPad3, "3"),
                    new KeyLayoutEntry(PhysicalKey.NumPadEnter, "") {
                        GapAfter = SizeUnit.ZERO,
                        KeyRowPosition = KeyRowPosition.TWO_ROWS_UPPER_ROW
                    }
                ];
                break;
            default:
                gap = SizeUnit.ZERO;
                tail = [];
                break;
        }

        KeyLayoutEntry[] leftKeys = standard == LayoutStandard.Ansi
            ? [LeftShiftKey(new SizeUnit(12.5))]
            : [LeftShiftKey(new SizeUnit(6.5)), new KeyLayoutEntry(PhysicalKey.Tilde, "`")];

        return new RowLayoutEntry([
            .. leftKeys,
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
                GapAfter = gap,
                LabelHAlignment = HorizontalAlignment.Right,
                LabelVAlignment = VerticalAlignment.Bottom
            },
            .. tail]);
    }

    private static KeyLayoutEntry LeftShiftKey(SizeUnit width) {
        return new KeyLayoutEntry(PhysicalKey.LeftShift, "⇧") {
            Width = width,
            LabelHAlignment = HorizontalAlignment.Left,
            LabelVAlignment = VerticalAlignment.Bottom
        };
    }

    private static RowLayoutEntry BuildRow6(KeyboardModel model) {
        KeyLayoutEntry[] arrowsAndNumpad = [
            new KeyLayoutEntry(PhysicalKey.Left, "◀"),
            new KeyLayoutEntry(PhysicalKey.Down, "▼"),
            new KeyLayoutEntry(PhysicalKey.Right, "▶") { GapAfter = SizeUnit.STANDARD_GAP * 2 },
            new KeyLayoutEntry(PhysicalKey.NumPad0, "0") { Width = new SizeUnit(11) },
            new KeyLayoutEntry(PhysicalKey.NumPadDecimal, "."),
            new KeyLayoutEntry(PhysicalKey.NumPadEnter, "⌅") {
                GapAfter = SizeUnit.ZERO,
                KeyRowPosition = KeyRowPosition.TWO_ROWS_LOWER_ROW,
                LabelVAlignment = VerticalAlignment.Bottom
            }
        ];

        switch (model.Product) {
            case A1843 or A2520:
                return new RowLayoutEntry([
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
                    .. arrowsAndNumpad]);
            case A3119:
                return new RowLayoutEntry([
                    new KeyLayoutEntry(PhysicalKey.Fn, "Fn") {
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
                    .. arrowsAndNumpad]);
            default:
                return new RowLayoutEntry([
                    new KeyLayoutEntry(PhysicalKey.Fn, "Fn") {
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
                    new KeyLayoutEntry(PhysicalKey.Right, "▶") { GapAfter = SizeUnit.ZERO }]);
        }
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

public enum LayoutStandard {
    Ansi,
    Iso
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
