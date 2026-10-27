using Microsoft.Win32;
using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlusTests;

[Collection(TestCollections.Registry)]
public sealed class RegistryDefaultSettingsTests : IDisposable {
    private readonly ScratchKey scratch = new();
    private readonly string defaultsPath;
    private readonly string keyboardsPath;
    private readonly RegistryDefaultSettings defaults;
    private readonly RegistryKeyboardProfileStore profiles;

    public RegistryDefaultSettingsTests() {
        defaultsPath = scratch.Path("Defaults");
        keyboardsPath = scratch.Path("Keyboards");
        defaults = new RegistryDefaultSettings(Registry.CurrentUser, defaultsPath, keyboardsPath);
        profiles = new RegistryKeyboardProfileStore(Registry.CurrentUser, keyboardsPath);
    }

    public void Dispose() {
        scratch.Dispose();
    }

    private static DefaultSettings A1843() {
        return new DefaultSettings(
            KeyboardProduct.A1843,
            true,
            [
                new DefaultRule(0x00000068, 0x00000046, false, "F13 -> PrintScreen"),
                new DefaultRule(0x0100003A, 0x01000070, true, "Fn+F1 -> Brightness Down"),
                new DefaultRule(0x000000F1, 0x00000049, false, null),
            ]);
    }

    private RegistryKey? Open(string path) {
        return Registry.CurrentUser.OpenSubKey(path);
    }

    private KeyboardProfile NewProfile() {
        KeyboardProfile profile = new(Guid.NewGuid(), KeyboardProduct.A1843, [KeyboardIdentity.Parse("bt:C4141104498E")!]);
        profiles.Save(profile);
        return profile;
    }

    private string ProfilePath(KeyboardProfile profile) {
        return $@"{keyboardsPath}\{RegistryKeyboardProfileStore.ProfileKeyName(profile.Id)}";
    }

    [Fact]
    public void TheDefaultsOfAModel_AreKeyedByItsUsbProductId() {
        Assert.Equal("026C", RegistryDefaultSettings.ModelKeyName(KeyboardProduct.A1843));
        Assert.Equal("0267", RegistryDefaultSettings.ModelKeyName(KeyboardProduct.A1644));
    }

    [Fact]
    public void WrittenDefaults_ReadBackTheSame() {
        defaults.Write(A1843());

        DefaultSettings? read = defaults.Read(KeyboardProduct.A1843);

        Assert.NotNull(read);
        Assert.Equal(KeyboardProduct.A1843, read.Model);
        Assert.True(read.FnLock);
        Assert.Equal(A1843().Rules.OrderBy(rule => rule.Source), read.Rules);
    }

    [Fact]
    public void TheValuesAreStoredTheWayTheDriverReadsThem() {
        defaults.Write(A1843());

        using RegistryKey model = Open($@"{defaultsPath}\026C")!;
        Assert.Equal(1, model.GetValue("FnLock"));
        Assert.Equal(RegistryValueKind.DWord, model.GetValueKind("FnLock"));
        using RegistryKey rules = model.OpenSubKey("Rules")!;
        Assert.Equal(["00000068", "000000F1", "0100003A"], rules.GetSubKeyNames().OrderBy(name => name, StringComparer.Ordinal));

        using RegistryKey fnRule = rules.OpenSubKey("0100003A")!;
        Assert.Equal(0x0100003A, fnRule.GetValue("Source"));
        Assert.Equal(0x01000070, fnRule.GetValue("Target"));
        Assert.Equal(RegistryValueKind.DWord, fnRule.GetValueKind("Source"));
        Assert.Equal(1, fnRule.GetValue("FnLockAware"));
        Assert.Equal("Fn+F1 -> Brightness Down", fnRule.GetValue("Comment"));

        using RegistryKey plainRule = rules.OpenSubKey("000000F1")!;
        Assert.Null(plainRule.GetValue("FnLockAware"));
        Assert.Null(plainRule.GetValue("Comment"));
    }

    [Fact]
    public void ARuleWithAHighBitIsStoredAsTheDwordTheDriverReads() {
        defaults.Write(new DefaultSettings(KeyboardProduct.A1843, false, [new DefaultRule(0xFF000001, 0x80000000, false, null)]));

        DefaultSettings read = defaults.Read(KeyboardProduct.A1843)!;

        Assert.Equal(new DefaultRule(0xFF000001, 0x80000000, false, null), Assert.Single(read.Rules));
    }

    [Fact]
    public void WritingAgain_ReplacesWhatTheModelHad() {
        defaults.Write(A1843());
        using (RegistryKey stray = Registry.CurrentUser.CreateSubKey($@"{defaultsPath}\026C\Rules\Old", writable: true)) {
            stray.SetValue("Source", 1);
            stray.SetValue("Target", 2);
        }

        defaults.Write(new DefaultSettings(KeyboardProduct.A1843, false, [new DefaultRule(0x68, 0x46, false, null)]));

        DefaultSettings read = defaults.Read(KeyboardProduct.A1843)!;
        Assert.False(read.FnLock);
        Assert.Equal([new DefaultRule(0x68, 0x46, false, null)], read.Rules);
    }

