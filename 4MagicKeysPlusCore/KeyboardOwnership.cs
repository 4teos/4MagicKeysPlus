namespace _4MagicKeysPlus.Core;

// Which of the keyboards on this PC have no profile yet.
public static class KeyboardOwnership {
    // The keyboards that are connected right now, by the way they are connected, whose identity no profile lists: the
    // driver gives them no rules, so someone has to give them a profile (the app does, or asks to link them to a profile
    // of the same keyboard). A paired Bluetooth keyboard that is out of reach is not one of them, there is nothing to
    // answer for it yet.
    public static IReadOnlyList<KeyboardEndpoint> UnownedLiveEndpoints(IEnumerable<KeyboardEndpoint> endpoints, IEnumerable<KeyboardProfile> profiles) {
        List<KeyboardProfile> known = [.. profiles];
        return [.. endpoints.Where(endpoint => endpoint.IsLive && !known.Any(profile => profile.Owns(endpoint.Identity)))];
    }
}
