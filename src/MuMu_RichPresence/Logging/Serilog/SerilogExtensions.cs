namespace Dawn.Serilog.CustomEnrichers;

using global::Serilog;
using global::Serilog.Configuration;

public static class SerilogExtensions
{
    public static LoggerConfiguration WithClassName(
        this LoggerEnrichmentConfiguration enrichmentConfiguration)
    {
        if (enrichmentConfiguration == null) throw new ArgumentNullException(nameof(enrichmentConfiguration));
        return enrichmentConfiguration.With<ClassNameEnricher>();
    }
}