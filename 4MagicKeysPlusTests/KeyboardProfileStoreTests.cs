using Microsoft.Win32;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

[Collection(TestCollections.Registry)]
public sealed class RegistryKeyboardProfileStoreTests : IDisposable {
    private readonly ScratchKey scratch = new();
    private readonly string keyboardsPath;
    private readonly RegistryKeyboardProfileStore store;

    public RegistryKeyboardProfileStoreTests() {
        keyboardsPath = scratch.Path("Keyboards");
        store = new RegistryKeyboardProfileStore(Registry.CurrentUser, keyboardsPath);
    }

    public void Dispose() {
        scratch.Dispose();
    }

    private static KeyboardProfile Profile(string name, params string[] identities) {
        return new KeyboardProfile(Guid.NewGuid(), name, KeyboardProduct.A1843, [.. identities.Select(text => KeyboardIdentity.Parse(text)!)]);
    }

    private RegistryKey RawProfile(string name) {
        return Registry.CurrentUser.CreateSubKey($@"{keyboardsPath}\{name}", writable: true);
    }

    private string[] SubKeyNames() {
        using RegistryKey keyboardsKey = Registry.CurrentUser.OpenSubKey(keyboardsPath)!;
        return keyboardsKey.GetSubKeyNames();
    }

    [Fact]
    public void WithoutTheKey_ThereAreNoProfiles() {
        Assert.Empty(store.Load());
    }

    [Fact]
    public void ASavedProfile_ReadsBackTheSame() {
        KeyboardProfile profile = Profile("Desk", "bt:C4141104498E", "usb:F0T125100BWJKPGAS");

        store.Save(profile);

        KeyboardProfile loaded = Assert.Single(store.Load());
        Assert.Equal(profile.Id, loaded.Id);
        Assert.Equal("Desk", loaded.Name);
        Assert.Equal(KeyboardProduct.A1843, loaded.Product);
        Assert.Equal(profile.Identities, loaded.Identities);
    }

    [Fact]
    public void TheProfileIsStoredUnderItsGuidInBraces() {
        KeyboardProfile profile = Profile("Desk");

        store.Save(profile);

        Assert.Equal([profile.Id.ToString("B").ToUpperInvariant()], SubKeyNames());
    }

    [Fact]
    public void TheValuesAreStoredTheWayTheDriverReadsThem() {
        KeyboardProfile profile = Profile("Desk", "bt:C4141104498E", "usb:F0T125100BWJKPGAS");

        store.Save(profile);

        using RegistryKey key = Registry.CurrentUser.OpenSubKey($@"{keyboardsPath}\{DriverStatus.ProfileKeyName(profile.Id)}")!;
        Assert.Equal("Desk", key.GetValue("Name"));
        Assert.Equal(0x026C, key.GetValue("Model"));
        Assert.Equal(RegistryValueKind.MultiString, key.GetValueKind("Identities"));
        Assert.Equal(["bt:C4141104498E", "usb:F0T125100BWJKPGAS"], (string[])key.GetValue("Identities")!);
    }

    [Fact]
    public void Saving_LeavesTheSettingsOfTheProfileAlone() {
        KeyboardProfile profile = Profile("Desk", "bt:C4141104498E");
        using (RegistryKey key = RawProfile(DriverStatus.ProfileKeyName(profile.Id))) {
            key.SetValue("FnLock", 1, RegistryValueKind.DWord);
            using RegistryKey rule = key.CreateSubKey(@"Rules\01000041");
            rule.SetValue("Source", 0x01000041, RegistryValueKind.DWord);
        }

        store.Save(profile);

        using RegistryKey saved = Registry.CurrentUser.OpenSubKey($@"{keyboardsPath}\{DriverStatus.ProfileKeyName(profile.Id)}")!;
        Assert.Equal(1, saved.GetValue("FnLock"));
        using RegistryKey rules = saved.OpenSubKey("Rules")!;
        Assert.Equal(["01000041"], rules.GetSubKeyNames());
    }

