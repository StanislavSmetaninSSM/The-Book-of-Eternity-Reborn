using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Publishes a first-turn wound and its signed receipt for a later-turn guarantee control.
    /// </summary>
    /// <param name="context">
    /// Isolated canonical fixture that will retain the published first-turn roots.
    /// </param>
    /// <returns>
    /// The first-turn receipt whose wound identity must survive the next turn.
    /// </returns>
    private static async Task<JsonObject> PublishFirstWoundForGuaranteeAsync(
        ResourceMaterializationTestContext context)
    {
        await WriteCompleteConflictFrameExchangeAsync(context);
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Первый обмен завершён.\"}"));
        JsonObject firstReceipt;
        AcceptedMechanicsPlan firstPlan;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            _ = context.FileSystem.GetOrCreateSessionGeneration(lease);
            var validator = new ValidationService(context.FileSystem,
                NullLogger<ValidationService>.Instance);
            var captured = await validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
            AssertNoConflictFrameErrors(captured.Issues);
            using (var warm = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture))
            {
                AssertNoConflictFrameErrors(await warm.BeginResourceExecutionAsync(lease));
                var step = await warm.AdvanceNextResourceExchangeAsync(lease);
                AssertNoConflictFrameErrors(step.Issues);
                var committed = await warm.CommitC2FirstTransportAsync(lease, step.Step!.Interval!);
                Assert.Equal("committed", committed.Disposition);
            }
            var opened = await validator.OpenC2PrivateSessionAsync(lease);
            Assert.Equal("offer", opened.Disposition);
            using var offer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            var completed = await offer.SubmitDecisionAsync(lease,
                OriginalSpiritualWoundDecision(offer.Offer!.OpportunityRef,
                    "spiritual_action_cost_burden", "guard"),
                "Чужое давление надломило волю хранителя.");
            Assert.Equal("completed_unpublished", completed.Disposition);
            using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
            var reduced = await terminal.ReduceCompletedDecisionsAsync(lease);
            Assert.True(reduced.Success, string.Join(Environment.NewLine,
                reduced.Issues.Select(issue => $"{issue.Code}: {issue}")));
            firstReceipt = reduced.ReceiptAfterImage!.DeepClone().AsObject();
            var ordinary = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
                reduced.Reduction);
            var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
                AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                    ordinary, ordinary.Resources));
            Assert.Empty(planned.Issues);
            firstPlan = Assert.IsType<AcceptedMechanicsPlan>(planned.Plan);
        }
        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.DefinitionPath,
            firstPlan.DefinitionAfterImage.ToJsonString());
        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.StatePath,
            firstPlan.StateAfterImage.ToJsonString());
        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.HistoryPath,
            firstPlan.HistoryAfterImage.ToJsonString());
        await context.WriteExactJsonAsync(EffectAcceptedTurnPlan.IdentityIndexPath,
            firstPlan.EffectIdentityAfterImage.ToJsonString());
        foreach (var (path, root) in firstPlan.EffectCarrierAfterImages)
            await context.WriteExactJsonAsync(path, root.ToJsonString());
        foreach (var (path, root) in firstPlan.OwnerCompanionAfterImages)
            await context.WriteExactJsonAsync(path, root.ToJsonString());
        foreach (var (path, root) in firstPlan.WoundCarrierAfterImages)
            await context.WriteExactJsonAsync(path, root.ToJsonString());
        await context.WriteExactJsonAsync(WoundIdentityState.StatePath,
            firstPlan.WoundIdentityAfterImage!.ToJsonString());
        await context.WriteExactJsonAsync(WoundHistoryState.HistoryPath,
            firstPlan.WoundHistoryAfterImage!.ToJsonString());
        await context.WriteExactJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath,
            firstReceipt.ToJsonString());
        foreach (var (path, root) in firstPlan.PendingAfterImages)
        {
            if (root is null)
                await context.DeleteAsync(path);
            else
                await context.WriteExactJsonAsync(path, root.ToJsonString());
        }
        foreach (var path in firstPlan.ConsumedPaths)
            await context.DeleteAsync(path);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            firstPlan.DefinitionAfterImage.ToJsonString(), allowMissingPristine: false);
        Assert.True(definitions.IsValid);
        var state = ResourceStateContract.ParseCanonical(
            firstPlan.StateAfterImage.ToJsonString(), definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(state.IsValid);
        var history = ResourceHistoryState.ParseCanonical(
            firstPlan.HistoryAfterImage.ToJsonString(), definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(history.IsValid);
        await WriteComposedAuthorityAsync(context, definitions.Catalog!, state.Ledger!,
            history.History!);
        await context.DeleteAsync(SpiritualWoundCaptureCheckpointState.StatePath);
        await context.DeleteAsync(SpiritualWoundDecisionPendingState.StatePath);
        return firstReceipt;
    }

    /// <summary>
    /// Requires the authenticated receipt when an older wound satisfies a zero-ceiling source.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC3_NextTurnSatisfiedGuaranteeRequiresReceiptWithoutInsertion()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        var firstReceipt = await PublishFirstWoundForGuaranteeAsync(context);
        var woundId = firstReceipt["decisions"]![0]!["woundId"]!.GetValue<string>();
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeEntityProfileState.StatePath));
        var art = SourceOwnerSpecialArt("player_soul", "player_soul");
        art["tier"] = 5;
        art["spiritualWoundEnvelope"] = new JsonObject
        {
            ["schemaVersion"] = 1, ["maximumSeverityRank"] = 1,
            ["guaranteedSeverityRank"] = 1
        };
        profiles["profiles"]![0]!["specialArts"]!.AsArray().Add(art);
        profiles["profiles"]![1]!["standardArts"]!["spiritual_resilience"] = 5;
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath,
            profiles.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43,
            currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8],
            sessionId: "session_resource_materialization_43",
            requestId: "request_resource_materialization_43");
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var candidate = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeSpiritualConflictState.StatePath));
        var second = candidate["activeConflict"]!["exchangeLog"]![1]!.AsObject();
        second["turnNumber"] = 43;
        second["specialArtAudit"] = new JsonObject
        {
            ["artId"] = "art_source_owner", ["ownerActorType"] = "player_soul",
            ["ownerActorId"] = "player_soul", ["baseOperation"] = "pressure",
            ["costMultiplierPercent"] = 200,
            ["effectNote"] = "Искусство оставляет след в духовном узоре."
        };
        var cost = second["actionCostAudit"]!["player"]!.AsObject();
        cost["after"] = 1;
        cost["artTier"] = 5;
        cost["effectiveCost"] = 2;
        cost["specialArtId"] = "art_source_owner";
        cost["specialCostMultiplierPercent"] = 200;
        cost["standardEffectiveCost"] = 1;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath,
            candidate.ToJsonString());
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Второй обмен завершён.\"}"));

        var validator = new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using (var owner = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture))
        {
            AssertNoConflictFrameErrors(await owner.BeginResourceExecutionAsync(lease));
            var step = await owner.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(step.Issues);
            var source = Assert.Single(Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
                OriginalCaptureField(owner, "_source")).Sources);
            Assert.Equal(0, source.Calculation.MaximumSeverityRank);
            Assert.Equal(1, source.GuaranteedSeverityRank);
            var admitted = await owner.AdmitWoundSourceAsync(lease, step.Step!.Interval!, source);
            AssertNoConflictFrameErrors(admitted.Issues);
            var fulfilled = await owner.ReadWoundOpportunityAsync(lease, admitted.Admission!);
            Assert.Empty(fulfilled.Issues);
            Assert.Null(fulfilled.Opportunity);
            Assert.Equal(woundId, fulfilled.Satisfaction?.WoundId);
            var illegalChoice = await owner.MaterializeWoundAsync(lease, admitted.Admission!,
                JsonSerializer.SerializeToElement(new { decision = "none" }), null);
            Assert.Contains(illegalChoice.Issues,
                issue => issue.Code == "spiritual_wound_client_owned_satisfaction");
            var committed = await owner.CommitC2FirstTransportAsync(lease, step.Step.Interval!);
            Assert.Equal("committed", committed.Disposition);
        }
        var opened = await validator.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("completed_unpublished", opened.Disposition);
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var terminalCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(
            OriginalCaptureField(terminal, "_capture"));
        var reduced = await terminal.ReduceCompletedDecisionsAsync(lease);
        Assert.True(reduced.Success, string.Join(Environment.NewLine,
            reduced.Issues.Select(issue => $"{issue.Code}: {issue}")));
        var ordinary = await terminalCapture.CompleteOrdinaryReductionAsync(lease);
        Assert.True(ordinary.Success, string.Join(Environment.NewLine,
            ordinary.Issues.Select(issue => $"{issue.Code}: {issue}")));
        Assert.True(ordinary.Reduction!.RequiresSatisfactionReceipt);
        Assert.Null(ordinary.Reduction.LiveWoundCompletion);
        var premature = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                ordinary.Reduction, ordinary.Reduction.Resources));
        Assert.Null(premature.Plan);
        Assert.Contains(premature.Issues,
            issue => issue.Code == "spiritual_live_wound_receipt_join_required");
        var completedReduction = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
            reduced.Reduction);
        Assert.True(completedReduction.RequiresSatisfactionReceipt);
        Assert.Null(completedReduction.LiveWoundCompletion);
        Assert.True(completedReduction.HasLiveDecisionReceipt);
        var rows = reduced.ReceiptAfterImage!["decisions"]!.AsArray();
        Assert.Equal("guarantee_satisfied", rows[1]!["decision"]!.GetValue<string>());
        Assert.Equal(woundId, rows[1]!["woundId"]!.GetValue<string>());
        Assert.Equal(1, rows[1]!["satisfiedSeverityRank"]!.GetValue<int>());
        var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                completedReduction, completedReduction.Resources));
        Assert.Empty(planned.Issues);
        Assert.NotNull(planned.Plan);

        terminal.Dispose();
        var reopened = await validator.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("completed_unpublished", reopened.Disposition);
        using var foreignTerminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(
            reopened.Session);
        var foreignReduced = await foreignTerminal.ReduceCompletedDecisionsAsync(lease);
        Assert.True(foreignReduced.Success, string.Join(Environment.NewLine,
            foreignReduced.Issues.Select(issue => $"{issue.Code}: {issue}")));
        Assert.True(JsonNode.DeepEquals(reduced.ReceiptAfterImage,
            foreignReduced.ReceiptAfterImage));
        var foreignCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(
            OriginalCaptureField(foreignTerminal, "_capture"));
        var foreignOrdinary = await foreignCapture.CompleteOrdinaryReductionAsync(lease);
        Assert.True(foreignOrdinary.Success);
        var foreignSigned = foreignCapture.ReadVerifiedSignedC1Origin(lease);
        Assert.Throws<InvalidOperationException>(() =>
            foreignOrdinary.Reduction!.WithOwnerCompanionAfterImage(
                SpiritualWoundOpportunityReceiptState.StatePath, foreignSigned.Receipt,
                reduced.ReceiptAfterImage!, reduced.ReceiptProof));
        foreignTerminal.Dispose();

        var replayValidator = new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance);
        var baselineReplay = await replayValidator.ReplaySavedSpiritualCheckpointAsync(lease);
        Assert.Empty(baselineReplay.Issues);
        Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(baselineReplay.Capture).Dispose();

        async Task RejectChangedSavedOriginAsync(string path, Action<JsonObject> change)
        {
            var original = await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath);
            Assert.NotNull(original);
            var checkpoint = JsonNode.Parse(original)!.AsObject();
            var body = checkpoint["checkpoint"]!.AsObject();
            var row = body["originalDraftImages"]!.AsArray().Single(image =>
                image!["path"]!.GetValue<string>() == path)!.AsObject();
            var altered = JsonNode.Parse(Convert.FromBase64String(
                row["contentBase64"]!.GetValue<string>()))!.AsObject();
            change(altered);
            var alteredBytes = Encoding.UTF8.GetBytes(altered.ToJsonString());
            row["contentBase64"] = Convert.ToBase64String(alteredBytes);
            row["contentFingerprint"] = new CanonicalBeforeImage(true, alteredBytes).Fingerprint;
            body["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(body,
                "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
            var inventory = body["originalDraftImages"]!.AsArray().Select(image =>
                image!["path"]!.GetValue<string>()).ToArray();
            var structurallyValid = SpiritualWoundCaptureCheckpointState.Parse(
                checkpoint.ToJsonString(), SpiritualWoundCaptureCheckpointState.StatePath,
                inventory);
            Assert.True(structurallyValid.IsValid, string.Join(Environment.NewLine,
                structurallyValid.Issues.Select(issue => $"{issue.Code}: {issue}")));
            await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath,
                Encoding.UTF8.GetBytes(checkpoint.ToJsonString()));
            try
            {
                var replay = await new ValidationService(context.FileSystem,
                    NullLogger<ValidationService>.Instance)
                    .ReplaySavedSpiritualCheckpointAsync(lease);
                Assert.Null(replay.Capture);
                Assert.NotEmpty(replay.Issues);
                Assert.DoesNotContain(replay.Issues,
                    issue => issue.Code == "spiritual_checkpoint_first_replay_invalid");
            }
            finally
            {
                await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                    SpiritualWoundCaptureCheckpointState.StatePath, original);
            }
        }

        await RejectChangedSavedOriginAsync(AfterlifeSpiritualConflictState.StatePath, root =>
            root["activeConflict"]!["exchangeLog"]![1]!["specialArtAudit"]!["effectNote"] =
                "Подменённый источник.");
        await RejectChangedSavedOriginAsync(AfterlifeSpiritualConflictState.StatePath, root =>
            root["activeConflict"]!["oppositionSide"]!["leadContestant"]!["actorId"] =
                "foreign_guardian");
        await RejectChangedSavedOriginAsync(AfterlifeSpiritualConflictState.StatePath, root =>
            root["activeConflict"]!["conflictId"] = "foreign_conflict");
        await RejectChangedSavedOriginAsync(AfterlifeEntityProfileState.StatePath, root =>
        {
            var wound = root["profiles"]![1]!["activeWounds"]!.AsArray().Single()!.AsObject();
            Assert.Equal(woundId, wound["woundId"]!.GetValue<string>());
            wound["woundId"] = "foreign_wound";
        });
        await RejectChangedSavedOriginAsync(AfterlifeEntityProfileState.StatePath, root =>
        {
            var wound = root["profiles"]![1]!["activeWounds"]!.AsArray().Single()!.AsObject();
            wound["severity"]!["rank"] = 2;
            wound["severity"]!["value"] = "II";
        });
    }

    /// <summary>
    /// Derives a fulfilled guarantee from an equal or stronger existing conflict wound.
    /// </summary>
    /// <param name="firstRank">
    /// Rank of the wound created by the first exchange.
    /// </param>
    /// <param name="firstStrain">
    /// Destination strain that bounds the first wound.
    /// </param>
    /// <param name="secondStrain">
    /// Later destination strain that makes the guaranteed source harmful.
    /// </param>
    [Theory]
    [InlineData(1, "strained", "fractured")]
    [InlineData(2, "fractured", "broken")]
    public async Task OriginalSpiritualC2_LaterSatisfiedGuaranteeAdvancesAutomatically(
        int firstRank, string firstStrain, string secondStrain)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync,
            signedDice: [15, 5, 15, 5]);
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        var art = SourceOwnerSpecialArt("player_soul", "player_soul");
        art["tier"] = 5;
        art["spiritualWoundEnvelope"] = new JsonObject
        {
            ["schemaVersion"] = 1, ["maximumSeverityRank"] = 1,
            ["guaranteedSeverityRank"] = 1
        };
        profiles["profiles"]![0]!["specialArts"]!.AsArray().Add(art);
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42,
            currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 15, 5]);
        await CommitInitialC2PairAsync(context, conflict =>
        {
            var active = conflict["activeConflict"]!.AsObject();
            var first = active["exchangeLog"]![0]!.AsObject();
            first["after"]!["oppositionSideStrain"] = firstStrain;
            var second = first.DeepClone().AsObject();
            second["exchangeId"] = "exchange_guarantee_satisfied";
            second["before"] = first["after"]!.DeepClone();
            second["after"] = second["before"]!.DeepClone();
            second["after"]!["oppositionSideStrain"] = secondStrain;
            second["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 2;
            second["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 3;
            second["specialArtAudit"] = new JsonObject
            {
                ["artId"] = "art_source_owner", ["ownerActorType"] = "player_soul",
                ["ownerActorId"] = "player_soul", ["baseOperation"] = "pressure",
                ["costMultiplierPercent"] = 200,
                ["effectNote"] = "Искусство оставляет след в духовном узоре."
            };
            var playerCost = second["actionCostAudit"]!["player"]!.AsObject();
            playerCost["before"] = 3;
            playerCost["after"] = 1;
            playerCost["artTier"] = 5;
            playerCost["effectiveCost"] = 2;
            playerCost["specialArtId"] = "art_source_owner";
            playerCost["specialCostMultiplierPercent"] = 200;
            playerCost["standardEffectiveCost"] = 1;
            second["actionCostAudit"]!["opposition"]!["before"] = 3;
            second["actionCostAudit"]!["opposition"]!["after"] = 0;
            active["exchangeLog"]!.AsArray().Add(second);
            active["oppositionSideStrain"] = secondStrain;
        });
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var validator = new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance);
        var opened = await validator.OpenC2PrivateSessionAsync(lease);
        Assert.True(opened.Disposition == "offer", opened.Disposition + ": " +
            string.Join(Environment.NewLine,
                opened.Issues.Select(issue => $"{issue.Code}: {issue}")));
        using var first = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        Assert.Null(first.Offer!.RequiredSeverityRank);
        var firstDecision = OriginalSpiritualWoundDecision(first.Offer.OpportunityRef,
            "spiritual_action_cost_burden", "guard");
        if (firstRank == 2)
        {
            var draft = JsonNode.Parse(firstDecision.GetRawText())!.AsObject();
            draft["proposal"]!["severity"] = "II";
            var definitions = draft["proposal"]!["consequenceDefinitions"]!.AsArray();
            var extra = definitions[0]!.DeepClone();
            extra["definitionRef"] = "maneuver_burden";
            extra["definition"]!["definitionKey"] = "maneuver_burden";
            extra["definition"]!["stacking"]!["stackKey"] = "stack_maneuver_burden";
            extra["definition"]!["components"]![0]!["payload"]!["operation"] = "maneuver";
            definitions.Add(extra);
            firstDecision = JsonSerializer.SerializeToElement(draft);
        }
        var completed = await first.SubmitDecisionAsync(lease,
            firstDecision,
            "Чужое давление надломило волю хранителя.");
        Assert.True(completed.Disposition == "completed_unpublished",
            completed.Disposition + ": " + string.Join(Environment.NewLine,
                completed.Issues.Select(issue => $"{issue.Code}: {issue}")));
        var checkpoint = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            SpiritualWoundCaptureCheckpointState.StatePath));
        var advances = checkpoint["checkpoint"]!["advances"]!.AsArray();
        Assert.Equal(2, advances.Count);
        Assert.Empty(advances[1]!["inputChanges"]!.AsArray());
        var firstCommand = Assert.Single(advances[0]!["inputChanges"]!.AsArray(),
            change => change!["path"]!.GetValue<string>() == AcceptedMechanicsPlan.WoundCommandPath);
        Assert.Equal(Convert.FromBase64String(firstCommand!["contentBase64"]!.GetValue<string>()),
            await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
        var reduced = await terminal.ReduceCompletedDecisionsAsync(lease);
        Assert.True(reduced.Success, string.Join(Environment.NewLine,
            reduced.Issues.Select(issue => $"{issue.Code}: {issue}")));
        var rows = reduced.ReceiptAfterImage!["decisions"]!.AsArray();
        Assert.Equal(2, rows.Count);
        Assert.Equal("guarantee_satisfied", rows[1]!["decision"]!.GetValue<string>());
        Assert.Equal(rows[0]!["woundId"]!.GetValue<string>(),
            rows[1]!["woundId"]!.GetValue<string>());
        Assert.Equal(1, rows[1]!["sourceWitness"]!["guaranteedSeverityRank"]!.GetValue<int>());
        Assert.Equal(1, rows[1]!["sourceWitness"]!["maximumSeverityRank"]!.GetValue<int>());
        Assert.Equal(firstRank, rows[0]!["selectedSeverityRank"]!.GetValue<int>());
        Assert.Equal(firstRank, rows[1]!["satisfiedSeverityRank"]!.GetValue<int>());
        Assert.Null(rows[1]!["selectedSeverityRank"]);
        Assert.Null(rows[1]!["transitionId"]);
        var ordinary = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
            reduced.Reduction);
        var insertion = Assert.Single(Assert.IsType<SpiritualLiveWoundCompletion>(
            ordinary.LiveWoundCompletion).Insertions);
        Assert.Equal(firstRank, insertion.Wound.Severity.Rank);
    }

    /// <summary>
    /// Keeps the satisfaction receipt at its historical rank when a later source worsens the wound.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2_SatisfiedGuaranteeRetainsRankAfterLaterWorsening()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync,
            signedDice: [15, 5, 15, 5, 18, 3]);
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeEntityProfileState.StatePath));
        foreach (var profile in profiles["profiles"]!.AsArray())
            profile!["standardArts"]!["pressure"] = 2;
        var art = SourceOwnerSpecialArt("player_soul", "player_soul");
        art["tier"] = 2;
        art["spiritualWoundEnvelope"] = new JsonObject
        {
            ["schemaVersion"] = 1, ["maximumSeverityRank"] = 1,
            ["guaranteedSeverityRank"] = 1
        };
        profiles["profiles"]![0]!["specialArts"]!.AsArray().Add(art);
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath,
            profiles.ToJsonString());
        const string soulPath = "game_state/meta/soul_state.json";
        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(soulPath));
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]!["pressure"] = 2;
        await context.WriteExactJsonAsync(soulPath, soul.ToJsonString());
        var originalConflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            AfterlifeSpiritualConflictState.StatePath));
        originalConflict["activeConflict"]!["oppositionSide"]!["leadContestant"]!
            ["actorArtTierSnapshot"]!["pressure"] = 2;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath,
            originalConflict.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42,
            currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 15, 5, 18, 3]);
        await CommitInitialC2PairAsync(context, conflict =>
        {
            var active = conflict["activeConflict"]!.AsObject();
            var first = active["exchangeLog"]![0]!.AsObject();
            foreach (var side in new[] { "player", "opposition" })
            {
                var cost = first["actionCostAudit"]![side]!;
                cost["artTier"] = 2;
                cost["effectiveCost"] = 1;
                cost["after"] = 5;
            }
            var second = first.DeepClone().AsObject();
            second["exchangeId"] = "exchange_guarantee_satisfied";
            second["before"] = first["after"]!.DeepClone();
            second["after"] = second["before"]!.DeepClone();
            second["after"]!["oppositionSideStrain"] = "fractured";
            second["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 2;
            second["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 3;
            second["specialArtAudit"] = new JsonObject
            {
                ["artId"] = "art_source_owner", ["ownerActorType"] = "player_soul",
                ["ownerActorId"] = "player_soul", ["baseOperation"] = "pressure",
                ["costMultiplierPercent"] = 200,
                ["effectNote"] = "Искусство оставляет след в духовном узоре."
            };
            var secondPlayerCost = second["actionCostAudit"]!["player"]!;
            secondPlayerCost["before"] = 5;
            secondPlayerCost["after"] = 3;
            secondPlayerCost["effectiveCost"] = 2;
            secondPlayerCost["specialArtId"] = "art_source_owner";
            secondPlayerCost["specialCostMultiplierPercent"] = 200;
            secondPlayerCost["standardEffectiveCost"] = 1;
            second["actionCostAudit"]!["opposition"]!["before"] = 5;
            second["actionCostAudit"]!["opposition"]!["after"] = 4;
            var third = first.DeepClone().AsObject();
            third["exchangeId"] = "exchange_after_satisfied_guarantee";
            third["before"] = second["after"]!.DeepClone();
            third["after"] = third["before"]!.DeepClone();
            third["after"]!["oppositionSideStrain"] = "broken";
            third["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 4;
            third["diceAudit"]!["diceUsed"]![0]!["value"] = 18;
            third["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 5;
            third["diceAudit"]!["diceUsed"]![1]!["value"] = 3;
            third["diceAudit"]!["playerTotal"] = 18;
            third["diceAudit"]!["oppositionTotal"] = 3;
            third["diceAudit"]!["margin"] = 15;
            third["diceAudit"]!["outcomeBand"] = "decisive_player_success";
            third["actionCostAudit"]!["player"]!["before"] = 3;
            third["actionCostAudit"]!["player"]!["after"] = 2;
            third["actionCostAudit"]!["opposition"]!["before"] = 4;
            third["actionCostAudit"]!["opposition"]!["after"] = 3;
            active["exchangeLog"]!.AsArray().Add(second);
            active["exchangeLog"]!.AsArray().Add(third);
            active["oppositionSideStrain"] = "broken";
        });
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var validator = new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance);
        var opened = await validator.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("offer", opened.Disposition);
        using var firstOffer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var firstDecision = OriginalSpiritualWoundDecision(firstOffer.Offer!.OpportunityRef,
            "spiritual_action_cost_burden", "guard");
        var advanced = await firstOffer.SubmitDecisionAsync(lease, firstDecision,
            "Чужое давление надломило волю хранителя.");
        Assert.True(advanced.Disposition == "offer", advanced.Disposition + ": " +
            string.Join(Environment.NewLine, advanced.Issues.Select(issue => $"{issue.Code}: {issue}")));
        using var lastOffer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(advanced.Session);
        Assert.Equal(2, lastOffer.Offer!.MinimumSeverityRank);
        var worsening = JsonNode.Parse(OriginalSpiritualWoundDecision(
            lastOffer.Offer.OpportunityRef, "spiritual_action_cost_burden", "guard").GetRawText())!;
        worsening["proposal"]!["severity"] = "II";
        const string laterNarration = "Новый натиск углубил надлом воли хранителя.";
        const string completeNarration = "Чужое давление надломило волю хранителя. " + laterNarration;
        worsening["proposal"]!["display"]!["acquisitionNarration"] = laterNarration;
        var definitions = worsening["proposal"]!["consequenceDefinitions"]!.AsArray();
        var extra = definitions[0]!.DeepClone();
        extra["definitionRef"] = "maneuver_burden";
        extra["definition"]!["definitionKey"] = "maneuver_burden";
        extra["definition"]!["stacking"]!["stackKey"] = "stack_maneuver_burden";
        extra["definition"]!["components"]![0]!["payload"]!["operation"] = "maneuver";
        definitions.Add(extra);
        var completed = await lastOffer.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(worsening),
            completeNarration);
        Assert.True(completed.Disposition == "completed_unpublished", completed.Disposition + ": " +
            string.Join(Environment.NewLine, completed.Issues.Select(issue => $"{issue.Code}: {issue}")));
        var checkpoint = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            SpiritualWoundCaptureCheckpointState.StatePath));
        var advances = checkpoint["checkpoint"]!["advances"]!.AsArray();
        Assert.Equal(3, advances.Count);
        Assert.NotEmpty(advances[0]!["inputChanges"]!.AsArray());
        Assert.Empty(advances[1]!["inputChanges"]!.AsArray());
        Assert.NotEmpty(advances[2]!["inputChanges"]!.AsArray());
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
        var reduced = await terminal.ReduceCompletedDecisionsAsync(lease);
        Assert.True(reduced.Success, string.Join(Environment.NewLine,
            reduced.Issues.Select(issue => $"{issue.Code}: {issue}")));
        var rows = reduced.ReceiptAfterImage!["decisions"]!.AsArray();
        Assert.Equal(3, rows.Count);
        Assert.Equal("materialize", rows[0]!["decision"]!.GetValue<string>());
        Assert.Equal("guarantee_satisfied", rows[1]!["decision"]!.GetValue<string>());
        Assert.Equal("materialize", rows[2]!["decision"]!.GetValue<string>());
        var woundId = rows[0]!["woundId"]!.GetValue<string>();
        Assert.All(rows, row => Assert.Equal(woundId, row!["woundId"]!.GetValue<string>()));
        Assert.Equal(1, rows[1]!["satisfiedSeverityRank"]!.GetValue<int>());
        Assert.Equal(1, rows[1]!["sourceWitness"]!["guaranteedSeverityRank"]!.GetValue<int>());
        Assert.Equal(1, rows[1]!["sourceWitness"]!["maximumSeverityRank"]!.GetValue<int>());
        Assert.Null(rows[1]!["transitionId"]);
        Assert.Null(rows[1]!["selectedSeverityRank"]);
        Assert.Equal(2, rows[2]!["selectedSeverityRank"]!.GetValue<int>());
        var ordinary = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
            reduced.Reduction);
        var insertions = Assert.IsType<SpiritualLiveWoundCompletion>(
            ordinary.LiveWoundCompletion).Insertions;
        Assert.Equal(2, insertions.Count);
        Assert.Equal("create", Assert.Single(insertions[0].Input.Transitions).Kind);
        Assert.Equal("worsen", Assert.Single(insertions[1].Input.Transitions).Kind);
        Assert.Equal(2, insertions[1].Wound.Severity.Rank);
        var presentation = BookOfEternityClient.UI.WoundPlayerNotification.ComposeSpiritualAcceptedTurn(
            ordinary.LiveWoundCompletion!, completeNarration);
        Assert.Empty(presentation.Issues);
        Assert.Equal(2, presentation.Notifications.Count);
        Assert.Contains("Получена духовная рана", presentation.Notifications[0].Text.PlainText, StringComparison.Ordinal);
        Assert.Contains("(I)", presentation.Notifications[0].Text.PlainText, StringComparison.Ordinal);
        Assert.Contains("Духовная рана ухудшилась", presentation.Notifications[1].Text.PlainText, StringComparison.Ordinal);
        Assert.Contains("(II)", presentation.Notifications[1].Text.PlainText, StringComparison.Ordinal);
        Assert.All(presentation.Notifications, notification => Assert.Equal("/раны", notification.DetailCommand));
        var missingNarration = BookOfEternityClient.UI.WoundPlayerNotification.ComposeSpiritualAcceptedTurn(
            ordinary.LiveWoundCompletion!, "Хранитель продолжает бой.");
        Assert.Empty(missingNarration.Notifications);
        Assert.Equal(2, missingNarration.Issues.Count);
        Assert.All(missingNarration.Issues, issue => Assert.Equal("wound_acquisition_narration_missing", issue.Code));
        var missingEarlierNarration = BookOfEternityClient.UI.WoundPlayerNotification.ComposeSpiritualAcceptedTurn(
            ordinary.LiveWoundCompletion!, laterNarration);
        Assert.Empty(missingEarlierNarration.Notifications);
        Assert.Equal("wound_acquisition_narration_missing", Assert.Single(missingEarlierNarration.Issues).Code);
        var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                ordinary, ordinary.Resources));
        Assert.Empty(planned.Issues);
        Assert.NotNull(planned.Plan);
    }
}
