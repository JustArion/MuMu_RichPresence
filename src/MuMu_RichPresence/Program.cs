using System.Diagnostics;
using Dawn.MuMu.RichPresence.Logging;
using Dawn.MuMu.RichPresence.PlayGames;

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
    private const string FILE_PATH = @"Google\Play Games\Logs\Service.log";
    private static readonly string _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FILE_PATH);

    [STAThread]
    private static void Main(string[] args)
    {
        Arguments = new(args);

        ApplicationLogs.Initialize();

        SingleInstanceApplication.Ensure();

        _richPresenceHandler = new();
        var reader = new PlayGamesAppSessionMessageReader(_filePath);

        _trayIcon = new(_filePath);
        _trayIcon.RichPresenceEnabledChanged += OnRichPresenceEnabledChanged;

        reader.OnSessionInfoReceived += SessionInfoReceived;
        reader.StartAsync();

        if (Arguments.HasProcessBinding)
            _processBinding = new ProcessBinding(Arguments.ProcessBinding);

        Application.Run();
        _richPresenceHandler.Dispose();
        _processBinding?.Dispose();
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

    private static void SessionInfoReceived(object? sender, MuMuSessionInfo sessionInfo) => Task.Run(() => SetPresenceFromSessionInfoAsync(sessionInfo));


    private static AppSessionState _currentAppState;
    private static async ValueTask SetPresenceFromSessionInfoAsync(MuMuSessionInfo sessionInfo)
    {
        if (_currentAppState == sessionInfo.AppState)
            return;

        if (Process.GetProcessesByName("MuMuPlayer").Length == 0)
        {
            Log.Debug("Emulator is not running, likely a log-artifact, crosvm.exe ({SessionTitle})", sessionInfo.Title);
            return;
        }

        // This is a bit of a loaded if statement. Let me break it down a bit
        // If the state went from Starting -> Started we don't do anything
        // If the state went from anything -> Starting / Started we subscribe to the app exit
        // This should prevent a double subscribe if weird app orders start appearing (Running -> Starting)
        if (_currentAppState is not (AppSessionState.Starting or AppSessionState.Focused) && sessionInfo.AppState is AppSessionState.Starting or AppSessionState.Focused)
        {
            _cts = new ();
            ProcessExit.Subscribe("MuMuPlayer", exitCode =>
            {
                Log.Information("[{ExitCode}] MuMuPlayer.exe has exited", exitCode);
                var previousAppState = _currentAppState;
                _currentAppState = AppSessionState.Stopped;
                Log.Information("App State Changed from {PreviousAppState} -> {CurrentAppState}", previousAppState, _currentAppState);
                ClearPresenceFor(sessionInfo);
            }, _cts.Token);
        }



        Log.Information("App State Changed from {PreviousAppState} -> {CurrentAppState} | {Timestamp}", _currentAppState, sessionInfo.AppState, sessionInfo.StartTime);
        _currentAppState = sessionInfo.AppState;

        switch (sessionInfo.AppState)
        {
            case AppSessionState.Starting:
                await SetPresenceFor(sessionInfo, new()
                {
                    Assets = new()
                    {
                        LargeImageText = "Starting up..."
                    }
                });
                break;
            case AppSessionState.Focused:
                await SetPresenceFor(sessionInfo, new()
                {
                    Timestamps = new Timestamps(sessionInfo.StartTime.DateTime)
                });
                break;
            case AppSessionState.Stopping or AppSessionState.Stopped:
                await _cts.CancelAsync();
                ClearPresenceFor(sessionInfo);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void ClearPresenceFor(MuMuSessionInfo sessionInfo)
    {
        Log.Information("Clearing Rich Presence for {GameTitle}", sessionInfo.Title);

        _richPresenceHandler.RemovePresence();
    }

    private static RichPresence? _currentPresence;
    private static async Task SetPresenceFor(MuMuSessionInfo sessionInfo, RichPresence presence)
    {
        var iconUrl = await PlayStoreAppIconScraper.TryGetIconLinkAsync(sessionInfo.PackageName);

        presence.Details ??= sessionInfo.Title;

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
