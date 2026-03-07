using Dawn.MuMu.RichPresence.Models;

namespace Dawn.MuMu.RichPresence.MuMu.Interop;

public interface IMuMuInterop : IAsyncDisposable
{
    public Task<string> GetAppLabel(AppInfo info, CancellationToken token = default);
    public Task<AndroidProcess?> GetFocusedApp(CancellationToken token = default);

    public Task<AppInfo?> GetForegroundAppInfo(CancellationToken token = default);

    public Task<string> GetInfo(AppInfo info, string path, CancellationToken token = default);

    public Task<string> GetPackagePath(AppInfo info, CancellationToken token = default);

    public Task<FileInfo> PullFile(string path, DirectoryInfo directory, CancellationToken token = default);
    public Task PullFile(string path, FileInfo file, CancellationToken token = default);
}
