using System.ComponentModel;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.ViewModels;
using _4MagicKeysPlus.Views;

namespace _4MagicKeysPlusTests;

public sealed class KeyboardSelectorViewModelTests {
    private const string Bluetooth = "bt:C4141104498E";
    private const string Usb = "usb:F0T125100BWJKPGAS";

    private readonly FakeProfileStore store = new();
    private readonly FakeDriver driver = new();
    private List<KeyboardEndpoint> endpoints = [];

    private (KeyboardSession Session, KeyboardSelectorViewModel Selector) Open() {
        KeyboardSession session = new(store, () => endpoints, driver);
        session.Refresh();
        return (session, new KeyboardSelectorViewModel(session));
    }

    [Fact]
    public void WithoutKeyboards_ThereIsNothingToSelect() {
        (_, KeyboardSelectorViewModel selector) = Open();

        Assert.False(selector.HasSeveralKeyboards);
        Assert.Empty(selector.Items);
        Assert.Null(selector.Selected);
        Assert.False(selector.HasLinkPrompt);
    }

    [Fact]
    public void WithOneKeyboardKnown_ThereIsNothingToChooseFrom_AndWithTwoThereIs() {
        store.Profiles.Add(KeyboardTestData.Profile(Bluetooth));
        endpoints = [KeyboardTestData.Endpoint(Bluetooth)];
        (KeyboardSession session, KeyboardSelectorViewModel selector) = Open();
        Assert.False(selector.HasSeveralKeyboards);
        Assert.Single(selector.Items);

        store.Profiles.Add(KeyboardTestData.Profile(2, "bt:001122334455"));
        session.Refresh();

        Assert.True(selector.HasSeveralKeyboards);
    }

    [Fact]
    public void TheKnownKeyboards_AreListed_AndTheConnectedOneIsSelected() {
        store.Profiles.AddRange([KeyboardTestData.Profile("bt:AAAAAAAAAAAA"), KeyboardTestData.Profile(2, "bt:BBBBBBBBBBBB")]);
        endpoints = [KeyboardTestData.Endpoint("bt:BBBBBBBBBBBB")];

        (_, KeyboardSelectorViewModel selector) = Open();

        Assert.True(selector.HasSeveralKeyboards);
        Assert.Equal([KeyboardTestData.Shown(), KeyboardTestData.Shown(2)], selector.Items.Select(item => item.Name));
        Assert.Equal([false, true], selector.Items.Select(item => item.IsConnected));
        Assert.Equal(KeyboardTestData.Shown(2), selector.Selected?.Name);
    }

    [Fact]
    public void AKeyboardIsShownByItsConnection() {
        store.Profiles.Add(KeyboardTestData.Profile(Bluetooth, Usb));
        endpoints = [KeyboardTestData.Endpoint(Usb)];

        (KeyboardSession session, KeyboardSelectorViewModel selector) = Open();
        KeyboardItem item = Assert.Single(selector.Items);
        Assert.Equal(Glyphs.Usb, item.ConnectionGlyph);

        endpoints = [KeyboardTestData.Endpoint(Bluetooth)];
        session.Refresh();
        Assert.Equal(Glyphs.Bluetooth, item.ConnectionGlyph);

        endpoints = [];
        session.Refresh();
        Assert.Equal("", item.ConnectionGlyph);
        Assert.Equal($"{item.Name} ({Strings.KeyboardNotConnected})", item.Tooltip);
    }

    [Fact]
    public void TheTooltipOfAConnectedKeyboard_IsItsName() {
        store.Profiles.Add(KeyboardTestData.Profile(Bluetooth));
        endpoints = [KeyboardTestData.Endpoint(Bluetooth)];

        (_, KeyboardSelectorViewModel selector) = Open();

        KeyboardItem item = Assert.Single(selector.Items);
        Assert.Equal(item.Name, item.Tooltip);
    }

    [Fact]
    public void ARefresh_UpdatesTheItemsInPlace() {
        store.Profiles.AddRange([KeyboardTestData.Profile("bt:AAAAAAAAAAAA"), KeyboardTestData.Profile(2, "bt:BBBBBBBBBBBB")]);
        endpoints = [KeyboardTestData.Endpoint("bt:AAAAAAAAAAAA")];
        (KeyboardSession session, KeyboardSelectorViewModel selector) = Open();
        KeyboardItem[] items = [.. selector.Items];
        KeyboardItem? selected = selector.Selected;

        session.Refresh();

        Assert.Equal(items, selector.Items);
        Assert.Same(selected, selector.Selected);
        Assert.Equal(2, selector.Items.Count);
    }

