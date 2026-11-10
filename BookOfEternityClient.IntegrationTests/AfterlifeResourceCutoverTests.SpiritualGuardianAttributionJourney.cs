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
    /// <summary>Reconstructed v5 guardian ordinary/actual/C1, real first C2, one cold Open, none/C3, common C4 and signed-next journey.</summary>
    [Fact]
    public Task SpiritualGuardianAttribution_ActualColdNonePublishesOnceAndValidatesSignedNext() =>
        WithGuardianAttributionJourneyAsync("guardian_attribution_publication", async setup =>
        {
            var completed = await CompleteGuardianAttributionColdC3Async(setup, setup.Copy.Context.Validator);
            await PublishGuardianAttributionAndReadNextAsync(setup, setup.Copy.Context.Validator, completed);
        });

    /// <summary>Detached completion DATA; no live owner, lease, admission or publication permission leaves the phase.</summary>
    private sealed record GuardianAttributionCompleted(string ActualRootJson, string WitnessJson, string ReceiptJson,
        AcceptedMechanicsResourcePlanningResult Resources, string EffectPayloadFingerprint);

    /// <summary>Owns real intake through C3. Fixed cleanup stages retain the primary failure and every eligible cleanup error.</summary>
    private async Task<GuardianAttributionCompleted> CompleteGuardianAttributionColdC3Async(GuardianAttributionJourney setup,
        ValidationService validator,
        Action<ValidationService.SpiritualOriginalTurnCapture, ValidationService.SpiritualC2PrivateSession,
            ValidationService.SpiritualC2PrivateSession>? retainDisposedForRevocationCheck = null,
        GuardianCappedAttemptCapture? cappedAttempt = null)
    {
        var context = setup.Copy.Context;
        if (cappedAttempt is not null)
        { Assert.True(setup.IsGuardian); Assert.Equal(GuardianSingletonPressureContour.CappedBroken, setup.GuardianContour); }
        FileSystemManager.CanonicalWriteLease? warmPhaseLease = null;
        FileSystemManager.CanonicalWriteLease? lease = null;
        ValidationService.SpiritualOriginalTurnCapture? warm = null;
        ValidationService.SpiritualC2PrivateSession? choice = null;
        ValidationService.SpiritualC2PrivateSession? completed = null;
        GuardianAttributionCompleted? result = null;
        var firstCommits = 0;
        var explicitColdOpens = 0;
        var noneSubmissions = 0;
        await ExecutePressureGroupFixtureStagesAsync(new(
            Work: async () =>
            {
                lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                var recorded = await MeasurePressureOwnerPhaseAsync("guardian_journey_intake", setup.Name,
                    () => validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease));
                warm = recorded.Capture; // Retain disposal ownership before assertions.
                RequireGuardianJourneyIssues(setup.Name, "intake", recorded.Issues);
                Assert.NotNull(warm);
                RequireGuardianJourneyIssues(setup.Name, "resource_begin", await warm.BeginResourceExecutionAsync(lease));
                var prepared = await MeasurePressureOwnerPhaseAsync("guardian_journey_original_prefix", setup.Name,
                    () => warm.PrepareOriginalResourcePrefixAsync(lease));
                RequireGuardianJourneyIssues(setup.Name, "original_prefix", prepared.Issues);
                Assert.True(prepared.Ready); Assert.False(prepared.PrefixBlocked);
                var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                var frontier = await ReadPressureOwnerSnapshotAsync(warm, lease, setup.Name + "_guardian_before");
                Assert.Equal(0, frontier.NextOrdinal);
                Assert.Empty(warm.ReadClosedExchangeEvidence(lease));
                var inventory = warm.ReadC1ImageInventory(lease);
                var authoredRaw = setup.Authored[AfterlifeSpiritualConflictState.StatePath]!;
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "singleton_journey_original_raw_input", setup.Name,
                    setup.Role, setup.ExpectedRank, setup.Version,
                    bytesBase64 = Convert.ToBase64String(authoredRaw),
                    fingerprint = new CanonicalBeforeImage(true, authoredRaw).Fingerprint }));
                var comparison = await AssertGuardianAttributionRepeatedQueriesAsync(setup, warm, lease, frontier, files, generation);
                var beforeActual = warm.ReadExistingStrainReuseCounts();
                var sourceBeforeActual = warm.ReadExistingPressureGroupComputationCounts();
                var advanced = await MeasurePressureOwnerPhaseAsync("guardian_journey_actual_admission", setup.Name,
                    () => warm.AdvanceNextResourceExchangeAsync(lease));
                RequireGuardianJourneyIssues(setup.Name, "actual_admission", advanced.Issues);
                AssertExistingPressureInputInert(context, lease, files, generation);
                AssertPressureNoPrivatePair(context, lease);
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
                Assert.Equal((0, setup.ExchangeId), (interval.Ordinal, interval.ExchangeId));
                Assert.Empty(interval.ReplayTransitions);
                Assert.Equal(2, interval.AppliedTransitions.Count);
                foreach (var expected in comparison.ExpectedTransitions)
                {
                    var spend = Assert.Single(interval.AppliedTransitions, row => row.Coordinate == expected.Coordinate);
                    var maximum = expected.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor ? 10m : 9m;
                    Assert.Equal(ResourceTransitionOperation.Spend, spend.Operation);
                    Assert.Equal((46, 3m, 3m, 3m, 0m, maximum, maximum),
                        (spend.Turn, spend.RequestedAmount, spend.AppliedAmount, spend.BeforeState!.Current,
                            spend.AfterState!.Current, spend.BeforeState.Maximum, spend.AfterState.Maximum));
                }
                var closed = Assert.Single(warm.ReadClosedExchangeEvidence(lease));
                Assert.Equal((interval.ConflictId, interval.ExchangeId, 0), (closed.ConflictId, closed.ExchangeId, closed.Ordinal));
                Assert.Equal(new[] { 0, 1 }, closed.DiceClaims.Select(row => row.SourceIndex));
                Assert.Equal(setup.Dice, closed.DiceClaims.Select(row => row.Value));
                Assert.True(JsonNode.DeepEquals(setup.Actual, JsonNode.Parse(closed.ExchangeJson)));
                var source = Assert.Single(closed.Sources);
                Assert.Equal(("activeConflict.exchangeLog[0]", setup.AffectedSide, "pressure", setup.ActingKind + ":" + setup.ActingId, setup.AffectedKind + ":" + setup.AffectedId),
                    (source.Coordinate, source.AffectedSide, source.Operation, source.ActingActor, source.AffectedActor));
                Assert.Equal(setup.ActualCalculation, source.Calculation);
                Assert.True(JsonNode.DeepEquals(setup.Actual, JsonNode.Parse(source.ExchangeJson)));
                Assert.Null(source.OriginalSpecialArtJson); Assert.Null(source.GuaranteedSeverityRank); Assert.Null(source.RetraumaWoundId);
                AssertGuardianAttributionProof(setup, JsonNode.Parse(Assert.IsType<string>(source.StrainPreimageJson))!.AsObject());
                var afterActual = warm.ReadExistingStrainReuseCounts();
                Assert.Equal(beforeActual.OrdinaryEvaluations, afterActual.OrdinaryEvaluations);
                Assert.Equal(beforeActual.ActualEvaluations + 1, afterActual.ActualEvaluations);
                Assert.Equal(beforeActual.SignedParentReads, afterActual.SignedParentReads);
                Assert.True(afterActual.FreshInputChecks > beforeActual.FreshInputChecks);
                Assert.True(afterActual.FreshDependencyChecks > beforeActual.FreshDependencyChecks);
                var sourceAfterActual = warm.ReadExistingPressureGroupComputationCounts();
                Assert.Equal(sourceBeforeActual with { ResourceBatchComputations = sourceBeforeActual.ResourceBatchComputations + 1 }, sourceAfterActual);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "singleton_journey_actual_reuse", setup.Name,
                    beforeActual = new { beforeActual.Queries, beforeActual.SignedParentReads, beforeActual.OrdinaryEvaluations,
                        beforeActual.ActualEvaluations, beforeActual.FreshInputChecks, beforeActual.FreshDependencyChecks },
                    afterActual = new { afterActual.Queries, afterActual.SignedParentReads, afterActual.OrdinaryEvaluations,
                        afterActual.ActualEvaluations, afterActual.FreshInputChecks, afterActual.FreshDependencyChecks },
                    sourceBeforeActual, sourceAfterActual }));
                var prefix = warm.ReadClosedResourceCandidatePrefix(lease, interval);
                AssertGuardianAttributionOnlyProjections(setup, prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]);
                var projected = AfterlifeConflictActionPointProjectionService.Resolve(prefix.DefinitionsJson, prefix.StateJson,
                    setup.ActualRoot["activeConflict"]!.AsObject());
                RequireGuardianJourneyIssues(setup.Name, "actual_ap_projection", projected.Issues);
                Assert.Equal((0m, 0m), (projected.Projection!.Player.Current, projected.Projection.Opposition.Current));
                var candidate = await warm.ReadC1CandidateOwnerImagesAsync(lease, interval);
                Assert.True(JsonNode.DeepEquals(setup.ActualRoot, ReadGuardianAttributionJson(candidate[AfterlifeSpiritualConflictState.StatePath].Bytes!)));
                foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
                    Assert.Equal(setup.Origin.Tracked[path], candidate.TryGetValue(path, out var image) ? image.Bytes : inventory.BeforeImages[path].Bytes);
                var admitted = await warm.AdmitLatestClosedWoundSourceAsync(lease, setup.AffectedSide);
                RequireGuardianJourneyIssues(setup.Name, "source_admission", admitted.Issues);
                var admission = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.WoundSourceAdmission>(admitted.Admission);
                Assert.True(warm.OwnsWoundAdmission(admission)); Assert.Same(interval, admission.Interval); Assert.Equal(source, admission.Source);
                var opportunityResult = await warm.ReadWoundOpportunityAsync(lease, admission);
                RequireGuardianJourneyIssues(setup.Name, "opportunity", opportunityResult.Issues);
                Assert.Null(opportunityResult.Satisfaction);
                var opportunity = Assert.IsType<WoundOpportunityAuthority>(opportunityResult.Opportunity);
                Assert.Null(opportunity.WorseningTarget); Assert.Null(opportunity.RequiredSeverityRank);
                var offered = await warm.ComposeC1FirstOfferAsync(lease, interval);
                RequireGuardianJourneyIssues(setup.Name, "first_c1", offered.Issues);
                var packet = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(
                    Assert.IsType<SpiritualWoundDecisionPendingState>(offered.Pending)))!["pending"]!.AsObject();
                var firstWitness = Assert.Single(packet["sources"]!.AsArray())!.AsObject();
                AssertGuardianAttributionWitness(setup, firstWitness);
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(source.StrainPreimageJson!), firstWitness["strainPreimage"]));
                var signedOrigin = warm.ReadVerifiedSignedC1Origin(lease);
                var evidence = firstWitness["turnEvidence"]!.AsObject();
                foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "turn", "realm", "originalSnapshotFingerprint", "bounds" })
                    Assert.True(JsonNode.DeepEquals(packet[field], evidence[field]), field);
                Assert.Equal(signedOrigin.SnapshotFingerprint, evidence["originalSnapshotFingerprint"]!.GetValue<string>());
                Assert.True(JsonNode.DeepEquals(packet["diceClaims"], evidence["diceClaims"]));
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
                Assert.Equal(SpiritualWoundStateJson.Hash(new JsonObject { ["dangerMode"] = "hostile" }, "danger_declaration"),
                    firstWitness["dangerDeclarationFingerprint"]!.GetValue<string>());
                Assert.Equal((1, 2, 2, inventory.RegisteredPaths.Count),
                    (packet["bounds"]!["exchangeCount"]!.GetValue<int>(), packet["bounds"]!["sourceSlotCount"]!.GetValue<int>(),
                        packet["bounds"]!["diceCount"]!.GetValue<int>(), packet["bounds"]!["imagePathCount"]!.GetValue<int>()));
                Assert.Equal((1, 0, 0), (packet["cursor"]!["exchangeOrdinal"]!.GetValue<int>(),
                    packet["cursor"]!["waveOrdinal"]!.GetValue<int>(), packet["cursor"]!["nextSourceOrdinal"]!.GetValue<int>()));
                Assert.Empty(packet["stagedDecisions"]!.AsArray());
                AssertPressurePairPacketImages(packet["beforeImages"]!.AsArray(), inventory.RegisteredPaths, inventory.BeforeImages, "original_before");
                var expectedCandidates = inventory.BeforeImages.ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
                foreach (var row in candidate) expectedCandidates[row.Key] = row.Value;
                AssertPressurePairPacketImages(packet["candidateAfterImages"]!.AsArray(), inventory.RegisteredPaths, expectedCandidates, "candidate_after");
                AssertExistingPressureInputInert(context, lease, files, generation);
                var commitStarted = Stopwatch.GetTimestamp();
                firstCommits++;
                var committed = await warm.CommitC2FirstTransportAsync(lease, interval);
                RequireGuardianJourneyIssues(setup.Name, "first_c2_commit", committed.Issues);
                var expectedCold = ReadExpectedSpiritualColdOfferData(warm, lease, committed, source);
                Assert.Equal(setup.Authored[AfterlifeSpiritualConflictState.StatePath], Convert.FromBase64String(expectedCold.OriginalConflictBytesBase64));
                var witness = Assert.Single(JsonNode.Parse(expectedCold.ReconstructedPendingJson)!["pending"]!["sources"]!.AsArray())!.AsObject();
                Assert.True(JsonNode.DeepEquals(firstWitness, witness));
                AssertGuardianAttributionWitness(setup, witness);
                if (cappedAttempt is not null)
                    await CaptureGuardianCappedFirstAsync(setup, cappedAttempt, warm, lease, interval, source, firstWitness,
                        SpiritualWoundDecisionPendingState.SerializeCanonical(offered.Pending!), committed);
                warm.Dispose(); Assert.False(warm.IsCurrentOwner);
                if (cappedAttempt is not null)
                {
                    // Only the capped normal caller closes the physical warm lease before a genuinely fresh cold owner.
                    warmPhaseLease = lease;
                    await warmPhaseLease.DisposeAsync();
                    Assert.False(warmPhaseLease.IsActive);
                    lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                    Assert.NotSame(warmPhaseLease, lease);
                    Assert.Equal(authoredRaw, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
                }
                WritePressureOwnerTiming("guardian_journey_first_c2_commit", setup.Name, commitStarted);

                // Each cold handle has its own accepted fixed cleanup scope. No Dispose can replace an earlier failure.
                await ExecuteGuardianColdSessionStagesAsync(async () =>
                {
                    var coldStarted = Stopwatch.GetTimestamp();
                    explicitColdOpens++;
                    var coldValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
                    if (cappedAttempt is not null) Assert.NotSame(validator, coldValidator);
                    var opened = await coldValidator.OpenC2PrivateSessionAsync(lease); // Exactly one explicit cold Open for this attempt.
                    choice = opened.Session;
                    RequireGuardianJourneyIssues(setup.Name, "cold_open", opened.Issues);
                    Assert.Equal("offer", opened.Disposition);
                    Assert.NotNull(choice);
                    if (cappedAttempt is not null)
                    { Assert.False(warm.IsCurrentOwner); Assert.False(warmPhaseLease!.IsActive); Assert.NotSame(warmPhaseLease, lease); }
                    var offer = Assert.IsType<ValidationService.SpiritualC2PrivateOffer>(choice.Offer);
                    Assert.Equal(opportunity.PublicRef, offer.OpportunityRef);
                    Assert.Equal((1, setup.ActualCalculation.MaximumSeverityRank), (offer.MinimumSeverityRank, offer.MaximumSeverityRank));
                    Assert.Null(offer.RequiredSeverityRank);
                    Assert.Equal(new[] { "none", "materialize" }, offer.AllowedDecisions);
                    var coldFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                    var coldGeneration = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                    WritePressureOwnerTiming("guardian_journey_single_cold_open", setup.Name, coldStarted);
                    var observationStarted = Stopwatch.GetTimestamp();
                    var observed = await choice.ReadOfferReplayEvidenceAsync(lease);
                    AssertSpiritualColdOfferEvidence(expectedCold, observed);
                    AssertExistingPressureInputInert(context, lease, coldFiles, coldGeneration);
                    WritePressureOwnerTiming("guardian_journey_complete_cold_observation", setup.Name, observationStarted);
                    await ExecuteGuardianColdSessionStagesAsync(async () =>
                    {
                        var c3Started = Stopwatch.GetTimestamp();
                        noneSubmissions++;
                        var declined = await choice.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
                            { opportunityRef = offer.OpportunityRef, decision = "none" }), null);
                        completed = declined.Session;
                        RequireGuardianJourneyIssues(setup.Name, "none_decision", declined.Issues);
                        Assert.Equal("completed_unpublished", declined.Disposition); Assert.NotNull(completed); Assert.Null(completed.Offer);
                        var pending = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, SpiritualWoundDecisionPendingState.StatePath))!)!["pending"]!;
                        Assert.Equal(1, pending["cursor"]!["nextSourceOrdinal"]!.GetValue<int>());
                        var staged = Assert.Single(pending["stagedDecisions"]!.AsArray())!;
                        Assert.Equal((offer.OpportunityRef, witness["sourceId"]!.GetValue<string>(), "none"),
                            (staged["opportunityRef"]!.GetValue<string>(), staged["sourceId"]!.GetValue<string>(), staged["decision"]!.GetValue<string>()));
                        foreach (var field in new[] { "selectedSeverityRank", "woundDraftBase64", "woundDraftFingerprint" }) Assert.Null(staged[field]);
                        var reduced = await completed.ReduceCompletedDeclinesAsync(lease);
                        RequireGuardianJourneyIssues(setup.Name, "c3_reduction", reduced.Issues);
                        Assert.True(reduced.Success); Assert.NotNull(reduced.ReceiptProof);
                        var reduction = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(reduced.Reduction);
                        Assert.True(reduction.RequiresSatisfactionReceipt); Assert.True(reduction.HasLiveDecisionReceipt);
                        Assert.False(reduction.HasLiveWoundWork); Assert.Null(reduction.LiveWoundCompletion);
                        Assert.False(reduced.ReceiptProof!.Matches(null, null,
                            new CanonicalBeforeImage(true, setup.Origin.Tracked[SpiritualWoundOpportunityReceiptState.StatePath]), reduced.ReceiptAfterImage!));
                        AssertGuardianAttributionOnlyProjections(setup, reduction.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]);
                        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.StateJson), JsonNode.Parse(reduction.Resources.StateAfterImage!.ToCanonicalJson())));
                        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.HistoryJson), JsonNode.Parse(reduction.Resources.HistoryAfterImage!.ToCanonicalJson())));
                        Assert.Equal(prefix.AppliedTransitions, reduction.Resources.AppliedTransitions);
                        Assert.Empty(reduction.Resources.ReplayTransitions); Assert.Equal(interval.Events, reduction.Resources.Events);
                        var effects = Assert.IsType<EffectAcceptedTurnPlan>(reduction.Effects);
                        Assert.True(effects.IsAcceptedBoundaryComplete); Assert.False(effects.HasPressureConflictCarrierCollision());
                        AssertGuardianAttributionReceiptAppend(setup, reduced.ReceiptAfterImage!, witness);
                        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
                            Assert.Equal(setup.Origin.Tracked[path], await context.FileSystem.ReadFileBytesAsync(lease, path));
                        Assert.Equal(setup.Authored[AfterlifeSpiritualConflictState.StatePath],
                            await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
                        AssertPressurePrivateTransportOnly(context, files, generation);
                        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                        result = new(setup.ActualRoot.ToJsonString(), witness.ToJsonString(), reduced.ReceiptAfterImage!.ToJsonString(),
                            reduction.Resources, WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(effects));
                        if (cappedAttempt is not null)
                            await CaptureGuardianCappedCompletedAsync(setup, cappedAttempt, lease, observed, reduced, reduction, new
                            { warmCaptureCurrent = warm.IsCurrentOwner, warmLeaseActive = warmPhaseLease!.IsActive,
                                freshValidator = !ReferenceEquals(validator, coldValidator), freshLease = !ReferenceEquals(warmPhaseLease, lease) });
                        WritePressureOwnerTiming("guardian_journey_none_and_c3", setup.Name, c3Started);
                    }, () => completed);
                }, () => choice);
                await AssertPressureGroupRetiredOwnersAsync(warm, choice!, completed!, lease);
                retainDisposedForRevocationCheck?.Invoke(warm, choice!, completed!);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "singleton_journey_phase_counts", setup.Name,
                    firstCommits, explicitColdOpens, noneSubmissions }));
                Assert.Equal((1, 1, 1), (firstCommits, explicitColdOpens, noneSubmissions));
            },
            HasWindow: () => false, HasCapture: () => warm is not null, HasLease: () => lease is not null, HasContext: () => false,
            AssertWindowInert: () => Task.CompletedTask, CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => { },
            AssertDisposedCaptureInert: () => { }, DisposeCaptureImplicitly: () => warm!.Dispose(),
            // The completed private pair is intentionally retained DATA for common C4, not an owner carried across phases.
            AssertNoPrivatePair: () => { }, DisposeLease: async () => await lease!.DisposeAsync(),
            DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
        return Assert.IsType<GuardianAttributionCompleted>(result);
    }

    /// <summary>Uses the accepted fixed cleanup mechanism for one exact private session, retaining earlier work failures.</summary>
    private static Task ExecuteGuardianColdSessionStagesAsync(Func<Task> work,
        Func<ValidationService.SpiritualC2PrivateSession?> session) => ExecutePressureGroupFixtureStagesAsync(new(
        Work: work, HasWindow: () => false, HasCapture: () => session() is not null, HasLease: () => false, HasContext: () => false,
        AssertWindowInert: () => Task.CompletedTask, CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => { },
        AssertDisposedCaptureInert: () => { }, DisposeCaptureImplicitly: () => session()!.Dispose(), AssertNoPrivatePair: () => { },
        DisposeLease: () => ValueTask.CompletedTask, DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));

    /// <summary>Writes every issue field before any semantic assertion; diagnostics have no count or byte cutoff.</summary>
    private void RequireGuardianJourneyIssues(string name, string phase, IEnumerable<ValidationIssue> issues)
    {
        var rows = issues.ToArray();
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { name, phase, issues = rows.Select(issue => new
        {
            issue.FilePath, issue.Severity, issue.Message, issue.Category, issue.Code, issue.Actor, issue.Section,
            issue.Expected, issue.Actual, issue.RepairHint, issue.RepairTargetFiles
        }) }));
        AssertNoConflictFrameErrors(rows);
    }
}
