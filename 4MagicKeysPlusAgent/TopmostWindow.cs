namespace _4MagicKeysPlusAgent;

internal static class TopmostWindow {
    public enum Result {
        Direct,
        ViaAttachedInput,
        Denied,
    }

    // WS_EX_TOPMOST given at creation is silently dropped when the process has no foreground rights, which is the agent's situation when a notice is
    // due minutes after logon while some other (often fullscreen) window is in front: Windows leaves the popup under it. Re-asserting the
    // position after sharing the foreground thread's input state is the known way past this.
    public static Result Ensure(IntPtr hWnd) {
        if (Apply(hWnd)) {
            return Result.Direct;
        }

        uint foregroundThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
        uint ourThread = NativeMethods.GetCurrentThreadId();
        if (foregroundThread == 0 || foregroundThread == ourThread || !NativeMethods.AttachThreadInput(ourThread, foregroundThread, true)) {
            return Result.Denied;
        }

        try {
            return Apply(hWnd) ? Result.ViaAttachedInput : Result.Denied;
        } finally {
            NativeMethods.AttachThreadInput(ourThread, foregroundThread, false);
        }
    }

    private static bool Apply(IntPtr hWnd) {
        NativeMethods.SetWindowPos(hWnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        return (NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt64() & NativeMethods.WS_EX_TOPMOST) != 0;
    }
}
