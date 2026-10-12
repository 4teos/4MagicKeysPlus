#if DEBUG
using System.Runtime.InteropServices;
using System.Text;

namespace _4MagicKeysPlusAgent;

// Debug-only: writes to the log which visible top-level windows sit above the popup in the
// z-order, to find out who covers it (e.g. a fullscreen browser).
internal static class ZOrderDiagnostics {
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOPMOST = 0x8;
    private const int MaxListed = 15;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    public static void Log(IntPtr popup) {
        List<string> above = [];
        EnumWindows((hWnd, _) => {
            if (hWnd == popup) {
                return false;
            }

            if (IsWindowVisible(hWnd) && GetWindowRect(hWnd, out RECT rect) && rect.Right > rect.Left && rect.Bottom > rect.Top) {
                above.Add(Describe(hWnd));
            }

            return true;
        }, IntPtr.Zero);

        Logger.Info($"Popup {Describe(popup)}");
        Logger.Info($"Foreground: {Describe(GetForegroundWindow())}");
        Logger.Info($"Visible windows above the popup: {above.Count}");
        foreach (string line in above.Take(MaxListed)) {
            Logger.Info($"  above: {line}");
        }
    }

    private static string Describe(IntPtr hWnd) {
        StringBuilder title = new StringBuilder(128);
        StringBuilder className = new StringBuilder(128);
        GetWindowText(hWnd, title, title.Capacity);
        GetClassName(hWnd, className, className.Capacity);
        GetWindowRect(hWnd, out RECT rect);
        GetWindowThreadProcessId(hWnd, out uint pid);
        bool topmost = (GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64() & WS_EX_TOPMOST) != 0;
        return $"0x{hWnd:X} class={className} title='{title}' pid={pid} topmost={topmost} rect=({rect.Left},{rect.Top},{rect.Right},{rect.Bottom})";
    }
}
#endif
