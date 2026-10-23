using Microsoft.Win32;

namespace _4MagicKeysPlusTests;

// A key of its own under HKCU\Software\4MagicKeysPlusTests, deleted with the test (and the
// parent too, once it is left empty).
internal sealed class ScratchKey : IDisposable {
    private const string Root = @"Software\4MagicKeysPlusTests";
    private readonly string folder = $@"{Root}\{Guid.NewGuid():N}";

    public string Path(string name) {
        return $@"{folder}\{name}";
    }

    public RegistryKey Create(string name) {
        return Registry.CurrentUser.CreateSubKey(Path(name), writable: true);
    }

    public RegistryKey? Open(string name) {
        return Registry.CurrentUser.OpenSubKey(Path(name), writable: true);
    }

    public void Dispose() {
        Registry.CurrentUser.DeleteSubKeyTree(folder, throwOnMissingSubKey: false);
        using RegistryKey? root = Registry.CurrentUser.OpenSubKey(Root);
        if (root?.SubKeyCount == 0) {
            Registry.CurrentUser.DeleteSubKey(Root, throwOnMissingSubKey: false);
        }
    }
}
