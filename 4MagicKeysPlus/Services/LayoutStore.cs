using Microsoft.Win32;
using _4MagicKeysPlus.Models;

namespace _4MagicKeysPlus.Services;

internal interface ILayoutStore {
    LayoutStandard Read();

    void Write(LayoutStandard standard);
}

// The picture only: the driver never reads it. A profile without the value is drawn as ISO.
internal sealed class RegistryLayoutStore : ILayoutStore {
    private const string ValueName = "Layout";

    private readonly RegistryKey root;
    private readonly string profilePath;

    public RegistryLayoutStore(RegistryKey root, string profilePath) {
        this.root = root;
        this.profilePath = profilePath;
    }

    public LayoutStandard Read() {
        using RegistryKey? key = root.OpenSubKey(profilePath);
        return Enum.TryParse(key?.GetValue(ValueName) as string, ignoreCase: true, out LayoutStandard standard) && Enum.IsDefined(standard)
            ? standard
            : LayoutStandard.Iso;
    }

    public void Write(LayoutStandard standard) {
        using RegistryKey key = root.CreateSubKey(profilePath, writable: true);
        key.SetValue(ValueName, standard.ToString(), RegistryValueKind.String);
    }
}
