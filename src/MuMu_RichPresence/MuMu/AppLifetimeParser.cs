using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.Tools;

namespace Dawn.MuMu.RichPresence.MuMu;

internal static partial class AppLifetimeParser
{
    private static readonly string[] SystemLevelPackageHints =
        [
            "com.android",
            "com.google",
            "com.mumu",
            // Added in MuMu Player 5
            "com.netease.mumu",
            "app.lawnchair" // (Default home screen)
        ];

    private static partial class ShellRegexes
    {
        // [00:45:31.968 13508/13680][info][:] OnFocusOnApp Called: id=task:22, packageName=com.SmokymonkeyS.Triglav, appName=Triglav
        [GeneratedRegex(@"\[(?'StartTime'.+?) (?'ProcessId'\d+?)/.+?OnFocusOnApp Called:.+?packageName=(?'PackageName'.+?), appName=(?'Title'.+?)$", RegexOptions.Multiline)]
        internal static partial Regex FocusedAppRegex();

        // [00:45:31.970 13508/11972][info][:] [Gateway] onAppLaunch: package=com.SmokymonkeyS.Triglav code= msg=
        [GeneratedRegex(@"\[(?'StartTime'.+?) (?'ProcessId'\d+?)/.+?onAppLaunch: package=(?'PackageName'.+?) ", RegexOptions.Multiline)]
        internal static partial Regex AppLaunchRegex();

        // [00:52:32.304 13508/11972][info][:] [ShellWindow::onTabClose]: index: 1, app info: [id:task:22, packageName:com.SmokymonkeyS.Triglav, appName:Triglav, originName:Triglav, displayId:7]
        [GeneratedRegex(@"\[ShellWindow::onTabClose]:.+?packageName:(?'PackageName'.+?),", RegexOptions.Multiline)]
        internal static partial Regex TabCloseRegex();

        // [20:00:06.264 18088/26636][info]  AppInfo {id:task:287, name:Arknights, originName:Arknights, packageName:com.YoStarEN.Arknights, tag:, icon_size:2548, isLauncher0, displayId:0, rotation:90, pid:5020, uid:10046, isNewTask:1, canChangeAppOrient:0}
        [GeneratedRegex(@"\[(?'StartTime'.+?) (?'ProcessId'\d+?)/.+?AppInfo.+?{.+?packageName:(?'PackageName'.+?),.+?isNewTask:(?'IsNewTask'.+?),")]
        internal static partial Regex AppInfoChangeRegex();
    }

    [SuppressMessage("ReSharper", "InlineOutVariableDeclaration")]
    public static void MutateLifetime(string info, ObservableCollection<MuMuSessionLifetime> lifetimes, ObservableCollection<MuMuSessionLifetime> graveyard)
    {
        try
        {
            string? packageName;
            TimeSpan startTime;
            int pid;

            // Removed due to newer versions including "AppLaunch" whenever you focus a tab (Like ??? who thinks that's a good idea)
            // if (IsAppLaunchEvent(info, out packageName, out startTime, out var pid))
            // {
            //     CreateAppLaunchEvent(info, packageName, startTime, pid, lifetimes, graveyard);
            //     return;
            // }

            if (IsAppInfoEvent(info, out packageName, out startTime, out pid, out var isNewTask))
            {
                if (!isNewTask)
                    return;

                Log.Verbose("Creating AppInfo Event: {AppInfo}", new { packageName, startTime, pid, isNewTask });
                CreateAppLaunchEvent(info, packageName, startTime, pid, lifetimes, graveyard);
                return;
            }

            if (IsTabCloseEvent(info, out packageName))
            {
                Log.Verbose("Creating TabClose Event: {AppCloseEvent}", packageName);
                CreateTabCloseEvent(packageName, lifetimes, graveyard);
                return;
            }

            if (IsFocusEvent(info, out packageName, out var title, out startTime, out pid))
            {
                Log.Verbose("Creating FocusEvent: {AppFocusEvent}", new { packageName, title, startTime, pid });
                CreateFocusEvent(info, packageName, title, startTime, pid, lifetimes, graveyard);
            }

        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to build session info");
        }
    }

