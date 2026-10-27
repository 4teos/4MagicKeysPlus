using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlus.Services;

// A profile and the endpoint it is connected over right now, null when it is not connected.
internal sealed record KeyboardEntry(KeyboardProfile Profile, KeyboardEndpoint? Live) {
    public bool IsConnected => Live is not null;

    public string DisplayName => KeyboardCatalog.DisplayName(Profile);
}

// A keyboard that is connected but that no profile lists. Candidates are the profiles it may be the same keyboard as.
internal sealed record UnownedKeyboard(KeyboardEndpoint Endpoint, IReadOnlyList<KeyboardProfile> Candidates);

// The keyboards the app knows (the profiles, in the order the store gives them) and the ones that still need a profile,
// worked out from what is stored and what is connected, without touching either.
internal sealed class KeyboardCatalog {
    public static readonly KeyboardCatalog Empty = new([], []);

    private KeyboardCatalog(IReadOnlyList<KeyboardEntry> keyboards, IReadOnlyList<UnownedKeyboard> unowned) {
        Keyboards = keyboards;
        Unowned = unowned;
    }

    public IReadOnlyList<KeyboardEntry> Keyboards {
        get;
    }

    public IReadOnlyList<UnownedKeyboard> Unowned {
        get;
    }

    public static KeyboardCatalog Build(IReadOnlyList<KeyboardProfile> profiles, IReadOnlyList<KeyboardEndpoint> endpoints) {
        return new KeyboardCatalog(
            [.. profiles.Select(profile => new KeyboardEntry(profile, LiveEndpoint(profile, endpoints)))],
            [.. KeyboardOwnership.UnownedLiveEndpoints(endpoints, profiles).Select(endpoint => new UnownedKeyboard(endpoint, Candidates(endpoint, profiles)))]);
    }

    // A keyboard is shown by its model, in the language of the app, and by its index when that is above 1: nothing of that is
    // stored but the index.
    public static string DisplayName(KeyboardProfile profile) {
        return ModelName(profile.Product, profile.Index);
    }

    public static string ModelName(KeyboardProduct product, int index) {
        string model = KeyboardModel.From(product).DeviceDescription;
        return index > 1 ? $"{model} ({index})" : model;
    }

    // The index of a new profile of a model: the first one that no profile of the model has.
    public static int NewIndex(KeyboardProduct product, IEnumerable<KeyboardProfile> profiles) {
        HashSet<int> taken = [.. profiles.Where(profile => profile.Product == product).Select(profile => profile.Index)];
        int index = 1;
        while (taken.Contains(index)) {
            index++;
        }
        return index;
    }

    // With both of its ways live (Windows may take a while to let the Bluetooth one go when the cable goes in) the cable is
    // the one to go by.
    private static KeyboardEndpoint? LiveEndpoint(KeyboardProfile profile, IReadOnlyList<KeyboardEndpoint> endpoints) {
        return endpoints
            .Where(endpoint => endpoint.IsLive && profile.Owns(endpoint.Identity))
            .OrderBy(endpoint => endpoint.Identity.Connection == KeyboardConnection.Usb ? 0 : 1)
            .FirstOrDefault();
    }

    // A profile of the same model that has no way over this kind of connection yet could be this keyboard on its other one. Whether
    // the profile is connected right now is no matter: Windows drops the Bluetooth link of a keyboard a moment after its cable
    // goes in.
    private static IReadOnlyList<KeyboardProfile> Candidates(KeyboardEndpoint endpoint, IReadOnlyList<KeyboardProfile> profiles) {
        return [.. profiles.Where(profile => profile.Product == endpoint.Product && profile.Identities.All(identity => identity.Connection != endpoint.Identity.Connection))];
    }
}
