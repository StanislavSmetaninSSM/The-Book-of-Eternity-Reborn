using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Desired success: complete signed/current singletonIII/IV ordinary14/II equals actual14/II at broken through v3/C1 after cold/warm proof.</summary>
    /// <param name="rank">Independent full parent severity before signing; one parent contributes exactly one step at either rank.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public Task SpiritualSingletonCapped_PlayerRealOwnerAdvancesAndComposesC1(int rank) =>
        WithSingletonIvJourneyFailureAsync("singleton_capped_player_owner_" + rank, () =>
            WithPressureGroupCaseAsync(new("singleton_capped_player_owner_" + rank, Parents: 1, After: "broken",
                Ranks: new(rank, 3), AdvanceExpected: true, CappedOrdinary: true), window =>
                AssertSingletonIvOwnerAsync(window.Spec.Name, window.Copy, window.Capture, window.Lease, window.Files,
                    window.Generation, window.Frontier, JsonNode.Parse(Assert.Single(window.ExpectedParentJson))!.AsObject(),
                    guardian: false, expectedParentRank: rank, playerContour: PlayerSingletonPressureContour.CappedBroken,
                    observeActualRefusal: true)));

    /// <summary>Future GREEN only: independently owned III/IV player normal publication through actual/C1, first C2, explicit cold Open, none/C3, common C4 and signed47.</summary>
    /// <param name="rank">Independent parentIII/IV; both normal rows require accepted outcomes before rollback qualification.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public Task SpiritualSingletonCapped_PlayerActualColdNonePublishesOnceAndValidatesSignedNext(int rank) =>
        WithSingletonIvJourneyFailureAsync("singleton_capped_player_publication_" + rank, () =>
            WithSingletonIvPlayerJourneyAsync("singleton_capped_player_publication_" + rank, async setup =>
            {
                var completed = await CompleteGuardianAttributionColdC3Async(setup, setup.Copy.Context.Validator);
                await PublishGuardianAttributionAndReadNextAsync(setup, setup.Copy.Context.Validator, completed);
            }, retainedRank: rank, contour: PlayerSingletonPressureContour.CappedBroken));

    /// <summary>Future GREEN only: witnessed committed actual/resource/receipt writes, complete signed-origin rollback and one fresh same-byte retry at III/IV.</summary>
    /// <param name="rank">Independent pre-sign parentIII/IV, never inferred from retained issued DATA.</param>
    [Theory]
    [InlineData(4)]
    public Task SpiritualSingletonCapped_PlayerLateCutRestoresSignedOriginAndFreshRetryPublishesOnce(int rank) =>
        WithSingletonIvJourneyFailureAsync("singleton_capped_player_rollback_" + rank, () =>
            AssertGuardianAttributionLatePublicationCutAsync("singleton_capped_player_rollback_" + rank, retainedRank: rank,
                role: PressureSingletonJourneyRole.Player, playerContour: PlayerSingletonPressureContour.CappedBroken));
}
