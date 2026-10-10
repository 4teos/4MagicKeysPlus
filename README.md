# 4MagicKeys+

Windows driver for Apple Magic Keyboards.

Originally forked from the [samartzidis/winAppleKey](https://github.com/samartzidis/winAppleKey) repository.

### Supported Keyboards

| Model | Name | Connection |
|-------|------|------------|
| A1644 | Apple Magic Keyboard | USB / Bluetooth |
| A1843 | Apple Magic Keyboard with Numeric Keypad | USB / Bluetooth |

### Features

- One list of key rules in the registry (`Parameters\Rules`) replaces all the old remap tables: remap any key to another key, swap modifiers, map Fn/Eject to a modifier or a key (and back), turn a key into a modifier combination, or into a media/brightness/volume action
- Fn layer: any key can have a separate "with Fn" rule (Home/End/Page Up/Down, Insert, Print Screen, etc. are ordinary rules now, nothing is hardcoded)
- `FnLock`-aware rules, so the F-row media keys can behave as media keys by default or as regular function keys, with Fn flipping them
- A small agent (`4MagicKeysPlusAgent`) that launches an application on a hotkey, and warns when the keyboard battery runs low
- A configuration app (`4MagicKeysPlus`) that currently shows the connected keyboard, its battery/connection/Fn Lock state and highlights the remapped keys (editing is not implemented yet)

### Technical Details

4MagicKeys+ consists of four parts:

- **Driver** (`4MagicKeysPlusDriver`) - a HIDCLASS LowerFilter WDM kernel mode driver. It intercepts HID input at the Bluetooth (L2CAP) or USB (URB) level and transforms the 9-byte keyboard report buffer before it reaches the HID class driver. For media/brightness/volume actions it also exposes a virtual Consumer Control HID device (via VHF) that reports the corresponding usage code. All behavior comes from the rules in the registry; the driver has no built-in key tables.
- **Agent** (`4MagicKeysPlusAgent`) - a small WinForms background process (no tray icon): registers global hotkeys for the launch entries in the registry, and polls the keyboard battery.
- **Configuration app** (`4MagicKeysPlus`) - WPF app. Needs the .NET 10 Desktop Runtime.
- **Core** (`4MagicKeysPlusCore`) - class library shared by the app and the agent: finds the connected keyboard over HID and reads its battery.

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

### Agent and Configuration App

Both are framework-dependent .NET 10 apps, so they need the **.NET Desktop Runtime 10** installed. There is no installer yet: copy the build output to a folder of your choice (for the agent: the `.exe`, `.dll`, `.runtimeconfig.json`, `.deps.json`, `4MagicKeysPlus.Core.dll` and the `ru` satellite folder; the configuration app needs the same kind of files plus its own). To start the agent at logon, add a string value with its full path to `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`. The configuration app only reads the registry, so it needs no elevation for now.

### Key Rules

The driver has no built-in key mappings. Everything - Fn combinations, media keys, modifier swaps - is a rule under:

```
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Rules\<subkey>
```

The subkey name does not matter to the driver; the convention is the 8-digit hex of `Source` (`01000041`), which also keeps two rules from sharing a source. Values:

| Value | Type | |
|-------|------|-|
| `Source` | DWORD | what is remapped |
| `Target` | DWORD | what it becomes |
| `FnLockAware` | DWORD | optional, default `0` (see below) |
| `Comment` | SZ | optional, ignored by the driver |

A subkey without both `Source` and `Target` is not a key rule (it may be a launch entry, see [Launch Entries](#launch-entries)). Rules the driver cannot interpret are ignored, as are rules beyond the first 128.

#### Source and Target encoding

Both are the same 32-bit value:

```
bits  0-15   code: a HID key code (see the reference below), F0 = Eject, F1 = Fn,
             or, in a Target with the consumer flag, a Consumer Control usage
bits 16-23   modifier mask (see Modifier Bits below)
bit  24      Source: "only while Fn is held" (Fn layer)
             Target: the code is a Consumer Control usage, not a key
bits 25-31   reserved, a rule with any of these set is ignored
```

Supported rule shapes:

| Source | Target | Example |
|--------|--------|---------|
| key | key (optionally plus modifiers) | `Source=0x68` (F13), `Target=0x46` (Print Screen) |
| key | modifiers only | `Source=0x01000068`, `Target=0x00030000` (Fn+F13 -> Ctrl+Shift) |
| key | consumer usage | `Source=0x01000041`, `Target=0x010000CD` (Fn+F8 -> Play/Pause) |
| modifier(s) | modifier(s) | `Source=0x00040000`, `Target=0x00080000` (Left Alt -> Left Cmd) |
| modifier(s) | Fn / Eject | `Source=0x00010000`, `Target=0xF1` (Left Ctrl -> Fn) |
| Fn / Eject | modifier(s) | `Source=0xF1`, `Target=0x00010000` (Fn -> Left Ctrl) |
| Fn / Eject | Fn / Eject | `Source=0xF0`, `Target=0xF1` (Eject -> Fn) |
| Fn / Eject | key | `Source=0xF1`, `Target=0x49` (Fn -> Insert) |

Not supported: a key as the source together with modifiers (e.g. Cmd+L -> F12), a key mapped to Fn/Eject, and a modifier mapped to a key. Windows receives a modifier before the key that follows it, so a replaced combination would leak the modifier (the Start menu opening on a Win tap, Alt activating menus).

#### How the rules are applied

- Each keyboard report is processed in one pass from its original state. Rules do not chain: a rule producing Fn does not trigger the Fn rules in the same report, and a swap (Alt <-> Cmd) stays a swap.
- Only the source side of a rule is cleared; the target is added.
- A Fn-layer rule (bit 24 in the source) applies while Fn is held and wins over the plain rule of the same key. Otherwise the plain rule applies, so e.g. a plain `F13 -> Print Screen` keeps working while Fn is held.
- `FnLockAware` flips the Fn requirement of a Fn-layer rule while `FnLock` is `0`. With `FnLock=1`, F8 is F8 and Fn+F8 is Play/Pause; with `FnLock=0` it is the other way round. One `Fn+F8 -> Play/Pause` rule with `FnLockAware=1` covers both. Rules without `FnLockAware` ignore `FnLock`.
- If several rules have the same source, the first one found wins. If two media keys are pressed at once, the first one wins and the other one is swallowed.
- Keys produced by Fn/Eject -> key rules are added after the key pass and are not remapped again.

Rules are read once at driver load; see [Driver Settings](#driver-settings) for how to reload.

#### Typical rules

The usual Apple-keyboard set (Fn combinations for the keys a Windows keyboard has, and the F-row actions). `FnLockAware` is `1` for the media rows only:

```
Fn+Left   -> Home           Source=0x01000050  Target=0x0000004A
Fn+Right  -> End            Source=0x0100004F  Target=0x0000004D
Fn+Up     -> Page Up        Source=0x01000052  Target=0x0000004B
Fn+Down   -> Page Down      Source=0x01000051  Target=0x0000004E
Fn+Return -> Insert         Source=0x01000028  Target=0x00000049
Fn+P      -> Print Screen   Source=0x01000013  Target=0x00000046
Fn+S      -> Scroll Lock    Source=0x01000016  Target=0x00000047
Fn+B      -> Pause/Break    Source=0x01000005  Target=0x00000048

Fn+F1  -> Brightness Down   Source=0x0100003A  Target=0x01000070
Fn+F2  -> Brightness Up     Source=0x0100003B  Target=0x0100006F
Fn+F7  -> Previous Track    Source=0x01000040  Target=0x010000B6
Fn+F8  -> Play/Pause        Source=0x01000041  Target=0x010000CD
Fn+F9  -> Next Track        Source=0x01000042  Target=0x010000B5
Fn+F10 -> Mute              Source=0x01000043  Target=0x010000E2
Fn+F11 -> Volume Down       Source=0x01000044  Target=0x010000EA
Fn+F12 -> Volume Up         Source=0x01000045  Target=0x010000E9
```

Consumer usages used above: `6F` Brightness Up, `70` Brightness Down, `B5` Scan Next Track, `B6` Scan Previous Track, `CD` Play/Pause, `E2` Mute, `E9` Volume Up, `EA` Volume Down.

Adding one rule from an administrative command prompt (Fn+F8 -> Play/Pause):

```
reg add "HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Rules\01000041" /v Source /t REG_DWORD /d 0x01000041 /f
reg add "HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Rules\01000041" /v Target /t REG_DWORD /d 0x010000CD /f
reg add "HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Rules\01000041" /v FnLockAware /t REG_DWORD /d 1 /f
```

Other examples: swap Left Alt and Left Cmd needs two rules (`0x00040000 -> 0x00080000` and `0x00080000 -> 0x00040000`); Eject as Fn is `0xF0 -> 0xF1`; Fn typing Insert is `0xF1 -> 0x49`.

The configuration app is not able to edit rules yet. Hand editing is meant for development and for advanced users.

### Launch Entries

The agent starts an application on a hotkey. An entry is a subkey of the same `Rules` key with:

| Value | Type | |
|-------|------|-|
| `VkKey` | DWORD | Windows virtual-key code of the hotkey ([list](https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes)) |
| `VkModifier` | DWORD | optional modifiers, default `0`: `1` Alt, `2` Ctrl, `4` Shift, `8` Win (sum them) |
| `Path` | SZ | what to start: an executable, shortcut, ... |
| `Comment` | SZ | optional label used in the agent log |

Both `VkKey` and `Path` are needed; the driver ignores these subkeys when they have no `Source`/`Target`. A subkey with exactly one of the two gets a warning in the agent log. Example, a calculator on F16 (virtual-key `0x7F`):

```
reg add "HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Rules\Launch_00_7F" /v VkKey /t REG_DWORD /d 0x7F /f
reg add "HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Rules\Launch_00_7F" /v Path /t REG_SZ /d "C:\Windows\System32\calc.exe" /f
```

The agent reads the entries at start and registers the hotkeys; restart it after changes. The agent sees ordinary Windows keystrokes, so Fn cannot be a part of a hotkey directly (map Fn+key to a free key such as F20 with a rule and launch on that). Hotkey registration errors and other problems go to `%LOCALAPPDATA%\4MagicKeysPlus\agent.log`.

### Battery Notice

The agent reads the keyboard battery (the vendor HID report `0x90` of the keyboard, no driver involved) every 15 minutes, the first time 15 minutes after it starts, and shows a small always-on-top notice at the bottom center of the screen when the charge reaches 20% and again at 5%. It does not take focus, closes by itself after a few seconds or on a click, and stays quiet while the keyboard is charging. Plugging the keyboard in re-arms both warnings.

### Driver Settings

All settings are in the registry at:

```
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters
```

Settings and rules are read once at driver load. After changing values, restart the device to reload the driver (Administrative command prompt):

```
pnputil /restart-device /deviceid "BTHENUM\{00001124-0000-1000-8000-00805f9b34fb}_VID&0001004c_PID&026C"
```

Replace the device instance ID with the appropriate one for your keyboard model. You can also disconnect/reconnect the keyboard or reboot.

#### FnLock (DWORD)

Used by rules with `FnLockAware=1` (see [How the rules are applied](#how-the-rules-are-applied)); any non-zero value means on. Does not affect rules that are not `FnLockAware`. Default: `0`.

The older `KeyMap`, `ModMap` and `SpecialModMap` binary values are no longer read and can be deleted.

### HID Key Code Reference

#### Regular Keys

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
53=Num Lock     54=KP /        55=KP *     56=KP -
57=KP +         58=KP Enter    
59=KP 1         5A=KP 2        5B=KP 3
5C=KP 4         5D=KP 5        5E=KP 6
5F=KP 7         60=KP 8        61=KP 9
62=KP 0         63=KP .
```

#### Virtual Codes (Apple-specific keys)

```
F0=Eject    F1=Fn   (as the code of a rule value)
```

#### Modifier Bits (bits 16-23 of a rule value)

The bit goes to the mask shifted by 16: Left Alt (`04`) in a rule value is `0x00040000`.

```
01=Left Ctrl        02=Left Shift
04=Left Alt         08=Left Cmd (Win)
10=Right Ctrl       20=Right Shift
40=Right Alt        80=Right Cmd (Win)
```

### Build Instructions

Requirements:
- Visual Studio 2026 with the **Desktop development with C++** workload
- The **Windows Driver Kit** individual component (installed via the Visual Studio Installer - adds the driver project templates and build tooling)
- A Windows SDK whose build number matches your installed WDK build number exactly (e.g. both `10.0.28000.x`)
- The .NET 10 SDK for the agent, the configuration app and their tests

This project currently targets KMDF 1.35, which ships with the latest WDK (`10.0.28000.x`, for Visual Studio 2026) - it won't build against an older WDK/SDK pair (e.g. the `10.0.26100.x` kit that ships for Visual Studio 2022) without changes. If you want to build on Visual Studio 2022 instead, either install the latest WDK/SDK alongside it, or lower `KMDF_VERSION_MAJOR`/`KMDF_VERSION_MINOR` in `4MagicKeysPlusDriver\4MagicKeysPlusDriver.vcxproj` to a version your installed WDK actually provides (check `C:\Program Files (x86)\Windows Kits\10\Lib\wdf\kmdf\x64\` for the KMDF versions installed on your machine).

Build the `4MagicKeysPlusDriver` project for x64 (Debug or Release). The driver, INF and catalog will be in the output directory. The .NET projects build with `dotnet build`, or from the solution in Visual Studio.

#### Tests

- Driver logic (`KeyProcessor`, compiled into the test project as plain user-mode C++): `4MagicKeysPlusDriverTests`, run from Visual Studio's Test Explorer or `vstest.console.exe` on the built DLL.
- Agent (launch entry loading, battery notice policy): `dotnet test 4MagicKeysPlusAgentTests`.
- Configuration app (rule decoding, tooltips, key names): `dotnet test 4MagicKeysPlusTests`.

The Debug build of the agent accepts two test switches that are not compiled into Release: `--demo-battery <percent> [delay seconds]` shows the battery notice once and exits, `--battery-interval <seconds>` checks the battery that often and logs every reading.

#### Test Certificate

The build test-signs the driver using `4MagicKeysPlusDriver\4MagicKeysPlus.pfx`, which is **not checked into git** (`.pfx` is gitignored, since it bundles the private key). If you get an MSBuild error like `Invalid argument <...4MagicKeysPlus.pfx> for property <TestCertificate>`, generate a new self-signed test certificate locally (PowerShell, run from the `4MagicKeysPlusDriver\` folder):

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
