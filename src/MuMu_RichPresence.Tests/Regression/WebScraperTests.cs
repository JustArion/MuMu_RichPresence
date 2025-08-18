namespace MuMu_RichPresence.Tests.Regression;

using Dawn.MuMu.RichPresence;
using FluentAssertions;

[TestFixture(TestOf = typeof(Dawn.MuMu.RichPresence.MuMu.PlayStoreWebScraper))]
public class WebScraperTests
{
    private static readonly string[] _validPackages = ["com.YoStarEN.Arknights", "com.nexon.bluearchive"];
    private static readonly string[] _invalidPackages = ["com.android.vending", "com.android.browser"];

    [Test]
    [TestCaseSource(nameof(_validPackages))]
    public async Task TryGetInfoAsync_WithValidAppPackage_ReturnsValidLink(string packageName)
    {
        // Act
        var packageInfo = await Dawn.MuMu.RichPresence.MuMu.PlayStoreWebScraper.TryGetPackageInfo(packageName);

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
        var packageInfo = await Dawn.MuMu.RichPresence.MuMu.PlayStoreWebScraper.TryGetPackageInfo(packageName);

        // Assert
        packageInfo.Should().BeNull();
    }
    
    [Test]
    public async Task TryGetInfoAsync_WithEmptyAppPackage_ReturnsNull()
    {
        // Act
        var packageInfo = await Dawn.MuMu.RichPresence.MuMu.PlayStoreWebScraper.TryGetPackageInfo(string.Empty);
            
        // Assert
        packageInfo.Should().BeNull();
    }
}