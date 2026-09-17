using System.Reactive.Subjects;
using Polly;
using Polly.Retry;
using Velopack;
using Velopack.Sources;

namespace Dawn.MuMu.RichPresence.Tools;

internal static class AutoUpdate
{
    private const string REPO_NAME = "MuMu_RichPresence";

    private const int MAX_RETRIES = 3;
    private static readonly AsyncRetryPolicy<UpdateInfo?> _retryPolicy = Policy<UpdateInfo?>
        .Handle<Exception>()
        .WaitAndRetryAsync(MAX_RETRIES,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) - 1)); // 4, 8, 16

    private static readonly Lazy<UpdateManager> LazyPreReleaseManager = new(() => new(new GithubSource($"https://github.com/JustArion/{REPO_NAME}", null, true)));
    private static readonly Lazy<UpdateManager> LazyUpdateManager = new(() => new(new GithubSource($"https://github.com/JustArion/{REPO_NAME}", null, Arguments.CheckPreReleases)));

    private static readonly SemaphoreSlim UpdateSemaphore = new(1, 1);
    /// <summary>
    /// Checks for updates with a retry policy of retrying 3 times, with the time between each retry expanding exponentially
    /// </summary>
    /// <returns>
    /// If the standalone version of the app is used, this will return false<br/>
    /// If checking for updates fails, returns false<br/>
    /// If there's no update, returns false
    /// </returns>
    public static async Task CheckForUpdates()
    {
        await UpdateSemaphore.WaitAsync();
        try
        {
            var manager = UpdateManager;

            if (manager.IsInstalled)
                Log.Information("The Velopack Update Manager is present");
            else
            {
                Log.Information("The Velopack Update Manager is not present. You will not receive auto-updates");
                return;
            }

            var response = await _retryPolicy.ExecuteAndCaptureAsync(manager.CheckForUpdatesAsync);
            if (response.Outcome == OutcomeType.Failure)
            {
                Log.Error(response.FinalException, "Failed to check for updates");
                return;
            }

            if (response.Result is not { } update)
                return;

            HasPendingUpdate.OnNext(true);

            await manager.DownloadUpdatesAsync(update);

            Log.Information("Updates are ready to be installed and will be applied on next restart ({Version})", update.TargetFullRelease.Version);
            // manager.ApplyUpdatesAndRestart(update);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to update using Velopack");
        }
        finally
        {
            UpdateSemaphore.Release();
        }
    }

    public static readonly BehaviorSubject<bool> HasPendingUpdate = new(false);

    public static UpdateManager UpdateManager => Features.CheckPreReleases
        ? LazyPreReleaseManager.Value
        : LazyUpdateManager.Value;
}
