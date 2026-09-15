using _4MagicKeysPlus.Properties;

namespace _4MagicKeysPlus.Models;

public record KeyboardModel {
    public static readonly KeyboardModel A1644 = new(Strings.KeysModel_MagicKeyboard); // 1st generation without numpad
    public static readonly KeyboardModel A1843 = new(Strings.KeysModel_MagicKeyboardWithNumpad); // 1st generation with numpad

    private KeyboardModel(String deviceDescription) {
        DeviceDescription = deviceDescription;
    }

    public string DeviceDescription {
        get;
    }
}
