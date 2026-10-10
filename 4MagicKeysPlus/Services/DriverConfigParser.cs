using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlus.Services;

internal static class DriverConfigParser {
    // Matches MAX_KEYMAP_SIZE/MAX_MODMAP_SIZE/MAX_SPECIAL_MODMAP_SIZE in KeyProcessorTypes.h
    private const int MaxKeyMapPairs = 64;
    private const int MaxModMapPairs = 8;
    private const int MaxSpecialModMapPairs = 2;

    public static List<KeyMapEntry> ParseKeyMap(byte[]? raw) {
        List<KeyMapEntry> entries = [];
        foreach ((byte source, byte target) in Pairs(raw, MaxKeyMapPairs)) {
            PhysicalKey sourceKey = PhysicalKeyHidMap.GetPhysicalKey(source);
            PhysicalKey targetKey = PhysicalKeyHidMap.GetPhysicalKey(target);
            if (sourceKey == PhysicalKey.None || targetKey == PhysicalKey.None) {
                continue;
            }
            entries.Add(new KeyMapEntry(sourceKey, targetKey));
        }
        return entries;
    }

    public static List<ModMapEntry> ParseModMap(byte[]? raw) {
        List<ModMapEntry> entries = [];
        foreach ((byte sourceMask, byte targetMask) in Pairs(raw, MaxModMapPairs)) {
            IReadOnlySet<PhysicalKey> sourceModifiers = PhysicalKeyHidMap.DecodeModifiers(sourceMask);
            IReadOnlySet<PhysicalKey> targetModifiers = PhysicalKeyHidMap.DecodeModifiers(targetMask);
            entries.Add(new ModMapEntry(sourceModifiers, targetModifiers));
        }
        return entries;
    }

    public static List<SpecialModMapEntry> ParseSpecialModMap(byte[]? raw) {
        List<SpecialModMapEntry> entries = [];
        foreach ((byte source, byte target) in Pairs(raw, MaxSpecialModMapPairs)) {
            PhysicalKey sourceKey = PhysicalKeyHidMap.GetPhysicalKey(source);
            if (IsVirtual(sourceKey)) {
                IReadOnlySet<PhysicalKey> targetModifiers = PhysicalKeyHidMap.DecodeModifiers(target);
                entries.Add(new SpecialModMapEntry(sourceKey, targetModifiers, SpecialModMapDirection.KeyToModifiers));
                continue;
            }

            PhysicalKey targetKey = PhysicalKeyHidMap.GetPhysicalKey(target);
            if (IsVirtual(targetKey)) {
                IReadOnlySet<PhysicalKey> sourceModifiers = PhysicalKeyHidMap.DecodeModifiers(source);
                entries.Add(new SpecialModMapEntry(targetKey, sourceModifiers, SpecialModMapDirection.ModifiersToKey));
            }
        }
        return entries;
    }

    private static bool IsVirtual(PhysicalKey key) {
        return key is PhysicalKey.Eject or PhysicalKey.Fn;
    }

    private static IEnumerable<(byte, byte)> Pairs(byte[]? raw, int maxPairs) {
        if (raw is null) {
            yield break;
        }

        int clampedLength = Math.Min(raw.Length, maxPairs * 2);
        int pairCount = clampedLength / 2;
        for (int i = 0; i < pairCount; i++) {
            yield return (raw[i * 2], raw[i * 2 + 1]);
        }
    }
}
