using Microsoft.Win32;
using _4MagicKeysPlus.Core;

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

    private static KeyboardProfile Profile(params string[] identities) {
        return new KeyboardProfile(Guid.NewGuid(), KeyboardProduct.A1843, [.. identities.Select(text => KeyboardIdentity.Parse(text)!)]);
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
        KeyboardProfile profile = Profile("bt:C4141104498E", "usb:F0T125100BWJKPGAS");

        store.Save(profile);

        KeyboardProfile loaded = Assert.Single(store.Load());
        Assert.Equal(profile.Id, loaded.Id);
        Assert.Equal(KeyboardProduct.A1843, loaded.Product);
        Assert.Equal(profile.Identities, loaded.Identities);
    }

    [Fact]
    public void TheProfileIsStoredUnderItsGuidInBraces() {
        KeyboardProfile profile = Profile();

        store.Save(profile);

        Assert.Equal([profile.Id.ToString("B").ToUpperInvariant()], SubKeyNames());
    }

    [Fact]
    public void TheValuesAreStoredTheWayTheDriverReadsThem() {
        KeyboardProfile profile = Profile("bt:C4141104498E", "usb:F0T125100BWJKPGAS");

        store.Save(profile);

        using RegistryKey key = Registry.CurrentUser.OpenSubKey($@"{keyboardsPath}\{RegistryKeyboardProfileStore.ProfileKeyName(profile.Id)}")!;
        Assert.Null(key.GetValue("Name"));
        Assert.Equal(0x026C, key.GetValue("Model"));
        Assert.Equal(RegistryValueKind.MultiString, key.GetValueKind("Identities"));
        Assert.Equal(["bt:C4141104498E", "usb:F0T125100BWJKPGAS"], (string[])key.GetValue("Identities")!);
    }

    [Fact]
    public void Saving_LeavesTheSettingsOfTheProfileAlone() {
        KeyboardProfile profile = Profile("bt:C4141104498E");
        using (RegistryKey key = RawProfile(RegistryKeyboardProfileStore.ProfileKeyName(profile.Id))) {
            key.SetValue("FnLock", 1, RegistryValueKind.DWord);
            using RegistryKey rule = key.CreateSubKey(@"Rules\01000041");
            rule.SetValue("Source", 0x01000041, RegistryValueKind.DWord);
        }

        store.Save(profile);

        using RegistryKey saved = Registry.CurrentUser.OpenSubKey($@"{keyboardsPath}\{RegistryKeyboardProfileStore.ProfileKeyName(profile.Id)}")!;
        Assert.Equal(1, saved.GetValue("FnLock"));
        using RegistryKey rules = saved.OpenSubKey("Rules")!;
        Assert.Equal(["01000041"], rules.GetSubKeyNames());
    }

    [Fact]
    public void SavingAgain_ReplacesTheIndexAndTheIdentities() {
        KeyboardProfile profile = Profile("bt:C4141104498E");
        store.Save(profile);

        store.Save(profile with {
            Index = 2,
            Identities = [KeyboardIdentity.Parse("usb:F0T125100BWJKPGAS")!]
        });

        KeyboardProfile loaded = Assert.Single(store.Load());
        Assert.Equal(2, loaded.Index);
        Assert.Equal(["usb:F0T125100BWJKPGAS"], loaded.Identities.Select(identity => identity.ToString()));
    }

    [Fact]
    public void AnIdentityOfAnotherProfile_IsRefused() {
        store.Save(Profile("bt:C4141104498E"));
        KeyboardProfile other = Profile("usb:F0T125100BWJKPGAS", "bt:C4141104498E");

        Assert.Throws<InvalidOperationException>(() => store.Save(other));

        Assert.Single(store.Load());
    }

    [Fact]
    public void AProfile_MayBeSavedAgainWithItsOwnIdentities() {
        KeyboardProfile profile = Profile("bt:C4141104498E");
        store.Save(profile);

        store.Save(profile with {
            Index = 4
        });

        Assert.Equal(4, Assert.Single(store.Load()).Index);
    }

    [Fact]
    public void ANameByHand_IsLeftAlone_AndNeverWritten() {
        KeyboardProfile profile = Profile("bt:C4141104498E");
        using (RegistryKey key = RawProfile(RegistryKeyboardProfileStore.ProfileKeyName(profile.Id))) {
            key.SetValue("Name", "Desk");
        }

        store.Save(profile);

        using RegistryKey saved = Registry.CurrentUser.OpenSubKey($@"{keyboardsPath}\{RegistryKeyboardProfileStore.ProfileKeyName(profile.Id)}")!;
        Assert.Equal("Desk", saved.GetValue("Name"));
        Assert.Single(store.Load());
    }

    [Fact]
    public void AnIndexAboveOne_IsKeptAsADword_AndReadBack() {
        KeyboardProfile profile = Profile("bt:C4141104498E") with {
            Index = 3
        };

        store.Save(profile);

        using RegistryKey key = Registry.CurrentUser.OpenSubKey($@"{keyboardsPath}\{RegistryKeyboardProfileStore.ProfileKeyName(profile.Id)}")!;
        Assert.Equal(3, key.GetValue("Index"));
        Assert.Equal(RegistryValueKind.DWord, key.GetValueKind("Index"));
        Assert.Equal(3, Assert.Single(store.Load()).Index);
    }

    [Fact]
    public void TheIndexOne_IsNotWritten_AndAnIndexThatGoesBackToOneIsDeleted() {
        KeyboardProfile profile = Profile("bt:C4141104498E");
        store.Save(profile);
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey($@"{keyboardsPath}\{RegistryKeyboardProfileStore.ProfileKeyName(profile.Id)}")!) {
            Assert.Null(key.GetValue("Index"));
        }

        store.Save(profile with {
            Index = 2
        });
        store.Save(profile with {
            Index = 1
        });

        using RegistryKey saved = Registry.CurrentUser.OpenSubKey($@"{keyboardsPath}\{RegistryKeyboardProfileStore.ProfileKeyName(profile.Id)}")!;
        Assert.Null(saved.GetValue("Index"));
        Assert.Equal(1, Assert.Single(store.Load()).Index);
    }

    [Theory]
    [InlineData(-4)]
    [InlineData(0)]
    [InlineData(1)]
    public void Load_TakesAnIndexBelowTwoAsOne(int stored) {
        using (RegistryKey key = RawProfile(RegistryKeyboardProfileStore.ProfileKeyName(Guid.NewGuid()))) {
            key.SetValue("Model", 0x026C, RegistryValueKind.DWord);
            key.SetValue("Index", stored, RegistryValueKind.DWord);
        }

        Assert.Equal(1, Assert.Single(store.Load()).Index);
    }

    [Fact]
    public void Load_TakesAnIndexThatIsNoDwordAsOne() {
        using (RegistryKey key = RawProfile(RegistryKeyboardProfileStore.ProfileKeyName(Guid.NewGuid()))) {
            key.SetValue("Model", 0x026C, RegistryValueKind.DWord);
            key.SetValue("Index", "3", RegistryValueKind.String);
        }

        Assert.Equal(1, Assert.Single(store.Load()).Index);
    }

    [Fact]
    public void Load_ListsTheProfilesOfAModelByTheirIndex() {
        store.Save(Profile("bt:111111111111") with {
            Index = 3
        });
        store.Save(Profile("bt:222222222222"));
        store.Save(Profile("bt:333333333333") with {
            Index = 2
        });

        Assert.Equal([1, 2, 3], store.Load().Select(profile => profile.Index));
    }

    [Fact]
    public void Load_SkipsWhatIsNotAProfile() {
        using (RegistryKey notAGuid = RawProfile("NotAGuid")) {
            notAGuid.SetValue("Model", 0x026C, RegistryValueKind.DWord);
        }
        using (RegistryKey noModel = RawProfile(RegistryKeyboardProfileStore.ProfileKeyName(Guid.NewGuid()))) {
            noModel.SetValue("Index", 2, RegistryValueKind.DWord);
        }
        using (RegistryKey unknownModel = RawProfile(RegistryKeyboardProfileStore.ProfileKeyName(Guid.NewGuid()))) {
            unknownModel.SetValue("Model", 0x9999, RegistryValueKind.DWord);
        }
        using (RegistryKey outOfRange = RawProfile(RegistryKeyboardProfileStore.ProfileKeyName(Guid.NewGuid()))) {
            outOfRange.SetValue("Model", -1, RegistryValueKind.DWord);
        }

        Assert.Empty(store.Load());
    }

    [Fact]
    public void Load_DropsIdentitiesThatDoNotParse() {
        Guid id = Guid.NewGuid();
        using (RegistryKey key = RawProfile(RegistryKeyboardProfileStore.ProfileKeyName(id))) {
            key.SetValue("Model", 0x0267, RegistryValueKind.DWord);
            key.SetValue("Identities", new[] { "bt:C4141104498E", "garbage", "usb:" }, RegistryValueKind.MultiString);
        }

        KeyboardProfile profile = Assert.Single(store.Load());

        Assert.Equal(KeyboardProduct.A1644, profile.Product);
        Assert.Equal(["bt:C4141104498E"], profile.Identities.Select(identity => identity.ToString()));
    }

    [Fact]
    public void Load_WithoutIdentities_GivesNoIdentities() {
        using (RegistryKey key = RawProfile(RegistryKeyboardProfileStore.ProfileKeyName(Guid.NewGuid()))) {
            key.SetValue("Model", 0x026C, RegistryValueKind.DWord);
        }

        KeyboardProfile profile = Assert.Single(store.Load());

        Assert.Empty(profile.Identities);
    }

    [Fact]
    public void Load_ListsTheProfilesByModel() {
        store.Save(Profile("bt:111111111111"));
        store.Save(new KeyboardProfile(Guid.NewGuid(), KeyboardProduct.A1644, [KeyboardIdentity.Parse("bt:222222222222")!]));

        Assert.Equal([KeyboardProduct.A1644, KeyboardProduct.A1843], store.Load().Select(profile => profile.Product));
    }

    [Fact]
    public void AnIdentityOfASubkeyThatLoadDoesNotShow_IsRefusedAsWell() {
        using (RegistryKey noModel = RawProfile(RegistryKeyboardProfileStore.ProfileKeyName(Guid.NewGuid()))) {
            noModel.SetValue("Identities", new[] { "bt:C4141104498E" }, RegistryValueKind.MultiString);
        }
        using (RegistryKey notAGuid = RawProfile("ByHand")) {
            notAGuid.SetValue("Identities", new[] { "usb:F0T125100BWJKPGAS" }, RegistryValueKind.MultiString);
        }

        Assert.Throws<InvalidOperationException>(() => store.Save(Profile("bt:C4141104498E")));
        Assert.Throws<InvalidOperationException>(() => store.Save(Profile("usb:F0T125100BWJKPGAS")));
        Assert.Equal(2, SubKeyNames().Length);
    }

    [Fact]
    public void AProfile_OwnsItsIdentitiesOnly() {
        KeyboardProfile profile = Profile("bt:C4141104498E");

        Assert.True(profile.Owns(KeyboardIdentity.Parse("bt:C4141104498E")!));
        Assert.False(profile.Owns(KeyboardIdentity.Parse("usb:C4141104498E")!));
    }
}
