
using System.Collections.Concurrent;

namespace Dawn.MuMu.RichPresence.PlayGames;

using System.Globalization;
using System.Text.RegularExpressions;
using Domain;
using global::Serilog;

internal static partial class AppSessionInfoBuilder
{
    private static readonly string[] SystemLevelPackageNames =
        [
            "com.android",
            "com.google",
            "com.mumu"
        ];

    private static partial class ShellRegexes
    {
        // [00:45:31.968 13508/13680][info][:] OnFocusOnApp Called: id=task:22, packageName=com.SmokymonkeyS.Triglav, appName=Triglav
        [GeneratedRegex("OnFocusOnApp Called:.+?packageName=(?'PackageName'.+?), appName=(?'Title'.+?)$", RegexOptions.Multiline)]
        internal static partial Regex FocusedAppRegex();

        // [00:45:31.970 13508/11972][info][:] [Gateway] onAppLaunch: package=com.SmokymonkeyS.Triglav code= msg=
        [GeneratedRegex(@"\[(?'StartTime'.+?) (?'ProcessId'\d+?)/.+?onAppLaunch: package=(?'PackageName'.+?) ", RegexOptions.Multiline)]
        internal static partial Regex AppLaunchRegex();

        // [00:52:32.304 13508/11972][info][:] [ShellWindow::onTabClose]: index: 1, app info: [id:task:22, packageName:com.SmokymonkeyS.Triglav, appName:Triglav, originName:Triglav, displayId:7]
        [GeneratedRegex(@"\[ShellWindow::onTabClose]:.+?packageName:(?'PackageName'.+?),", RegexOptions.Multiline)]
        internal static partial Regex TabCloseRegex();
    }

    private record struct PackageStartInfo(DateTimeOffset StartTime, CancellationTokenSource CancellationTokenSource);
    private static readonly ConcurrentDictionary<string, PackageStartInfo> _startTimeCache = [];
    public static MuMuSessionInfo? Build(string info)
    {
        PackageStartInfo startInfo;

        if (IsAppLaunchEvent(info, out var packageName, out var startTime, out var pid))
        {
            if (IsSystemLevelPackage(packageName))
                return null;

            var cts = new CancellationTokenSource();

            var approximateStartTime = GetApproximateStartTime(startTime);

            _startTimeCache.AddOrUpdate(packageName, new PackageStartInfo(approximateStartTime, cts), (_, _) => new (approximateStartTime, cts));

            ProcessExit.Subscribe(pid, _ =>
            {
                _startTimeCache.TryRemove(packageName, out var _);
                Log.Debug("{PackageName} has exited", packageName);
            }, cts.Token);

            Log.Verbose("{StartTime} | App Launched: {PackageName}", startTime, packageName);
            return null;
        }

        if (IsTabCloseEvent(info, out packageName))
        {
            _startTimeCache.TryRemove(packageName, out startInfo);
            startInfo.CancellationTokenSource.Cancel();
            Log.Verbose("{StartTime} | Tab Closed: {PackageName}", startInfo.StartTime, packageName);

            return new MuMuSessionInfo(packageName, DateTimeOffset.UnixEpoch, "Closing", AppSessionState.Stopped);
        }

        if (!IsFocusEvent(info, out packageName, out var title) || IsSystemLevelPackage(packageName))
            return null;

        return _startTimeCache.TryGetValue(packageName, out startInfo)
            ? new MuMuSessionInfo(packageName, startInfo.StartTime, title, AppSessionState.Focused)
            : null;
    }

    // The log format is "00:45:31.968" which is a timespan, not a timestamp
    // Timestamp: 16:00
    // Current Time: 13:06
    // It's probably safe to assume the app has been started yesterday since logs from the future haven't been invented yet
    // We lose all timestamp data recorded earlier than the last +-48 hrs though
    private static DateTimeOffset GetApproximateStartTime(TimeSpan startTime)
    {
        var today = DateTimeOffset.Now.Date;

        return today.Add(startTime) > DateTimeOffset.Now
            ? today.Add(startTime).AddDays(-1)
            : today.Add(startTime);
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

    private static bool IsFocusEvent(string info, out string packageName, out string title)
    {
        packageName = string.Empty;
        title = string.Empty;

        var match = ShellRegexes.FocusedAppRegex().Match(info);
        if (!match.Success)
            return false;

        packageName = match.Groups["PackageName"].Value;
        title = match.Groups["Title"].Value;

        return true;
    }

    private static bool IsSystemLevelPackage(string packageName) => SystemLevelPackageNames.Contains(packageName);
}
