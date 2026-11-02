using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Properties;

namespace _4MagicKeysPlus.Models;

public record KeyboardModel {
    public static readonly KeyboardModel A1644 = new(KeyboardProduct.A1644, Strings.KeysModel_A1644);
    public static readonly KeyboardModel A1843 = new(KeyboardProduct.A1843, Strings.KeysModel_A1843);
    public static readonly KeyboardModel A2449 = new(KeyboardProduct.A2449, Strings.KeysModel_A2449);
    public static readonly KeyboardModel A2450 = new(KeyboardProduct.A2450, Strings.KeysModel_A2450);
    public static readonly KeyboardModel A2520 = new(KeyboardProduct.A2520, Strings.KeysModel_A2520);
    public static readonly KeyboardModel A3118 = new(KeyboardProduct.A3118, Strings.KeysModel_A3118);
    public static readonly KeyboardModel A3119 = new(KeyboardProduct.A3119, Strings.KeysModel_A3119);
    public static readonly KeyboardModel A3203 = new(KeyboardProduct.A3203, Strings.KeysModel_A3203);

    private KeyboardModel(KeyboardProduct product, string deviceDescription) {
        Product = product;
        DeviceDescription = deviceDescription;
    }

    public KeyboardProduct Product {
        get;
    }

    public string DeviceDescription {
        get;
    }

    public static KeyboardModel From(KeyboardProduct product) {
        return product switch {
            KeyboardProduct.A1644 => A1644,
            KeyboardProduct.A1843 => A1843,
            KeyboardProduct.A2449 => A2449,
            KeyboardProduct.A2450 => A2450,
            KeyboardProduct.A2520 => A2520,
            KeyboardProduct.A3118 => A3118,
            KeyboardProduct.A3119 => A3119,
            KeyboardProduct.A3203 => A3203,
            _ => throw new ArgumentOutOfRangeException(nameof(product), product, null),
        };
    }
}
