using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlusTests;

internal sealed class FakeFactoryDefaults : IFactoryDefaults {
    public List<(Guid ProfileId, KeyboardProduct Model)> Copies { get; } = [];
    public Exception? Failure {
        get; set;
    }

    public bool CopyToProfile(Guid profileId, KeyboardProduct model) {
        if (Failure is not null) {
            throw Failure;
        }

        Copies.Add((profileId, model));
        return true;
    }
}

public sealed class KeyboardSessionDefaultsTests {
    private const string Bluetooth = "bt:C4141104498E";
    private const string Usb = "usb:F0T125100BWJKPGAS";

    private readonly FakeProfileStore store = new();
    private readonly FakeDriver driver = new();
    private readonly FakeFactoryDefaults defaults = new();
    private List<KeyboardEndpoint> endpoints = [];

    private KeyboardSession Session() {
        return new KeyboardSession(store, () => endpoints, driver, defaults);
    }

    [Fact]
    public void ANewKeyboard_GetsTheFactoryDefaultsOfItsModel() {
        endpoints = [KeyboardTestData.Endpoint(Bluetooth, product: KeyboardProduct.A1644)];

        Session().Refresh();

        KeyboardProfile profile = Assert.Single(store.Profiles);
        Assert.Equal([(profile.Id, KeyboardProduct.A1644)], defaults.Copies);
    }

    [Fact]
    public void RefreshingAgain_CopiesNothingMore() {
        endpoints = [KeyboardTestData.Endpoint(Bluetooth)];
        KeyboardSession session = Session();

        session.Refresh();
        session.Refresh();

        Assert.Single(defaults.Copies);
    }

    [Fact]
    public void AKeyboardThatIsAnotherOne_GetsThemToo() {
        store.Profiles.Add(KeyboardTestData.Profile(Bluetooth));
        endpoints = [KeyboardTestData.Endpoint(Usb)];
        KeyboardSession session = Session();
        session.Refresh();
        Assert.Empty(defaults.Copies);

        session.StartNew(session.Catalog.Unowned.Single());

        KeyboardProfile created = store.Profiles.Single(profile => profile.Identities.Any(identity => identity.ToString() == Usb));
        Assert.Equal([(created.Id, KeyboardProduct.A1843)], defaults.Copies);
    }

    [Fact]
    public void AKeyboardThatIsTheSameAsAKnownOne_KeepsTheSettingsOfThatProfile() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth);
        store.Profiles.Add(desk);
        endpoints = [KeyboardTestData.Endpoint(Usb)];
        KeyboardSession session = Session();
        session.Refresh();

        session.Link(session.Catalog.Unowned.Single(), desk);

        Assert.Empty(defaults.Copies);
    }

    [Fact]
    public void WithoutDefaults_TheProfileIsMadeAsBefore() {
        endpoints = [KeyboardTestData.Endpoint(Bluetooth)];

        new KeyboardSession(store, () => endpoints, driver).Refresh();

        Assert.Single(store.Profiles);
    }

    [Fact]
    public void WhenTheCopyFails_StartingNewIsAFailureNotACrash() {
        store.Profiles.Add(KeyboardTestData.Profile(Bluetooth));
        endpoints = [KeyboardTestData.Endpoint(Usb)];
        KeyboardSession session = Session();
        session.Refresh();
        defaults.Failure = new IOException("disk");

        ApplyResult result = session.StartNew(session.Catalog.Unowned.Single());

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
    }
}
