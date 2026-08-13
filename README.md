# RoundedTB Revived

#### Add margins, rounded corners and segments to your taskbars!

RoundedTB Revived is a community-maintained continuation of RoundedTB (originally by
torchgm, under the GNU General Public License v3.0). It adds margins, rounded corners
and configurable segments to the Windows 10 / 11 taskbar.

![image](https://user-images.githubusercontent.com/31840547/134795141-76349eaf-12da-40f8-b2a0-d7b7c268d152.png)

## How do I get it?

Grab the latest release, unzip it and run `RoundedTB.exe`. You can also compile it
yourself from source (`build.bat`).

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
- **TranslucentTB Compatibility** - due to a bug in Windows, apps that alter the composition of the taskbar don't allow RoundedTB Revived's changes to show up automatically. Enabling this option works together with [TranslucentTB](https://github.com/TranslucentTB/TranslucentTB). This is experimental and *will* flicker slightly. It requires TranslucentTB version 2021.5 to function.
- **About RoundedTB Revived** - provides information about the current version. The "Debug" section lets you open the config and log files.

## Known issues
 - Auto-hiding is still incredibly experimental and may lead to a lot of flickering, especially with TranslucentTB compatibility or dynamic/split mode enabled.
 - Rounded corners are not antialiased due to a Windows limitation.
 - Dynamic mode won't hide the left side of the taskbar if the taskbar alignment has never been changed. This can be worked around by changing the alignment to Left and back to Center.
 - Dynamic mode/split mode only work correctly when the taskbar is horizontal at the top/bottom of the screen.
 - Split mode on Windows 10 only supports the main taskbar, secondary taskbars will not be split.
 - When using dynamic mode, the taskbar may occasionally become too large, too small or not update. This can usually be fixed by moving a window to or from that monitor or briefly changing the taskbar alignment.
 - Compatibility with taskbar mods outside of TranslucentTB version 2021.5 is not currently guaranteed.

## Credits

- **torchgm** — original author of [RoundedTB](https://github.com/torchgm/RoundedTB) (GPL v3).
- **gniang** ([Gniang/RoundedTB](https://github.com/Gniang/RoundedTB)) — several stability fixes in this
  build are ported from their fork: secondary-taskbar tray geometry, hover-state persistence,
  atomic settings writes with legacy-config migration, worker-loop recovery, and Explorer-restart
  backoff. Thank you for the excellent work!

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
