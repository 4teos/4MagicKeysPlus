using System.Runtime.InteropServices;

namespace _4MagicKeysPlusAgent;

// A hidden, message-only window (HWND_MESSAGE parent) - just enough to receive WM_HOTKEY,
// never actually shown on screen.
internal sealed class HotkeyWindow : NativeWindow, IHotkeyRegistrar, IDisposable {
    private readonly List<int> _registeredIds = [];

    public event Action<int>? HotKeyPressed;

    public HotkeyWindow() {
        CreateParams cp = new CreateParams { Parent = new IntPtr(-3) }; // HWND_MESSAGE
        CreateHandle(cp);
    }

    public bool RegisterHotKey(HotkeyEntry entry, out int win32Error) {
        win32Error = 0;
        if (!NativeMethods.RegisterHotKey(Handle, entry.HotkeyId, entry.Modifiers | NativeMethods.MOD_NOREPEAT, entry.VirtualKey)) {
            win32Error = Marshal.GetLastWin32Error();
            return false;
        }

        _registeredIds.Add(entry.HotkeyId);
        return true;
    }

    public void UnregisterAll() {
        foreach (int id in _registeredIds) {
            NativeMethods.UnregisterHotKey(Handle, id);
        }

        _registeredIds.Clear();
    }

    protected override void WndProc(ref Message m) {
        if (m.Msg == NativeMethods.WM_HOTKEY) {
            HotKeyPressed?.Invoke((int)m.WParam);
        }

        base.WndProc(ref m);
    }

    public void Dispose() {
        UnregisterAll();
        DestroyHandle();
    }
}
