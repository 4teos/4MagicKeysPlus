using System.Diagnostics;
using System.IO;

namespace _4MagicKeysPlus.Services;

internal static class HotkeyNames {
    // What an entry is called until the user names it: the product name of an executable, or
    // the file name (a shortcut says nothing about the program it points to).
    public static string FromPath(string path) {
        try {
            if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && File.Exists(path)) {
                string? product = FileVersionInfo.GetVersionInfo(path).ProductName;
                if (!string.IsNullOrWhiteSpace(product)) {
                    return product.Trim();
                }
            }
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException) {
            // Unreadable version info is no reason to fail: the file name will do.
        }

        string name = Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }
}
