// #define DEBUG
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dawn.MuMu.RichPresence.Discord;
using Dawn.MuMu.RichPresence.MuMu.Interop;
using DynamicData.Binding;
using NuGet.Versioning;
using Velopack;

namespace Dawn.MuMu.RichPresence;

using DiscordRPC;
using Logging;
using Models;
using Tools;
using Serilog;
using Tray;
using MuMu;

internal static class Program
{
    internal static LaunchArgs Arguments { get; private set; }

    private static RichPresence_Tray _trayIcon = null!;
    private static ProcessBinding? _processBinding;

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

        if (!Arguments.NoAutoUpdate)
            Task.Run(AutoUpdate.CheckForUpdates);

        _trayIcon = new();
        _trayIcon.RichPresenceEnabledChanged += MuMuNegotiator.OnRichPresenceEnabledChanged;

        var disposables = MuMuNegotiator.UseApproach(Arguments.ExperimentalADB
            ? RichPresenceApproach.AndroidDebugBridge
            : RichPresenceApproach.LogFileWatcher);

        if (Arguments.HasProcessBinding)
            _processBinding = new ProcessBinding(Arguments.ProcessBinding);

        Application.Run();
        _processBinding?.Dispose();
        disposables.Dispose();
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
