namespace _4MagicKeysPlus.Core;

public static class KeyboardDetector {
    public static DetectedKeyboard? DetectConnectedKeyboard() {
        LiveKeyboardInterface? live = LiveKeyboardScanner.Scan().FirstOrDefault();
        return live is null ? null : new DetectedKeyboard(live.Product, live.Connection, live.DevicePath);
    }
}
