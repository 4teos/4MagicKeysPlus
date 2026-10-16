using Microsoft.Win32;

namespace _4MagicKeysPlus.Services;

// Where the driver's Fn Lock is kept: the FnLock DWORD of its Parameters key.
internal interface IFnLockStore {
    bool Read();

    void Write(bool enabled);
}

internal sealed class RegistryFnLockStore : IFnLockStore {
    private const string ValueName = "FnLock";

    private readonly RegistryKey root;
    private readonly string parametersPath;

    public RegistryFnLockStore() : this(Registry.LocalMachine, DriverStatus.ParametersKeyPath) {
    }

    // The root and path are parameters so that tests can use a scratch key of their own.
    public RegistryFnLockStore(RegistryKey root, string parametersPath) {
        this.root = root;
        this.parametersPath = parametersPath;
    }

    public bool Read() {
        using RegistryKey? key = root.OpenSubKey(parametersPath);
        return key?.GetValue(ValueName) is int value && value != 0;
    }

    public void Write(bool enabled) {
        using RegistryKey key = root.CreateSubKey(parametersPath, writable: true);
        key.SetValue(ValueName, enabled ? 1 : 0, RegistryValueKind.DWord);
    }
}
