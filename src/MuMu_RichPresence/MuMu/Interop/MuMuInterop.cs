using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AlphaOmega.Debug;
using AlphaOmega.Debug.Manifest;
using CliWrap.Exceptions;
using Dawn.MuMu.RichPresence.Exceptions;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.Scrapers;
using Dawn.MuMu.RichPresence.Tools;
using Polly;
using Polly.Retry;

namespace Dawn.MuMu.RichPresence.MuMu.Interop;

public partial class MuMuInterop(ConnectionInfo adb) : IMuMuInterop
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private IAsyncDisposable? _connection;
    private readonly ResiliencePipeline _pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<NotConnectedException>(),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            MaxRetryAttempts = 5,
            Delay = TimeSpan.FromSeconds(1)
        })
        .Build();

    private async ValueTask KeepAlive(CancellationToken token = default)
    {
        if (_connection is not null)
            return;

        await _semaphore.WaitAsync(token);

        // We do this twice, since the first is a quick path, and the second prevents a race condition
        if (_connection is not null)
            return;
        try
        {
            // The pipeline handles the retries and the back-off delay internally
            _connection = await _pipeline.ExecuteAsync(async ct => await adb.Connect(ct), token);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async ValueTask<IAsyncDisposable> EnsureConnected(CancellationToken token = default)
    {
        if (!adb.KeepAlive)
            return await adb.Connect(token);

        await KeepAlive(token);
        // We return None since the methods using this disposes the Disposable after every invocation, so we use a renewable disposable stub
        return AnonymousAsyncDisposable.None;
    }

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
    public async Task<AppInfo?> GetForegroundAppInfo(CancellationToken token = default)
    {
        await using (await EnsureConnected(token))
        {
            // topResumedActivity=ActivityRecord{9846be1 u0 com.YoStarEN.Arknights/com.u8.sdk.U8UnityContext t365}
            // topResumedActivity=ActivityRecord{321bd4c u0 app.lawnchair/.LawnchairLauncher t2}
            // ResumedActivity: ActivityRecord{9846be1 u0 com.YoStarEN.Arknights/com.u8.sdk.U8UnityContext t365}
            var result = await adb.Execute("dumpsys activity activities | grep ResumedActivity", token: token);

            if (string.IsNullOrWhiteSpace(result))
                return null; // There's no ForegroundApp active

            var match = GetForegroundApp().Match(result);

            if (!match.Success)
            {
                Log.Verbose("Cound not find AppInfo, can't match {Regex} to {String}", GetForegroundApp(), result);
                return null;
            }

            var packageName = match.Groups["PackageName"].Value;

            var pid = await adb.Execute<int>($"pidof {packageName}", token: token);

            int startTime;
            try
            {
                startTime = await GetStartTime(pid, token);
            }
            catch (FormatException)
            {
                // The VM could be exiting, so we there's no AppInfo, the start time fails and returns an empty string.
                // The exception would be: System.FormatException: The input string '' was not in a correct format.
                return null;
            }

            return new AppInfo(packageName, pid, DateTimeOffset.FromUnixTimeSeconds(startTime));
        }
    }

    /*  We're executing this:

        cat /proc/<pid>/<path>
     */
    public async Task<string> GetInfo(AppInfo info, string path, CancellationToken token = default)
    {
        await using (await EnsureConnected(token))
            return await adb.Execute($"cat /proc/{info.Pid}/{path}", token: token);
    }

    /*  We're executing this:

        pm path <packageName>
    */
    public async Task<string> GetPackagePath(AppInfo info, CancellationToken token = default)
    {
        await using (await EnsureConnected(token))
            return (await adb.Execute($"pm path {info.PackageName}", token: token))
            .TrimStart("package:")
            .ToString();
    }

    public async Task<FileInfo> PullFile(string path, DirectoryInfo directory, CancellationToken token = default)
    {
        var fileName = Path.GetFileName(path);
        var targetFi = new FileInfo(Path.Combine(directory.FullName, fileName));

        if (targetFi.Exists)
            targetFi.Delete();

        // pull [-a] [-z ALGORITHM] [-Z] REMOTE... LOCAL
        //     copy files/dirs from device
        //     -a: preserve file timestamp and mode
        //     -q: suppress progress messages
        //     -Z: disable compression
        //     -z: enable compression with a specified algorithm (any/none/brotli/lz4/zstd)
        await using (await EnsureConnected(token))
        {
            // System.Exception: /data/local/tmp/resources.arsc: 1 file pulled, 0 skipped. 42.5 MB/s (1599740 bytes in 0.036s)
            // pulling files seems to use adb's std-err for some reason :shrug: we ignore them anyways via a #define
            await adb.ExecuteRaw<string>(["pull", path, directory.FullName], token);

            targetFi.Refresh();
            Debug.Assert(targetFi.Exists);

            return targetFi.Exists
                ? targetFi
                : throw new FileNotFoundException(targetFi.FullName);
        }
    }

    public async Task PullFile(string path, FileInfo file, CancellationToken token = default)
    {
        var fi = await PullFile(path, file.Directory!, token);

        fi.MoveTo(file.FullName);
        file.Refresh();
        Debug.Assert(file.Exists);

        if (!file.Exists)
            throw new FileNotFoundException(file.FullName);
    }


    /*  We're executing this:

        pm list packages -f <packageName>
        adb pull <packagePath> <directory>
    */
    public async Task<FileInfo> PullAPK(AppInfo info, DirectoryInfo directory, CancellationToken token = default)
    {
        var path = await GetPackagePath(info, token);

        directory.Create();
        var dest = new FileInfo(Path.Combine(directory.FullName, $"{info.PackageName}.apk"));

        Log.Debug("Pulling {PackageName} ({FileName}) to {DirectoryPath}", info.PackageName, Path.GetFileName(path), directory.FullName);

        await PullFile(path, dest, token);

        return dest;
    }

    private static readonly ConcurrentDictionary<string, string> _appLabelCache = new();
    public async Task<string> GetAppLabel(AppInfo info, CancellationToken token = default)
    {
        if (_appLabelCache.TryGetValue(info.PackageName, out var label))
            return label;

        try
        {
            label = await GetLabel(info, token);
        }
        catch (Exception e)
        {
            Log.Error(e, "Unable to fetch the app label for {PackageName}", info);
            label = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(label))
            _appLabelCache.TryAdd(info.PackageName, label);

        return label;
    }

    private const string VIRTUAL_TEMP_FOLDER = "/data/local/tmp";
    private async Task<string> GetLabel(AppInfo info, CancellationToken token)
    {
        Log.Debug("Fetching Label for {PackageName}", info.PackageName);
        var apkVirtualPath = await GetPackagePath(info, token);

        await using (await EnsureConnected(token))
        {
            // usage: unzip [-d DIR] [-lnopqv] ZIP [FILE...] [-x FILE...]
            //
            // Extract FILEs from ZIP archive. Default is all files. Both the include and
            // exclude (-x) lists use shell glob patterns.
            //
            // -d DIR  Extract into DIR
            // -j      Junk (ignore) file paths
            // -l      List contents (-lq excludes archive name, -lv is verbose)
            // -n      Never overwrite files (default: prompt)
            // -o      Always overwrite files
            // -p      Pipe to stdout
            // -q      Quiet
            // -t      Test compressed data (do not extract)
            // -v      List contents verbosely
            // -x FILE Exclude files

            // We're overwriting other AndroidManifest.xml files and other resrouces.arsc files
            // unzip -d /data/local/tmp -o <apk_path> AndroidManifest.xml resources.arsc
            await adb.Execute(["unzip", $"-d {VIRTUAL_TEMP_FOLDER}", "-o", apkVirtualPath, "AndroidManifest.xml", "resources.arsc"], token);

            FileInfo? manifestFi = null;
            FileInfo? resourcesFi = null;
            try
            {
                manifestFi = await PullFile($"{VIRTUAL_TEMP_FOLDER}/AndroidManifest.xml", CacheDirectory, token);
                resourcesFi = await PullFile($"{VIRTUAL_TEMP_FOLDER}/resources.arsc", CacheDirectory, token);

                await using var resourcesStream = resourcesFi.OpenRead();
                var resources = new ArscFile(resourcesStream);

                using var manifestFile = new AxmlFile(StreamLoader.FromFile(manifestFi.FullName));
                var manifest = AndroidManifest.Load(manifestFile, resources);

                var labelIndex = manifest.Application.Label
                    .AsSpan().TrimStart('@');

                var resourceId = int.Parse(labelIndex, NumberStyles.HexNumber);

                var label = resources.ResourceMap[resourceId].First().Value!;

                Log.Debug("Got label for {PackageName} ({Label})", info.PackageName, label);

                return label;
            }
            finally
            {
                manifestFi?.Delete();
                resourcesFi?.Delete();
            }
        }
    }

    public async Task<AndroidProcess?> GetFocusedApp(CancellationToken token = default)
    {
        // This times out if the emulator is starting up, since ADB waits for the emulator to fully start up
        // We can reasonably say that there's no focused app at that point
        AppInfo? app;
        try
        {
            app = await GetForegroundAppInfo(token);
        }
        catch (Exception e) when (e is OperationCanceledException or TaskCanceledException or NotConnectedException or CommandExecutionException)
        {
            return null;
        }
        catch (Exception e)
        {
            Log.Error(e, "Could not get ForegroundAppInfo");
            return null;
        }

        if (app is not { } info)
            return null;


        if (AppLifetimeParser.IsSystemLevelPackage(info.PackageName))
            return null;

        var title = await GetAppLabel(info, token);
        var session = new MuMuSessionLifetime { AppState = AppState.Focused, PackageName = info.PackageName, Title = title };

        if (!string.IsNullOrWhiteSpace(title))
            return new(title, info);

        var packageInfo = await PackageScraper.TryGetPackageInfo(session);
        if (packageInfo != null)
            return new(packageInfo.Title, info);

        Log.Debug("Could not find a title for Package '{PackageName}'",  info.PackageName);
        return null;
    }

    // --
    public static async Task<IMuMuInterop?> TryCreate(bool keepAlive = false)
    {
        var config = await GetVMConfig();
        if (config is null)
            return null;

        var adb = Pathfinder.GetADBFileInfo();
        if (adb is null)
            return null;

        var port = config.VM.NAT.PortForward.ADB;
        if (!int.TryParse(port.HostPort, out var portNumber) || string.IsNullOrWhiteSpace(port.GuestIP))
            return null;

        var connectionInfo = new ConnectionInfo(port.GuestIP, portNumber, adb.FullName, keepAlive);
        return new MuMuInterop(connectionInfo);
    }

    // The VM Config contains the local IP of the emulator, which we can use to connect to via ADB,
    // MuMu also has 2 identical versions of ADB, we can use them
    private static async Task<VMConfig?> GetVMConfig()
    {
        var rootPath = Pathfinder.TryGetRootDirectory();

        var vms = rootPath?.GetDirectories("vms").FirstOrDefault();

        var configFile = vms?.GetFiles("vm_config.json", SearchOption.AllDirectories).FirstOrDefault();

        if (configFile is not { Exists: true})
            return null;

        await using var file = configFile.OpenRead();

        var config = await JsonSerializer.DeserializeAsync<VMConfig>(file);

        return config;
    }

    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        GC.SuppressFinalize(this);
        if (_connection != null)
            await _connection.DisposeAsync();
    }
}
