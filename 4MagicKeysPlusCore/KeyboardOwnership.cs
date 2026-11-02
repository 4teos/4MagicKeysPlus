namespace _4MagicKeysPlus.Core;

public static class KeyboardOwnership {
    // A paired Bluetooth keyboard that is out of reach is not one of them: there is nothing to answer for it yet.
    public static IReadOnlyList<KeyboardEndpoint> UnownedLiveEndpoints(IEnumerable<KeyboardEndpoint> endpoints, IEnumerable<KeyboardProfile> profiles) {
        List<KeyboardProfile> known = [.. profiles];
        return [.. endpoints.Where(endpoint => endpoint.IsLive && !known.Any(profile => profile.Owns(endpoint.Identity)))];
    }
}
