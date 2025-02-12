using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using Dawn.MuMu.RichPresence.Logging;
using Dawn.MuMu.RichPresence.PlayGames;
using DynamicData.Binding;

namespace Dawn.MuMu.RichPresence;

using DiscordRichPresence;
using DiscordRPC;
using Domain;
using global::Serilog;
using Tray;

internal static class Program
{
    internal static LaunchArgs Arguments { get; private set; }

    private static RichPresence_Tray _trayIcon = null!;
    private static RichPresenceHandler _richPresenceHandler = null!;
    private static ProcessBinding? _processBinding;
    private static MuMuPlayerLogReader _logReader;
    private const string FILE_PATH = @"shell.log";
    private static readonly string _filePath = Path.Combine("D:\\Games\\MuMuPlayerGlobal-12.0\\vms\\MuMuPlayerGlobal-12.0-0\\logs", FILE_PATH);

    [STAThread]
    private static void Main(string[] args)
    {
        Arguments = new(args);

        ApplicationLogs.Initialize();

        SingleInstanceApplication.Ensure();

        _richPresenceHandler = new();
        _logReader = new MuMuPlayerLogReader(_filePath);

        _trayIcon = new(_filePath);
        _trayIcon.RichPresenceEnabledChanged += OnRichPresenceEnabledChanged;

        _logReader.Sessions.CollectionChanged += ReaderSessionsChanged;

        _logReader.StartAsync();

        if (Arguments.HasProcessBinding)
            _processBinding = new ProcessBinding(Arguments.ProcessBinding);

        Application.Run();
        _richPresenceHandler.Dispose();
        _processBinding?.Dispose();
    }

    private static void ReaderSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            if (e.Action != NotifyCollectionChangedAction.Add)
                Task.Run(() => UpdatePresenceIfNecessary(_logReader));
            else
            {
                if (e.NewItems == null)
                    return;

                foreach (var lifetime in e.NewItems.Cast<MuMuSessionLifetime>())
                {
                    if (MuMuLifetimeChecker.IsSystemLevelPackage(lifetime.PackageName))
                        continue;

                    lifetime.AppState.WhenPropertyChanged(entry => entry.Value).Subscribe(_ =>
                    {
                        Task.Run(() => UpdatePresenceIfNecessary(_logReader));
                        Log.Verbose("Updating from AppState Subscription");
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while handling session collection change");
        }
    }

    private static async Task UpdatePresenceIfNecessary(MuMuPlayerLogReader reader)
    {
        var focusedApp = GetFocusedApp(reader.Sessions);

        if (focusedApp == null)
        {
            ClearPresenceFor(reader.SessionGraveyard.Last());
            Log.Debug("No focused app found, clearing presence");
            return;
        }

        await SetPresenceFor(focusedApp, new()
        {
            Timestamps = new Timestamps(focusedApp.StartTime.DateTime)
        });
        Log.Information("Presence updated for {SessionTitle}", focusedApp);
    }

    private static MuMuSessionLifetime? GetFocusedApp(ObservableCollection<MuMuSessionLifetime> sessions)
    {
        foreach (var session in sessions)
        {
            if (MuMuLifetimeChecker.IsSystemLevelPackage(session.PackageName))
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
            if (_currentPresence != null)
                _richPresenceHandler.SetPresence(_currentPresence);
            return;
        }

        _richPresenceHandler.RemovePresence();
    }

    private static CancellationTokenSource _cts = new();

    private static void SessionInfoReceived(object? sender, MuMuSessionLifetime sessionLifetime) => Task.Run(() => SetPresenceFromSessionInfoAsync(sessionLifetime));


    private static AppState _currentAppState;
    private static async ValueTask SetPresenceFromSessionInfoAsync(MuMuSessionLifetime sessionLifetime)
    {
        if (_currentAppState == sessionLifetime.AppState)
            return;

        if (Process.GetProcessesByName("MuMuPlayer").Length == 0)
        {
            Log.Debug("Emulator is not running, likely a log-artifact, crosvm.exe ({SessionTitle})", sessionLifetime.Title);
            return;
        }

        // This is a bit of a loaded if statement. Let me break it down a bit
        // If the state went from Starting -> Started we don't do anything
        // If the state went from anything -> Starting / Started we subscribe to the app exit
        // This should prevent a double subscribe if weird app orders start appearing (Running -> Starting)
        if (_currentAppState is not (AppState.Started or AppState.Focused) && sessionLifetime.AppState.Value is AppState.Started or AppState.Focused)
        {
            _cts = new ();
            ProcessExit.Subscribe("MuMuPlayer", exitCode =>
            {
                Log.Information("[{ExitCode}] MuMuPlayer.exe has exited", exitCode);
                var previousAppState = _currentAppState;
                _currentAppState = AppState.Stopped;
                Log.Information("App State Changed from {PreviousAppState} -> {CurrentAppState}", previousAppState, _currentAppState);
                ClearPresenceFor(sessionLifetime);
            }, _cts.Token);
        }



        Log.Information("App State Changed from {PreviousAppState} -> {CurrentAppState} | {Timestamp}", _currentAppState, sessionLifetime.AppState.Value, sessionLifetime.StartTime);
        _currentAppState = sessionLifetime.AppState;

        switch (sessionLifetime.AppState.Value)
        {
            case AppState.Started:
                await SetPresenceFor(sessionLifetime, new()
                {
                    Assets = new()
                    {
                        LargeImageText = "Starting up..."
                    }
                });
                break;
            case AppState.Focused:
                await SetPresenceFor(sessionLifetime, new()
                {
                    Timestamps = new Timestamps(sessionLifetime.StartTime.DateTime)
                });
                break;
            case AppState.Stopping or AppState.Stopped or AppState.Unfocused:
                await _cts.CancelAsync();
                ClearPresenceFor(sessionLifetime);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void ClearPresenceFor(MuMuSessionLifetime sessionLifetime)
    {
        Log.Information("Clearing Rich Presence for {GameTitle}", sessionLifetime.Title);

        _richPresenceHandler.RemovePresence();
    }

    private static RichPresence? _currentPresence;
    private static async Task SetPresenceFor(MuMuSessionLifetime sessionLifetime, RichPresence presence)
    {
        var iconUrl = await PlayStoreAppIconScraper.TryGetIconLinkAsync(sessionLifetime.PackageName);

        presence.Details ??= sessionLifetime.Title;

        if (!string.IsNullOrWhiteSpace(iconUrl))
        {
            if (presence.HasAssets())
                presence.Assets!.LargeImageKey = iconUrl;
            else
                presence.Assets = new()
                {
                    LargeImageKey = iconUrl
                };
        }

        _richPresenceHandler.SetPresence(presence);
        _currentPresence = presence;
    }
}