    [Fact]
    public void WritingOneModel_LeavesTheOthers() {
        defaults.Write(A1843());

        defaults.Write(new DefaultSettings(KeyboardProduct.A1644, false, []));

        Assert.Equal(3, defaults.Read(KeyboardProduct.A1843)!.Rules.Count);
        Assert.Empty(defaults.Read(KeyboardProduct.A1644)!.Rules);
    }

    [Fact]
    public void OnlyTheModelsThatWereWritten_HaveDefaults() {
        defaults.Write(A1843());

        Assert.True(defaults.Has(KeyboardProduct.A1843));
        Assert.False(defaults.Has(KeyboardProduct.A1644));
    }

    [Fact]
    public void AModelWithoutDefaults_ReadsAsNull() {
        defaults.Write(A1843());

        Assert.Null(defaults.Read(KeyboardProduct.A1644));
    }

    [Fact]
    public void ASubkeyWithoutBothSourceAndTarget_IsNotARule() {
        defaults.Write(A1843());
        using (RegistryKey launchOnly = Registry.CurrentUser.CreateSubKey($@"{defaultsPath}\026C\Rules\Odd", writable: true)) {
            launchOnly.SetValue("Source", 5);
        }

        Assert.Equal(3, defaults.Read(KeyboardProduct.A1843)!.Rules.Count);
    }

    [Fact]
    public void ACopyGivesTheProfileTheFnLockAndTheRules() {
        defaults.Write(A1843());
        KeyboardProfile profile = NewProfile();

        bool copied = defaults.CopyToProfile(profile.Id, KeyboardProduct.A1843);

        Assert.True(copied);
        using RegistryKey key = Open(ProfilePath(profile))!;
        Assert.Equal(1, key.GetValue("FnLock"));
        using RegistryKey rules = key.OpenSubKey("Rules")!;
        Assert.Equal(3, rules.SubKeyCount);
        using RegistryKey rule = rules.OpenSubKey("00000068")!;
        Assert.Equal(0x68, rule.GetValue("Source"));
        Assert.Equal(0x46, rule.GetValue("Target"));
        Assert.Equal("F13 -> PrintScreen", rule.GetValue("Comment"));
    }

    [Fact]
    public void ACopy_KeepsTheValuesThatMakeTheProfile() {
        defaults.Write(A1843());
        KeyboardProfile profile = NewProfile();

        defaults.CopyToProfile(profile.Id, KeyboardProduct.A1843);

        KeyboardProfile loaded = Assert.Single(profiles.Load());
        Assert.Equal(profile.Id, loaded.Id);
        Assert.Equal(profile.Identities, loaded.Identities);
        Assert.Equal(KeyboardProduct.A1843, loaded.Product);
    }

    [Fact]
    public void ACopy_ReplacesTheRulesTheProfileHad() {
        defaults.Write(A1843());
        KeyboardProfile profile = NewProfile();
        using (RegistryKey old = Registry.CurrentUser.CreateSubKey($@"{ProfilePath(profile)}\Rules\00000004", writable: true)) {
            old.SetValue("Source", 4);
            old.SetValue("Target", 5);
        }

        defaults.CopyToProfile(profile.Id, KeyboardProduct.A1843);

        using RegistryKey rules = Open($@"{ProfilePath(profile)}\Rules")!;
        Assert.DoesNotContain("00000004", rules.GetSubKeyNames());
        Assert.Equal(3, rules.SubKeyCount);
    }

    [Fact]
    public void ACopyIsNotALink_ChangingTheDefaultsLeavesTheProfile() {
        defaults.Write(A1843());
        KeyboardProfile profile = NewProfile();
        defaults.CopyToProfile(profile.Id, KeyboardProduct.A1843);

        defaults.Write(new DefaultSettings(KeyboardProduct.A1843, false, []));

        using RegistryKey key = Open(ProfilePath(profile))!;
        Assert.Equal(1, key.GetValue("FnLock"));
        using RegistryKey rules = key.OpenSubKey("Rules")!;
        Assert.Equal(3, rules.SubKeyCount);
    }

    [Fact]
    public void WithoutDefaultsOfTheModel_TheProfileIsLeftAsItIs() {
        defaults.Write(new DefaultSettings(KeyboardProduct.A1644, true, []));
        KeyboardProfile profile = NewProfile();

        bool copied = defaults.CopyToProfile(profile.Id, KeyboardProduct.A1843);

        Assert.False(copied);
        using RegistryKey key = Open(ProfilePath(profile))!;
        Assert.Null(key.GetValue("FnLock"));
        Assert.Null(key.OpenSubKey("Rules"));
    }

    [Fact]
    public void ACopyToAProfileThatIsNotThere_IsRefusedAndMakesNothing() {
        defaults.Write(A1843());
        Guid missing = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => defaults.CopyToProfile(missing, KeyboardProduct.A1843));

        Assert.Null(Open($@"{keyboardsPath}\{RegistryKeyboardProfileStore.ProfileKeyName(missing)}"));
    }
}
