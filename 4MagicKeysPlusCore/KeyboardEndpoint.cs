namespace _4MagicKeysPlus.Core;

// One way a keyboard reaches the PC: a cable or a pairing (one keyboard may have both). IsLive: the cable is plugged in or the Bluetooth link
// is up (Windows knows a pairing also while the link is down). DevicePath, of a live endpoint only: the HID interface that answers the battery report.
public sealed record KeyboardEndpoint(KeyboardIdentity Identity, KeyboardProduct Product, bool IsLive, string? DevicePath);
