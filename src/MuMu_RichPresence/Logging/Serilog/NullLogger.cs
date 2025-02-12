using Serilog.Events;

namespace Dawn.Serilog.CustomEnrichers;

public sealed class NullLogger : ILogger
{
    public void Write(LogEvent logEvent) { }
}
