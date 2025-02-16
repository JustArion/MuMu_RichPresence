using Dawn.MuMu.RichPresence.Logging;
using Polly;
using Polly.Retry;
using Velopack;
using Velopack.Sources;

namespace Dawn.MuMu.RichPresence.Tools;

internal static class AutoUpdate
{
    private const int MAX_RETRIES = 3;
    private static readonly AsyncRetryPolicy<UpdateInfo?> _retryPolicy = Policy<UpdateInfo?>
        .Handle<Exception>()
        .WaitAndRetryAsync(MAX_RETRIES, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) - 1));
    internal static async Task Velopack()
    {
        VelopackApp.Build().Run(new SerilogToMicrosoftLogger(Log.Logger));

        var manager = new UpdateManager(new GithubSource("https://github.com/JustArion/PlayGames_RichPresence", null, false));

        if (manager.IsInstalled)
            Log.Information("The Velopack Update Manager is present");
        else
        {
            Log.Information("The Velopack Update Manager is not present");
            return;
        }

        var response = await _retryPolicy.ExecuteAndCaptureAsync(async () => await manager.CheckForUpdatesAsync());
        if (response.Outcome == OutcomeType.Failure)
        {
            Log.Error(response.FinalException, "Failed to check for updates");
            return;
        }

        var version = response.Result;
        if (version == null)
            return;

        await manager.DownloadUpdatesAsync(version);

        Log.Information("Updates are ready to be installed and will be applied on next restart ({Version})", version.TargetFullRelease.Version);
        // manager.ApplyUpdatesAndRestart(version);
    }
}
