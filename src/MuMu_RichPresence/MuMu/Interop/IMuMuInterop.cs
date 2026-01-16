using Dawn.MuMu.RichPresence.Models;

namespace Dawn.MuMu.RichPresence.MuMu.Interop;

public interface IMuMuInterop : IAsyncDisposable
{
    public Task<AndroidProcess?> GetFocusedApp(CancellationToken token = default);

    public Task<AppInfo> GetForegroundAppInfo(CancellationToken token = default);

    public Task<string> GetInfo(AppInfo info, string path, CancellationToken token = default);
}
