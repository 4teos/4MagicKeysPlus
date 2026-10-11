namespace _4MagicKeysPlus.Models;

// What a rule can send: a key (or modifier) or a media (consumer control) usage.
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
