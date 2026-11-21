using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlus.Models;

public static class KeyRemapRules {
    private static readonly IReadOnlyList<PhysicalKey> Modifiers = [
        PhysicalKey.LeftShift, PhysicalKey.LeftControl, PhysicalKey.LeftOption, PhysicalKey.LeftCommand,
        PhysicalKey.RightShift, PhysicalKey.RightCommand, PhysicalKey.RightOption, PhysicalKey.RightControl
    ];

    private static readonly IReadOnlyList<PhysicalKey> VirtualTargets = [PhysicalKey.Eject, PhysicalKey.Fn];

    private static readonly IReadOnlyList<PhysicalKey> VirtualSources = [.. VirtualTargets, PhysicalKey.Lock, PhysicalKey.TouchId];

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

    private static readonly IReadOnlyList<PhysicalKey> ExtendedSources = [.. KeysInRange(PhysicalKey.F13, PhysicalKey.F19), PhysicalKey.Menu];

    private static readonly IReadOnlyList<PhysicalKey> FunctionKeyTargets = KeysInRange(PhysicalKey.F13, PhysicalKey.F24);

    private static readonly IReadOnlyList<PhysicalKey> ExtendedTargets = [
        PhysicalKey.PrintScreen, PhysicalKey.ScrollLock, PhysicalKey.PauseBreak, PhysicalKey.Insert, PhysicalKey.Application
    ];

    private static readonly IReadOnlyList<PhysicalKey> ModifierTargets = [.. Modifiers, .. VirtualTargets];

    private static readonly IReadOnlyList<PhysicalKey> NavigationTargets = [.. Navigation, PhysicalKey.Insert];

    // F13-F24 are only ever extra targets: every list that offers them offers other keys too.
    public static bool CanRemap(PhysicalKey source, bool fnLayer = false) {
        return GetCandidates(source, fnLayer, FunctionKeyTargets).Count > 0;
    }

    // The keyboards without F13-F19 declare key usages only up to Application (0x65): Windows drops a higher key in their reports.
    public static IReadOnlyList<RemapTarget> GetAllowedTargets(PhysicalKey source, KeyboardModel model, bool fnLayer = false) {
        IReadOnlyList<PhysicalKey> functionKeys = model.Product switch {
            KeyboardProduct.A1843 or KeyboardProduct.A2520 or KeyboardProduct.A3119 => FunctionKeyTargets,
            _ => [],
        };
        return GetCandidates(source, fnLayer, functionKeys).Where(target => target != source).ToList();
    }

    // Null also when the target is not a single key (key + modifiers, several modifiers, a media usage).
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

    // Only a key source can send a media usage: the driver has no rule shape for a modifier or
    // Fn/Eject/Lock source with a consumer target.
    private static IReadOnlyList<RemapTarget> GetCandidates(PhysicalKey source, bool fnLayer, IReadOnlyList<PhysicalKey> functionKeys) {
        // What a key can be remapped to while Fn is held. Any key but the modifiers and Fn/Eject/Lock
        // can be a source; the targets are the keys Apple keyboards lack.
        if (fnLayer) {
            bool isKey = source != PhysicalKey.None && !Modifiers.Contains(source) && !VirtualSources.Contains(source);
            return isKey ? KeysAndMedia(NavigationTargets.Union(functionKeys).Union(ExtendedTargets)) : [];
        }
        if (Modifiers.Contains(source)) {
            return Keys(ModifierTargets);
        }
        if (VirtualSources.Contains(source)) {
            return Keys([.. ModifierTargets, .. functionKeys, .. ExtendedTargets]);
        }
        if (Typing.Contains(source)) {
            return KeysAndMedia(Typing);
        }
        if (Navigation.Contains(source)) {
            return KeysAndMedia(NavigationTargets);
        }
        if (ExtendedSources.Contains(source)) {
            return KeysAndMedia([.. functionKeys, .. ExtendedTargets]);
        }
        if (source == PhysicalKey.NumPadEquals) {
            return [PhysicalKey.EqualsKey];
        }
        return [];
    }

    private static List<RemapTarget> Keys(IEnumerable<PhysicalKey> keys) {
        return [.. keys.Select(key => (RemapTarget)key)];
    }

    private static List<RemapTarget> KeysAndMedia(IEnumerable<PhysicalKey> keys) {
        return [.. Keys(keys), .. ConsumerUsageNames.Usages.Select(RemapTarget.Media)];
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
