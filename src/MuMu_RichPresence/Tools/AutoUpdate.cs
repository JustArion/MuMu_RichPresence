using Dawn.MuMu.RichPresence.Logging;
using Polly;
using Polly.Retry;
using Velopack;
using Velopack.Sources;

namespace Dawn.MuMu.RichPresence.Tools;

internal static class AutoUpdate
{
    private const string REPO_LINK = "https://github.com/JustArion/MuMu_RichPresence";
    private const int MAX_RETRIES = 3;

    private static readonly AsyncRetryPolicy<UpdateInfo?> _retryPolicy = Policy<UpdateInfo?>
        .Handle<Exception>()
        .WaitAndRetryAsync(MAX_RETRIES,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) - 1)); // 4, 8, 16

    /// <summary>
    /// Checks for updates with a retry policy of retrying 3 times, with the time between each retry expanding exponentially
    /// </summary>
    /// <returns>
    /// If the standalone version of the app is used, this will return false<br/>
    /// If checking for updates fails, returns false<br/>
    /// If there's no update, returns false
    /// </returns>
    internal static async Task<bool> Check_WithVelopack()
    {
        try
        {
            var manager =
                new UpdateManager(new GithubSource(REPO_LINK, null, false));

            if (manager.IsInstalled)
                Log.Information("The Velopack Update Manager is present");
            else
            {
                Log.Information("The Velopack Update Manager is not present. You will not receive auto-updates");
                return false;
            }

            var response = await _retryPolicy.ExecuteAndCaptureAsync(manager.CheckForUpdatesAsync);
            if (response.Outcome == OutcomeType.Failure)
            {
                Log.Error(response.FinalException, "Failed to check for updates");
                return false;
            }

            var update = response.Result;
            if (update == null)
                return false;

            await manager.DownloadUpdatesAsync(update);

            Log.Information("Updates are ready to be installed and will be applied on next restart ({Version})",
                update.TargetFullRelease.Version);
            // manager.ApplyUpdatesAndRestart(update);

            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to update using Velopack");
            return false;
        }
    }
}
