using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Dawn.MuMu.RichPresence.Logging.Serilog.Enrichers;

public class ClassNameEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var st = new StackTrace();
        var frame = st.GetFrames().FirstOrDefault(x =>
        {
            var type = x.GetMethod()?.ReflectedType;
            if (type == null || type == typeof(ClassNameEnricher))
                return false;

            return !type.FullName!.StartsWith("Serilog.");
        });
        var type = frame?.GetMethod()?.ReflectedType;
        if (type == null)
            return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Source", GetClassName(type)));
    }

    private static string? GetClassName(Type type, bool includeNamespace = false)
    {
        var last = type.FullName!.Split('.').LastOrDefault();

        var className = last?.Split('+').FirstOrDefault()?.Replace("`1", string.Empty).Replace('_', '-');

        if (includeNamespace)
            return type.Namespace != null
            ? $"{type.Namespace}.{className}"
            : className;

        return className;
    }

    private const string BLUE_ANSI = "\e[38;2;59;120;255m";

}
