using Microsoft.Win32;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlus.Services;

// Where the profiles of the keyboards are kept: a subkey of Parameters\Keyboards for each, named by its GUID. The
// values of the profile itself are Name (SZ), Model (DWORD, the USB product ID) and Identities (MULTI_SZ of the
// bt:/usb: texts); the settings of the keyboard sit beside them (FnLock and the Rules subkey) and are not touched here.
internal interface IKeyboardProfileStore {
    IReadOnlyList<KeyboardProfile> Load();

    // Creates the profile or rewrites its own values. An identity that another profile owns is refused.
    void Save(KeyboardProfile profile);
}

internal sealed class RegistryKeyboardProfileStore : IKeyboardProfileStore {
    private readonly RegistryKey root;
    private readonly string keyboardsPath;

    public RegistryKeyboardProfileStore() : this(Registry.LocalMachine, DriverStatus.KeyboardsKeyPath) {
    }

    // The root and path are parameters so that tests can use a scratch key of their own.
    public RegistryKeyboardProfileStore(RegistryKey root, string keyboardsPath) {
        this.root = root;
        this.keyboardsPath = keyboardsPath;
    }

    // A subkey that is not named by a GUID, or has no model this app knows, is not a profile it can show.
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

            string profileName = key.GetValue("Name") as string ?? "";
            string[] identityTexts = key.GetValue("Identities") as string[] ?? [];
            profiles.Add(new KeyboardProfile(id, profileName, product, [.. identityTexts.Select(KeyboardIdentity.Parse).OfType<KeyboardIdentity>()]));
        }

        return [.. profiles.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase).ThenBy(profile => profile.Id)];
    }

    public void Save(KeyboardProfile profile) {
        List<KeyboardProfile> others = [.. Load().Where(other => other.Id != profile.Id)];
        foreach (KeyboardIdentity identity in profile.Identities) {
            KeyboardProfile? owner = others.FirstOrDefault(other => other.Owns(identity));
            if (owner is not null) {
                throw new InvalidOperationException($"{identity} already belongs to the profile {DriverStatus.ProfileKeyName(owner.Id)}.");
            }
        }

        using RegistryKey key = root.CreateSubKey($@"{keyboardsPath}\{DriverStatus.ProfileKeyName(profile.Id)}", writable: true);
        key.SetValue("Name", profile.Name, RegistryValueKind.String);
        key.SetValue("Model", KnownKeyboards.GetProductId(profile.Product), RegistryValueKind.DWord);
        key.SetValue("Identities", profile.Identities.Select(identity => identity.ToString()).ToArray(), RegistryValueKind.MultiString);
    }

    private static bool TryReadProduct(RegistryKey key, out KeyboardProduct product) {
        if (key.GetValue("Model") is int model && model is >= 0 and <= ushort.MaxValue) {
            return KnownKeyboards.TryGetProduct((ushort)model, out product);
        }

        product = default;
        return false;
    }
}
