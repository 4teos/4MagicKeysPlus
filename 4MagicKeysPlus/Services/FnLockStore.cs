using Microsoft.Win32;

namespace _4MagicKeysPlus.Services;

internal interface IFnLockStore {
    bool Read();

    void Write(bool enabled);
}

internal sealed class RegistryFnLockStore : IFnLockStore {
    private const string ValueName = "FnLock";

    private readonly RegistryKey root;
    private readonly string profilePath;

    public RegistryFnLockStore(RegistryKey root, string profilePath) {
        this.root = root;
        this.profilePath = profilePath;
    }

    public bool Read() {
        using RegistryKey? key = root.OpenSubKey(profilePath);
        return key?.GetValue(ValueName) is int value && value != 0;
    }

    public void Write(bool enabled) {
        using RegistryKey key = root.CreateSubKey(profilePath, writable: true);
        key.SetValue(ValueName, enabled ? 1 : 0, RegistryValueKind.DWord);
    }
}
