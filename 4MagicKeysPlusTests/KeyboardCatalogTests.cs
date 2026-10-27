using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

internal static class KeyboardTestData {
    public static KeyboardEndpoint Endpoint(string identity, bool isLive = true, KeyboardProduct product = KeyboardProduct.A1843) {
        return new KeyboardEndpoint(KeyboardIdentity.Parse(identity)!, product, isLive, isLive ? $@"\\?\hid#{identity}" : null);
    }

    public static KeyboardProfile Profile(KeyboardProduct product, params string[] identities) {
        return new KeyboardProfile(Guid.NewGuid(), product, [.. identities.Select(text => KeyboardIdentity.Parse(text)!)]);
    }

    public static KeyboardProfile Profile(params string[] identities) {
        return Profile(KeyboardProduct.A1843, identities);
    }

    // The index tells the keyboards of a model apart (it is never 0: that would read as a KeyboardProduct).
    public static KeyboardProfile Profile(int index, params string[] identities) {
        return Profile(identities) with {
            Index = index
        };
    }

    // What a keyboard is shown by.
    public static string Shown(int index = 1, KeyboardProduct product = KeyboardProduct.A1843) {
        return KeyboardCatalog.ModelName(product, index);
    }
}

public sealed class KeyboardCatalogTests {
    private const string Bluetooth = "bt:C4141104498E";
    private const string Usb = "usb:F0T125100BWJKPGAS";

    [Fact]
    public void WithoutAnything_ThereAreNoKeyboards() {
        KeyboardCatalog catalog = KeyboardCatalog.Build([], []);

        Assert.Empty(catalog.Keyboards);
        Assert.Empty(catalog.Unowned);
    }

