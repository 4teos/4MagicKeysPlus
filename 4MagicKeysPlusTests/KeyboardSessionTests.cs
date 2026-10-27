using Microsoft.Win32;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlusTests;

internal sealed class FakeProfileStore : IKeyboardProfileStore {
    public List<KeyboardProfile> Profiles { get; } = [];
    public Exception? SaveFailure {
        get; set;
    }

    // Like the registry, it gives the profiles in the order of their names.
    public IReadOnlyList<KeyboardProfile> Load() {
        return [.. Profiles.OrderBy(profile => profile.Product).ThenBy(profile => profile.Index).ThenBy(profile => profile.Id)];
    }

    public void Save(KeyboardProfile profile) {
        if (SaveFailure is not null) {
            throw SaveFailure;
        }

        KeyboardIdentity? clash = profile.Identities.FirstOrDefault(identity => Profiles.Any(other => other.Id != profile.Id && other.Owns(identity)));
        if (clash is not null) {
            throw new InvalidOperationException($"{clash} already belongs to another profile.");
        }

        Profiles.RemoveAll(existing => existing.Id == profile.Id);
        Profiles.Add(profile);
    }
}

public sealed class KeyboardSessionTests {
    private const string Bluetooth = "bt:C4141104498E";
    private const string Usb = "usb:F0T125100BWJKPGAS";

    private readonly FakeProfileStore store = new();
    private readonly FakeDriver driver = new();
    private List<KeyboardEndpoint> endpoints = [];

    private KeyboardSession Session() {
        return new KeyboardSession(store, () => endpoints, driver);
    }

    private static KeyboardEndpoint Endpoint(string identity, bool isLive = true) {
        return KeyboardTestData.Endpoint(identity, isLive);
    }

    [Fact]
    public void AKeyboardNoProfileKnows_GetsAnEmptyProfileThatIsShownByItsModel() {
        endpoints = [Endpoint(Bluetooth)];
        KeyboardSession session = Session();

        session.Refresh();

        KeyboardProfile profile = Assert.Single(store.Profiles);
        Assert.Equal(1, profile.Index);
        Assert.Equal(KeyboardModel.A1843.DeviceDescription, KeyboardCatalog.DisplayName(profile));
        Assert.Equal(KeyboardProduct.A1843, profile.Product);
        Assert.Equal([KeyboardIdentity.Parse(Bluetooth)!], profile.Identities);
        Assert.Equal(profile.Id, session.SelectedId);
        Assert.Empty(session.Catalog.Unowned);
        Assert.Equal(0, driver.Reloads);
    }

    [Fact]
    public void RefreshingAgain_MakesNoSecondProfile() {
        endpoints = [Endpoint(Bluetooth)];
        KeyboardSession session = Session();

        session.Refresh();
        session.Refresh();
        session.Refresh();

        Assert.Single(store.Profiles);
    }

    [Fact]
    public void AnotherKeyboardOfTheSameModel_GetsTheNextIndex() {
        KeyboardProfile named = KeyboardTestData.Profile(Bluetooth, Usb);
        store.Profiles.Add(named);
        endpoints = [Endpoint("bt:001122334455")];

        Session().Refresh();

        KeyboardProfile created = store.Profiles.Single(profile => profile.Id != named.Id);
        Assert.Equal(2, created.Index);
        Assert.Equal($"{KeyboardModel.A1843.DeviceDescription} (2)", KeyboardCatalog.DisplayName(created));
    }

