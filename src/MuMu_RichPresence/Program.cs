// #define DEBUG

using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using NuGet.Versioning;
using Velopack;

namespace Dawn.MuMu.RichPresence;

using Logging;
using Models;
using Tools;
using Tray;
using MuMu;

internal static class Program
{
    internal static DirectoryInfo CacheDirectory { get; private set; } = null!;

    internal static LaunchArgs Arguments { get; private set; }
    internal static ApplicationFeatures Features { get; } = new();

    private static RichPresence_Tray _trayIcon = null!;

    [STAThread]
    private static void Main(string[] args)
    {
        Environment.CurrentDirectory = AppContext.BaseDirectory; // Startup sets it to %windir%

        // This might throw an access violation if we don't have permissions to read it, we just don't read further when that happens
        SuppressExceptions(()=> DotNetEnv.Env
            .TraversePath()
            .Load());

        Arguments = new(args)
        {
            #if DEBUG
            ExtendedLogging = true
            #endif
        };

        InitializeApplication();

        CacheDirectory = new(Path.Combine(Environment.CurrentDirectory, "cache"));
        if (!CacheDirectory.Exists)
            CacheDirectory.Create();

        _trayIcon = new(MuMuNegotiator.LogSubject);
        Features.WhenPropertyChanged(x => x.RichPresenceEnabled)
            .Select(x => x.Value)
            .Subscribe(MuMuNegotiator.OnRichPresenceEnabledChanged);

        _disposables = MuMuNegotiator.UseApproach(Arguments.ExperimentalADB
            ? RichPresenceApproach.AndroidDebugBridge
            : RichPresenceApproach.LogFileWatcher);

        // This might trigger when the process is closed outside of something Application.Run can handle
        AppDomain.CurrentDomain.ProcessExit += (_, _) => EnsureDisposed();

        if (Arguments.HasProcessBinding)
            _disposables.Add(new ProcessBinding(Arguments.ProcessBinding));

        Application.Run();
        EnsureDisposed();
    }

    private static void InitializeApplication()
    {
        InitializeVelopack();
        InitializeLogs();

        SingleInstanceApplication.Ensure();

        ApplicationLogs.ListenToEvents();

        if (Arguments.AutoUpdate)
            Task.Run(AutoUpdate.CheckForUpdates);
    }

    private static bool _logsInitialized;
    private static void InitializeLogs()
    {
        if (_logsInitialized)
            return;

        var wd = new DirectoryInfo(Environment.CurrentDirectory);
        ApplicationLogs.Initialize(AutoUpdate.UpdateManager.Value is { IsInstalled: true, IsPortable: false }
            ? wd.Parent! // The setup version's persistent storage is in the parent directory (This would be %LocalAppData%/MuMu-RichPresence)
            : wd);
        _logsInitialized = true;
    }

    private static bool _disposed;
    private static CompositeDisposable? _disposables;
    private static void EnsureDisposed()
    {
        if (_disposables == null || _disposed)
            return;

        _disposed = true;

        _disposables.Dispose();
        _disposables = null;
    }

    private static void InitializeVelopack()
    {
        var app = VelopackApp.Build();
        app.OnBeforeUninstallFastCallback(OnUninstall);
        app.OnAfterUpdateFastCallback(OnUpdate);
        app.Run();
    }

    private static void OnUninstall(SemanticVersion version) => Startup.RemoveStartup(Application.ProductName!);
    private static void OnUpdate(SemanticVersion version)
    {
        InitializeLogs();
        Log.Information("Updated to {Version}!", version);
    }

    internal static void SuppressExceptions(Action act)
    {
        try
        {
            act();
        }
        catch
        {
            // ignored
        }
    }
}