    [Fact]
    public void AKeyboardThatAppears_IsAddedAndOneThatGoes_IsRemoved() {
        store.Profiles.Add(KeyboardTestData.Profile(2, "bt:AAAAAAAAAAAA"));
        (KeyboardSession session, KeyboardSelectorViewModel selector) = Open();
        KeyboardItem desk = Assert.Single(selector.Items);

        KeyboardProfile office = KeyboardTestData.Profile("bt:BBBBBBBBBBBB");
        store.Profiles.Add(office);
        session.Refresh();
        Assert.Equal([KeyboardTestData.Shown(), KeyboardTestData.Shown(2)], selector.Items.Select(item => item.Name));
        Assert.Same(desk, selector.Items[1]);

        store.Profiles.RemoveAll(profile => profile.Id == office.Id);
        session.Refresh();
        Assert.Equal([desk], selector.Items);
    }

    [Fact]
    public void ChoosingAConnectedKeyboard_SelectsIt() {
        store.Profiles.AddRange([KeyboardTestData.Profile("bt:AAAAAAAAAAAA"), KeyboardTestData.Profile(2, "bt:BBBBBBBBBBBB")]);
        endpoints = [KeyboardTestData.Endpoint("bt:AAAAAAAAAAAA"), KeyboardTestData.Endpoint("bt:BBBBBBBBBBBB")];
        (KeyboardSession session, KeyboardSelectorViewModel selector) = Open();

        selector.Selected = selector.Items[1];

        Assert.Equal(selector.Items[1].Id, session.SelectedId);
        Assert.Same(selector.Items[1], selector.Selected);
    }

    [Fact]
    public void ChoosingAKeyboardThatIsNotConnected_ChangesNothing_AndTheBoxIsToldToShowTheOldOne() {
        store.Profiles.AddRange([KeyboardTestData.Profile("bt:AAAAAAAAAAAA"), KeyboardTestData.Profile(2, "bt:BBBBBBBBBBBB")]);
        endpoints = [KeyboardTestData.Endpoint("bt:AAAAAAAAAAAA")];
        (KeyboardSession session, KeyboardSelectorViewModel selector) = Open();
        List<string?> changed = [];
        selector.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        selector.Selected = selector.Items[1];

        Assert.Equal(selector.Items[0].Id, session.SelectedId);
        Assert.Same(selector.Items[0], selector.Selected);
        Assert.Contains(nameof(KeyboardSelectorViewModel.Selected), changed);
    }

    [Fact]
    public void AKeyboardThatMayBeAKnownOne_IsAskedAbout() {
        store.Profiles.Add(KeyboardTestData.Profile(Bluetooth));
        endpoints = [KeyboardTestData.Endpoint(Usb)];

        (_, KeyboardSelectorViewModel selector) = Open();

        Assert.True(selector.HasLinkPrompt);
        Assert.False(selector.HasSeveralCandidates);
        Assert.Equal(KeyboardTestData.Shown(), Assert.Single(selector.Candidates).Name);
        Assert.Contains(KeyboardTestData.Shown(), selector.LinkPromptText);
        Assert.Contains(KeyboardModel.A1843.DeviceDescription, selector.LinkPromptText);
        Assert.Contains(Strings.ConnectionUsb, selector.LinkPromptText);
    }

    [Fact]
    public void WithSeveralCandidates_TheUserChoosesAmongThem() {
        store.Profiles.AddRange([KeyboardTestData.Profile(Bluetooth), KeyboardTestData.Profile(2, "bt:001122334455")]);
        endpoints = [KeyboardTestData.Endpoint(Usb)];

        (_, KeyboardSelectorViewModel selector) = Open();

        Assert.True(selector.HasSeveralCandidates);
        Assert.Equal([KeyboardTestData.Shown(), KeyboardTestData.Shown(2)], selector.Candidates.Select(candidate => candidate.Name));
        Assert.Equal(KeyboardTestData.Shown(), selector.SelectedCandidate?.Name);
    }