    [Fact]
    public void AKeyboardThatMayBeAKnownOne_WaitsForTheAnswer() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth);
        store.Profiles.Add(desk);
        endpoints = [Endpoint(Usb), Endpoint(Bluetooth, isLive: false)];
        KeyboardSession session = Session();

        session.Refresh();

        Assert.Single(store.Profiles);
        UnownedKeyboard waiting = Assert.Single(session.Catalog.Unowned);
        Assert.Equal([desk.Id], waiting.Candidates.Select(candidate => candidate.Id));
        Assert.Null(session.SelectedId);
    }

    [Fact]
    public void BothWaysOfANewKeyboard_AreOneProfileAndAQuestion() {
        endpoints = [Endpoint(Bluetooth), Endpoint(Usb)];
        KeyboardSession session = Session();

        session.Refresh();

        KeyboardProfile profile = Assert.Single(store.Profiles);
        Assert.Equal([KeyboardIdentity.Parse(Bluetooth)!], profile.Identities);
        UnownedKeyboard waiting = Assert.Single(session.Catalog.Unowned);
        Assert.Equal(Usb, waiting.Endpoint.Identity.ToString());
        Assert.Equal([profile.Id], waiting.Candidates.Select(candidate => candidate.Id));
    }

    [Fact]
    public void Linking_AddsTheConnectionToTheProfile_TellsTheDriver_AndSelectsIt() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth);
        store.Profiles.Add(desk);
        endpoints = [Endpoint(Usb), Endpoint(Bluetooth, isLive: false)];
        KeyboardSession session = Session();
        session.Refresh();
        UnownedKeyboard waiting = session.Catalog.Unowned.Single();

        ApplyResult result = session.Link(waiting, desk);

        Assert.Equal(ApplyOutcome.Applied, result.Outcome);
        KeyboardProfile saved = Assert.Single(store.Profiles);
        Assert.Equal([Bluetooth, Usb], saved.Identities.Select(identity => identity.ToString()));
        Assert.Equal(1, driver.Reloads);
        Assert.Equal(desk.Id, session.SelectedId);
        Assert.Empty(session.Catalog.Unowned);
    }

    [Fact]
    public void Linking_WithoutAControlDevice_StillKeepsTheLink() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth);
        store.Profiles.Add(desk);
        driver.Result = null;
        endpoints = [Endpoint(Usb)];
        KeyboardSession session = Session();
        session.Refresh();

        ApplyResult result = session.Link(session.Catalog.Unowned.Single(), desk);

        Assert.Equal(ApplyOutcome.Warning, result.Outcome);
        Assert.Equal(2, Assert.Single(store.Profiles).Identities.Count);
    }

    [Fact]
    public void AFailedLink_ChangesNothingAndDoesNotTouchTheDriver() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth);
        store.Profiles.Add(desk);
        endpoints = [Endpoint(Usb)];
        KeyboardSession session = Session();
        session.Refresh();
        store.SaveFailure = new UnauthorizedAccessException("denied");

        ApplyResult result = session.Link(session.Catalog.Unowned.Single(), desk);

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.Single(Assert.Single(store.Profiles).Identities);
        Assert.Equal(0, driver.Reloads);
        Assert.Single(session.Catalog.Unowned);
    }

    [Fact]
    public void LinkingToAnIdentityThatSomeoneElseTookMeanwhile_IsAFailureNotACrash() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth);
        store.Profiles.Add(desk);
        endpoints = [Endpoint(Usb)];
        KeyboardSession session = Session();
        session.Refresh();
        UnownedKeyboard waiting = session.Catalog.Unowned.Single();
        store.Profiles.Add(KeyboardTestData.Profile("bt:001122334455", Usb));

        ApplyResult result = session.Link(waiting, desk);

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.Equal(0, driver.Reloads);
    }

    [Fact]
    public void StartingNew_MakesAnEmptyProfileOfItsOwn_AndSelectsIt() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth);
        store.Profiles.Add(desk);
        endpoints = [Endpoint(Usb)];
        KeyboardSession session = Session();
        session.Refresh();

        ApplyResult result = session.StartNew(session.Catalog.Unowned.Single());

        Assert.Equal(ApplyOutcome.Applied, result.Outcome);
        Assert.Equal(2, store.Profiles.Count);
        KeyboardProfile created = store.Profiles.Single(profile => profile.Id != desk.Id);
        Assert.Equal([KeyboardIdentity.Parse(Usb)!], created.Identities);
        Assert.Equal(created.Id, session.SelectedId);
        Assert.Empty(session.Catalog.Unowned);
        Assert.Single(desk.Identities);
    }

    [Fact]
    public void AFailedStartingNew_IsAFailureNotACrash() {
        store.Profiles.Add(KeyboardTestData.Profile(Bluetooth));
        endpoints = [Endpoint(Usb)];
        KeyboardSession session = Session();
        session.Refresh();
        store.SaveFailure = new IOException("disk");

        ApplyResult result = session.StartNew(session.Catalog.Unowned.Single());

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.Single(store.Profiles);
    }

    [Fact]
    public void WhenAProfileCannotBeMade_RefreshingCarriesOn() {
        store.SaveFailure = new UnauthorizedAccessException("denied");
        endpoints = [Endpoint(Bluetooth)];
        KeyboardSession session = Session();

        session.Refresh();

        Assert.Empty(store.Profiles);
        Assert.Single(session.Catalog.Unowned);
        Assert.Null(session.SelectedId);
    }

    [Fact]
    public void TheFirstConnectedKeyboard_IsSelected() {
        KeyboardProfile alpha = KeyboardTestData.Profile("bt:AAAAAAAAAAAA");
        KeyboardProfile beta = KeyboardTestData.Profile(2, "bt:BBBBBBBBBBBB");
        KeyboardProfile gamma = KeyboardTestData.Profile(3, "bt:CCCCCCCCCCCC");
        store.Profiles.AddRange([gamma, beta, alpha]);
        endpoints = [Endpoint("bt:BBBBBBBBBBBB"), Endpoint("bt:CCCCCCCCCCCC")];
        KeyboardSession session = Session();

        session.Refresh();

        Assert.Equal(beta.Id, session.SelectedId);
    }

    [Fact]
    public void TheSelectedKeyboard_StaysWhileItIsConnected() {
        KeyboardProfile alpha = KeyboardTestData.Profile("bt:AAAAAAAAAAAA");
        KeyboardProfile beta = KeyboardTestData.Profile(2, "bt:BBBBBBBBBBBB");
        store.Profiles.AddRange([alpha, beta]);
        endpoints = [Endpoint("bt:AAAAAAAAAAAA"), Endpoint("bt:BBBBBBBBBBBB")];
        KeyboardSession session = Session();
        session.Refresh();

        Assert.True(session.Select(beta.Id));
        session.Refresh();

        Assert.Equal(beta.Id, session.SelectedId);
    }

    [Fact]
    public void WhenTheSelectedKeyboardGoes_TheFirstConnectedOneTakesOver_AndNoneIsNone() {
        KeyboardProfile alpha = KeyboardTestData.Profile("bt:AAAAAAAAAAAA");
        KeyboardProfile beta = KeyboardTestData.Profile(2, "bt:BBBBBBBBBBBB");
        store.Profiles.AddRange([alpha, beta]);
        endpoints = [Endpoint("bt:AAAAAAAAAAAA"), Endpoint("bt:BBBBBBBBBBBB")];
        KeyboardSession session = Session();
        session.Refresh();
        session.Select(beta.Id);

        endpoints = [Endpoint("bt:AAAAAAAAAAAA"), Endpoint("bt:BBBBBBBBBBBB", isLive: false)];
        session.Refresh();
        Assert.Equal(alpha.Id, session.SelectedId);

        endpoints = [];
        session.Refresh();
        Assert.Null(session.SelectedId);
        Assert.Null(session.Selected);
    }

    [Fact]
    public void AKeyboardThatIsNotConnected_CannotBeSelected() {
        KeyboardProfile alpha = KeyboardTestData.Profile("bt:AAAAAAAAAAAA");
        KeyboardProfile beta = KeyboardTestData.Profile(2, "bt:BBBBBBBBBBBB");
        store.Profiles.AddRange([alpha, beta]);
        endpoints = [Endpoint("bt:AAAAAAAAAAAA")];
        KeyboardSession session = Session();
        session.Refresh();

        Assert.False(session.Select(beta.Id));
        Assert.False(session.Select(Guid.NewGuid()));

        Assert.Equal(alpha.Id, session.SelectedId);
    }

    [Fact]
    public void Changed_IsRaisedByARefreshAndBySelecting() {
        KeyboardProfile alpha = KeyboardTestData.Profile("bt:AAAAAAAAAAAA");
        store.Profiles.Add(alpha);
        endpoints = [Endpoint("bt:AAAAAAAAAAAA")];
        KeyboardSession session = Session();
        int raised = 0;
        session.Changed += () => raised++;

        session.Refresh();
        session.Select(alpha.Id);

        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task ARefreshThatIsStillGoingOn_IsNotStartedASecondTime() {
        using ManualResetEventSlim release = new();
        int enumerations = 0;
        KeyboardSession session = new(store, () => {
            Interlocked.Increment(ref enumerations);
            release.Wait(TimeSpan.FromSeconds(10));
            return [];
        }, driver);

        Task first = session.RefreshAsync();
        await session.RefreshAsync();
        release.Set();
        await first;

        Assert.Equal(1, enumerations);
    }
}

