<img src="assets/icon/icon.svg" alt="4MagicKeys+ icon" width="96" align="right">

# 4MagicKeys+

Windows driver and apps for Apple Magic Keyboards. They make the keyboard behave like a Windows one: any key can be remapped, the Fn layer gives Home, End, Page Up/Down, Insert and Print Screen, the F-row works as media keys or as function keys, and a notice warns when the battery runs low. Every keyboard has settings of its own, the same over USB and over Bluetooth.

![The configuration app showing a Magic Keyboard with Touch ID](docs/images/app.png)

Originally forked and inspired by the [samartzidis/winAppleKey](https://github.com/samartzidis/winAppleKey) repository.

<details>
<summary><b>Supported keyboards</b></summary>

| Model | Name | Connection | Tested on hardware |
|-------|------|------------|--------------------|
| A1644 | Apple Magic Keyboard | USB / Bluetooth | Yes (by the author of the original project) |
| A1843 | Apple Magic Keyboard with Numeric Keypad | USB / Bluetooth | Yes |
| A2449 | Apple Magic Keyboard with Touch ID | USB / Bluetooth | No |
| A2450 | Apple Magic Keyboard with Lock Key | USB / Bluetooth | No |
| A2520 | Apple Magic Keyboard with Touch ID and Numeric Keypad | USB / Bluetooth | No |
| A3118 | Apple Magic Keyboard with Touch ID | USB / Bluetooth | Yes |
| A3119 | Apple Magic Keyboard with Touch ID and Numeric Keypad | USB / Bluetooth | No |
| A3203 | Apple Magic Keyboard with Lock Key | USB / Bluetooth | No |

A model marked No is supported on the strength of what is known about it (its product ID and the layout of its keys), but nobody has tried it on a real keyboard yet. If you have one, please tell how it goes in an issue.

</details>

## Features

- Remap any key to another key, swap modifiers, turn Fn, Eject or Lock into a key or a modifier, and turn a key into a modifier combination or a media, volume or brightness action.
- An Fn layer with rules of its own, and Fn Lock: the F-row keys work as media keys or as function keys, and Fn flips them.
- Settings per keyboard, the same over USB and over Bluetooth. Each model starts with defaults.
- A configuration app that shows the connected keyboards and edits their rules on a picture of the keyboard, with a dark and a light theme and a choice of language.
- An agent that starts programs on hotkeys and warns when the battery of a keyboard is low.

How it all works is in the [wiki](https://github.com/4teos/4MagicKeysPlus/wiki).

## Requirements

Windows 11 24H2 or newer, x64. Installing the driver and starting the configuration app need administrator rights.

## Installation

**DISCLAIMER:** The driver is signed with a self-signed (test) certificate that the installer (or `sign.cmd`) makes on your computer. Windows will not load the driver unless running in **TESTSIGNING** mode ([what it is](https://github.com/4teos/4MagicKeysPlus/wiki/Test-Signing)). Permanently running Windows in TESTSIGNING mode leaves your system open to potential security risks. Any consequence is solely your own responsibility. 4MagicKeys+ is ***free software*** that you build and/or use completely ***at your own risk.*** If your system runs UEFI with **Secure Boot** enabled, you will need to disable **Secure Boot** in BIOS first.

### With the installer

Download `4MagicKeysPlus-<version>-setup.exe` from [Releases](https://github.com/4teos/4MagicKeysPlus/releases) and start it. It is not signed, so Windows SmartScreen may warn about an unknown publisher (**More info** → **Run anyway**). Setup turns TESTSIGNING on when it is off, signs the driver and the agent on your computer, installs the driver and the apps, and asks for a restart when the driver needs one. Uninstall through **Settings → Apps → 4MagicKeys+**.

### From the release archive

1. Enable TESTSIGNING mode (Administrative command prompt), then reboot:

   ```
   bcdedit.exe -set TESTSIGNING ON
   ```

2. Download `4MagicKeysPlus-<version>-win-x64.zip` from [Releases](https://github.com/4teos/4MagicKeysPlus/releases) and unpack it, keeping its folders as they are.
3. Start `sign.cmd`. It signs the driver and the agent on your computer, with a certificate that it makes and that Windows then trusts.
4. Install the driver via Device Manager: find your Apple keyboard under **Human Interface Devices**, right-click → **Update driver** → **Browse my computer** → **Let me pick from a list** → **Have Disk...**, and point to the `driver` folder.
5. Copy the `app` folder to a folder of its own under `C:\Program Files`, for example `C:\Program Files\4MagicKeysPlus` (the commands below use that one). It has to be in Program Files: Windows lets the agent receive hotkeys over elevated windows only when it is signed and in a protected folder.
6. Put the default settings of the keyboard models into the registry (Administrative command prompt):

   ```
   start /wait "" "C:\Program Files\4MagicKeysPlus\4MagicKeysPlus.exe" --install-defaults
   ```

7. Reconnect the keyboard, and start `4MagicKeysPlus.exe` from that folder.

Checking the download, updating and uninstalling are described on the [Installation](https://github.com/4teos/4MagicKeysPlus/wiki/Installation) page of the wiki.

## Uninstallation

Installed with the installer: **Settings → Apps → 4MagicKeys+** removes everything. Removing 4MagicKeys+ by hand, step by step and in the order that matters, is described on the [Manual Uninstall](https://github.com/4teos/4MagicKeysPlus/wiki/Manual-Uninstall) page of the wiki.

## Building and contributing

Building from source, the layout of the repository, the tests and how a release is made are in the [wiki](https://github.com/4teos/4MagicKeysPlus/wiki): see [Building from Source](https://github.com/4teos/4MagicKeysPlus/wiki/Building-from-Source) and [Contributing](https://github.com/4teos/4MagicKeysPlus/wiki/Contributing).

## Thanks

Big shout out to [gozaltech/mkBatteryChecker](https://github.com/gozaltech/mkBatteryChecker) and [hank1101444/WinMagicBattery](https://github.com/hank1101444/WinMagicBattery), which showed how the battery level of a Magic Keyboard can be read.

## License

[MIT](LICENSE).

Apple and Magic Keyboard are trademarks of Apple Inc. 4MagicKeys+ is an independent project and is not affiliated with or endorsed by Apple.
