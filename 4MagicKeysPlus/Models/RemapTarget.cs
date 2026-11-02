namespace _4MagicKeysPlus.Models;

public readonly record struct RemapTarget(PhysicalKey Key, int ConsumerUsage = 0) {
    public static implicit operator RemapTarget(PhysicalKey key) {
        return new RemapTarget(key);
    }

    public static RemapTarget Media(int usage) {
        return new RemapTarget(PhysicalKey.None, usage);
    }

    public bool IsMedia => ConsumerUsage != 0;

    public string DisplayName => IsMedia ? ConsumerUsageNames.Get(ConsumerUsage) : PhysicalKeyNames.GetDisplayName(Key);
}
