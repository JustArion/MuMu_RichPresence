using System.Reactive.Disposables;
using Dawn.MuMu.RichPresence.Discord;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.MuMu.Devices;
using Dawn.MuMu.RichPresence.Scrapers;
using Dawn.MuMu.RichPresence.Tools;
using DiscordRPC;

namespace Dawn.MuMu.RichPresence.MuMu;

// A manager on which implemetation to use, ADB or file logs
public static partial class MuMuNegotiator
{
    private static RichPresenceApproach? _currentApproach;
    private static RichPresenceHandler? _richPresenceHandler;
    private static DiscoverabilityHandler _discoverabilityHandler = null!;
    private static readonly CompositeDisposable _disposables = new();
    private static readonly DeviceWatcher _deviceWatcher = new();


    public static CompositeDisposable UseApproach(RichPresenceApproach approach)
    {
        if (_currentApproach != null)
            throw  new InvalidOperationException("Cannot use an approach more than once");

        _currentApproach = approach;
        _discoverabilityHandler = new(CacheDirectory);
        _disposables.Add(_richPresenceHandler = new());

        // Clear presence when we switch devices
        // The ADB approach already clears the presence before the emulator exits via
        // StartADB -> DispatchFocusedApp -> UpdatePresenceIfNecessary -> RemovePresence
        // In a graceful exit. But the device watcher should catch and clean up ungraceful exits (MuMu Player crashes)
        // Same should in theory happen for the File Watching approach
        _deviceWatcher.CurrentDevice
            .Subscribe(_ => RemovePresence());

        Log.Information("Using {Approach} approach", approach);
        if (approach == RichPresenceApproach.LogFileWatcher)
            StartFileWatching();
        else
            StartADB();

        return _disposables;
    }

    private static async Task CoercePresenceToFocusedLifetime(MuMuSessionLifetime lifetime, string emulatorName)
    {
        if (await SetPresenceFor(lifetime, new() { Timestamps = new(lifetime.StartTime.DateTime) }, emulatorName, true))
            Log.Debug("Presence coerced for {SessionTitle}", lifetime);
    }

    // User toggled by the tray whether to enable or disable dispatching presences to Discord
    internal static void OnRichPresenceEnabledChanged(bool presenceActive)
    {
        if (_richPresenceHandler == null)
            return;

        var lifetime = _focusedLifetime;

        if (!presenceActive)
        {
            ClearPresence(lifetime);
            return;
        }

        if (!_deviceWatcher.CurrentDevice.TryGetValue(out var device) || device == null)
            return; // There's no NxDevice / Emulator running

        // There was no prior presence, but there is a lifetime
        if (_richPresenceHandler.CurrentPresence is not { } presence)
        {
            if (lifetime != null)
                Task.Run(async () => await CoercePresenceToFocusedLifetime(lifetime, _focusedEmulatorName!));
        }
        else if (lifetime == null)
            Log.Error("Trying to set a rich presence without an associated lifetime! Known details are: AppId: {AppId}, Details: {Details}", _currentApplicationId, presence.Details);
        else
            _richPresenceHandler.TrySetPresence(lifetime.Title, presence, _currentApplicationId);
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
        if (_richPresenceHandler == null)
            return;

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

        Log.Debug("Trying to update focused app: {App}", focusedApp.Title);

        if (_deviceWatcher.CurrentDevice.TryGetValue(out var device) && device != null && device.IsActive())
        {
            if (await SetPresenceFor(focusedApp, new() { Timestamps = new(focusedApp.StartTime.DateTime) }, device.UnderlyingProcess.ProcessName))
                Log.Debug("Presence updated for {SessionTitle}", focusedApp);
            return;
        }

        Log.Debug("Emulator is not running, likely an old entry ({SessionTitle})", focusedApp.Title);
        RemovePresence();
    }

    private static string? _currentApplicationId;
    private static MuMuSessionLifetime? _focusedLifetime;
    private static string? _focusedEmulatorName;
    private static async Task<bool> SetPresenceFor(MuMuSessionLifetime sessionLifetime, DiscordRPC.RichPresence presence, string emulatorProcessName, bool isForced = false)
    {
        if (_richPresenceHandler == null)
            return false;

        // A race condition is possible here, so we use Interlocked.Exchange
        if (!isForced && Interlocked.Exchange(ref _focusedLifetime, sessionLifetime) == sessionLifetime)
            return false;
        _focusedEmulatorName = emulatorProcessName;

        var discoverabilityTask = _discoverabilityHandler.TryGetOfficialApplicationId(sessionLifetime.Title);
        var packageInfoTask = PackageScraper.TryGetPackageInfo(sessionLifetime);

        if (await discoverabilityTask is not { } officialApplicationId)
        {
            // There's no official application id associated with this presence (or the discoverability handler isn't initialized yet
            // This can be due to multiple reasons
            // - https://discord.com/api/v9/games/detectable is down / blocks your traffic
            // - Our program started recently while the user is playing a game and the api hit hasn't completed yet
            officialApplicationId = null;
            presence.Details ??= sessionLifetime.Title;
            presence.WithStatusDisplay(StatusDisplayType.Details);
            presence.WithDetailsUrl(await PackageScraper.GetStoreLinkForSession(sessionLifetime));
        }

        var packageInfo = await packageInfoTask;
        var iconLink = packageInfo?.IconLink;

        if (!string.IsNullOrWhiteSpace(iconLink))
            await PopulatePresenceAssets(sessionLifetime, presence, iconLink, officialApplicationId == null);

        var retVal = _richPresenceHandler.TrySetPresence(sessionLifetime.Title, presence, officialApplicationId);
        if (!retVal)
            return retVal;

        _currentApplicationId = officialApplicationId;
        return retVal;
    }

    private static async Task PopulatePresenceAssets(MuMuSessionLifetime sessionLifetime, DiscordRPC.RichPresence presence, string iconLink, bool linkToStorePage)
    {
        if (!presence.HasAssets())
            presence.Assets = new();

        var assets = presence.Assets;
        assets.LargeImageKey = iconLink;
        assets.LargeImageText = presence.Details;

        if (linkToStorePage)
            assets.LargeImageUrl = await PackageScraper.GetStoreLinkForSession(sessionLifetime);
    }
}
