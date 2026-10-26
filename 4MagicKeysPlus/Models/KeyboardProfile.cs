using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlus.Models;

// One physical keyboard, regardless of the way it is connected, and the settings that belong to it. The identities are the ways
// Windows has seen it (its Bluetooth pairing, its USB cable): the driver finds the profile of a keyboard by them, so
// an identity belongs to one profile at most.
public sealed record KeyboardProfile(Guid Id, string Name, KeyboardProduct Product, IReadOnlyList<KeyboardIdentity> Identities) {
    public bool Owns(KeyboardIdentity identity) {
        return Identities.Contains(identity);
    }
}