[Collection(TestCollections.Registry)]
public sealed class KeyboardSessionRegistryTests : IDisposable {
    private readonly ScratchKey scratch = new();

    public void Dispose() {
        scratch.Dispose();
    }

    // Every load of the registry gives profiles of their own, equal to the last ones by value only: the keyboard that is
    // shown must not change because of that.
    [Fact]
    public void RefreshingOverTheRegistry_KeepsTheSelectionAndTheItemsWhereTheyAre() {
        RegistryKeyboardProfileStore store = new(Registry.CurrentUser, scratch.Path("Keyboards"));
        KeyboardProfile alpha = KeyboardTestData.Profile("bt:AAAAAAAAAAAA");
        KeyboardProfile beta = KeyboardTestData.Profile(2, "bt:BBBBBBBBBBBB");
        store.Save(alpha);
        store.Save(beta);
        List<KeyboardEndpoint> endpoints = [KeyboardTestData.Endpoint("bt:AAAAAAAAAAAA"), KeyboardTestData.Endpoint("bt:BBBBBBBBBBBB")];
        KeyboardSession session = new(store, () => endpoints, new FakeDriver());
        session.Refresh();
        KeyboardSelectorViewModel selector = new(session);
        session.Select(beta.Id);
        KeyboardItem[] items = [.. selector.Items];
        KeyboardItem? selected = selector.Selected;

        session.Refresh();
        session.Refresh();

        Assert.Equal(beta.Id, session.SelectedId);
        Assert.Same(selected, selector.Selected);
        Assert.Equal(items, selector.Items);
        Assert.Equal(2, store.Load().Count);
    }
}
