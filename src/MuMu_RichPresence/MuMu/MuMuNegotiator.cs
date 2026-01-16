using System.Collections.Specialized;
using System.Diagnostics;
using System.Reactive.Disposables;
using Dawn.MuMu.RichPresence.Discord;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.Tools;
using DiscordRPC;
using DynamicData.Binding;

namespace Dawn.MuMu.RichPresence.MuMu;

// A manager on which implemetation to use, ADB or file logs
public static partial class MuMuNegotiator
{
    private static RichPresenceApproach? _currentApproach;
    private static CancellationTokenSource? _processSubscriptionCts = new();
    private static RichPresenceHandler _richPresenceHandler = null!;
    private static MuMuPlayerLogReader? _logReader;
    private static DiscoverabilityHandler _discoverabilityHandler = null!;
    private static readonly CompositeDisposable _disposables = new();


    public static CompositeDisposable UseApproach(RichPresenceApproach approach)
    {
        if (_currentApproach != null)
            throw  new InvalidOperationException("Cannot use an approach more than once");

        _currentApproach = approach;
        _discoverabilityHandler = new();
        _disposables.Add(_richPresenceHandler = new());

        if (approach == RichPresenceApproach.LogFileWatcher)
        {
            Log.Debug("Using File Watching approach");
            StartFileWatching();
        }
        else
        {
            Log.Debug("Using ADB approach");
            StartADB();
        }

        return _disposables;
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

    internal static void OnRichPresenceEnabledChanged(object? sender, bool active)
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

    internal static void RemovePresence()
    {
        // Basically an atomic version of
        // var lifetime = _focusedLifetime;
        // if (_focusedLifetime == null) return;
        // _focusedLifetime = null;
        if (Interlocked.Exchange(ref _focusedLifetime, null) is not { } lifetime)
            return;

        ClearPresence(lifetime);
    }

    internal static void ClearPresence(MuMuSessionLifetime? lifetime)
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

    private static async Task UpdatePresenceIfNecessary(MuMuSessionLifetime? focusedApp = null)
    {
        if (focusedApp == null)
        {
            RemovePresence();
            return;
        }

        foreach (var processName in Pathfinder.EmulatorProcessNames)
        {
            var currentMuMuProcess = Process.GetProcessesByName(processName).FirstOrDefault();
            if (currentMuMuProcess == null)
                continue;

            _currentProcessState.CurrentEmulatorProcess = currentMuMuProcess;

            if (await SetPresenceFor(focusedApp, new()
                {
                    Timestamps = new(focusedApp.StartTime.DateTime),
                }, processName))
                Log.Debug("Presence updated for {SessionTitle}", focusedApp);
            return;
        }

        Log.Debug("Emulator is not running, likely an old entry ({SessionTitle})", focusedApp.Title);
        RemovePresence();
    }

    private static string? _currentApplicationId;
    private static MuMuSessionLifetime? _focusedLifetime;
    private static async Task<bool> SetPresenceFor(MuMuSessionLifetime sessionLifetime, DiscordRPC.RichPresence presence, string emulatorProcessName)
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

    private static void PopulatePresenceAssets(MuMuSessionLifetime sessionLifetime, DiscordRPC.RichPresence presence, string iconLink, bool linkToStorePage)
    {
        if (!presence.HasAssets())
            presence.Assets = new();

        var assets = presence.Assets;
        assets.LargeImageKey = iconLink;
        assets.LargeImageText = presence.Details;

        if (linkToStorePage)
            assets.LargeImageUrl = PlayStoreWebScraper.GetPlayStoreLinkForPackage(sessionLifetime.PackageName);
    }

    private static readonly MuMuProcessState _currentProcessState = new();
}
