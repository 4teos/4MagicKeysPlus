# 4MagicKeys+

Windows driver for Apple Magic Keyboards.

Originally forked from the [samartzidis/winAppleKey](https://github.com/samartzidis/winAppleKey) repository.

### Supported Keyboards

| Model | Name | Connection |
|-------|------|------------|
| A1644 | Apple Magic Keyboard | USB / Bluetooth |
| A1843 | Apple Magic Keyboard with Numeric Keypad | USB / Bluetooth |

### Features

- One list of key rules in the registry (per keyboard, see [Keyboard Profiles](#keyboard-profiles)) replaces all the old remap tables: remap any key to another key, swap modifiers, map Fn/Eject to a modifier or a key (and back), turn a key into a modifier combination, or into a media/brightness/volume action
- Fn layer: any key can have a separate "with Fn" rule (Home/End/Page Up/Down, Insert, Print Screen, etc. are ordinary rules now, nothing is hardcoded)
- `FnLock`-aware rules, so the F-row media keys can behave as media keys by default or as regular function keys, with Fn flipping them
- A small agent (`4MagicKeysPlusAgent`) that launches an application on a hotkey, and warns when the keyboard battery runs low
- A configuration app (`4MagicKeysPlus`) that shows the connected keyboard, its battery/connection/Fn Lock state, highlights the remapped keys and lets you edit the rules on a picture of the keyboard. Changes are saved to the registry and applied by the driver at once, no restart needed
- A Settings screen in the configuration app: theme (system, dark or light), language, whether the agent runs (with a button to start it) and whether it starts with Windows, and whether the driver and the keyboard are there

### Technical Details

4MagicKeys+ consists of four parts:

- **Driver** (`4MagicKeysPlusDriver`) - a HIDCLASS LowerFilter WDM kernel mode driver. It intercepts HID input at the Bluetooth (L2CAP) or USB (URB) level and transforms the 9-byte keyboard report buffer before it reaches the HID class driver. For media/brightness/volume actions it also exposes a virtual Consumer Control HID device (via VHF) that reports the corresponding usage code. All behavior comes from the rules in the registry, in the profile of the keyboard; the driver has no built-in key tables. While a keyboard is connected the driver also has a control device that makes it re-read the rules without a restart (see [Applying Changes](#applying-changes)).
- **Agent** (`4MagicKeysPlusAgent`) - a small WinForms background process (no tray icon): registers global hotkeys for the launch entries in the registry, and polls the keyboard battery.
- **Configuration app** (`4MagicKeysPlus`) - WPF app. Needs the .NET 10 Desktop Runtime and runs as administrator (see [Agent and Configuration App](#agent-and-configuration-app)).
- **Core** (`4MagicKeysPlusCore`) - class library shared by the app and the agent: finds the keyboards (over PnP and HID), works out their identities (see [Keyboard Profiles](#keyboard-profiles)) and reads the battery.

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
   - Point to the directory with `4MagicKeysPlus.inf` and `4MagicKeysPlus.sys` (`bin\Release\driver` after a Release build)

4. Disconnect and reconnect the keyboard (or reboot).

To uninstall, revert to the default driver via Device Manager, then disable TESTSIGNING:

```
bcdedit.exe -set TESTSIGNING OFF
```

### Agent and Configuration App

Both are framework-dependent .NET 10 apps, so they need the **.NET Desktop Runtime 10** installed. There is no installer yet: copy the build output (`bin\Release\` in the root of the repository, without its `driver` subfolder) to a folder of your choice. The agent and the configuration app need the same kind of files (the `.exe`, `.dll`, `.runtimeconfig.json`, `.deps.json`, `4MagicKeysPlus.Core.dll` and the `ru` satellite folder), so they can run from one folder. To start the agent at logon, turn on **Start with Windows** on the Settings screen of the configuration app (see below), or add a string value named `4MagicKeysPlusAgent` with its full path to `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` by hand.

#### Editing rules in the configuration app

The configuration app asks for administrator rights when it starts: it writes the rules under `HKEY_LOCAL_MACHINE` and talks to the driver's control device, and both need them. (When debugging it from Visual Studio, start Visual Studio as administrator.)

- Click a key on the picture to select it. The panel below shows the key and its current rules; click the empty space (or the key again) to unselect it. Keys with a rule are highlighted, and a tooltip shows what they do.
- **Set mapping** switches to picking the target: the current rules are hidden, the keys that cannot be a target are dimmed, and the keys this keyboard does not have (F20-F24, Print Screen, Home/End on the keyboard without them, ...) and the media keys (play/pause, tracks, volume, brightness) appear in a row of extra keys under the picture. Click the target, or **Cancel** (or Esc) to leave the picking.
- **Only with Fn** makes it a rule of the Fn layer (and changes which targets are allowed; you can switch it while picking). **Follow Fn Lock** makes that rule `FnLockAware` and only applies to the Fn layer. When a key can be remapped in one layer only, the first option is fixed.
- **Remove mapping** deletes every rule of the key, in both layers. Be careful with the F-row: removing the Fn+F8 rule also removes the media action, and there is no "reset to defaults" yet.
- Setting a mapping replaces only the rule of the same key and layer; the key's rules of the other layer stay.
- Every change is saved to `Parameters\Rules` and the driver is told to reload at once. The glyph at the right of the panel shows how that went: a green check, a yellow triangle (saved, but the driver did not take the rules: no keyboard is connected, the driver is too old to reload, or it ignored some rules; they are loaded when the keyboard connects), or a red cross (nothing was saved). Hover it for the details.

The configuration app only edits key rules. The launch entries and `FnLock` still have to be set in the registry.

#### Settings screen

The gear in the header opens it. The settings of the app itself are per user, in `HKEY_CURRENT_USER\Software\4MagicKeysPlus` (the agent reads the language from it): `Theme` (`System`, `Dark` or `Light`) and `Language` (a language code such as `ru` or `de`; no value means the language of Windows).

- **Theme** applies at once. *System* follows the app mode of Windows (`AppsUseLightTheme`), also while the app is open.
- **Language** is read once at start-up, so a change waits for a restart: a notice with a **Restart** button appears. The list is English (the neutral resources) plus every folder next to the app that is named after a language (`ru`, `de`, `es`, ...) and holds a `4MagicKeysPlus.resources.dll`, so a new `Strings.<language>.resx` shows up there without any code. There are no regional variants: the languages are only the languages themselves (a folder such as `ru-RU` is not offered, and a `Language` value of `ru-RU` is read as `ru`), and a Windows in a regional variant of a language (`ru-UA`) gets that language too. The agent follows the same language in its notices: it reads the value each time it shows one, so it does not have to be restarted. The reason of a failed hotkey registration is Windows' own text in the language of Windows, except "already registered", which the agent translates.
- **Agent** shows whether it answers on its pipe, with a **Start** button (started through Explorer, so that it keeps the normal rights of the user; the agent is the one next to the app or, when there is none, the one the autostart value points at). **Start with Windows** adds the `4MagicKeysPlusAgent` value to `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` for the agent next to the app, unless the value is already there: an existing one keeps its path. Turning it off does not delete the value but sets the "off" flag that Task Manager keeps in `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run`, so it can be turned back on without knowing where the agent is, and Task Manager shows it as disabled. The switch counts that flag: an entry switched off in Task Manager shows as off, and turning it on clears the flag.
- **Driver and keyboard** show whether the driver is installed (its service key exists), whether it is working (its control device `\\.\4MagicKeysPlus` exists, which it does while a keyboard is filtered) and which keyboard is connected.
- **Versions** are written in two lines in the bottom-left corner: the version of the application (that of the running app, which is also that of the agent and the libraries) and the version of the installed driver. The driver version is that of the installed driver package, read from the registry: the service names the `.inf` it was installed from (`DisplayName` like `@oem157.inf,...`), and the device keys of the HID class keep the version of that `.inf`. It shows a dash while there is none (no driver is installed).

All colors of the app are in `4MagicKeysPlus\Themes\` (`Dark.xaml` and `Light.xaml`); the rest of the XAML and the code only refer to those keys. A theme has to define the same keys, which the tests check.

### Keyboard Profiles

The settings belong to a keyboard, not to the driver: every physical keyboard has a profile, a subkey of

```
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Keyboards\<profile>
```

The name of the subkey does not matter to the driver (the configuration app names it by a GUID in braces, and lists only the profiles that are named so and have a `Model`). Values:

| Value | Type | |
|-------|------|-|
| `Identities` | MULTI_SZ | how the driver recognizes the keyboard, see below |
| `FnLock` | DWORD | optional, default `0`, see [FnLock](#fnlock-dword) |
| `Rules` | subkey | the key rules of the keyboard, see [Key Rules](#key-rules) |
| `Name` | SZ | optional, for the configuration app, ignored by the driver |
| `Model` | DWORD | the USB product ID of the model (`0x0267` A1644, `0x026C` A1843), for the configuration app, ignored by the driver |

An identity is a text in upper case: `bt:` and the 12 hex digits of the Bluetooth address of the keyboard (`bt:C4141104498E`), or `usb:` and its serial number (`usb:F0T125100BWJKPGAS`). One keyboard has both: Windows sees its cable and its Bluetooth pairing as unrelated devices, so a profile lists both, and then the keyboard has the same settings whichever way it is connected.

When a keyboard connects the driver works out its identity (the address from the instance ID of the paired Bluetooth device, the serial number from the instance ID of the USB device the interface belongs to; the Debug build prints it) and takes the first profile that lists it, ignoring case. A keyboard that has no profile, or no identity (a USB device without a serial number), gets no rules and Fn Lock off. List an identity in one profile only. The older `Parameters\Rules` and `Parameters\FnLock` are not read any more.

The identities of your keyboards are in the instance IDs of the devices:

```powershell
Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -match '^(USB\\VID_05AC&PID_026[7C]\\|BTHENUM\\\{00001124-.*_VID&0001004C_PID&026[7C]\\)' } | Select-Object InstanceId
```

`BTHENUM\{00001124-...}\D&15B70467&0&C4141104498E_C00000000` is `bt:C4141104498E` (the 12 digits before `_C0000000`), `USB\VID_05AC&PID_026C\F0T125100BWJKPGAS` is `usb:F0T125100BWJKPGAS`. A profile for a keyboard, from an administrative command prompt (the settings of the keyboard are under it; the rules are described below):

```
reg add "HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Keyboards\{6F1D2C3A-8B4E-4F70-9A21-5C3E7D8B0A11}" /v Identities /t REG_MULTI_SZ /d "bt:C4141104498E\0usb:F0T125100BWJKPGAS" /f
reg add "HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Keyboards\{6F1D2C3A-8B4E-4F70-9A21-5C3E7D8B0A11}" /v Model /t REG_DWORD /d 0x026C /f
reg add "HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Keyboards\{6F1D2C3A-8B4E-4F70-9A21-5C3E7D8B0A11}" /v FnLock /t REG_DWORD /d 1 /f
```

### Key Rules

The driver has no built-in key mappings. Everything - Fn combinations, media keys, modifier swaps - is a rule under the profile of the keyboard:

```
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Keyboards\<profile>\Rules\<subkey>
```

The subkey name does not matter to the driver; the convention is the 8-digit hex of `Source` (`01000041`), which also keeps two rules from sharing a source. Values:

| Value | Type | |
|-------|------|-|
| `Source` | DWORD | what is remapped |
| `Target` | DWORD | what it becomes |
| `FnLockAware` | DWORD | optional, default `0` (see below) |
| `Comment` | SZ | optional, ignored by the driver |

A subkey without both `Source` and `Target` is not a key rule and is ignored. Rules the driver cannot interpret are ignored too, as are rules beyond the first 128.

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

Rules are read when a keyboard connects and when the driver is told to reload them; see [Applying Changes](#applying-changes).

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
reg add "HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Keyboards\{6F1D2C3A-8B4E-4F70-9A21-5C3E7D8B0A11}\Rules\01000041" /v Source /t REG_DWORD /d 0x01000041 /f
reg add "HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Keyboards\{6F1D2C3A-8B4E-4F70-9A21-5C3E7D8B0A11}\Rules\01000041" /v Target /t REG_DWORD /d 0x010000CD /f
reg add "HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Keyboards\{6F1D2C3A-8B4E-4F70-9A21-5C3E7D8B0A11}\Rules\01000041" /v FnLockAware /t REG_DWORD /d 1 /f
```

Other examples: swap Left Alt and Left Cmd needs two rules (`0x00040000 -> 0x00080000` and `0x00080000 -> 0x00040000`); Eject as Fn is `0xF0 -> 0xF1`; Fn typing Insert is `0xF1 -> 0x49`.

The configuration app is not able to edit rules yet. Hand editing is meant for development and for advanced users.

### Launch Entries

The agent starts an application on a hotkey. An entry is a subkey of `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Hotkeys` with:

| Value | Type | |
|-------|------|-|
| `VkKey` | DWORD | Windows virtual-key code of the hotkey ([list](https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes)) |
| `VkModifier` | DWORD | optional modifiers, default `0`: `1` Alt, `2` Ctrl, `4` Shift, `8` Win (sum them) |
| `Path` | SZ | what to start: an executable, shortcut, ... |
| `Comment` | SZ | optional label used in the agent log |

Both `VkKey` and `Path` are needed; a subkey with exactly one of the two gets a warning in the agent log. Example, a calculator on F16 (virtual-key `0x7F`):

```
reg add "HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Hotkeys\Launch_00_7F" /v VkKey /t REG_DWORD /d 0x7F /f
reg add "HKLM\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters\Hotkeys\Launch_00_7F" /v Path /t REG_SZ /d "C:\Windows\System32\calc.exe" /f
```

The agent reads the entries at start and registers the hotkeys; restart it after changes. The agent sees ordinary Windows keystrokes, so Fn cannot be a part of a hotkey directly (map Fn+key to a free key such as F20 with a rule and launch on that). Hotkey registration errors and other problems go to `%LOCALAPPDATA%\4MagicKeysPlus\agent.log`.

### Battery Notice

The agent reads the keyboard battery (the vendor HID report `0x90` of the keyboard, no driver involved) every 15 minutes, the first time 15 minutes after it starts, and shows a small always-on-top notice at the bottom center of the screen when the charge reaches 20% and again at 5%. It does not take focus, closes by itself after a few seconds or on a click, and stays quiet while the keyboard is charging. Plugging the keyboard in re-arms both warnings.

### Driver Settings

All settings are in the registry at:

```
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\4MagicKeysPlus\Parameters
```

The profiles of the keyboards are under `Keyboards` (see [Keyboard Profiles](#keyboard-profiles)); nothing else is read. A profile is read when its keyboard connects and when the driver is told to reload (see below).

#### Applying Changes

The configuration app does this for you. After changing the registry by hand, apply the changes in one of these ways:

- Tell the driver to reload (below). It looks up the profile of every connected keyboard again and applies it, to all of them at once.
- Or restart the device (Administrative command prompt):

  ```
  pnputil /restart-device /deviceid "BTHENUM\{00001124-0000-1000-8000-00805f9b34fb}_VID&0001004c_PID&026C"
  ```

  Replace the device instance ID with the appropriate one for your keyboard model. You can also disconnect/reconnect the keyboard or reboot.

**Control device.** While at least one keyboard is connected, the driver has a device `\\.\4MagicKeysPlus`, open to administrators and SYSTEM only. The request `IOCTL_MKP_RELOAD_CONFIG` (`0x0022A000`, `CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800, METHOD_BUFFERED, FILE_WRITE_ACCESS)`) makes the driver reload. It takes no input; the optional output is three DWORDs: the rules read, the rules the driver could interpret (both summed over the keyboards) and the number of keyboards. The definitions are in `4MagicKeysPlusDriver\DriverInterface.h`.

Open the device, send the request and close the handle at once; never keep it open. An open handle keeps the device alive after the last keyboard is gone, which blocks unloading the driver (an update then needs a reboot) and makes reloading fail the next time a keyboard connects. `CreateFile` fails with error 2 when no keyboard is connected or the driver is older than this feature, and with error 5 when not elevated.

For example, from an elevated Windows PowerShell 5.1:

```powershell
Add-Type -TypeDefinition @"
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

public static class MagicKeysDriver {
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(SafeFileHandle device, uint code, IntPtr input, uint inputSize, uint[] output, uint outputSize, out uint returned, IntPtr overlapped);

    public static uint[] Reload() {
        using (SafeFileHandle device = CreateFile(@"\\.\4MagicKeysPlus", 0x40000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero)) {
            if (device.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            uint[] result = new uint[3];
            uint returned;
            if (!DeviceIoControl(device, 0x0022A000, IntPtr.Zero, 0, result, 12, out returned, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return result;
        }
    }
}
"@
$r = [MagicKeysDriver]::Reload()
"Rules read: $($r[0]), accepted: $($r[1]), keyboards: $($r[2])"
```

The rules read are those of the profiles the connected keyboards have found, counted per keyboard: `0` with a profile that has rules means that no connected keyboard found it (check its [identity](#keyboard-profiles)).

#### FnLock (DWORD)

A value of the profile of the keyboard, so every keyboard has its own. Used by rules with `FnLockAware=1` (see [How the rules are applied](#how-the-rules-are-applied)); any non-zero value means on. Does not affect rules that are not `FnLockAware`. Default: `0`.

The older `KeyMap`, `ModMap` and `SpecialModMap` binary values are no longer read and the driver INF no longer creates them; they can be deleted. So can `Rules` and `FnLock` directly under `Parameters`: the driver reads the profiles only.

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

Build the `4MagicKeysPlusDriver` project for x64 (Debug or Release). The .NET projects build with `dotnet build`, or from the solution in Visual Studio.

The build output goes to `bin\<Configuration>` (`bin\Debug`, `bin\Release`) next to the solution file: the executables and libraries of the application directly into it, the driver (`.sys`, `.inf`, `.cer` and the package folder with the catalog) into its `driver` subfolder. The tests keep their own output folders.

#### Versions

There are two versions, each declared in one place:

| Version | Covers | Where it is |
| --- | --- | --- |
| Application | `4MagicKeysPlus.exe`, `4MagicKeysPlusAgent.exe`, `4MagicKeysPlus.Core.dll` (and the test assemblies) | `MkpAppVersion` in `Directory.Build.props` in the root of the repository |
| Driver | `4MagicKeysPlus.sys`, `.inf` | the version after the date in the `DriverVer` line of `4MagicKeysPlusDriver\4MagicKeysPlus.inf` |

The application version is one number `a.b.c.d`; the SDK takes `AssemblyVersion`, `FileVersion` and the product version from it, so a new .NET project of the repository gets the version, the company, the product name and the copyright from `Directory.Build.props` without declaring anything (a project only gives its `AssemblyTitle`, the "file description"). The driver file gets its version resource from `4MagicKeysPlusDriver\Driver.rc`, which takes the number from the `DriverVer` line of the `.inf` (through `MkpDriverVersion` in the project file), so the `.sys` and the INF cannot differ; the date in that line is still set by hand. The `.inf` and `.cat` are not executables and have no file version of their own, `DriverVer` is the version of the driver package. The tests check all of this.

The "Product version" in the file properties of a .NET file is followed by `+` and the commit it was built from; that is added by the SDK and is meant to be there. Since the libraries share the version of the application, copy the app, the agent and `4MagicKeysPlus.Core.dll` together: an assembly does not load a library with a lower version than the one it was built against.

#### Tests

- Driver logic (`KeyProcessor`, compiled into the test project as plain user-mode C++): `4MagicKeysPlusDriverTests`, run from Visual Studio's Test Explorer or `vstest.console.exe` on the built DLL.
- Agent (launch entry loading, battery notice policy): `dotnet test 4MagicKeysPlusAgentTests`.
- Configuration app (rule decoding and encoding, editing, saving rules, tooltips, key names, theme keys, settings, languages, autostart): `dotnet test 4MagicKeysPlusTests`. The tests of saving use a scratch key under `HKEY_CURRENT_USER\Software\4MagicKeysPlusTests`, which they delete afterwards; they never touch the driver's rules.

The Debug build of the agent accepts two test switches that are not compiled into Release: `--demo-battery <percent> [delay seconds]` shows the battery notice once and exits, `--battery-interval <seconds>` checks the battery that often and logs every reading.

#### Code Formatting

C#, C and C++ sources are formatted automatically: `.editorconfig` with `dotnet format whitespace` for C#, `.clang-format` for C and C++. Enable the pre-commit hook once per clone:

```
git config core.hooksPath .githooks
```

On every commit the hook formats what is staged and puts the result into the index; unstaged changes are never touched. For a partially staged file only the staged part is formatted and its working copy stays as it was, so `git diff` shows the formatting it still lacks. `bash scripts/format.sh fix` formats everything by hand, `bash scripts/format.sh check` is what the pull request check runs: when it fails, it lists the files that need changes and tells you to run `fix` and commit the result (`FORMAT_VERBOSE=1` adds the formatters' own output).

The style is K&R, and nothing shares a line: method bodies, `if` and loop bodies and statements each start on their own line, an empty body is `{` and `}` on two lines, `if`, `else` and loops always have braces, and C# auto-properties are three lines (`{`, `get; set;`, `}`; Roslyn keeps the ones with an initializer, like `Cards { get; } = [];`, on one line). Adding the braces is a C# analyzer (IDE0011), not formatting: `scripts/format.sh` fixes and checks it (about 20 seconds, it loads the projects) and Visual Studio shows it as a warning, but the pre-commit hook skips it.

The hook needs `dotnet` on `PATH`. clang-format is taken from `PATH` or from the Visual Studio installation. Its version is pinned in `scripts/format.sh` (the one bundled with Visual Studio 2026), because the output differs between releases.

#### Continuous Integration

Pull requests and `main` run two GitHub Actions workflows: `Format` (above) and `CI` (`.github/workflows/ci.yml`), which has two jobs on `windows-2025-vs2026`:

- `driver` builds the driver (Release, x64) and `4MagicKeysPlusDriverTests` (Debug, x64) with MSBuild and runs the tests with `vstest.console.exe`.
- `dotnet` builds the agent and the configuration app in Release (including the uiAccess signing of the agent) and runs `4MagicKeysPlusAgentTests` and `4MagicKeysPlusTests`.

Both jobs create a throwaway test certificate with `scripts\new-test-certificate.ps1`, which refuses to overwrite an existing `.pfx`. The hosted runner has the WDK only for SDK `10.0.26100`, so the `driver` job restores the WDK and the SDK from NuGet (`4MagicKeysPlusDriver\packages.config`) and `4MagicKeysPlusDriver\Directory.Build.props` uses them whenever the folder `4MagicKeysPlusDriver\packages` exists. Without that folder a local build uses the installed WDK. To build against the packages locally, run `nuget restore 4MagicKeysPlusDriver\packages.config -PackagesDirectory 4MagicKeysPlusDriver\packages` and build with the 64-bit MSBuild (`MSBuild\Current\Bin\amd64`): the package has no x86 `stampinf.exe`, so the 32-bit one fails with `TRK0005`. When the WDK is updated, change the version in `packages.config`, in `Directory.Build.props` and the pinned `WindowsTargetPlatformVersion` in the driver project.

The steps run through `scripts\ci-run.ps1`, which repeats the first errors and the test totals as annotations on the run page, because the job logs need a sign-in even in a public repository.

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
