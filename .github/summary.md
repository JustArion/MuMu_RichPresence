There are two main functions our app (`MuMu_RichPresence`) can do.

1. We read from a `.log` file and display a Rich Presence based on what we read
2. We ask MuMu directly what the app that's currently focused is and display a Rich Presence based on that

#### 1.
MuMu Player records what the emulator is doing in a file called `shell.log`<br/>
We read that file, and display a rich presence for you when a game is being played.

---
#### 2.
We connect to MuMu Player via a program called `Android Debug Bridge` (adb), MuMu Player comes with 2 copies of `adb`. We ask MuMu Player through `adb` what the current game is, and display a rich presence for you when a game is being played.

---
#### Which option is better?

In short, neither; but mostly `1`. Both come with drawbacks and advantages.

- `1` (.log file method)
  - Is unreliable in some rare cases
- `2` (adb method)
  - Is slow

Currently `1` is the default choice as it's been used since the start, but `2` can be enabled by adding `--experimental` as a [launch argument](readme.md#custom-launch-args)

A more detailed explanation of both is below

---
`1` is unreliable when the .log file becomes full after you started the game already. MuMu Player will delete the log when it becomes full, meaning we will have no record of you playing a game.
This only happens if our program `MuMu_RichPresence` starts after all this happened, if it started before (Like if you're a regular user of this program) you should never experience this.

This leaves a case where a new user might see the program as not working and just uninstall.
Our fix is to use the adb method once, on startup, if it detects no games are being played (So it's fine to be slow there)

`2` is slow since everytime we tell or ask `adb` to do something we're basically starting a new program every time. And connecting to `adb` takes a bit.

This can be mitigated (to some degree, but not entirely) by some code changes, but the goal here is to keep things as simple as possible, but its already pretty intricate.