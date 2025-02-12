namespace Dawn.MuMu.RichPresence.DiscordRichPresence;

using System.Diagnostics.CodeAnalysis;
using DiscordRPC.Logging;
using global::Serilog.Core;
using ILogger = ILogger;

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
