using Microsoft.Win32;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlusTests;

public class HotkeysPlanTests {
    private static HotkeyEntry Entry(string? name, uint virtualKey = 0x7F, uint modifiers = 0, string label = "Calc", string path = @"C:\calc.exe", int? order = null) {
        return new HotkeyEntry(name, label, path, virtualKey, modifiers, order);
    }

    [Fact]
    public void ANewEntry_IsWrittenUnderItsModifiersAndKey() {
        HotkeysPlan plan = HotkeysPlan.Create([], [Entry(null, virtualKey: 0x41, modifiers: 0x03)]);

        Assert.Equal("Hotkey_03_41", Assert.Single(plan.Write).RegistryName);
        Assert.Equal("Hotkey_03_41", Assert.Single(plan.Result).RegistryName);
        Assert.Empty(plan.Release);
    }

    [Fact]
    public void AnUnchangedEntry_IsNotWrittenAgain() {
        HotkeyEntry saved = Entry("Launch_00_7F", order: 0);

        HotkeysPlan plan = HotkeysPlan.Create([saved], [saved]);

        Assert.Empty(plan.Write);
        Assert.Empty(plan.Release);
    }

    [Fact]
    public void ANewNameOrPath_IsWrittenInPlaceWhateverTheSubkeyIsCalled() {
        HotkeyEntry saved = Entry("Launch_00_7F", order: 0);

        HotkeysPlan plan = HotkeysPlan.Create([saved], [saved with { Name = "Renamed", Path = @"C:\new.exe" }]);

        Assert.Equal("Launch_00_7F", Assert.Single(plan.Write).RegistryName);
        Assert.Empty(plan.Release);
    }

    [Fact]
    public void AChangedCombination_MovesToANewSubkeyAndReleasesTheOldOne() {
        HotkeyEntry saved = Entry("Launch_00_7F", order: 0);

        HotkeysPlan plan = HotkeysPlan.Create([saved], [saved with { VirtualKey = 0x80 }]);

        Assert.Equal("Hotkey_00_80", Assert.Single(plan.Write).RegistryName);
        Assert.Equal(["Launch_00_7F"], plan.Release);
        Assert.Equal("Hotkey_00_80", Assert.Single(plan.Result).RegistryName);
    }

    [Fact]
    public void ARemovedEntry_IsReleased() {
        HotkeyEntry kept = Entry("Hotkey_00_80", virtualKey: 0x80, order: 0);
        HotkeyEntry removed = Entry("Hotkey_00_7F", order: 1);

        HotkeysPlan plan = HotkeysPlan.Create([kept, removed], [kept]);

        Assert.Equal(["Hotkey_00_7F"], plan.Release);
        Assert.Empty(plan.Write);
    }

    [Fact]
    public void ANewEntry_MayTakeTheSubkeyOfARemovedOneWithoutReleasingIt() {
        HotkeyEntry removed = Entry("Hotkey_00_7F", order: 0);

        HotkeysPlan plan = HotkeysPlan.Create([removed], [Entry(null, label: "Other", path: @"C:\other.exe")]);

        Assert.Empty(plan.Release);
        Assert.Equal("Hotkey_00_7F", Assert.Single(plan.Write).RegistryName);
    }

    [Fact]
    public void TwoEntries_NeverShareASubkey() {
        HotkeyEntry odd = Entry("Hotkey_00_7F", virtualKey: 0x80, order: 0);

        HotkeysPlan plan = HotkeysPlan.Create([odd], [odd, Entry(null)]);

        Assert.Equal("Hotkey_00_7F_2", Assert.Single(plan.Write).RegistryName);
    }

    [Fact]
    public void ThePositionInTheList_IsTheOrder() {
        HotkeysPlan plan = HotkeysPlan.Create([], [Entry(null), Entry(null, virtualKey: 0x80)]);

        Assert.Equal([0, 1], plan.Result.Select(entry => entry.Order));
    }

    [Fact]
    public void AnEntryThatMoved_IsWrittenWithItsNewOrder() {
        HotkeyEntry first = Entry("Hotkey_00_7F", order: 0);
        HotkeyEntry second = Entry("Hotkey_00_80", virtualKey: 0x80, order: 1);

        HotkeysPlan plan = HotkeysPlan.Create([first, second], [second, first]);

        Assert.Equal([0, 1], plan.Write.Select(entry => entry.Order));
        Assert.Equal(["Hotkey_00_80", "Hotkey_00_7F"], plan.Write.Select(entry => entry.RegistryName));
        Assert.Empty(plan.Release);
    }

