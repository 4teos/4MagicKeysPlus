using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusTests;

public sealed class KeyboardOwnershipTests {
    private static KeyboardEndpoint Endpoint(string identity, bool isLive = true) {
        return new KeyboardEndpoint(KeyboardIdentity.Parse(identity)!, KeyboardProduct.A1843, isLive, isLive ? @"\\?\hid#path" : null);
    }

    private static KeyboardProfile Profile(params string[] identities) {
        return new KeyboardProfile(Guid.NewGuid(), KeyboardProduct.A1843, [.. identities.Select(text => KeyboardIdentity.Parse(text)!)]);
    }

    [Fact]
    public void WithoutProfiles_EveryLiveEndpointIsUnowned() {
        KeyboardEndpoint[] endpoints = [Endpoint("bt:C4141104498E"), Endpoint("usb:F0T125100BWJKPGAS")];

        Assert.Equal(endpoints, KeyboardOwnership.UnownedLiveEndpoints(endpoints, []));
    }

    [Fact]
    public void AnEndpointOfAProfile_IsOwned() {
        KeyboardEndpoint[] endpoints = [Endpoint("bt:C4141104498E"), Endpoint("usb:F0T125100BWJKPGAS")];

        IReadOnlyList<KeyboardEndpoint> unowned = KeyboardOwnership.UnownedLiveEndpoints(endpoints, [Profile("bt:C4141104498E")]);

        Assert.Equal([endpoints[1]], unowned);
    }

    [Fact]
    public void AKeyboardKnownOverOneTransportOnly_IsUnownedOverTheOther() {
        KeyboardEndpoint usb = Endpoint("usb:F0T125100BWJKPGAS");

        Assert.Equal([usb], KeyboardOwnership.UnownedLiveEndpoints([usb, Endpoint("bt:C4141104498E", isLive: false)], [Profile("bt:C4141104498E")]));
    }

    [Fact]
    public void AnEndpointThatIsNotLive_IsLeftAlone() {
        Assert.Empty(KeyboardOwnership.UnownedLiveEndpoints([Endpoint("bt:C4141104498E", isLive: false)], []));
    }

    [Fact]
    public void WhoeverOwnsTheIdentity_ItIsOwned() {
        KeyboardEndpoint[] endpoints = [Endpoint("usb:F0T125100BWJKPGAS")];

        Assert.Empty(KeyboardOwnership.UnownedLiveEndpoints(endpoints, [Profile("bt:C4141104498E"), Profile("usb:F0T125100BWJKPGAS")]));
    }
}
