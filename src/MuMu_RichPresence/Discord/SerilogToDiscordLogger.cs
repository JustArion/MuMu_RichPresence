using System.Diagnostics.CodeAnalysis;
using DiscordRPC.Logging;
using Serilog.Core;
#pragma warning disable CA2254

namespace Dawn.MuMu.RichPresence.Discord;

using ILogger = Serilog.ILogger;

[SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem")]
public class SerilogToDiscordLogger(ILogger logger) : DiscordRPC.Logging.ILogger
{
    public void Trace(string message, params object[] args)
    {
        if (Level <= LogLevel.Trace)
            logger.Debug(message, args);
    }

    public void Info(string message, params object[] args)
    {
        if (Level <= LogLevel.Info)
            logger.Verbose(message, args);
    }

    public void Warning(string message, params object[] args)
    {
        if (Level <= LogLevel.Warning)
            logger.Information(message, args);
    }

    public void Error(string message, params object[] args)
    {
        if (message.StartsWith("Failed connection to"))
            return;

        if (Level <= LogLevel.Error)
            logger.Warning(message, args);
    }

    public LogLevel Level { get; set; } = LogLevel.Error;

    public static explicit operator SerilogToDiscordLogger(Logger logger) => new(logger);
}
