using Dawn.MuMu.RichPresence.Discord;
using FluentAssertions;

namespace MuMu_RichPresence.Tests.Integration;

[TestFixture(TestOf =  typeof(DiscoverabilityHandler))]
public class DiscoverabilityHandlerTests
{
    [TestCase]
    public async Task Should_ReturnEntries()
    {
        // Arrange
        var handler = new DiscoverabilityHandler();

        // Act
        var applicationId = await handler.TryGetOfficialApplicationId("Clash of Clans");

        // Assert
        applicationId
            .Should()
            .NotBeNullOrWhiteSpace();

        handler._discoverablePresences
            .Should()
            .HaveCountGreaterThan(1000);

        handler._discoverablePresences
            .Should()
            .AllSatisfy(x =>
            {
                x.Name
                    .Should()
                    .NotBeNullOrWhiteSpace();
                
                x.Id
                    .Should()
                    .NotBeNullOrWhiteSpace();
            });

    }
}