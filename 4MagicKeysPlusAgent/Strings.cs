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

    public static string HotkeyErrorsTitle {
        get {
            return ResourceManager.GetString(nameof(HotkeyErrorsTitle)) ?? nameof(HotkeyErrorsTitle);
        }
    }

    public static string NoticeDismissHint {
        get {
            return ResourceManager.GetString(nameof(NoticeDismissHint)) ?? nameof(NoticeDismissHint);
        }
    }
}
