using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Serilog;

namespace Dawn.Serilog.CustomEnrichers;

public static class Warn
{
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
    public static IDisposable OnLongerThan(int durationMs, string? message = null, ILogger? logger = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = default)
    {
        logger ??= Log.Logger;
        var startTime = Stopwatch.StartNew();

        return new WarnDisposer(startTime, durationMs, logger, message, memberName, filePath, lineNumber);
    }

    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
    public static IDisposable OnLongerThan(TimeSpan ts, string? message = null, ILogger? logger = null, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = default)
        => OnLongerThan((int)ts.TotalMilliseconds, message, logger, memberName, filePath, lineNumber);

    private class WarnDisposer(
        Stopwatch StartTime,
        int DurationMs,
        ILogger Logger,
        string? Message = null,
        [CallerMemberName] string MemberName = "",
        [CallerFilePath] string FilePath = "",
        [CallerLineNumber] int LineNumber = default) : IDisposable
    {
        public void Dispose()
        {
            StartTime.Stop();
            var actualDuration = StartTime.ElapsedMilliseconds;
            if (actualDuration <= DurationMs)
                return;

            if (string.IsNullOrWhiteSpace(Message))
                Logger.Warning("Operation took longer than expected. Duration is {ActualDuration}ms, when expected sub {ExpectedDuration}ms, Caller Info: {CallerInfo}",
                    actualDuration,
                    DurationMs,
                    (MemberName, FilePath, LineNumber));
            else
                Logger.Warning("Operation took longer than expected. Duration is {ActuialDuration}ms, when expected sub {ExpectedDuration}ms | {Message}, Caller Info: {CallerInfo}",
                    actualDuration,
                    DurationMs,
                    Message,
                    (MemberName, FilePath, LineNumber));
        }
    }
}
