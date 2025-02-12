namespace Dawn.MuMu.RichPresence.Domain;

public record MuMuSessionInfo(string PackageName, DateTimeOffset StartTime, string Title, AppSessionState AppState)
{
    public string RawText { get; init; } = "";
}
