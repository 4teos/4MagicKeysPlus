using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusAgent;

// Key: the profile of the keyboard (its identity while it has none), the same over USB and over Bluetooth.
// Name tells the notices apart; there is none while there is only one keyboard.
internal sealed record BatteryTarget(string Key, string? Name, DetectedKeyboard Keyboard);

internal static class BatteryTargets {
    // The cable is preferred when both ways of a keyboard are live.
    public static IReadOnlyList<BatteryTarget> Select(IReadOnlyList<KeyboardEndpoint> endpoints, IReadOnlyList<KeyboardProfile> profiles, Func<KeyboardProduct, string> modelName) {
        List<(string Key, KeyboardProfile? Profile, KeyboardEndpoint Endpoint)> connected = [];
        foreach (KeyboardProfile profile in profiles) {
            KeyboardEndpoint? best = Best(endpoints.Where(endpoint => profile.Owns(endpoint.Identity)));
            if (best is not null) {
                connected.Add((profile.Id.ToString("B"), profile, best));
            }
        }

        foreach (IGrouping<string, KeyboardEndpoint> unowned in KeyboardOwnership.UnownedLiveEndpoints(endpoints, profiles).GroupBy(endpoint => endpoint.Identity.ToString())) {
            KeyboardEndpoint? best = Best(unowned);
            if (best is not null) {
                connected.Add((unowned.Key, null, best));
            }
        }

        // A keyboard that is not connected is still one of those a notice has to be told from.
        bool severalKnown = profiles.Count + connected.Count(keyboard => keyboard.Profile is null) > 1;
        return [.. connected.Select(keyboard => new BatteryTarget(
            keyboard.Key,
            severalKnown ? NameOf(keyboard.Profile, keyboard.Endpoint, modelName) : null,
            new DetectedKeyboard(keyboard.Endpoint.Product, keyboard.Endpoint.Identity.Connection, keyboard.Endpoint.DevicePath!)))];
    }

    private static KeyboardEndpoint? Best(IEnumerable<KeyboardEndpoint> endpoints) {
        return endpoints
            .Where(endpoint => endpoint.IsLive && endpoint.DevicePath is not null)
            .OrderBy(endpoint => endpoint.Identity.Connection == KeyboardConnection.Usb ? 0 : 1)
            .FirstOrDefault();
    }

    // The model, and the index of the profile when it is above 1, as the configuration app shows a keyboard.
    private static string NameOf(KeyboardProfile? profile, KeyboardEndpoint endpoint, Func<KeyboardProduct, string> modelName) {
        string model = modelName(endpoint.Product);
        return profile is { Index: > 1 } ? $"{model} ({profile.Index})" : model;
    }
}
