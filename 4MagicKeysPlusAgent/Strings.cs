using System.Resources;

namespace _4MagicKeysPlusAgent;

internal static class Strings {
    private static readonly ResourceManager ResourceManager =
        new("_4MagicKeysPlusAgent.Strings", typeof(Strings).Assembly);

    public static string LowBatteryMessage {
        get {
            return ResourceManager.GetString(nameof(LowBatteryMessage)) ?? nameof(LowBatteryMessage);
        }
    }
}
