namespace _4MagicKeysPlus.Models;

public static class KeyRemapRules {
    private static readonly IReadOnlyList<PhysicalKey> Modifiers = [
        PhysicalKey.LeftShift, PhysicalKey.LeftControl, PhysicalKey.LeftOption, PhysicalKey.LeftCommand,
        PhysicalKey.RightShift, PhysicalKey.RightCommand, PhysicalKey.RightOption, PhysicalKey.RightControl
    ];

    private static readonly IReadOnlyList<PhysicalKey> VirtualKeys = [PhysicalKey.Eject, PhysicalKey.Fn];

    private static readonly IReadOnlyList<PhysicalKey> Typing = [
        .. KeysInRange(PhysicalKey.A, PhysicalKey.D0),
        PhysicalKey.Minus, PhysicalKey.EqualsKey, PhysicalKey.OpeningBracket, PhysicalKey.ClosingBracket,
        PhysicalKey.Backslash, PhysicalKey.Semicolon, PhysicalKey.Quote, PhysicalKey.Tilde,
        PhysicalKey.Comma, PhysicalKey.Period, PhysicalKey.Slash, PhysicalKey.Section
    ];

    private static readonly IReadOnlyList<PhysicalKey> Navigation = [
        PhysicalKey.Home, PhysicalKey.PageUp, PhysicalKey.Delete, PhysicalKey.End, PhysicalKey.PageDown,
        PhysicalKey.Right, PhysicalKey.Left, PhysicalKey.Down, PhysicalKey.Up
    ];

    private static readonly IReadOnlyList<PhysicalKey> ExtendedSources = KeysInRange(PhysicalKey.F13, PhysicalKey.F19);

    private static readonly IReadOnlyList<PhysicalKey> ExtendedTargets = [
        .. KeysInRange(PhysicalKey.F13, PhysicalKey.F24),
        PhysicalKey.PrintScreen, PhysicalKey.ScrollLock, PhysicalKey.PauseBreak, PhysicalKey.Insert
    ];

    private static readonly IReadOnlyList<PhysicalKey> ModifierTargets = [.. Modifiers, .. VirtualKeys];

    private static readonly IReadOnlyList<PhysicalKey> VirtualKeyTargets = [.. ModifierTargets, .. ExtendedTargets];

    private static readonly IReadOnlyList<PhysicalKey> NavigationTargets = [.. Navigation, PhysicalKey.Insert];

    public static bool CanRemap(PhysicalKey source) {
        return GetCandidates(source).Count > 0;
    }

    public static IReadOnlyList<PhysicalKey> GetAllowedTargets(PhysicalKey source) {
        return GetCandidates(source).Where(target => target != source).ToList();
    }

    // The key the source is remapped to on the base layer (no Fn), or null when it is not remapped
    // that way. A target that is not a single key (key + modifiers, several modifiers, a media
    // usage) is not representable here.
    public static PhysicalKey? GetCurrentTarget(DriverKeyRemapConfig config, PhysicalKey source) {
        foreach (KeyRule rule in config.Rules) {
            if (rule.FnLayer || rule.Source.Count != 1 || !rule.Source.Contains(source)) {
                continue;
            }
            if (rule.ConsumerUsage is not null) {
                return null;
            }
            if (rule.TargetKey != PhysicalKey.None && rule.TargetModifiers.Count == 0) {
                return rule.TargetKey;
            }
            if (rule.TargetKey == PhysicalKey.None && rule.TargetCode == 0 && rule.TargetModifiers.Count == 1) {
                return rule.TargetModifiers.Single();
            }
            return null;
        }
        return null;
    }

    private static IReadOnlyList<PhysicalKey> GetCandidates(PhysicalKey source) {
        if (Modifiers.Contains(source)) {
            return ModifierTargets;
        }
        if (VirtualKeys.Contains(source)) {
            return VirtualKeyTargets;
        }
        if (Typing.Contains(source)) {
            return Typing;
        }
        if (Navigation.Contains(source)) {
            return NavigationTargets;
        }
        if (ExtendedSources.Contains(source)) {
            return ExtendedTargets;
        }
        if (source == PhysicalKey.NumPadEquals) {
            return [PhysicalKey.EqualsKey];
        }
        return [];
    }

    private static List<PhysicalKey> KeysInRange(PhysicalKey first, PhysicalKey last) {
        List<PhysicalKey> keys = [];
        for (int code = (byte)first; code <= (byte)last; code++) {
            if (Enum.IsDefined(typeof(PhysicalKey), (byte)code)) {
                keys.Add((PhysicalKey)code);
            }
        }
        return keys;
    }
}
