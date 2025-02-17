using System.Collections.ObjectModel;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.Tools;

namespace Dawn.MuMu.RichPresence;

using System.Globalization;
using System.Text.RegularExpressions;
using global::Serilog;

internal static partial class AppLifetimeParser
{
    private static readonly string[] SystemLevelPackageHints =
        [
            "com.android",
            "com.google",
            "com.mumu"
        ];

    private static partial class ShellRegexes
    {
        // [00:45:31.968 13508/13680][info][:] OnFocusOnApp Called: id=task:22, packageName=com.SmokymonkeyS.Triglav, appName=Triglav
        [GeneratedRegex("\\[(?'StartTime'.+?) .+?OnFocusOnApp Called:.+?packageName=(?'PackageName'.+?), appName=(?'Title'.+?)$", RegexOptions.Multiline)]
        internal static partial Regex FocusedAppRegex();

        // [00:45:31.970 13508/11972][info][:] [Gateway] onAppLaunch: package=com.SmokymonkeyS.Triglav code= msg=
        [GeneratedRegex(@"\[(?'StartTime'.+?) (?'ProcessId'\d+?)/.+?onAppLaunch: package=(?'PackageName'.+?) ", RegexOptions.Multiline)]
        internal static partial Regex AppLaunchRegex();

        // [00:52:32.304 13508/11972][info][:] [ShellWindow::onTabClose]: index: 1, app info: [id:task:22, packageName:com.SmokymonkeyS.Triglav, appName:Triglav, originName:Triglav, displayId:7]
        [GeneratedRegex(@"\[ShellWindow::onTabClose]:.+?packageName:(?'PackageName'.+?),", RegexOptions.Multiline)]
        internal static partial Regex TabCloseRegex();
    }

    public static void MutateLifetime(string info, ObservableCollection<MuMuSessionLifetime> lifetimes, ObservableCollection<MuMuSessionLifetime> graveyard)
    {
        try
        {
            if (IsAppLaunchEvent(info, out var packageName, out var startTime, out var pid))
            {
                CreateAppLaunchEvent(info, packageName, startTime, pid, lifetimes, graveyard);
                return;
            }

            if (IsTabCloseEvent(info, out packageName))
            {
                CreateTabCloseEvent(packageName, lifetimes, graveyard);
                return;
            }

            if (IsFocusEvent(info, out packageName, out var title, out startTime))
                CreateFocusEvent(info, packageName, title, startTime, lifetimes);

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
                StartTime = approximateStartTime
            };
            lifetimes.Add(existingLifetime);
        }
        else
        {
            existingLifetime.SessionSubscriptions?.Cancel();

            if (existingLifetime.AppState.Value != AppState.Focused)
                existingLifetime.AppState.Value = AppState.Started;

            existingLifetime.SessionSubscriptions = cts;

            if (existingLifetime.StartTime == default)
               existingLifetime.StartTime = approximateStartTime;
        }
        existingLifetime.PackageLifetimeEntries.Add(info);

        ProcessExit.Subscribe(pid, _ =>
        {
            try
            {
                lifetimes.Remove(existingLifetime);
                Log.Debug("The gravekeeper has come for {LifetimePackageName}, MuMu Player has exited", existingLifetime.PackageName);
                graveyard.Add(existingLifetime);
            }
            catch { }

        }, cts.Token);

        // Log.Verbose("[{StartTime:hh:mm}] App Launched: {PackageName}", existingLifetime.StartTime.ToLocalTime(), packageName);
    }

    private static void CreateTabCloseEvent(string packageName, ObservableCollection<MuMuSessionLifetime> lifetimes, ObservableCollection<MuMuSessionLifetime> graveyard)
    {
        var existingLifetime = lifetimes.FirstOrDefault(x => x.PackageName == packageName);
        if (existingLifetime == null)
            return;

        existingLifetime.SessionSubscriptions?.Cancel();
        existingLifetime.SessionSubscriptions?.Dispose();
        existingLifetime.SessionSubscriptions = null;
        existingLifetime.AppState.Value = AppState.Stopped;

        lifetimes.Remove(existingLifetime);
        graveyard.Add(existingLifetime);

        Log.Verbose("[{StartTime:hh:mm}] Tab Closed: {Title} ({PackageName})", existingLifetime.StartTime.ToLocalTime(), existingLifetime.Title, packageName);
    }

    private static void CreateFocusEvent(string info, string packageName, string title, TimeSpan time, ObservableCollection<MuMuSessionLifetime> lifetimes)
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

        Log.Information("[{StartTime:hh:mm}] Focused: {Title} ({PackageName})", existingLifetime.StartTime.ToLocalTime(), title, packageName);
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

    private static bool IsTabCloseEvent(string info, out string packageName)
    {
        packageName = string.Empty;

        var match = ShellRegexes.TabCloseRegex().Match(info);
        if (!match.Success)
            return false;

        packageName = match.Groups["PackageName"].Value;

        return true;
    }

    private static bool IsFocusEvent(string info, out string packageName, out string title, out TimeSpan timeSpan)
    {
        packageName = string.Empty;
        title = string.Empty;
        timeSpan = TimeSpan.Zero;

        var match = ShellRegexes.FocusedAppRegex().Match(info);
        if (!match.Success)
            return false;

        packageName = match.Groups["PackageName"].Value;
        title = match.Groups["Title"].Value;
        var startTimeString = match.Groups["StartTime"].Value;

        return TimeSpan.TryParse(startTimeString, out timeSpan);
    }

    public static bool IsSystemLevelPackage(string packageName) => SystemLevelPackageHints.Any(packageName.StartsWith);
}
