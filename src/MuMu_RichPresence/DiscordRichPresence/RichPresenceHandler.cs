#define LISTEN_TO_RPCS
using DiscordRPC.Message;

namespace Dawn.MuMu.RichPresence.DiscordRichPresence;

using DiscordRPC;
using Serilog;
using Serilog.Core;

public class RichPresenceHandler : IDisposable
{
    private const string DEFAULT_APPLICATION_ID = "1339586347576328293";

    private readonly Logger _logger = (Logger)Log.ForContext<RichPresenceHandler>();
    private DiscordRpcClient _client;
    private RichPresence? _currentPresence;
    private CancellationTokenSource? _disposingSource;

    public RichPresenceHandler()
    {
        _logger.Debug("Initializing IPC Client");

        var applicationId = Arguments.HasCustomApplicationId
            ? Arguments.CustomApplicationId
            : DEFAULT_APPLICATION_ID;

        _client = new DiscordRpcClient(applicationId, logger: (SerilogToDiscordLogger)_logger);

        _client.SkipIdenticalPresence = false;
        _client.Initialize();
        #if LISTEN_TO_RPCS
        _client.OnRpcMessage += (_, msg) => Log.Debug("Received RPC Message: {@Message}", msg);
        #endif

        _client.OnPresenceUpdate += OnPresenceUpdate;
    }

    public bool SetPresence(RichPresence? presence)
    {
        if (!ApplicationFeatures.GetFeature(x => x.RichPresenceEnabled) || Interlocked.Exchange(ref _currentPresence, presence) == presence)
        {
            _logger.Verbose("Rich Presence is disabled");
            return false;
        }

        if (presence != null)
            Log.Information("Setting Rich Presence for {GameTitle}", presence.Details);

        _client.SetPresence(presence);

        _disposingSource = new();
        var token = _disposingSource.Token;
        // This continiously sets the presence to the current one.
        // It's relevant since the user can toggle showing rich presence and then it would just be gone until our app restarts or changes game.
        Task.Factory.StartNew(async _ =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

            while (await timer.WaitForNextTickAsync(token) && ApplicationFeatures.GetFeature(x => x.RichPresenceEnabled))
                _client.SetPresence(_currentPresence);
        }, TaskCreationOptions.LongRunning, token);
        return true;
    }

    public void RemovePresence()
    {
        var presence = Interlocked.Exchange(ref _currentPresence, null);
        if (presence != null)
            Log.Information("Clearing Rich Presence for {PresenceTitle}", presence.Details);

        _client.ClearPresence();
        _disposingSource?.Dispose();
        _disposingSource = null;
    }

    private void OnPresenceUpdate(object _, PresenceMessage args)
    {
        if (args.Presence == null)
            return;

        // We clear up some ghosting
        if (_currentPresence != null)
            return;

        _logger.Verbose("Attempting to correct some rich presence ghosting");
        _client.ClearPresence();
    }


    public void Dispose()
    {
        Log.Debug("Disposing IPC Client");
        _disposingSource?.Cancel();
        _disposingSource = null;
        _client.ClearPresence();
        _client.Dispose();
        _client = null!;
        GC.SuppressFinalize(this);
    }
}
