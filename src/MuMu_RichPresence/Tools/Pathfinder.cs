using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using Shortcut = ShellLink.Shortcut;

namespace Dawn.MuMu.RichPresence.Tools;

internal static class Pathfinder
{
    [SuppressMessage("ReSharper", "RemoveRedundantBraces")]
    public static async ValueTask<string> GetOrWaitForFilePath()
    {
        if (TryGetFromProcess(out var logPath)
            || TryGetFromShortcut(out logPath))
            return logPath.FullName;

        Log.Warning("Unable to find MuMu Player path, waiting for the player to start instead");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync())
        {
            if (TryGetFromProcess(out logPath))
                return logPath.FullName;
        }

        throw new UnreachableException("Failed to get path from process");
    }

    internal static bool TryGetFromProcess([NotNullWhen(true)] out FileInfo? logPath)
    {
        logPath = null;
        try
        {
            foreach (var emulatorName in EmulatorProcessNames)
            {
                if (PathFromPlayer(emulatorName) is { } validPath)
                {
                    logPath = validPath;
                    return true;
                }
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


    internal static readonly string[] EmulatorProcessNames =
        [
            "MuMuPlayer",
            "MuMuNxMain", // \MuMuPlayerGlobal-12.0\nx_main\MuMuNxMain.exe
        ];


    [SupportedOSPlatform("windows")]
    internal static bool TryGetFromShortcut(out FileInfo logPath)
    {
        Log.Verbose("Trying to get path from shortcut");
        logPath = null!;

        try
        {
            var dir = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.Programs));

            var playerLink = dir.GetDirectories("MuMu*").FirstOrDefault()
                ?.GetFiles("MuMu Player*").FirstOrDefault();

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

    private static FileInfo GetLogPathFromBaseDirectory(DirectoryInfo mumuDirectory)
    {
        var vms = mumuDirectory.Parent!.GetDirectories("vms").First();

        // The base-vm is 'MuMuPlayerGlobal-12.0-0-base' this probably contains system files, but we need the folder with the 'logs' folder in, so we skip this.
        var vm = vms.EnumerateDirectories().First(x => !x.Name.Contains("base"));

        return vm.GetDirectories("logs").First().GetFiles("shell.log").First();
    }
}
