using System.Collections.Concurrent;
using Dawn.MuMu.RichPresence.Models;
using Polly.Retry;

namespace Dawn.MuMu.RichPresence.Scrapers;

public static class PackageScraper
{
    private static readonly ConcurrentDictionary<string, StorePackageInfo?> _packageInfoCache = new();

    private static readonly IWebStoreScraper[] _scrapers =
    [
        new PlayStoreScraper(),
        new BiliGameScraper()
    ];

    public static async ValueTask<string> GetStoreLinkForSession(MuMuSessionLifetime session)
    {
        foreach (var scraper in _scrapers)
            if (await scraper.TryGetPackageInfo(session) is not null)
                return await scraper.GetStoreLinkForSession(session);

        return string.Empty;
    }

    // If all the scrapers fail once, we don't keep trying for future requests
    public static async ValueTask<StorePackageInfo?> TryGetPackageInfo(MuMuSessionLifetime session, AsyncRetryPolicy<StorePackageInfo?>? retryPolicy = null)
    {
        if (_packageInfoCache.TryGetValue(session.PackageName, out var info))
            return info;

        foreach (var scraper in _scrapers)
        {
            var value = await scraper.TryGetPackageInfo(session, retryPolicy);

            if (value == null)
                continue;

            _packageInfoCache.TryAdd(session.PackageName, value);
            return value;
        }

        _packageInfoCache.TryAdd(session.PackageName, null);
        return null;
    }
}
