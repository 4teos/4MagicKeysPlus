# 4MagicKeys+

Windows driver for Apple Magic Keyboards.

### Supported Keyboards

| Model | Name | Connection |
|-------|------|------------|
| A1644 | Apple Magic Keyboard | USB / Bluetooth |
| A1843 | Apple Magic Keyboard with Numeric Keypad | USB / Bluetooth |

### Features

- Configurable key remapping via registry (`KeyMap`) — remap any key to any other key, including Fn and Eject
- Configurable modifier remapping via registry (`ModMap`) — swap or remap Alt, Cmd, Ctrl, Shift
- Configurable Fn/Eject ↔ modifier remapping via registry (`SpecialModMap`) — e.g. swap Fn and Left Ctrl
- Fn-key combinations for missing Windows keys (Home, End, Page Up/Down, Insert, Print Screen, Scroll Lock, Pause/Break)
- F1-F12 act as media/brightness/volume keys by default, or as regular function keys with `FnLock` set (and vice versa while holding Fn)

### Technical Details

4MagicKeys+ is a HIDCLASS LowerFilter WDM kernel mode driver. It intercepts HID input at the Bluetooth (L2CAP) or USB (URB) level and transforms the 9-byte keyboard report buffer before it reaches the HID class driver. For the F1-F12 media/brightness/volume actions it also exposes a virtual Consumer Control HID device (via VHF) that reports the corresponding usage code.

### Installation

**DISCLAIMER:** This driver is signed with a self-signed (test/development) certificate. Windows will not allow the driver installation unless running in **TESTSIGNING** mode. Permanently running Windows in TESTSIGNING mode leaves your system open to potential security risks. Any consequence is solely your own responsibility. 4MagicKeys+ is ***free software*** that you build and/or use completely ***at your own risk.*** If your system runs UEFI with **Secure Boot** enabled, you will need to disable **Secure Boot** in BIOS first.

1. Enable TESTSIGNING mode (Administrative command prompt), then reboot:

   ```
   bcdedit.exe -set TESTSIGNING ON
   ```

2. Install the test certificate:

   ```
   certutil -addstore "TrustedPublisher" 4MagicKeysPlus.cer
   certutil -addstore "Root" 4MagicKeysPlus.cer
   ```

3. Install the driver via Device Manager:
   - Find your Apple keyboard under **Human Interface Devices**
   - Right-click → **Update driver** → **Browse my computer** → **Let me pick from a list** → **Have Disk...**
   - Point to the directory with `4MagicKeysPlus.inf` and `4MagicKeysPlus.sys`

4. Disconnect and reconnect the keyboard (or reboot).

To uninstall, revert to the default driver via Device Manager, then disable TESTSIGNING:

```
bcdedit.exe -set TESTSIGNING OFF
```

### Default Key Mappings

These Fn-combinations are always active (built into the driver, independent of `FnLock`):

```
Fn + Left       ->  Home
Fn + Right      ->  End
Fn + Up         ->  Page Up
Fn + Down       ->  Page Down
Fn + Return     ->  Insert
Fn + P          ->  Print Screen
Fn + S          ->  Scroll Lock
Fn + B          ->  Pause/Break
```

### Multimedia Keys (F1-F12)

F1-F12 report a Consumer Control usage instead of their regular HID code by default; F3-F6 have no assigned action and always behave as regular function keys.

```
F1  ->  Brightness Down
F2  ->  Brightness Up
F7  ->  Previous Track
F8  ->  Play/Pause
F9  ->  Next Track
F10 ->  Mute
F11 ->  Volume Down
F12 ->  Volume Up
```

Holding Fn while pressing an F-key sends the regular F1-F12 code instead. Setting `FnLock` to `1` swaps the default: F-keys behave as regular function keys, and holding Fn gives the media action.

No default `KeyMap` is set out of the box (nothing to remap unless you configure one).

### Driver Settings

All settings are in the registry at:

```
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters
```

Settings are read once at driver load. After changing values, restart the device to reload the driver (Administrative command prompt):

```
pnputil /restart-device "BTHENUM\{00001124-0000-1000-8000-00805f9b34fb}_VID&0001004c_PID&026C"
```

