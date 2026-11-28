using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using _4MagicKeysPlus.Core;

namespace _4MagicKeysPlus.Services;

internal readonly record struct WindowBounds(int Left, int Top, int Right, int Bottom) {
    public bool IsEmpty => Right <= Left || Bottom <= Top;

    public override string ToString() {
        return string.Create(CultureInfo.InvariantCulture, $"{Left},{Top},{Right},{Bottom}");
    }

    public static WindowBounds? Parse(string? text) {
        string[] parts = text?.Split(',') ?? [];
        int[] numbers = new int[4];
        if (parts.Length != numbers.Length) {
            return null;
        }
        for (int i = 0; i < numbers.Length; i++) {
            if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i])) {
                return null;
            }
        }

        WindowBounds bounds = new(numbers[0], numbers[1], numbers[2], numbers[3]);
        return bounds.IsEmpty ? null : bounds;
    }
}

// A lost position only means the window opens where Windows puts it, so registry trouble is never an error here.
internal sealed class RegistryWindowPlacementStore {
    private const string ValueName = "WindowPlacement";

    private readonly RegistryKey root;
    private readonly string keyPath;

    public RegistryWindowPlacementStore() : this(Registry.CurrentUser, AppSettingsRegistry.KeyPath) {
    }

    public RegistryWindowPlacementStore(RegistryKey root, string keyPath) {
        this.root = root;
        this.keyPath = keyPath;
    }

    public WindowBounds? Read() {
        try {
            using RegistryKey? key = root.OpenSubKey(keyPath);
            return WindowBounds.Parse(key?.GetValue(ValueName) as string);
        } catch (Exception exception) when (exception is IOException or SecurityException or UnauthorizedAccessException) {
            return null;
        }
    }

    public void Write(WindowBounds bounds) {
        try {
            using RegistryKey key = root.CreateSubKey(keyPath, writable: true);
            key.SetValue(ValueName, bounds.ToString(), RegistryValueKind.String);
        } catch (Exception exception) when (exception is IOException or SecurityException or UnauthorizedAccessException) {
        }
    }
}

// The normal (restored) rectangle of a window. Windows itself moves a restored window back onto a screen when the one it was on is gone.
internal static class WindowPlacement {
    private const int SW_HIDE = 0;

    public static WindowBounds? Get(IntPtr window) {
        WINDOWPLACEMENT placement = New();
        if (!GetWindowPlacement(window, ref placement)) {
            return null;
        }

        RECT r = placement.rcNormalPosition;
        WindowBounds bounds = new(r.Left, r.Top, r.Right, r.Bottom);
        return bounds.IsEmpty ? null : bounds;
    }

    // For a window that is not shown yet: it keeps hidden and opens later at these bounds.
    public static void Set(IntPtr window, WindowBounds bounds) {
        WINDOWPLACEMENT placement = New();
        if (!GetWindowPlacement(window, ref placement)) {
            return;
        }

        placement.flags = 0;
        placement.showCmd = SW_HIDE;
        placement.rcNormalPosition = new RECT { Left = bounds.Left, Top = bounds.Top, Right = bounds.Right, Bottom = bounds.Bottom };
        SetWindowPlacement(window, ref placement);
    }

    private static WINDOWPLACEMENT New() {
        return new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowPlacement(IntPtr window, ref WINDOWPLACEMENT placement);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPlacement(IntPtr window, ref WINDOWPLACEMENT placement);
}
