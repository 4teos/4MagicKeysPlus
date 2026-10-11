using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

// The same 25 rules as kUserRules in 4MagicKeysPlusDriverTests.cpp and the author's registry,
// so what the Keys screen shows is checked against the rule set the driver tests cover.
internal static class UserRules {
    public static readonly (uint Source, uint Target, bool FnLockAware)[] Raw = [
        (0x000000F0, 0x000000F1, false), // Eject -> Fn
        (0x000000F1, 0x00000049, false), // Fn -> Insert
        (0x00000068, 0x00000046, false), // F13 -> PrintScreen
        (0x00000069, 0x00000047, false), // F14 -> ScrollLock
        (0x0000006A, 0x00000048, false), // F15 -> Pause
        (0x00040000, 0x00080000, false), // LeftOption -> LeftCommand
        (0x00080000, 0x00040000, false), // LeftCommand -> LeftOption
        (0x00400000, 0x00800000, false), // RightOption -> RightCommand
        (0x00800000, 0x00400000, false), // RightCommand -> RightOption
        (0x01000050, 0x0000004A, false), // Fn+Left -> Home
        (0x0100004F, 0x0000004D, false), // Fn+Right -> End
        (0x01000052, 0x0000004B, false), // Fn+Up -> PageUp
        (0x01000051, 0x0000004E, false), // Fn+Down -> PageDown
        (0x01000028, 0x00000049, false), // Fn+Enter -> Insert
        (0x01000013, 0x00000046, false), // Fn+P -> PrintScreen
        (0x01000005, 0x00000048, false), // Fn+B -> Pause
        (0x01000016, 0x00000047, false), // Fn+S -> ScrollLock
        (0x0100003A, 0x01000070, true), // Fn+F1 -> Brightness Down
        (0x0100003B, 0x0100006F, true), // Fn+F2 -> Brightness Up
        (0x01000040, 0x010000B6, true), // Fn+F7 -> Previous Track
        (0x01000041, 0x010000CD, true), // Fn+F8 -> Play/Pause
        (0x01000042, 0x010000B5, true), // Fn+F9 -> Next Track
        (0x01000043, 0x010000E2, true), // Fn+F10 -> Mute
        (0x01000044, 0x010000EA, true), // Fn+F11 -> Volume Down
        (0x01000045, 0x010000E9, true), // Fn+F12 -> Volume Up
    ];

    public static DriverKeyRemapConfig Config(bool fnLockEnabled) {
        List<KeyRule> rules = [];
        foreach ((uint source, uint target, bool fnLockAware) in Raw) {
            KeyRule? rule = DriverConfigParser.ParseRule(source, target, fnLockAware);
            if (rule is not null) {
                rules.Add(rule with { RegistryName = source.ToString("X8") }); // how the registry names them
            }
        }
        return new DriverKeyRemapConfig(rules, fnLockEnabled);
    }
}
