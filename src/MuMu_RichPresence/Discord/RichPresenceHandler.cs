// #define LISTEN_TO_RPCS

using DiscordRPC;
using DiscordRPC.Message;
using Serilog.Core;

namespace Dawn.MuMu.RichPresence.Discord;

public class RichPresenceHandler : IDisposable
{
    private const string DEFAULT_APPLICATION_ID = "1339586347576328293";
    private readonly string _sessionApplicationId;
    private string _currentApplicationId;

    private DiscordRpcClient? _client;
    public DiscordRPC.RichPresence? CurrentPresence { get; private set; }
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
        // Todo: Check if the cancel actually matters here, the below polling doesn't get triggered when rich presence is not running
        _presenceLifetimePollSource?.Cancel();
        _presenceLifetimePollSource = new();
        var token = _presenceLifetimePollSource.Token;
        // This continiously sets the presence to the current one.
        // It's relevant since the user can toggle showing rich presence and then it would just be gone until our app restarts or changes game.
        Task.Factory.StartNew(async _ =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

            while (await timer.WaitForNextTickAsync(token) && ApplicationFeatures.GetFeature(x => x.RichPresenceEnabled))
                _client?.SetPresence(CurrentPresence);
        }, TaskCreationOptions.LongRunning, token);
    }

    private void OnPresenceUpdate(object _, PresenceMessage args)
    {
        if (args.Presence == null)
            return;

        // We clear up some ghosting
        if (CurrentPresence != null)
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
            if (!ApplicationFeatures.GetFeature(x => x.RichPresenceEnabled) || CurrentPresence == presence)
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
                // We indicate with • that it's an official rich presence
                if (applicationId != _sessionApplicationId)
                    PrependOfficialGameTag(ref presenceName);

                Log.Information("Setting Rich Presence for {GameTitle}", presenceName);
            }

            CurrentPresence = presence;
            _client?.SetPresence(presence);

            EnsurePresenceLifetime();
            return true;
        }
    }

    public static void PrependOfficialGameTag(ref string str) => str = $"• {str}";

    public void ClearPresence()
    {
        lock (_sync)
        {
            CurrentPresence = null;

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
