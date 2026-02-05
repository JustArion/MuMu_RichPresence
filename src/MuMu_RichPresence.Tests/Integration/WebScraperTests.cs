using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.MuMu;
using Dawn.MuMu.RichPresence.Scrapers;

namespace MuMu_RichPresence.Tests.Integration;

using System.Collections.Frozen;
using FluentAssertions;

[TestFixture(TestOf = typeof(PlayStoreScraper))]
public class WebScraperTests
{
    private static readonly FrozenDictionary<string, string> _appPackagesToTitles = new Dictionary<string, string>
    {
        ["com.YoStarEN.Arknights"] = "Arknights",
        ["com.nexon.bluearchive"] = "Blue Archive"
    }.ToFrozenDictionary();
    
    [TestCaseSource(nameof(_appPackagesToTitles))]
    public async Task ShouldScrape_Titles(KeyValuePair<string, string> package)
    {
        // Act
        var session = new MuMuSessionLifetime { AppState = AppState.Focused, PackageName = package.Key, Title = package.Value};
        
        var packageInfo = await PackageScraper.TryGetPackageInfo(session);

        // Assert
        packageInfo.Should().NotBeNull();

        packageInfo!.Title.Should().Be(package.Value);
    }
}