namespace Dawn.MuMu.RichPresence.MuMu.Interop;

public readonly record struct AppInfo(
    string PackageName,
    int Pid,
    DateTimeOffset StartTime);
