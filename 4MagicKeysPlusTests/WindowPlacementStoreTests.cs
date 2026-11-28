using Microsoft.Win32;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

[Collection(TestCollections.Registry)]
public sealed class WindowPlacementStoreTests : IDisposable {
    private readonly ScratchKey scratch = new();
    private readonly RegistryWindowPlacementStore store;

    public WindowPlacementStoreTests() {
        store = new RegistryWindowPlacementStore(Registry.CurrentUser, scratch.Path("Settings"));
    }

    public void Dispose() {
        scratch.Dispose();
    }

    [Fact]
    public void WithoutAKey_ThereIsNoPlacement() {
        Assert.Null(store.Read());
    }

    [Theory]
    [InlineData(100, 50, 1220, 750)]
    [InlineData(-1800, -20, -680, 680)]
    public void WhatIsWritten_IsRead(int left, int top, int right, int bottom) {
        store.Write(new WindowBounds(left, top, right, bottom));

        Assert.Equal(new WindowBounds(left, top, right, bottom), store.Read());
    }

    [Fact]
    public void ThePlacement_IsKeptAsReadableText() {
        store.Write(new WindowBounds(-10, 20, 1110, 720));

        using RegistryKey key = scratch.Open("Settings")!;
        Assert.Equal("-10,20,1110,720", key.GetValue("WindowPlacement"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1,2,3")]
    [InlineData("1,2,3,4,5")]
    [InlineData("a,b,c,d")]
    [InlineData("100,100,100,500")]
    [InlineData("100,500,800,400")]
    public void AValue_ThatIsNoUsableRectangle_MeansNoPlacement(string stored) {
        using (RegistryKey key = scratch.Create("Settings")) {
            key.SetValue("WindowPlacement", stored);
        }

        Assert.Null(store.Read());
    }

    [Fact]
    public void AValueOfAnotherType_MeansNoPlacement() {
        using (RegistryKey key = scratch.Create("Settings")) {
            key.SetValue("WindowPlacement", 42, RegistryValueKind.DWord);
        }

        Assert.Null(store.Read());
    }

    [Fact]
    public void Spaces_AroundTheNumbers_AreAllowed() {
        using (RegistryKey key = scratch.Create("Settings")) {
            key.SetValue("WindowPlacement", " 1, 2 ,3 , 4");
        }

        Assert.Equal(new WindowBounds(1, 2, 3, 4), store.Read());
    }
}
