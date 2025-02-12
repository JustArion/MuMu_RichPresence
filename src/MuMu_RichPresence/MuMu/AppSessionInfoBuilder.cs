
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
                                                       // 9/24/2024 1:05:02 PM +00:00
        internal const string STARTED_TIMESTAMP_FORMAT = "M/d/yyyy h:mm:ss tt zzz";

        [GeneratedRegex("OnFocusOnApp Called:.+?packageName=(?'PackageName'.+?), appName=(?'Title'.+?)$", RegexOptions.Multiline)]
        internal static partial Regex FocusedAppRegex(); // package_name=com.YoStarEN.Arknights

        [GeneratedRegex("onAppLaunch: package=(?'PackageName'.+?) ", RegexOptions.Multiline)]
        internal static partial Regex AppLaunchRegex(); // title=Arknights

        [GeneratedRegex(@"\[ShellWindow::onTabClose]:.+?packageName:(?'PackageName'.+?),", RegexOptions.Multiline)]
        internal static partial Regex TabCloseRegex(); // started_timestamp=9/24/2024 1:05:02 PM +00:00
    }

    // [00:45:31.970 13508/11972][info][:] [Gateway] onAppLaunch: package=com.SmokymonkeyS.Triglav code= msg=
    // [00:45:31.968 13508/13680][info][:] OnFocusOnApp Called: id=task:22, packageName=com.SmokymonkeyS.Triglav, appName=Triglav
    // [00:52:32.304 13508/11972][info][:] [ShellWindow::onTabClose]: index: 1, app info: [id:task:22, packageName:com.SmokymonkeyS.Triglav, appName:Triglav, originName:Triglav, displayId:7]
    public static PlayGamesSessionInfo? Build(string info)
    {


    }
}
