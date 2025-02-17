> [!NOTE]
> - The project has a [sister-repo](https://github.com/JustArion/PlayGames_RichPresence) for `Google Play Games`
> - Additional options available in the Tray Icon

## Table of Contents
- [Requirements](#requirements)
- [Installation](#installation)
- [Tray Options](#tray-options)
- [Auto-Startup](#auto-startup)
- [Custom Launch Args](#custom-launch-args)
- [Previews](#previews)
- [Building from Source](#building-from-source)
- [Permissions](#permissions)

### Requirements
[.NET 8.0.X Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (x64)

---
### Installation
- Standalone
    - No Auto Update
- Portable
    - Auto Update
- Setup
    - Auto Update
    - Shortcut in Start Menu
    - Can be uninstalled by right clicking uninstall in Start Menu
    - Installed in `%appdata%/Local`

---
### Tray Options

- Enabled (Checkbox)
- Run on Startup (Checkbox)
- Hide Tray (Button, Hides the Tray Icon until next start)
- Exit (Closes the program)

---
### Auto-Startup

Enabling `Run on Startup` clones the current launch arguments and runs it as that on startup.

---
### Custom Launch Args

| Argument                 |     Default Value     | Description                                                                                |
|:-------------------------|:---------------------:|:-------------------------------------------------------------------------------------------|
| --custom-application-id= |  1339586347576328293  | [Discord Application Id](https://discord.com/developers/applications)                      |
| --seq-url=               | http://localhost:9999 | Seq Logging Platform                                                                       |
| --bind-to=               |         `N/A`         | Binds this process to another process' ID. When the other process exits, this one does too |
| --extended-logging       |         `N/A`         | File Log Level: Verbose (From Warning)                                                     |
| --rp-disabled-on-start   |         `N/A`         | Rich Presence is Disabled for *MuMu Emulator*                                              |
| --no-file-logging        |         `N/A`         | Disables logging to the file (Located in the current directory)                            |

**Launch Args Example**

`& '.\MuMu RichPresence.exe' --extended-logging --seq-url=http://localhost:9999`

---
### Previews
![context-menu-preview](images/TrayContextMenuPreview.png)
![rich-presence-preview](images/RichPresencePreview.png)
---

## For advanced users

### Building from Source

#### Pre-Build Requirements

- [.NET SDK 8.0.X](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (x64)<br>
- [Git](https://git-scm.com/downloads)

---
#### Build Steps

**Manual**
```ps1
git clone https://github.com/JustArion/MuMu_RichPresence && cd "MuMu_RichPresence"
git submodule init
git submodule update
dotnet publish .\src\MuMu_RichPresence\ --runtime win-x64 --output ./bin/
```

**with Auto-Update**
```ps1
git clone https://github.com/JustArion/MuMu_RichPresence && cd "MuMu_RichPresence"
git submodule init
git submodule update
dotnet publish .\src\MuMu_RichPresence\ --runtime win-x64 --output ./bin/
vpk pack -packId 'MuMu-RichPresence' -v '1.0.0' --outputDir 'velopack' --mainExe 'MuMu RichPresence Standalone.exe' --packDir 'bin'
echo "Successfully built to 'velopack'"
```

**Makefile**
```ps1
git clone https://github.com/JustArion/MuMu_RichPresence && cd "MuMu_RichPresence"
make build
```

After running these commands the output should be in the `bin` folder in the root directory of the repo.

### Permissions

A comprehensive list of permissions the application needs / could need can be found [here](permissions.md)

---

