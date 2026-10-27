using System.Collections.ObjectModel;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Services;
using _4MagicKeysPlus.Views;

namespace _4MagicKeysPlus.ViewModels;

// A keyboard in the tabs of the title bar. The object of a keyboard stays the same for as long as the keyboard is
// known, whatever is refreshed, so that what is bound to it (the selection) is not replaced every few seconds.
public sealed class KeyboardItem : ObservableObject {
    private string name = "";
    private string connectionGlyph = "";
    private bool isConnected;

    internal KeyboardItem(Guid id) {
        Id = id;
    }

    public Guid Id {
        get;
    }

    public string Name {
        get => name;
        private set {
            if (SetField(ref name, value)) {
                Notify(nameof(Tooltip));
            }
        }
    }

    public string ConnectionGlyph {
        get => connectionGlyph;
        private set => SetField(ref connectionGlyph, value);
    }

    // A keyboard that is not connected is listed but its settings cannot be opened.
    public bool IsConnected {
        get => isConnected;
        private set {
            if (SetField(ref isConnected, value)) {
                Notify(nameof(Tooltip));
            }
        }
    }

    // What the tab of the keyboard says on hover: its name, and that it is not connected when it is not.
    public string Tooltip => isConnected ? name : $"{name} ({Strings.KeyboardNotConnected})";

    internal void Update(KeyboardEntry entry) {
        Name = entry.DisplayName;
        ConnectionGlyph = entry.Live?.Identity.Connection switch {
            KeyboardConnection.Usb => Glyphs.Usb,
            KeyboardConnection.Bluetooth => Glyphs.Bluetooth,
            _ => "",
        };
        IsConnected = entry.IsConnected;
    }
}

// A profile that the keyboard being asked about may be the same keyboard as.
public sealed class LinkCandidate {
    internal LinkCandidate(Guid id, string name) {
        Id = id;
        Name = name;
    }

    public Guid Id {
        get;
    }

    public string Name {
        get;
    }
}

// The tabs of the keyboards in the title bar, and the question that sits under the header when a keyboard that has just
// been connected may be one of those that are known ("is it the same keyboard?"). Lives for the whole app session.
public sealed class KeyboardSelectorViewModel : ObservableObject {
    private readonly KeyboardSession session;
    private readonly Dictionary<Guid, KeyboardItem> known = [];
    private KeyboardItem? selected;
    private UnownedKeyboard? prompt;
    private LinkCandidate? selectedCandidate;
    private string linkPromptText = "";
    private string? problem;
    private bool updating;

    internal KeyboardSelectorViewModel(KeyboardSession session) {
        this.session = session;
        session.Changed += Update;
        Update();
    }

    public ObservableCollection<KeyboardItem> Items { get; } = [];

    // The tabs are there to choose from, so they are shown once there are two keyboards or more.
    public bool HasSeveralKeyboards => Items.Count > 1;

    // Choosing a keyboard that is not connected does nothing: the box goes on showing the one that is selected.
    public KeyboardItem? Selected {
        get => selected;
        set {
            if (updating) {
                return;
            }

            if (value is { IsConnected: true } item && item.Id != session.SelectedId) {
                session.Select(item.Id);
            } else {
                Notify(nameof(Selected));
            }
        }
    }

    public ObservableCollection<LinkCandidate> Candidates { get; } = [];

    public bool HasLinkPrompt => prompt is not null;

    public bool HasSeveralCandidates => Candidates.Count > 1;

    public string LinkPromptText => linkPromptText;

    public LinkCandidate? SelectedCandidate {
        get => selectedCandidate;
        set => SetField(ref selectedCandidate, value);
    }

    public string? Problem => problem;

    public bool HasProblem => problem is not null;

    // The keyboard that was connected is the one of the chosen profile.
    public void LinkToCandidate() {
        if (prompt is null || SelectedCandidate is null) {
            return;
        }

        KeyboardProfile? profile = prompt.Candidates.FirstOrDefault(candidate => candidate.Id == SelectedCandidate.Id);
        if (profile is not null) {
            Answer(session.Link(prompt, profile));
        }
    }

    public void StartNewKeyboard() {
        if (prompt is not null) {
            Answer(session.StartNew(prompt));
        }
    }

    // What came of the answer: a profile that could not be saved leaves the question where it is, with the reason.
    private void Answer(ApplyResult result) {
        problem = result.Outcome == ApplyOutcome.Failed ? result.Message : null;
        Notify(nameof(Problem), nameof(HasProblem));
    }

    private void Update() {
        updating = true;
        try {
            UpdateItems();
            selected = session.SelectedId is Guid id && known.TryGetValue(id, out KeyboardItem? item) ? item : null;
            UpdatePrompt();
        } finally {
            updating = false;
        }

        Notify(nameof(Selected), nameof(HasSeveralKeyboards));
    }

    private void UpdateItems() {
        IReadOnlyList<KeyboardEntry> keyboards = session.Catalog.Keyboards;
        foreach (KeyboardEntry entry in keyboards) {
            if (!known.TryGetValue(entry.Profile.Id, out KeyboardItem? item)) {
                item = new KeyboardItem(entry.Profile.Id);
                known[entry.Profile.Id] = item;
            }
            item.Update(entry);
        }

        List<KeyboardItem> wanted = [.. keyboards.Select(entry => known[entry.Profile.Id])];
        foreach (KeyboardItem gone in Items.Except(wanted).ToList()) {
            Items.Remove(gone);
            known.Remove(gone.Id);
        }

        for (int index = 0; index < wanted.Count; index++) {
            int current = Items.IndexOf(wanted[index]);
            if (current < 0) {
                Items.Insert(index, wanted[index]);
            } else if (current != index) {
                Items.Move(current, index);
            }
        }
    }

    // The question stays as it is while it is the same one (the same keyboard, the same profiles to choose from), so that the
    // choice of the user among several is not undone by a refresh.
    private void UpdatePrompt() {
        UnownedKeyboard? next = session.Catalog.Unowned.FirstOrDefault(keyboard => keyboard.Candidates.Count > 0);
        bool same = next is not null && prompt is not null
            && next.Endpoint.Identity == prompt.Endpoint.Identity
            && next.Candidates.Select(candidate => candidate.Id).SequenceEqual(prompt.Candidates.Select(candidate => candidate.Id));
        prompt = next;
        if (same) {
            return;
        }

        Candidates.Clear();
        foreach (KeyboardProfile candidate in next?.Candidates ?? []) {
            Candidates.Add(new LinkCandidate(candidate.Id, KeyboardCatalog.DisplayName(candidate)));
        }
        SelectedCandidate = Candidates.FirstOrDefault();
        linkPromptText = next is null ? "" : DescribeKeyboard(next);
        problem = null;
        Notify(nameof(HasLinkPrompt), nameof(HasSeveralCandidates), nameof(LinkPromptText), nameof(Problem), nameof(HasProblem));
    }

    private static string DescribeKeyboard(UnownedKeyboard keyboard) {
        string model = KeyboardModel.From(keyboard.Endpoint.Product).DeviceDescription;
        string connection = keyboard.Endpoint.Identity.Connection == KeyboardConnection.Usb ? Strings.ConnectionUsb : Strings.ConnectionBluetooth;
        return keyboard.Candidates.Count == 1
            ? string.Format(Strings.LinkPromptOne, model, connection, KeyboardCatalog.DisplayName(keyboard.Candidates[0]))
            : string.Format(Strings.LinkPromptSeveral, model, connection);
    }
}