    private static void CreateAppLaunchEvent(string info, string packageName, TimeSpan startTime, int pid, ObservableCollection<MuMuSessionLifetime> lifetimes, ObservableCollection<MuMuSessionLifetime> graveyard)
    {
        var cts = new CancellationTokenSource();

        var approximateStartTime = GetApproximateStartTime(startTime);

        var existingLifetime = lifetimes.FirstOrDefault(x => x.PackageName == packageName);
        if (existingLifetime == null)
        {
            existingLifetime = new MuMuSessionLifetime
            {
                Title = packageName,
                PackageName = packageName,
                AppState = AppState.Started,
                SessionSubscriptions = cts,
                StartTime = approximateStartTime,
            };
            lifetimes.Add(existingLifetime);
        }
        else
        {
            lock (existingLifetime.SynchronizationRoot)
            {
                existingLifetime.SessionSubscriptions?.Cancel();
                existingLifetime.SessionSubscriptions = cts;
            }

            if (existingLifetime.AppState.Value != AppState.Focused)
                existingLifetime.AppState.Value = AppState.Started;

            // if (existingLifetime.StartTime == default)
               existingLifetime.StartTime = approximateStartTime;
        }
        existingLifetime.PackageLifetimeEntries.Add(info);

        try
        {
            var proc = Process.GetProcessById(pid);

            // Reading old lifetimes can cause subscriptions to pids that are no longer associated with the emulator
            if (!Pathfinder.EmulatorProcessNames.Contains(proc.ProcessName))
            {
                OnExit(0);
                return;
            }

            ProcessExit.Subscribe(pid, OnExit, cts.Token);
        }
        catch
        {
            OnExit(0);
        }


        Log.Verbose("[{StartTime:hh:mm}] App Launched: {PackageName}", existingLifetime.StartTime.ToLocalTime(), packageName);
        return;

        void OnExit(int _)
        {
            try
            {
                ClearTabLifetime(existingLifetime, lifetimes, graveyard);
                Log.Debug("The gravekeeper has come for {LifetimePackageName}, MuMu Player has exited", existingLifetime.PackageName);
            }
            catch
            {
                // ignored
            }
        }
    }

    private static void ClearTabLifetime(MuMuSessionLifetime lifetime, ObservableCollection<MuMuSessionLifetime> lifetimes, ObservableCollection<MuMuSessionLifetime> graveyard)
    {
        lock (lifetime.SynchronizationRoot)
        {
            lifetime.SessionSubscriptions?.Cancel();
            lifetime.SessionSubscriptions?.Dispose();
            lifetime.SessionSubscriptions = null;
        }

        lifetime.AppState.Value = AppState.Stopped;

        lifetimes.Remove(lifetime);
        graveyard.Add(lifetime);
    }
    private static void CreateTabCloseEvent(string packageName, ObservableCollection<MuMuSessionLifetime> lifetimes, ObservableCollection<MuMuSessionLifetime> graveyard)
    {
        var existingLifetime = lifetimes.FirstOrDefault(x => x.PackageName == packageName);
        if (existingLifetime == null)
            return;

        ClearTabLifetime(existingLifetime, lifetimes, graveyard);

        Log.Verbose("[{StartTime:hh:mm}] Tab Closed: {Title} ({PackageName})", existingLifetime.StartTime.ToLocalTime(), existingLifetime.Title, packageName);
    }

