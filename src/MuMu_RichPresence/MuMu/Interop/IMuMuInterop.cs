namespace Dawn.MuMu.RichPresence.MuMu.Interop;

public interface IMuMuInterop
{
    public Task<AppInfo> GetForegroundAppInfo(CancellationToken token = default);

    public Task<string> GetInfo(AppInfo info, string path, CancellationToken token = default);
}
