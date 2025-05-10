using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ILogger = Serilog.ILogger;

namespace Dawn.MuMu.RichPresence.Logging;

[SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem")]
public class VelopackUpdateLogger(ILogger logger) : Microsoft.Extensions.Logging.ILogger
{
    public static VelopackUpdateLogger Create() => new(global::Serilog.Log.Logger);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        switch (logLevel)
        {
            case LogLevel.Debug:
                logger.Debug(exception, formatter(state, exception));
                break;
            case LogLevel.Warning:
                logger.Warning(exception, formatter(state, exception));
                break;
            case LogLevel.Error:
                logger.Error(exception, formatter(state, exception));
                break;
            case LogLevel.Critical:
                logger.Fatal(exception, formatter(state, exception));
                break;
            case LogLevel.None:
            case LogLevel.Trace:
            case LogLevel.Information:
            default:
                logger.Verbose(exception, formatter(state, exception));
                break;
        }
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
