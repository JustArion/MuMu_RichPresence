using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Dawn.MuMu.RichPresence.Models;
using Polly;
using Polly.Retry;

namespace Dawn.MuMu.RichPresence.Discord;

public class DiscoverabilityHandler
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNamingPolicy =  JsonNamingPolicy.CamelCase };

    private const int MAX_RETRIES = 3;
    private static readonly AsyncRetryPolicy<DiscoverableRichPresence[]?> _retryPolicy = Policy<DiscoverableRichPresence[]?>
        .Handle<Exception>()
        .WaitAndRetryAsync(MAX_RETRIES,
            _ => TimeSpan.FromSeconds(1));

    private readonly SemaphoreSlim _sync = new(1, 1);
    private bool _aquiredInitialLock;

    internal DiscoverableRichPresence[]? _discoverablePresences = [];
    public DiscoverabilityHandler() => Task.Run(PopulateDiscoverablePresences);

    private async Task PopulateDiscoverablePresences()
    {
        await _sync.WaitAsync();
        _aquiredInitialLock = true;

        try
        {
            try
            {
                if (File.Exists("detectable.json"))
                {
                    Log.Debug("Found detectable.json");

                    _discoverablePresences =
                        JsonSerializer.Deserialize<DiscoverableRichPresence[]>(
                            await File.ReadAllTextAsync("detectable.json"), _options);

                    var count = _discoverablePresences?.Length ?? 0;
                    Log.Debug("Read from disk, found {OfficialPresenceCount} Official Presences", count);

                    _ = Task.Run(GetDiscoverablePresencesFromRemote).ContinueWith(async task =>
                    {
                        if (!task.IsCompletedSuccessfully || _discoverablePresences == null || task.Result == null)
                            return;

                        await _sync.WaitAsync();
                        try
                        {
                            var remotePresences = task.Result;
                            var localLength = _discoverablePresences.Length;
                            var remoteLength = remotePresences.Length;

                            if (localLength == remoteLength)
                                return;

                            Log.Debug("Difference detected in Official Presence Count. ({LocalLength}/{RemoteLength}) Merging Local and Remote!", localLength, remoteLength);

                            // We're merging local and remote (While filtering out duplicates)
                            // This allows us to possibly hold a larger collection of presences than remote
                            // This is good for if remote reduces the presence count from their api (for some reason), which has happened before
                            // And is the reason for all these checks
                            _discoverablePresences = _discoverablePresences.Concat(remotePresences)
                                .DistinctBy(x => x.Id)
                                .ToArray();

                            _ = Task.Run(SavePresencesToDisk);
                        }
                        finally
                        {
                            _sync.Release();
                        }
                    });

                    return;
                }
            }
            catch (JsonException) { }

            _discoverablePresences = await GetDiscoverablePresencesFromRemote();
            _ = Task.Run(SavePresencesToDisk);
        }
        finally
        {
            _sync.Release();
        }
    }

    // This is a bit more safer than directly writing to detectable.json
    // Since the app can exit / crash / the PC powers off during a JSON write, causing corruption
    private async Task SavePresencesToDisk()
    {
        try
        {
            await File.WriteAllTextAsync("detectable.temp.json", JsonSerializer.Serialize(_discoverablePresences, _options));

            File.Move("detectable.temp.json", "detectable.json", true);
        }
        finally
        {
            if (File.Exists("detectable.temp.json"))
                File.Delete("detectable.temp.json");
        }

        Log.Verbose("Saved Official Presences -> detectable.json");
    }

    private static async Task<DiscoverableRichPresence[]?> GetDiscoverablePresencesFromRemote()
    {
        try
        {
            using var client = new HttpClient();

            var retVal = await _retryPolicy.ExecuteAsync(async ()
                => await client.GetFromJsonAsync<DiscoverableRichPresence[]?>(
                    "https://discord.com/api/v9/games/detectable", _options));

            return retVal;
        }
        catch (HttpRequestException) { }
        catch (Exception e) when (e is not HttpRequestException)
        {
            Log.Error(e, "Failed to get Official Rich Presence Ids");
        }

        return null;
    }

    public async ValueTask<bool> IsOfficialGame(string appName) => !string.IsNullOrWhiteSpace(await TryGetOfficialApplicationId(appName));

    public async ValueTask<string?> TryGetOfficialApplicationId(string appName)
    {
        // There's a possibility that within the time it takes to create a task this method (TryGetOfficialApplicationId) can get called
        // Resulting in the semaphore getting passed to this method first, instead of PopulateDiscoverablePresences.
        // This would cause _discoverablePresences to be null for the first invocation
        if (!_aquiredInitialLock)
            await Task.Delay(TimeSpan.FromSeconds(1));

        if (!await _sync.WaitAsync(TimeSpan.FromSeconds(2)))
        {
            Log.Verbose("Abandoning wait, taking too long to get an Official App Id");
            return null;
        }

        try
        {
            var appId = _discoverablePresences?.FirstOrDefault(x => x.Name == appName)?.Id;

            if (appId != null)
                Log.Verbose("Found an Official Presence for Application {ApplicationName} ({ApplicationId})", appName, appId);
            else
                Log.Debug("Unable to find an AppId for {ApplicationName}", appName);

            return appId;
        }
        finally
        {
            _sync.Release();
        }
    }
}
