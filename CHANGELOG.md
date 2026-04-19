# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Development]

## [2.2.0] / 2026-04-19
- ⚡️ `--experimental` mode now supports non-Play Store apps
- 🦺 General optimizations
  - Less overall web activity
    - Web scraping results are now cached
    - Web scraping results aren't retried after respective failures
  - If `--experimental` mode fails to start up, tries to fall back to normal file watching

## [2.1.0] / 2026-02-08
- 🦺 Bugfix: In rare cases on v`5.14`, couldn't detect rich presences from MuMu Player
- ⚡️Added support for `.env` files
  - Place a file called `.env` in either the MuMu_RichPresence Directory or it's parent 
- ⚡️Added an experimental launch argument `--experimental` that uses `ADB` instead of watching log files, to show Rich Presences
- ⚡️If no Rich Presence is detected when the app starts, will try to ask `ADB` if there's a running app
  - This is a fallback measure for installing MuMu_RichPresence while MuMu is running for a while
- 🦺 Changed checking the log file for updates from every `100ms` to every `1 second`
- ⚡️Added opening the log file as a tray option if ExtendedLogging is enabled
- 🦺 Bugfix: Toggling the Rich Presence from the tray would not re-enable the presence under certain circumstances
- ⚡️Added `BiliGame` as an additional app store to get metadata from!
  - Metadata like the app icon or the app name
  - Metadata isn't yet available for `BiliGame` while on `--experimental` mode

## [2.0.0] / 2025-12-23
- 🦺 Bugfix: Fixed Rich Presences displaying old game durations in some cases
- ⚡️Rich Presences now have clickable links directing them to the respective game's listing on the Play Store
- ⚡️Now shows the game you're playing in the members / direct message list instead of "MuMu Player"
    - This is different from the change below. The one below uses Rich Presences made by Discord, this change uses Rich Presences made by MuMu Rich Presence (If no official rich presence is detected)
- ⚡️Most games will now show "Playing \<game name>" instead of normally "Playing MuMu Player"
    - Discord has added a ton of mobile games to their "Official Presences" list, which we now also use if we detect any of them!
    - We save a list of these "Official Presences" instead of asking Discord for them every time (`detectable.json`)
    - What this does **not** do is show the game in your "Recently Played" list. As this to my knowledge requires each game to have a specific path / process name which can *somewhat* be set by the user but not programatically
- ⚡️ Updated .NET Runtime (.NET 9 -> .NET 10)
    - **Huge apologies for updating the runtime again!**
    - Won't change the runtime version for a really long time now
    - A missing dependencies popup will appear with an option to install the update. Pressing "Install Update" will update the app
- 🦺 Improved startup times for `Auto Update` users. Checking for updates caused the app to wait until checking was done..
- 🦺 Bugfix: In some cases Rich Presence wasn't removed after MuMu Player closes.
    - This occurs when MuMu Player is run as admin while this program was not admin.

## [1.2.3] / 2025-11-16
- 🦺 Bugfix: Enabling Rich Presence on Discord after a game has already started would not show the game as being played. It now correctly updates within 5 seconds.
- 🦺 Reduced log level of App Focus events from Info -> Verbose
    - This is partly due to the recent patch adding further MuMu log file histories for more accurate presences
- 🦺 Bugfix: Fixed a rare case where "Run on Startup" would be checked but would not actually start. This was due to the .exe being moved after "Run on Startup" was checked.
- 🦺 MuMu Rich Presence will now only keep the current version's logs
- ⚡️Added launch arg for hiding the tray icon on start

## [1.2.2] / 2025-08-27
- 🦺 Rich Presence would unintentionally be cleared after prolonged activity in MuMu Player
    - This is partly a technical issue on MuMu's side but a workaround has been created.
- 🦺 Added More MuMu specific apps to the Rich Presence blacklist (App Cloner, Home Screen)

## [1.2.1] / 2025-08-26
- ⚡️ Updated .NET Runtime (.NET 8 -> .NET 9)
- 🦺 Various minor optimizations and improvements
- 🦺 Added support for MuMu Player 5 (Up from MuMu Player 4)

## [1.1.5] / 2025-03-05
- ⚡️ Velopack log level reduced by 1 level
- ⚡️ "Open App Directory" button added to tray
- 🦺 Bugfix: Addresses #1
- 🦺 Bugfix: Application log file would be created in an invalid directory leading to no logs being recorded for applications started via Windows (Startup)

## [1.1.4] / 2025-02-19
- 🦺 Bugfix: Presence would sometimes not be set
- 🦺 Bugfix: Tray Icon should be created before waiting for MuMu Player
- ⚡️ Added option for Velopack (Auto-Update) users to disable auto-updates
    - Run with `--no-auto-update`

## [1.1.3] / 2025-02-18
- ⚡️ Releases are now provided and built by [Github Actions](https://github.com/JustArion/MuMu_RichPresence/actions)

## [1.1.1] / 2025-02-18
- 🦺 Bugfix: Presence wouldn't show if you switch tabs from Presence -> System App -> Same Presence
- ⚡️ Hovering over the Rich Presence art will now show the Title
- ⚡️ SEQ Log Level is now Information (From Warning)
- ⚡️ Removed Auto-Update Desktop Shortcut

## [1.1.0] / 2025-02-16
- ⚡️ Added the ability to auto-update by downloading the Setup or Portable versions (Standalone won't auto-update)
- ⚡️ Scraping Google Play for information is more resilient
- 🦺 Bugfix: A crash could occur when closing the program due to a race condition

## [1.0.0] / 2025-02-13
- ⚡️Initial Release!

[Development]: https://github.com/JustArion/MuMu_RichPresence/compare/2.2.0...HEAD
[2.2.0]: https://github.com/JustArion/MuMu_RichPresence/compare/2.1.0...2.2.0
[2.1.0]: https://github.com/JustArion/MuMu_RichPresence/compare/2.0.0...2.1.0
[2.0.0]: https://github.com/JustArion/MuMu_RichPresence/compare/1.2.3...2.0.0
[1.2.3]: https://github.com/JustArion/MuMu_RichPresence/compare/1.2.2...1.2.3
[1.2.2]: https://github.com/JustArion/MuMu_RichPresence/compare/1.2.1...1.2.2
[1.2.1]: https://github.com/JustArion/MuMu_RichPresence/compare/1.1.5...1.2.1
[1.1.5]: https://github.com/JustArion/MuMu_RichPresence/compare/1.1.4...1.1.5
[1.1.4]: https://github.com/JustArion/MuMu_RichPresence/compare/1.1.3...1.1.4
[1.1.3]: https://github.com/JustArion/MuMu_RichPresence/compare/1.1.1...1.1.3
[1.1.1]: https://github.com/JustArion/MuMu_RichPresence/compare/1.1.0...1.1.1
[1.1.0]: https://github.com/JustArion/MuMu_RichPresence/compare/1.0.0...1.1.0
[1.0.0]: https://github.com/JustArion/MuMu_RichPresence/tree/1.0.0
