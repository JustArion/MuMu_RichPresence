using System.Net.Http.Json;
using System.Text.Json;
using Dawn.MuMu.RichPresence.Models;
using Polly;
using Polly.Retry;

namespace Dawn.MuMu.RichPresence.Discord;

internal class DiscoverabilityHandler
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNamingPolicy =  JsonNamingPolicy.CamelCase };

    private const int MAX_RETRIES = 3;
    private static readonly AsyncRetryPolicy<DiscoverableRichPresence[]?> _retryPolicy = Policy<DiscoverableRichPresence[]?>
        .Handle<Exception>()
        .WaitAndRetryAsync(MAX_RETRIES,
            _ => TimeSpan.FromSeconds(1));

    internal DiscoverableRichPresence[]? _discoverablePresences = [];
    private readonly Task _initializationTask;
    public DiscoverabilityHandler() =>
        _initializationTask = Task.Run(async () =>
        {
            try
            {
                using var client = new HttpClient();

                _discoverablePresences = await _retryPolicy.ExecuteAsync(async ()
                    => await client.GetFromJsonAsync<DiscoverableRichPresence[]?>("https://discord.com/api/v9/games/detectable", _options));

                var count = _discoverablePresences?.Length ?? 0;

                Log.Verbose("Got {DiscoverableRichPresenceCount} Official Rich Presence Ids", count);
            }
            catch (HttpRequestException) { }
            catch (Exception e) when (e is not HttpRequestException)
            {
                Log.Error(e, "Failed to get Official Rich Presence Ids");
            }
        });

    public async Task<string?> TryGetOfficialApplicationId(string appName)
    {
        await _initializationTask;

        var appId = _discoverablePresences?.FirstOrDefault(x => x.Name == appName)?.Id;

        if (appId != null)
            Log.Verbose("Found an Official Presence for Application {ApplicationName} ({ApplicationId})", appName, appId);

        return appId;
    }
}
