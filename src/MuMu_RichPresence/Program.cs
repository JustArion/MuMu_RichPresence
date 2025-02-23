using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using DynamicData.Binding;

namespace Dawn.MuMu.RichPresence;

using DiscordRichPresence;
using DiscordRPC;
using Logging;
using Models;
using Tools;
using global::Serilog;
using Tray;

internal static class Program
{
    internal static LaunchArgs Arguments { get; private set; }

    private static RichPresence_Tray _trayIcon = null!;
    private static RichPresenceHandler _richPresenceHandler = null!;
    private static ProcessBinding? _processBinding;
    private static MuMuPlayerLogReader _logReader = null!;
    private static string _filePath = null!;

    [STAThread]
    private static async Task Main(string[] args)
    {
        Arguments = new(args);

        ApplicationLogs.Initialize(false);

        if (!Arguments.NoAutoUpdate)
        {
            var supportsVelopack = await AutoUpdate.Velopack();
            if (supportsVelopack)
                ApplicationLogs.Initialize(true);
        }
        ApplicationLogs.ListenToEvents();

        SingleInstanceApplication.Ensure();

        _richPresenceHandler = new();
        _trayIcon = new();
        _trayIcon.RichPresenceEnabledChanged += OnRichPresenceEnabledChanged;

        _filePath = await GetOrWaitForFilePath();

        _logReader = new MuMuPlayerLogReader(_filePath);
        _logReader.Sessions.CollectionChanged += ReaderSessionsChanged;
        _logReader.StartAsync();

        if (Arguments.HasProcessBinding)
            _processBinding = new ProcessBinding(Arguments.ProcessBinding);

        Application.Run();
        _richPresenceHandler.Dispose();
        _processBinding?.Dispose();
    }

    private static async Task<string> GetOrWaitForFilePath() => await Pathfinder.GetOrWaitForFilePath();

    private static void ReaderSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            Log.Debug("[{ChangeType}] Session Updated", e.Action);

            if (e.Action != NotifyCollectionChangedAction.Add)
                Task.Run(() => UpdatePresenceIfNecessary(_logReader));
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
                            Task.Run(() => UpdatePresenceIfNecessary(_logReader, lifetime));
                        else
                            Task.Run(() => UpdatePresenceIfNecessary(_logReader));

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

        if (Process.GetProcessesByName("MuMuPlayer").Length == 0)
        {
            Log.Debug("Emulator is not running, likely an old entry ({SessionTitle})", focusedApp.Title);
            RemovePresence();
            return;
        }

        if (await SetPresenceFor(focusedApp, new() { Timestamps = new Timestamps(focusedApp.StartTime.DateTime) }))
            Log.Debug("Presence updated for {SessionTitle}", focusedApp);
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
            if (_currentPresence != null)
                _richPresenceHandler.SetPresence(_currentPresence);
            return;
        }

        _richPresenceHandler.RemovePresence();
    }

    private static void RemovePresence()
    {
        if (Interlocked.Exchange(ref _focusedLifetime, null) == null)
            return;

        _richPresenceHandler.RemovePresence();
    }

    private static volatile RichPresence? _currentPresence;
    private static MuMuSessionLifetime? _focusedLifetime;
    private static CancellationTokenSource? _processSubscriptionCts = new();
    private static async Task<bool> SetPresenceFor(MuMuSessionLifetime sessionLifetime, RichPresence presence)
    {
        // A race condition is possible here, so we use Interlocked.Exchange
        if (Interlocked.Exchange(ref _focusedLifetime, sessionLifetime) == sessionLifetime)
            return false;

        var packageInfo = await PlayStoreWebScraper.TryGetPackageInfo(sessionLifetime.PackageName);

        var iconLink = packageInfo?.IconLink;

        presence.Details ??= sessionLifetime.Title;

        if (!string.IsNullOrWhiteSpace(iconLink))
        {
            if (presence.HasAssets())
            {
                var assets = presence.Assets;
                assets.LargeImageKey = iconLink;
                assets.LargeImageText = presence.Details;

            }
            else
                presence.Assets = new()
                {
                    LargeImageKey = iconLink,
                    LargeImageText = presence.Details
                };
        }

        var retVal = _richPresenceHandler.SetPresence(presence);
        if (!retVal)
            return retVal;

        _currentPresence = presence;
        await RemovePresenceOnMuMuPlayerExit();

        return retVal;
    }

    private static async Task RemovePresenceOnMuMuPlayerExit()
    {
        if (_processSubscriptionCts != null)
        {
            await _processSubscriptionCts.CancelAsync();
            _processSubscriptionCts.Dispose();
        }
        _processSubscriptionCts = new();

        ProcessExit.Subscribe("MuMuPlayer", _ => RemovePresence(), _processSubscriptionCts.Token);
    }
}
