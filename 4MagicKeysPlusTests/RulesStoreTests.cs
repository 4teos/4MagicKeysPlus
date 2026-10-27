using System.ComponentModel;
using Microsoft.Win32;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlusTests;

internal sealed class FakeStore : IRulesStore {
    public Exception? Failure {
        get; set;
    }
    public int Saves {
        get; private set;
    }

    public IReadOnlyList<KeyRule> Save(IReadOnlyList<KeyRule> saved, IReadOnlyList<KeyRule> wanted) {
        if (Failure is not null) {
            throw Failure;
        }
        Saves++;
        return RulesPlan.Create(saved, wanted).Result;
    }
}

internal sealed class FakeDriver : IDriverControl {
    public DriverReloadResult? Result { get; set; } = new(25, 25, 1);
    public Exception? Failure {
        get; set;
    }
    public int Reloads {
        get; private set;
    }

    public DriverReloadResult? Reload() {
        Reloads++;
        return Failure is not null ? throw Failure : Result;
    }
}

public class RulesPlanTests {
    private static KeyRule Rule(PhysicalKey source, RemapTarget target, bool fnLayer = false, string? name = null) {
        return DriverRuleEncoder.CreateRule(source, target, fnLayer, fnLockAware: false)! with {
            RegistryName = name
        };
    }

    [Fact]
    public void ANewRule_IsWrittenUnderTheHexOfItsSource() {
        RulesPlan plan = RulesPlan.Create([], [Rule(PhysicalKey.Left, PhysicalKey.Home, fnLayer: true)]);

        RuleWrite write = Assert.Single(plan.Write);
        Assert.Equal(("01000050", 0x01000050u, 0x0000004Au), (write.Name, write.Source, write.Target));
        Assert.Empty(plan.Delete);
        Assert.Equal("01000050", Assert.Single(plan.Result).RegistryName);
    }

    [Fact]
    public void AnUnchangedRule_IsNotWrittenAgain() {
        KeyRule saved = Rule(PhysicalKey.F13, PhysicalKey.PrintScreen, name: "00000068");

        RulesPlan plan = RulesPlan.Create([saved], [saved]);

        Assert.Empty(plan.Write);
        Assert.Empty(plan.Delete);
    }

    [Fact]
    public void AReplacedRule_DeletesTheOldSubkeyWhateverItIsCalled() {
        KeyRule old = Rule(PhysicalKey.F13, PhysicalKey.PrintScreen, name: "My F13 rule");

        RulesPlan plan = RulesPlan.Create([old], [Rule(PhysicalKey.F13, PhysicalKey.ScrollLock)]);

        Assert.Equal(["My F13 rule"], plan.Delete);
        Assert.Equal("00000068", Assert.Single(plan.Write).Name);
    }

    [Fact]
    public void AReplacedRule_KeepsTheSubkeyWhenTheNewOneTakesItsName() {
        KeyRule old = Rule(PhysicalKey.F13, PhysicalKey.PrintScreen, name: "00000068");

        RulesPlan plan = RulesPlan.Create([old], [Rule(PhysicalKey.F13, PhysicalKey.ScrollLock)]);

        Assert.Empty(plan.Delete);
        Assert.Equal("00000068", Assert.Single(plan.Write).Name);
    }

    [Fact]
    public void ARemovedRule_IsDeleted() {
        KeyRule kept = Rule(PhysicalKey.F14, PhysicalKey.ScrollLock, name: "00000069");
        KeyRule removed = Rule(PhysicalKey.F13, PhysicalKey.PrintScreen, name: "00000068");

        RulesPlan plan = RulesPlan.Create([kept, removed], [kept]);

        Assert.Equal(["00000068"], plan.Delete);
        Assert.Empty(plan.Write);
    }
}

[Collection(TestCollections.Registry)]
public sealed class RegistryRulesStoreTests : IDisposable {
    private const string ScratchRoot = @"Software\4MagicKeysPlusTests";
    private readonly string rulesPath = $@"{ScratchRoot}\{Guid.NewGuid():N}\Rules";
    private readonly RegistryRulesStore store;

    public RegistryRulesStoreTests() {
        store = new RegistryRulesStore(Registry.CurrentUser, rulesPath);
    }

    public void Dispose() {
        Registry.CurrentUser.DeleteSubKeyTree(rulesPath[..rulesPath.LastIndexOf('\\')], throwOnMissingSubKey: false);
        using (RegistryKey? root = Registry.CurrentUser.OpenSubKey(ScratchRoot)) {
            if (root?.SubKeyCount == 0) {
                Registry.CurrentUser.DeleteSubKey(ScratchRoot, throwOnMissingSubKey: false);
            }
        }
    }

    private static KeyRule Rule(PhysicalKey source, RemapTarget target, bool fnLayer = false, bool fnLockAware = false) {
        return DriverRuleEncoder.CreateRule(source, target, fnLayer, fnLockAware)!;
    }

    private RegistryKey Open(string name) {
        return Registry.CurrentUser.OpenSubKey($@"{rulesPath}\{name}", writable: true) ?? throw new InvalidOperationException($"No subkey {name}");
    }

    [Fact]
    public void Save_WritesTheRuleAsDwordValues() {
        IReadOnlyList<KeyRule> saved = store.Save([], [Rule(PhysicalKey.F8, RemapTarget.Media(0xCD), fnLayer: true, fnLockAware: true)]);

        using RegistryKey key = Open("01000041");
        Assert.Equal(0x01000041, key.GetValue("Source"));
        Assert.Equal(unchecked((int)0x010000CD), key.GetValue("Target"));
        Assert.Equal(1, key.GetValue("FnLockAware"));
        Assert.Equal("01000041", Assert.Single(saved).RegistryName);
    }

