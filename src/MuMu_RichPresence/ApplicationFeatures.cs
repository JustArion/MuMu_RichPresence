namespace Dawn.MuMu.RichPresence;

using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using global::Serilog;

/// <summary>
/// Application features differs from LaunchArgs as LaunchArgs is immutable
/// </summary>
internal class ApplicationFeatures
{
    private ApplicationFeatures() { }
    private static readonly ApplicationFeatures Instance = new();

    public static void SyncFeature<T>(Expression<Func<ApplicationFeatures, T>> expression, T value)
    {
        if (expression.Body is not MemberExpression memberExpression)
            return;

        var propertyInfo = (System.Reflection.PropertyInfo)memberExpression.Member;

        var fieldName = ConvertToCamelCase(propertyInfo.Name);
        var field = typeof(ApplicationFeatures).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (field == null)
            throw new UnreachableException($"Developer mistake, this should not happen, PropertyName: {propertyInfo.Name}, Expected Field was not found: {fieldName}");

        field.SetValue(Instance, value);
        Log.Verbose("FeatureSync: {Property} = {Value}", propertyInfo.Name, value);
    }

    public static ApplicationFeatures GetAllFeatures() => Instance;
    public static T GetFeature<T>(Expression<Func<ApplicationFeatures, T>> expression)
    {
        if (expression.Body is not MemberExpression memberExpression)
            throw new ArgumentException("Invalid expression", nameof(expression));

        var propertyInfo = (System.Reflection.PropertyInfo)memberExpression.Member;

        return (T)propertyInfo.GetValue(Instance)!;
    }

    public static void SetFeature<T>(Expression<Func<ApplicationFeatures, T>> expression, T value)
    {
        if (expression.Body is not MemberExpression memberExpression)
            throw new ArgumentException("Invalid expression", nameof(expression));

        var propertyInfo = (System.Reflection.PropertyInfo)memberExpression.Member;

        propertyInfo.SetValue(Instance, value);
    }
    private static string ConvertToCamelCase(string propertyName) => $"_{char.ToLowerInvariant(propertyName[0])}{propertyName[1..]}";

    private static void SetAndRaiseFeatureChanged<T>(ref T featureBackingField, T newValue, [CallerMemberName] string callerName = "")
    {
        Log.Verbose("FeatureChange: [{CallerName}] {Previous} -> {Current}", callerName, featureBackingField, newValue);
        featureBackingField = newValue;
    }

    private bool _richPresenceEnabled;

    public bool RichPresenceEnabled
    {
        get => _richPresenceEnabled;
        set => SetAndRaiseFeatureChanged(ref _richPresenceEnabled, value);
    }
}
