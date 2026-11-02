namespace _4MagicKeysPlus.Core;

public static class KeyboardEnumerator {
    public static IReadOnlyList<KeyboardEndpoint> Enumerate() {
        string[] present = [.. PnpDeviceIds.Present("USB"), .. PnpDeviceIds.Present("BTHENUM")];
        return Combine(present, LiveKeyboardScanner.Scan());
    }

    // Present devices give the endpoints (a pairing exists whether its link is up or not); the live interfaces mark which are connected
    // and add the ones the device IDs did not explain.
    internal static IReadOnlyList<KeyboardEndpoint> Combine(IEnumerable<string> presentDeviceIds, IEnumerable<LiveKeyboardInterface> liveInterfaces) {
        Dictionary<KeyboardIdentity, KeyboardEndpoint> endpoints = [];

        foreach (string deviceId in presentDeviceIds) {
            PnpKeyboard? keyboard = KeyboardPnpIds.Parse(deviceId);
            if (keyboard is not null) {
                endpoints.TryAdd(keyboard.Identity, new KeyboardEndpoint(keyboard.Identity, keyboard.Product, keyboard.Identity.Connection == KeyboardConnection.Usb, null));
            }
        }

        foreach (LiveKeyboardInterface live in liveInterfaces) {
            if (live.Identity is null || (endpoints.TryGetValue(live.Identity, out KeyboardEndpoint? known) && known.DevicePath is not null)) {
                continue;
            }
            endpoints[live.Identity] = new KeyboardEndpoint(live.Identity, live.Product, true, live.DevicePath);
        }

        return [.. endpoints.Values
            .OrderBy(endpoint => endpoint.Product)
            .ThenBy(endpoint => endpoint.Identity.Connection)
            .ThenBy(endpoint => endpoint.Identity.Id, StringComparer.Ordinal)];
    }
}
