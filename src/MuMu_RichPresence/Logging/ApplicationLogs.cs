using System.Runtime.Versioning;

namespace Dawn.MuMu.RichPresence.Logging;

using global::Serilog;
using global::Serilog.Events;
using Serilog.CustomEnrichers;
using Serilog.Themes;

internal static class ApplicationLogs
{
    static ApplicationLogs()
    {
        AttachParent();

        if (Console.IsOutputRedirected)
            return;

        var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        Console.SetOut(stdout);
    }

    private const string LOGGING_FORMAT =
        "{Level:u1} {Timestamp:yyyy-MM-dd HH:mm:ss.ffffff}   [{Source}] {Message:lj}{NewLine}{Exception}";

    #if RELEASE
    private const string DEFAULT_SEQ_URL = "http://localhost:9999";
    #endif

    [SupportedOSPlatform("windows")]
    private static void AttachParent()
    {
        if (AttachConsole(ATTACH_PARENT_PROCESS))
            Console.WriteLine("[*] Attached Console to Parent");
    }
    public static void Initialize(bool writeToParent)
    {
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
                config.WriteTo.File(
                    writeToParent
                        ? Path.Combine(AppContext.BaseDirectory, $"{Application.ProductName}.log")
                        : Path.Combine(Directory.GetParent(".")!.FullName, $"{Application.ProductName}.log"),
                    outputTemplate: LOGGING_FORMAT,
                    restrictedToMinimumLevel: Arguments.ExtendedLogging
                        ? LogEventLevel.Verbose
                        : LogEventLevel.Information,
                    buffered: true,
                    retainedFileCountLimit: 1,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: (long)Math.Pow(1024, 2) * 20, flushToDiskInterval // 20mb
                    : TimeSpan.FromSeconds(1));

            #if RELEASE
            // This is personal preference, but you can set your Seq server to catch :9999 too.
            // (Logs to nowhere if there's no Seq server listening on port 9999
            config.WriteTo.Seq(Arguments.HasCustomSeqUrl
                    ? Arguments.CustomSeqUrl
                    : DEFAULT_SEQ_URL,
                restrictedToMinimumLevel: LogEventLevel.Information);
            #endif

            Log.Logger = config.CreateLogger();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            // Setting a null logger prevents exceptions when other places within the code tries to log when setting up the logger has failed already.
            Log.Logger = new NullLogger();
        }
    }

    internal static void ListenToEvents()
    {
        AppDomain.CurrentDomain.UnhandledException +=
            (_, eo) => Log.Fatal(eo.ExceptionObject as Exception, "Unhandled Exception");

            #if !DEBUG
            AppDomain.CurrentDomain.ProcessExit +=
                (_, _) => Log.Information("Shutting Down...");
            #endif

        Log.Information("Initialized on version {ApplicationVersion}", Application.ProductVersion);
    }
}
