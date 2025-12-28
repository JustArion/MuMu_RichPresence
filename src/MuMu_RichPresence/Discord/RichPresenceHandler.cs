// #define LISTEN_TO_RPCS

using DiscordRPC;
using DiscordRPC.Message;
using Serilog.Core;

namespace Dawn.MuMu.RichPresence.Discord;

public class RichPresenceHandler
{
    private const string DEFAULT_APPLICATION_ID = "1339586347576328293";
    private readonly string _sessionApplicationId;
    private string _currentApplicationId;

    private DiscordRpcClient? _client;
    private DiscordRPC.RichPresence? _currentPresence;
    private CancellationTokenSource? _presenceLifetimePollSource;
    private readonly Lock _sync = new();

    public RichPresenceHandler()
    {
        Log.Debug("Initializing IPC Client");

        _currentApplicationId = _sessionApplicationId = Arguments.HasCustomApplicationId
            ? Arguments.CustomApplicationId
            : DEFAULT_APPLICATION_ID;

        InitializeClient(_sessionApplicationId);
    }

    private void InitializeClient(string applicationId)
    {
        lock (_sync) // We can lock here since .NET allows re-entrant locks (double locking on the same thread)
        {
            if (_client != null)
                DisposeClient();

            _client = new DiscordRpcClient(applicationId, logger: (SerilogToDiscordLogger)(Logger)Log.Logger);
            _currentApplicationId = applicationId;

            _client.SkipIdenticalPresence = false;
            _client.Initialize();
            #if LISTEN_TO_RPCS
            _client.OnRpcMessage += (_, msg) => Log.Debug("Received RPC Message: {@Message}", msg);
            #endif

            _client.OnPresenceUpdate += OnPresenceUpdate;
        }
    }

    private void EnsurePresenceLifetime()
    {
        _presenceLifetimePollSource = new();
        var token = _presenceLifetimePollSource.Token;
        // This continiously sets the presence to the current one.
        // It's relevant since the user can toggle showing rich presence and then it would just be gone until our app restarts or changes game.
        Task.Factory.StartNew(async _ =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

            while (await timer.WaitForNextTickAsync(token) && ApplicationFeatures.GetFeature(x => x.RichPresenceEnabled))
                _client?.SetPresence(_currentPresence);
        }, TaskCreationOptions.LongRunning, token);
    }

    private void OnPresenceUpdate(object _, PresenceMessage args)
    {
        if (args.Presence == null)
            return;

        // We clear up some ghosting
        if (_currentPresence != null)
            return;

        Log.Verbose("Attempting to correct some rich presence ghosting");
        _client?.ClearPresence();
    }


    private void DisposeClient()
    {
        _client?.ClearPresence();
        _client?.Dispose();
        _client = null!;
    }

    public void Dispose()
    {
        Log.Debug("Disposing IPC Client");
        _presenceLifetimePollSource?.Cancel();
        _presenceLifetimePollSource = null;
        DisposeClient();
        GC.SuppressFinalize(this);
    }

    public bool TrySetPresence(string presenceName, DiscordRPC.RichPresence? presence, string? applicationId = null)
    {
        lock (_sync)
        {
            if (!ApplicationFeatures.GetFeature(x => x.RichPresenceEnabled) || _currentPresence == presence)
            {
                Log.Verbose("Rich Presence is disabled");
                return false;
            }

            applicationId ??= _sessionApplicationId;
            // We don't need to reinitialize a client if we're just clearing the presence
            if (presence != null && applicationId != _currentApplicationId)
                InitializeClient(applicationId);

            if (presence != null)
            {
                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                if (applicationId == _sessionApplicationId)
                    Log.Information("Setting Rich Presence for {GameTitle}", presenceName);
                else // We indicate with • that it's an official rich presence
                    Log.Information("Setting Rich Presence for • {GameTitle}", presenceName);
            }

            _currentPresence = presence;
            _client?.SetPresence(presence);

            EnsurePresenceLifetime();
            return true;
        }
    }

    public void ClearPresence(string? presenceName = null)
    {
        lock (_sync)
        {
            if (_currentPresence != null)
            {
                Log.Debug("Clearing Rich Presence for {AppName}", _currentPresence.Details ?? presenceName);
                _currentPresence = null;
            }

            _client?.ClearPresence();
            // We don't need to reset the application id since we're not disposing the client, we're just clearing the presence

            if (_presenceLifetimePollSource == null)
                return;

            _presenceLifetimePollSource.Cancel();
            _presenceLifetimePollSource.Dispose();
            _presenceLifetimePollSource = null;
        }
    }
}
