using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlus.Services;

// The reverse of DriverConfigParser: the Source/Target values the driver reads for a rule.
// Modifiers go to the mask in bits 16-23 (HID order), everything else is a HID code - never the
// raw PhysicalKey value of a modifier, which is fake.
internal static class DriverRuleEncoder {
    private const int ModsShift = 16;
    private const uint SourceFnFlag = 0x01000000;
    private const uint TargetConsumerFlag = 0x01000000;

    public static (uint Source, uint Target) Encode(PhysicalKey source, RemapTarget target, bool fnLayer) {
        uint encodedSource = EncodeKey(source);
        if (fnLayer) {
            encodedSource |= SourceFnFlag;
        }
        uint encodedTarget = target.IsMedia ? (uint)target.ConsumerUsage | TargetConsumerFlag : EncodeKey(target.Key);
        return (encodedSource, encodedTarget);
    }

    // Null when the driver would drop the combination as invalid. FnLockAware only means something
    // for a rule of the Fn layer.
    public static KeyRule? CreateRule(PhysicalKey source, RemapTarget target, bool fnLayer, bool fnLockAware) {
        (uint encodedSource, uint encodedTarget) = Encode(source, target, fnLayer);
        return DriverConfigParser.ParseRule(encodedSource, encodedTarget, fnLayer && fnLockAware);
    }

    private static uint EncodeKey(PhysicalKey key) {
        if (PhysicalKeyHidMap.IsModifier(key)) {
            return (uint)PhysicalKeyHidMap.EncodeModifiers(new HashSet<PhysicalKey> { key }) << ModsShift;
        }
        return PhysicalKeyHidMap.GetKeyByte(key);
    }
}
