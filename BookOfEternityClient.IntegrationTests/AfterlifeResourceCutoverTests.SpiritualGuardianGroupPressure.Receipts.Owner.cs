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
    /// Requires genuine mixed N2 cold none/C3 and common unpublished preparation, including the capture-bound receipt join.
    /// Parser, retained-reader and unauthenticated-pair gates must be qualified independently before this source-owner gate.
    /// </summary>
    /// <returns>The one independently owned desired-success journey, including failure-preserving root cleanup.</returns>
    [Fact]
    public Task SpiritualGuardianGroupPressure_N2ColdNonePreparesUnpublished()
        => WithGuardianGroupRegisteredPlanAsync("guardian_group_cold_none_unpublished");

    /// <summary>Shares the genuine cold N2 journey; publication callbacks run only after all preparation owners close.</summary>
    private Task WithGuardianGroupRegisteredPlanAsync(string name,
        Func<GuardianAttributionJourney, GuardianAttributionCompleted, AcceptedMechanicsPlan, Task>? afterPreparation = null,
        FileSystemManagerHooks? hooks = null)
    {
        return WithPressureMultiParentFailureAsync(name, () => WithGuardianAttributionJourneyAsync(name, async setup =>
        {
            var context = setup.Copy.Context;
            FileSystemManager.CanonicalWriteLease? lease = null;
            FileSystemManager.CanonicalWriteLease? retiredWarmLease = null;
            ValidationService.SpiritualOriginalTurnCapture? warm = null;
            ValidationService.SpiritualC2PrivateSession? choice = null;
            ValidationService.SpiritualC2PrivateSession? completed = null;
            GuardianAttributionCompleted? completion = null;
            IReadOnlyDictionary<string, byte[]>? durableBaseline = null;
            IReadOnlyDictionary<string, byte[]>? disposalFiles = null;
            IReadOnlyDictionary<string, byte[]>? completedTransport = null;
            byte[]? generation = null;
            var firstCommits = 0;
            var explicitColdOpens = 0;
            var replayObservations = 0;
            var noneSubmissions = 0;
            var c3Reductions = 0;
            await ExecuteGuardianGroupReceiptStagesAsync(name, "warm_to_cold_c3", new(
                Work: async () =>
                {
                    lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                    var recorded = await MeasurePressureOwnerPhaseAsync("guardian_group_receipt_intake", name,
                        () => context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease));
                    warm = recorded.Capture; // Retain cleanup ownership before inspecting the result.
                    RequireGuardianJourneyIssues(name, "receipt_intake", recorded.Issues);
                    Assert.NotNull(warm);
                    RequireGuardianJourneyIssues(name, "receipt_resource_begin", await warm.BeginResourceExecutionAsync(lease));
                    var prepared = await MeasurePressureOwnerPhaseAsync("guardian_group_receipt_prefix", name,
                        () => warm.PrepareOriginalResourcePrefixAsync(lease));
                    RequireGuardianJourneyIssues(name, "receipt_original_prefix", prepared.Issues);
                    Assert.True(prepared.Ready); Assert.False(prepared.PrefixBlocked);
                    var files = durableBaseline = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                    generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                    var raw = files[AfterlifeSpiritualConflictState.StatePath];
                    Assert.Equal(setup.Authored[AfterlifeSpiritualConflictState.StatePath], raw);
                    Assert.True(raw.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
                    var frontier = await ReadPressureOwnerSnapshotAsync(warm, lease, name + "_before");
                    Assert.Equal(0, frontier.NextOrdinal); Assert.Empty(warm.ReadClosedExchangeEvidence(lease));
                    var inventory = warm.ReadC1ImageInventory(lease);
                    var expectedProof = CreateGuardianGroupReceiptExpectedProof(setup, raw);
                    _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_original_operands", name,
                        originalProfilesBase64 = Convert.ToBase64String(setup.Origin.Tracked[AfterlifeEntityProfileState.StatePath]!),
                        originalConflictBase64 = Convert.ToBase64String(setup.Origin.Tracked[AfterlifeSpiritualConflictState.StatePath]!),
                        originalHistoryBase64 = Convert.ToBase64String(setup.Origin.Tracked[WoundHistoryState.HistoryPath]!),
                        originalReceiptJson = setup.Origin.ReceiptJson, ordinaryRawBase64 = Convert.ToBase64String(raw),
                        ordinaryRoot = setup.Copy.Conflict, expectedActual = setup.ActualRoot, expectedProof,
                        parents = setup.Retained.Parents, effects = setup.Retained.Effects }));
                    var originalHistory = ReadGuardianGroupReceiptOriginalHistory(setup);

                    // Only immutable DATA is reused inside this warm owner's ValidationOperation. No owner crosses cold disposal.
                    var beforeOrdinary = ReadPhaseCounts("before_ordinary");
                    var ordinary = await QueryOrdinaryAsync("cold");
                    var coldCounts = ReadPhaseCounts("ordinary_cold");
                    var coldComputations = warm.ReadExistingPressureGroupComputationCounts();
                    Assert.Equal((beforeOrdinary.Queries + 1, beforeOrdinary.SignedParentReads + 2,
                        beforeOrdinary.OrdinaryEvaluations + 1, beforeOrdinary.ActualEvaluations),
                        (coldCounts.Queries, coldCounts.SignedParentReads, coldCounts.OrdinaryEvaluations, coldCounts.ActualEvaluations));
                    Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 2), coldComputations.CurrentBatch);
                    Assert.Equal(coldComputations.CurrentBatch, coldComputations.SignedBatch);
                    Assert.Equal(1, coldComputations.ResourceBatchComputations);
                    var repeated = await QueryOrdinaryAsync("warm");
                    var warmCounts = ReadPhaseCounts("ordinary_warm");
                    Assert.Equal(coldCounts.Queries + 1, warmCounts.Queries);
                    Assert.Equal((coldCounts.SignedParentReads, coldCounts.OrdinaryEvaluations, coldCounts.ActualEvaluations),
                        (warmCounts.SignedParentReads, warmCounts.OrdinaryEvaluations, warmCounts.ActualEvaluations));
                    Assert.True(warmCounts.FreshInputChecks > coldCounts.FreshInputChecks);
                    Assert.True(warmCounts.FreshDependencyChecks > coldCounts.FreshDependencyChecks);
                    Assert.Equal(coldComputations, warm.ReadExistingPressureGroupComputationCounts());
                    Assert.Equal(ordinary.OrdinaryCalculation, repeated.OrdinaryCalculation);

                    var advanced = await MeasurePressureOwnerPhaseAsync("guardian_group_receipt_actual", name,
                        () => warm.AdvanceNextResourceExchangeAsync(lease));
                    RequireGuardianJourneyIssues(name, "receipt_actual", advanced.Issues);
                    var afterActual = ReadPhaseCounts("actual_advance"); // Before admission/C1's separate fresh source probes.
                    Assert.Equal((warmCounts.OrdinaryEvaluations, warmCounts.ActualEvaluations + 1, warmCounts.SignedParentReads),
                        (afterActual.OrdinaryEvaluations, afterActual.ActualEvaluations, afterActual.SignedParentReads));
                    Assert.True(afterActual.FreshInputChecks > warmCounts.FreshInputChecks);
                    Assert.True(afterActual.FreshDependencyChecks > warmCounts.FreshDependencyChecks);
                    Assert.Equal(coldComputations with { ResourceBatchComputations = 2 }, warm.ReadExistingPressureGroupComputationCounts());
                    var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
                    Assert.Equal((0, setup.ExchangeId), (interval.Ordinal, interval.ExchangeId));
                    Assert.Empty(interval.ReplayTransitions); Assert.Equal(2, interval.AppliedTransitions.Count);
                    foreach (var expected in ordinary.ExpectedTransitions)
                    {
                        var spend = Assert.Single(interval.AppliedTransitions, row => row.Coordinate == expected.Coordinate);
                        var maximum = expected.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor ? 10m : 9m;
                        Assert.Equal(ResourceTransitionOperation.Spend, spend.Operation);
                        Assert.Equal((46, 3m, 3m, 3m, 0m, maximum, maximum), (spend.Turn, spend.RequestedAmount,
                            spend.AppliedAmount, spend.BeforeState!.Current, spend.AfterState!.Current,
                            spend.BeforeState.Maximum, spend.AfterState.Maximum));
                    }
                    var closed = Assert.Single(warm.ReadClosedExchangeEvidence(lease));
                    Assert.Equal((interval.ConflictId, setup.ExchangeId, 0), (closed.ConflictId, closed.ExchangeId, closed.Ordinal));
                    Assert.Equal(new[] { 0, 1 }, closed.DiceClaims.Select(row => row.SourceIndex));
                    Assert.Equal(new[] { 12, 4 }, closed.DiceClaims.Select(row => row.Value));
                    Assert.True(JsonNode.DeepEquals(setup.Actual, JsonNode.Parse(closed.ExchangeJson)));
                    var source = Assert.Single(closed.Sources);
                    Assert.Equal(("activeConflict.exchangeLog[0]", "opposition", "pressure", "player_soul:player_soul", "guardian:guardian_frame"),
                        (source.Coordinate, source.AffectedSide, source.Operation, source.ActingActor, source.AffectedActor));
                    Assert.Equal(GuardianGroupOwnerActualCalculation(), source.Calculation);
                    Assert.True(JsonNode.DeepEquals(setup.Actual, JsonNode.Parse(source.ExchangeJson)));
                    Assert.Null(source.OriginalSpecialArtJson); Assert.Null(source.GuaranteedSeverityRank);
                    Assert.Null(source.RetraumaWoundId); Assert.Null(source.RetraumaWoundJson);
                    Assert.True(JsonNode.DeepEquals(expectedProof, JsonNode.Parse(source.StrainPreimageJson!)));
                    var prefix = warm.ReadClosedResourceCandidatePrefix(lease, interval);
                    AssertGuardianAttributionOnlyProjections(setup, prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]);
                    var ap = AfterlifeConflictActionPointProjectionService.Resolve(prefix.DefinitionsJson, prefix.StateJson,
                        setup.ActualRoot["activeConflict"]!.AsObject());
                    RequireGuardianJourneyIssues(name, "receipt_actual_ap", ap.Issues);
                    Assert.Equal((0m, 0m), (ap.Projection!.Player.Current, ap.Projection.Opposition.Current));
                    var candidate = await warm.ReadC1CandidateOwnerImagesAsync(lease, interval);
                    Assert.True(JsonNode.DeepEquals(setup.ActualRoot,
                        ReadGuardianAttributionJson(candidate[AfterlifeSpiritualConflictState.StatePath].Bytes!)));
                    var profiles = ReadGuardianAttributionJson(candidate[AfterlifeEntityProfileState.StatePath].Bytes!);
                    Assert.True(JsonNode.DeepEquals(new JsonArray(setup.Retained.Parents.Select(row => row.DeepClone()).ToArray()),
                        ReadGuardianAttributionProfile(profiles)["activeWounds"]));
                    foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, SpiritualWoundOpportunityReceiptState.StatePath })
                        Assert.Equal(inventory.BeforeImages[path].Bytes,
                            candidate.TryGetValue(path, out var image) ? image.Bytes : inventory.BeforeImages[path].Bytes);

                    var admitted = await warm.AdmitLatestClosedWoundSourceAsync(lease, "opposition");
                    RequireGuardianJourneyIssues(name, "receipt_source_admission", admitted.Issues);
                    ReadPhaseCounts("source_admission");
                    var admission = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.WoundSourceAdmission>(admitted.Admission);
                    Assert.True(warm.OwnsWoundAdmission(admission)); Assert.Same(interval, admission.Interval); Assert.Equal(source, admission.Source);
                    var opportunityResult = await warm.ReadWoundOpportunityAsync(lease, admission);
                    RequireGuardianJourneyIssues(name, "receipt_opportunity", opportunityResult.Issues);
                    Assert.Null(opportunityResult.Satisfaction);
                    var opportunity = Assert.IsType<WoundOpportunityAuthority>(opportunityResult.Opportunity);
                    Assert.Null(opportunity.WorseningTarget); Assert.Null(opportunity.RequiredSeverityRank);
                    var offered = await warm.ComposeC1FirstOfferAsync(lease, interval);
                    RequireGuardianJourneyIssues(name, "receipt_first_c1", offered.Issues);
                    ReadPhaseCounts("c1_first_offer");
                    var firstPacket = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(
                        Assert.IsType<SpiritualWoundDecisionPendingState>(offered.Pending)))!["pending"]!.AsObject();
                    var firstWitness = Assert.Single(firstPacket["sources"]!.AsArray())!.AsObject();
                    AssertGuardianGroupReceiptWitness(setup, firstWitness, expectedProof);
                    AssertGuardianGroupReceiptFirstPacket(setup, firstPacket);
                    var signedOrigin = warm.ReadVerifiedSignedC1Origin(lease);
                    Assert.Equal(signedOrigin.SnapshotFingerprint, firstWitness["turnEvidence"]!["originalSnapshotFingerprint"]!.GetValue<string>());
                    Assert.Equal(WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
                    {
                        "book_of_eternity.spiritual_closed_resource_result", "1", interval.ConflictId, interval.ExchangeId, "0",
                        JsonSerializer.Serialize(interval.AppliedTransitions), JsonSerializer.Serialize(interval.ReplayTransitions), JsonSerializer.Serialize(interval.Events)
                    }), firstWitness["resourceResultFingerprint"]!.GetValue<string>());
                    Assert.Equal(WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
                    {
                        "book_of_eternity.spiritual_closed_effect_prefix", "1", admission.Draft.ReadBasePlanPayloadFingerprint(), interval.EffectAfter.Fingerprint
                    }), firstWitness["effectPlanFingerprint"]!.GetValue<string>());
                    Assert.Equal(SpiritualWoundStateJson.Hash(ReadGuardianAttributionJson(signedOrigin.Conflict.Bytes!), "closed_conflict"),
                        firstWitness["conflictBeforeFingerprint"]!.GetValue<string>());
                    Assert.Equal(SpiritualWoundStateJson.Hash(setup.ActualRoot, "closed_conflict"), firstWitness["conflictAfterFingerprint"]!.GetValue<string>());
                    Assert.Equal(inventory.RegisteredPaths.Count, firstPacket["bounds"]!["imagePathCount"]!.GetValue<int>());
                    AssertPressurePairPacketImages(firstPacket["beforeImages"]!.AsArray(), inventory.RegisteredPaths, inventory.BeforeImages, "original_before");
                    var expectedCandidates = inventory.BeforeImages.ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
                    foreach (var row in candidate) expectedCandidates[row.Key] = row.Value;
                    AssertPressurePairPacketImages(firstPacket["candidateAfterImages"]!.AsArray(), inventory.RegisteredPaths, expectedCandidates, "candidate_after");
                    AssertExistingPressureInputInert(context, lease, files, generation);
                    AssertPressureNoPrivatePair(context, lease);
                    Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));

                    firstCommits++;
                    var committed = await MeasurePressureOwnerPhaseAsync("guardian_group_receipt_first_c2", name,
                        () => warm.CommitC2FirstTransportAsync(lease, interval));
                    RequireGuardianJourneyIssues(name, "receipt_first_c2", committed.Issues);
                    var expectedCold = ReadExpectedSpiritualColdOfferData(warm, lease, committed, source);
                    Assert.Equal(raw, Convert.FromBase64String(expectedCold.OriginalConflictBytesBase64));
                    var committedPacket = JsonNode.Parse(expectedCold.ReconstructedPendingJson)!["pending"]!.AsObject();
                    Assert.True(JsonNode.DeepEquals(firstPacket, committedPacket));
                    ReadPhaseCounts("first_c2_committed");
                    _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_first_c2_operands", name,
                        source = JsonNode.Parse(SerializeSpiritualColdSourceData(source)), firstPacket, committedPacket, expectedCold,
                        appliedTransitions = interval.AppliedTransitions, replayTransitions = interval.ReplayTransitions,
                        prefix.DefinitionsJson, prefix.StateJson, prefix.HistoryJson, prefix.CompanionAfterImages }));
                    AssertPressurePrivateTransportOnly(context, files, generation);
                    var firstTransport = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                    warm.Dispose(); Assert.False(warm.IsCurrentOwner);
                    retiredWarmLease = lease;
                    await retiredWarmLease.DisposeAsync();
                    Assert.False(retiredWarmLease.IsActive);
                    lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                    Assert.NotSame(retiredWarmLease, lease); Assert.True(lease.IsActive);
                    AssertExistingPressureInputInert(context, lease, firstTransport, generation);
                    await Assert.ThrowsAsync<ObjectDisposedException>(async () => await warm.ReadExistingPressureOrdinaryProbeStateAsync(lease));

                    await ExecuteGuardianColdSessionStagesAsync(async () =>
                    {
                        var coldValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
                        Assert.NotSame(context.Validator, coldValidator);
                        explicitColdOpens++;
                        var opened = await MeasurePressureOwnerPhaseAsync("guardian_group_receipt_cold_open", name,
                            () => coldValidator.OpenC2PrivateSessionAsync(lease));
                        choice = opened.Session;
                        RequireGuardianJourneyIssues(name, "receipt_cold_open", opened.Issues);
                        Assert.Equal("offer", opened.Disposition); Assert.NotNull(choice);
                        Assert.False(warm.IsCurrentOwner); Assert.False(retiredWarmLease.IsActive);
                        var offer = Assert.IsType<ValidationService.SpiritualC2PrivateOffer>(choice.Offer);
                        Assert.Equal(opportunity.PublicRef, offer.OpportunityRef);
                        Assert.Equal((1, 3), (offer.MinimumSeverityRank, offer.MaximumSeverityRank));
                        Assert.Null(offer.RequiredSeverityRank); Assert.Equal(new[] { "none", "materialize" }, offer.AllowedDecisions);
                        var coldFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                        replayObservations++;
                        var observed = await MeasurePressureOwnerPhaseAsync("guardian_group_receipt_cold_observation", name,
                            () => choice.ReadOfferReplayEvidenceAsync(lease));
                        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_cold_operands", name, offer, observed,
                            warmOwnerCurrent = warm.IsCurrentOwner, warmLeaseActive = retiredWarmLease.IsActive,
                            freshValidator = !ReferenceEquals(context.Validator, coldValidator), freshLease = !ReferenceEquals(retiredWarmLease, lease) }));
                        AssertSpiritualColdOfferEvidence(expectedCold, observed);
                        AssertExistingPressureInputInert(context, lease, coldFiles, generation);
                        await ExecuteGuardianColdSessionStagesAsync(async () =>
                        {
                            noneSubmissions++;
                            var declined = await MeasurePressureOwnerPhaseAsync("guardian_group_receipt_none", name,
                                () => choice.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
                                    { opportunityRef = offer.OpportunityRef, decision = "none" }), null));
                            completed = declined.Session;
                            RequireGuardianJourneyIssues(name, "receipt_none", declined.Issues);
                            Assert.Equal("completed_unpublished", declined.Disposition); Assert.NotNull(completed); Assert.Null(completed.Offer);
                            var pending = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease,
                                SpiritualWoundDecisionPendingState.StatePath))!)!["pending"]!.AsObject();
                            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_none_packet", name, pending }));
                            AssertGuardianGroupReceiptNonePacket(firstPacket, pending, offer.OpportunityRef);
                            completedTransport = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                            c3Reductions++;
                            var reduced = await MeasurePressureOwnerPhaseAsync("guardian_group_receipt_authenticated_c3", name,
                                () => completed.ReduceCompletedDeclinesAsync(lease));
                            // Preserve actual issues and operands before asserting the desired identity-bearing success.
                            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_c3_operands", name,
                                reduced.Success, reduced.Issues, reduced.ReceiptAfterImage, receiptProofPresent = reduced.ReceiptProof is not null,
                                requiresReceipt = reduced.Reduction?.RequiresSatisfactionReceipt,
                                hasJoinedReceipt = reduced.Reduction?.HasLiveDecisionReceipt,
                                inputFingerprint = reduced.Reduction?.InputFingerprint,
                                companionAfterImages = reduced.Reduction?.OwnerCompanionAfterImages,
                                resourceResult = reduced.Reduction is { } diagnosticReduction ? new
                                {
                                    stateJson = diagnosticReduction.Resources.StateAfterImage?.ToCanonicalJson(),
                                    historyJson = diagnosticReduction.Resources.HistoryAfterImage?.ToCanonicalJson(),
                                    diagnosticReduction.Resources.AppliedTransitions, diagnosticReduction.Resources.ReplayTransitions,
                                    diagnosticReduction.Resources.Events,
                                    resourceTriggerExecutions = diagnosticReduction.Resources.ResourceTriggerExecutions.Select(row => new
                                    {
                                        row.EffectId, row.TriggerId, row.EventKind, row.EventRef, row.MutationKeys,
                                        row.RemainingUseBudget, row.ComponentIds, row.TriggerEventRef,
                                        componentIdsByMutation = row.ComponentIdsByMutation?.Select(pair => new { pair.Key, pair.Value })
                                    }),
                                    diagnosticReduction.Resources.AcceptedResolvedPendingRequestIds, diagnosticReduction.Resources.Issues
                                } : null,
                                effectPayload = reduced.Reduction?.Effects is { } diagnosticEffects
                                    ? WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(diagnosticEffects) : null }));
                            RequireGuardianJourneyIssues(name, "receipt_c3_desired_success", reduced.Issues);
                            Assert.True(reduced.Success); Assert.NotNull(reduced.ReceiptProof);
                            var reduction = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(reduced.Reduction);
                            Assert.True(reduction.RequiresSatisfactionReceipt); Assert.True(reduction.HasLiveDecisionReceipt);
                            Assert.False(reduction.HasLiveWoundWork); Assert.Null(reduction.LiveWoundCompletion);
                            var receipt = Assert.IsType<JsonObject>(reduced.ReceiptAfterImage);
                            var receiptBefore = new CanonicalBeforeImage(true, setup.Origin.Tracked[SpiritualWoundOpportunityReceiptState.StatePath]);
                            // The real completed-reduction consumer has already checked its private capture identity against this proof.
                            Assert.False(reduced.ReceiptProof!.Matches(null, null, receiptBefore, receipt));
                            Assert.Equal(receiptBefore.Fingerprint, reduction.Input.BeforeImages[SpiritualWoundOpportunityReceiptState.StatePath].Fingerprint);
                            Assert.True(JsonNode.DeepEquals(receipt, reduction.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath]));
                            AssertGuardianAttributionOnlyProjections(setup, reduction.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]);
                            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.StateJson), JsonNode.Parse(reduction.Resources.StateAfterImage!.ToCanonicalJson())));
                            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.HistoryJson), JsonNode.Parse(reduction.Resources.HistoryAfterImage!.ToCanonicalJson())));
                            Assert.Equal(prefix.AppliedTransitions, reduction.Resources.AppliedTransitions);
                            Assert.Empty(reduction.Resources.ReplayTransitions); Assert.Equal(interval.Events, reduction.Resources.Events);
                            var effects = Assert.IsType<EffectAcceptedTurnPlan>(reduction.Effects);
                            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_complete_effect_operands", name,
                                effects.InputFingerprint, effects.CarrierAuthorityFingerprint, effects.SourceAuthorityFingerprint,
                                effects.TargetAuthorityFingerprint, effects.EventInput, effects.ActiveEffects,
                                effects.CarrierBeforeImages, effects.CarrierAfterImages, effects.IdentityIndexBeforeImage,
                                effects.IdentityIndexAfterImage, effects.Sources, effects.Targets,
                                effects.AllocatedEffectIds, effects.AllocatedTransitionIds, effects.TouchedPaths, effects.DeletedPaths,
                                effects.AcceptedBoundaryBasePlanFingerprint, effects.AcceptedBoundaryFinalPlanFingerprint }));
                            Assert.True(effects.IsAcceptedBoundaryComplete); Assert.False(effects.HasPressureConflictCarrierCollision());
                            AssertGuardianGroupReceiptAppendAndReplay(setup, receipt, firstWitness, originalHistory);
                            AssertExistingPressureInputInert(context, lease, completedTransport, generation);
                            AssertPressurePrivateTransportOnly(context, files, generation);
                            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                            completion = new(setup.ActualRoot.ToJsonString(), firstWitness.ToJsonString(), receipt.ToJsonString(),
                                reduction.Resources, WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(effects));
                        }, () => completed);
                    }, () => choice);
                    await AssertPressureGroupRetiredOwnersAsync(warm, choice!, completed!, lease);
                    AssertExistingPressureInputInert(context, lease, completedTransport!, generation);
                    _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_completed_phase_counts", name,
                        firstCommits, explicitColdOpens, replayObservations, noneSubmissions, c3Reductions }));
                    Assert.Equal((1, 1, 1, 1, 1), (firstCommits, explicitColdOpens, replayObservations, noneSubmissions, c3Reductions));

                    // Reads actual warm-phase counters without assuming reuse across the subsequent cold owner.
                    (int Queries, int SignedParentReads, int OrdinaryEvaluations, int ActualEvaluations,
                        int FreshInputChecks, int FreshDependencyChecks) ReadPhaseCounts(string stage)
                    {
                        var counts = warm.ReadExistingStrainReuseCounts();
                        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_warm_source_counts", name, stage,
                            counts = new { counts.Queries, counts.SignedParentReads, counts.OrdinaryEvaluations, counts.ActualEvaluations,
                                counts.FreshInputChecks, counts.FreshDependencyChecks }, sourceCounts = warm.ReadExistingPressureGroupComputationCounts() }));
                        return counts;
                    }

                    // Executes a genuine ordinary query inside this one warm owner's stable window.
                    async Task<ValidationService.SpiritualOrdinaryPressureComparisonData> QueryOrdinaryAsync(string phase)
                    {
                        var query = await MeasurePressureOwnerPhaseAsync("guardian_group_receipt_ordinary_" + phase, name,
                            () => warm.ReadExistingPressureOrdinaryComparisonAsync(lease));
                        RequireGuardianJourneyIssues(name, "receipt_ordinary_" + phase, query.Issues);
                        Assert.False(query.PrefixBlocked); Assert.True(warm.IsCurrentOwner);
                        var comparison = Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(query.Comparison);
                        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_ordinary_operands", name,
                            queryPhase = phase, comparison.OrdinaryCalculation, ordinary = comparison.ReadOrdinaryExchange(),
                            comparison.RawInputFingerprint, comparison.ExchangePointer, comparison.ExpectedTransitions }));
                        Assert.Equal(GuardianAttributionOrdinaryCalculation(), comparison.OrdinaryCalculation);
                        Assert.True(JsonNode.DeepEquals(setup.Ordinary, comparison.ReadOrdinaryExchange()));
                        Assert.Equal(new CanonicalBeforeImage(true, raw).Fingerprint, comparison.RawInputFingerprint);
                        Assert.Equal("/activeConflict/exchangeLog/0", comparison.ExchangePointer);
                        Assert.Equal(2, comparison.ExpectedTransitions.Count);
                        foreach (var owner in new[] { ResourceOwnerKind.AfterlifeActor, ResourceOwnerKind.AfterlifeConflictSide })
                        {
                            var side = owner == ResourceOwnerKind.AfterlifeActor ? "player" : "opposition";
                            var id = owner == ResourceOwnerKind.AfterlifeActor ? "player_soul" : setup.Copy.Conflict["activeConflict"]!
                                [AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]!["opposition"]!["resourceOwnerId"]!.GetValue<string>();
                            Assert.Equal(new AfterlifeSpiritualConflictResourceOutcome.ExpectedTransition(
                                "turn_46:afterlife_conflict:" + setup.ExchangeId + ":" + side, "afterlife_cost", setup.ExchangeId,
                                new("chaos_sea", owner, id, "spiritual_action_points"), ResourceTransitionOperation.Spend, 3m, 0m, 3m, 0m),
                                Assert.Single(comparison.ExpectedTransitions, row => row.Coordinate.OwnerKind == owner));
                        }
                        AssertPressureOrdinaryFrontiersEqual(frontier, await ReadPressureOwnerSnapshotAsync(warm, lease, name + "_" + phase));
                        AssertExistingPressureInputInert(context, lease, files, generation); AssertPressureNoPrivatePair(context, lease);
                        return comparison;
                    }
                },
                HasWindow: () => durableBaseline is not null && generation is not null,
                HasCapture: () => warm is not null, HasLease: () => lease is not null, HasContext: () => false,
                AssertWindowInert: () =>
                {
                    AssertPressurePrivateTransportOnly(context, durableBaseline!, generation!);
                    if (completedTransport is not null) AssertGuardianGroupReceiptExactFiles(context, completedTransport, generation!);
                    return Task.CompletedTask;
                },
                CaptureDisposalFiles: () => disposalFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath),
                DisposeCaptureExplicitly: () => warm!.Dispose(),
                AssertDisposedCaptureInert: () =>
                {
                    Assert.False(warm!.IsCurrentOwner);
                    if (disposalFiles is not null) AssertGuardianGroupReceiptExactFiles(context, disposalFiles, generation!);
                },
                DisposeCaptureImplicitly: () => warm!.Dispose(), AssertNoPrivatePair: () => { },
                DisposeLease: async () => await lease!.DisposeAsync(), DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
            Assert.False(lease!.IsActive); Assert.False(retiredWarmLease!.IsActive);
            var detached = Assert.IsType<GuardianAttributionCompleted>(completion);
            // Registration only. No normalization/publication, signed-next snapshot or C4 cleanup is performed.
            RequireGuardianJourneyIssues(name, "receipt_common_unpublished_plan", await MeasurePressureOwnerPhaseAsync(
                "guardian_group_receipt_common_preparation", name, () => context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync()));
            var plan = await PeekPlanAsync(context);
            AssertGuardianAttributionRegisteredPlan(setup, detached, plan);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_common_registered_unpublished", name,
                plan.OwnerCompanionAfterImages, plan.StateAfterImage, plan.HistoryAfterImage, plan.ResourceEvents,
                effectPayload = WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(Assert.IsType<EffectAcceptedTurnPlan>(plan.EffectPlan)) }));
            await ExecuteGuardianLeaseStagesAsync(context, current =>
            {
                AssertGuardianGroupReceiptExactFiles(context, completedTransport!, generation!);
                Assert.True(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, current));
                return Task.CompletedTask;
            });
            if (afterPreparation is not null) await afterPreparation(setup, detached, plan);
        }, hooks: hooks, groupContour: GuardianGroupPressureCase.MixedPair));
    }
}
