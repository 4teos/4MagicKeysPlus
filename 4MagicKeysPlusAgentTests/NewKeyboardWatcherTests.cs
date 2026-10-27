using _4MagicKeysPlus.Core;
using _4MagicKeysPlusAgent;

namespace _4MagicKeysPlusAgentTests;

public sealed class NewKeyboardWatcherTests {
    private List<KeyboardEndpoint> endpoints = [];
    private List<KeyboardProfile> profiles = [];
    private bool enabled = true;
    private bool appRunning;
    private int opened;

    private NewKeyboardWatcher Watcher() {
        return new NewKeyboardWatcher(() => endpoints, () => profiles, () => enabled, () => appRunning, () => opened++, TimeSpan.FromSeconds(10));
    }

    private static KeyboardEndpoint Endpoint(string identity, bool isLive = true) {
        return new KeyboardEndpoint(KeyboardIdentity.Parse(identity)!, KeyboardProduct.A1843, isLive, isLive ? @"\\?\hid#path" : null);
    }

    private static KeyboardProfile Profile(params string[] identities) {
        return new KeyboardProfile(Guid.NewGuid(), KeyboardProduct.A1843, [.. identities.Select(text => KeyboardIdentity.Parse(text)!)]);
    }

    [Fact]
    public void ALiveKeyboardWithoutAProfile_OpensTheApp() {
        endpoints = [Endpoint("bt:C4141104498E")];

        Watcher().Check();

        Assert.Equal(1, opened);
    }

    [Fact]
    public void TheSameKeyboard_OpensTheAppOnce() {
        endpoints = [Endpoint("bt:C4141104498E")];
        NewKeyboardWatcher watcher = Watcher();

        watcher.Check();
        watcher.Check();
        watcher.Check();

        Assert.Equal(1, opened);
    }

    [Fact]
    public void AKeyboardWithAProfile_LeavesTheAppAlone() {
        endpoints = [Endpoint("bt:C4141104498E")];
        profiles = [Profile("bt:C4141104498E")];

        Watcher().Check();

        Assert.Equal(0, opened);
    }

    [Fact]
    public void AKnownKeyboardOnANewTransport_OpensTheApp() {
        endpoints = [Endpoint("usb:F0T125100BWJKPGAS"), Endpoint("bt:C4141104498E", isLive: false)];
        profiles = [Profile("bt:C4141104498E")];

        Watcher().Check();

        Assert.Equal(1, opened);
    }

    [Fact]
    public void APairedKeyboardOutOfReach_LeavesTheAppAlone() {
        endpoints = [Endpoint("bt:C4141104498E", isLive: false)];

        Watcher().Check();

        Assert.Equal(0, opened);
    }

    [Fact]
    public void SeveralKeyboardsAtOnce_OpenTheAppOnce() {
        endpoints = [Endpoint("bt:C4141104498E"), Endpoint("usb:F0T125100BWJKPGAS")];

        Watcher().Check();

        Assert.Equal(1, opened);
    }

    [Fact]
    public void AnotherKeyboardLater_OpensTheAppAgain() {
        endpoints = [Endpoint("bt:C4141104498E")];
        NewKeyboardWatcher watcher = Watcher();
        watcher.Check();

        endpoints = [Endpoint("bt:C4141104498E"), Endpoint("usb:F0T125100BWJKPGAS")];
        watcher.Check();

        Assert.Equal(2, opened);
    }

    [Fact]
    public void WhileTheAppRuns_ItIsNotOpenedAgain_AndTheKeyboardCountsAsBroughtUp() {
        endpoints = [Endpoint("bt:C4141104498E")];
        appRunning = true;
        NewKeyboardWatcher watcher = Watcher();

        watcher.Check();
        appRunning = false;
        watcher.Check();

        Assert.Equal(0, opened);
    }

    [Fact]
    public void WhenTheSettingIsOff_NothingOpens_UntilItIsOn() {
        endpoints = [Endpoint("bt:C4141104498E")];
        enabled = false;
        NewKeyboardWatcher watcher = Watcher();

        watcher.Check();
        Assert.Equal(0, opened);

        enabled = true;
        watcher.Check();
        Assert.Equal(1, opened);
    }

    [Fact]
    public void WhenTheSettingIsOff_TheKeyboardsAreNotEvenLookedFor() {
        enabled = false;
        int enumerations = 0;
        NewKeyboardWatcher watcher = new(() => {
            enumerations++;
            return [];
        }, () => [], () => enabled, () => false, () => opened++, TimeSpan.FromSeconds(10));

        watcher.Check();

        Assert.Equal(0, enumerations);
    }

    [Fact]
    public void AKeyboardBroughtUpOnce_IsNotBroughtUpAgain_WhenItsProfileIsGone() {
        endpoints = [Endpoint("bt:C4141104498E")];
        NewKeyboardWatcher watcher = Watcher();
        watcher.Check();

        profiles = [Profile("bt:C4141104498E")];
        watcher.Check();
        profiles = [];
        watcher.Check();

        Assert.Equal(1, opened);
    }
}