    [Fact]
    public void Save_ReplacingARule_DropsTheOldFnLockAwareValue() {
        IReadOnlyList<KeyRule> first = store.Save([], [Rule(PhysicalKey.F8, RemapTarget.Media(0xCD), fnLayer: true, fnLockAware: true)]);

        store.Save(first, [Rule(PhysicalKey.F8, PhysicalKey.F20, fnLayer: true)]);

        using RegistryKey key = Open("01000041");
        Assert.Equal(0x01000041, key.GetValue("Source"));
        Assert.Equal(0x0000006F, key.GetValue("Target"));
        Assert.Null(key.GetValue("FnLockAware"));
    }

    [Fact]
    public void Save_RemovingARule_DeletesItsSubkey() {
        IReadOnlyList<KeyRule> first = store.Save([], [Rule(PhysicalKey.F13, PhysicalKey.PrintScreen)]);

        store.Save(first, []);

        using RegistryKey rulesKey = Registry.CurrentUser.OpenSubKey(rulesPath)!;
        Assert.Empty(rulesKey.GetSubKeyNames());
    }

    [Fact]
    public void WhatIsSaved_ReadsBackAsTheSameRules() {
        List<KeyRule> wanted = [
            Rule(PhysicalKey.LeftOption, PhysicalKey.LeftCommand),
            Rule(PhysicalKey.Left, PhysicalKey.Home, fnLayer: true),
            Rule(PhysicalKey.F8, RemapTarget.Media(0xCD), fnLayer: true, fnLockAware: true),
        ];

        store.Save([], wanted);

        using RegistryKey rulesKey = Registry.CurrentUser.OpenSubKey(rulesPath)!;
        List<(uint, uint, bool)> read = [.. rulesKey.GetSubKeyNames().Select(name => {
            using RegistryKey key = rulesKey.OpenSubKey(name)!;
            return (unchecked((uint)(int)key.GetValue("Source")!), unchecked((uint)(int)key.GetValue("Target")!), key.GetValue("FnLockAware") is 1);
        }).Order()];
        List<(uint, uint, bool)> expected = [.. wanted.Select(rule => (rule.RawSource, rule.RawTarget, rule.FnLockAware)).Order()];
        Assert.Equal(expected, read);
    }
}

public class KeyRemapEditorApplyTests {
    private static KeyRemapEditor Editor(FakeStore store, FakeDriver driver) {
        return new KeyRemapEditor([], store, driver, new FakeFnLockStore());
    }

    [Fact]
    public void ASavedChange_IsHandedToTheDriver() {
        FakeStore store = new();
        FakeDriver driver = new() {
            Result = new DriverReloadResult(1, 1, 2)
        };
        KeyRemapEditor editor = Editor(store, driver);

        ApplyResult result = editor.SetMapping(PhysicalKey.F13, PhysicalKey.PrintScreen, fnLayer: false, fnLockAware: false);

        Assert.Equal(ApplyOutcome.Applied, result.Outcome);
        Assert.Equal((1, 1), (store.Saves, driver.Reloads));
        Assert.Single(editor.Rules);
    }

    [Fact]
    public void WithoutAControlDevice_TheChangeIsSavedAndReportedAsNotApplied() {
        KeyRemapEditor editor = Editor(new FakeStore(), new FakeDriver { Result = null });

        ApplyResult result = editor.SetMapping(PhysicalKey.F13, PhysicalKey.PrintScreen, fnLayer: false, fnLockAware: false);

        Assert.Equal(ApplyOutcome.Warning, result.Outcome);
        Assert.Single(editor.Rules);
    }

    [Fact]
    public void ADriverError_StillKeepsTheSavedChange() {
        KeyRemapEditor editor = Editor(new FakeStore(), new FakeDriver { Failure = new Win32Exception(5) });

        ApplyResult result = editor.SetMapping(PhysicalKey.F13, PhysicalKey.PrintScreen, fnLayer: false, fnLockAware: false);

        Assert.Equal(ApplyOutcome.Warning, result.Outcome);
        Assert.Single(editor.Rules);
    }

    [Fact]
    public void AFailedSave_ChangesNothingAndDoesNotTouchTheDriver() {
        FakeDriver driver = new();
        KeyRemapEditor editor = Editor(new FakeStore { Failure = new UnauthorizedAccessException("denied") }, driver);

        ApplyResult result = editor.SetMapping(PhysicalKey.F13, PhysicalKey.PrintScreen, fnLayer: false, fnLockAware: false);

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.Empty(editor.Rules);
        Assert.Equal(0, driver.Reloads);
    }

    [Fact]
    public void RemovingAMapping_IsSavedAndAppliedToo() {
        FakeStore store = new();
        FakeDriver driver = new();
        KeyRemapEditor editor = Editor(store, driver);
        editor.SetMapping(PhysicalKey.F13, PhysicalKey.PrintScreen, fnLayer: false, fnLockAware: false);

        ApplyResult result = editor.RemoveMappings(PhysicalKey.F13);

        Assert.Equal(ApplyOutcome.Applied, result.Outcome);
        Assert.Empty(editor.Rules);
        Assert.Equal((2, 2), (store.Saves, driver.Reloads));
    }
}
