using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Preserves chronological wound-adjusted costs after actual dependent completion and common publication,
    /// while rejecting premature comparisons, changed exchanges and reuse against a later signed turn.
    /// </summary>
    /// <returns>
    /// A task completing after publication, scoped validation, drift rejection and ordinary historical validation.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualC4_PublishedDependentCostsRetainCausalityAndRejectDrift()
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: true, dependentCost: true);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var narrative = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, ProjectionNarrativePath))!)!;
            narrative["response"] = "Чужое давление надломило волю хранителя.";
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, ProjectionNarrativePath,
                System.Text.Encoding.UTF8.GetBytes(narrative.ToJsonString()));
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            Assert.Equal("offer", opened.Disposition);
            using (var session = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session))
            {
                var selected = await session.SubmitDecisionAsync(lease,
                    OriginalSpiritualWoundDecision(session.Offer!.OpportunityRef, "spiritual_action_cost_burden", "guard"),
                    "Чужое давление надломило волю хранителя.");
                using var selectedSession = selected.Session;
                Assert.True(selected.Disposition == "dependent_continuation", FormatC2SubmissionIssues(selected));
            }
            var raw = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, AfterlifeSpiritualConflictState.StatePath))!)!;
            raw["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["effectiveCost"] = 3;
            raw["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["after"] = 0;
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
                System.Text.Encoding.UTF8.GetBytes(raw.ToJsonString()));
            var current = await context.Validator.OpenC2PrivateSessionAsync(lease);
            Assert.True(current.Disposition == "dependent_continuation", FormatC2SubmissionIssues(current));
            using var resumedSession = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(current.Session);
            var resumed = await resumedSession.ResumeDependentContinuationAsync(lease);
            using var completedSession = resumed.Session;
            Assert.True(resumed.Disposition == "completed_unpublished", FormatC2SubmissionIssues(resumed));
        }

        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        ValidationService.SpiritualOriginalTurnCapture.SpiritualCompletedConflictValidation completion;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out var authority));
            completion = authority.CompletedConflictValidation;
        }
        using (context.Validator.UseCompletedSpiritualConflictValidationScope(completion))
            Assert.Contains(await ValidatePublishedConflictPhaseAsync(context),
                issue => issue.Code == "spiritual_completed_conflict_validation_mismatch");

        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues);
        Assert.NotNull(published.MechanicsPlan);
        Assert.Same(completion, published.SpiritualConflictValidation);
        var originalBytes = (await context.FileSystem.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath))!;
        using (context.Validator.UseCompletedSpiritualConflictValidationScope(completion))
        {
            AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
            var original = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(originalBytes).TrimStart('\uFEFF'))!;
            foreach (var mutation in new[] { "append_null", "append_object", "prepend_object", "cost", "order", "actor", "empty", "absent" })
            {
                var changed = original.DeepClone();
                if (mutation == "append_null")
                    changed["activeConflict"]!["exchangeLog"]!.AsArray().Add((JsonNode?)null);
                else if (mutation == "append_object")
                    changed["activeConflict"]!["exchangeLog"]!.AsArray().Add(new JsonObject());
                else if (mutation == "prepend_object")
                    changed["activeConflict"]!["exchangeLog"]!.AsArray().Insert(0, new JsonObject());
                else if (mutation == "cost")
                    changed["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["effectiveCost"] = 2;
                else if (mutation == "order")
                {
                    var log = changed["activeConflict"]!["exchangeLog"]!.AsArray();
                    var first = log[0]!.DeepClone();
                    log[0] = log[1]!.DeepClone();
                    log[1] = first;
                }
                else if (mutation == "actor")
                    changed["activeConflict"]!["oppositionSide"]!["leadContestant"]!["actorId"] = "foreign_guardian";
                else if (mutation == "empty")
                    changed["activeConflict"]!["exchangeLog"] = new JsonArray();
                else
                    changed["activeConflict"] = null;
                await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, changed.ToJsonString());
                Assert.Contains(await ValidatePublishedConflictPhaseAsync(context),
                    issue => issue.Code == "spiritual_completed_conflict_validation_mismatch");
                await context.FileSystem.WriteFileAtomicBytesAsync(AfterlifeSpiritualConflictState.StatePath, originalBytes);
            }
            AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
        }
        Assert.Contains(await ValidatePublishedConflictPhaseAsync(context),
            issue => issue.Code == "afterlife_conflict_opposition_action_cost_mismatch");

        var originalManifest = JsonNode.Parse((await context.FileSystem.ReadFileAsync(
            "game_state/control/pending_turn_snapshot.json"))!);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8]);
        AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
        using (context.Validator.UseCompletedSpiritualConflictValidationScope(completion))
        {
            Assert.Contains(await ValidatePublishedConflictPhaseAsync(context),
                issue => issue.Code == "spiritual_completed_conflict_validation_mismatch");
            using (context.Validator.UsePrevalidatedPendingTurnSnapshotScope(originalManifest))
                Assert.Contains(await ValidatePublishedConflictPhaseAsync(context),
                    issue => issue.Code == "spiritual_completed_conflict_validation_mismatch");
        }
    }

    /// <summary>
    /// Runs the same ordinary spiritual conflict phase invoked after the GameEngine's common publication.
    /// </summary>
    /// <param name="context">
    /// Fixture retaining the actual published canonical state and authenticated pending snapshot.
    /// </param>
    /// <returns>
    /// The ordinary phase diagnostics without materializing or altering canonical state.
    /// </returns>
    private static async Task<IReadOnlyList<ValidationIssue>> ValidatePublishedConflictPhaseAsync(
        ResourceMaterializationTestContext context) => await context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState));
}
