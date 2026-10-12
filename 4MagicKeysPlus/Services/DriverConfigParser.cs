using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlus.Services;

// Decodes one Source/Target pair of Parameters\Rules the way the driver does (ClassifyRule in
// KeyProcessor.cpp): whatever the driver drops as invalid must not show up here either, or the
// Keys screen would highlight remaps that do nothing. Keep the two in step.
internal static class DriverConfigParser {
    private const uint CodeMask = 0x0000FFFF;
    private const int ModsShift = 16;
    private const uint ModsMask = 0x00FF0000;
    private const uint SourceFnFlag = 0x01000000;
    private const uint TargetConsumerFlag = 0x01000000;
    private const uint FlagsMask = 0xFF000000;
    private const uint VirtualEject = 0xF0;
    private const uint VirtualFn = 0xF1;
    private const uint MaxConsumerUsage = 0x03FF;

    public static KeyRule? ParseRule(uint source, uint target, bool fnLockAware) {
        uint sourceFlags = source & FlagsMask;
        uint targetFlags = target & FlagsMask;
        if ((sourceFlags & ~SourceFnFlag) != 0 || (targetFlags & ~TargetConsumerFlag) != 0) {
            return null;
        }

        bool sourceFn = sourceFlags != 0;
        bool targetConsumer = targetFlags != 0;
        uint sourceCode = source & CodeMask;
        uint targetCode = target & CodeMask;
        byte sourceMods = (byte)((source & ModsMask) >> ModsShift);
        byte targetMods = (byte)((target & ModsMask) >> ModsShift);

        if (sourceCode > 0xFF) {
            return null;
        }

        bool sourceIsMod = sourceCode == 0 && sourceMods != 0;
        bool sourceIsVirtual = IsVirtual(sourceCode);
        bool sourceIsKey = sourceCode != 0 && !sourceIsVirtual && sourceMods == 0;

        if (sourceIsMod || sourceIsVirtual) {
            if (sourceFn || targetConsumer || (sourceIsVirtual && sourceMods != 0)) {
                return null;
            }

            bool targetIsValid = targetCode == 0
                ? targetMods != 0
                : IsVirtual(targetCode) ? targetMods == 0 : sourceIsVirtual && targetCode <= 0xFF;
            if (!targetIsValid) {
                return null;
            }
        } else if (sourceIsKey) {
            bool targetIsValid = targetConsumer
                ? targetCode != 0 && targetCode <= MaxConsumerUsage
                : targetCode <= 0xFF && !IsVirtual(targetCode) && (targetCode != 0 || targetMods != 0);
            if (!targetIsValid) {
                return null;
            }
        } else {
            return null;
        }

        IReadOnlySet<PhysicalKey> sourceKeys;
        if (sourceIsMod) {
            sourceKeys = PhysicalKeyHidMap.DecodeModifiers(sourceMods);
        } else {
            PhysicalKey sourceKey = PhysicalKeyHidMap.GetPhysicalKey((byte)sourceCode);
            if (sourceKey == PhysicalKey.None) {
                return null;
            }
            sourceKeys = new HashSet<PhysicalKey> { sourceKey };
        }

        if (targetConsumer) {
            return new KeyRule(sourceKeys, sourceFn, PhysicalKey.None, 0, new HashSet<PhysicalKey>(), (int)targetCode, fnLockAware) { RawSource = source, RawTarget = target };
        }

        PhysicalKey targetKey = targetCode == 0 ? PhysicalKey.None : PhysicalKeyHidMap.GetPhysicalKey((byte)targetCode);
        return new KeyRule(sourceKeys, sourceFn, targetKey, (int)targetCode, PhysicalKeyHidMap.DecodeModifiers(targetMods), null, fnLockAware) { RawSource = source, RawTarget = target };
    }

    private static bool IsVirtual(uint code) {
        return code is VirtualEject or VirtualFn;
    }
}
