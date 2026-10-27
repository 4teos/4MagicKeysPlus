using Microsoft.Win32;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlusTests;

public sealed class DefaultsEditorTests {
    private readonly Guid profileId = Guid.NewGuid();
    private readonly FakeDefaultSettings defaults = new();
    private readonly FakeDriver driver = new();
    private readonly FakeFnLockStore fnLockStore = new();
    private readonly KeyRule oldRule = DriverRuleEncoder.CreateRule(PhysicalKey.F13, PhysicalKey.PrintScreen, false, false)!;
    private readonly KeyRule defaultRule = DriverRuleEncoder.CreateRule(PhysicalKey.F14, PhysicalKey.ScrollLock, false, false)!;
    private readonly KeyRemapEditor rules;
    private int reads;

    public DefaultsEditorTests() {
        rules = new KeyRemapEditor([oldRule], new FakeStore(), driver, fnLockStore);
    }

    private DefaultsEditor Editor(KeyboardProduct product = KeyboardProduct.A1843) {
        return new DefaultsEditor(profileId, product, defaults, rules, () => {
            reads++;
            return [defaultRule];
        }, driver);
    }

    [Fact]
    public void AReset_CopiesTheDefaultsOfTheModelToTheProfile() {
        Editor(KeyboardProduct.A1644).Reset();

        Assert.Equal([(profileId, KeyboardProduct.A1644)], defaults.Copies);
    }

    [Fact]
    public void AfterAReset_TheRuleEditorHasWhatTheRegistryHasNow() {
        Editor().Reset();

        Assert.Equal([defaultRule], rules.Rules);
    }

    [Fact]
    public void AReset_IsHandedToTheDriver() {
        ApplyResult result = Editor().Reset();

        Assert.Equal(ApplyOutcome.Applied, result.Outcome);
        Assert.Equal(1, driver.Reloads);
    }

    [Fact]
    public void AReset_TellsThatTheKeyboardChanged() {
        int changes = 0;
        DefaultsEditor editor = Editor();
        editor.Changed += () => changes++;

        editor.Reset();

        Assert.Equal(1, changes);
    }

    [Fact]
    public void WhenTheDriverDoesNotTakeThem_TheResetIsSavedAndReportedAsNotApplied() {
        driver.Result = null;

        ApplyResult result = Editor().Reset();

        Assert.Equal(ApplyOutcome.Warning, result.Outcome);
        Assert.Equal([defaultRule], rules.Rules);
    }

