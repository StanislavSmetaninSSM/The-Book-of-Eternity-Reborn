using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private static int _spiritualStagedOriginalPreparationCount;
    private static int _spiritualStagedAcceptedPreparationCount;

    /// <summary>
    /// Seeds the fixed original intake and matching tier-two pressure mirrors before snapshot signing.
    /// </summary>
    /// <param name="context">
    /// Fresh conflict frame whose original item, location, profile, soul and opposition tier bytes are written.
    /// </param>
    /// <returns>
    /// Completion after the same original inputs used by the staged and critical-frontier fixtures are present.
    /// </returns>
    private static async Task SeedOriginalIntakeTierTwoBaselinesAsync(ResourceMaterializationTestContext context)
    {
        await SeedOriginalIntakeBaselinesAsync(context);
        await SeedSpiritualWorseningTierAuthorityAsync(context);
    }

    /// <summary>
    /// Creates the genuine saved player-wound selection whose middle exchange needs authored critical narration.
    /// </summary>
    /// <returns>
    /// Disposable signed three-exchange context with an unadvanced pending submission; the caller owns disposal.
    /// </returns>
    private static async Task<ResourceMaterializationTestContext> CreateSpiritualStagedContinuationContextAsync()
    {
        Interlocked.Increment(ref _spiritualStagedOriginalPreparationCount);
        var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeTierTwoBaselinesAsync,
            signedDice: [5, 15, 20, 18, 9, 8]);
        try
        {
            await WriteSpiritualStagedOriginalExchangesAsync(context);
            const string scene = "Чужое давление надломило волю души.";
            await context.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject
            {
                ["response"] = scene, ["timestamp"] = "2026-08-15T00:01:00Z"
            }.ToJsonString());
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await CommitSpiritualContinuationDraftFirstPairAsync(context, lease);
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            Assert.Equal("offer", opened.Disposition);
            using var offered = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            var submitted = await offered.SubmitDecisionAsync(lease,
                CreateSpiritualStagedPlayerDecision(offered.Offer!.OpportunityRef), scene);
            using var submittedOwner = submitted.Session;
            Assert.True(submitted.Disposition == "dependent_continuation", FormatC2SubmissionIssues(submitted));
            return context;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Reuses the reviewed critical fixture's original pressure sequence without selecting a wound or acquiring private authority.
    /// </summary>
    /// <param name="context">
    /// Signed fixture with tier-two pressure and dice 5, 15, 20, 18, 9, 8.
    /// </param>
    /// <returns>
    /// The complete written original conflict with only the first harmful source.
    /// </returns>
    internal static async Task<JsonObject> WriteSpiritualStagedOriginalExchangesAsync(ResourceMaterializationTestContext context)
    {
        await WritePositionBurdenExchangesAsync(context, playerWound: true, modifier: "missing");
        return await WriteCriticalPositionContinuationDraftAsync(context);
    }

    /// <summary>
    /// Authors the current offer's rank-I player pressure-position wound using the existing complete proposal.
    /// </summary>
    /// <param name="opportunityRef">
    /// Exact reference from the real initial offer.
    /// </param>
    /// <returns>
    /// The detached decision with magnitude one and no additional consequence profile.
    /// </returns>
    internal static JsonElement CreateSpiritualStagedPlayerDecision(string opportunityRef)
    {
        var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(opportunityRef,
            "spiritual_position_burden", "pressure", "player").GetRawText())!;
        decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 1;
        return JsonSerializer.SerializeToElement(decision);
    }

    /// <summary>
    /// Authors only the middle exchange's prescribed arithmetic and both actual GM narrative constraints.
    /// </summary>
    /// <param name="original">
    /// Complete original direct carrier; this object is not mutated.
    /// </param>
    /// <returns>
    /// A detached A-only candidate preserving the third exchange and every independent input.
    /// </returns>
    internal static JsonObject CreateSpiritualStagedCorrectionA(JsonObject original)
    {
        var candidate = original.DeepClone().AsObject();
        var dice = candidate["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!;
        Assert.Empty(dice["modifierBreakdown"]!["opposition"]!.AsArray());
        dice["modifierBreakdown"]!["opposition"]!.AsArray().Add(CriticalPositionOppositionModifier());
        dice["oppositionTotal"] = 20;
        dice["margin"] = 1;
        dice["criticalResult"] = new JsonObject
        {
            ["playerNaturalRoll"] = 20, ["oppositionNaturalRoll"] = 18,
            ["marginOutcomeBand"] = "mixed_or_no_effect", ["normalizedOutcomeBand"] = "player_success",
            ["scaleLimit"] = "Удачный бросок ограничен текущим духовным обменом и не создаёт нового вреда.",
            ["narrativeConstraint"] = "Обе стороны сохраняют напряжение, позицию и исходный no_effect."
        };
        return candidate;
    }

    /// <summary>
    /// Authors only the later arithmetic group after actual completion of A permits its independent request B.
    /// </summary>
    /// <param name="completedA">
    /// A-only candidate or its exact current physical image; this object is not mutated.
    /// </param>
    /// <returns>
    /// A detached fully corrected carrier with third totals nine and ten, preserving the mixed band and all results.
    /// </returns>
    internal static JsonObject CreateSpiritualStagedCorrectionB(JsonObject completedA)
    {
        var candidate = completedA.DeepClone().AsObject();
        var dice = candidate["activeConflict"]!["exchangeLog"]![2]!["diceAudit"]!;
        Assert.Empty(dice["modifierBreakdown"]!["opposition"]!.AsArray());
        dice["modifierBreakdown"]!["opposition"]!.AsArray().Add(CriticalPositionOppositionModifier());
        dice["oppositionTotal"] = 10;
        dice["margin"] = -1;
        return candidate;
    }

    /// <summary>
    /// Creates the closed empty-decision response for one issued dependent draft frontier.
    /// </summary>
    /// <param name="request">
    /// The actual dependent request whose continuation ID the response must echo.
    /// </param>
    /// <returns>
    /// A correlated response that makes no new wound choice.
    /// </returns>
    private static SpiritualWoundContinuationResponse CreateSpiritualStagedEmptyResponse(
        SpiritualWoundContinuationRequest request)
    {
        Assert.Equal("dependent_draft", request.Phase);
        Assert.Null(request.Offer);
        return new() { ContinuationId = request.ContinuationId, WoundDecisions = [] };
    }
}
