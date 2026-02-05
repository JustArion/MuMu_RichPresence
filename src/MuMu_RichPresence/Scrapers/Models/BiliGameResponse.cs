namespace Dawn.MuMu.RichPresence.Scrapers.Models;

// There are a lot more properties, but we only need the ones listed below
public record BiliGameResponse<T>(int Code, T[] Data)
{
    public const int SUCCESS_CODE = 0;
}
