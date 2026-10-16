using System.ComponentModel;
using Microsoft.Win32;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlusTests;

internal sealed class FakeFnLockStore : IFnLockStore {
    public bool Enabled { get; set; }
    public Exception? Failure { get; set; }

    public bool Read() {
        return Enabled;
    }

    public void Write(bool enabled) {
        if (Failure is not null) {
            throw Failure;
        }
        Enabled = enabled;
    }
}

public class FnLockEditorTests {
    [Fact]
    public void ASavedChange_IsHandedToTheDriver() {
        FakeFnLockStore store = new();
        FakeDriver driver = new();
        FnLockEditor editor = new(store, driver);

        ApplyResult result = editor.Set(true);

        Assert.Equal(ApplyOutcome.Applied, result.Outcome);
        Assert.True(store.Enabled);
        Assert.True(editor.IsEnabled);
        Assert.Equal(1, driver.Reloads);
    }

    [Fact]
    public void TurningItOff_IsSavedToo() {
        FakeFnLockStore store = new() { Enabled = true };
        FnLockEditor editor = new(store, new FakeDriver());

        editor.Set(false);

        Assert.False(store.Enabled);
    }

    [Fact]
    public void Changed_IsRaisedOnceTheChangeIsSaved() {
        FakeFnLockStore store = new();
        FnLockEditor editor = new(store, new FakeDriver());
        bool enabledWhenRaised = false;
        editor.Changed += () => enabledWhenRaised = store.Enabled;

        editor.Set(true);

        Assert.True(enabledWhenRaised);
    }

    [Fact]
    public void AFailedSave_ChangesNothingAndDoesNotTouchTheDriver() {
        FakeDriver driver = new();
        FakeFnLockStore store = new() { Failure = new UnauthorizedAccessException("denied") };
        FnLockEditor editor = new(store, driver);
        int raised = 0;
        editor.Changed += () => raised++;

        ApplyResult result = editor.Set(true);

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.False(editor.IsEnabled);
        Assert.Equal(0, driver.Reloads);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void WithoutAControlDevice_TheChangeIsSavedAndReportedAsNotApplied() {
        FakeFnLockStore store = new();
        FnLockEditor editor = new(store, new FakeDriver { Result = null });

        ApplyResult result = editor.Set(true);

        Assert.Equal(ApplyOutcome.Warning, result.Outcome);
        Assert.True(store.Enabled);
    }

    [Fact]
    public void ADriverError_StillKeepsTheSavedChange() {
        FakeFnLockStore store = new();
        FnLockEditor editor = new(store, new FakeDriver { Failure = new Win32Exception(5) });

        ApplyResult result = editor.Set(true);

        Assert.Equal(ApplyOutcome.Warning, result.Outcome);
        Assert.True(store.Enabled);
    }
}

[Collection(TestCollections.Registry)]
public sealed class RegistryFnLockStoreTests : IDisposable {
    private const string ScratchRoot = @"Software\4MagicKeysPlusTests";
    private readonly string parametersPath = $@"{ScratchRoot}\{Guid.NewGuid():N}\Parameters";
    private readonly RegistryFnLockStore store;

    public RegistryFnLockStoreTests() {
        store = new RegistryFnLockStore(Registry.CurrentUser, parametersPath);
    }

    public void Dispose() {
        Registry.CurrentUser.DeleteSubKeyTree(parametersPath[..parametersPath.LastIndexOf('\\')], throwOnMissingSubKey: false);
        using (RegistryKey? root = Registry.CurrentUser.OpenSubKey(ScratchRoot)) {
            if (root?.SubKeyCount == 0) {
                Registry.CurrentUser.DeleteSubKey(ScratchRoot, throwOnMissingSubKey: false);
            }
        }
    }

    [Fact]
    public void WithoutTheKey_FnLockIsOff() {
        Assert.False(store.Read());
    }

    [Fact]
    public void WhatIsWritten_IsReadBack() {
        store.Write(true);
        Assert.True(store.Read());

        store.Write(false);
        Assert.False(store.Read());
    }

    [Fact]
    public void ItIsKeptAsADword() {
        store.Write(true);

        using RegistryKey key = Registry.CurrentUser.OpenSubKey(parametersPath)!;
        Assert.Equal(1, key.GetValue("FnLock"));
        Assert.Equal(RegistryValueKind.DWord, key.GetValueKind("FnLock"));
    }
}
