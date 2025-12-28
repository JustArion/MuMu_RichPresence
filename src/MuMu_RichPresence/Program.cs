// #define DEBUG
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using Dawn.MuMu.RichPresence.Discord;
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
    private static RichPresenceHandler _richPresenceHandler = null!;
    private static DiscoverabilityHandler _discoverabilityHandler = null!;
    private static ProcessBinding? _processBinding;
    private static MuMuPlayerLogReader? _logReader;

    [STAThread]
    private static void Main(string[] args)
    {
        Environment.CurrentDirectory = AppContext.BaseDirectory; // Startup sets it to %windir%
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


        _richPresenceHandler = new();
        _trayIcon = new();
        _trayIcon.RichPresenceEnabledChanged += OnRichPresenceEnabledChanged;
        _discoverabilityHandler = new();

        // The below code is within a Task block since 'GetOrWaitForFilePath' can take an unknown amount of time to complete
        Task.Run(async () =>
        {
            var filePath = await Pathfinder.GetOrWaitForFilePath();

            _logReader = new MuMuPlayerLogReader(filePath, _currentProcessState);
            _logReader.Sessions.CollectionChanged += ReaderSessionsChanged;
            _logReader.StartAsync(); // This starts a long running operation, the method doesn't need to be awaited

            if (Arguments.HasProcessBinding)
                _processBinding = new ProcessBinding(Arguments.ProcessBinding);
        });

        Application.Run();
        _richPresenceHandler.Dispose();
        _processBinding?.Dispose();
    }

    private static void InitializeVelopack()
    {
        var app = VelopackApp.Build();
        app.OnBeforeUninstallFastCallback(OnUninstall);
        app.Run();
    }

    private static void OnUninstall(SemanticVersion version) => Startup.RemoveStartup(Application.ProductName!);

    private static void ReaderSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            Log.Debug("[{ChangeType}] Session Updated", e.Action);

            var logReader = _logReader!;
            if (e.Action != NotifyCollectionChangedAction.Add)
                Task.Run(() => UpdatePresenceIfNecessary(logReader));
            else
            {
                if (e.NewItems == null)
                    return;

                foreach (var lifetime in e.NewItems.Cast<MuMuSessionLifetime>())
                {
                    if (AppLifetimeParser.IsSystemLevelPackage(lifetime.PackageName))
                        continue;

                    lifetime.AppState.WhenPropertyChanged(entry => entry.Value).Subscribe(a =>
                    {

                        if (a.Value == AppState.Focused)
                            Task.Run(() => UpdatePresenceIfNecessary(logReader, lifetime));
                        else
                            Task.Run(() => UpdatePresenceIfNecessary(logReader));

                        Log.Debug("Updating from AppState Change {NewState}", a.Value);
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while handling session collection change");
        }
    }

    private static async Task UpdatePresenceIfNecessary(MuMuPlayerLogReader reader, MuMuSessionLifetime? focusedApp = null)
    {
        focusedApp ??= GetFocusedApp(reader.Sessions);

        if (focusedApp == null)
        {
            Log.Debug("No focused app found, clearing presence");
            RemovePresence();
            return;
        }

        foreach (var emulatorProcessName in Pathfinder.EmulatorProcessNames)
        {
            var currentMuMuProcess = Process.GetProcessesByName(emulatorProcessName);
            if (currentMuMuProcess.Length == 0)
                continue;

            _currentProcessState.CurrentEmulatorProcess = currentMuMuProcess.First();

            if (await SetPresenceFor(focusedApp, new()
                {
                    Timestamps = new(focusedApp.StartTime.DateTime),
                }, emulatorProcessName))
                Log.Debug("Presence updated for {SessionTitle}", focusedApp);
            return;
        }

        Log.Debug("Emulator is not running, likely an old entry ({SessionTitle})", focusedApp.Title);
        RemovePresence();
    }

    private static MuMuSessionLifetime? GetFocusedApp(ObservableCollection<MuMuSessionLifetime> sessions)
    {
        foreach (var session in sessions)
        {
            if (AppLifetimeParser.IsSystemLevelPackage(session.PackageName))
                continue;

            if (session.AppState.Value is AppState.Focused or AppState.Started)
                return session;
        }

        return null;
    }

    private static void OnRichPresenceEnabledChanged(object? sender, bool active)
    {
        if (active)
        {
            if (_currentPresence is not { } presence)
                return;

            if (_focusedLifetime == null)
            {
                Log.Error("Trying to set a rich presence without an associated lifetime! Known details are: AppId: {AppId}, Details: {Details}", _currentApplicationId, presence.Details);
                return;
            }

            _richPresenceHandler.TrySetPresence(_focusedLifetime.Title, presence, _currentApplicationId);
            return;
        }

        _richPresenceHandler.ClearPresence(_focusedLifetime?.Title);
    }

    private static void RemovePresence()
    {
        var title = _focusedLifetime?.Title;
        if (Interlocked.Exchange(ref _focusedLifetime, null) == null)
            return;

        _richPresenceHandler.ClearPresence(title);
    }

    private static volatile RichPresence? _currentPresence;
    private static string? _currentApplicationId;
    private static MuMuSessionLifetime? _focusedLifetime;
    private static CancellationTokenSource? _processSubscriptionCts = new();
    private static async Task<bool> SetPresenceFor(MuMuSessionLifetime sessionLifetime, RichPresence presence, string emulatorProcessName)
    {
        // A race condition is possible here, so we use Interlocked.Exchange
        if (Interlocked.Exchange(ref _focusedLifetime, sessionLifetime) == sessionLifetime)
            return false;

        var discoverabilityTask = _discoverabilityHandler.TryGetOfficialApplicationId(sessionLifetime.Title);
        var packageInfoTask = PlayStoreWebScraper.TryGetPackageInfo(sessionLifetime.PackageName);

        if (await discoverabilityTask is not { } officialApplicationId)
        {
            // There's no official application id associated with this presence (or the discoverability handler isn't initialized yet
            // This can be due to multiple reasons
            // - https://discord.com/api/v9/games/detectable is down / blocks your traffic
            // - Our program started recently while the user is playing a game and the api hit hasn't completed yet
            officialApplicationId = null;
            presence.Details ??= sessionLifetime.Title;
            presence.WithStatusDisplay(StatusDisplayType.Details);
            presence.WithDetailsUrl(PlayStoreWebScraper.GetPlayStoreLinkForPackage(sessionLifetime.PackageName));
        }

        var packageInfo = await packageInfoTask;
        var iconLink = packageInfo?.IconLink;

        if (!string.IsNullOrWhiteSpace(iconLink))
            PopulatePresenceAssets(sessionLifetime, presence, iconLink, officialApplicationId == null);

        var retVal = _richPresenceHandler.TrySetPresence(sessionLifetime.Title, presence, officialApplicationId);
        if (!retVal)
            return retVal;

        _currentApplicationId = officialApplicationId;
        _currentPresence = presence;
        await RemovePresenceOnMuMuPlayerExit(emulatorProcessName);

        return retVal;
    }

    private static void PopulatePresenceAssets(MuMuSessionLifetime sessionLifetime, RichPresence presence, string iconLink, bool linkToStorePage)
    {
        if (!presence.HasAssets())
            presence.Assets = new();

        var assets = presence.Assets;
        assets.LargeImageKey = iconLink;
        assets.LargeImageText = presence.Details;

        if (linkToStorePage)
            assets.LargeImageUrl = PlayStoreWebScraper.GetPlayStoreLinkForPackage(sessionLifetime.PackageName);
    }

    private static async Task RemovePresenceOnMuMuPlayerExit(string emulatorProcessName)
    {
        if (_processSubscriptionCts != null)
        {
            await _processSubscriptionCts.CancelAsync();
            _processSubscriptionCts.Dispose();
        }
        _processSubscriptionCts = new();

        ProcessExit.Subscribe(emulatorProcessName, _ =>
        {
            RemovePresence();
            _currentProcessState.CurrentEmulatorProcess = null;
        }, _processSubscriptionCts.Token);
    }

    private static readonly MuMuProcessState _currentProcessState = new();
}