    [Fact]
    public void AnEntryWithoutAnOrder_GetsOneOnTheFirstSave() {
        HotkeyEntry legacy = Entry("Launch_00_7F");

        HotkeysPlan plan = HotkeysPlan.Create([legacy], [legacy]);

        Assert.Equal(0, Assert.Single(plan.Write).Order);
    }

    [Fact]
    public void ChangingTheCombination_KeepsThePosition() {
        HotkeyEntry first = Entry("Hotkey_00_7F", order: 0);
        HotkeyEntry second = Entry("Hotkey_00_80", virtualKey: 0x80, order: 1);

        HotkeysPlan plan = HotkeysPlan.Create([first, second], [first with { VirtualKey = 0x90 }, second]);

        Assert.Equal(["Hotkey_00_90", "Hotkey_00_80"], plan.Result.Select(entry => entry.RegistryName));
        Assert.Equal(0, Assert.Single(plan.Write).Order);
    }
}

public sealed class RegistryHotkeyStoreTests : IDisposable {
    private const string ScratchRoot = @"Software\4MagicKeysPlusTests";
    private readonly string rulesPath = $@"{ScratchRoot}\{Guid.NewGuid():N}\Rules";
    private readonly RegistryHotkeyStore store;

    public RegistryHotkeyStoreTests() {
        store = new RegistryHotkeyStore(Registry.CurrentUser, rulesPath);
    }

    public void Dispose() {
        Registry.CurrentUser.DeleteSubKeyTree(rulesPath[..rulesPath.LastIndexOf('\\')], throwOnMissingSubKey: false);
        using (RegistryKey? root = Registry.CurrentUser.OpenSubKey(ScratchRoot)) {
            if (root?.SubKeyCount == 0) {
                Registry.CurrentUser.DeleteSubKey(ScratchRoot, throwOnMissingSubKey: false);
            }
        }
    }

    private static HotkeyEntry Entry(string? name, uint virtualKey = 0x7F, uint modifiers = 0, string label = "Calc", string path = @"C:\calc.exe") {
        return new HotkeyEntry(name, label, path, virtualKey, modifiers);
    }

    private RegistryKey Open(string name) {
        return Registry.CurrentUser.OpenSubKey($@"{rulesPath}\{name}", writable: true) ?? throw new InvalidOperationException($"No subkey {name}");
    }

    private string[] SubKeyNames() {
        using RegistryKey rulesKey = Registry.CurrentUser.OpenSubKey(rulesPath)!;
        return rulesKey.GetSubKeyNames();
    }

    [Fact]
    public void Save_WritesTheEntryAsValues() {
        IReadOnlyList<HotkeyEntry> saved = store.Save([], [Entry(null, virtualKey: 0x41, modifiers: 0x03, label: "Editor", path: @"C:\edit.exe")]);

        using RegistryKey key = Open("Hotkey_03_41");
        Assert.Equal(0x41, key.GetValue("VkKey"));
        Assert.Equal(0x03, key.GetValue("VkModifier"));
        Assert.Equal(@"C:\edit.exe", key.GetValue("Path"));
        Assert.Equal("Editor", key.GetValue("Comment"));
        Assert.Equal(0, key.GetValue("Order"));
        Assert.Equal("Hotkey_03_41", Assert.Single(saved).RegistryName);
    }

    [Fact]
    public void Save_WithoutModifiers_WritesNoVkModifier() {
        store.Save([], [Entry(null)]);

        using RegistryKey key = Open("Hotkey_00_7F");
        Assert.Null(key.GetValue("VkModifier"));
    }

    [Fact]
    public void Save_ANewPath_KeepsTheSubkeyAndDropsAStaleModifierNeverWritten() {
        IReadOnlyList<HotkeyEntry> first = store.Save([], [Entry(null)]);

        store.Save(first, [first[0] with { Path = @"C:\other.exe", Name = "Other" }]);

        using RegistryKey key = Open("Hotkey_00_7F");
        Assert.Equal(@"C:\other.exe", key.GetValue("Path"));
        Assert.Equal("Other", key.GetValue("Comment"));
        Assert.Single(SubKeyNames());
    }

