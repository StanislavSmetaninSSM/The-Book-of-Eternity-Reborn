using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Retains the actual selected wound when it removes the last binding's only leverage,
    /// and requests the bounded failed result without changing its original successful dice band.
    /// </summary>
    /// <param name="strong">
    /// Selects force binding with original dominance; its boolean setup alone must not preserve strong leverage.
    /// </param>
    /// <param name="alternative">
    /// Uses no independent leverage, ready setup, decisive dice, a different target operation, a mixed cost burden, an out-of-scope strain consequence, or an original incoming action.
    /// </param>
    /// <returns>
    /// A task completing after original admission and exact current-exchange permissions are checked.
    /// </returns>
    [Theory]
    [InlineData(false, "lost")]
    [InlineData(true, "lost")]
    [InlineData(false, "ready")]
    [InlineData(true, "ready")]
    [InlineData(true, "decisive")]
    [InlineData(false, "nonmatching")]
    [InlineData(false, "mixed")]
    [InlineData(false, "outside")]
    [InlineData(false, "blocked_incoming")]
    public Task OriginalSpiritualC2_LastBindingLosesLeverageRequestsExactFailedResult(bool strong, string alternative) =>
        VerifyOriginalLastBindingAsync(strong, alternative, continueFinalControl: false);

    /// <summary>
    /// Shares original admission and saved-choice assertions between the bounded policy and real terminal-continuation scenarios.
    /// </summary>
    /// <param name="strong">
    /// Selects force binding instead of ordinary binding.
    /// </param>
    /// <param name="alternative">
    /// Exact scenario variant defined by the public theory.
    /// </param>
    /// <param name="continueFinalControl">
    /// Continues the mixed-burden fixture through actual A and B responses when <see langword="true"/>.
    /// </param>
    /// <param name="wrapper">
    /// Uses the effective replacement carrier and an ignored duplicate prefix when <see langword="true"/>.
    /// </param>
    /// <returns>
    /// A task completing after this isolated fixture's required contract checks.
    /// </returns>
    private async Task VerifyOriginalLastBindingAsync(bool strong, string alternative, bool continueFinalControl,
        bool wrapper = false)
    {
        var operation = strong ? "force_binding" : "binding";
        var mixed = alternative == "mixed";
        var losesLeverage = alternative is "lost" or "blocked_incoming";
        var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: fixture => SeedOriginalLastBindingInputsAsync(fixture, strong, mixed),
            signedDice: [5, 15, 13, alternative == "decisive" ? 3 : strong ? 11 : 10]);
        using var owned = new OriginalFixtureCompletion(context.RootPath,
            () => context.DisposeAsync().GetAwaiter().GetResult(), _storageOutput.WriteLine);
        var originalDraft = await WriteOriginalLastBindingDraftAsync(context, strong, alternative);
        if (wrapper)
        {
            originalDraft = WrapC2DependentConflict(originalDraft);
            // Explicit first-exchange control would override the replacement's terminal carrier.
            originalDraft[AfterlifeSpiritualConflictState.ResponseField]!["exchange"]!["after"]!.AsObject().Remove("controlState");
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, originalDraft.ToJsonString());
        }
        const string scene = "Чужое давление надломило волю души.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath,
            new JsonObject { ["response"] = scene, ["timestamp"] = "2026-09-30T00:00:00Z" }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        // The original must be lawful before inserting any wound or requesting a correction.
        var ordinary = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(ordinary.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(ordinary.Session);
        Assert.Equal(2, source.CheckedExchanges.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, source.ClaimedDice);

        var preserved = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[] { "input/turn_request.json", ResourceMaterializationContract.StatePath,
                     ResourceMaterializationContract.HistoryPath, WoundIdentityState.StatePath,
                     WoundHistoryState.HistoryPath, SpiritualWoundOpportunityReceiptState.StatePath,
                     EffectIdentityState.StatePath, "game_state/meta/soul_state.json", AfterlifeEntityProfileState.StatePath })
            preserved[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);

        await CommitSpiritualContinuationDraftFirstPairAsync(context, lease);
        var offered = await context.Validator.OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(offered.Issues);
        Assert.Equal("offer", offered.Disposition);
        using var offerSession = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(offered.Session);
        var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(offerSession.Offer!.OpportunityRef,
            "spiritual_position_burden", alternative == "nonmatching" ? "guard" : operation, "player").GetRawText())!;
        decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 1;
        if (mixed)
        {
            Assert.True(offerSession.Offer.MaximumSeverityRank >= 2);
            decision["proposal"]!["severity"] = "II";
            var consequence = decision["proposal"]!["consequenceDefinitions"]![0]!;
            consequence["definition"]!["components"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundComponent(
                "spiritual_action_cost_burden", "component_002", operation, JsonValue.Create(1)));
            consequence["definition"]!["triggers"]![0]!["componentIds"]!.AsArray().Add("component_002");
            consequence["root"]!["slots"]!.AsArray().Add(new JsonObject
            { ["profileKey"] = "spiritual_action_cost_burden", ["readableSummary"] = "Оковы требуют больше духовной силы." });
        }
        var selected = await offerSession.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(decision), scene);
        using var selectedSession = selected.Session;
        var selectedCommand = await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        Assert.NotNull(selectedCommand);
        if (alternative == "nonmatching")
        {
            Assert.True(selected.Disposition == "completed_unpublished", FormatC2SubmissionIssues(selected));
            AssertNoConflictFrameErrors(selected.Issues);
            await AssertUnpublishedAsync();
            return;
        }
        Assert.True(selected.Disposition == "dependent_continuation", FormatC2SubmissionIssues(selected));
        offerSession.Dispose();
        if (alternative == "outside")
        {
            // Original admission above proved this extra strain consequence lawful, but this
            // bounded repair cannot revise it merely because the selected wound removed leverage.
            var blocked = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
                .ReadSpiritualWoundContinuationAsync(lease);
            Assert.Null(blocked.Request);
            Assert.NotEmpty(blocked.Issues);
            var saved = JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath))!)!["checkpoint"]!["pendingSubmission"];
            Assert.NotNull(saved);
            await AssertUnpublishedAsync();
            return;
        }
        var restored = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
            .OpenC2PrivateSessionAsync(lease);
        Assert.True(restored.Disposition == "dependent_continuation", FormatC2SubmissionIssues(restored));
        using var restoredSession = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(restored.Session);
        var dependent = Assert.IsType<ValidationService.SpiritualC2DependentContext>(
            await restoredSession.ReadDependentContextAsync(lease));
        var exchangePointer = wrapper ? "/afterlifeSpiritualConflictUpdate/activeConflictAfter/exchangeLog/1/" :
            "/activeConflict/exchangeLog/1/";
        var expectedFields = new List<string>
        { "diceAudit/margin", "diceAudit/modifierBreakdown", "diceAudit/playerTotal" };
        if (losesLeverage || mixed) expectedFields.AddRange(["after/controlState", "outcome"]);
        if (mixed) expectedFields.AddRange(["actionCostAudit/player/after", "actionCostAudit/player/effectiveCost"]);
        Assert.Equal(expectedFields.Select(field => exchangePointer + field).Order(StringComparer.Ordinal),
            dependent.DependentDraftFields.Select(field => field.JsonPointer));
        Assert.All(dependent.DependentDraftFields,
            field => Assert.Equal(AfterlifeSpiritualConflictState.StatePath, field.Path));
        var policy = Assert.IsType<SpiritualWoundDependentDraftPolicy>(dependent.DraftPolicy);
        Assert.True(policy.Allows(originalDraft));
        if (!losesLeverage && !mixed)
        {
            var forbiddenResult = originalDraft.DeepClone().AsObject();
            var binding = BindingRawActive(forbiddenResult, wrapper)["exchangeLog"]![1]!;
            binding["outcome"] = "blocked";
            binding["after"]!["controlState"] = binding["before"]!["controlState"]!.DeepClone();
            Assert.False(policy.Allows(forbiddenResult));
            await AssertUnpublishedAsync();
            return;
        }
        var corrected = originalDraft.DeepClone().AsObject();
        var correctedBinding = BindingRawActive(corrected, wrapper)["exchangeLog"]![1]!;
        correctedBinding["diceAudit"]!["modifierBreakdown"]!["player"] = new JsonArray();
        if (strong)
            correctedBinding["diceAudit"]!["modifierBreakdown"]!["player"]!.AsArray().Add(new JsonObject
            {
                ["modifierType"] = "conflict_position", ["source"] = "conflictPosition",
                ["position"] = "player_advantaged", ["value"] = 2
            });
        correctedBinding["diceAudit"]!["playerTotal"] = strong ? 15 : 13;
        correctedBinding["diceAudit"]!["margin"] = strong ? 4 : 3;
        correctedBinding["after"]!["controlState"] = correctedBinding["before"]!["controlState"]!.DeepClone();
        if (mixed)
        {
            correctedBinding["actionCostAudit"]!["player"]!["effectiveCost"] = 3;
            correctedBinding["actionCostAudit"]!["player"]!["after"] = 0;
        }
        foreach (var outcome in new[] { "blocked", "no_effect" })
        {
            correctedBinding["outcome"] = outcome;
            Assert.True(policy.Allows(corrected), "The bounded policy rejected " + outcome);
        }
        // Reuse the genuine owner-derived policy for cheap immutable-field checks.
        foreach (var mutation in new[] { "setup", "operation", "signed_die", "dice_source", "independent_modifier",
                     "incoming_action", "canonical_position", "cost", "closed_prefix", "future_echo", "control", "successful",
                     "actor", "strain" })
        {
            var damaged = corrected.DeepClone().AsObject();
            var damagedActive = BindingRawActive(damaged, wrapper);
            var exchange = damagedActive["exchangeLog"]![1]!;
            switch (mutation)
            {
                case "setup": exchange["setup"] = !strong; break;
                case "operation": exchange["operationType"] = "guard"; break;
                case "signed_die": exchange["diceAudit"]!["diceUsed"]![0]!["value"] = 14; break;
                case "dice_source": exchange["diceAudit"]!["diceSource"] = "caller"; break;
                case "independent_modifier":
                    exchange["diceAudit"]!["modifierBreakdown"]!["player"]!.AsArray().Add(new JsonObject
                    { ["modifierType"] = "situational", ["source"] = "unapproved", ["value"] = 0 });
                    break;
                case "incoming_action": exchange["incomingAction"] = new JsonObject(); break;
                case "canonical_position": exchange["after"]!["conflictPosition"] = "contested"; break;
                case "cost": exchange["actionCostAudit"]!["player"]!["baseCost"] = 99; break;
                case "actor": damagedActive["playerSide"]!["leadContestant"]!["actorId"] = "another_soul"; break;
                case "strain": exchange["after"]!["oppositionSideStrain"] = "strained"; break;
                case "closed_prefix":
                    damagedActive["exchangeLog"]![0]!["after"]!["playerSideStrain"] = "clear";
                    break;
                case "future_echo": damagedActive["controlState"] = new JsonObject { ["level"] = "none" }; break;
                case "control": exchange["after"]!["controlState"] = null; break;
                case "successful": exchange["outcome"] = "success"; break;
            }
            Assert.False(policy.Allows(damaged), "The bounded policy accepted " + mutation);
        }
        await AssertUnpublishedAsync();
        if (!continueFinalControl && losesLeverage)
        {
            restoredSession.Dispose();
            var issued = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
            Assert.Equal("dependent_draft", issued.Disposition);
            Assert.NotEmpty(issued.Issues);
            var a = Assert.IsType<SpiritualWoundContinuationRequest>(issued.Request);
            Assert.Equal(dependent.DependentDraftFields.Select(field => field.JsonPointer),
                a.DependentDraftFields.Select(field => field.JsonPointer));
            var responseA = CreateSpiritualStagedEmptyResponse(a);
            var checkpointBefore = await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath);
            var pendingBefore = await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath);
            Assert.NotNull(checkpointBefore);

            // The incoming action belongs to the original admission, never to this correction.
            correctedBinding["outcome"] = "blocked";
            Assert.True(policy.Allows(corrected));
            var originalBinding = BindingRawActive(originalDraft, wrapper)["exchangeLog"]![1]!;
            Assert.True(JsonNode.DeepEquals(originalBinding["incomingAction"], correctedBinding["incomingAction"]));
            Assert.Equal(alternative == "blocked_incoming", correctedBinding["incomingAction"] is JsonObject);
            Assert.True(JsonNode.DeepEquals(BindingRawActive(originalDraft, wrapper)["controlState"],
                BindingRawActive(corrected, wrapper)["controlState"]));
            Assert.False(JsonNode.DeepEquals(correctedBinding["after"]!["controlState"],
                BindingRawActive(corrected, wrapper)["controlState"]));
            var submitted = Encoding.UTF8.GetBytes(corrected.ToJsonString());
            var beforeSubmission = await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath);
            Assert.NotNull(beforeSubmission);
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath, submitted);
            var evaluated = await context.Validator.EvaluateSpiritualWoundContinuationDraftAsync(lease, a, responseA);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { root = context.RootPath, strong, alternative,
                continuationId = a.ContinuationId, disposition = evaluated.Disposition.ToString(), evaluated.Issues }));
            if (alternative == "blocked_incoming")
            {
                Assert.Equal(ValidationService.SpiritualWoundContinuationDisposition.Advanced, evaluated.Disposition);
                // Advanced carries the deliberately held B diagnostic; it is not a resolved turn.
                var terminal = Assert.Single(evaluated.Issues);
                Assert.Equal("afterlife_conflict_control_snapshot_missing", terminal.Code);
                Assert.Equal("activeConflict.controlState", terminal.FilePath);
            }
            else
            {
                Assert.Equal(ValidationService.SpiritualWoundContinuationDisposition.Rejected, evaluated.Disposition);
                var error = Assert.Single(evaluated.Issues,
                    issue => issue.Code == "afterlife_conflict_blocked_missing_incoming_action");
                Assert.Equal(IssueSeverity.Error, error.Severity);
                Assert.Equal("activeConflict.exchangeLog[1].incomingAction", error.FilePath);
            }
            Assert.Null(evaluated.NextRequest);
            Assert.Equal(submitted, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
            await AssertPrivateAndCanonicalBytesAsync();
            if (alternative == "lost")
            {
                // A rejected physical proposal cannot furnish a continuation until its exact input is restored.
                var blockedRead = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { strong, alternative,
                    readerPhase = "rejected_physical_draft", blockedRead.Disposition, blockedRead.Issues }));
                Assert.Equal("blocked", blockedRead.Disposition);
                Assert.Null(blockedRead.Request);
                Assert.Equal(submitted, await context.FileSystem.ReadFileBytesAsync(lease,
                    AfterlifeSpiritualConflictState.StatePath));
                await AssertPrivateAndCanonicalBytesAsync();
                await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
                    beforeSubmission);
            }
            var stillIssued = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { strong, alternative,
                readerPhase = alternative == "lost" ? "exact_draft_restored" : "advanced_uncommitted_draft",
                stillIssued.Disposition, stillIssued.Issues }));
            Assert.Equal("dependent_draft", stillIssued.Disposition);
            Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(stillIssued.Request));
            Assert.Equal(alternative == "lost" ? beforeSubmission : submitted,
                await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
            await AssertPrivateAndCanonicalBytesAsync();
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { strong, alternative,
                NoncommittingBytesPreserved = true, SameIssuedA = true, NextRequestAbsent = true,
                ExactPreSubmissionDraftRestored = alternative == "lost" }));

            async Task AssertPrivateAndCanonicalBytesAsync()
            {
                Assert.Equal(checkpointBefore, await context.FileSystem.ReadFileBytesAsync(lease,
                    SpiritualWoundCaptureCheckpointState.StatePath));
                Assert.Equal(pendingBefore, await context.FileSystem.ReadFileBytesAsync(lease,
                    SpiritualWoundDecisionPendingState.StatePath));
                Assert.Equal(selectedCommand, await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                foreach (var image in preserved)
                    Assert.Equal(image.Value, await context.FileSystem.ReadFileBytesAsync(lease, image.Key));
            }
        }
        if (continueFinalControl)
        {
            restoredSession.Dispose();
            await VerifyBindingFinalControlAsync(context, lease, corrected, selectedCommand, preserved, wrapper, mixed);
        }

        async Task AssertUnpublishedAsync()
        {
            Assert.True(JsonNode.DeepEquals(originalDraft,
                JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath))!)));
            foreach (var image in preserved)
                Assert.Equal(image.Value, await context.FileSystem.ReadFileBytesAsync(lease, image.Key));
            Assert.Equal(selectedCommand,
                await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        }
    }

    /// <summary>
    /// Reads the fixture's selected direct or replacement conflict without merging ignored siblings.
    /// </summary>
    /// <param name="raw">
    /// Fixture draft in its original raw shape.
    /// </param>
    /// <param name="wrapper">
    /// Whether the replacement inside the exchange update is selected.
    /// </param>
    /// <returns>
    /// The mutable raw conflict carrier owned by this isolated draft.
    /// </returns>
    private static JsonObject BindingRawActive(JsonObject raw, bool wrapper) =>
        (wrapper ? raw[AfterlifeSpiritualConflictState.ResponseField]!["activeConflictAfter"] : raw["activeConflict"])!.AsObject();

    /// <summary>
    /// Seeds the original position, absent control and binding art before the fixture signs its snapshot.
    /// </summary>
    /// <param name="context">
    /// Unsigned isolated fixture whose canonical item and location baselines are also required by intake.
    /// </param>
    /// <param name="strong">
    /// Selects original dominance, force-binding art and the opposing pressure tier needed for a real rank-I wound.
    /// </param>
    /// <param name="mixed">
    /// Uses binding tier two so a later action-cost burden increases its effective payment from two to three.
    /// </param>
    /// <param name="seedIntake">
    /// Seeds minimal intake files unless a full lifecycle scaffold already supplies them.
    /// </param>
    /// <returns>
    /// A task completing after the original soul and player-profile art authorities agree.
    /// </returns>
    internal static async Task SeedOriginalLastBindingInputsAsync(ResourceMaterializationTestContext context, bool strong, bool mixed,
        bool seedIntake = true)
    {
        if (seedIntake) await SeedOriginalIntakeBaselinesAsync(context);
        var operation = strong ? "force_binding" : "binding";
        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync("game_state/meta/soul_state.json"));
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]![operation] = mixed ? 2 : 3;
        await context.WriteExactJsonAsync("game_state/meta/soul_state.json", soul.ToJsonString());
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        var player = profiles["profiles"]!.AsArray().Single(row => row!["actorId"]!.GetValue<string>() == "player_soul")!;
        player["standardArts"]![operation] = mixed ? 2 : 3;
        if (strong)
        {
            var guardian = profiles["profiles"]!.AsArray().Single(row => row!["actorId"]!.GetValue<string>() != "player_soul")!;
            guardian["standardArts"]!["pressure"] = 1;
        }
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        conflict["activeConflict"]!["conflictPosition"] = strong ? "player_dominant" : "player_advantaged";
        if (strong)
            conflict["activeConflict"]!["oppositionSide"]!["leadContestant"]!["actorArtTierSnapshot"]!["pressure"] = 1;
        conflict["activeConflict"]!["controlState"] = new JsonObject { ["level"] = "none" };
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflict.ToJsonString());
    }

    /// <summary>
    /// Authors harmful pressure followed by a last binding whose only consequence is gaining control.
    /// </summary>
    /// <param name="context">
    /// Signed original fixture whose position, art authorities and dice agree with the selected binding family.
    /// </param>
    /// <param name="strong">
    /// Selects the force-binding case at position plus two with later dice 13 and 11.
    /// </param>
    /// <param name="alternative">
    /// Selects ready setup, decisive leverage, a mixed wound, or an ordinary-valid extra strain consequence outside bounded result repair.
    /// </param>
    /// <returns>
    /// The exact raw draft retained as the unchanged baseline for the selected wound.
    /// </returns>
    internal static async Task<JsonObject> WriteOriginalLastBindingDraftAsync(ResourceMaterializationTestContext context,
        bool strong, string alternative)
    {
        var operation = strong ? "force_binding" : "binding";
        var position = strong ? "player_dominant" : "player_advantaged";
        var mixed = alternative == "mixed";
        await WriteCompleteConflictFrameExchangeAsync(context);
        var draft = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = draft["activeConflict"]!;
        var pressure = active["exchangeLog"]![0]!;
        pressure["outcome"] = "setback";
        foreach (var state in new[] { "before", "after" })
        {
            pressure[state]!["conflictPosition"] = position;
            pressure[state]!["controlState"] = new JsonObject { ["level"] = "none" };
        }
        pressure["after"]!["playerSideStrain"] = mixed ? "fractured" : "strained";
        pressure["after"]!["oppositionSideStrain"] = "clear";
        var pressureDice = pressure["diceAudit"]!;
        pressureDice["diceUsed"]![0]!["value"] = 5;
        pressureDice["diceUsed"]![1]!["value"] = 15;
        pressureDice["modifierBreakdown"]!["player"]!.AsArray().Add(new JsonObject
        {
            ["modifierType"] = "conflict_position", ["source"] = "conflictPosition",
            ["position"] = position, ["value"] = strong ? 4 : 2
        });
        pressureDice["playerTotal"] = strong ? 9 : 7;
        pressureDice["oppositionTotal"] = 15;
        pressureDice["margin"] = strong ? -6 : -8;
        pressureDice["outcomeBand"] = strong ? "opposition_success" : "decisive_opposition_success";
        if (strong)
        {
            var cost = pressure["actionCostAudit"]!["opposition"]!;
            cost["artTier"] = 1;
            cost["effectiveCost"] = 2;
            cost["after"] = 4;
        }
        active["playerSideStrain"] = mixed ? "fractured" : "strained";
        active["oppositionSideStrain"] = "clear";
        active["conflictPosition"] = position;
        active["controlState"] = pressure["after"]!["controlState"]!.DeepClone();

        var binding = pressure.DeepClone();
        binding["exchangeId"] = "last_binding_result";
        binding["operationType"] = operation;
        if (alternative == "blocked_incoming")
            binding["incomingAction"] = new JsonObject
            {
                ["operationType"] = "pressure", ["summary"] = "Хранитель отвечает духовным давлением."
            };
        if (strong) binding["setup"] = true;
        if (alternative == "ready") binding["bindingSetup"] = "ready";
        binding["outcome"] = "success";
        binding["before"] = pressure["after"]!.DeepClone();
        binding["after"] = binding["before"]!.DeepClone();
        binding["after"]!["controlState"] = new JsonObject
        {
            ["level"] = "hindered", ["controllerSide"] = "player", ["controlId"] = "last_binding_control",
            ["sourceOperation"] = operation, ["restrictedOperations"] = new JsonArray("maneuver", "binding"),
            ["summary"] = "Позиционное преимущество позволяет душе наложить оковы."
        };
        if (alternative == "outside")
        {
            binding["after"]!["oppositionSideStrain"] = "strained";
            active["oppositionSideStrain"] = "strained";
        }
        binding["matchupAudit"]!["playerOperation"] = operation;
        binding["matchupAudit"]!["primaryResolutionLane"] = operation;
        binding["matchupAudit"]!["riskProfile"] = "control_leverage";
        binding["matchupAudit"]!["matchupRationale"] = "Душа использует преимущество для оков, хранитель отвечает давлением.";
        var playerCost = binding["actionCostAudit"]!["player"]!;
        playerCost["operationType"] = operation;
        playerCost["baseCost"] = strong ? 5 : 4;
        playerCost["minCost"] = 2;
        playerCost["artTier"] = mixed ? 2 : 3;
        playerCost["effectiveCost"] = 2;
        playerCost["before"] = 3;
        playerCost["after"] = 1;
        binding["actionCostAudit"]!["opposition"]!["before"] = strong ? 4 : 3;
        binding["actionCostAudit"]!["opposition"]!["after"] = strong ? 2 : 0;
        var dice = binding["diceAudit"]!;
        dice["diceUsed"]![0]!["sourceIndex"] = 2;
        dice["diceUsed"]![0]!["value"] = 13;
        dice["diceUsed"]![1]!["sourceIndex"] = 3;
        var opposingDie = alternative == "decisive" ? 3 : strong ? 11 : 10;
        dice["diceUsed"]![1]!["value"] = opposingDie;
        dice["playerTotal"] = strong ? 17 : 15;
        dice["oppositionTotal"] = opposingDie;
        dice["margin"] = (strong ? 17 : 15) - opposingDie;
        dice["outcomeBand"] = alternative == "decisive" ? "decisive_player_success" : "player_success";
        active["exchangeLog"]!.AsArray().Add(binding);
        active["controlState"] = binding["after"]!["controlState"]!.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, draft.ToJsonString());
        return draft;
    }
}
