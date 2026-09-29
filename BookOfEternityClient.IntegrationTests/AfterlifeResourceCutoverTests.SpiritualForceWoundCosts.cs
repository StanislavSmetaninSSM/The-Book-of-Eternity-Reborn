using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Adds only an owned wound payment to a future force action and preserves the free path without a burden.
    /// </summary>
    /// <param name="player">
    /// Whether the newly wounded actor is the player rather than the opposition.
    /// </param>
    /// <param name="burden">
    /// Whether the wound decision adds a cost; false exercises the original free action without an audit root.
    /// </param>
    /// <param name="existingSibling">
    /// Whether an independent recovery audit must remain unchanged.
    /// </param>
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    public Task OriginalSpiritualGeneration_ForcePaysOnlyOwnedWound(bool player, bool burden, bool existingSibling) =>
        VerifyForceWoundCostAsync(player, burden, existingSibling);

    /// <summary>
    /// Rejects a force payment that lacks either prior funds or an applicable owned wound.
    /// </summary>
    /// <param name="player">
    /// Whether the submitted force payment belongs to the player rather than opposition.
    /// </param>
    /// <param name="insufficient">
    /// True checks missing prior funds; false checks a fabricated payment without a wound.
    /// </param>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public Task OriginalSpiritualGeneration_ForceRejectsUnaffordableOrUnownedPayment(bool player, bool insufficient) =>
        VerifyForceWoundCostAsync(player, burden: insufficient, existingSibling: false,
            rejection: insufficient ? "insufficient" : "unowned");

    /// <summary>
    /// Removes only the future force audit made obsolete by the first payment exhausting a one-use wound effect.
    /// </summary>
    /// <param name="preserveSibling">
    /// Whether removal must preserve an ordinary opposition recovery audit instead of removing the otherwise empty root.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OriginalSpiritualGeneration_ForceRemovesExpiredFutureAudit(bool preserveSibling) =>
        VerifyForceWoundCostAsync(player: true, burden: true, existingSibling: preserveSibling, rejection: "expires");

    /// <summary>
    /// Preserves one saved choice and stable permissions across payment and expiry correction rounds.
    /// </summary>
    /// <param name="preserveSibling">
    /// Whether the expiring audit shares its container with an independent opposition audit.
    /// </param>
    /// <returns>
    /// Completion after both diagnostic rounds and the single saved resume have been verified.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OriginalSpiritualC2DependentContext_ClosesForceExpiry(bool preserveSibling) =>
        VerifyForceWoundCostAsync(player: true, burden: true, existingSibling: preserveSibling,
            rejection: "expires", inspectContext: true);

    /// <summary>
    /// Preserves accepted operation spelling while deriving an existing future force audit's numeric correction.
    /// </summary>
    /// <returns>
    /// Completion after stable permissions and one owner-bound payment have been checked.
    /// </returns>
    [Fact]
    public Task OriginalSpiritualC2DependentContext_PreservesExistingForceEvidence() =>
        VerifyForceWoundCostAsync(player: true, burden: true, existingSibling: false,
            rejection: "expires", inspectContext: true, existingForceAudit: true);

    /// <summary>
    /// Exercises force payment admission, dependent correction and completed-plan replay through actual owners.
    /// </summary>
    /// <param name="player">
    /// Whether the targeted cost belongs to the player rather than opposition.
    /// </param>
    /// <param name="burden">
    /// Whether the decision installs a force-cost wound.
    /// </param>
    /// <param name="existingSibling">
    /// Whether an independent recovery audit must be preserved.
    /// </param>
    /// <param name="rejection">
    /// Optional affordability/missing-owner defect or the expires lifetime scenario; null uses ordinary completion.
    /// </param>
    /// <param name="inspectContext">
    /// Whether to verify the complete dependent correction context across two expiry repair rounds.
    /// </param>
    /// <param name="existingForceAudit">
    /// Whether the first correction must preserve an existing audit's accepted uppercase operation token.
    /// </param>
    /// <returns>
    /// A task completing after the selected owner-bound scenario has been checked.
    /// </returns>
    private async Task VerifyForceWoundCostAsync(bool player, bool burden, bool existingSibling,
        string? rejection = null, bool inspectContext = false, bool existingForceAudit = false)
    {
        var insufficient = rejection == "insufficient";
        var expires = rejection == "expires";
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync,
            signedDice: player ? [5, 15, 12, 8] : null,
            playerCurrent: insufficient && player ? 3m : 6m,
            oppositionCurrent: insufficient && !player ? 3m : 6m);
        await WriteCompleteConflictFrameExchangeAsync(context);
        if (player)
        {
            var initial = await ReadProjectedSourceContinuationCandidateAsync(context);
            var active = initial["activeConflict"]!;
            var exchange = active["exchangeLog"]![0]!;
            exchange["outcome"] = "setback";
            exchange["after"]!["playerSideStrain"] = "strained";
            exchange["after"]!["oppositionSideStrain"] = "clear";
            var dice = exchange["diceAudit"]!;
            dice["diceUsed"]![0]!["value"] = 5;
            dice["diceUsed"]![1]!["value"] = 15;
            dice["playerTotal"] = 5;
            dice["oppositionTotal"] = 15;
            dice["margin"] = -10;
            dice["outcomeBand"] = "decisive_opposition_success";
            active["playerSideStrain"] = "strained";
            active["oppositionSideStrain"] = "clear";
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, initial.ToJsonString());
        }
        if (insufficient)
        {
            var initial = await ReadProjectedSourceContinuationCandidateAsync(context);
            var initialCost = initial["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]![player ? "player" : "opposition"]!;
            initialCost["before"] = 3;
            initialCost["after"] = 0;
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, initial.ToJsonString());
        }
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var conflict = candidate["activeConflict"]!;
        var second = conflict["exchangeLog"]![1]!.AsObject();
        second["operationType"] = "force_incarnation";
        second["outcome"] = "no_effect";
        second["after"] = second["before"]!.DeepClone();
        second.Remove("diceAudit");
        second.Remove("actionCostAudit");
        second["incomingAction"] = new JsonObject
        {
            ["operationType"] = "force_incarnation", ["actorType"] = "guardian", ["actorId"] = "guardian_frame",
            ["summary"] = "Хранитель пытается направить душу к воплощению."
        };
        second["matchupAudit"]!["playerOperation"] = "force_incarnation";
        second["matchupAudit"]!["oppositionOperation"] = "force_incarnation";
        second["matchupAudit"]!["primaryResolutionLane"] = "force_incarnation";
        second["matchupAudit"]!["riskProfile"] = "terminal_choice";
        if (existingSibling && !expires)
        {
            second["incomingAction"]!["operationType"] = "guard";
            second["incomingAction"]!["finalOperationType"] = "force_incarnation";
            second["operationType"] = "recover_spiritual_power";
            second["matchupAudit"]!["playerOperation"] = "recover_spiritual_power";
            second["matchupAudit"]!["primaryResolutionLane"] = "recover_spiritual_power";
            second["matchupAudit"]!["riskProfile"] = "recovery_timing";
            second["actionCostAudit"] = new JsonObject
            {
                ["player"] = CostAudit("recover_spiritual_power", 0, 3, 3)
            };
        }
        if (rejection == "unowned")
            second["actionCostAudit"] = new JsonObject
            {
                [player ? "player" : "opposition"] = new JsonObject
                {
                    ["operationType"] = "force_incarnation", ["baseCost"] = 0, ["minCost"] = 0,
                    ["artTier"] = 0, ["effectiveCost"] = 1, ["before"] = 3, ["after"] = 2
                }
            };
        if (expires)
        {
            second["actionCostAudit"] = new JsonObject
            {
                ["player"] = new JsonObject
                {
                    ["operationType"] = "force_incarnation", ["baseCost"] = 0, ["minCost"] = 0,
                    ["artTier"] = 0, ["effectiveCost"] = 1, ["before"] = 3, ["after"] = 2
                }
            };
            if (existingSibling)
            {
                second["incomingAction"]!["operationType"] = "recover_spiritual_power";
                second["matchupAudit"]!["oppositionOperation"] = "recover_spiritual_power";
                second["actionCostAudit"]!["opposition"] = CostAudit("recover_spiritual_power", 0, 3, 3);
            }
            var third = second.DeepClone().AsObject();
            third["exchangeId"] = "exchange_source_third";
            third["actionCostAudit"]!["player"]!["before"] = 2;
            third["actionCostAudit"]!["player"]!["after"] = 1;
            conflict["exchangeLog"]!.AsArray().Add(third);
            if (existingForceAudit)
            {
                second["actionCostAudit"]!["player"]!["effectiveCost"] = 0;
                second["actionCostAudit"]!["player"]!["after"] = 3;
                second["actionCostAudit"]!["player"]!["operationType"] = "FORCE_INCARNATION";
            }
            else if (inspectContext)
                RemovePlayerAudit(second);
        }
        foreach (var side in new[] { "playerSideStrain", "oppositionSideStrain" })
            conflict[side] = second["after"]![side]!.DeepClone();
        var existingForceEvidence = existingForceAudit
            ? second["actionCostAudit"]!["player"]!.DeepClone().AsObject()
            : null;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Духовный обмен завершён.\"}"));
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        _ = context.FileSystem.GetOrCreateSessionGeneration(lease);
        var forceAuditWasPresentAtCapture = second["actionCostAudit"]?[player ? "player" : "opposition"] is JsonObject;
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var committed = await capture.CommitC2FirstTransportAsync(lease, first.Step!.Interval!);
        AssertNoConflictFrameErrors(committed.Issues);
        Assert.Equal("committed", committed.Disposition);
        capture.Dispose();
        var adapter = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var offered = await adapter.OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(offered.Issues);
        Assert.Equal("offer", offered.Disposition);
        using var offerSession = offered.Session!;
        JsonElement Decision(string opportunity)
        {
            if (!burden)
                return JsonSerializer.SerializeToElement(new { opportunityRef = opportunity, decision = "none" });
            var response = OriginalSpiritualWoundDecision(opportunity, "spiritual_action_cost_burden",
                "force_incarnation", player ? "player" : "guardian");
            if (!expires)
                return response;
            var root = JsonNode.Parse(response.GetRawText())!;
            var definition = root["proposal"]!["consequenceDefinitions"]![0]!["definition"]!;
            definition["triggers"] = new JsonArray(new JsonObject
            {
                ["triggerId"] = "last_force_payment", ["eventType"] = "resource_spent", ["priority"] = 100,
                ["componentIds"] = new JsonArray("component_001"), ["consumeUses"] = true,
                ["resolutionMode"] = "deterministic"
            });
            definition["lifetime"] = new JsonObject
            {
                ["mode"] = "uses", ["initialUses"] = 1,
                ["consumingEventTypes"] = new JsonArray("resource_spent")
            };
            return JsonSerializer.SerializeToElement(root);
        }
        var response = Decision(offerSession.Offer!.OpportunityRef);
        var woundNarration = player ? "Чужое давление надломило волю души." : "Чужое давление надломило волю хранителя.";
        var originalCheckpoint = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var originalPending = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath);
        var originalResources = await context.FileSystem.ReadFileBytesAsync(lease,
            ResourceMaterializationContract.StatePath);
        var result = await offerSession.SubmitDecisionAsync(lease, response,
            burden ? woundNarration : null);
        var selectedCheckpoint = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var selectedCommand = await context.FileSystem.ReadFileBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath);
        ValidationService.SpiritualC2DependentContext? initialContext = null;
        if (inspectContext)
        {
            var selectedOpen = await adapter.OpenC2PrivateSessionAsync(lease);
            Assert.Equal("dependent_continuation", selectedOpen.Disposition);
            using var selectedSession = selectedOpen.Session!;
            Assert.Null(selectedSession.Offer);
            initialContext = Assert.IsType<ValidationService.SpiritualC2DependentContext>(
                await selectedSession.ReadDependentContextAsync(lease));
            var forceFields = new[] { "operationType", "baseCost", "minCost", "artTier", "effectiveCost", "before", "after" };
            Assert.Equal(new[] { 1, 2 }.SelectMany(index =>
                    (index == 1 && existingForceAudit ? new[] { "effectiveCost", "after" } : forceFields).Select(field =>
                    $"/activeConflict/exchangeLog/{index}/actionCostAudit/player/{field}"))
                .OrderBy(value => value, StringComparer.Ordinal),
                initialContext.DependentDraftFields.Select(field => field.JsonPointer));
            Assert.Contains(initialContext.BaselineIssues,
                issue => issue.Code == (existingForceAudit ? "afterlife_conflict_action_cost_mismatch" :
                    "afterlife_conflict_wound_force_cost_audit_missing"));
            Assert.Contains(initialContext.BaselineIssues,
                issue => issue.Code == "afterlife_conflict_wound_force_cost_audit_obsolete");
            Assert.Equal(selectedCheckpoint, await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath));
            Assert.Equal(selectedCommand, await context.FileSystem.ReadFileBytesAsync(lease,
                AcceptedMechanicsPlan.WoundCommandPath));
        }
        if (burden || rejection == "unowned")
        {
            var original = JsonNode.Parse(originalCheckpoint!)!["checkpoint"]!;
            var selected = JsonNode.Parse(selectedCheckpoint!)!["checkpoint"]!;
            foreach (var field in new[] { "allocations", "advances", "committedAdvance", "expectedPendingPacketFingerprint" })
                Assert.True(JsonNode.DeepEquals(original[field], selected[field]), field);
            var submission = Assert.IsType<JsonObject>(selected["pendingSubmission"]);
            Assert.Equal(original["committedAdvance"]!.GetValue<int>(),
                submission["priorCommittedAdvance"]!.GetValue<int>());
            Assert.Equal(burden ? "materialize" : "none", submission["stagedDecision"]!["decision"]!.GetValue<string>());
            Assert.Equal(response.GetProperty("opportunityRef").GetString(),
                submission["stagedDecision"]!["opportunityRef"]!.GetValue<string>());
            Assert.Equal(selectedCommand,
                Convert.FromBase64String(submission["command"]!["contentBase64"]!.GetValue<string>()));
            if (burden)
                Assert.NotEmpty(submission["allocations"]!.AsArray());
            Assert.Equal(originalPending, await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath));
            Assert.Equal(originalResources, await context.FileSystem.ReadFileBytesAsync(lease,
                ResourceMaterializationContract.StatePath));
        }
        if (burden)
        {
            Assert.True(result.Disposition == "dependent_continuation",
                result.Disposition + Environment.NewLine + string.Join(Environment.NewLine,
                    result.Issues.Select(value => $"{value.Code}: {value}; expected={value.Expected}; actual={value.Actual}")));
            var cost = CostAudit("force_incarnation", 1, insufficient ? 0 : 3, insufficient ? 0 : 2);
            cost["baseCost"] = 0;
            cost["minCost"] = 0;
            cost["artTier"] = 0;
            cost.Remove("max");
            if (existingForceAudit) cost["operationType"] = "FORCE_INCARNATION";
            if (second["actionCostAudit"] is not JsonObject)
                second["actionCostAudit"] = new JsonObject();
            second["actionCostAudit"]![player ? "player" : "opposition"] = cost;
            if (expires)
            {
                if (inspectContext)
                {
                    await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                        AfterlifeSpiritualConflictState.StatePath, Encoding.UTF8.GetBytes(candidate.ToJsonString()));
                    var cold = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
                    var partialOpen = await cold.OpenC2PrivateSessionAsync(lease);
                    Assert.Equal("dependent_continuation", partialOpen.Disposition);
                    using var partialSession = partialOpen.Session!;
                    for (var gate = 0; gate < 2; gate++)
                    {
                        var partialContext = Assert.IsType<ValidationService.SpiritualC2DependentContext>(
                            await partialSession.ReadDependentContextAsync(lease));
                        Assert.Equal(initialContext!.ContinuationId, partialContext.ContinuationId);
                        Assert.Equal(initialContext.DependentDraftFields.Select(field => field.JsonPointer),
                            partialContext.DependentDraftFields.Select(field => field.JsonPointer));
                        Assert.Contains(partialContext.CurrentIssues, issue =>
                            issue.Code == "afterlife_conflict_wound_force_cost_audit_obsolete" &&
                            issue.FilePath.Contains("exchangeLog[2]", StringComparison.Ordinal));
                        Assert.Equal(selectedCheckpoint, await context.FileSystem.ReadFileBytesAsync(lease,
                            SpiritualWoundCaptureCheckpointState.StatePath));
                        Assert.Equal(selectedCommand, await context.FileSystem.ReadFileBytesAsync(lease,
                            AcceptedMechanicsPlan.WoundCommandPath));
                        Assert.Equal(originalPending, await context.FileSystem.ReadFileBytesAsync(lease,
                            SpiritualWoundDecisionPendingState.StatePath));
                        Assert.Equal(originalResources, await context.FileSystem.ReadFileBytesAsync(lease,
                            ResourceMaterializationContract.StatePath));
                    }
                }
                RemovePlayerAudit(conflict["exchangeLog"]![2]!.AsObject());
                var waivedPayment = candidate.DeepClone().AsObject();
                RemovePlayerAudit(waivedPayment["activeConflict"]!["exchangeLog"]![1]!.AsObject());
                offerSession.Dispose();
                await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                    AfterlifeSpiritualConflictState.StatePath, Encoding.UTF8.GetBytes(waivedPayment.ToJsonString()));
                await context.FileSystem.WriteFileAtomicBytesAsync(lease, ProjectionNarrativePath,
                    Encoding.UTF8.GetBytes("{\"response\":\"Рана затруднила попытку воплощения.\",\"timestamp\":\"2026-01-01T00:00:00Z\"}"));
                var missingOpen = await adapter.OpenC2PrivateSessionAsync(lease);
                Assert.Equal("dependent_continuation", missingOpen.Disposition);
                Assert.Contains(missingOpen.Issues, value => value.Code == "afterlife_conflict_wound_force_cost_audit_missing");
                using var missingSession = missingOpen.Session!;
                Assert.Null(missingSession.Offer);
                var missingResult = await missingSession.ResumeDependentContinuationAsync(lease);
                if (forceAuditWasPresentAtCapture)
                {
                    // Removing an audit preserved by the frozen baseline exceeds its correction permissions.
                    Assert.Equal("blocked", missingResult.Disposition);
                    Assert.Contains(missingResult.Issues,
                        value => value.Code == "spiritual_c2_dependent_correction_invalid");
                }
                else
                {
                    // Missing payment cannot advance the saved choice and remains correctable.
                    Assert.True(missingResult.Disposition == "dependent_continuation",
                        missingResult.Disposition + Environment.NewLine + string.Join(Environment.NewLine,
                            missingResult.Issues.Select(value => $"{value.Code}: {value}; expected={value.Expected}; actual={value.Actual}")));
                    Assert.Contains(missingResult.Issues, value => value.Code == "afterlife_conflict_wound_force_cost_audit_missing");
                }
                Assert.Equal(selectedCheckpoint, await context.FileSystem.ReadFileBytesAsync(lease,
                    SpiritualWoundCaptureCheckpointState.StatePath));
                Assert.Equal(selectedCommand, await context.FileSystem.ReadFileBytesAsync(lease,
                    AcceptedMechanicsPlan.WoundCommandPath));
                Assert.Equal(originalPending, await context.FileSystem.ReadFileBytesAsync(lease,
                    SpiritualWoundDecisionPendingState.StatePath));
                Assert.Equal(originalResources, await context.FileSystem.ReadFileBytesAsync(lease,
                    ResourceMaterializationContract.StatePath));
                missingResult.Session?.Dispose();
                missingSession.Dispose();
                if (existingSibling)
                {
                    var droppedSibling = candidate.DeepClone().AsObject();
                    droppedSibling["activeConflict"]!["exchangeLog"]![2]!.AsObject().Remove("actionCostAudit");
                    await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                        AfterlifeSpiritualConflictState.StatePath, Encoding.UTF8.GetBytes(droppedSibling.ToJsonString()));
                    var siblingOpen = await adapter.OpenC2PrivateSessionAsync(lease);
                    var siblingResult = siblingOpen;
                    if (siblingOpen.Disposition == "dependent_continuation")
                    {
                        using var siblingSession = siblingOpen.Session!;
                        Assert.Null(siblingSession.Offer);
                        siblingResult = await siblingSession.ResumeDependentContinuationAsync(lease);
                    }
                    Assert.Equal("blocked", siblingResult.Disposition);
                    Assert.Equal(selectedCheckpoint, await context.FileSystem.ReadFileBytesAsync(lease,
                        SpiritualWoundCaptureCheckpointState.StatePath));
                    siblingResult.Session?.Dispose();
                }
            }
            if (player && rejection == null)
            {
                var checkpoint = await context.FileSystem.ReadFileBytesAsync(lease,
                    SpiritualWoundCaptureCheckpointState.StatePath);
                offerSession.Dispose();
                foreach (var mutation in new[] { "constant", "extra_field", "actor", "closed_prefix" })
                {
                    var invalid = candidate.DeepClone().AsObject();
                    var invalidLog = invalid["activeConflict"]!["exchangeLog"]!;
                    var invalidSecond = invalidLog[1]!;
                    switch (mutation)
                    {
                        case "constant": invalidSecond["actionCostAudit"]!["player"]!["baseCost"] = 1; break;
                        case "extra_field": invalidSecond["actionCostAudit"]!["player"]!["woundId"] = "caller_claim"; break;
                        case "actor": invalidSecond["incomingAction"]!["actorId"] = "guardian_elsewhere"; break;
                        case "closed_prefix": invalidLog[0]!["after"]!["playerSideStrain"] = "clear"; break;
                    }
                    await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                        AfterlifeSpiritualConflictState.StatePath, Encoding.UTF8.GetBytes(invalid.ToJsonString()));
                    await context.FileSystem.WriteFileAtomicBytesAsync(lease, ProjectionNarrativePath,
                        Encoding.UTF8.GetBytes("{\"response\":\"Рана затруднила попытку воплощения.\",\"timestamp\":\"2026-01-01T00:00:00Z\"}"));
                    var invalidOpen = await adapter.OpenC2PrivateSessionAsync(lease);
                    if (invalidOpen.Disposition == "dependent_continuation")
                    {
                        using var invalidSession = invalidOpen.Session!;
                        Assert.Null(invalidSession.Offer);
                        var invalidResult = await invalidSession.ResumeDependentContinuationAsync(lease);
                        Assert.True(invalidResult.Disposition == "blocked",
                            mutation + ": " + invalidResult.Disposition + "; " + string.Join(Environment.NewLine, invalidResult.Issues));
                        invalidResult.Session?.Dispose();
                    }
                    else
                    {
                        Assert.True(invalidOpen.Disposition == "blocked", mutation + ": " + invalidOpen.Disposition);
                        invalidOpen.Session?.Dispose();
                    }
                    Assert.Equal(checkpoint, await context.FileSystem.ReadFileBytesAsync(lease,
                        SpiritualWoundCaptureCheckpointState.StatePath));
                }
            }
            await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                AfterlifeSpiritualConflictState.StatePath, Encoding.UTF8.GetBytes(candidate.ToJsonString()));
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, ProjectionNarrativePath,
                Encoding.UTF8.GetBytes("{\"response\":\"Рана затруднила попытку воплощения.\",\"timestamp\":\"2026-01-01T00:00:00Z\"}"));
            offerSession.Dispose();
            var corrected = await adapter.OpenC2PrivateSessionAsync(lease);
            if (insufficient)
                result = corrected;
            else
            {
                AssertNoConflictFrameErrors(corrected.Issues);
                Assert.Equal("dependent_continuation", corrected.Disposition);
                using var correctedSession = corrected.Session!;
                Assert.Null(correctedSession.Offer);
                if (inspectContext)
                {
                    var correctedContext = Assert.IsType<ValidationService.SpiritualC2DependentContext>(
                        await correctedSession.ReadDependentContextAsync(lease));
                    Assert.Equal(initialContext!.ContinuationId, correctedContext.ContinuationId);
                    Assert.Empty(correctedContext.CurrentIssues);
                }
                result = await correctedSession.ResumeDependentContinuationAsync(lease);
            }
        }
        if (rejection is "insufficient" or "unowned")
        {
            Assert.Equal(insufficient ? "blocked" : "dependent_continuation", result.Disposition);
            var prefix = player ? "afterlife_conflict_" : "afterlife_conflict_opposition_";
            Assert.Contains(result.Issues, issue => issue.Code == (insufficient ? prefix + "action_points_insufficient" :
                "afterlife_conflict_wound_force_cost_audit_obsolete"));
            Assert.Equal(selectedCheckpoint, await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath));
            Assert.Equal(selectedCommand, await context.FileSystem.ReadFileBytesAsync(lease,
                AcceptedMechanicsPlan.WoundCommandPath));
            Assert.Equal(originalPending, await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath));
            Assert.Equal(originalResources, await context.FileSystem.ReadFileBytesAsync(lease,
                ResourceMaterializationContract.StatePath));
            result.Session?.Dispose();
            return;
        }
        AssertNoConflictFrameErrors(result.Issues);
        Assert.Equal("completed_unpublished", result.Disposition);
        Assert.Null(JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath))!)!["checkpoint"]!["pendingSubmission"]);
        Assert.Equal(selectedCommand, await context.FileSystem.ReadFileBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath));
        using var terminal = result.Session!;
        var reduced = await terminal.ReduceCompletedDecisionsAsync(lease);
        Assert.True(reduced.Success, string.Join(Environment.NewLine, reduced.Issues));
        var ordinary = reduced.Reduction!;
        if (existingForceEvidence is not null)
        {
            var acceptedAudit = ordinary.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]
                ["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["player"]!;
            foreach (var field in new[] { "operationType", "baseCost", "minCost", "artTier", "before" })
                Assert.Equal(existingForceEvidence[field]!.ToJsonString(), acceptedAudit[field]!.ToJsonString());
        }
        if (expires)
        {
            Assert.DoesNotContain(ordinary.Resources.AppliedTransitions,
                value => value.OriginId == "exchange_source_third");
            var insertion = Assert.Single(ordinary.LiveWoundCompletion!.Insertions);
            var binding = Assert.Single(insertion.Wound.Consequences.OwnedEffectSources.RootBindings);
            Assert.DoesNotContain(ordinary.Effects!.ActiveEffects,
                value => value["effectId"]!.GetValue<string>() == binding.EffectId);
        }
        var costs = ordinary.Resources.AppliedTransitions.Where(value => value.OriginId == "exchange_source_second").ToArray();
        if (burden)
        {
            var payment = Assert.Single(costs);
            Assert.Equal(ResourceTransitionOperation.Spend, payment.Operation);
            Assert.Equal(player ? ResourceOwnerKind.AfterlifeActor : ResourceOwnerKind.AfterlifeConflictSide,
                payment.Coordinate.OwnerKind);
            Assert.Equal(1m, payment.AppliedAmount);
            Assert.Equal(3m, payment.BeforeState!.Current);
            Assert.Equal(2m, payment.AfterState!.Current);
        }
        else
            Assert.Empty(costs);
        var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(ordinary, ordinary.Resources));
        Assert.Empty(planned.Issues);
        Assert.Equal(burden && player ? 2m : 3m,
            ResolvePlannedActionPoints(planned.Plan!, ResourceOwnerKind.AfterlifeActor).Current);
        Assert.Equal(burden && !player ? 2m : 3m,
            ResolvePlannedActionPoints(planned.Plan!, ResourceOwnerKind.AfterlifeConflictSide).Current);
        terminal.Dispose();
        var reopened = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
            .OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(reopened.Issues);
        Assert.Equal("completed_unpublished", reopened.Disposition);
        using var replaySession = reopened.Session!;
        var replayed = await replaySession.ReduceCompletedDecisionsAsync(lease);
        Assert.True(replayed.Success, string.Join(Environment.NewLine, replayed.Issues));
        Assert.Equal(ordinary.Resources.HistoryAfterImage!.ToCanonicalJson(),
            replayed.Reduction!.Resources.HistoryAfterImage!.ToCanonicalJson());
        Assert.True(JsonNode.DeepEquals(
            ordinary.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath],
            replayed.Reduction.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
        var replayPlan = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                replayed.Reduction, replayed.Reduction.Resources));
        Assert.Empty(replayPlan.Issues);
        Assert.True(JsonNode.DeepEquals(planned.Plan!.StateAfterImage, replayPlan.Plan!.StateAfterImage));
        Assert.True(JsonNode.DeepEquals(planned.Plan.HistoryAfterImage, replayPlan.Plan.HistoryAfterImage));
        Assert.True(JsonNode.DeepEquals(reduced.ReceiptAfterImage, replayed.ReceiptAfterImage));

        static void RemovePlayerAudit(JsonObject exchange)
        {
            var audit = exchange["actionCostAudit"]!.AsObject();
            audit.Remove("player");
            if (audit.Count == 0)
                exchange.Remove("actionCostAudit");
        }
    }
}
