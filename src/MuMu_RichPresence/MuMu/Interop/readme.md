#### Why might we need an ADB interop layer for log files?

Our Rich Presence application currently starts up on boot and monitors MuMu's shell.log files.
That means that if for any reason MuMu decides to delete those files, we'll have more info than MuMu.

What's not great is if those files are deleted after you started a game already, or haven't switched tabs (If enabled).

This would mean that there's no history in the `shell.log` files of you ever starting a game. So there's nothing to track.
> We still have more info than MuMu themselves though no?

Yes, but only if we started **before** MuMu Player did.

Now add that MuMu player logs every mouse movement to the log file as `:enter` and `:exit`, this fills the log files up quite quickly, which can push out "old" events like us ever starting our game.

> This is a bit of a niche problem though no?

Not entirely, there are cases where users might download the app while MuMu is open, on a game and no Rich Presence appears since they've been playing that 1 game for a while (say 10+ min)

#### Proposal:

- Add an ADB interop layer that does the same as the log file reading
- Make the interop layer a LaunchArg switch to have as the main reader
- Use ADB only on app start (if it's not a launch arg)
  - This will allow the `shell.log` file users to still have an app show up as an initial presence if RichPresence was started too late
