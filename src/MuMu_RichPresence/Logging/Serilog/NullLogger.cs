using Serilog.Events;

namespace Dawn.MuMu.RichPresence.Logging.Serilog;

public sealed class NullLogger : ILogger
{
    public void Write(LogEvent logEvent) { }
}
