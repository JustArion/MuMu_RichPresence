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
        if (TryGetFromProcess(out var logPath))
            return logPath.FullName;

        if (TryGetFromShortcut(out logPath))
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
        Log.Verbose("Trying to get path from process");
        logPath = null;
        try
        {
            var proc = Process.GetProcessesByName("MuMuPlayer").FirstOrDefault();
            if (proc == null)
                return false;

            var mumuPath = new FileInfo(proc.MainModule!.FileName);

            var mumuDirectory = mumuPath.Directory;
            if (mumuDirectory == null)
                return false;

            var vms = mumuDirectory.Parent!.GetDirectories("vms").First();

            // The base-vm is 'MuMuPlayerGlobal-12.0-0-base' this probably contains system files, but we need the folder with the 'logs' folder in, so we skip this.
            var vm = vms.EnumerateDirectories().First(x => !x.Name.Contains("base"));

            logPath = vm.GetDirectories("logs").First().GetFiles("shell.log").First();
            Log.Verbose("Found log path from process: {ProcessName}.exe ({ProcessId}) -> {MuMuPlayerPath} -> {Path}", proc.ProcessName, proc.Id, mumuPath.FullName, logPath.FullName);
            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to get path from process");
            return false;
        }
    }

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

            var vms = mumuPath.Directory!.Parent!.GetDirectories("vms").First();

            // The base-vm is 'MuMuPlayerGlobal-12.0-0-base' this probably contains system files, but we need the folder with the 'logs' folder in, so we skip this.
            var vm = vms.EnumerateDirectories().First(x => !x.Name.Contains("base"));

            logPath = vm.GetDirectories("logs").First().GetFiles("shell.log").First();
            Log.Verbose("Found log path from shortcut: {LinkPath} -> {MuMuPlayerPath} -> {Path}", playerLink.FullName, shortcutPath, logPath.FullName);
            return true;

        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to get path from shortcut");
            return false;
        }
    }

}