    [Fact]
    public void EveryProfileIsAKeyboard_InTheOrderOfTheStore() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth);
        KeyboardProfile office = KeyboardTestData.Profile(2, "bt:001122334455");

        KeyboardCatalog catalog = KeyboardCatalog.Build([desk, office], []);

        Assert.Equal([desk, office], catalog.Keyboards.Select(keyboard => keyboard.Profile));
        Assert.All(catalog.Keyboards, keyboard => Assert.False(keyboard.IsConnected));
    }

    [Fact]
    public void AProfile_IsConnectedOverItsLiveEndpoint() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth, Usb);
        KeyboardEndpoint live = KeyboardTestData.Endpoint(Usb);

        KeyboardEntry entry = Assert.Single(KeyboardCatalog.Build([desk], [live, KeyboardTestData.Endpoint(Bluetooth, isLive: false)]).Keyboards);

        Assert.True(entry.IsConnected);
        Assert.Same(live, entry.Live);
    }

    [Fact]
    public void AnEndpointThatIsNotLive_DoesNotConnectTheProfile() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth);

        KeyboardEntry entry = Assert.Single(KeyboardCatalog.Build([desk], [KeyboardTestData.Endpoint(Bluetooth, isLive: false)]).Keyboards);

        Assert.False(entry.IsConnected);
    }

    [Fact]
    public void WithBothWaysLive_TheCableIsTheOneToGoBy() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth, Usb);

        KeyboardEntry entry = Assert.Single(KeyboardCatalog.Build([desk], [KeyboardTestData.Endpoint(Bluetooth), KeyboardTestData.Endpoint(Usb)]).Keyboards);

        Assert.Equal(KeyboardConnection.Usb, entry.Live!.Identity.Connection);
    }

    [Fact]
    public void ALiveKeyboardNoProfileHas_IsUnowned_WithoutCandidatesWhenThereIsNoProfileOfItsModel() {
        KeyboardProfile other = KeyboardTestData.Profile(KeyboardProduct.A1644, "bt:001122334455");

        UnownedKeyboard unowned = Assert.Single(KeyboardCatalog.Build([other], [KeyboardTestData.Endpoint(Bluetooth)]).Unowned);

        Assert.Empty(unowned.Candidates);
    }

    [Fact]
    public void AProfileOfTheSameModelWithoutThatConnection_IsACandidate() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth);

        UnownedKeyboard unowned = Assert.Single(KeyboardCatalog.Build([desk], [KeyboardTestData.Endpoint(Usb)]).Unowned);

        Assert.Equal([desk], unowned.Candidates);
    }

    [Fact]
    public void AProfileThatIsNotConnected_IsACandidateAsWell() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth);

        KeyboardCatalog catalog = KeyboardCatalog.Build([desk], [KeyboardTestData.Endpoint(Usb)]);

        Assert.False(Assert.Single(catalog.Keyboards).IsConnected);
        Assert.Single(Assert.Single(catalog.Unowned).Candidates);
    }

    [Fact]
    public void AProfileThatHasThatConnectionAlready_IsNoCandidate() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth, "usb:SOMEOTHERSERIAL");

        UnownedKeyboard unowned = Assert.Single(KeyboardCatalog.Build([desk], [KeyboardTestData.Endpoint(Usb)]).Unowned);

        Assert.Empty(unowned.Candidates);
    }

    [Fact]
    public void AProfileOfAnotherModel_IsNoCandidate() {
        KeyboardProfile small = KeyboardTestData.Profile(KeyboardProduct.A1644, Bluetooth);

        UnownedKeyboard unowned = Assert.Single(KeyboardCatalog.Build([small], [KeyboardTestData.Endpoint(Usb)]).Unowned);

        Assert.Empty(unowned.Candidates);
    }

    [Fact]
    public void SeveralProfilesMayBeCandidates() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth);
        KeyboardProfile office = KeyboardTestData.Profile(2, "bt:001122334455");

        UnownedKeyboard unowned = Assert.Single(KeyboardCatalog.Build([desk, office], [KeyboardTestData.Endpoint(Usb)]).Unowned);

        Assert.Equal([desk, office], unowned.Candidates);
    }

    [Fact]
    public void TheFirstKeyboardOfAModel_HasTheIndexOne() {
        Assert.Equal(1, KeyboardCatalog.NewIndex(KeyboardProduct.A1843, []));
        Assert.Equal(1, KeyboardCatalog.NewIndex(KeyboardProduct.A1644, [KeyboardTestData.Profile(Bluetooth)]));
    }

    [Fact]
    public void AnIndexThatIsTaken_MakesTheNextOne() {
        KeyboardProfile first = KeyboardTestData.Profile(Bluetooth);
        KeyboardProfile second = KeyboardTestData.Profile(2, "bt:001122334455");
        KeyboardProfile third = KeyboardTestData.Profile(3, "bt:AAAAAAAAAAAA");

        Assert.Equal(2, KeyboardCatalog.NewIndex(KeyboardProduct.A1843, [first]));
        Assert.Equal(3, KeyboardCatalog.NewIndex(KeyboardProduct.A1843, [first, second]));
        Assert.Equal(2, KeyboardCatalog.NewIndex(KeyboardProduct.A1843, [first, third]));
        Assert.Equal(1, KeyboardCatalog.NewIndex(KeyboardProduct.A1843, [second]));
    }

    [Fact]
    public void KeyboardsOfAnotherModel_DoNotTakeTheIndex() {
        KeyboardProfile small = KeyboardTestData.Profile(KeyboardProduct.A1644, Bluetooth);

        Assert.Equal(1, KeyboardCatalog.NewIndex(KeyboardProduct.A1843, [small]));
    }

    [Fact]
    public void AKeyboard_IsShownByItsModelAndItsIndex() {
        string model = KeyboardModel.A1843.DeviceDescription;

        Assert.Equal(model, KeyboardCatalog.DisplayName(KeyboardTestData.Profile()));
        Assert.Equal(model, KeyboardCatalog.DisplayName(KeyboardTestData.Profile(1)));
        Assert.Equal($"{model} (2)", KeyboardCatalog.DisplayName(KeyboardTestData.Profile(2)));
        Assert.Equal($"{model} (3)", KeyboardCatalog.DisplayName(KeyboardTestData.Profile(3)));
        Assert.Equal(KeyboardModel.A1644.DeviceDescription, KeyboardCatalog.DisplayName(KeyboardTestData.Profile(KeyboardProduct.A1644)));
    }
}