    private static void CreateFocusEvent(string info, string packageName, string title, TimeSpan time, int pid, ObservableCollection<MuMuSessionLifetime> lifetimes, ObservableCollection<MuMuSessionLifetime> graveyard)
    {
        var existingLifetime = lifetimes.FirstOrDefault(x => x.PackageName == packageName);
        if (existingLifetime == null)
        {
            existingLifetime = new MuMuSessionLifetime { Title = title, PackageName = packageName, AppState = AppState.Focused };
            lifetimes.Add(existingLifetime);
        }
        existingLifetime.PackageLifetimeEntries.Add(info);

        if (existingLifetime.StartTime == default)
            existingLifetime.StartTime = GetApproximateStartTime(time);

        existingLifetime.Title = title;

        existingLifetime.AppState.Value = AppState.Focused;
        foreach (var lifetime in lifetimes.Where(x => x != existingLifetime))
            lifetime.AppState.Value = AppState.Unfocused;

        Log.Verbose("[{StartTime:hh:mm}] Focused: {Title} ({PackageName})", existingLifetime.StartTime.ToLocalTime(), title, packageName);

        try
        {
            var proc = Process.GetProcessById(pid);

            if (!Pathfinder.EmulatorProcessNames.Contains(proc.ProcessName))
                ClearTabLifetime(existingLifetime, lifetimes, graveyard);
        }
        catch
        {
            // There's no Process associated with the PID
            ClearTabLifetime(existingLifetime, lifetimes, graveyard);
        }
    }

    // The log format is "00:45:31.968" which is a timespan, not a timestamp
    // Timestamp: 16:00
    // Current Time: 13:06
    // It's probably safe to assume the app has been started yesterday since logs from the future haven't been invented yet
    // We lose all timestamp data recorded earlier than the last +-48 hrs though
    private static DateTimeOffset GetApproximateStartTime(TimeSpan startTime)
    {
        var today = DateTimeOffset.Now.Date;

        return (today.Add(startTime) > DateTimeOffset.Now
            ? today.Add(startTime).AddDays(-1)
            : today.Add(startTime)).ToUniversalTime();
    }

    private static bool IsAppLaunchEvent(string info, out string packageName, out TimeSpan startTime, out int processId)
    {
        packageName = string.Empty;
        startTime = TimeSpan.Zero;
        processId = 0;

        var match = ShellRegexes.AppLaunchRegex().Match(info);
        if (!match.Success)
            return false;

        packageName = match.Groups["PackageName"].Value;

        processId = int.Parse(match.Groups["ProcessId"].Value, CultureInfo.InvariantCulture);

        var startTimeString = match.Groups["StartTime"].Value;
        if (TimeSpan.TryParse(startTimeString, out startTime))
            return true;

        Log.Warning("Failed to parse start time: {StartTime}", startTimeString);
        return false;
    }

    private static bool IsAppInfoEvent(string info, out string packageName, out TimeSpan startTime, out int processId, out bool isNewTask)
    {
        packageName = string.Empty;
        startTime = TimeSpan.Zero;
        processId = 0;
        isNewTask = false;

        var match = ShellRegexes.AppInfoChangeRegex().Match(info);
        if (!match.Success)
            return false;

        packageName = match.Groups["PackageName"].Value;

        processId = int.Parse(match.Groups["ProcessId"].Value, CultureInfo.InvariantCulture);

        isNewTask = int.Parse(match.Groups["IsNewTask"].Value, CultureInfo.InvariantCulture) == 1;

        var startTimeString = match.Groups["StartTime"].Value;
        if (TimeSpan.TryParse(startTimeString, out startTime))
            return true;

        Log.Warning("Failed to parse start time: {StartTime}", startTimeString);
        return false;
    }

    private static bool IsTabCloseEvent(string info, out string packageName)
    {
        packageName = string.Empty;

        var match = ShellRegexes.TabCloseRegex().Match(info);
        if (!match.Success)
            return false;

        packageName = match.Groups["PackageName"].Value;

        return true;
    }

    private static bool IsFocusEvent(string info, out string packageName, out string title, out TimeSpan timeSpan, out int pid)
    {
        packageName = string.Empty;
        title = string.Empty;
        timeSpan = TimeSpan.Zero;
        pid = 0;

        var match = ShellRegexes.FocusedAppRegex().Match(info);
        if (!match.Success)
            return false;

        packageName = match.Groups["PackageName"].Value;
        title = match.Groups["Title"].Value;
        var startTimeString = match.Groups["StartTime"].Value;

        return int.TryParse(match.Groups["ProcessId"].Value, out pid)
               && TimeSpan.TryParse(startTimeString, out timeSpan);
    }

    public static bool IsSystemLevelPackage(string packageName) => SystemLevelPackageHints.Any(packageName.StartsWith);
}
