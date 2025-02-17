namespace MuMu_RichPresence.Tests.Unit;

using System.Collections.ObjectModel;
using Dawn.MuMu.RichPresence;
using Dawn.MuMu.RichPresence.Models;
using FluentAssertions;

[TestFixture(TestOf = typeof(MuMuPlayerLogReader))]
public class AppSessionMessageReaderTests
{
    [SetUp]
    public void SetUp()
    {
        _sut = new MuMuPlayerLogReader("Assets/shell.log");
    }
    private MuMuPlayerLogReader _sut;


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
        var sessions = new ObservableCollection<MuMuSessionLifetime>();
        var graveyard = new ObservableCollection<MuMuSessionLifetime>();
        
        // Act
        await _sut.GetAllSessionInfos(fileLock, sessions, graveyard);

        // Assert
        sessions.Count.Should().Be(2);

        graveyard.Count.Should().Be(6);
    }

    [Test]
    public async Task FirstGame_ShouldBe_Triglav()
    {
        // Arrange
        await using var fileLock = _sut.AquireFileLock();
        var sessions = new ObservableCollection<MuMuSessionLifetime>();
        var graveyard = new ObservableCollection<MuMuSessionLifetime>();
        
        // Act
        await _sut.GetAllSessionInfos(fileLock, sessions, graveyard);

        // Assert
        var playedGames = graveyard.Where(x => !AppLifetimeParser.IsSystemLevelPackage(x.PackageName)).ToArray();
        playedGames.Should().HaveCountGreaterThanOrEqualTo(1);

        var first = playedGames.First();

        first.Title.Should().Be("Triglav");
        first.PackageName.Should().Be("com.SmokymonkeyS.Triglav");
        first.AppState.Value.Should().Be(AppState.Stopped);
    }
    
    [Test]
    public async Task LastGame_ShouldBe_NightOfTheFullMoon()
    {
        // Arrange
        await using var fileLock = _sut.AquireFileLock();
        var sessions = new ObservableCollection<MuMuSessionLifetime>();
        var graveyard = new ObservableCollection<MuMuSessionLifetime>();
        
        // Act
        await _sut.GetAllSessionInfos(fileLock, sessions, graveyard);

        // Assert
        var playedGames = graveyard.Where(x => !AppLifetimeParser.IsSystemLevelPackage(x.PackageName)).ToArray();
        playedGames.Should().HaveCountGreaterThanOrEqualTo(1);

        var last = playedGames.Last();

        last.Title.Should().Be("Night of the Full Moon");
        last.PackageName.Should().Be("com.ztgame.yyzy");
        last.AppState.Value.Should().Be(AppState.Stopped);
    }
}