using Dawn.MuMu.RichPresence.MuMu;
using Polly;
using Polly.Retry;

namespace MuMu_RichPresence.Tests.Regression;

using FluentAssertions;

[TestFixture(TestOf = typeof(PlayStoreWebScraper))]
public class WebScraperTests
{
    private static readonly string[] _validPackages = ["com.YoStarEN.Arknights", "com.nexon.bluearchive"];
    private static readonly string[] _invalidPackages = ["com.android.vending", "com.android.browser"];

    private static readonly AsyncRetryPolicy<PlayStoreWebScraper.PlayStorePackageInfo?> _noRetryPolicy = Policy<PlayStoreWebScraper.PlayStorePackageInfo?>
        .Handle<Exception>()
        .WaitAndRetryAsync(1, _ => TimeSpan.FromSeconds(0));

    
    [Test]
    [TestCaseSource(nameof(_validPackages))]
    public async Task TryGetInfoAsync_WithValidAppPackage_ReturnsValidLink(string packageName)
    {
        // Act
        var packageInfo = await PlayStoreWebScraper.TryGetPackageInfo(packageName, _noRetryPolicy);

        var link = packageInfo?.IconLink;

        // Assert
        packageInfo.Should().NotBeNull();
        link.Should().NotBeNullOrEmpty();
            
        Uri.TryCreate(link, UriKind.Absolute, out var uri).Should().BeTrue();
        uri!.Scheme.Should().Be("https");
    }
    
    [Test]
    [TestCaseSource(nameof(_invalidPackages))]
    public async Task TryGetInfoAsync_WithInvalidAppPackage_ReturnsNull(string packageName)
    {
        // Act
        var packageInfo = await PlayStoreWebScraper.TryGetPackageInfo(packageName, _noRetryPolicy);

        // Assert
        packageInfo.Should().BeNull();
    }
    
    [Test]
    public async Task TryGetInfoAsync_WithEmptyAppPackage_ReturnsNull()
    {
        // Act
        var packageInfo = await PlayStoreWebScraper.TryGetPackageInfo(string.Empty, _noRetryPolicy);
            
        // Assert
        packageInfo.Should().BeNull();
    }
}