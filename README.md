# PSP2Tools

A unity package to streamline development for the PlayStation Vita

Supports building and installing your projects in one click.

(The rest of the readme will come someday, most is just self explanatory)
VitaFTPI but better.

## WARNING

Keep in mind, while this tool has been tested, since this toolkit runs in the unity editor instead of a standalone program you should properly save your work *before* running install operations to ensure you don't lose any work if any freezes decide to make themselves apparent. (Sometimes it might randomly freeze due to bad network)
__*you have been warned*__

## Requirements

Install [vitacompanion](https://github.com/Ibrahim778/vitacompanion) (including it's kernel module) V2.4 or later.


### Installation
This section is for people who haven't used any of my plugins before and are unsure how to install the required dependencies on thier devices. If you have already gotten my fork of vitacompanion up and running, you don't need to follow this—just import the .unitypackage file to your project.

#### 1. Install QuickMenuReborn
QuickMenuReborn is my extension plugin for the QuickMenu. VitaCompanion utilises this to display controls for the ftp and command servers as well as USB mounting inside the quick menu.

1. Download the latest version of `qmr_plugin.rco` and `QuickMenuReborn.suprx` from [here](https://github.com/Ibrahim778/QuickMenuReborn/releases).
1. Place qmr_plugin.rco inside `ur0:/QuickMenuReborn/` (case sensitive!)
1. Place QuickMenuReborn.suprx inside your `tai` folder and add it to your config.txt under `*main`

#### 2. Install VitaCompanion
   
1. Download the latest version of `vitacompanion.suprx` and `VCKernel.skprx` from [here](https://github.com/Ibrahim778/vitacompanion/releases).
1. Place vitacompanion.suprx inside `ur0:/QuickMenuReborn/` (NOT your tai folder! Do NOT add this to your config.txt)
1. Place VCKernel.skprx inside your `tai` folder and add it under `*KERNEL`
1. Reboot your vita and open the quick menu. Enable the command and ftp servers by checking the box.

#### 3. Import PSP2Tools into your project

You're now ready to download the latest .unitypackage for PSP2Tools from the releaes page and import the files into your project. A new editor window button should be visible in your toolbar.
