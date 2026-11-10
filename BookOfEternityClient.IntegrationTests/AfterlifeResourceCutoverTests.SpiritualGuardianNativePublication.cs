using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Detached original source, insertion identity and independently authored publication expectation.</summary>
    /// <param name="Witness">Actual closed source persisted through C2 and C3.</param>
    /// <param name="InsertedWoundId">Real materializer identity at birth, otherwise absent.</param>
    /// <param name="ExpectedConflict">Full ordinary root plus exactly two expected strain changes for the v5 consumer.</param>
    /// <param name="PublishedParent">Detached full parent graph read after successful common publication.</param>
    private sealed record NativeGuardianCompletion(JsonObject Witness, string? InsertedWoundId, JsonObject? ExpectedConflict,
        NativeGuardianParent PublishedParent);

    /// <summary>Runs real first C2, one cold decision, C3 and common C4; exports only detached published facts.</summary>
    /// <param name="context">This Fact's filesystem containing the signed original and authored ordinary output.</param>
    /// <param name="ordinary">Independently authored complete exchange before actual projection.</param>
    /// <param name="proposal">Pre-sign complete III/IV proposal for birth, otherwise absent.</param>
    /// <param name="parent">Actual published and recovered parent for current v5 use, otherwise absent.</param>
    /// <param name="birthRank">Independent expected rank fixed before the original birth.</param>
    /// <returns>Detached source, insertion identity and independent full actual conflict expectation after C4.</returns>
    private async Task<NativeGuardianCompletion> CompleteNativeGuardianExchangeAsync(ResourceMaterializationTestContext context,
        JsonObject ordinary, JsonObject? proposal, NativeGuardianParent? parent, int birthRank)
    {
        var birth = parent is null;
        var turn = birth ? 44 : 46;
        var actual = ordinary.DeepClone().AsObject();
        if (!birth) actual["after"]!["oppositionSideStrain"] = "overwhelmed";
        JsonObject? expectedConflict = null;
        if (!birth)
        {
            // Freeze the authored ordinary root before capture. Only these two scalar paths may change.
            expectedConflict = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.DeepClone().AsObject();
            var active = expectedConflict["activeConflict"]!;
            Assert.True(JsonNode.DeepEquals(ordinary, Assert.Single(active["exchangeLog"]!.AsArray())));
            Assert.Equal("fractured", active["oppositionSideStrain"]!.GetValue<string>());
            active["exchangeLog"]![0]!["after"]!["oppositionSideStrain"] = "overwhelmed";
            active["oppositionSideStrain"] = "overwhelmed";
        }
        JsonObject? witness = null;
        string? insertedWound = null;
        AcceptedMechanicsResourcePlanningResult? completedResources = null;
        FileSystemManager.CanonicalWriteLease? lease = null;
        ValidationService.SpiritualOriginalTurnCapture? warm = null;
        ValidationService.SpiritualC2PrivateSession? choice = null;
        ValidationService.SpiritualC2PrivateSession? completed = null;
        var started = Stopwatch.GetTimestamp();
        await ExecutePressureGroupFixtureStagesAsync(new(
            Work: async () =>
            {
                lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                var captured = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
                warm = captured.Capture;
                RequireGuardianJourneyIssues(NativeGuardianSession, "capture_" + turn, captured.Issues);
                Assert.NotNull(warm);
                RequireGuardianJourneyIssues(NativeGuardianSession, "begin_" + turn, await warm.BeginResourceExecutionAsync(lease));
                RequireGuardianJourneyIssues(NativeGuardianSession, "prefix_" + turn,
                    (await warm.PrepareOriginalResourcePrefixAsync(lease)).Issues);
                if (!birth)
                {
                    var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                    var generation = System.IO.File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                    var first = await warm.ReadExistingPressureOrdinaryComparisonAsync(lease);
                    RequireGuardianJourneyIssues(NativeGuardianSession, "ordinary_cold", first.Issues);
                    Assert.NotNull(first.Comparison);
                    Assert.True(JsonNode.DeepEquals(ordinary, first.Comparison.ReadOrdinaryExchange()));
                    Assert.Equal(SpiritualWoundOpportunityMath.Calculate(new(8, 0, 0, "strained", "fractured", "hostile", 4)),
                        first.Comparison.OrdinaryCalculation);
                    var cold = warm.ReadExistingStrainReuseCounts();
                    var coldSources = warm.ReadExistingPressureGroupComputationCounts();
                    var second = await warm.ReadExistingPressureOrdinaryComparisonAsync(lease);
                    RequireGuardianJourneyIssues(NativeGuardianSession, "ordinary_warm", second.Issues);
                    Assert.NotNull(second.Comparison);
                    Assert.Equal(first.Comparison.OrdinaryExchangeJson, second.Comparison.OrdinaryExchangeJson);
                    var reused = warm.ReadExistingStrainReuseCounts();
                    Assert.Equal((1, 1, 0), (cold.OrdinaryEvaluations, cold.SignedParentReads, cold.ActualEvaluations));
                    Assert.Equal((cold.OrdinaryEvaluations, cold.SignedParentReads, cold.ActualEvaluations),
                        (reused.OrdinaryEvaluations, reused.SignedParentReads, reused.ActualEvaluations));
                    Assert.Equal(coldSources, warm.ReadExistingPressureGroupComputationCounts());
                    Assert.True(reused.FreshInputChecks > cold.FreshInputChecks);
                    Assert.True(reused.FreshDependencyChecks > cold.FreshDependencyChecks);
                    AssertExistingPressureInputInert(context, lease, files, generation);
                }
                var advanced = await warm.AdvanceNextResourceExchangeAsync(lease);
                RequireGuardianJourneyIssues(NativeGuardianSession, "actual_" + turn, advanced.Issues);
                var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
                Assert.Equal((0, ordinary["exchangeId"]!.GetValue<string>()), (interval.Ordinal, interval.ExchangeId));
                var closed = Assert.Single(warm.ReadClosedExchangeEvidence(lease));
                Assert.True(JsonNode.DeepEquals(actual, JsonNode.Parse(closed.ExchangeJson)));
                Assert.Equal(birth ? new[] { 15, 5 } : new[] { 12, 4 }, closed.DiceClaims.Select(row => row.Value));
                var source = Assert.Single(closed.Sources);
                Assert.Equal(("opposition", "player_soul:player_soul", "guardian:guardian_frame", "pressure"),
                    (source.AffectedSide, source.ActingActor, source.AffectedActor, source.Operation));
                Assert.Equal(SpiritualWoundOpportunityMath.Calculate(new(birth ? 10 : 8, 0, 0,
                    birth ? "clear" : "strained", birth && birthRank == 4 ? "broken" : "overwhelmed", "hostile", 4)), source.Calculation);
                Assert.Equal(birth ? (birthRank == 4 ? (25L, 4) : (20L, 3)) : (15L, 2),
                    (source.Calculation.TraumaPressure, source.Calculation.MaximumSeverityRank));
                Assert.Null(source.RetraumaWoundId); Assert.Null(source.GuaranteedSeverityRank);
                var beforeFirstC2 = warm.ReadExistingStrainReuseCounts();
                if (!birth)
                    Assert.Equal((1, 1, 1), (beforeFirstC2.OrdinaryEvaluations,
                        beforeFirstC2.ActualEvaluations, beforeFirstC2.SignedParentReads));
                var committed = await warm.CommitC2FirstTransportAsync(lease, interval);
                RequireGuardianJourneyIssues(NativeGuardianSession, "first_c2_" + turn, committed.Issues);
                Assert.Equal("committed", committed.Disposition);
                var packet = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(committed.Pending!))!["pending"]!;
                witness = Assert.Single(packet["sources"]!.AsArray())!.DeepClone().AsObject();
                if (birth) Assert.Null(source.StrainPreimageJson);
                else
                {
                    var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(witness, out var strain);
                    Assert.Equal(source.Calculation, calculation);
                    Assert.NotNull(strain); Assert.Equal(5, strain.Version);
                    Assert.Equal(WoundMaterializationContract.SerializeCanonical(parent!.Wound),
                        WoundMaterializationContract.SerializeCanonical(Assert.Single(strain.Parents).ParentWound));
                    // First C2 composes C1 with one fresh discarded source-validation probe, not another resource advance.
                    var afterFirstC2 = warm.ReadExistingStrainReuseCounts();
                    Assert.Equal((1, 2, 1), (afterFirstC2.OrdinaryEvaluations,
                        afterFirstC2.ActualEvaluations, afterFirstC2.SignedParentReads));
                    _storageOutput.WriteLine(JsonSerializer.Serialize(new
                    {
                        phase = "native_guardian_current_phase_counts",
                        beforeFirstC2 = new { beforeFirstC2.OrdinaryEvaluations, beforeFirstC2.ActualEvaluations, beforeFirstC2.SignedParentReads },
                        afterFirstC2 = new { afterFirstC2.OrdinaryEvaluations, afterFirstC2.ActualEvaluations, afterFirstC2.SignedParentReads }
                    }));
                }
                warm.Dispose();
                await ExecuteGuardianColdSessionStagesAsync(async () =>
                {
                    var opened = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
                        .OpenC2PrivateSessionAsync(lease);
                    choice = opened.Session;
                    RequireGuardianJourneyIssues(NativeGuardianSession, "cold_open_" + turn, opened.Issues);
                    Assert.Equal("offer", opened.Disposition); Assert.NotNull(choice);
                    Assert.Equal(birth ? birthRank : 2, choice.Offer!.MaximumSeverityRank);
                    var decision = new JsonObject { ["opportunityRef"] = choice.Offer.OpportunityRef,
                        ["decision"] = birth ? "materialize" : "none" };
                    if (birth) { decision["woundRef"] = "new_wound"; decision["proposal"] = proposal!.DeepClone(); }
                    if (birth && birthRank == 3)
                    {
                        // One-field invalid constructor proposal, never persisted or normalized into a legal wound.
                        var beforeRejection = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                        var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                        var invalid = decision.DeepClone().AsObject();
                        invalid["proposal"]!["recovery"]!["currentStepThreshold"] = 3;
                        var rejected = await choice.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(invalid),
                            "Чужое давление надломило волю хранителя.");
                        Assert.Equal("blocked", rejected.Disposition); Assert.Null(rejected.Session);
                        var issue = Assert.Single(rejected.Issues);
                        Assert.Equal("wound_spiritual_recovery_threshold_mismatch", issue.Code);
                        Assert.Equal("woundDecisions[0].proposal.recovery.currentStepThreshold", issue.FilePath);
                        Assert.Equal("6", issue.Expected); Assert.Equal("3", issue.Actual);
                        Assert.NotNull(issue.WoundRepairContext);
                        Assert.True(JsonNode.DeepEquals(invalid, issue.WoundRepairContext.RejectedDecision));
                        var repair = Assert.Single(WoundRepairPacketBuilder.Build(rejected.Issues));
                        Assert.Equal("construct_wound", repair.CandidateKind);
                        Assert.True(repair.MatchesRejectedDecision(invalid));
                        Assert.True(repair.MatchesCorrectedDecision(decision, "Чужое давление надломило волю хранителя."));
                        Assert.Equal("proposal.recovery.currentStepThreshold", Assert.Single(repair.Issues).Path);
                        Assert.Equal("wound_spiritual_recovery_threshold_mismatch", Assert.Single(repair.Issues).Code);
                        Assert.Equal(new[] { "proposal.recovery.currentStepThreshold" },
                            repair.RequiredResponseShape["woundDecisions"]![0]!["proposal"]!["correctOnly"]!
                                .AsArray().Select(value => value!.GetValue<string>()));
                        var preserved = invalid["proposal"]!.DeepClone().AsObject();
                        preserved["recovery"]!.AsObject().Remove("currentStepThreshold");
                        Assert.True(JsonNode.DeepEquals(preserved, repair.PreservedProposal));
                        AssertExistingPressureInputInert(context, lease, beforeRejection, generation);
                        Assert.Equal(6, decision["proposal"]!["recovery"]!["currentStepThreshold"]!.GetValue<int>());
                        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "native_guardian_threshold_rejected",
                            rejected.Disposition, issue.Code, issue.FilePath, issue.Expected, issue.Actual,
                            unchangedPhysicalFiles = beforeRejection.Count, unchangedGeneration = true }));
                    }
                    var selected = await choice.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(decision),
                        birth ? "Чужое давление надломило волю хранителя." : null);
                    completed = selected.Session;
                    RequireGuardianJourneyIssues(NativeGuardianSession, "decision_" + turn, selected.Issues);
                    Assert.Equal("completed_unpublished", selected.Disposition); Assert.NotNull(completed);
                    await ExecuteGuardianColdSessionStagesAsync(async () =>
                    {
                        var reduced = await completed.ReduceCompletedDecisionsAsync(lease);
                        RequireGuardianJourneyIssues(NativeGuardianSession, "c3_" + turn, reduced.Issues);
                        Assert.True(reduced.Success); Assert.NotNull(reduced.Reduction);
                        completedResources = reduced.Reduction.Resources;
                        if (birth) insertedWound = Assert.Single(reduced.Reduction.LiveWoundCompletion!.Insertions).Wound.WoundId;
                        else
                        {
                            Assert.Null(reduced.Reduction.LiveWoundCompletion);
                            AssertNativeGuardianReceiptAppend(parent!.Receipt, reduced.ReceiptAfterImage!, witness!);
                        }
                    }, () => completed);
                }, () => choice);
                await AssertPressureGroupRetiredOwnersAsync(warm, choice!, completed!, lease);
            },
            HasWindow: () => false, HasCapture: () => warm is not null, HasLease: () => lease is not null, HasContext: () => false,
            AssertWindowInert: () => Task.CompletedTask, CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => { },
            AssertDisposedCaptureInert: () => { }, DisposeCaptureImplicitly: () => warm!.Dispose(), AssertNoPrivatePair: () => { },
            DisposeLease: async () => await lease!.DisposeAsync(), DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
        RequireGuardianJourneyIssues(NativeGuardianSession, "common_plan_" + turn,
            await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var plan = await PeekPlanAsync(context);
        if (expectedConflict is not null)
            Assert.True(JsonNode.DeepEquals(expectedConflict, plan.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completedResources!.StateAfterImage!.ToCanonicalJson()), plan.StateAfterImage));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completedResources.HistoryAfterImage!.ToCanonicalJson()), plan.HistoryAfterImage));
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(context.FileSystem,
            context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        RequireGuardianJourneyIssues(NativeGuardianSession, "common_c4_" + turn, published.Issues);
        Assert.Same(plan, published.MechanicsPlan);
        await AssertNativeGuardianPublishedPlanAsync(context, plan);
        if (expectedConflict is not null)
            Assert.True(JsonNode.DeepEquals(expectedConflict, await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath)));
        var publishedParent = await ReadNativeGuardianParentAsync(context, birthRank);
        if (parent is not null) AssertNativeGuardianEffectAppend(parent, publishedParent, plan, turn, completedResources);
        WritePressureOwnerTiming("guardian_native_exchange_c4", "turn_" + turn, started);
        return new(Assert.IsType<JsonObject>(witness), insertedWound, expectedConflict, publishedParent);
    }

    /// <summary>Publishes a new conflict's initial I and one original-safe guardian recovery tick in the same real plan.</summary>
    /// <param name="context">This Fact's actual terminal birth publication.</param>
    /// <param name="definitions">Unchanged parsed original resource definitions.</param>
    /// <param name="seed">Original participant DATA without transferable side ownership.</param>
    /// <param name="born">Actual guardian parent and its original creation lineage.</param>
    /// <param name="retiredOwner">Actual terminal side owner that must never return to live state.</param>
    /// <param name="birthRank">Independent expected rank fixed before the original birth.</param>
    /// <returns>The freshly composed new conflict side owner ID.</returns>
    private async Task<string> PublishNativeGuardianStartAsync(ResourceMaterializationTestContext context,
        ResourceDefinitionCatalog definitions, JsonObject seed, NativeGuardianParent born, string retiredOwner, int birthRank)
    {
        var started = Stopwatch.GetTimestamp();
        var before = await CaptureExistingStrainCanonicalAsync(context);
        const string request = "guardian_native_start_45";
        var control = await new ProgressionScheduleService(context.FileSystem, NullLogger<ProgressionScheduleService>.Instance)
            .BuildControlForNextTurnAsync();
        JsonObject? projection = null;
        await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            projection = await AfterlifeWoundTreatmentAuthority.ProjectNaturalRecoveryAsync(context.FileSystem, lease,
                new(NativeGuardianSession, request, 45, control)));
        Assert.NotNull(projection);
        await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = NativeGuardianSession, RequestId = request, TurnNumber = 45, CurrentRealm = "Chaos Sea",
            PlayerAction = "После спокойного восстановления объявить новое духовное противостояние без обменов.", ProgressionControl = control
        });
        await context.FileSystem.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, JsonSerializer.Serialize(new
        {
            progressionProcessingReport = new
            {
                sessionId = NativeGuardianSession, requestId = request, turnNumber = 45,
                worldCyclesProcessed = 0, factionCyclesProcessed = 0, chaosSeaCyclesProcessed = control.ChaosSeaCyclesExpectedThisTurn,
                guardianProjectCyclesProcessed = control.GuardianProjectCyclesExpectedThisTurn,
                residentAgencyCyclesProcessed = control.ResidentAgencyCyclesExpectedThisTurn,
                shiningAbodeCyclesProcessed = control.ShiningAbodeCyclesExpectedThisTurn,
                shiningFactionCyclesProcessed = control.ShiningFactionCyclesExpectedThisTurn,
                shiningTradeCyclesProcessed = control.ShiningTradeCyclesExpectedThisTurn,
                newLastChaosSeaSimulationOrdinal = control.NextChaosSeaTurnOrdinal,
                newLastGuardianProjectCycleOrdinal = control.NextGuardianProjectCycleOrdinal,
                newLastResidentAgencyCycleOrdinal = control.NextResidentAgencyCycleOrdinal,
                newLastShiningAbodeCycleOrdinal = control.NextShiningAbodeCycleOrdinal,
                newLastShiningFactionCycleOrdinal = control.NextShiningFactionCycleOrdinal,
                newLastShiningTradeCycleOrdinal = control.NextShiningTradeCycleOrdinal,
                afterlifeCatchupProcessed = false, afterlifeCatchupSummaryEventsProcessed = 0
            }
        }));
        string? ownerId = null;
        AcceptedMechanicsPlan? startPlan = null;
        await ExecuteGuardianLeaseStagesAsync(context, async lease =>
        {
            foreach (var pair in before) Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
            var signed = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease, PressureOrdinarySignedPaths);
            RequireGuardianJourneyIssues(NativeGuardianSession, "signed_start", signed.Issues);
            var authority = Assert.IsType<PendingTurnSnapshotReadAuthority>(signed.Snapshot);
            Assert.Equal((45, request), (authority.TurnNumber, authority.RequestId));
            var resourcesBefore = await ReadPressurePairResourcesAsync(context, lease, definitions);
            AssertPressurePairRetiredOwners(resourcesBefore, [retiredOwner]);
            var root = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
            Assert.Null(root["activeConflict"]);
            var next = seed.DeepClone().AsObject();
            next.Remove("resourceOwnerBindings"); next.Remove("combatConditions");
            next["conflictId"] = "guardian_native_new_conflict"; next["playerSideStrain"] = "clear";
            next["oppositionSideStrain"] = "strained"; next["exchangeLog"] = new JsonArray();
            next["oppositionSide"]!["resourceMaterialization"] = new JsonObject
                { ["resources"] = new JsonArray(new JsonObject { ["resourceKey"] = "spiritual_action_points", ["maximum"] = 6 }) };
            root[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject { ["mode"] = "start", ["conflictState"] = next };
            await context.FileSystem.WriteFileAtomicAsync(lease, AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
            RequireGuardianJourneyIssues(NativeGuardianSession, "start_location", await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(lease));
            RequireGuardianJourneyIssues(NativeGuardianSession, "start_items", await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
            RequireGuardianJourneyIssues(NativeGuardianSession, "start_owner", await context.Validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease));
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(context.FileSystem, lease, out _, out var result));
            var plan = Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
            startPlan = plan;
            var owner = Assert.Single(plan.OwnerAuthority.Entries.Values, row => row.Key.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);
            Assert.True(owner.SameTurn); ownerId = owner.Key.ResourceOwnerId; Assert.NotEqual(retiredOwner, ownerId);
            Assert.Contains(new ResourceOwnerKey("chaos_sea", ResourceOwnerKind.AfterlifeConflictSide, retiredOwner), plan.OwnerAuthority.ExportHistoricalOwners());
            Assert.Same(plan, await context.Normalizer.BindTo(lease).NormalizeAccumulatedStateWithPlanAsync(new Dictionary<string, string>()));
            RequireGuardianJourneyIssues(NativeGuardianSession, "start_resources", await context.Validator.ValidateAcceptedTurnCanonicalResourceMaterializationAsync(lease));
            RequireGuardianJourneyIssues(NativeGuardianSession, "start_effects", await context.Validator.ValidateAcceptedTurnCanonicalEffectMaterializationAsync(lease));
            await AssertNativeGuardianPublishedPlanAsync(context, plan);
            var resources = await ReadPressurePairResourcesAsync(context, lease, definitions);
            Assert.Equal((3m, 10m), PressurePairPlayerBalance(resources.State));
            AssertPressurePairRetiredOwners(resources, [retiredOwner]);
            var initialized = Assert.Single(AssertPressurePairResourceAppend(resourcesBefore.History, resources.History, 1));
            Assert.Equal((ResourceTransitionOperation.Initialize, ownerId, 45),
                (initialized.Operation, initialized.Coordinate.ResourceOwnerId, initialized.Turn));
            Assert.Null(initialized.BeforeState); Assert.Equal((6m, 6m), (initialized.AfterState!.Current, initialized.AfterState.Maximum));
            var current = await ReadNativeGuardianParentAsync(context, birthRank);
            AssertPressurePairWoundHistoryPrefix(born.History, current.History);
            var tick = Assert.Single(current.History.Transitions.Skip(born.History.Transitions.Count));
            Assert.Equal(("recover", 45, born.Wound.WoundId), (tick.Kind, tick.Turn, tick.WoundId));
            Assert.Null(tick.AttemptId);
            Assert.Equal("natural", Assert.IsType<SpiritualHealingPersistedResult>(tick.TransitionResult).Mode);
            Assert.Equal((birthRank, 1L), (current.Wound.Severity.Rank, current.Wound.Recovery.CurrentStepProgress));
            var prior = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(born.Wound))!.AsObject();
            var recovered = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(current.Wound))!.AsObject();
            foreach (var field in new[] { "recovery", "lastTransition" }) { prior.Remove(field); recovered.Remove(field); }
            Assert.True(JsonNode.DeepEquals(prior, recovered)); Assert.True(JsonNode.DeepEquals(born.Receipt, current.Receipt));
            var actual = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!;
            Assert.True(JsonNode.DeepEquals(root["recentConflicts"], actual["recentConflicts"]));
            Assert.Equal("strained", actual["activeConflict"]!["oppositionSideStrain"]!.GetValue<string>());
            Assert.Empty(actual["activeConflict"]!["exchangeLog"]!.AsArray());
            Assert.True(await ProgressionScheduleService.IsPublishedSpiritualCycleCoreAsync(control, NativeGuardianSession,
                request, 45, path => context.FileSystem.ReadFileBytesAsync(lease, path), authority.SnapshotToken, expectedNaturalProjection: projection));
            AssertNativeGuardianEffectAppend(born, current, plan, 45);
        });
        Assert.Throws<InvalidDataException>(() => startPlan!.SpiritualHealingContinuation!.RequireExecutionCurrent());
        await ExecuteGuardianLeaseStagesAsync(context, lease =>
        {
            Assert.Throws<InvalidDataException>(() => startPlan!.SpiritualHealingContinuation!.RequireCurrent(context.FileSystem, lease));
            return Task.CompletedTask;
        });
        WritePressureOwnerTiming("guardian_native_safe_start", NativeGuardianSession, started);
        return Assert.IsType<string>(ownerId);
    }

    /// <summary>Checks every completed plan image against actual canonical publication and consumed controls.</summary>
    /// <param name="context">This Fact's filesystem after common publication.</param>
    /// <param name="plan">Actual issued common plan, never caller-created authority.</param>
    /// <returns>Completion after full image and pending-control assertions.</returns>
    private static async Task AssertNativeGuardianPublishedPlanAsync(ResourceMaterializationTestContext context, AcceptedMechanicsPlan plan)
    {
        foreach (var pair in plan.WoundCarrierAfterImages.Concat(plan.EffectCarrierAfterImages).Concat(plan.OwnerCompanionAfterImages))
            Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
        foreach (var pair in new[] { (ResourceMaterializationContract.StatePath, plan.StateAfterImage),
                     (ResourceMaterializationContract.HistoryPath, plan.HistoryAfterImage), (EffectIdentityState.StatePath, plan.EffectIdentityAfterImage),
                     (WoundIdentityState.StatePath, plan.WoundIdentityAfterImage), (WoundHistoryState.HistoryPath, plan.WoundHistoryAfterImage) })
            if (pair.Item2 is not null) Assert.True(JsonNode.DeepEquals(pair.Item2, await context.ReadJsonAsync(pair.Item1)), pair.Item1);
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));
    }

    /// <summary>Requires one new-instance none source/decision while retaining the complete original creation records.</summary>
    /// <param name="before">Actual receipt after birth and lawful recovery.</param>
    /// <param name="after">Actual C3 or published receipt for current v5 use.</param>
    /// <param name="witness">The same real source issued by the current capture.</param>
    private static void AssertNativeGuardianReceiptAppend(JsonObject before, JsonObject after, JsonObject witness)
    {
        foreach (var field in new[] { "instances", "sources", "decisions" })
        {
            Assert.Single(before[field]!.AsArray()); Assert.Equal(2, after[field]!.AsArray().Count);
            Assert.True(JsonNode.DeepEquals(before[field]![0], after[field]![0]));
        }
        Assert.True(JsonNode.DeepEquals(before["closures"], after["closures"]));
        Assert.True(JsonNode.DeepEquals(witness, after["sources"]![1]!["witness"]));
        var decision = after["decisions"]![1]!;
        Assert.Equal("none", decision["decision"]!.GetValue<string>());
        Assert.Null(decision["woundId"]); Assert.Null(decision["selectedSeverityRank"]);
        Assert.True(JsonNode.DeepEquals(witness, decision["sourceWitness"]));
    }

    /// <summary>Proves literal spends, unchanged maxima and optional later retirement on complete append-only History.</summary>
    /// <param name="before">Complete actual ledger and History before the exchange.</param>
    /// <param name="after">Complete actual ledger and History after common C4.</param>
    /// <param name="turn">Exact accepted exchange turn.</param>
    /// <param name="playerBefore">Literal original player balance.</param>
    /// <param name="oppositionBefore">Literal original side balance.</param>
    /// <param name="ownerId">Exact current side owner ID.</param>
    /// <param name="terminal">Whether retirement must follow both paid spends.</param>
    private static void AssertNativeGuardianResourceSpends(PressurePairResources before, PressurePairResources after,
        int turn, decimal playerBefore, decimal oppositionBefore, string ownerId, bool terminal)
    {
        var rows = AssertPressurePairResourceAppend(before.History, after.History, terminal ? 3 : 2);
        var spends = rows.Where(row => row.Operation == ResourceTransitionOperation.Spend).ToArray(); Assert.Equal(2, spends.Length);
        var player = Assert.Single(spends, row => row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor);
        var side = Assert.Single(spends, row => row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);
        Assert.Equal((10m, 10m), (player.BeforeState!.Maximum, player.AfterState!.Maximum));
        Assert.Equal((6m, 6m), (side.BeforeState!.Maximum, side.AfterState!.Maximum));
        Assert.Equal((turn, playerBefore, playerBefore - 3m, 3m), (player.Turn, player.BeforeState!.Current, player.AfterState!.Current, player.AppliedAmount));
        Assert.Equal((turn, ownerId, oppositionBefore, oppositionBefore - 3m, 3m),
            (side.Turn, side.Coordinate.ResourceOwnerId, side.BeforeState!.Current, side.AfterState!.Current, side.AppliedAmount));
        if (terminal)
        {
            var retired = Assert.Single(rows, row => row.Operation == ResourceTransitionOperation.Retire);
            Assert.Equal(side.Coordinate, retired.Coordinate); Assert.All(spends, row => Assert.True(row.ExecutionSequence < retired.ExecutionSequence));
        }
    }
    /// <summary>Proves only one real guardian lifecycle append and chronology change; all effect payload and unrelated profile DATA survive.</summary>
    /// <param name="before">Actual parent graph before this accepted turn.</param>
    /// <param name="after">Actual published graph, whose wound recovery or unchanged wound is checked separately.</param>
    /// <param name="plan">Real common plan whose completed effect allocations must match the append.</param>
    /// <param name="turn">Exact accepted clean-start or existing-pressure turn.</param>
    /// <param name="resources">Actual completed C3 execution for the current-use turn; absent on the separate clean start.</param>
    private void AssertNativeGuardianEffectAppend(NativeGuardianParent before, NativeGuardianParent after,
        AcceptedMechanicsPlan plan, int turn, AcceptedMechanicsResourcePlanningResult? resources = null)
    {
        var effectId = Assert.Single(before.Wound.Consequences.OwnedEffectSources.RootBindings).EffectId;
        var prior = Assert.Single(before.Effects["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == effectId)!;
        var next = Assert.Single(after.Effects["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == effectId)!;
        var transitions = next["transitions"]!.AsArray();
        Assert.Equal(prior["transitions"]!.AsArray().Count + 1, transitions.Count);
        for (var index = 0; index < transitions.Count - 1; index++)
            Assert.True(JsonNode.DeepEquals(prior["transitions"]![index], transitions[index]));
        var appended = transitions[^1]!;
        var transitionId = appended["transitionId"]!.GetValue<string>();
        Assert.Equal(("trigger", turn), (appended["kind"]!.GetValue<string>(), appended["turn"]!.GetValue<int>()));
        Assert.Null(appended["receiptId"]);
        foreach (var field in new[] { "sourceEffectIds", "resultEffectIds" })
            Assert.Equal(effectId, Assert.Single(appended[field]!.AsArray())!.GetValue<string>());
        var effects = Assert.IsType<EffectAcceptedTurnPlan>(plan.EffectPlan);
        Assert.True(effects.IsAcceptedBoundaryComplete);
        Assert.Empty(effects.AllocatedEffectIds);
        Assert.Equal(transitionId, Assert.Single(effects.AllocatedTransitionIds));
        Assert.DoesNotContain(prior["transitions"]!.AsArray(), row => row!["transitionId"]!.GetValue<string>() == transitionId);
        var target = new JsonObject { ["kind"] = "guardian", ["targetId"] = "guardian_frame" };
        Assert.Equal("chaos_sea", prior["realm"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(target, prior["target"]));
        var ownerEvent = Assert.Single(effects.EventInput["lifecycleEvents"]!.AsArray().OfType<JsonObject>(), row =>
            row["phase"]!.GetValue<string>() == "owner_turn_end" && row["realm"]!.GetValue<string>() == "chaos_sea" &&
            JsonNode.DeepEquals(target, row["target"]));
        var eventRef = $"turn_{turn}:lifecycle:owner_turn_end:chaos_sea:guardian:guardian_frame";
        Assert.Equal((turn, eventRef), (ownerEvent["turn"]!.GetValue<int>(), ownerEvent["eventRef"]!.GetValue<string>()));
        Assert.Equal(EffectAcceptedTurnPlanner.CreateLifecycleResourceTriggerEventRef(eventRef, effectId,
            new ResourcePendingAuthorityBinding("permanent", effectId), "on_owner_turn_end"), appended["eventRef"]!.GetValue<string>());
        // SourceBindings lists applications/changed groups; an unchanged passive source is resolved from full canonical authority.
        var binding = effects.SourceAuthority.ResolveCanonicalBinding(new EffectSourceKey("chaos_sea", "wound",
            before.Wound.WoundId, prior["source"]!["definitionKey"]!.GetValue<string>()), "guardian");
        Assert.True(binding.Success); Assert.NotNull(binding.Source);
        var definition = binding.Source.Definition;
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Assert.Single(before.Wound.Consequences.OwnedEffectSources.Definitions).GetRawText()), definition));
        Assert.Equal("source_bound", definition["lifetime"]!["mode"]!.GetValue<string>());
        Assert.False(Assert.Single(definition["triggers"]!.AsArray())!["consumeUses"]!.GetValue<bool>());
        if (resources is not null)
        {
            var execution = Assert.Single(resources.ResourceTriggerExecutions, row => row.EffectId == effectId);
            Assert.Equal(("on_owner_turn_end", "owner_turn_end", eventRef),
                (execution.TriggerId, execution.EventKind, execution.TriggerEventRef));
            Assert.Equal(appended["eventRef"]!.GetValue<string>(), execution.EventRef);
            Assert.Empty(execution.MutationKeys);
            Assert.NotNull(execution.ComponentIds); Assert.Empty(execution.ComponentIds);
            Assert.Null(execution.RemainingUseBudget);
            var transcript = Assert.IsType<AcceptedEffectBoundaryTranscript>(resources.EffectBoundaryTranscript);
            var accepted = Assert.Single(transcript.AcceptedActivations, row => row.Activation.Stamp.Identity.EffectId == effectId);
            Assert.Equal(new EffectActivationCandidateIdentity(effectId, execution.TriggerId, execution.EventKind,
                execution.EventRef, execution.TriggerEventRef!), accepted.Activation.Stamp.Identity);
            Assert.Equal(new ResourcePendingAuthorityBinding("permanent", effectId), accepted.Activation.Stamp.EffectAuthority);
            Assert.False(accepted.Activation.Stamp.ConsumesUse);
            Assert.Null(accepted.Activation.Stamp.UsesBefore); Assert.Null(accepted.Activation.UsesAfter);
        }
        var restoredIdentity = after.Effects.DeepClone().AsObject();
        Assert.Single(restoredIdentity["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == effectId)!["transitions"] = prior["transitions"]!.DeepClone();
        Assert.True(JsonNode.DeepEquals(before.Effects, restoredIdentity));
        var priorEffect = Assert.Single(ReadGuardianAttributionProfile(before.Profiles)["activeEffects"]!.AsArray())!;
        var currentEffect = Assert.Single(ReadGuardianAttributionProfile(after.Profiles)["activeEffects"]!.AsArray())!;
        var expectedChronology = priorEffect["chronology"]!.DeepClone();
        expectedChronology["lastTransitionId"] = transitionId;
        expectedChronology["lastTransitionTurn"] = turn;
        Assert.True(JsonNode.DeepEquals(expectedChronology, currentEffect["chronology"]));
        var restoredProfiles = after.Profiles.DeepClone().AsObject();
        var restoredGuardian = ReadGuardianAttributionProfile(restoredProfiles);
        Assert.Single(restoredGuardian["activeEffects"]!.AsArray())!["chronology"] = priorEffect["chronology"]!.DeepClone();
        // The caller independently proves exactly the permitted recovery change, or no wound change at all.
        restoredGuardian["activeWounds"] = ReadGuardianAttributionProfile(before.Profiles)["activeWounds"]!.DeepClone();
        var expectedProfiles = before.Profiles.DeepClone().AsObject();
        if (turn == 45)
        {
            // Safe-start ProjectAcceptedRoot first adds the client-owned mirrors from this fixture's zero-currency/progression soul.
            var player = Assert.Single(expectedProfiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(), row =>
                row!["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul")!.AsObject();
            Assert.False(player.ContainsKey("currencies")); Assert.False(player.ContainsKey("progression"));
            player["currencies"] = new JsonObject { ["inkFeathers"] = 0, ["lightSparks"] = 0 };
            player["progression"] = new JsonObject
            {
                ["enlightenment"] = new JsonObject { ["experience"] = 0, ["tier"] = 0 },
                ["radiance"] = new JsonObject { ["experience"] = 0, ["tier"] = 0 }
            };
        }
        var differences = new JsonArray();
        AppendPressurePairJsonDifferences(expectedProfiles, true, restoredProfiles, true, "", differences);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new
        {
            phase = "native_guardian_effect_profile_operands", turn,
            originalProfiles = before.Profiles, expectedProfiles, publishedProfiles = after.Profiles, restoredProfiles, differences
        }));
        Assert.True(JsonNode.DeepEquals(expectedProfiles, restoredProfiles));
    }

}
