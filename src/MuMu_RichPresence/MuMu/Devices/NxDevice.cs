using System.Diagnostics;
using System.Text.Json;
using Dawn.MuMu.RichPresence.MuMu.Interop;
using Dawn.MuMu.RichPresence.Tools;

namespace Dawn.MuMu.RichPresence.MuMu.Devices;

// C:\Program Files\Netease\MuMuPlayer\nx_device\15.0\shell\MuMuNxDevice.exe
// C:\Program Files\Netease\MuMuPlayer\nx_device\12.0\shell\MuMuNxDevice.exe
public record NxDevice(Process UnderlyingProcess, DirectoryInfo DeviceRoot, FileInfo File, Version DeviceVersion) : IMuMuDevice
{
    public bool KeepAlive { get; init; }
    public bool IsActive() => KeepAlive || !UnderlyingProcess.HasExited;

    public ProcessBinding CreateProcessBinding(Action<int> onExit) => new(UnderlyingProcess.Id, onExit);

    // C:\Program Files\Netease\MuMuPlayer\vms\MuMuPlayerGlobal-15.0-0\
    // C:\Program Files\Netease\MuMuPlayer\vms\MuMuPlayerGlobal-12.0-1\
    public DirectoryInfo? FindVM() => Pathfinder.GetVMs()
        .FirstOrDefault(x => x.Name
            .Contains(DeviceVersion.ToString()));

    // C:\Program Files\Netease\MuMuPlayer\vms\MuMuPlayerGlobal-15.0-0\configs\vm_config.json
    // C:\Program Files\Netease\MuMuPlayer\vms\MuMuPlayerGlobal-12.0-1\configs\vm_config.json
    public async ValueTask<VMConfig?> GetVMConfig()
    {
        var vm = FindVM();

        var configFile = vm?.GetFiles("vm_config.json", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (configFile is not { Exists: true})
            return null;

        await using var file = configFile.OpenRead();

        var config = await JsonSerializer.DeserializeAsync<VMConfig>(file);

        return config;
    }

    public FileInfo? FindLogFile()
    {
        var vm = FindVM();
        if (vm == null)
            return null;

        // C:\Program Files\Netease\MuMuPlayer\vms\MuMuPlayerGlobal-15.0-0\logs\shell.log
        var file = new FileInfo(Path.Combine(vm.FullName, "logs", "shell.log"));

        return file.Exists
            ? file
            : null;
    }

    public FileInfo? FindADB()
    {
        // C:\Program Files\Netease\MuMuPlayer\nx_device\15.0\shell\adb.exe
        var file = new FileInfo(Path.Combine(File.DirectoryName!, "adb.exe"));

        return file.Exists
            ? file
            : null;
    }


    internal static NxDevice CreateFromProcess(Process proc)
    {
        // C:\Program Files\Netease\MuMuPlayer\nx_device\15.0\shell\MuMuNxDevice.exe
        // C:\Program Files\Netease\MuMuPlayer\nx_device\12.0\shell\MuMuNxDevice.exe
        var file = new FileInfo(proc.MainModule!.FileName);

        var deviceRoot = file.Directory;
        var deviceVersion = default(Version);
        while (deviceRoot != null)
        {
            if (Version.TryParse(deviceRoot.Name, out deviceVersion))
                break;
            deviceRoot = deviceRoot.Parent;
        }

        return deviceVersion == null
            ? throw new DirectoryNotFoundException(nameof(file.FullName))
            : new(proc, deviceRoot!, file, deviceVersion);
    }
}
