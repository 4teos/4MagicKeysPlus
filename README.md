# WinAppleKey

Windows driver for Apple Magic Keyboards.

### Supported Keyboards

| Model | Name | Connection |
|-------|------|------------|
| A1644 | Apple Magic Keyboard | USB / Bluetooth |
| A1843 | Apple Magic Keyboard with Numeric Keypad | USB / Bluetooth |

### Features

- Configurable key remapping via registry (`KeyMap`) — remap any key to any other key, including Fn and Eject
- Configurable modifier remapping via registry (`ModMap`) — swap or remap Alt, Cmd, Ctrl, Shift
- Fn Lock mode — makes Fn-combinations (arrows to navigation, F1-F12 to F13-F24) permanent without holding Fn
- Fn-key combinations for missing Windows keys (Delete, Insert, Print Screen, etc.)

### Technical Details

WinAppleKey is a HIDCLASS LowerFilter WDM kernel mode driver. It intercepts HID input at the Bluetooth (L2CAP) or USB (URB) level and transforms the 9-byte keyboard report buffer before it reaches the HID class driver.

![keyboard-driver-stack](keyboard-driver-stack.png)

### Installation

**DISCLAIMER:** This driver is signed with a self-signed (test/development) certificate. Windows will not allow the driver installation unless running in **TESTSIGNING** mode. Permanently running Windows in TESTSIGNING mode leaves your system open to potential security risks. Any consequence is solely your own responsibility. WinAppleKey is ***free software*** that you build and/or use completely ***at your own risk.*** If your system runs UEFI with **Secure Boot** enabled, you will need to disable **Secure Boot** in BIOS first.

1. Enable TESTSIGNING mode (Administrative command prompt), then reboot:

   ```
   bcdedit.exe -set TESTSIGNING ON
   ```

2. Install the test certificate:

   ```
   certutil -addstore "TrustedPublisher" WinAppleKey.cer
   certutil -addstore "Root" WinAppleKey.cer
   ```

3. Install the driver via Device Manager:
   - Find your Apple keyboard under **Human Interface Devices**
   - Right-click → **Update driver** → **Browse my computer** → **Let me pick from a list** → **Have Disk...**
   - Point to the directory with `WinAppleKey.inf` and `WinAppleKey.sys`

4. Disconnect and reconnect the keyboard (or reboot).

To uninstall, revert to the default driver via Device Manager, then disable TESTSIGNING:

```
bcdedit.exe -set TESTSIGNING OFF
```

### Default Key Mappings

These Fn-combinations are always active (built into the driver):

```
Fn + Left       ->  Home
Fn + Right      ->  End
Fn + Up         ->  Page Up
Fn + Down       ->  Page Down
Fn + Return     ->  Insert
Fn + F1...F12   ->  F13...F24
Fn + P          ->  Print Screen
Fn + S          ->  Scroll Lock
Fn + B          ->  Pause/Break
Fn + LCtrl      ->  Right Ctrl
```

The default `KeyMap` maps Eject → Delete.

### Driver Settings

All settings are in the registry at:

```
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WinAppleKey\Parameters
```

Settings are read once at driver load. After changing values, restart the device to reload the driver (Administrative command prompt):

```
pnputil /restart-device "BTHENUM\{00001124-0000-1000-8000-00805f9b34fb}_VID&0001004c_PID&026C"
```

Replace the device instance ID with the appropriate one for your keyboard model. You can also disconnect/reconnect the keyboard or reboot.

#### FnLock (DWORD)

When set to `1`, Fn-combinations are always active without holding the Fn key. Regular keys that don't have a Fn-combination still work normally. Default: `0`.

#### KeyMap (REG_BINARY)

Pairs of bytes `[source, target, source, target, ...]` that remap keycodes. Applied to all 6 key slots in the HID report.

Special virtual codes for keys in the Apple-specific byte:
- `F0` — Eject key
- `F1` — Fn key

When Fn is mapped to a key via KeyMap, it sends that key instead of acting as a modifier (Fn-combinations are still available via FnLock).

Default: `F0 4C` (Eject → Delete).

Example — remap F13/F14/F15 and Fn:
```
KeyMap = hex:F0,4C,F1,49,68,46,69,47,6A,48
```
Means: Eject→Delete, Fn→Insert, F13→PrtScr, F14→ScrLck, F15→Pause/Break.

#### ModMap (REG_BINARY)

Pairs of bytes `[source_bit, target_bit, ...]` that remap modifier keys. Applied to the modifier bitmask byte.

Example — swap Alt and Cmd (both sides):
```
ModMap = hex:04,08,08,04,40,80,80,40
```

Default: not set (no modifier remapping).

### HID Key Code Reference

#### Regular Keys (for KeyMap)

```
04=A    05=B    06=C    07=D    08=E    09=F    0A=G    0B=H
0C=I    0D=J    0E=K    0F=L    10=M    11=N    12=O    13=P
14=Q    15=R    16=S    17=T    18=U    19=V    1A=W    1B=X
1C=Y    1D=Z

1E=1    1F=2    20=3    21=4    22=5    23=6    24=7    25=8
26=9    27=0

28=Enter        29=Escape       2A=Backspace    2B=Tab
2C=Space        2D=Minus        2E=Equals       2F=[
30=]            31=\            33=;            34='
35=`            36=,            37=.            38=/
39=Caps Lock
```

#### Function Keys

```
3A=F1   3B=F2   3C=F3   3D=F4   3E=F5   3F=F6
40=F7   41=F8   42=F9   43=F10  44=F11  45=F12

68=F13  69=F14  6A=F15  6B=F16  6C=F17  6D=F18
6E=F19  6F=F20  70=F21  71=F22  72=F23  73=F24
```

#### Navigation & Editing Keys

```
46=Print Screen     47=Scroll Lock      48=Pause/Break
49=Insert           4A=Home             4B=Page Up
4C=Delete           4D=End              4E=Page Down
4F=Right Arrow      50=Left Arrow       51=Down Arrow
52=Up Arrow
```

#### Numeric Keypad

```
53=Num Lock     54=KP /     55=KP *     56=KP -
57=KP +         58=KP Enter
59=KP 1     5A=KP 2     5B=KP 3
5C=KP 4     5D=KP 5     5E=KP 6
5F=KP 7     60=KP 8     61=KP 9
62=KP 0     63=KP .
```

#### Virtual Codes (Apple-specific keys)

```
F0=Eject    F1=Fn
```

#### Modifier Bits (for ModMap)

```
01=Left Ctrl        02=Left Shift
04=Left Alt         08=Left Cmd (Win)
10=Right Ctrl       20=Right Shift
40=Right Alt        80=Right Cmd (Win)
```

### Build Instructions

Requirements:
- Visual Studio 2019+ with C++ desktop development workload
- Windows Driver Kit (WDK) matching your Windows SDK version

Build the `WinAppleKey` project for x64 (Debug or Release). The driver, INF and catalog will be in the output directory.

To run tests, build and run the `Tests` project — it validates `KeyProcessor.c` logic without requiring driver installation.
