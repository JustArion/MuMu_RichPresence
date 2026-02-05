using Dawn.MuMu.RichPresence.Models;
using Polly.Retry;

namespace Dawn.MuMu.RichPresence.Scrapers;

public interface IWebStoreScraper
{
    public ValueTask<string> GetStoreLinkForSession(MuMuSessionLifetime session);
    public ValueTask<StorePackageInfo?> TryGetPackageInfo(MuMuSessionLifetime session, AsyncRetryPolicy<StorePackageInfo?>? retryPolicy = null);
}
