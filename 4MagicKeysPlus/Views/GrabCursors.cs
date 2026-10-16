using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32.SafeHandles;

namespace _4MagicKeysPlus.Views;

// Windows has no cursors for grabbing: an open hand and a fist are made from the pictures in
// Resources (embedded under their file names), scaled to the cursor size of the screen.
internal static class GrabCursors {
    private const int BaseCursorSize = 32;
    private const string OpenHandPicture = "HandOpen.png";
    private const string ClosedHandPicture = "HandClosed.png";

    public static Cursor Open { get; } = Create(OpenHandPicture);

    public static Cursor Closed { get; } = Create(ClosedHandPicture);

    internal static BitmapSource Draw(bool closed, int size) {
        return Draw(closed ? ClosedHandPicture : OpenHandPicture, size);
    }

    private static BitmapSource Draw(string picture, int size) {
        BitmapImage image = new();
        using (Stream stream = typeof(GrabCursors).Assembly.GetManifestResourceStream(picture)
            ?? throw new InvalidOperationException($"The resource {picture} is missing")) {
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
        }
        image.Freeze();

        DrawingVisual visual = new();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (DrawingContext context = visual.RenderOpen()) {
            context.DrawImage(image, new Rect(0, 0, size, size));
        }

        RenderTargetBitmap bitmap = new(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static Cursor Create(string picture) {
        int size = (int)Math.Round(BaseCursorSize * GetDpiForSystem() / 96.0);
        byte[] pixels = new byte[size * size * 4];
        Draw(picture, size).CopyPixels(pixels, size * 4, 0);

        // The bitmap is premultiplied, GDI+ wants straight alpha.
        for (int i = 0; i < pixels.Length; i += 4) {
            byte alpha = pixels[i + 3];
            if (alpha is > 0 and < 255) {
                for (int channel = 0; channel < 3; channel++) {
                    pixels[i + channel] = (byte)Math.Min(255, pixels[i + channel] * 255 / alpha);
                }
            }
        }

        using System.Drawing.Bitmap bitmap = new(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        System.Drawing.Imaging.BitmapData data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, size, size),
            System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        bitmap.UnlockBits(data);

        IntPtr icon = bitmap.GetHicon();
        try {
            if (!GetIconInfo(icon, out IconInfo info)) {
                throw new InvalidOperationException("GetIconInfo failed");
            }
            info.IsIcon = false;
            info.HotspotX = size / 2;
            info.HotspotY = size / 2;
            IntPtr cursor = CreateIconIndirect(ref info);
            DeleteObject(info.Mask);
            DeleteObject(info.Color);
            return CursorInteropHelper.Create(new SafeCursorHandle(cursor));
        } finally {
            DestroyIcon(icon);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo {
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsIcon;
        public int HotspotX;
        public int HotspotY;
        public IntPtr Mask;
        public IntPtr Color;
    }

    // The cursors live as long as the app does, so the handle is never released.
    private sealed class SafeCursorHandle : SafeHandleZeroOrMinusOneIsInvalid {
        public SafeCursorHandle(IntPtr handle) : base(ownsHandle: false) {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle() {
            return true;
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetDpiForSystem();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetIconInfo(IntPtr icon, out IconInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconIndirect(ref IconInfo info);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);
}
