using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Dawn.MuMu.RichPresence.Models;
using Polly;
using Polly.Retry;

namespace Dawn.MuMu.RichPresence.Scrapers;

public partial class PlayStoreScraper : IWebStoreScraper
{
    private static readonly HttpClient _client = new();
    private static readonly ConcurrentDictionary<string, StorePackageInfo> _webCache = new();

    private static readonly AsyncRetryPolicy<StorePackageInfo?> _retryPolicy = Policy<StorePackageInfo?>
        .Handle<Exception>()
        .WaitAndRetryAsync(MAX_RETRIES, _ => TimeSpan.FromSeconds(1));

    private const int MAX_RETRIES = 3;

    public ValueTask<string> GetStoreLinkForSession(MuMuSessionLifetime session) => ValueTask.FromResult($"https://play.google.com/store/apps/details?id={session.PackageName}");

    public async ValueTask<StorePackageInfo?> TryGetPackageInfo(MuMuSessionLifetime session, AsyncRetryPolicy<StorePackageInfo?>? retryPolicy = null)
    {
        var packageName = session.PackageName;

        if (_webCache.TryGetValue(packageName, out var link))
            return link;

        retryPolicy ??= _retryPolicy;

        try
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                var storePageContent = await _client.GetStringAsync(await GetStoreLinkForSession(session));

                var match = GetImageRegex().Match(storePageContent);

                if (!match.Success)
                {
                    Log.Warning("Failed to find icon link for {PackageName}", packageName);
                    return null;
                }

                var imageLink = match.Groups[1].Value;
                var titleMatch = GetTitleRegex().Match(storePageContent);
                var title = titleMatch.Success ? titleMatch.Groups[1].Value : string.Empty;

                var info = new StorePackageInfo(imageLink, title);
                _webCache.TryAdd(packageName, info);

                return info;
            });
        }
        catch (HttpRequestException e)
        {
            #if DEBUG
            Log.Debug(e, "Failed to get icon link for {Title}", session.Title);
            #endif
            return null;
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to get icon link for {Title}", session.Title);
            return null;
        }
    }

    [GeneratedRegex("<meta property=\"og:image\" content=\"(.+?)\">")]
    private static partial Regex GetImageRegex();

    [GeneratedRegex("<meta property=\"og:title\" content=\"(.+?) - Apps on Google Play\">")]
    private static partial Regex GetTitleRegex();
}
