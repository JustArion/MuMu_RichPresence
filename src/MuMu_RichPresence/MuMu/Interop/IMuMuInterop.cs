namespace Dawn.MuMu.RichPresence.MuMu.Interop;

public interface IMuMuInterop
{
    public Task<AppInfo> GetForegroundAppInfo();

    public Task<string> GetInfo(AppInfo info, string path);
}
