using System.Diagnostics.CodeAnalysis;
using DiscordRPC.Logging;
using Velopack.Logging;
using ILogger = Serilog.ILogger;
#pragma warning disable CA2254

namespace Dawn.MuMu.RichPresence.Logging;

[SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem")]
public class VelopackUpdateLogger(ILogger logger) : IVelopackLogger
{
    public static VelopackUpdateLogger Create() => new(global::Serilog.Log.Logger);

    public void Log(VelopackLogLevel logLevel, string? message, Exception? exception)
    {
        message ??= string.Empty;
        switch (logLevel)
        {
            case VelopackLogLevel.Debug:
                logger.Debug(exception, message);
                break;
            case VelopackLogLevel.Warning:
                logger.Warning(exception, message);
                break;
            case VelopackLogLevel.Error:
                logger.Error(exception, message);
                break;
            case VelopackLogLevel.Critical:
                logger.Fatal(exception, message);
                break;
            case VelopackLogLevel.Trace:
            case VelopackLogLevel.Information:
            default:
                throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
        }
    }
}
