using Microsoft.Win32;

namespace _4MagicKeysPlus.Core;

// Values of a profile key: Model (the USB product ID), Index (none while it is 1), Identities (MULTI_SZ of bt:/usb: texts).
// The settings beside them (FnLock, the Rules subkey) are not touched here.
public interface IKeyboardProfileStore {
    IReadOnlyList<KeyboardProfile> Load();

    // Rewrites the values of the profile; an identity that another profile owns is refused.
    void Save(KeyboardProfile profile);
}

public sealed class RegistryKeyboardProfileStore : IKeyboardProfileStore {
    // The driver reads the profiles from here.
    public const string KeyboardsKeyPath = @"SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Keyboards";

    private readonly RegistryKey root;
    private readonly string keyboardsPath;

    public RegistryKeyboardProfileStore() : this(Registry.LocalMachine, KeyboardsKeyPath) {
    }

    public RegistryKeyboardProfileStore(RegistryKey root, string keyboardsPath) {
        this.root = root;
        this.keyboardsPath = keyboardsPath;
    }

    public static string ProfileKeyName(Guid profileId) {
        return profileId.ToString("B").ToUpperInvariant();
    }

    public IReadOnlyList<KeyboardProfile> Load() {
        List<KeyboardProfile> profiles = [];
        using RegistryKey? keyboardsKey = root.OpenSubKey(keyboardsPath);
        if (keyboardsKey is null) {
            return profiles;
        }

        foreach (string name in keyboardsKey.GetSubKeyNames()) {
            using RegistryKey? key = keyboardsKey.OpenSubKey(name);
            if (key is null || !Guid.TryParse(name, out Guid id) || !TryReadProduct(key, out KeyboardProduct product)) {
                continue;
            }

            profiles.Add(new KeyboardProfile(id, product, ReadIdentities(key), key.GetValue("Index") is int index and > 1 ? index : 1));
        }

        return [.. profiles.OrderBy(profile => profile.Product).ThenBy(profile => profile.Index).ThenBy(profile => profile.Id)];
    }

    // The driver takes the identities of every subkey, also of one that Load does not show, so a clash with any of them is refused.
    public void Save(KeyboardProfile profile) {
        string ownName = ProfileKeyName(profile.Id);
        using (RegistryKey? keyboardsKey = root.OpenSubKey(keyboardsPath)) {
            foreach (string name in keyboardsKey?.GetSubKeyNames() ?? []) {
                if (string.Equals(name, ownName, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                using RegistryKey? other = keyboardsKey!.OpenSubKey(name);
                KeyboardIdentity? clash = other is null ? null : ReadIdentities(other).FirstOrDefault(profile.Owns);
                if (clash is not null) {
                    throw new InvalidOperationException($"{clash} already belongs to the profile {name}.");
                }
            }
        }

        using RegistryKey key = root.CreateSubKey($@"{keyboardsPath}\{ownName}", writable: true);
        if (profile.Index > 1) {
            key.SetValue("Index", profile.Index, RegistryValueKind.DWord);
        } else {
            key.DeleteValue("Index", throwOnMissingValue: false);
        }
        key.SetValue("Model", KnownKeyboards.GetProductId(profile.Product), RegistryValueKind.DWord);
        key.SetValue("Identities", profile.Identities.Select(identity => identity.ToString()).ToArray(), RegistryValueKind.MultiString);
    }

    private static List<KeyboardIdentity> ReadIdentities(RegistryKey key) {
        string[] identityTexts = key.GetValue("Identities") as string[] ?? [];
        return [.. identityTexts.Select(KeyboardIdentity.Parse).OfType<KeyboardIdentity>()];
    }

    private static bool TryReadProduct(RegistryKey key, out KeyboardProduct product) {
        if (key.GetValue("Model") is int model && model is >= 0 and <= ushort.MaxValue) {
            return KnownKeyboards.TryGetProduct((ushort)model, out product);
        }

        product = default;
        return false;
    }
}
