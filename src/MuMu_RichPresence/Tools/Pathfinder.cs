using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using Shortcut = ShellLink.Shortcut;

namespace Dawn.MuMu.RichPresence.Tools;

internal static class Pathfinder
{
    [SuppressMessage("ReSharper", "RemoveRedundantBraces")]
    public static async ValueTask<FileInfo> GetOrWaitForLogFilePath()
    {
        if (TryGetLogPathFromProcess(out var logPath) || TryGetFromShortcut(out logPath))
            return logPath;

        Log.Warning("Unable to find MuMu Player path, waiting for the player to start instead");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync())
        {
            if (TryGetLogPathFromProcess(out logPath))
                return logPath;
        }

        throw new UnreachableException("Failed to get path from process");
    }

    internal static bool TryGetLogPathFromProcess([NotNullWhen(true)] out FileInfo? logPath)
    {
        logPath = null;
        try
        {
            foreach (var emulatorName in MuMuProcessNames)
                if (PathFromPlayer(emulatorName) is { } validPath)
                {
                    logPath = validPath;
                    return true;
                }

            return false;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to get path from process");
            return false;
        }
    }

    private static FileInfo? PathFromPlayer(string playerProcessName)
    {
        var proc = Process.GetProcessesByName(playerProcessName).FirstOrDefault();
        if (proc == null)
            return null;

        var mumuPath = new FileInfo(proc.MainModule!.FileName);

        var mumuDirectory = mumuPath.Directory;
        if (mumuDirectory == null)
            return null;

        var retVal = GetLogPathFromBaseDirectory(mumuDirectory);
        Log.Verbose("Found log path from process: {ProcessName}.exe ({ProcessId}) -> {MuMuPlayerPath} -> {Path}", proc.ProcessName, proc.Id, mumuPath.FullName, retVal.FullName);

        return retVal;
    }

    public static FileInfo? GetADBFileInfo(DirectoryInfo vm) => vm.GetFiles("adb.exe", SearchOption.AllDirectories).FirstOrDefault();

    /// C:\Program Files\Netease\MuMuPlayer\nx_device
    public static DirectoryInfo[] GetDevices() => FromRootDirectory("nx_device")?.GetDirectories() ?? [];

    /// C:\Program Files\Netease\MuMuPlayer\vms
    public static DirectoryInfo[] GetVMs() => FromRootDirectory("vms")?.GetDirectories() ?? [];

    private static DirectoryInfo? FromRootDirectory(string directoryName)
    {
        // C:\Program Files\Netease\MuMuPlayer\
        var root = TryGetRootDirectory();

        if (root != null)
            return root.GetDirectories(directoryName)
                .FirstOrDefault();

        Log.Debug("Couldn't find any MuMu root directories");
        return null;

    }

    public static DirectoryInfo? TryGetRootDirectory() =>
        TryGetRootDirectoryFromProcess(out var rootDirectory) || TryGetRootDirectoryFromShortcut(out rootDirectory)
        ? rootDirectory
        : null;

    private static bool TryGetRootDirectoryFromProcess([NotNullWhen(true)] out DirectoryInfo? rootDirectory)
    {
        rootDirectory = null;

        // NxMain is the emulator manager
        // NxDevice is the emulator
        foreach (var procName in (Span<string>)["MuMuNxMain", "MuMuNxDevice"])
        {
            var process = Process.GetProcessesByName(procName).FirstOrDefault();

            if (process == null)
                continue;

            var procPath = process.MainModule!.FileName;

            rootDirectory = procName switch
            {
                // ..\MuMuPlayerGlobal-12.0\nx_main\MuMuNxMain.exe
                "MuMuNxMain" => new DirectoryInfo(Path.GetDirectoryName(procPath)!).Parent,

                // ..\MuMuPlayerGlobal-12.0\nx_device\12.0\shell\MuMuNxDevice.exe
                "MuMuNxDevice" => new DirectoryInfo(Path.Combine(Path.GetDirectoryName(procPath)!, @"..\..\..\")),
                _ => rootDirectory
            };
        }

        return rootDirectory is { Exists: true };
    }


    internal static readonly string[] EmulatorProcessNames =
        [
            "MuMuPlayer",
            "MuMuNxDevice" // \MuMuPlayerGlobal-12.0\nx_device\12.0\shell\MuMuNxDevice.exe
        ];

    private static readonly string[] MuMuProcessNames =
    [
        "MuMuPlayer",
        "MuMuNxMain", // \MuMuPlayerGlobal-12.0\nx_main\MuMuNxMain.exe
    ];

    private static bool TryGetRootDirectoryFromShortcut([NotNullWhen(true)] out DirectoryInfo? rootDirectory)
    {
        Log.Verbose("Trying to get root directory from shortcut");
        rootDirectory = null!;

        try
        {
            var dir = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.Programs));

            var mumuShortcutDirectory = dir.GetDirectories("MuMu*").FirstOrDefault();
            if (mumuShortcutDirectory == null)
                return false;

            var playerLink = mumuShortcutDirectory.GetFiles("MuMu Player*").FirstOrDefault()
                             ?? mumuShortcutDirectory.GetFiles("MuMuPlayer*").FirstOrDefault();

            if (playerLink == null)
                return false;

            var shortcut = Shortcut.ReadFromFile(playerLink.FullName);

            if (shortcut == null)
                return false;

            var shortcutPath = shortcut.LinkInfo.LocalBasePath;

            // ..\MuMuPlayerGlobal-12.0\nx_main\MuMuNxMain.exe
            var mumuPath = new FileInfo(shortcutPath);
            if (!mumuPath.Exists) // The shortcut could be broken, so we check
                return false;

            rootDirectory = mumuPath.Directory!;
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to get path from shortcut");
            return false;
        }
    }


    [SupportedOSPlatform("windows")]
    public static bool TryGetFromShortcut(out FileInfo logPath)
    {
        Log.Verbose("Trying to get path from shortcut");
        logPath = null!;

        try
        {
            var dir = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.Programs));

            var mumuShortcutDirectory = dir.GetDirectories("MuMu*").FirstOrDefault();
            if (mumuShortcutDirectory == null)
                return false;

            var playerLink = mumuShortcutDirectory.GetFiles("MuMu Player*").FirstOrDefault()
                             ?? mumuShortcutDirectory.GetFiles("MuMuPlayer*").FirstOrDefault();

            if (playerLink == null)
                return false;

            var shortcut = Shortcut.ReadFromFile(playerLink.FullName);

            if (shortcut == null)
                return false;

            var shortcutPath = shortcut.LinkInfo.LocalBasePath;
            var mumuPath = new FileInfo(shortcutPath);
            if (!mumuPath.Exists)
                return false;

            var mumuDirectory = mumuPath.Directory!;

            logPath = GetLogPathFromBaseDirectory(mumuDirectory);
            Log.Verbose("Found log path from shortcut: {LinkPath} -> {MuMuPlayerPath} -> {Path}", playerLink.FullName, shortcutPath, logPath.FullName);
            return true;

        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to get path from shortcut");
            return false;
        }
    }

    public static Version? GetRunningNxDeviceVersion()
    {
        Version? _priorityTarget = null;
        var nxDevicePaths = Process.GetProcessesByName(NxDevice)
            .Select(x => x.MainModule?.FileName)
            .Where(x => x != null)
            .Distinct()
            .Select(x => new FileInfo(x!))
            .ToArray();

        if (nxDevicePaths.Length > 1)
            Log.Warning("Multiple Nx Devices detected, will prioritize last (Highest version) ({Versions})", string.Join(", ", nxDevicePaths.Select(x => x.FullName)));

        var nxDevicePath = nxDevicePaths.LastOrDefault();
        if (nxDevicePath == null)
            return _priorityTarget;

        var current = nxDevicePath.Directory;
        while (current != null)
        {
            if (Version.TryParse(current.Name, out _priorityTarget))
            {
                Log.Verbose("Found a priority target for interop: {Path}", current.Name);
                break;
            }
            current = current.Parent;
        }

        return _priorityTarget;
    }

    //                                                 C:\Program Files\Netease\MuMuPlayer\nx_main
    private static FileInfo GetLogPathFromBaseDirectory(DirectoryInfo mumuDirectory)
    {
        Log.Verbose("Trying to get the logs path from {Directory}", mumuDirectory.FullName);
        // MuMuPlayerGlobal-12.0\vms
        // C:\Program Files\Netease\MuMuPlayer\vms
        var vms = mumuDirectory.Parent!.GetDirectories("vms")
            .First();

        return vms.GetDirectories()
            .Last() // Last is usually the highest version of Android
            .GetFiles("shell.log", SearchOption.AllDirectories)
            .First();
    }

    public const string NxDevice = "MuMuNxDevice";
}
