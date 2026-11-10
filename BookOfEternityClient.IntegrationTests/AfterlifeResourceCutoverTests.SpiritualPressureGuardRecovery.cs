using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Selects the roll burden authored in the genuine original III decision before any parent publication.</summary>
    private enum PressureParentRollBurdenOperation { Counter, Pressure }

    /// <summary>Qualifies genuine III birth, lawful source-free guard publication below cap and a fresh signed read, without a later pressure exchange.</summary>
    [Fact]
    public async Task SpiritualPressureStrain_RealBirthThenGuardPublishesBelowCapAndKeepsSignedParent()
        => await WithSignedPressureParentBelowCapAsync([15, 5], "request_pressure_guard_recovery_read_44");

    /// <summary>Publishes the genuine III parent and lawful guard recovery before signing an independent continuation.</summary>
    /// <param name="finalSignedDice">Actual turn44 dice fixed before the continuation authors its ordinary exchange.</param>
    /// <param name="finalRequestId">Distinct request identity for the signed turn44 origin.</param>
    /// <param name="continuation">Optional work using this fixture's signed below-cap frontier; cleanup remains owned here.</param>
    /// <param name="rollBurdenOperation">Original birth decision's roll operation; Counter preserves the established fixture exactly.</param>
    /// <param name="beforeFinalSignatureAsync">Optional legal fixture authoring after all genuine guard assertions and before the first signed44 snapshot.</param>
    private async Task WithSignedPressureParentBelowCapAsync(int[] finalSignedDice, string finalRequestId,
        Func<ResourceMaterializationTestContext, Task>? continuation = null,
        PressureParentRollBurdenOperation rollBurdenOperation = PressureParentRollBurdenOperation.Counter,
        Func<ResourceMaterializationTestContext, Task>? beforeFinalSignatureAsync = null)
    {
        var rollOperation = PressureParentRollOperationToken(rollBurdenOperation);
        var started = Stopwatch.GetTimestamp();
        var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync, signedDice: [5, 15],
            playerCurrent: 10m, oppositionCurrent: 9m, spiritFocusTier: 3, oppositionMaximum: 9m);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guard_recovery_fixture", root = context.RootPath,
            elapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds }));
        try
        {
            var birthStarted = Stopwatch.GetTimestamp();
            await PublishPressureGuardRecoveryParentAsync(context, rollBurdenOperation);
            var parent = await ReadCombatHealingWoundAsync(context);
            var parentFingerprint = WoundIdentityState.ComputeSemanticFingerprint(parent);
            var parentJson = WoundMaterializationContract.SerializeCanonical(parent);
            var parentRoots = parent.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId).ToArray();
            Assert.NotEmpty(parentRoots);
            var components = Assert.Single(parent.Consequences.OwnedEffectSources.Definitions)
                .GetProperty("components").EnumerateArray().ToArray();
            Assert.Equal(3, components.Length);
            Assert.Equal("spiritual_strain_burden", components[0].GetProperty("profile").GetString());
            Assert.Equal("pressure", components[0].GetProperty("payload").GetProperty("operation").GetString());
            Assert.Equal(1, components[0].GetProperty("payload").GetProperty("magnitude").GetInt32());
            Assert.Equal("spiritual_roll_hindrance", components[1].GetProperty("profile").GetString());
            Assert.Equal(rollOperation, components[1].GetProperty("payload").GetProperty("operation").GetString());
            Assert.Equal("spiritual_action_cost_burden", components[2].GetProperty("profile").GetString());
            Assert.Equal("guard", components[2].GetProperty("payload").GetProperty("operation").GetString());
            Assert.Equal(2, components[2].GetProperty("payload").GetProperty("magnitude").GetInt32());
            var receiptBefore = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.DeepClone();
            Assert.Single(receiptBefore["sources"]!.AsArray());
            Assert.Single(receiptBefore["decisions"]!.AsArray());
            Assert.Equal("materialize", receiptBefore["decisions"]![0]!["decision"]!.GetValue<string>());
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guard_recovery_birth_published", parent.WoundId,
                parentFingerprint, parentRoots, elapsedMilliseconds = Stopwatch.GetElapsedTime(birthStarted).TotalMilliseconds }));

            await context.CaptureValidatedPendingSnapshotAsync(turn: 43, currentRealm: "Chaos Sea",
                preGeneratedDices1d20: [8, 4], requestId: "request_pressure_guard_recovery_43");
            var preserved = await CaptureExistingStrainCanonicalAsync(context);
            var identityBeforeGuard = (await context.ReadJsonAsync(EffectIdentityState.StatePath))!.DeepClone().AsObject();
            var authoredGuard = await AppendPressureGuardRecoveryDraftAsync(context);
            var guardStarted = Stopwatch.GetTimestamp();
            AcceptedMechanicsPlan expected;
            AcceptedMechanicsResourcePlanningResult guardResources;
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
                AssertNoConflictFrameErrors(recorded.Issues);
                using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
                AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
                var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
                AssertNoConflictFrameErrors(advanced.Issues);
                var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
                var closed = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
                Assert.Empty(closed.Sources); // Improvement is source-free, not an invented zero-ceiling injury.
                Assert.Equal(new[] { 0, 1 }, closed.DiceClaims.Select(row => row.SourceIndex));
                Assert.Equal(new[] { 8, 4 }, closed.DiceClaims.Select(row => row.Value));
                Assert.True(JsonNode.DeepEquals(authoredGuard, JsonNode.Parse(closed.ExchangeJson)));
                var noOffer = await capture.CommitC2FirstTransportAsync(lease, interval);
                AssertNoConflictFrameErrors(noOffer.Issues);
                Assert.Equal("no_offer", noOffer.Disposition);
                Assert.Null(noOffer.Checkpoint);
                Assert.Null(noOffer.Pending);
                var completed = await capture.CompleteOrdinaryReductionAsync(lease);
                AssertNoConflictFrameErrors(completed.Issues);
                Assert.True(completed.Success);
                Assert.NotNull(completed.Reduction);
                guardResources = completed.Reduction.Resources;
                var prepared = await capture.PrepareC4PublicationAsync(lease);
                AssertNoConflictFrameErrors(prepared.Issues);
                Assert.True(prepared.Authority is not null, string.Join("\n", prepared.Issues));
                var authority = prepared.Authority!;
                expected = authority.Plan;
                Assert.Null(authority.LiveWoundCompletion);
                var plannedReceipt = expected.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath];
                Assert.True(JsonNode.DeepEquals(receiptBefore["sources"], plannedReceipt["sources"]));
                Assert.True(JsonNode.DeepEquals(receiptBefore["decisions"], plannedReceipt["decisions"]));
                Assert.Empty(await authority.ValidateCurrentInputsAsync(context.FileSystem, lease));
                foreach (var (path, bytes) in preserved)
                    Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                Assert.True(AcceptedMechanicsPlanAuthority.RegisterSpiritualPublication(context.FileSystem, lease, authority));
            }
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            Assert.Same(expected, await PeekPlanAsync(context));
            var spent = expected.ResourceEvents.Where(row => row.EventKind == "resource_spent").ToArray();
            Assert.Equal(2, expected.ResourceEvents.Count);
            Assert.Equal(2, spent.Length);
            var player = Assert.Single(spent, row => row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor);
            var opposition = Assert.Single(spent, row => row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);
            Assert.Equal((7m, 3m, 4m), (player.Before, player.After, player.AppliedAmount));
            Assert.Equal((6m, 3m, 3m), (opposition.Before, opposition.After, opposition.AppliedAmount));
            Assert.Equal(3m, ResolvePlannedActionPoints(expected, ResourceOwnerKind.AfterlifeActor).Current);
            Assert.Equal(3m, ResolvePlannedActionPoints(expected, ResourceOwnerKind.AfterlifeConflictSide).Current);
            var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
            AssertNoConflictFrameErrors(published.Issues);
            Assert.Same(expected, published.MechanicsPlan);
            foreach (var pair in expected.WoundCarrierAfterImages.Concat(expected.EffectCarrierAfterImages)
                         .Concat(expected.OwnerCompanionAfterImages))
                Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
            Assert.True(JsonNode.DeepEquals(expected.EffectIdentityAfterImage,
                await context.ReadJsonAsync(EffectIdentityState.StatePath)));
            Assert.True(JsonNode.DeepEquals(expected.StateAfterImage,
                await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
            Assert.True(JsonNode.DeepEquals(expected.HistoryAfterImage,
                await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
            var conflict = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!;
            Assert.Equal(2, conflict["exchangeLog"]!.AsArray().Count);
            Assert.True(JsonNode.DeepEquals(authoredGuard, conflict["exchangeLog"]![1]));
            Assert.Equal("strained", conflict["playerSideStrain"]!.GetValue<string>());
            Assert.Equal("clear", conflict["oppositionSideStrain"]!.GetValue<string>());
            Assert.Equal("contested", conflict["conflictPosition"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(authoredGuard["before"]!["controlState"], conflict["controlState"]));
            Assert.True(JsonNode.DeepEquals(authoredGuard["before"]!["tempoAdvantage"], conflict["tempoAdvantage"]));
            var after = await ReadCombatHealingWoundAsync(context);
            Assert.Equal(parent.WoundId, after.WoundId);
            Assert.Equal(3, after.Severity.Rank);
            Assert.Equal(parentFingerprint, WoundIdentityState.ComputeSemanticFingerprint(after));
            Assert.Equal(parentJson, WoundMaterializationContract.SerializeCanonical(after));
            Assert.Equal(parentRoots, after.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId));
            foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
            {
                var bytes = await context.FileSystem.ReadFileBytesAsync(path);
                Assert.True(bytes is not null && preserved[path]!.SequenceEqual(bytes), path);
            }
            var identityAfterGuard = (await context.ReadJsonAsync(EffectIdentityState.StatePath))!.AsObject();
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guard_recovery_identity_images",
                path = EffectIdentityState.StatePath, before = identityBeforeGuard, after = identityAfterGuard }));
            AssertPressureGuardRecoveryIdentityAppend(identityBeforeGuard, identityAfterGuard, parentRoots, guardResources, expected);
            var mechanics = await EffectMechanicsSnapshot.LoadAsync(context.FileSystem);
            Assert.True(mechanics.IsAccepted, string.Join("\n", mechanics.Issues));
            foreach (var root in parentRoots)
                Assert.Contains(mechanics.Components, row => row.EffectId == root);
            var receiptAfter = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!;
            Assert.True(JsonNode.DeepEquals(receiptBefore["sources"], receiptAfter["sources"]));
            Assert.True(JsonNode.DeepEquals(receiptBefore["decisions"], receiptAfter["decisions"]));
            var history = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(),
                WoundHistoryState.HistoryPath);
            var ledger = SpiritualWoundOpportunityReceiptState.Parse(receiptAfter.ToJsonString(),
                SpiritualWoundOpportunityReceiptState.StatePath);
            Assert.True(history.IsValid, string.Join("\n", history.Issues));
            Assert.True(ledger.IsValid, string.Join("\n", ledger.Issues));
            Assert.True(ledger.State!.HasExactStrainHistory(history.State!));
            foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                         SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
                Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guard_recovery_guard_published", strain = "strained",
                playerActionPoints = 3, oppositionActionPoints = 3, harmfulSources = 0,
                elapsedMilliseconds = Stopwatch.GetElapsedTime(guardStarted).TotalMilliseconds }));

            // Sign the actual published bytes before any optional third exchange is authored.
            if (beforeFinalSignatureAsync is not null)
                await beforeFinalSignatureAsync(context);
            var publishedBytes = await CaptureExistingStrainCanonicalAsync(context);
            publishedBytes[AfterlifeSpiritualConflictState.StatePath] =
                await context.FileSystem.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath);
            await context.CaptureValidatedPendingSnapshotAsync(turn: 44, currentRealm: "Chaos Sea",
                preGeneratedDices1d20: finalSignedDice, requestId: finalRequestId);
            var signed44 = (await context.ReadJsonAsync("game_state/control/pending_turn_snapshot.json"))!;
            Assert.Equal(44, signed44["turnNumber"]!.GetValue<int>());
            foreach (var (path, bytes) in publishedBytes)
            {
                Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(path));
                if (bytes is not null)
                    Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(signed44["files"]![path]!.GetValue<string>()));
            }
            AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guard_recovery_signed44_validated",
                elapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds }));
            if (continuation is not null)
                await continuation(context);
        }
        finally
        {
            await context.DisposeAsync();
            var removed = !Directory.Exists(context.RootPath);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guard_recovery_owned_cleanup", root = context.RootPath, removed }));
            Assert.True(removed, "The qualification must remove its own isolated fixture root.");
        }
    }

    /// <summary>Preserves complete parent provenance while proving its actual non-consuming owner-turn-end append.</summary>
    private static void AssertPressureGuardRecoveryIdentityAppend(JsonObject before, JsonObject after,
        IReadOnlyList<string> parentRoots, AcceptedMechanicsResourcePlanningResult resources, AcceptedMechanicsPlan plan, int turn = 43)
    {
        var parsedBefore = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(before), EffectIdentityState.StatePath);
        var parsedAfter = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(after), EffectIdentityState.StatePath);
        Assert.Empty(parsedBefore.Issues);
        Assert.Empty(parsedAfter.Issues);
        var prior = Assert.IsType<EffectIdentityState>(parsedBefore.State);
        var current = Assert.IsType<EffectIdentityState>(parsedAfter.State);
        Assert.Equal(prior.Entries.Select(row => row.EffectId).OrderBy(id => id),
            current.Entries.Select(row => row.EffectId).OrderBy(id => id));
        var effectPlan = Assert.IsType<EffectAcceptedTurnPlan>(plan.EffectPlan);
        var transcript = Assert.IsType<AcceptedEffectBoundaryTranscript>(resources.EffectBoundaryTranscript);
        var restored = after.DeepClone().AsObject();
        foreach (var entry in prior.Entries)
        {
            Assert.True(current.TryGetEntry(entry.EffectId, out var next));
            if (!parentRoots.Contains(entry.EffectId))
            {
                Assert.True(JsonNode.DeepEquals(entry.Raw, next.Raw), entry.EffectId);
                continue;
            }
            Assert.Equal("active", next.State);
            Assert.Equal(entry.Transitions.Count + 1, next.Transitions.Count);
            for (var i = 0; i < entry.Transitions.Count; i++)
                Assert.True(JsonNode.DeepEquals(entry.Transitions[i].Raw, next.Transitions[i].Raw), entry.EffectId);
            var execution = Assert.Single(resources.ResourceTriggerExecutions, row => row.EffectId == entry.EffectId);
            Assert.Equal("on_owner_turn_end", execution.TriggerId);
            Assert.Equal("owner_turn_end", execution.EventKind);
            Assert.Null(execution.RemainingUseBudget);
            var accepted = Assert.Single(transcript.AcceptedActivations,
                row => row.Activation.Stamp.Identity.EffectId == entry.EffectId);
            Assert.Equal(new EffectActivationCandidateIdentity(execution.EffectId, execution.TriggerId,
                execution.EventKind, execution.EventRef, execution.TriggerEventRef!), accepted.Activation.Stamp.Identity);
            Assert.False(accepted.Activation.Stamp.ConsumesUse);
            Assert.Null(accepted.Activation.Stamp.UsesBefore);
            Assert.Null(accepted.Activation.UsesAfter);
            Assert.False(accepted.Activation.EffectTerminal);
            var ownerEvent = Assert.Single(effectPlan.EventInput["lifecycleEvents"]!.AsArray().OfType<JsonObject>(), row =>
                row["phase"]!.GetValue<string>() == "owner_turn_end" && row["realm"]!.GetValue<string>() == entry.Realm &&
                JsonNode.DeepEquals(row["target"], entry.Target));
            Assert.Equal(turn, ownerEvent["turn"]!.GetValue<int>());
            var eventRef = ownerEvent["eventRef"]!.GetValue<string>();
            Assert.Equal($"turn_{turn}:lifecycle:owner_turn_end:chaos_sea:player:player_soul", eventRef);
            Assert.Equal(eventRef, execution.TriggerEventRef);
            Assert.Equal(new ResourcePendingAuthorityBinding("permanent", entry.EffectId), accepted.Activation.Stamp.EffectAuthority);
            Assert.Equal(EffectAcceptedTurnPlanner.CreateLifecycleResourceTriggerEventRef(eventRef, entry.EffectId,
                accepted.Activation.Stamp.EffectAuthority, execution.TriggerId), execution.EventRef);
            var appended = next.Transitions[^1];
            Assert.Equal("trigger", appended.Kind);
            Assert.Equal(turn, appended.Turn);
            Assert.Equal(execution.EventRef, appended.EventRef);
            Assert.Equal(new[] { entry.EffectId }, appended.SourceEffectIds);
            Assert.Equal(new[] { entry.EffectId }, appended.ResultEffectIds);
            Assert.Null(appended.ReceiptId);
            Assert.Contains(appended.TransitionId, effectPlan.AllocatedTransitionIds);
            Assert.DoesNotContain(appended.TransitionId, entry.Transitions.Select(row => row.TransitionId));
            var restoredEntry = Assert.Single(restored["entries"]!.AsArray().OfType<JsonObject>(),
                row => row["effectId"]!.GetValue<string>() == entry.EffectId);
            restoredEntry["transitions"] = entry.Raw["transitions"]!.DeepClone();
        }
        // Restoring only the verified appended prefixes must reproduce every original identity field and entry.
        Assert.True(JsonNode.DeepEquals(before, restored));
    }

    /// <summary>Uses the real prior C2 materialization/common C4 path with resource headroom signed before birth.</summary>
    /// <param name="context">Independent genuine fixture whose original birth source is not yet authored.</param>
    /// <param name="rollBurdenOperation">Roll operation authored only in the actual original III materialize decision.</param>
    private static async Task PublishPressureGuardRecoveryParentAsync(ResourceMaterializationTestContext context,
        PressureParentRollBurdenOperation rollBurdenOperation)
    {
        await WriteCompleteConflictFrameExchangeAsync(context);
        var raw = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = raw["activeConflict"]!;
        var first = Assert.Single(active["exchangeLog"]!.AsArray())!;
        first["outcome"] = "setback";
        first["after"]!["playerSideStrain"] = "overwhelmed";
        first["after"]!["oppositionSideStrain"] = "clear";
        first["incomingAction"] = new JsonObject { ["operationType"] = "pressure", ["actorType"] = "guardian",
            ["actorId"] = "guardian_frame", ["summary"] = "Хранитель оказывает духовное давление." };
        first["actionCostAudit"]!["player"] = CostAudit("pressure", 3m, 10m, 7m);
        first["actionCostAudit"]!["player"]!["max"] = 10m;
        first["actionCostAudit"]!["opposition"] = CostAudit("pressure", 3m, 9m, 6m);
        first["actionCostAudit"]!["opposition"]!["max"] = 9m;
        var dice = first["diceAudit"]!;
        dice["diceUsed"]![0]!["value"] = 5;
        dice["diceUsed"]![1]!["value"] = 15;
        dice["playerTotal"] = 5;
        dice["oppositionTotal"] = 15;
        dice["margin"] = -10;
        dice["outcomeBand"] = "decisive_opposition_success";
        active["playerSideStrain"] = "overwhelmed";
        active["oppositionSideStrain"] = "clear";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, raw.ToJsonString());
        const string scene = "Чужое давление надломило волю души.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject
            { ["response"] = scene, ["timestamp"] = "2026-09-30T00:00:00Z" }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var ordinary = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
            AssertNoConflictFrameErrors(ordinary.Issues);
            var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(ordinary.Session);
            Assert.Single(source.CheckedExchanges);
            Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
            var acquisition = Assert.Single(source.Sources);
            Assert.Equal("player", acquisition.AffectedSide);
            Assert.Equal(20L, acquisition.Calculation.TraumaPressure);
            Assert.Equal(3, acquisition.Calculation.MaximumSeverityRank);
            await CommitSpiritualContinuationDraftFirstPairAsync(context, lease);
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues);
            Assert.Equal("offer", opened.Disposition);
            using var choice = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            Assert.Equal(3, choice.Offer!.MaximumSeverityRank);
            var decision = CreateSpiritualStrainThirdDegreeDecision(choice.Offer.OpportunityRef);
            var consequence = decision["proposal"]!["consequenceDefinitions"]![0]!;
            consequence["definition"]!["components"]![1]!["payload"]!["operation"] =
                PressureParentRollOperationToken(rollBurdenOperation);
            consequence["root"]!["slots"]![1]!["readableSummary"] =
                rollBurdenOperation == PressureParentRollBurdenOperation.Counter
                    ? "Контрприём душе даётся труднее." : "Оказывать духовное давление душе труднее.";
            var selected = await choice.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(decision), scene);
            using var completed = selected.Session;
            AssertNoConflictFrameErrors(selected.Issues);
            Assert.Equal("completed_unpublished", selected.Disposition);
        }
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var plan = await PeekPlanAsync(context);
        Assert.Equal(7m, ResolvePlannedActionPoints(plan, ResourceOwnerKind.AfterlifeActor).Current);
        Assert.Equal(6m, ResolvePlannedActionPoints(plan, ResourceOwnerKind.AfterlifeConflictSide).Current);
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues);
        Assert.Same(plan, published.MechanicsPlan);
        Assert.Equal(3, (await ReadCombatHealingWoundAsync(context)).Severity.Rank);
        Assert.Equal("overwhelmed", (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!["playerSideStrain"]!.GetValue<string>());
    }

    /// <summary>Maps only the two supported original-decision fixture variants without changing any signed parent.</summary>
    /// <param name="operation">Typed original birth decision variant.</param>
    /// <returns>The exact existing profile-registry operation token.</returns>
    private static string PressureParentRollOperationToken(PressureParentRollBurdenOperation operation) => operation switch
    {
        PressureParentRollBurdenOperation.Counter => "counter",
        PressureParentRollBurdenOperation.Pressure => "pressure",
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    /// <summary>Authors only the lawful ordinary guard draft against the already signed actual parent.</summary>
    private static async Task<JsonNode> AppendPressureGuardRecoveryDraftAsync(ResourceMaterializationTestContext context)
    {
        var root = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!;
        var active = root["activeConflict"]!;
        var log = active["exchangeLog"]!.AsArray();
        var exchange = Assert.Single(log)!.DeepClone();
        exchange["exchangeId"] = "exchange_pressure_guard_recovery_43";
        exchange["turnNumber"] = 43;
        exchange["exchangeAtTurn"] = 43;
        exchange["operationType"] = "guard";
        exchange["outcome"] = "partial_success";
        exchange["summary"] = "Душа защищается от давления Хранителя и снижает духовное напряжение.";
        exchange["before"] = log[0]!["after"]!.DeepClone();
        exchange["after"] = exchange["before"]!.DeepClone();
        exchange["after"]!["playerSideStrain"] = "strained";
        var matchup = exchange["matchupAudit"]!;
        matchup["playerOperation"] = "guard";
        matchup["primaryResolutionLane"] = "guard";
        matchup["oppositionOperation"] = "pressure";
        matchup["riskProfile"] = "safe_defense";
        matchup["matchupRationale"] = "Защита смягчает встречное давление, не меняя позицию и не нанося напряжение Хранителю.";
        var playerCost = CostAudit("guard", 4m, 7m, 3m);
        playerCost["baseCost"] = 2m;
        playerCost["max"] = 10m;
        exchange["actionCostAudit"]!["player"] = playerCost;
        exchange["actionCostAudit"]!["opposition"] = CostAudit("pressure", 3m, 6m, 3m);
        exchange["actionCostAudit"]!["opposition"]!["max"] = 9m;
        var dice = exchange["diceAudit"]!;
        dice["diceUsed"]![0]!["sourceIndex"] = 0;
        dice["diceUsed"]![0]!["value"] = 8;
        dice["diceUsed"]![1]!["sourceIndex"] = 1;
        dice["diceUsed"]![1]!["value"] = 4;
        dice["playerTotal"] = 8;
        dice["oppositionTotal"] = 4;
        dice["margin"] = 4;
        dice["outcomeBand"] = "player_success";
        log.Add(exchange);
        active["playerSideStrain"] = "strained";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await context.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject
            { ["response"] = "Душа защищается от давления Хранителя, снижая напряжение до лёгкого. Старая духовная рана остаётся неизменной.",
                ["timestamp"] = "2026-09-30T00:00:00Z" }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        return exchange.DeepClone();
    }
}
