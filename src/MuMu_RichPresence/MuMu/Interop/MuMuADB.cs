using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dawn.MuMu.RichPresence.Tools;

namespace Dawn.MuMu.RichPresence.MuMu.Interop;

public static class MuMuADB
{
    public static async Task<(string Title, AppInfo AppInfo)?> TryFindGame()
    {
        foreach (var emulator in Pathfinder.EmulatorProcessNames)
        {
            var currentMuMuProcess = Process.GetProcessesByName(emulator);
            if (currentMuMuProcess.Length == 0)
                continue;

            var proc = currentMuMuProcess.First();

            var emulatorFile = new FileInfo(proc.MainModule!.FileName);

            // ..\MuMuPlayerGlobal-12.0\nx_device\12.0\shell
            var emuDir = emulatorFile.Directory!;

            var adbPath = emuDir.GetFiles("adb.exe").FirstOrDefault();
            // vms\MuMuPlayerGlobal-12.0-0\configs\vm_config.json
            var configFile = new FileInfo(Path.Combine(emuDir.FullName, "../../../vms/MuMuPlayerGlobal-12.0-0/configs/vm_config.json"));

            if (!configFile.Exists || adbPath is not { Exists: true })
                continue;

            await using var file = configFile.OpenRead();

            var elem = JsonSerializer.Deserialize<JsonObject>(file);
            if (elem == null)
                continue;

            var adb = elem["vm"]?["nat"]?["port_forward"]?["adb"];
            var guestIP = adb?["guest_ip"];
            var hostPort = adb?["host_port"];
            if (guestIP is null || hostPort is null)
                continue;

            var ip = guestIP.GetValue<string>();
            var port = hostPort.GetValue<string>();

            var connectionInfo = new ConnectionInfo(ip, int.Parse(port), adbPath.FullName);
            var interop = new MuMuInterop(connectionInfo);

            var foregroundInfo = await interop.GetForegroundAppInfo();

            if (AppLifetimeParser.IsSystemLevelPackage(foregroundInfo.PackageName))
                return null;

            var info = await PlayStoreWebScraper.TryGetPackageInfo(foregroundInfo.PackageName);
            if (info is null)
                return null;

            return (info.Title, foregroundInfo);
        }

        return null;
    }
}
