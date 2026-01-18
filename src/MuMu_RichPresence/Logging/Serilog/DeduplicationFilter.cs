using Serilog.Core;
using Serilog.Events;

namespace Dawn.MuMu.RichPresence.Logging.Serilog;

public class DeduplicationFilter(TimeSpan window) : ILogEventFilter
{
    private string? _lastMessage;
    private DateTime _lastLogged = DateTime.MinValue;

    public bool IsEnabled(LogEvent logEvent)
    {
        var msg = logEvent.RenderMessage();
        var now = DateTime.UtcNow;
        if (msg == _lastMessage && now - _lastLogged < window)
            return false;
        _lastMessage = msg;
        _lastLogged = now;
        return true;
    }
}
