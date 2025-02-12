There is a case where the `MuMu Player` app is entirely killed by something unexpected (Task Manager or other things)
- MuMuPlayer.exe
- MuMuVMMHeadless.exe
- MuMuVMMSVC.exe

This would cause the app state to be frozen in the log file. Meaning the Rich Presence will show that you're playing a game when you're not.

We can fix this by waiting for `MuMuPlayer.exe` to exit. If it does. We can clear the Rich Presence.

**Additional Concerns:**
- Access Violations

If by some chance `MuMuPlayer.exe` is ran as administrator, subscribing to their exit events will fail due to an access violation.

This should never realistically happen and I don't think the scope of our application should elevate to administrator to match it either.

We can probably poll query all `MuMuPlayer.exe` processes to wait for an exit like that. It's messy...

eg.
```csharp
// [ Async & Long Running ]

while (true)
{
    var process = Process.GetProcessesByName("MuMuPlayer").OrderBy(x => x.StartTime).FirstOrDefault();

    if (process == null)
    {
        _currentAppState = AppSessionState.Stopped;
        // Clear Rich Presence
        break;
    }
    await Task.Delay(TimeSpan.FromSeconds(5));
}

```