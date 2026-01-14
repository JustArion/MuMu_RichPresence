[![Test Runner](https://github.com/JustArion/MuMu_RichPresence/actions/workflows/tests.yml/badge.svg)](https://github.com/JustArion/MuMu_RichPresence/actions/workflows/tests.yml)

> [!NOTE]
> - The project has a [sister-repo](https://github.com/JustArion/PlayGames_RichPresence) for `Google Play Games`
> - Additional options available in the Tray Icon

## Table of Contents
- [Requirements](#requirements)
- [Installation Types](#installation)
- [Previews](#previews)
- [Tray Options](#tray-options)
- [Custom Launch Args](#custom-launch-args)
- [Advanced Users](#for-advanced-users)
- [Building from Source](./building.md)
- [Permissions](#permissions)

---
### Requirements
- [.NET 10 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-10.0.0-windows-x64-installer) (x64)

### Installation Types
- Standalone
    - No Auto Update
- Portable
    - Auto Update
    - Portable
- Setup
    - Auto Update
    - Shortcut in Start Menu
    - Can be uninstalled by right-clicking uninstall in Start Menu
    - Installed in `%appdata%/Local/MuMu-RichPresence`

---
### Previews
![rich-presence-preview](images/RichPresencePreview.png)
![context-menu-preview](images/TrayContextMenuPreview.png)

---

### Tray Options
- Enabled (Checkbox)
- Open App Directory (Button)
- Run on Startup (Checkbox)
- Hide Tray (Button, Hides the Tray Icon until next start)
- Exit (Closes the program)

---
### Custom Launch Args

| Argument                  |     Default Value     | Description                                                                                       |
|:--------------------------|:---------------------:|:--------------------------------------------------------------------------------------------------|
| --custom-application-id=  |  1339586347576328293  | [Discord Application Id](https://discord.com/developers/applications)                             |
| --seq-url=                | http://localhost:9999 | Seq Logging Platform                                                                              |
| --bind-to=                |         `N/A`         | Binds this process to another process' ID. When the other process exits, this one does too        |
| --extended-logging        |         `N/A`         | File Log Level: Verbose (From Warning)                                                            |
| --rp-disabled-on-start    |         `N/A`         | Rich Presence is Disabled for *MuMu Emulator*                                                     |
| --no-file-logging         |         `N/A`         | Disables logging to the file (Located in the current directory)                                   |
| --no-auto-update          |         `N/A`         | Disables Auto-Updates & Checking for Updates (Only affects Velopack (Portable / Setup) versions)  |
| --hide-tray-icon-on-start |         `N/A`         | Hides the Tray Icon when running `MuMu Rich Presence`                                             |
| --adb-experimental        |         `N/A`         | Enables an experimental mode where we don't look for presences in a log file but instead ask MuMu |

**Launch Args Example**

`& '.\MuMu RichPresence.exe' --extended-logging --seq-url=http://localhost:9999`

You can also provide a `.env` file in the MuMu_RichPresence's directory or it's parent folders.<br/>
For the Setup version of Velopack, it's recommended to place the `.env` file in the parent directory (`%AppData%\Local\MuMu-RichPresence\`) since every update wipes everything in the `%AppData%\Local\MuMu-RichPresence\current` folder

`.env` file example:
```env
# My Important Settings
no-file-logging=true
no-auto-update=true

# Note setting 
rp-disabled-on-start=false # has no impact and will still be treated as rp-disabled-on-start=true 
custom-application-id=1339586347576328293
```

---
## For advanced users

### Auto-Startup

Enabling `Run on Startup` clones the current launch arguments and runs it as that on startup.

### Permanently hiding the Tray Icon

There's currently no UI option to hide it permanently, but you can do so via the command line
- Open up PowerShell and paste this in

```ps1
$path = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$key = 'MuMu RichPresence Standalone'
$value = (Get-ItemProperty -Path $path).$key;
Set-ItemProperty -Path $path -Name $key -Value ($value + ' --hide-tray-icon-on-start')
```

### [Building from Source](./building.md)

---
### Permissions
- A comprehensive list of permissions the application needs / could need can be found [here](permissions.md)
