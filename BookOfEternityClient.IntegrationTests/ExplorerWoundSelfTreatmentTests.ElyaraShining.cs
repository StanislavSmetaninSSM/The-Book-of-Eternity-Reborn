using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Fact]
    public Task ConsoleCoLocatedShiningElyaraOpensFreshTreatmentAndPublishesOnePaidCycle() =>
        RunServiceJourneyAsync(browser: false, realm: "Shining Abode");

    [Fact]
    public Task BrowserCoLocatedShiningElyaraOpensFreshTreatmentAndPublishesOnePaidCycle() =>
        RunServiceJourneyAsync(browser: true, realm: "Shining Abode");

    [Fact]
    public Task ShiningElyaraPublicationFailureRestoresWholeUnpaidTurn() =>
        RunServiceJourneyAsync(browser: false, failPublication: true, realm: "Shining Abode");
}
