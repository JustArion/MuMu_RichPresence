using Dawn.MuMu.RichPresence.Logging.Serilog.Enrichers;
using Serilog.Configuration;

namespace Dawn.MuMu.RichPresence.Logging.Serilog;

public static class SerilogExtensions
{
    public static LoggerConfiguration WithClassName(
        this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        if (enrichmentConfiguration == null) throw new ArgumentNullException(nameof(enrichmentConfiguration));
        return enrichmentConfiguration.With<ClassNameEnricher>();
    }
}