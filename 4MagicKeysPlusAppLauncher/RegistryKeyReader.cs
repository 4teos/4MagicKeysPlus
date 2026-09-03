using Microsoft.Win32;

namespace _4MagicKeysPlusAppLauncher;

internal interface IRegistryKeyReader : IDisposable {
    IEnumerable<string> GetSubKeyNames();
    IRegistryKeyReader? OpenSubKey(string name);
    object? GetValue(string name);
}

internal sealed class Win32RegistryKeyReader : IRegistryKeyReader {
    private readonly RegistryKey _key;

    public Win32RegistryKeyReader(RegistryKey key) {
        _key = key;
    }

    public IEnumerable<string> GetSubKeyNames() {
        return _key.GetSubKeyNames();
    }

    public IRegistryKeyReader? OpenSubKey(string name) {
        RegistryKey? subKey = _key.OpenSubKey(name);
        return subKey is null ? null : new Win32RegistryKeyReader(subKey);
    }

    public object? GetValue(string name) {
        return _key.GetValue(name);
    }

    public void Dispose() {
        _key.Dispose();
    }
}
