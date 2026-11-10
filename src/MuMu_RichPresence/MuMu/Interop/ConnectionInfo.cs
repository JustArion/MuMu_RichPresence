namespace Dawn.MuMu.RichPresence.MuMu.Interop;

public record ConnectionInfo(
    string LocalIP,
    int LocalPort,
    string ADBPath,
    string WorkingDirectory,
    bool KeepAlive
)
{
    public string ConnectionString => $"{LocalIP}:{LocalPort}";

    public int LocalPort { get; set; } = LocalPort;
    public string LocalIP { get; set; } = LocalIP;
    public const int FALLBACK_PORT = 5555;
    public const string FALLBACK_IP = "localhost";
}