    [Fact]
    public void Save_AChangedCombination_MovesTheEntryToANewSubkey() {
        IReadOnlyList<HotkeyEntry> first = store.Save([], [Entry(null, modifiers: 0x01)]);

        store.Save(first, [first[0] with { Modifiers = 0 }]);

        Assert.Equal(["Hotkey_00_7F"], SubKeyNames());
        using RegistryKey key = Open("Hotkey_00_7F");
        Assert.Null(key.GetValue("VkModifier"));
    }

    [Fact]
    public void Save_RemovingAnEntry_DeletesItsSubkey() {
        IReadOnlyList<HotkeyEntry> first = store.Save([], [Entry(null)]);

        store.Save(first, []);

        Assert.Empty(SubKeyNames());
    }

    [Fact]
    public void Save_RemovingAnEntryThatIsAlsoADriverRule_KeepsTheRule() {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey($@"{rulesPath}\Launch_00_7F")) {
            key.SetValue("Source", 0x7F, RegistryValueKind.DWord);
            key.SetValue("Target", 0x7F, RegistryValueKind.DWord);
            key.SetValue("VkKey", 0x7F, RegistryValueKind.DWord);
            key.SetValue("Path", @"C:\calc.exe");
            key.SetValue("Comment", "Calc");
        }

        store.Save(store.Load(), []);

        using RegistryKey kept = Open("Launch_00_7F");
        Assert.Equal(0x7F, kept.GetValue("Source"));
        Assert.Equal(0x7F, kept.GetValue("Target"));
        Assert.Null(kept.GetValue("VkKey"));
        Assert.Null(kept.GetValue("Path"));
    }

    [Fact]
    public void Load_ReadsBackWhatWasSaved() {
        List<HotkeyEntry> wanted = [
            Entry(null, virtualKey: 0x7F, label: "Calc", path: @"C:\calc.exe"),
            Entry(null, virtualKey: 0x54, modifiers: 0x03, label: "Terminal", path: @"C:\wt.exe"),
        ];

        IReadOnlyList<HotkeyEntry> saved = store.Save([], wanted);

        Assert.Equal(saved.OrderBy(entry => entry.RegistryName), store.Load().OrderBy(entry => entry.RegistryName));
    }

    [Fact]
    public void Load_KeepsTheOrderOfTheListWhateverTheSubkeysAreCalled() {
        IReadOnlyList<HotkeyEntry> first = store.Save([], [Entry(null, virtualKey: 0x7F), Entry(null, virtualKey: 0x80, label: "Notes")]);

        store.Save(first, [first[0] with { VirtualKey = 0x90 }, first[1]]);

        Assert.Equal(["Hotkey_00_90", "Hotkey_00_80"], store.Load().Select(entry => entry.RegistryName));
    }

    [Fact]
    public void Load_PutsEntriesWithoutAnOrderAfterTheOthersInSubkeyOrder() {
        store.Save([], [Entry(null, virtualKey: 0x80)]);
        foreach (string name in new[] { "B_legacy", "A_legacy" }) {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey($@"{rulesPath}\{name}");
            key.SetValue("VkKey", 0x7F, RegistryValueKind.DWord);
            key.SetValue("Path", @"C:\calc.exe");
        }

        Assert.Equal(["Hotkey_00_80", "A_legacy", "B_legacy"], store.Load().Select(entry => entry.RegistryName));
    }

    [Fact]
    public void Load_SkipsSubkeysThatAreNotHotkeys() {
        using (RegistryKey rule = Registry.CurrentUser.CreateSubKey($@"{rulesPath}\01000041")) {
            rule.SetValue("Source", 0x01000041, RegistryValueKind.DWord);
            rule.SetValue("Target", 0x0000006F, RegistryValueKind.DWord);
        }
        using (RegistryKey half = Registry.CurrentUser.CreateSubKey($@"{rulesPath}\NoKey")) {
            half.SetValue("Path", @"C:\calc.exe");
        }

        Assert.Empty(store.Load());
    }

    [Fact]
    public void Load_WithoutAComment_NamesTheEntryAfterItsFile() {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey($@"{rulesPath}\Launch_00_7F")) {
            key.SetValue("VkKey", 0x7F, RegistryValueKind.DWord);
            key.SetValue("Path", @"C:\NoSuchFolder\Old Tool.lnk");
        }

        Assert.Equal("Old Tool", Assert.Single(store.Load()).Name);
    }
}
