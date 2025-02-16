using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ILogger = Serilog.ILogger;

namespace Dawn.MuMu.RichPresence.Logging;

[SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem")]
public class VelopackUpdateLogger(ILogger logger) : Microsoft.Extensions.Logging.ILogger
{
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        switch (logLevel)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
                logger.Debug(exception, formatter(state, exception));
                break;
            case LogLevel.Information:
                logger.Information(exception, formatter(state, exception));
                break;
            case LogLevel.Warning:
                logger.Warning(exception, formatter(state, exception));
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                logger.Error(exception, formatter(state, exception));
                break;
            case LogLevel.None:
            default:
                logger.Verbose(exception, formatter(state, exception));
                break;
        }
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
