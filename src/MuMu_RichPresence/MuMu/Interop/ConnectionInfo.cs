namespace Dawn.MuMu.RichPresence.MuMu.Interop;

public readonly record struct ConnectionInfo(
    string LocalIP,
    int LocalPort,
    string ADBPath,
    string WorkingDirectory,
    bool KeepAlive
)
{
    public const int FALLBACK_PORT = 5555;
}
