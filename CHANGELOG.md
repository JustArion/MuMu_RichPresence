# Changelog

## v2.0.0
- ⚡️Rich Presences now have clickable links directing them to the respective game's listing on the Play Store
- ⚡️Now shows the game you're playing in the members / direct message list instead of "MuMu Player"
    - This is different than the change below. The one below uses Rich Presences made by Discord, this change uses Rich Presences made by MuMu Rich Presence (If no official rich presence is detected)
- ⚡️Most games will now show "Playing \<game name>" instead of normally "Playing MuMu Player"
    - Discord has added a ton of mobile games to their "Official Presences" list, which we now also use if we detect any of them!
    - What this does **not** do is show the game in your "Recently Played" list. As this to my knowledge requires each game to have a specific path / process name which can *somewhat* be set by the user but not programatically
- ⚡️ Updated .NET Runtime (.NET 9 -> .NET 10)
    - **Huge apologies for updating the runtime again!**
    - Won't change the runtime version for a really long time now
    - A missing dependencies popup will appear with an option to install the update. Pressing "Install Update" will update the app
- 🦺 Improved startup times for `Auto Update` users. Checking for updates caused the app to wait until checking was done..
- 🦺 Bugfix: In some cases Rich Presence wasn't removed after MuMu Player closes.
    - This occurs when MuMu Player is ran as admin while this program was not admin.

## v1.2.3

- 🦺 Bugfix: Enabling Rich Presence on Discord after a game has already started would not show the game as being played. It now correctly updates within 5 seconds.
- 🦺 Reduced log level of App Focus events from Info -> Verbose
    - This is partly due to the recent patch adding further MuMu log file histories for more accurate presences
- 🦺 Bugfix: Fixed a rare case where "Run on Startup" would be checked but would not actually start. This was due to the .exe being moved after "Run on Startup" was checked.
- 🦺 MuMu Rich Presence will now only keep the current version's logs
- ⚡️Added launch arg for hiding the tray icon on start

## v1.2.2

- 🦺 Rich Presence would unintentionally be cleared after prolonged activity in MuMu Player
    - This is partly a technical issue on MuMu's side but a workaround has been created.
- 🦺 Added More MuMu specific apps to the Rich Presence blacklist (App Cloner, Home Screen)

## v1.2.1

- ⚡️ Updated .NET Runtime (.NET 8 -> .NET 9)
- 🦺 Various minor optimizations and improvements
- 🦺 Added support for MuMu Player 5 (Up from MuMu Player 4)

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
