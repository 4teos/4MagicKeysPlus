namespace _4MagicKeysPlus.Models;

// Consumer Control usages (usage page 0x0C) the driver can send, by usage code.
public static class ConsumerUsageNames {
    private static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string> {
        [0x6F] = "Brightness Up",
        [0x70] = "Brightness Down",
        [0xB5] = "Next Track",
        [0xB6] = "Previous Track",
        [0xCD] = "Play/Pause",
        [0xE2] = "Mute",
        [0xE9] = "Volume Up",
        [0xEA] = "Volume Down",
    };

    public static string Get(int usage) {
        return Names.TryGetValue(usage, out string? name) ? name : $"Consumer 0x{usage:X4}";
    }
}