Replace the device instance ID with the appropriate one for your keyboard model. You can also disconnect/reconnect the keyboard or reboot.

#### FnLock (DWORD)

Controls the default behavior of F1-F12 (see [Multimedia Keys](#multimedia-keys-f1-f12)). When `0` (default), F-keys act as media/brightness/volume controls by default and send their regular F1-F12 code while Fn is held. When set to `1`, this is reversed: F-keys act as regular function keys by default, and holding Fn gives the media action instead. Does not affect the other Fn-combinations (Home, End, Page Up/Down, etc.), which always require holding Fn. Default: `0`.

#### KeyMap (REG_BINARY)

Pairs of bytes `[source, target, source, target, ...]` that remap keycodes. Applied to all 6 key slots in the HID report.

Special virtual codes for keys in the Apple-specific byte:
- `F0` — Eject key
- `F1` — Fn key

When Fn is mapped to a key via KeyMap, it sends that key instead of acting as a modifier.

Default: none (empty).

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

#### SpecialModMap (REG_BINARY)

Pairs of bytes `[source, target, ...]` that remap between a real modifier bit and the virtual Fn (`F1`) / Eject (`F0`) codes. Each pair has exactly one modifier-bit side and one virtual-code side; the direction is inferred per pair, so a bidirectional swap needs both pairs (modifier→virtual and virtual→modifier).

Example — swap Fn and Left Ctrl (both directions):
```
SpecialModMap = hex:01,F1,F1,01
```
Means: Left Ctrl press acts as Fn, and Fn press acts as Left Ctrl.

Default: not set (no remapping).

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
- Visual Studio 2026 with the **Desktop development with C++** workload
- The **Windows Driver Kit** individual component (installed via the Visual Studio Installer — adds the driver project templates and build tooling)
- A Windows SDK whose build number matches your installed WDK build number exactly (e.g. both `10.0.28000.x`)

This project currently targets KMDF 1.35, which ships with the latest WDK (`10.0.28000.x`, for Visual Studio 2026) — it won't build against an older WDK/SDK pair (e.g. the `10.0.26100.x` kit that ships for Visual Studio 2022) without changes. If you want to build on Visual Studio 2022 instead, either install the latest WDK/SDK alongside it, or lower `KMDF_VERSION_MAJOR`/`KMDF_VERSION_MINOR` in `4MagicKeysPlus\4MagicKeysPlus.vcxproj` to a version your installed WDK actually provides (check `C:\Program Files (x86)\Windows Kits\10\Lib\wdf\kmdf\x64\` for the KMDF versions installed on your machine).

Build the `4MagicKeysPlus` project for x64 (Debug or Release). The driver, INF and catalog will be in the output directory.

#### Test Certificate

The build test-signs the driver using `4MagicKeysPlus\4MagicKeysPlus.pfx`, which is **not checked into git** (`.pfx` is gitignored, since it bundles the private key). If you get an MSBuild error like `Invalid argument <...4MagicKeysPlus.pfx> for property <TestCertificate>`, generate a new self-signed test certificate locally (PowerShell, run from the `4MagicKeysPlus\` folder):

```powershell
$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=4MagicKeysPlus" -CertStoreLocation Cert:\CurrentUser\My -HashAlgorithm SHA256 -KeyExportPolicy Exportable -Provider "Microsoft Strong Cryptographic Provider" -NotAfter (Get-Date "2040-01-01")
Export-Certificate -Cert $cert -FilePath 4MagicKeysPlus.cer
[System.IO.File]::WriteAllBytes("4MagicKeysPlus.pfx", $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, ""))
Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force
certutil -addstore "TrustedPublisher" 4MagicKeysPlus.cer
certutil -addstore "Root" 4MagicKeysPlus.cer
```

Notes:
- The `-Provider "Microsoft Strong Cryptographic Provider"` flag is required. Without it, `New-SelfSignedCertificate` defaults to a CNG key, and the resulting `.pfx` fails the build with `error : Invalid certificate or password` (from `WindowsDriver.common.targets`'s `SignTask`) even though the password is correct — the WDK's driver-signing tooling expects a legacy CAPI-backed key.
