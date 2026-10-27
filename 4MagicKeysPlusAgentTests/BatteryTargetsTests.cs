using _4MagicKeysPlus.Core;
using _4MagicKeysPlusAgent;

namespace _4MagicKeysPlusAgentTests;

public class BatteryTargetsTests {
    private const string Bluetooth = "bt:C4141104498E";
    private const string Usb = "usb:F0T125100BWJKPGAS";
    private const string OtherBluetooth = "bt:001122334455";

    private static string ModelName(KeyboardProduct product) {
        return $"model {product}";
    }

    private static KeyboardEndpoint Endpoint(string identity, bool isLive = true, KeyboardProduct product = KeyboardProduct.A1843) {
        return new KeyboardEndpoint(KeyboardIdentity.Parse(identity)!, product, isLive, isLive ? $@"\\?\hid#{identity}" : null);
    }

    private static KeyboardProfile Profile(KeyboardProduct product, int index, params string[] identities) {
        return new KeyboardProfile(Guid.NewGuid(), product, [.. identities.Select(text => KeyboardIdentity.Parse(text)!)], index);
    }

    private static KeyboardProfile Profile(params string[] identities) {
        return Profile(KeyboardProduct.A1843, 1, identities);
    }

    private static IReadOnlyList<BatteryTarget> Select(KeyboardEndpoint[] endpoints, params KeyboardProfile[] profiles) {
        return BatteryTargets.Select(endpoints, profiles, ModelName);
    }

    [Fact]
    public void WithoutAConnectedKeyboard_ThereIsNothingToRead() {
        Assert.Empty(Select([], Profile(Bluetooth)));
        Assert.Empty(Select([Endpoint(Bluetooth, isLive: false)], Profile(Bluetooth)));
    }

    [Fact]
    public void AKeyboardOfAProfile_IsOneTarget_KeyedByTheProfile() {
        KeyboardProfile desk = Profile(Bluetooth, Usb);

        BatteryTarget target = Assert.Single(Select([Endpoint(Bluetooth)], desk));

        Assert.Equal(desk.Id.ToString("B"), target.Key);
        Assert.Equal(KeyboardConnection.Bluetooth, target.Keyboard.Connection);
        Assert.Equal(KeyboardProduct.A1843, target.Keyboard.Product);
        Assert.Equal($@"\\?\hid#{Bluetooth}", target.Keyboard.DevicePath);
    }

    [Fact]
    public void TheKeyIsTheSame_WhicheverWayTheKeyboardIsConnected() {
        KeyboardProfile desk = Profile(Bluetooth, Usb);

        string overBluetooth = Select([Endpoint(Bluetooth), Endpoint(Usb, isLive: false)], desk).Single().Key;
        string overUsb = Select([Endpoint(Usb), Endpoint(Bluetooth, isLive: false)], desk).Single().Key;

        Assert.Equal(overBluetooth, overUsb);
    }

    [Fact]
    public void WithBothWaysLive_TheCableIsRead() {
        KeyboardProfile desk = Profile(Bluetooth, Usb);

        BatteryTarget target = Assert.Single(Select([Endpoint(Bluetooth), Endpoint(Usb)], desk));

        Assert.Equal(KeyboardConnection.Usb, target.Keyboard.Connection);
    }

    [Fact]
    public void EveryConnectedKeyboard_IsATarget_InTheOrderOfTheProfiles() {
        KeyboardProfile first = Profile(Bluetooth);
        KeyboardProfile second = Profile(KeyboardProduct.A1644, 1, OtherBluetooth);

        IReadOnlyList<BatteryTarget> targets = Select([Endpoint(OtherBluetooth, product: KeyboardProduct.A1644), Endpoint(Bluetooth)], first, second);

        Assert.Equal([first.Id.ToString("B"), second.Id.ToString("B")], targets.Select(target => target.Key));
        Assert.Equal([KeyboardProduct.A1843, KeyboardProduct.A1644], targets.Select(target => target.Keyboard.Product));
    }

    [Fact]
    public void ForOneKeyboardKnown_TheNoticeHasNoName() {
        BatteryTarget target = Assert.Single(Select([Endpoint(Bluetooth)], Profile(Bluetooth)));

        Assert.Null(target.Name);
    }

    [Fact]
    public void WithSeveralKeyboardsKnown_TheNoticeNamesTheKeyboard_ByItsModelAndItsIndex() {
        KeyboardProfile first = Profile(Bluetooth);
        KeyboardProfile second = Profile(KeyboardProduct.A1843, 2, OtherBluetooth);

        IReadOnlyList<BatteryTarget> targets = Select([Endpoint(Bluetooth), Endpoint(OtherBluetooth)], first, second);

        Assert.Equal(["model A1843", "model A1843 (2)"], targets.Select(target => target.Name));
    }

    [Fact]
    public void AKeyboardThatIsNotConnected_StillCountsAsKnown() {
        KeyboardProfile first = Profile(Bluetooth);
        KeyboardProfile away = Profile(KeyboardProduct.A1644, 1, OtherBluetooth);

        BatteryTarget target = Assert.Single(Select([Endpoint(Bluetooth)], first, away));

        Assert.Equal("model A1843", target.Name);
    }

    [Fact]
    public void AKeyboardWithoutAProfile_IsATarget_KeyedByItsIdentity() {
        BatteryTarget target = Assert.Single(Select([Endpoint(Usb)]));

        Assert.Equal(Usb, target.Key);
        Assert.Null(target.Name);
    }

    [Fact]
    public void AKeyboardWithoutAProfile_IsAnotherKeyboardToTellApart() {
        KeyboardProfile known = Profile(Bluetooth);

        IReadOnlyList<BatteryTarget> targets = Select([Endpoint(Bluetooth), Endpoint(Usb)], known);

        Assert.Equal([known.Id.ToString("B"), Usb], targets.Select(target => target.Key));
        Assert.All(targets, target => Assert.NotNull(target.Name));
    }
}