    [Fact]
    public void AModelWithoutDefaults_HasNothingToResetTo() {
        defaults.Available = false;
        DefaultsEditor editor = Editor();
        int changes = 0;
        editor.Changed += () => changes++;

        ApplyResult result = editor.Reset();

        Assert.False(editor.IsAvailable);
        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.Equal(Strings.ResetDefaultsUnavailable, result.Message);
        Assert.Equal(0, driver.Reloads);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void AModelWithDefaults_CanBeReset() {
        Assert.True(Editor().IsAvailable);
    }

    [Fact]
    public void AFailedCopy_IsAFailureAndTheDriverIsNotTold() {
        defaults.Failure = new IOException("disk");
        DefaultsEditor editor = Editor();
        int changes = 0;
        editor.Changed += () => changes++;

        ApplyResult result = editor.Reset();

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.Contains("disk", result.Message);
        Assert.Equal(0, driver.Reloads);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void AFailedCopy_StillLeavesTheRuleEditorShowingTheRegistry() {
        defaults.Failure = new UnauthorizedAccessException("denied");

        Editor().Reset();

        Assert.Equal(1, reads);
        Assert.Equal([defaultRule], rules.Rules);
    }
}

[Collection(TestCollections.Registry)]
public sealed class DefaultsEditorRegistryTests : IDisposable {
    private readonly ScratchKey scratch = new();
    private readonly string keyboardsPath;
    private readonly RegistryDefaultSettings defaults;
    private readonly RegistryKeyboardProfileStore profiles;
    private readonly KeyboardProfile profile = new(Guid.NewGuid(), KeyboardProduct.A1843, [KeyboardIdentity.Parse("bt:C4141104498E")!]);
    private readonly string profilePath;
    private readonly string rulesPath;
    private readonly RegistryFnLockStore fnLockStore;
    private readonly RegistryRulesStore rulesStore;

    public DefaultsEditorRegistryTests() {
        keyboardsPath = scratch.Path("Keyboards");
        defaults = new RegistryDefaultSettings(Registry.CurrentUser, scratch.Path("Defaults"), keyboardsPath);
        profiles = new RegistryKeyboardProfileStore(Registry.CurrentUser, keyboardsPath);
        profiles.Save(profile);
        profilePath = $@"{keyboardsPath}\{RegistryKeyboardProfileStore.ProfileKeyName(profile.Id)}";
        rulesPath = $@"{profilePath}\Rules";
        fnLockStore = new RegistryFnLockStore(Registry.CurrentUser, profilePath);
        rulesStore = new RegistryRulesStore(Registry.CurrentUser, rulesPath);
    }

    public void Dispose() {
        scratch.Dispose();
    }

    [Fact]
    public void AResetProfile_HasTheDefaultSettingsOfItsModelAndNothingElse() {
        defaults.Write(new DefaultSettings(
            KeyboardProduct.A1843,
            false,
            [new DefaultRule(0x00000069, 0x00000047, false, "F14 -> Scroll Lock"), new DefaultRule(0x01000041, 0x010000CD, true, "Fn+F8 -> Play/Pause")]));
        fnLockStore.Write(true);
        IReadOnlyList<KeyRule> mine = rulesStore.Save([], [DriverRuleEncoder.CreateRule(PhysicalKey.F13, PhysicalKey.PrintScreen, false, false)!]);
        KeyRemapEditor rules = new(mine, rulesStore, new FakeDriver(), fnLockStore);
        DefaultsEditor editor = new(profile.Id, KeyboardProduct.A1843, defaults, rules, () => DriverStatus.ReadRules(Registry.CurrentUser, rulesPath), new FakeDriver());

        ApplyResult result = editor.Reset();

        Assert.Equal(ApplyOutcome.Applied, result.Outcome);
        Assert.False(fnLockStore.Read());
        Assert.Equal([0x00000069u, 0x01000041u], rules.Rules.Select(rule => rule.RawSource).Order());
        Assert.DoesNotContain(rules.Rules, rule => rule.Source.Contains(PhysicalKey.F13));
        Assert.True(rules.Rules.Single(rule => rule.RawSource == 0x01000041u).FnLockAware);
        KeyboardProfile kept = Assert.Single(profiles.Load());
        Assert.Equal(profile.Identities, kept.Identities);
    }

    [Fact]
    public void ARuleSavedAfterAReset_ReplacesTheDefaultOneOfTheSameKey() {
        defaults.Write(new DefaultSettings(KeyboardProduct.A1843, false, [new DefaultRule(0x00000068, 0x00000046, false, "F13 -> Print Screen")]));
        KeyRemapEditor rules = new([], rulesStore, new FakeDriver(), fnLockStore);
        DefaultsEditor editor = new(profile.Id, KeyboardProduct.A1843, defaults, rules, () => DriverStatus.ReadRules(Registry.CurrentUser, rulesPath), new FakeDriver());
        editor.Reset();

        rules.SetMapping(PhysicalKey.F13, PhysicalKey.ScrollLock, fnLayer: false, fnLockAware: false);

        using RegistryKey key = Registry.CurrentUser.OpenSubKey($@"{rulesPath}\00000068")!;
        Assert.Equal(0x00000047, key.GetValue("Target"));
        Assert.Equal("F13 -> Scroll Lock", key.GetValue("Comment"));
    }
}
