namespace MuMu_RichPresence.Tests.Unit;

using System.Web;
using Dawn.MuMu.RichPresence;
using FluentAssertions;

[TestFixture(TestOf = typeof(PlayStoreWebScraper))]
public class IconScraperTests
{
    private static readonly string[] _appPackages = ["com.YoStarEN.Arknights", "com.krafton.defensederby"];
    [Test]
    public async Task ShouldScrape_Links()
    {
        // Act
        foreach (var appPackage in _appPackages)
        {
            var link = await PlayStoreWebScraper.TryGetInfoAsync(appPackage);

            // Assert
            link.Should().NotBeNullOrEmpty();
            
            Uri.TryCreate(link, UriKind.Absolute, out var uri).Should().BeTrue();
            uri!.Scheme.Should().Be("https");
        }

    }
}