using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task CoLocatedShiningElyaraOpensFreshTreatmentAndPublishesOnePaidCycle(bool browser) =>
        RunServiceJourneyAsync(browser, realm: "Shining Abode");

    [Fact]
    public Task ShiningElyaraPublicationFailureRestoresWholeUnpaidTurn() =>
        RunServiceJourneyAsync(browser: false, failPublication: true, realm: "Shining Abode");
}
