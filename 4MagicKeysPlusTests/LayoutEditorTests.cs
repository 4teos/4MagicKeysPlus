using Microsoft.Win32;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlusTests;

internal sealed class FakeLayoutStore : ILayoutStore {
    public LayoutStandard Standard {
        get; set;
    } = LayoutStandard.Iso;
    public Exception? Failure {
        get; set;
    }

    public LayoutStandard Read() {
        return Standard;
    }

    public void Write(LayoutStandard standard) {
        if (Failure is not null) {
            throw Failure;
        }
        Standard = standard;
    }
}

public class LayoutEditorTests {
    [Theory]
    [InlineData(LayoutStandard.Ansi)]
    [InlineData(LayoutStandard.Iso)]
    public void ASavedChoice_IsReadBack(LayoutStandard standard) {
        FakeLayoutStore store = new() {
            Standard = standard == LayoutStandard.Ansi ? LayoutStandard.Iso : LayoutStandard.Ansi
        };
        LayoutEditor editor = new(store);

        ApplyResult result = editor.Set(standard);

        Assert.Equal(ApplyOutcome.Applied, result.Outcome);
        Assert.Equal(standard, store.Standard);
        Assert.Equal(standard, editor.Standard);
    }

    [Fact]
    public void AFailedSave_ChangesNothingAndIsReported() {
        FakeLayoutStore store = new() {
            Failure = new UnauthorizedAccessException("denied")
        };
        LayoutEditor editor = new(store);

        ApplyResult result = editor.Set(LayoutStandard.Ansi);

        Assert.Equal(ApplyOutcome.Failed, result.Outcome);
        Assert.Contains("denied", result.Message);
        Assert.Equal(LayoutStandard.Iso, editor.Standard);
    }
}

[Collection(TestCollections.Registry)]
public sealed class RegistryLayoutStoreTests : IDisposable {
    private readonly ScratchKey scratch = new();
    private readonly RegistryLayoutStore store;

    public RegistryLayoutStoreTests() {
        store = new RegistryLayoutStore(Registry.CurrentUser, scratch.Path("Profile"));
    }

    public void Dispose() {
        scratch.Dispose();
    }

    [Fact]
    public void WithoutTheKey_TheLayoutIsIso() {
        Assert.Equal(LayoutStandard.Iso, store.Read());
    }

    [Theory]
    [InlineData(LayoutStandard.Ansi)]
    [InlineData(LayoutStandard.Iso)]
    public void WhatIsWritten_IsReadBack(LayoutStandard standard) {
        store.Write(standard);

        Assert.Equal(standard, store.Read());
    }

    [Fact]
    public void ItIsKeptAsText() {
        store.Write(LayoutStandard.Ansi);

        using RegistryKey key = scratch.Open("Profile")!;
        Assert.Equal("Ansi", key.GetValue("Layout"));
        Assert.Equal(RegistryValueKind.String, key.GetValueKind("Layout"));
    }

    [Fact]
    public void TheOtherValuesOfTheProfile_AreLeftAlone() {
        using (RegistryKey key = scratch.Create("Profile")) {
            key.SetValue("FnLock", 1, RegistryValueKind.DWord);
        }

        store.Write(LayoutStandard.Ansi);

        using RegistryKey profile = scratch.Open("Profile")!;
        Assert.Equal(1, profile.GetValue("FnLock"));
    }

    [Theory]
    [InlineData("JIS")]
    [InlineData("7")]
    [InlineData("")]
    public void ATextThatIsNoLayout_MeansIso(string text) {
        using (RegistryKey key = scratch.Create("Profile")) {
            key.SetValue("Layout", text, RegistryValueKind.String);
        }

        Assert.Equal(LayoutStandard.Iso, store.Read());
    }

    [Fact]
    public void ANumber_MeansIso() {
        using (RegistryKey key = scratch.Create("Profile")) {
            key.SetValue("Layout", 0, RegistryValueKind.DWord);
        }

        Assert.Equal(LayoutStandard.Iso, store.Read());
    }
}
