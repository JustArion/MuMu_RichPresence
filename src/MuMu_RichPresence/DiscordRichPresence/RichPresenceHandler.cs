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
    private readonly CancellationTokenSource _disposingSource = new();

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

        Task.Factory.StartNew(async _ =>
        {
            var token = _disposingSource.Token;
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

            while (await timer.WaitForNextTickAsync(token) && ApplicationFeatures.GetFeature(x => x.RichPresenceEnabled))
                _client.SetPresence(_currentPresence);
        }, TaskCreationOptions.LongRunning);
    }

    public bool SetPresence(RichPresence? presence)
    {
        if (!ApplicationFeatures.GetFeature(x => x.RichPresenceEnabled))
        {
            _logger.Verbose("Rich Presence is disabled");
            return false;
        }

        if (presence != null)
            Log.Information("Setting Rich Presence for {GameTitle}", presence.Details);

        _currentPresence = presence;
        _client.SetPresence(presence);
        return true;
    }

    public void RemovePresence()
    {
        var presence = Interlocked.Exchange(ref _currentPresence, null);
        if (presence != null)
            Log.Information("Clearing Rich Presence for {PresenceTitle}", presence.Details);

        _client.ClearPresence();
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
        _disposingSource.Cancel();
        _client.ClearPresence();
        _client.Dispose();
        _client = null!;
        GC.SuppressFinalize(this);
    }
}
