# Changelog

## v1.2.0

- ⚡️ Updated .NET Runtime (.NET 8 -> .NET 9)

## v1.1.5

- ⚡️ Velopack log level reduced by 1 level
- ⚡️ "Open App Directory" button added to tray
- 🦺 Bugfix: Addresses #1
- 🦺 Bugfix: Application log file would be created in an invalid directory leading to no logs being recorded for applications started via Windows (Startup)

## v1.1.4

- 🦺 Bugfix: Presence would sometimes not be set
- 🦺 Bugfix: Tray Icon should be created before waiting for MuMu Player
- ⚡️ Added option for Velopack (Auto-Update) users to disable auto-updates
    - Run with `--no-auto-update`

## v1.1.3

- ⚡️ Releases are now provided and built by [Github Actions](https://github.com/JustArion/MuMu_RichPresence/actions)

## v1.1.1

- 🦺 Bugfix: Presence wouldn't show if you switch tabs from Presence -> System App -> Same Presence
- ⚡️ Hovering over the Rich Presence art will now show the Title
- ⚡️ SEQ Log Level is now Information (From Warning)
- ⚡️ Removed Auto-Update Desktop Shortcut

## v1.1.0

- ⚡️ Added the ability to auto-update by downloading the Setup or Portable versions (Standalone won't auto-update)
- ⚡️ Scraping Google Play for information is more resilient
- 🦺 Bugfix: A crash could occur when closing the program due to a race condition
