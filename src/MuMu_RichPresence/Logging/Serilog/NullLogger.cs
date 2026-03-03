using Serilog.Events;

namespace Dawn.MuMu.RichPresence.Logging.Serilog;

public sealed class NullLogger : ILogger
{
    public static ILogger Instance { get; } = new NullLogger();

    public void Write(LogEvent logEvent) { }
}
