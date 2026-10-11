namespace _4MagicKeysPlus.Models;

// Bridges the driver's raw HID bytes to this app's PhysicalKey identity.
public static class PhysicalKeyHidMap {
    private static readonly IReadOnlyDictionary<byte, PhysicalKey> ModifiersByByte =
        new Dictionary<byte, PhysicalKey> {
            [0x01] = PhysicalKey.LeftControl,
            [0x02] = PhysicalKey.LeftShift,
            [0x04] = PhysicalKey.LeftOption,
            [0x08] = PhysicalKey.LeftCommand,
            [0x10] = PhysicalKey.RightControl,
            [0x20] = PhysicalKey.RightShift,
            [0x40] = PhysicalKey.RightOption,
            [0x80] = PhysicalKey.RightCommand
        };
    private static readonly IReadOnlyDictionary<PhysicalKey, byte> ModifiersByPhysicalKey =
        new Dictionary<PhysicalKey, byte> {
            [PhysicalKey.LeftControl] = 0x01,
            [PhysicalKey.LeftShift] = 0x02,
            [PhysicalKey.LeftOption] = 0x04,
            [PhysicalKey.LeftCommand] = 0x08,
            [PhysicalKey.RightControl] = 0x10,
            [PhysicalKey.RightShift] = 0x20,
            [PhysicalKey.RightOption] = 0x40,
            [PhysicalKey.RightCommand] = 0x80
        };

    // HID usages 0xE0-0xE7 are the modifiers, whose PhysicalKey values are fake and ordered
    // differently, so those usages deliberately map to None here.
    public static PhysicalKey GetPhysicalKey(byte code) {
        if (code is >= 0xE0 and <= 0xE7) {
            return PhysicalKey.None;
        }
        if (Enum.IsDefined(typeof(PhysicalKey), code)) {
            return (PhysicalKey)code;
        }
        return PhysicalKey.None;
    }

    public static bool IsModifier(PhysicalKey physicalKey) {
        return ModifiersByPhysicalKey.ContainsKey(physicalKey);
    }

    public static byte GetKeyByte(PhysicalKey physicalKey) {
        return (byte)physicalKey;
    }

    public static IReadOnlySet<PhysicalKey> DecodeModifiers(byte mask) {
        HashSet<PhysicalKey> keys = [];
        for (byte bit = 0; bit < 8; bit++) {
            byte singleModifierByte = (byte)(1 << bit);
            if ((mask & singleModifierByte) != 0 && ModifiersByByte.TryGetValue(singleModifierByte, out PhysicalKey key)) {
                keys.Add(key);
            }
        }
        return keys;
    }

    public static byte EncodeModifiers(IReadOnlySet<PhysicalKey> modifiers) {
        byte mask = 0;
        foreach (PhysicalKey modifier in modifiers) {
            byte modBit = ModifiersByPhysicalKey[modifier];
            mask |= modBit;
        }
        return mask;
    }
}