    [Fact]
    public void ARefresh_KeepsTheChoiceAmongTheCandidates() {
        store.Profiles.AddRange([KeyboardTestData.Profile(Bluetooth), KeyboardTestData.Profile(2, "bt:001122334455")]);
        endpoints = [KeyboardTestData.Endpoint(Usb)];
        (KeyboardSession session, KeyboardSelectorViewModel selector) = Open();
        selector.SelectedCandidate = selector.Candidates[1];

        session.Refresh();

        Assert.Equal(KeyboardTestData.Shown(2), selector.SelectedCandidate?.Name);
    }

    [Fact]
    public void YesLinksTheKeyboardToTheChosenProfile() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth);
        KeyboardProfile office = KeyboardTestData.Profile(2, "bt:001122334455");
        store.Profiles.AddRange([desk, office]);
        endpoints = [KeyboardTestData.Endpoint(Usb)];
        (KeyboardSession session, KeyboardSelectorViewModel selector) = Open();
        selector.SelectedCandidate = selector.Candidates[1];

        selector.LinkToCandidate();

        Assert.Equal(["bt:001122334455", Usb], store.Profiles.Single(profile => profile.Id == office.Id).Identities.Select(identity => identity.ToString()));
        Assert.Single(store.Profiles.Single(profile => profile.Id == desk.Id).Identities);
        Assert.Equal(office.Id, session.SelectedId);
        Assert.False(selector.HasLinkPrompt);
        Assert.Empty(selector.Candidates);
        Assert.Equal(1, driver.Reloads);
    }

    [Fact]
    public void NoMakesTheKeyboardAProfileOfItsOwn() {
        KeyboardProfile desk = KeyboardTestData.Profile(Bluetooth);
        store.Profiles.Add(desk);
        endpoints = [KeyboardTestData.Endpoint(Usb)];
        (KeyboardSession session, KeyboardSelectorViewModel selector) = Open();

        selector.StartNewKeyboard();

        Assert.Equal(2, store.Profiles.Count);
        Assert.False(selector.HasLinkPrompt);
        Assert.NotEqual(desk.Id, session.SelectedId);
        Assert.Equal(2, selector.Items.Count);
    }

    [Fact]
    public void AnAnswerThatCannotBeSaved_LeavesTheQuestionWithTheReason() {
        store.Profiles.Add(KeyboardTestData.Profile(Bluetooth));
        endpoints = [KeyboardTestData.Endpoint(Usb)];
        (_, KeyboardSelectorViewModel selector) = Open();
        store.SaveFailure = new UnauthorizedAccessException("denied");

        selector.LinkToCandidate();

        Assert.True(selector.HasLinkPrompt);
        Assert.True(selector.HasProblem);
        Assert.Contains("denied", selector.Problem);
        Assert.Equal(0, driver.Reloads);
    }

    [Fact]
    public void AnAnswerWithNothingToAnswer_DoesNothing() {
        (_, KeyboardSelectorViewModel selector) = Open();

        selector.LinkToCandidate();
        selector.StartNewKeyboard();

        Assert.Empty(store.Profiles);
    }

    [Fact]
    public void ANewKeyboardNoProfileCouldBe_IsNoQuestion() {
        endpoints = [KeyboardTestData.Endpoint(Bluetooth)];

        (_, KeyboardSelectorViewModel selector) = Open();

        Assert.False(selector.HasLinkPrompt);
        Assert.Single(selector.Items);
        Assert.Equal(KeyboardModel.A1843.DeviceDescription, selector.Selected?.Name);
    }

    [Fact]
    public void ThePropertyChanges_AreRaisedForWhatTheScreenBinds() {
        store.Profiles.Add(KeyboardTestData.Profile(Bluetooth));
        endpoints = [KeyboardTestData.Endpoint(Usb)];
        KeyboardSession session = new(store, () => endpoints, driver);
        KeyboardSelectorViewModel selector = new(session);
        List<string?> changed = [];
        ((INotifyPropertyChanged)selector).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        session.Refresh();

        Assert.Contains(nameof(KeyboardSelectorViewModel.HasLinkPrompt), changed);
        Assert.Contains(nameof(KeyboardSelectorViewModel.LinkPromptText), changed);
        Assert.Contains(nameof(KeyboardSelectorViewModel.HasSeveralKeyboards), changed);
        Assert.Contains(nameof(KeyboardSelectorViewModel.Selected), changed);
    }
}
