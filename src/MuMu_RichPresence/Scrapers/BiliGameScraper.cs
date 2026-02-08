using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.Scrapers.Models;
using Polly;
using Polly.Retry;

namespace Dawn.MuMu.RichPresence.Scrapers;

public class BiliGameScraper : IWebStoreScraper
{
    private static readonly HttpClient _client = new();
    private static readonly ConcurrentDictionary<string, StorePackageInfo> _webCache = new();
    private static readonly ConcurrentDictionary<string, BiliGameEntry> _gameCache = new();

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly AsyncRetryPolicy<StorePackageInfo?> _retryPolicy = Policy<StorePackageInfo?>
        .Handle<Exception>()
        .WaitAndRetryAsync(MAX_RETRIES, _ => TimeSpan.FromSeconds(1));

    private const int MAX_RETRIES = 3;

    private static Uri GetIconUrl(string partial)
    {
        var iconIdentifier = partial.Split("/bfs/game/").Last();

        return new($"https://article.biliimg.com/bfs/game/{iconIdentifier}");
    }

    public static string GetAPILinkForAppLabel(string appLabel) => $"https://line3-h5-mobile-api.biligame.com/game/center/h5/search/game_name?keyword={appLabel}&sdk_type=2";

    // https://www.biligame.com/detail/?id=101772
    // We never visit this site so it can be omitted from the repo's permissions.md
    // It's only linked to via a link on the Rich Presence on which people can click to visit the store page
    public async ValueTask<string> GetStoreLinkForSession(MuMuSessionLifetime session)
    {
        var identifier = session.Title;

        if (_gameCache.TryGetValue(identifier, out var entry))
            return $"https://www.biligame.com/detail/?id={entry.GameBaseId}";

        if (await TryGetPackageInfo(session) is not null)
            return _gameCache.TryGetValue(identifier, out entry)
                ? $"https://www.bilibili.com/detail/?id={entry.GameBaseId}"
                : string.Empty;

        return string.Empty;
    }

    public async ValueTask<StorePackageInfo?> TryGetPackageInfo(MuMuSessionLifetime session, AsyncRetryPolicy<StorePackageInfo?>? retryPolicy = null)
    {
        if (_webCache.TryGetValue(session.Title, out var info))
            return info;

        var link = GetAPILinkForAppLabel(session.Title);

        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var response = (await _client.GetFromJsonAsync<BiliGameResponse<BiliGameEntry>>(link, _options))!;

                Debug.Assert(response.Code == BiliGameResponse<BiliGameEntry>.SUCCESS_CODE);

                var result = response.Data.FirstOrDefault();

                if (result is not { Icon: not null })
                    return null;

                _gameCache.TryAdd(session.Title, result);
                var packageInfo = new StorePackageInfo(GetIconUrl(result.Icon!).ToString(), result.GameName);
                _webCache.TryAdd(session.Title, packageInfo);

                return packageInfo;
            });
        }
        #if DEBUG
        catch (HttpRequestException e)
        #else
        catch (HttpRequestException)
        #endif
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
}
