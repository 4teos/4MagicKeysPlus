using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.ViewModels;

namespace _4MagicKeysPlusTests;

public class HotkeyListViewModelTests {
    private const uint VkF16 = 0x7F;
    private const uint VkF17 = 0x80;

    private static HotkeyEntry Saved(string name, string label, uint virtualKey, uint modifiers = 0, int? order = null) {
        return new HotkeyEntry(name, label, $@"C:\NoSuchFolder\{label}.exe", virtualKey, modifiers, order);
    }

    private static HotkeyListViewModel List(FakeHotkeyStore store, FakeAgentLink? agent = null, Func<string?>? startAgent = null) {
        HotkeyEditor editor = new(store, agent ?? new FakeAgentLink());
        return new HotkeyListViewModel(editor, _ => null, startAgent ?? (() => null));
    }

    private static FakeHotkeyStore StoreWith(params HotkeyEntry[] entries) {
        FakeHotkeyStore store = new();
        store.Stored.AddRange(entries);
        return store;
    }

    private static async Task<HotkeyCardViewModel> AddedCard(HotkeyListViewModel list, uint virtualKey, uint modifiers = 0, string path = @"C:\NoSuchFolder\Tool.exe", string name = "") {
        HotkeyCardViewModel card = list.AddCard();
        card.DraftPath = path;
        card.DraftName = name;
        card.SetDraftCombination(virtualKey, modifiers);
        await list.CommitAsync(card);
        return card;
    }

    [Fact]
    public void TheCardsAreTheSavedEntries() {
        HotkeyListViewModel list = List(StoreWith(Saved("Hotkey_00_7F", "Calc", VkF16), Saved("Hotkey_00_80", "Notes", VkF17)));

        Assert.Equal(["Calc", "Notes"], list.Cards.Select(card => card.Name));
        Assert.All(list.Cards, card => Assert.False(card.IsEditing));
    }

    [Fact]
    public void ANewCard_StartsInEditMode() {
        HotkeyListViewModel list = List(StoreWith());

        HotkeyCardViewModel card = list.AddCard();

        Assert.True(card.IsEditing);
        Assert.True(card.IsNew);
        Assert.True(card.IsUntouched);
        Assert.Same(card, Assert.Single(list.Cards));
    }

