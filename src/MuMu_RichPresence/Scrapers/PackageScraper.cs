using Dawn.MuMu.RichPresence.Models;
using Polly.Retry;

namespace Dawn.MuMu.RichPresence.Scrapers;

public static class PackageScraper
{
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

    public static async ValueTask<StorePackageInfo?> TryGetPackageInfo(MuMuSessionLifetime session, AsyncRetryPolicy<StorePackageInfo?>? retryPolicy = null)
    {
        foreach (var scraper in _scrapers)
        {
            var value = await scraper.TryGetPackageInfo(session, retryPolicy);

            if (value != null)
                return value;
        }

        return null;
    }
}
