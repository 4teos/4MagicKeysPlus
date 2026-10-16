using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace _4MagicKeysPlus.Services;

internal static class AppIcons {
    // The icon Explorer shows for the file; null when there is none (the file is missing, or
    // Path is not a file at all).
    public static ImageSource? Load(string path) {
        try {
            if (!File.Exists(path)) {
                return null;
            }
            using System.Drawing.Icon? icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null) {
                return null;
            }
            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) {
            return null;
        }
    }
}
