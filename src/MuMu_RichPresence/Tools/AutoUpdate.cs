using Dawn.MuMu.RichPresence.Logging;
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
        .WaitAndRetryAsync(MAX_RETRIES, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) - 1));
    internal static async Task<bool> Velopack()
    {
        try
        {
            VelopackApp.Build().Run(new VelopackUpdateLogger(Log.Logger));

            var manager =
                new UpdateManager(new GithubSource($"https://github.com/JustArion/{REPO_NAME}", null, false));

            if (manager.IsInstalled)
                Log.Information("The Velopack Update Manager is present");
            else
            {
                Log.Information("The Velopack Update Manager is not present. You will not receive auto-updates");
                return false;
            }

            var response = await _retryPolicy.ExecuteAndCaptureAsync(async () => await manager.CheckForUpdatesAsync());
            if (response.Outcome == OutcomeType.Failure)
            {
                Log.Error(response.FinalException, "Failed to check for updates");
                return false;
            }

            var version = response.Result;
            if (version == null)
                return false;

            await manager.DownloadUpdatesAsync(version);

            Log.Information("Updates are ready to be installed and will be applied on next restart ({Version})",
                version.TargetFullRelease.Version);
            // manager.ApplyUpdatesAndRestart(version);

            return true;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to update using Velopack");
            return false;
        }
    }
}
