namespace _4MagicKeysPlus.Core;

// One physical keyboard, regardless of the way it is connected, and the settings that belong to it. The identities are the ways
// Windows has seen it (its Bluetooth pairing, its USB cable): the driver finds the profile of a keyboard by them, so
// an identity belongs to one profile at most. Index (above 1) tells the keyboards of one model apart: the app shows a keyboard by
// its model and its index.
public sealed record KeyboardProfile(Guid Id, KeyboardProduct Product, IReadOnlyList<KeyboardIdentity> Identities, int Index = 1) {
    public bool Owns(KeyboardIdentity identity) {
        return Identities.Contains(identity);
    }
}
