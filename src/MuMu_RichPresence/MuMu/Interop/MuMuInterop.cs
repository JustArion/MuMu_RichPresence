using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.Tools;

namespace Dawn.MuMu.RichPresence.MuMu.Interop;

public partial class MuMuInterop(ConnectionInfo adb) : IMuMuInterop
{
    [GeneratedRegex(@"^\W+?ResumedActivity: ActivityRecord{.+?\b(?'PackageName'[a-zA-Z0-9._]+(?:\.[a-zA-Z0-9_]+)*)\/.+?}", RegexOptions.Multiline)]
    private partial Regex GetForegroundApp();

    /*  We're executing this:

        clk=$(getconf CLK_TCK)
        boot=$(awk '{print int($1)}' /proc/uptime)
        start_ticks=$(awk '{print $22}' /proc/<our_pid>/stat)
        start=$((start_ticks / clk))
        now=$(date +%s)
        echo $((now - boot + start))
     */
    private async Task<int> GetStartTime(int pid, CancellationToken token = default)
    {
        var sb = new StringBuilder();
        // Gets the amount of ticks per second (Usually 100)
        sb.AppendLine("clk=$(getconf CLK_TCK)");

        // Gets the amount of ticks elapsed since system start
        sb.AppendLine("boot=$(awk '{print int($1)}' /proc/uptime)");

        // https://man7.org/linux/man-pages/man5/proc_pid_stat.5.html
        // We get the 22nd field in stat, which according to the man pages is the `starttime`
        sb.AppendLine($"start_ticks=$(awk '{{print $22}}' /proc/{pid}/stat)");

        // Gets the start time
        sb.AppendLine("start=$((start_ticks / clk))");

        // Gets the current time
        sb.AppendLine("now=$(date +%s)");

        // Prints the process's start time as a unix timestamp
        sb.AppendLine("echo $((now - boot + start))");

        var command = string.Join(" && ", sb.ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

        return await adb.Execute<int>(command, token: token);
    }

    /*  We're executing this:

        dumpsys activity activities | grep ResumedActivity
        pidof <package_name>
        -> GetStartTime
    */
    public async Task<AppInfo> GetForegroundAppInfo(CancellationToken token = default)
    {
        await using (await adb.Connect(token: token))
        {
            // topResumedActivity=ActivityRecord{9846be1 u0 com.YoStarEN.Arknights/com.u8.sdk.U8UnityContext t365}
            // topResumedActivity=ActivityRecord{321bd4c u0 app.lawnchair/.LawnchairLauncher t2}
            // ResumedActivity: ActivityRecord{9846be1 u0 com.YoStarEN.Arknights/com.u8.sdk.U8UnityContext t365}
            var result = await adb.Execute("dumpsys activity activities | grep ResumedActivity", token: token);
            var match = GetForegroundApp().Match(result);

            if (!match.Success)
                throw new Exception($"Could not find AppInfo, can't match {GetForegroundApp()} to {result}");

            var packageName = match.Groups["PackageName"].Value;

            var pid = await adb.Execute<int>($"pidof {packageName}", token: token);

            var startTime = await GetStartTime(pid, token);

            return new AppInfo(packageName, pid, DateTimeOffset.FromUnixTimeSeconds(startTime));
        }
    }

    /*  We're executing this:

        cat /proc/<pid>/<path>
     */
    public async Task<string> GetInfo(AppInfo info, string path, CancellationToken token = default) => await adb.Execute($"cat /proc/{info.Pid}/{path}", token: token);

    // Gets the focused non-system app
    public async Task<AndroidProcess?> GetFocusedApp(CancellationToken token = default)
    {
        // This times out if the emulator is starting up, since ADB waits for the emulator to fully start up
        // We can reasonably say that there's no focused app at that point
        if (token == CancellationToken.None)
            token = new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;
        AppInfo app;
        try
        {
            app = await GetForegroundAppInfo(token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        if (app == default)
            return null;

        if (AppLifetimeParser.IsSystemLevelPackage(app.PackageName))
            return null;

        var info = await PlayStoreWebScraper.TryGetPackageInfo(app.PackageName);

        if (info != null)
            return new(info.Title, app);

        Log.Debug("Could not find a title for Package '{PackageName}'",  app.PackageName);
        return null;
    }

}
