namespace _4MagicKeysPlus.Models;

// Consumer Control usages (usage page 0x0C) the driver can send, by usage code.
public static class ConsumerUsageNames {
    private static readonly (int Usage, string Name)[] Known = [
        (0xB6, "Previous Track"),
        (0xCD, "Play/Pause"),
        (0xB5, "Next Track"),
        (0xE2, "Mute"),
        (0xEA, "Volume Down"),
        (0xE9, "Volume Up"),
        (0x70, "Brightness Down"),
        (0x6F, "Brightness Up"),
    ];

    private static readonly IReadOnlyDictionary<int, string> Names = Known.ToDictionary(entry => entry.Usage, entry => entry.Name);

    // The usages the editor offers as targets, in the order they are shown.
    public static IReadOnlyList<int> Usages { get; } = [.. Known.Select(entry => entry.Usage)];

    public static string Get(int usage) {
        return Names.TryGetValue(usage, out string? name) ? name : $"Consumer 0x{usage:X4}";
    }
}
