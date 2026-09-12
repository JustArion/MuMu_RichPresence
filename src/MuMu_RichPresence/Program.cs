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

        InitializeVelopack();

        ApplicationLogs.Initialize();

        SingleInstanceApplication.Ensure();

        ApplicationLogs.ListenToEvents();

        if (Arguments.AutoUpdate)
            Task.Run(AutoUpdate.CheckForUpdates);

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
        app.Run();
    }

    private static void OnUninstall(SemanticVersion version) => Startup.RemoveStartup(Application.ProductName!);

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
