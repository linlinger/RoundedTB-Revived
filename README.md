# RoundedTB Revived

**English** | [简体中文](./README.zh-CN.md)

#### Add margins, rounded corners and segments to your taskbars!

RoundedTB Revived is a community-maintained continuation of RoundedTB (originally by
torchgm, under the GNU General Public License v3.0). It adds margins, rounded corners
and configurable segments to the Windows 10 / 11 taskbar.

![image](https://user-images.githubusercontent.com/31840547/134795141-76349eaf-12da-40f8-b2a0-d7b7c268d152.png)

## How do I get it?

Grab the latest release, unzip it and run `RoundedTB.exe`. You can also compile it
yourself from source (`build.bat`).

## Continuous build

Every push to this repository triggers a [GitHub Actions](https://github.com/linlinger/RoundedTB-Revived/actions)
Windows Release build. Artifacts are uploaded as `rtb-artifacts` on the run page — grab them to test the
latest commit without building locally. See `.github/workflows/ci.yml`.

## To use

### Basic options
The simplest way to use RoundedTB Revived is by simply entering a margin and corner radius.
 - **Margin** - controls how many pixels to remove from each side of the taskbar, creating a margin around it that you can see and click through.
 -  **Corner Radius** - adjusts how round the corners of the taskbar should be.

### Advanced options
The advanced options allow for further customisation, at the cost of some user-friendliness.
- **Independent Margins** - in the advanced settings, a <kbd>...</kbd> button appears on the margin box. Click it to enable independent margins, which allow you to specify the margin for each side of the taskbar. You can also use negative values to hide the rounded corners for some sides, allowing you to "attach" the taskbar to different sides of the monitor.
- **Dynamic Mode (Windows 11)** - dynamic mode automatically resizes the taskbars to accommodate the number of icons in it, making the taskbar behave similarly to macOS' Dock.
- **Split Mode (Windows 10)** - split mode is a simplified version of dynamic mode for Windows 10. Due to a more limited taskbar, dynamically resizing the taskbar isn't possible. However after some setup, split mode allows you to separate the taskbar from the system tray and resize it at will. For info on setting up, see the bottom of this readme.
- **Show System Tray** - this toggles whether or not the system tray, clock etc. is displayed in dynamic/split mode. It can be toggled at any time by pressing <kbd>Win</kbd>+<kbd>F2</kbd>.
- **TranslucentTB Compatibility** - due to a bug in Windows, apps that alter the composition of the taskbar don't allow RoundedTB Revived's changes to show up automatically. Enabling this option works together with [TranslucentTB](https://github.com/TranslucentTB/TranslucentTB) (current v4 releases; the old "requires 2021.5" note no longer applies). Note: on Windows 10, combining this with AutoHide can flicker while the taskbar fades (Explorer re-composes the taskbar); it's best not to use AutoHide and TranslucentTB together.
- **About RoundedTB Revived** - provides information about the current version. The "Debug" section lets you open the config and log files.

## Supported systems

- **Windows 11** 22H2+ — dynamic mode; verified on 24H2 (build 26100). Windows 11 26H1 is expected to work and will be confirmed after testing.
- **Windows 10** (1607+) — split mode.

## Known issues
 - **Auto-hide**: use the built-in AutoHide option. Enabling Windows' own auto-hide at the same time can cause flicker.
 - **Dynamic mode**: when a new app icon appears, the taskbar may briefly clip it in half before self-correcting (cosmetic; being fixed in the next release).
 - Rounded corners are not antialiased due to a Windows limitation.
 - Dynamic mode/split mode only work correctly when the taskbar is horizontal at the top/bottom of the screen.
 - Split mode on Windows 10 only supports the main taskbar, secondary taskbars will not be split.
 - Compatibility with taskbar mods outside of TranslucentTB version 2021.5 is not currently guaranteed.

## AI-generated code

This project is a community-maintained revival of RoundedTB. A significant portion of the recent
code — Windows 11 22H2+/24H2 compatibility fixes, localization (i18n), settings robustness and
worker-loop recovery — was written with the assistance of AI (the **DeepSeek V4** model, working
through Claude Code). Any AI-assisted code is released under the same **GNU GPL v3** terms as the
rest of the project.

## Credits

- **torchgm** — original author of [RoundedTB](https://github.com/torchgm/RoundedTB) (GPL v3).
- **gniang** ([Gniang/RoundedTB](https://github.com/Gniang/RoundedTB)) — several stability fixes in this
  build are ported from their fork: secondary-taskbar tray geometry, hover-state persistence,
  atomic settings writes with legacy-config migration, worker-loop recovery, and Explorer-restart
  backoff. Thank you for the excellent work!
- **DeepSeek (V4)** — model used to assist with the compatibility and stability work (see
  "AI-generated code" above).

## Other info
If anything breaks catastrophically, press <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>Esc</kbd> to open Task Manager, end RoundedTB Revived and then restart Explorer. At worst, just reboot your PC. RoundedTB Revived makes no permanent changes (though it will run on startup if you enable it from the tray icon), so restarting should clear any issues.

Bug reports and feature requests can be filed through the issue tracker of this repository.

### Configuring split mode on Windows 10
Split mode has a couple of limitations and requires a small amount of setup to get working properly.
#### Limitations
- Split mode doesn't resize itself automatically. This feature will be coming to RoundedTB Revived for Windows 10 in the future.
- Toolbars are not compatible with split mode currently, and will need to be disabled apart from one. This is because toolbars are used to mark the "empty" space on the taskbar.
- Split mode only works when the taskbar is horizontal at the top or bottom of the screen, and on the primary monitor.
#### Setup
1. Right-click the taskbar and disable "Lock the taskbar".
2. Right-click it again and turn off any existing toolbars.
3. Right-click a third time, select Toolbars > Desktop.
4. Use the small <kbd>||</kbd> handle to resize the taskbar as you please.

Watch the following video for a guide on setting up split mode:

https://user-images.githubusercontent.com/31840547/134795022-1312d011-40f2-4641-8c8d-3d6c0e752747.mp4
