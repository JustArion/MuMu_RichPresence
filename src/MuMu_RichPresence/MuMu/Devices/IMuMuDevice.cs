using Dawn.MuMu.RichPresence.MuMu.Interop;

namespace Dawn.MuMu.RichPresence.MuMu.Devices;

public interface IMuMuDevice
{
    public FileInfo File { get; }
    public DirectoryInfo? FindVM();
    public FileInfo? FindLogFile();
    public FileInfo? FindADB();
    public ValueTask<VMConfig?> GetVMConfig();
}