    [Fact]
    public void SavingAgain_ReplacesTheNameAndTheIdentities() {
        KeyboardProfile profile = Profile("Desk", "bt:C4141104498E");
        store.Save(profile);

        store.Save(profile with {
            Name = "Office",
            Identities = [KeyboardIdentity.Parse("usb:F0T125100BWJKPGAS")!]
        });

        KeyboardProfile loaded = Assert.Single(store.Load());
        Assert.Equal("Office", loaded.Name);
        Assert.Equal(["usb:F0T125100BWJKPGAS"], loaded.Identities.Select(identity => identity.ToString()));
    }

    [Fact]
    public void AnIdentityOfAnotherProfile_IsRefused() {
        store.Save(Profile("Desk", "bt:C4141104498E"));
        KeyboardProfile other = Profile("Office", "usb:F0T125100BWJKPGAS", "bt:C4141104498E");

        Assert.Throws<InvalidOperationException>(() => store.Save(other));

        Assert.Single(store.Load());
    }

    [Fact]
    public void AProfile_MayBeSavedAgainWithItsOwnIdentities() {
        KeyboardProfile profile = Profile("Desk", "bt:C4141104498E");
        store.Save(profile);

        store.Save(profile with {
            Name = "Renamed"
        });

        Assert.Equal("Renamed", Assert.Single(store.Load()).Name);
    }

    [Fact]
    public void Load_SkipsWhatIsNotAProfile() {
        using (RegistryKey notAGuid = RawProfile("NotAGuid")) {
            notAGuid.SetValue("Model", 0x026C, RegistryValueKind.DWord);
        }
        using (RegistryKey noModel = RawProfile(DriverStatus.ProfileKeyName(Guid.NewGuid()))) {
            noModel.SetValue("Name", "No model");
        }
        using (RegistryKey unknownModel = RawProfile(DriverStatus.ProfileKeyName(Guid.NewGuid()))) {
            unknownModel.SetValue("Model", 0x9999, RegistryValueKind.DWord);
        }
        using (RegistryKey outOfRange = RawProfile(DriverStatus.ProfileKeyName(Guid.NewGuid()))) {
            outOfRange.SetValue("Model", -1, RegistryValueKind.DWord);
        }

        Assert.Empty(store.Load());
    }

    [Fact]
    public void Load_DropsIdentitiesThatDoNotParse() {
        Guid id = Guid.NewGuid();
        using (RegistryKey key = RawProfile(DriverStatus.ProfileKeyName(id))) {
            key.SetValue("Model", 0x0267, RegistryValueKind.DWord);
            key.SetValue("Identities", new[] { "bt:C4141104498E", "garbage", "usb:" }, RegistryValueKind.MultiString);
        }

        KeyboardProfile profile = Assert.Single(store.Load());

        Assert.Equal(KeyboardProduct.A1644, profile.Product);
        Assert.Equal(["bt:C4141104498E"], profile.Identities.Select(identity => identity.ToString()));
    }

    [Fact]
    public void Load_WithoutANameOrIdentities_GivesAnEmptyNameAndNoIdentities() {
        using (RegistryKey key = RawProfile(DriverStatus.ProfileKeyName(Guid.NewGuid()))) {
            key.SetValue("Model", 0x026C, RegistryValueKind.DWord);
        }

        KeyboardProfile profile = Assert.Single(store.Load());

        Assert.Equal("", profile.Name);
        Assert.Empty(profile.Identities);
    }

    [Fact]
    public void Load_ListsTheProfilesByName() {
        store.Save(Profile("beta"));
        store.Save(Profile("Alpha"));
        store.Save(Profile("Gamma"));

        Assert.Equal(["Alpha", "beta", "Gamma"], store.Load().Select(profile => profile.Name));
    }

    [Fact]
    public void AProfile_OwnsItsIdentitiesOnly() {
        KeyboardProfile profile = Profile("Desk", "bt:C4141104498E");

        Assert.True(profile.Owns(KeyboardIdentity.Parse("bt:C4141104498E")!));
        Assert.False(profile.Owns(KeyboardIdentity.Parse("usb:C4141104498E")!));
    }
}
