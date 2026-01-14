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
    private static RichPresenceHandler _richPresenceHandler = null!;
    private static DiscoverabilityHandler _discoverabilityHandler = null!;
    private static ProcessBinding? _processBinding;
    private static MuMuPlayerLogReader? _logReader;

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

    private static void SuppressExceptions(Action act)
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

    private static void ReaderSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            Log.Debug("[{ChangeType}] Session Updated {PossibleNewItems}", e.Action, e.NewItems);

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
        focusedApp ??= reader.GetFocusedApp();

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

    private static void OnRichPresenceEnabledChanged(object? sender, bool active)
    {
        if (active)
        {
            if (_richPresenceHandler.CurrentPresence is not { } presence)
                return;

            if (_focusedLifetime == null)
            {
                Log.Error("Trying to set a rich presence without an associated lifetime! Known details are: AppId: {AppId}, Details: {Details}", _currentApplicationId, presence.Details);
                return;
            }

            _richPresenceHandler.TrySetPresence(_focusedLifetime.Title, presence, _currentApplicationId);
            return;
        }

        ClearPresence(_focusedLifetime);
    }

    private static void RemovePresence()
    {
        // Basically an atomic version of
        // var lifetime = _focusedLifetime;
        // if (_focusedLifetime == null) return;
        // _focusedLifetime = null;
        if (Interlocked.Exchange(ref _focusedLifetime, null) is not { } lifetime)
            return;

        ClearPresence(lifetime);
    }

    private static void ClearPresence(MuMuSessionLifetime? lifetime)
    {
        var appName = _richPresenceHandler.CurrentPresence?.Details ?? lifetime?.Title;
        if (string.IsNullOrWhiteSpace(appName))
        {
            Log.Warning("Unable to find a name associated with the current presence! {LifetimeInfo}", lifetime);
            return;
        }

        var vt = _discoverabilityHandler.IsOfficialGame(appName);

        var isOfficialGame = vt.IsCompletedSuccessfully
            ? vt.Result
            : vt.AsTask().GetAwaiter().GetResult();

        if (isOfficialGame)
            RichPresenceHandler.PrependOfficialGameTag(ref appName);

        Log.Debug("Clearing Rich Presence for {AppName}", appName);
        _richPresenceHandler.ClearPresence();
    }

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
