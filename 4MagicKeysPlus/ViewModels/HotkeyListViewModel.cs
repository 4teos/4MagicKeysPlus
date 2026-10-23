using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;

namespace _4MagicKeysPlus.ViewModels;

// The Quick launch screen: the cards, what the agent made of them, and the undo of a delete.
internal sealed class HotkeyListViewModel : INotifyPropertyChanged {
    private readonly HotkeyEditor editor;
    private readonly Func<string, ImageSource?> loadIcon;
    private readonly Func<string?> startAgent;
    private bool agentRunning = true;
    private string? agentProblem;
    private bool busy;
    private (HotkeyEntry Entry, int Index)? deleted;

    // startAgent starts the agent and answers what went wrong, or null.
    public HotkeyListViewModel(HotkeyEditor editor, Func<string, ImageSource?> loadIcon, Func<string?> startAgent) {
        this.editor = editor;
        this.loadIcon = loadIcon;
        this.startAgent = startAgent;
        foreach (HotkeyEntry entry in editor.Entries) {
            Cards.Add(HotkeyCardViewModel.FromEntry(entry, loadIcon));
        }
        Cards.CollectionChanged += (_, _) => Notify();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<HotkeyCardViewModel> Cards { get; } = [];

    public bool AgentStopped => !agentRunning;

    public string BannerText => agentProblem ?? Strings.HotkeyAgentNotRunning;

    public string AddText => Cards.Count == 0 ? Strings.HotkeyAddFirst : Strings.HotkeyAdd;

    public double AddHeight => Cards.Count == 0 ? 96 : 56;

    public bool CanUndo => deleted is not null;

    public string UndoText => deleted is { } gone ? string.Format(Strings.HotkeyDeleted, gone.Entry.Name) : "";

    public HotkeyCardViewModel AddCard() {
        HotkeyCardViewModel card = HotkeyCardViewModel.CreateNew(loadIcon);
        Cards.Add(card);
        return card;
    }

    // Asks the agent for a fresh start from what is saved, which also tells which hotkeys it
    // could not register and whether it runs at all.
    public async Task RefreshAsync() {
        List<HotkeyCardViewModel> saved = SavedCards();
        Show(await editor.ApplyAsync([.. saved.Select(card => card.ToEntry())]), saved);
    }

    // Saves the card's draft. False when the card has to stay in edit mode: it is not complete,
    // its combination is taken, or saving failed (the card says which).
    public async Task<bool> CommitAsync(HotkeyCardViewModel card) {
        if (!card.IsEditing || busy) {
            return false;
        }
        if (!card.IsComplete) {
            card.ShowValidation();
            return false;
        }
        card.SetSaveError(null);
        UpdateConflict(card);
        if (card.ErrorText is not null) {
            return false;
        }

        busy = true;
        try {
            string path = card.DraftPath.Trim();
            string name = string.IsNullOrWhiteSpace(card.DraftName) ? HotkeyNames.FromPath(path) : card.DraftName.Trim();
            HotkeyEntry entry = card.ToEntry() with {
                Name = name,
                Path = path,
                VirtualKey = card.DraftVirtualKey,
                Modifiers = card.DraftModifiers
            };

            List<HotkeyCardViewModel> saved = SavedCards();
            if (!saved.Contains(card)) {
                saved.Add(card);
            }
            List<HotkeyEntry> wanted = [.. saved.Select(other => other == card ? entry : other.ToEntry())];

            HotkeyApplyResult result = await editor.ApplyAsync(wanted);
            if (result.Outcome == ApplyOutcome.Failed) {
                card.SetSaveError(result.Message);
                return false;
            }

            card.Accept(result.Entries[saved.IndexOf(card)]);
            Show(result, saved);
            return true;
        } finally {
            busy = false;
        }
    }

    // Moves the card to the given place in the list without saving, as it is dragged along.
    public void MoveTo(HotkeyCardViewModel card, int index) {
        int from = Cards.IndexOf(card);
        if (from >= 0 && from != index && index >= 0 && index < Cards.Count) {
            Cards.Move(from, index);
        }
    }

    // Saves the order the cards are in now, once a drag has ended where it did not start. When
    // that fails the card goes back to where it was.
    public async Task SaveOrderAsync(HotkeyCardViewModel card, int startIndex) {
        if (card.IsNew || Cards.IndexOf(card) == startIndex) {
            return;
        }

        List<HotkeyCardViewModel> saved = SavedCards();
        HotkeyApplyResult result = await editor.ApplyAsync([.. saved.Select(other => other.ToEntry())]);
        if (result.Outcome == ApplyOutcome.Failed) {
            MoveTo(card, startIndex);
            card.SetSaveError(result.Message);
            return;
        }
        Show(result, saved);
    }

    // Leaves edit mode without saving; a card that was never saved goes away.
    public void Cancel(HotkeyCardViewModel card) {
        if (card.IsNew) {
            Cards.Remove(card);
        } else {
            card.Revert();
        }
    }

    // Shows whether another card already has the combination the card has captured.
    public void UpdateConflict(HotkeyCardViewModel card) {
        IEnumerable<HotkeyEntry> others = SavedCards().Where(other => other != card).Select(other => other.ToEntry());
        HotkeyEntry? conflict = card.DraftVirtualKey == 0 ? null : HotkeyEditor.FindConflict(others, card.DraftVirtualKey, card.DraftModifiers);
        card.SetConflict(conflict is null ? null : string.Format(Strings.HotkeyAlreadyAssigned, conflict.Name));
    }

    public async Task DeleteAsync(HotkeyCardViewModel card) {
        int index = Cards.IndexOf(card);
        if (index < 0) {
            return;
        }
        if (card.IsNew) {
            Cards.Remove(card);
            return;
        }

        HotkeyEntry entry = card.ToEntry();
        Cards.Remove(card);
        List<HotkeyCardViewModel> saved = SavedCards();
        HotkeyApplyResult result = await editor.ApplyAsync([.. saved.Select(other => other.ToEntry())]);
        if (result.Outcome == ApplyOutcome.Failed) {
            Cards.Insert(index, card);
            card.SetSaveError(result.Message);
            return;
        }

        deleted = (entry, index);
        Show(result, saved);
        Notify();
    }

    // Puts the deleted entry back where it was. It is saved as a new one, so it may get a new subkey name.
    public async Task UndoDeleteAsync() {
        if (deleted is not { } gone) {
            return;
        }
        deleted = null;

        HotkeyCardViewModel card = HotkeyCardViewModel.FromEntry(gone.Entry with {
            RegistryName = null
        }, loadIcon);
        Cards.Insert(Math.Min(gone.Index, Cards.Count), card);
        List<HotkeyCardViewModel> saved = SavedCards();
        HotkeyApplyResult result = await editor.ApplyAsync([.. saved.Select(other => other.ToEntry())]);
        if (result.Outcome == ApplyOutcome.Failed) {
            Cards.Remove(card);
            return;
        }
        Show(result, saved);
        Notify();
    }

    public void ClearUndo() {
        deleted = null;
        Notify();
    }

    // Starts the agent and waits for it to answer on its pipe.
    public async Task StartAgentAsync() {
        agentProblem = startAgent();
        Notify();
        if (agentProblem is not null) {
            return;
        }

        if (await AgentLauncher.WaitUntilRunningAsync(editor.IsAgentRunningAsync)) {
            await RefreshAsync();
        }
    }

    private List<HotkeyCardViewModel> SavedCards() {
        return [.. Cards.Where(card => !card.IsNew)];
    }

    // The cards are those the result's entries belong to, in the same order.
    private void Show(HotkeyApplyResult result, List<HotkeyCardViewModel> cards) {
        if (result.Outcome != ApplyOutcome.Failed && result.Entries.Count == cards.Count) {
            for (int i = 0; i < cards.Count; i++) {
                cards[i].SetRegistryName(result.Entries[i].RegistryName);
            }
        }

        agentRunning = result.AgentRunning;
        agentProblem = null;
        foreach (HotkeyCardViewModel card in cards) {
            HotkeyRegistration? failure = result.Registrations.FirstOrDefault(registration => registration.RegistryName == card.RegistryName && !registration.Succeeded);
            card.SetRegistrationError(failure is null ? null : new Win32Exception(failure.Win32Error).Message);
        }
        Notify();
    }

    private void Notify() {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
