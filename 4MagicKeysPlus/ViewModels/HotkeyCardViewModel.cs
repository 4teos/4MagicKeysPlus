using System.ComponentModel;
using System.Windows.Media;
using _4MagicKeysPlus.Core;
using _4MagicKeysPlus.Models;
using _4MagicKeysPlus.Properties;
using _4MagicKeysPlus.Views;

namespace _4MagicKeysPlus.ViewModels;

// One card of the Quick launch list. The saved values are what is in the registry; the draft
// ones are what the user is typing and capturing while the card is being edited.
internal sealed class HotkeyCardViewModel : INotifyPropertyChanged {
    private readonly Func<string, ImageSource?> loadIcon;
    private string? registryName;
    private string name = "";
    private string path = "";
    private uint virtualKey;
    private uint modifiers;
    private string draftName = "";
    private string draftPath = "";
    private uint draftVirtualKey;
    private uint draftModifiers;
    private bool isEditing;
    private bool isCapturing;
    private bool showValidation;
    private bool isDragging;
    private string? conflictText;
    private string? registrationError;
    private string? saveError;
    private ImageSource? icon;

    private HotkeyCardViewModel(Func<string, ImageSource?> loadIcon) {
        this.loadIcon = loadIcon;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // A card for an entry that is saved already, or was and is being put back.
    public static HotkeyCardViewModel FromEntry(HotkeyEntry entry, Func<string, ImageSource?> loadIcon) {
        HotkeyCardViewModel card = new(loadIcon);
        card.Accept(entry);
        return card;
    }

    // A blank card, in edit mode, for the entry the user is about to add.
    public static HotkeyCardViewModel CreateNew(Func<string, ImageSource?> loadIcon) {
        HotkeyCardViewModel card = new(loadIcon) { IsNew = true };
        card.BeginEdit();
        return card;
    }

    public bool IsNew { get; private set; }

    public string? RegistryName => registryName;

    public string Name => name;

    public string Path => path;

    public uint VirtualKey => virtualKey;

    public uint Modifiers => modifiers;

    public ImageSource? Icon => icon;

    public bool HasIcon => icon is not null;

    public bool IsEditing => isEditing;

    public bool IsViewing => !isEditing;

    public bool IsCapturing => isCapturing;

    // True while the card is being dragged: its place in the list stays as an empty slot.
    public bool IsDragging => isDragging;

    public string DraftName {
        get => draftName;
        set {
            draftName = value;
            Notify();
        }
    }

    public string DraftPath {
        get => draftPath;
        set {
            draftPath = value;
            Notify();
        }
    }

    public uint DraftVirtualKey => draftVirtualKey;

    public uint DraftModifiers => draftModifiers;

    public bool IsComplete => !string.IsNullOrWhiteSpace(draftPath) && draftVirtualKey != 0;

    // True while the card is a blank the user has not put anything into.
    public bool IsUntouched => IsNew && string.IsNullOrWhiteSpace(draftName) && string.IsNullOrWhiteSpace(draftPath) && draftVirtualKey == 0;

    public string? ErrorText => saveError ?? conflictText ?? (isEditing ? null : registrationError);

    public bool HasError => ErrorText is not null;

    public bool IsChipInvalid => ErrorText is not null || (showValidation && draftVirtualKey == 0);

    public bool IsPathInvalid => showValidation && string.IsNullOrWhiteSpace(draftPath);

    public string ChipText {
        get {
            if (!isEditing) {
                return HotkeyText.Format(virtualKey, modifiers);
            }
            if (isCapturing) {
                return Strings.HotkeyCapturePrompt;
            }
            return draftVirtualKey == 0 ? Strings.HotkeyNotSet : HotkeyText.Format(draftVirtualKey, draftModifiers);
        }
    }

    public string ActionGlyph => isEditing ? Glyphs.Accept : Glyphs.Edit;

    public string ActionTooltip => isEditing ? Strings.HotkeyDone : Strings.HotkeyEdit;

    public HotkeyEntry ToEntry() {
        return new HotkeyEntry(registryName, name, path, virtualKey, modifiers);
    }

    public void BeginEdit() {
        draftName = name;
        draftPath = path;
        draftVirtualKey = virtualKey;
        draftModifiers = modifiers;
        isEditing = true;
        showValidation = false;
        conflictText = null;
        saveError = null;
        Notify();
    }

    // Back to the saved values.
    public void Revert() {
        isEditing = false;
        isCapturing = false;
        showValidation = false;
        conflictText = null;
        saveError = null;
        Notify();
    }

    // The entry as saved, subkey name included.
    public void Accept(HotkeyEntry saved) {
        registryName = saved.RegistryName;
        name = saved.Name;
        path = saved.Path;
        virtualKey = saved.VirtualKey;
        modifiers = saved.Modifiers;
        icon = loadIcon(saved.Path);
        IsNew = false;
        Revert();
    }

    public void SetRegistryName(string? value) {
        registryName = value;
    }

    public void SetDraftCombination(uint virtualKey, uint modifiers) {
        draftVirtualKey = virtualKey;
        draftModifiers = modifiers;
        Notify();
    }

    public void SetDraftFile(string file, string defaultName) {
        draftPath = file;
        if (string.IsNullOrWhiteSpace(draftName)) {
            draftName = defaultName;
        }
        icon = loadIcon(file);
        Notify();
    }

    public void SetDragging(bool value) {
        isDragging = value;
        Notify();
    }

    public void SetCapturing(bool value) {
        isCapturing = value;
        Notify();
    }

    public void SetConflict(string? text) {
        conflictText = text;
        Notify();
    }

    public void SetSaveError(string? text) {
        saveError = text;
        Notify();
    }

    public void SetRegistrationError(string? text) {
        registrationError = text;
        Notify();
    }

    public void ShowValidation() {
        showValidation = true;
        Notify();
    }

    // Everything the card shows depends on several of these, so one notification covers all of it.
    private void Notify() {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
