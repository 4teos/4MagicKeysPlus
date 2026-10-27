namespace _4MagicKeysPlus.Core;

// One way a keyboard reaches this PC: a USB cable or a Bluetooth pairing (a keyboard that has both is two endpoints).
// IsLive: the cable is plugged in, or the Bluetooth link is up (a pairing is known to Windows also while the link is down).
// DevicePath: the HID interface that answers the battery report, only for a live endpoint.
public sealed record KeyboardEndpoint(KeyboardIdentity Identity, KeyboardProduct Product, bool IsLive, string? DevicePath);