    [Fact]
    public async Task AnIncompleteCard_StaysOpenAndSavesNothing() {
        FakeHotkeyStore store = StoreWith();
        HotkeyListViewModel list = List(store);
        HotkeyCardViewModel card = list.AddCard();
        card.DraftPath = @"C:\NoSuchFolder\Tool.exe";

        bool committed = await list.CommitAsync(card);

        Assert.False(committed);
        Assert.True(card.IsEditing);
        Assert.True(card.IsChipInvalid);
        Assert.False(card.IsPathInvalid);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task ACompleteNewCard_IsSavedAndNamedAfterItsFileWhenLeftBlank() {
        FakeHotkeyStore store = StoreWith();
        HotkeyListViewModel list = List(store);

        HotkeyCardViewModel card = await AddedCard(list, VkF16, modifiers: 0x03);

        Assert.False(card.IsEditing);
        Assert.False(card.IsNew);
        Assert.Equal("Tool", card.Name);
        Assert.Equal("Hotkey_03_7F", card.RegistryName);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public async Task ATakenCombination_IsRefusedAndSaysByWhom() {
        FakeHotkeyStore store = StoreWith(Saved("Hotkey_00_7F", "Calc", VkF16));
        HotkeyListViewModel list = List(store);
        HotkeyCardViewModel card = list.AddCard();
        card.DraftPath = @"C:\NoSuchFolder\Tool.exe";
        card.SetDraftCombination(VkF16, 0);
        list.UpdateConflict(card);

        bool committed = await list.CommitAsync(card);

        Assert.False(committed);
        Assert.Contains("Calc", card.ErrorText);
        Assert.True(card.IsChipInvalid);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task ChangingTheCombinationOfACard_DoesNotConflictWithItself() {
        HotkeyListViewModel list = List(StoreWith(Saved("Hotkey_00_7F", "Calc", VkF16)));
        HotkeyCardViewModel card = list.Cards[0];
        card.BeginEdit();
        card.DraftName = "Calculator";

        bool committed = await list.CommitAsync(card);

        Assert.True(committed);
        Assert.Equal("Calculator", card.Name);
        Assert.Equal("Hotkey_00_7F", card.RegistryName);
    }

    [Fact]
    public void Cancel_RevertsASavedCardAndDropsANewOne() {
        HotkeyListViewModel list = List(StoreWith(Saved("Hotkey_00_7F", "Calc", VkF16)));
        HotkeyCardViewModel existing = list.Cards[0];
        existing.BeginEdit();
        existing.DraftName = "Changed";
        HotkeyCardViewModel added = list.AddCard();

        list.Cancel(existing);
        list.Cancel(added);

        Assert.False(existing.IsEditing);
        Assert.Equal("Calc", existing.Name);
        Assert.Same(existing, Assert.Single(list.Cards));
    }

    [Fact]
    public async Task AFailedSave_KeepsTheCardOpenWithTheReason() {
        FakeHotkeyStore store = StoreWith();
        HotkeyListViewModel list = List(store);
        store.Failure = new UnauthorizedAccessException("denied");

        HotkeyCardViewModel card = await AddedCard(list, VkF16);

        Assert.True(card.IsEditing);
        Assert.Contains("denied", card.ErrorText);
    }

    [Fact]
    public async Task AHotkeyTheAgentCouldNotRegister_IsMarkedOnItsCard() {
        FakeAgentLink agent = new() {
            Reply = AgentResponse.Success([new HotkeyRegistration("Hotkey_00_7F", 0), new HotkeyRegistration("Hotkey_00_80", 1409)]),
        };
        HotkeyListViewModel list = List(StoreWith(Saved("Hotkey_00_7F", "Calc", VkF16), Saved("Hotkey_00_80", "Notes", VkF17)), agent);

        await list.RefreshAsync();

        Assert.Null(list.Cards[0].ErrorText);
        Assert.NotNull(list.Cards[1].ErrorText);
        Assert.True(list.Cards[1].IsChipInvalid);
        Assert.False(list.AgentStopped);
    }

    [Fact]
    public async Task WithoutTheAgent_TheListSaysSo() {
        HotkeyListViewModel list = List(StoreWith(Saved("Hotkey_00_7F", "Calc", VkF16)), new FakeAgentLink { Reply = null });

        await list.RefreshAsync();

        Assert.True(list.AgentStopped);
        Assert.NotEmpty(list.BannerText);
    }

    [Fact]
    public async Task StartingTheAgent_ThatCannotBeStarted_ShowsWhy() {
        HotkeyListViewModel list = List(StoreWith(), new FakeAgentLink { Reply = null }, () => "no file");

        await list.StartAgentAsync();

        Assert.Equal("no file", list.BannerText);
    }

    [Fact]
    public async Task StartingTheAgent_WaitsForItAndRefreshes() {
        FakeAgentLink agent = new() { Reply = null };
        HotkeyListViewModel list = List(StoreWith(Saved("Hotkey_00_7F", "Calc", VkF16)), agent, () => {
            agent.Reply = AgentResponse.Success([new HotkeyRegistration("Hotkey_00_7F", 0)]);
            return null;
        });
        await list.RefreshAsync();
        Assert.True(list.AgentStopped);

        await list.StartAgentAsync();

        Assert.False(list.AgentStopped);
    }

    [Fact]
    public async Task Delete_RemovesTheCardAndCanBeUndone() {
        FakeHotkeyStore store = StoreWith(Saved("Hotkey_00_7F", "Calc", VkF16), Saved("Hotkey_00_80", "Notes", VkF17));
        HotkeyListViewModel list = List(store);
        HotkeyCardViewModel calc = list.Cards[0];

        await list.DeleteAsync(calc);

        Assert.Equal(["Notes"], list.Cards.Select(card => card.Name));
        Assert.True(list.CanUndo);
        Assert.Contains("Calc", list.UndoText);

        await list.UndoDeleteAsync();

        Assert.Equal(["Calc", "Notes"], list.Cards.Select(card => card.Name));
        Assert.Equal(VkF16, list.Cards[0].VirtualKey);
        Assert.False(list.CanUndo);
        Assert.NotNull(list.Cards[0].RegistryName);
    }

    [Fact]
    public async Task DeletingACardThatWasNeverSaved_NeedsNoSaveAndNoUndo() {
        FakeHotkeyStore store = StoreWith();
        HotkeyListViewModel list = List(store);
        HotkeyCardViewModel card = list.AddCard();

        await list.DeleteAsync(card);

        Assert.Empty(list.Cards);
        Assert.False(list.CanUndo);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task AFailedDelete_PutsTheCardBack() {
        FakeHotkeyStore store = StoreWith(Saved("Hotkey_00_7F", "Calc", VkF16));
        HotkeyListViewModel list = List(store);
        store.Failure = new UnauthorizedAccessException("denied");
        HotkeyCardViewModel calc = list.Cards[0];

        await list.DeleteAsync(calc);

        Assert.Same(calc, Assert.Single(list.Cards));
        Assert.Contains("denied", calc.ErrorText);
        Assert.False(list.CanUndo);
    }

    private static (HotkeyListViewModel List, HotkeyEditor Editor, FakeHotkeyStore Store) ThreeCards() {
        FakeHotkeyStore store = StoreWith(Saved("Hotkey_00_7F", "A", VkF16, order: 0), Saved("Hotkey_00_80", "B", VkF17, order: 1), Saved("Hotkey_00_81", "C", 0x81, order: 2));
        HotkeyEditor editor = new(store, new FakeAgentLink());
        return (new HotkeyListViewModel(editor, _ => null, () => null), editor, store);
    }

    [Fact]
    public void MoveTo_ReordersTheCardsWithoutSaving() {
        (HotkeyListViewModel list, _, FakeHotkeyStore store) = ThreeCards();

        list.MoveTo(list.Cards[2], 0);

        Assert.Equal(["C", "A", "B"], list.Cards.Select(card => card.Name));
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task SaveOrderAsync_SavesTheOrderTheCardsAreIn() {
        (HotkeyListViewModel list, HotkeyEditor editor, FakeHotkeyStore store) = ThreeCards();
        HotkeyCardViewModel c = list.Cards[2];
        list.MoveTo(c, 0);

        await list.SaveOrderAsync(c, startIndex: 2);

        Assert.Equal(["C", "A", "B"], editor.Entries.Select(entry => entry.Name));
        Assert.Equal([0, 1, 2], editor.Entries.Select(entry => entry.Order));
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public async Task SaveOrderAsync_WhenTheCardIsBackWhereItStarted_SavesNothing() {
        (HotkeyListViewModel list, _, FakeHotkeyStore store) = ThreeCards();
        HotkeyCardViewModel b = list.Cards[1];
        list.MoveTo(b, 0);
        list.MoveTo(b, 1);

        await list.SaveOrderAsync(b, startIndex: 1);

        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task AFailedSaveOfTheOrder_PutsTheCardBack() {
        (HotkeyListViewModel list, _, FakeHotkeyStore store) = ThreeCards();
        store.Failure = new UnauthorizedAccessException("denied");
        HotkeyCardViewModel c = list.Cards[2];
        list.MoveTo(c, 0);

        await list.SaveOrderAsync(c, startIndex: 2);

        Assert.Equal(["A", "B", "C"], list.Cards.Select(card => card.Name));
        Assert.Contains("denied", c.ErrorText);
    }

    [Fact]
    public void TheAddButton_AsksForTheFirstHotkeyOnlyWhenThereIsNone() {
        HotkeyListViewModel empty = List(StoreWith());
        HotkeyListViewModel filled = List(StoreWith(Saved("Hotkey_00_7F", "Calc", VkF16)));

        Assert.NotEqual(empty.AddText, filled.AddText);
        Assert.True(empty.AddHeight > filled.AddHeight);
    }
}
