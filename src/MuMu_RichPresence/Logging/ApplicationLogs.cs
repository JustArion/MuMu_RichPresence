using System.Runtime.Versioning;

namespace Dawn.MuMu.RichPresence.Logging;

using global::Serilog;
using global::Serilog.Core;
using global::Serilog.Events;
using Serilog.CustomEnrichers;
using Serilog.Themes;

internal static class ApplicationLogs
{
    private const string LOGGING_FORMAT =
        "{Level:u1} {Timestamp:yyyy-MM-dd HH:mm:ss.ffffff}   [{Source}] {Message:lj}{NewLine}{Exception}";

    #if RELEASE
    private const string DEFAULT_SEQ_URL = "http://localhost:9999";
    #endif
    private static bool _initialized;

    [SupportedOSPlatform("windows")]
    private static void AttachParent()
    {
        if (AttachConsole(ATTACH_PARENT_PROCESS))
            Console.WriteLine("[*] Attached Console to Parent");
    }
    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;

        AttachParent();

        if (!Console.IsOutputRedirected)
        {
            var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            Console.SetOut(stdout);
        }

        try
        {
            var config = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.WithClassName()
                .Enrich.WithProcessName()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: LOGGING_FORMAT, theme: BlizzardTheme.GetTheme,
                    applyThemeToRedirectedOutput: true, standardErrorFromLevel: LogEventLevel.Error);

                if (!Arguments.NoFileLogging)
                    config.WriteTo.File(Path.Combine(AppContext.BaseDirectory, $"{Application.ProductName}.log"),
                    outputTemplate: LOGGING_FORMAT,
                    restrictedToMinimumLevel: Arguments.ExtendedLogging
                        ? LogEventLevel.Verbose
                        : LogEventLevel.Information,
                    buffered: true,
                    retainedFileCountLimit: 1,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: (long)Math.Pow(1024, 2) * 20, // 20mb
                    flushToDiskInterval: TimeSpan.FromSeconds(1));



            #if RELEASE
            // This is personal preference, but you can set your Seq server to catch :9999 too.
            // (Logs to nowhere if there's no Seq server listening on port 9999
            config.WriteTo.Seq(Arguments.HasCustomSeqUrl
                    ? Arguments.CustomSeqUrl
                    : DEFAULT_SEQ_URL,
                restrictedToMinimumLevel: LogEventLevel.Warning);
            #endif

            Log.Logger = config.CreateLogger();

            AppDomain.CurrentDomain.UnhandledException +=
                (_, eo) => Log.Fatal(eo.ExceptionObject as Exception, "Unhandled Exception");

            #if !DEBUG
            AppDomain.CurrentDomain.ProcessExit +=
                (_, _) => Log.Information("Shutting Down...");
            #endif

            Log.Information("Initialized on version {ApplicationVersion}", Application.ProductVersion);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            // Setting a null logger prevents exceptions when other places within the code tries to log when setting up the logger has failed already.
            Log.Logger = new NullLogger();
        }
    }
}
