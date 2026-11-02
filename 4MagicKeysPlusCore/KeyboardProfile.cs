namespace _4MagicKeysPlus.Core;

// One physical keyboard, however it is connected. An identity belongs to one profile at most (the driver finds the profile by it);
// Index (above 1) tells the keyboards of one model apart.
public sealed record KeyboardProfile(Guid Id, KeyboardProduct Product, IReadOnlyList<KeyboardIdentity> Identities, int Index = 1) {
    public bool Owns(KeyboardIdentity identity) {
        return Identities.Contains(identity);
    }
}
