#nullable enable
namespace Dawn.Serilog.CustomEnrichers;

using System.Diagnostics;
using global::Serilog.Core;
using global::Serilog.Events;

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

    private string? GetClassName(Type type)
    {
        var last = type.FullName!.Split('.').LastOrDefault();

        var className = last?.Split('+').FirstOrDefault()?.Replace("`1", string.Empty).Replace('_', '-');
        return type.Namespace != null 
            ? $"{type.Namespace}.{className}" 
            : className;
    }

    private const string BLUE_ANSI = "\u001b[38;2;59;120;255m";

}