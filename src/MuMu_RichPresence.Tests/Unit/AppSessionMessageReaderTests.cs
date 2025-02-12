namespace MuMu_RichPresence.Tests.Unit;

using Dawn.MuMu.RichPresence.Domain;
using Dawn.MuMu.RichPresence.PlayGames;
using FluentAssertions;

[TestFixture(TestOf = typeof(PlayGamesAppSessionMessageReader))]
public class AppSessionMessageReaderTests
{
    [SetUp]
    public void SetUp()
    {
        _sut = new PlayGamesAppSessionMessageReader("Assets/Service.log");
    }
    private PlayGamesAppSessionMessageReader _sut;


    [TearDown]
    public void Cleanup()
    {
        _sut.Dispose();
    }

    [Test]
    public async Task ShouldGet_FileLock()
    {
        await using var fileLock = _sut.AquireFileLock();
    }
    
    [Test]
    public async Task Sessions_ShouldBe_ExpectedAmount()
    {
        // Arrange
        await using var fileLock = _sut.AquireFileLock();
        
        // Act
        var sessionInfos = await _sut.GetAllSessionInfos(fileLock);

        // Assert
        sessionInfos.Count.Should().Be(137);
    }

    [Test]
    public async Task FirstSession_ShouldBe_DefenseDerby()
    {
        // Arrange
        await using var fileLock = _sut.AquireFileLock();
        
        // Act
        var sessionInfos = await _sut.GetAllSessionInfos(fileLock);

        // Assert
        sessionInfos.Should().HaveCountGreaterThanOrEqualTo(1);
        
        var first = sessionInfos[0];

        first.Title.Should().Be("Defense Derby");
        first.PackageName.Should().Be("com.krafton.defensederby");
        first.AppState.Should().Be(AppSessionState.Starting);
    }
    
    [Test]
    public async Task LastSession_ShouldBe_StoppedArknights()
    {
        // Arrange
        await using var fileLock = _sut.AquireFileLock();
        
        // Act
        var sessionInfos = await _sut.GetAllSessionInfos(fileLock);

        // Assert
        sessionInfos.Should().HaveCountGreaterThanOrEqualTo(1);
        
        var last = sessionInfos[^1];

        last.Title.Should().Be("Arknights");
        last.PackageName.Should().Be("com.YoStarEN.Arknights");
        last.AppState.Should().Be(AppSessionState.Stopped);
    }
    
    [Test]
    public async Task StartingSessions_ShouldHave_NoStartTime()
    {
        // Arrange
        await using var fileLock = _sut.AquireFileLock();
        
        // Act
        var sessionInfos = (await _sut.GetAllSessionInfos(fileLock))
            .Where(x => x.AppState == AppSessionState.Starting);

        // Assert
        sessionInfos.Should().AllSatisfy(x => x.StartTime.Should().BeExactly(default));
    } 

}