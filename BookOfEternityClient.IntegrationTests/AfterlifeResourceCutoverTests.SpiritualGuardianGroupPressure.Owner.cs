using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Desired genuine mixed N2 ordinary/actual/C1 success; an existing guardian count refusal must fail, never satisfy this leaf.</summary>
    [Fact]
    public Task SpiritualGuardianGroupPressure_N2MixedRealOwnerAdvancesAndComposesC1()
    {
        const string name = "guardian_group_mixed_owner";
        return WithPressureMultiParentFailureAsync(name, () => WithGuardianAttributionJourneyAsync(name, async setup =>
        {
            var context = setup.Copy.Context;
            FileSystemManager.CanonicalWriteLease? lease = null;
            ValidationService.SpiritualOriginalTurnCapture? capture = null;
            IReadOnlyDictionary<string, byte[]>? files = null;
            IReadOnlyDictionary<string, byte[]>? disposalFiles = null;
            byte[]? generation = null;
            await ExecutePressureGroupFixtureStagesAsync(new(
                Work: async () =>
                {
                    lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                    var recorded = await MeasurePressureOwnerPhaseAsync("guardian_group_intake", name,
                        () => context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease));
                    capture = recorded.Capture; // Own cleanup before an assertion can fail.
                    RequireGuardianJourneyIssues(name, "group_intake", recorded.Issues); Assert.NotNull(capture);
                    RequireGuardianJourneyIssues(name, "group_resource_begin", await MeasurePressureOwnerPhaseAsync(
                        "guardian_group_resource_begin", name, () => capture.BeginResourceExecutionAsync(lease)));
                    var prepared = await MeasurePressureOwnerPhaseAsync("guardian_group_original_prefix", name,
                        () => capture.PrepareOriginalResourcePrefixAsync(lease));
                    RequireGuardianJourneyIssues(name, "group_original_prefix", prepared.Issues);
                    Assert.True(prepared.Ready); Assert.False(prepared.PrefixBlocked);
                    files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                    generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                    var frontier = await ReadPressureOwnerSnapshotAsync(capture, lease, name + "_before");
                    Assert.Equal(0, frontier.NextOrdinal); Assert.Empty(capture.ReadClosedExchangeEvidence(lease));
                    Assert.True(capture.IsCurrentOwner);
                    var raw = files[AfterlifeSpiritualConflictState.StatePath];
                    Assert.Equal(setup.Authored[AfterlifeSpiritualConflictState.StatePath], raw);
                    Assert.True(raw.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
                    Assert.True(JsonNode.DeepEquals(setup.Copy.Conflict, ReadGuardianAttributionJson(raw)));
                    var original = ReadGuardianAttributionJson(Assert.IsType<byte[]>(setup.Origin.Tracked[AfterlifeSpiritualConflictState.StatePath]));
                    Assert.Empty(original["activeConflict"]!["exchangeLog"]!.AsArray());
                    Assert.Equal("strained", original["activeConflict"]!["oppositionSideStrain"]!.GetValue<string>());
                    Assert.NotEqual(new CanonicalBeforeImage(true, setup.Origin.Tracked[AfterlifeSpiritualConflictState.StatePath]).Fingerprint,
                        new CanonicalBeforeImage(true, raw).Fingerprint);
                    Assert.Equal(new[] { 3, 4 }, setup.Retained.Parents.Select(row => row["severity"]!["rank"]!.GetValue<int>()));
                    _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_prepared_owner_inputs", name,
                        originalConflictBase64 = Convert.ToBase64String(setup.Origin.Tracked[AfterlifeSpiritualConflictState.StatePath]!),
                        ordinaryRawBase64 = Convert.ToBase64String(raw), ordinaryRoot = setup.Copy.Conflict,
                        rawFingerprint = new CanonicalBeforeImage(true, raw).Fingerprint, frontier,
                        counts = capture.ReadExistingPressureGroupComputationCounts() }));
                    var inventory = capture.ReadC1ImageInventory(lease);
                    var before = capture.ReadExistingStrainReuseCounts();
                    var ordinary = await Query("cold");
                    var cold = capture.ReadExistingStrainReuseCounts();
                    var computations = capture.ReadExistingPressureGroupComputationCounts();
                    Assert.Equal((before.Queries + 1, before.SignedParentReads + 2, before.OrdinaryEvaluations + 1, before.ActualEvaluations),
                        (cold.Queries, cold.SignedParentReads, cold.OrdinaryEvaluations, cold.ActualEvaluations));
                    Assert.True(cold.FreshInputChecks > before.FreshInputChecks);
                    Assert.True(cold.FreshDependencyChecks > before.FreshDependencyChecks);
                    Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 2), computations.CurrentBatch);
                    Assert.Equal(computations.CurrentBatch, computations.SignedBatch);
                    Assert.Equal(1, computations.ResourceBatchComputations);
                    var warm = await Query("warm");
                    var warmCounts = capture.ReadExistingStrainReuseCounts();
                    Assert.Equal(cold.Queries + 1, warmCounts.Queries);
                    Assert.Equal((cold.SignedParentReads, cold.OrdinaryEvaluations, cold.ActualEvaluations),
                        (warmCounts.SignedParentReads, warmCounts.OrdinaryEvaluations, warmCounts.ActualEvaluations));
                    Assert.Equal(computations, capture.ReadExistingPressureGroupComputationCounts());
                    Assert.True(warmCounts.FreshInputChecks > cold.FreshInputChecks);
                    Assert.True(warmCounts.FreshDependencyChecks > cold.FreshDependencyChecks);
                    var advanced = await MeasurePressureOwnerPhaseAsync("guardian_group_actual", name,
                        () => capture.AdvanceNextResourceExchangeAsync(lease));
                    RequireGuardianJourneyIssues(name, "group_actual", advanced.Issues);
                    // Measure actual advancement before admission/C1 run their separate fresh source probes.
                    var afterActual = ReadPhaseCounts("actual_advance");
                    Assert.Equal((warmCounts.OrdinaryEvaluations, warmCounts.ActualEvaluations + 1, warmCounts.SignedParentReads),
                        (afterActual.OrdinaryEvaluations, afterActual.ActualEvaluations, afterActual.SignedParentReads));
                    Assert.True(afterActual.FreshInputChecks > warmCounts.FreshInputChecks);
                    Assert.True(afterActual.FreshDependencyChecks > warmCounts.FreshDependencyChecks);
                    Assert.Equal(computations with { ResourceBatchComputations = 2 }, capture.ReadExistingPressureGroupComputationCounts());
                    var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
                    Assert.Equal((0, setup.ExchangeId), (interval.Ordinal, interval.ExchangeId));
                    Assert.Empty(interval.ReplayTransitions); Assert.Equal(2, interval.AppliedTransitions.Count);
                    foreach (var expected in warm.ExpectedTransitions)
                    {
                        var spend = Assert.Single(interval.AppliedTransitions, row => row.Coordinate == expected.Coordinate);
                        var maximum = expected.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor ? 10m : 9m;
                        Assert.Equal(ResourceTransitionOperation.Spend, spend.Operation);
                        Assert.Equal((46, 3m, 3m, 3m, 0m, maximum, maximum), (spend.Turn, spend.RequestedAmount, spend.AppliedAmount,
                            spend.BeforeState!.Current, spend.AfterState!.Current, spend.BeforeState.Maximum, spend.AfterState.Maximum));
                    }
                    var closed = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
                    Assert.Equal((interval.ConflictId, setup.ExchangeId, 0), (closed.ConflictId, closed.ExchangeId, closed.Ordinal));
                    Assert.Equal(new[] { 0, 1 }, closed.DiceClaims.Select(row => row.SourceIndex));
                    Assert.Equal(new[] { 12, 4 }, closed.DiceClaims.Select(row => row.Value));
                    var expectedActual = setup.Actual;
                    var actualRoot = setup.ActualRoot;
                    Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(closed.ExchangeJson)));
                    var source = Assert.Single(closed.Sources);
                    Assert.Equal(("activeConflict.exchangeLog[0]", "opposition", "pressure", "player_soul:player_soul", "guardian:guardian_frame"),
                        (source.Coordinate, source.AffectedSide, source.Operation, source.ActingActor, source.AffectedActor));
                    Assert.Equal(GuardianGroupOwnerActualCalculation(), source.Calculation);
                    Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(source.ExchangeJson)));
                    Assert.Null(source.OriginalSpecialArtJson); Assert.Null(source.GuaranteedSeverityRank); Assert.Null(source.RetraumaWoundId);
                    var input = new JsonObject { ["path"] = AfterlifeSpiritualConflictState.StatePath, ["existed"] = true,
                        ["contentFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint, ["exchangePointer"] = "/activeConflict/exchangeLog/0" };
                    var expectedProof = SpiritualGuardianGroupPressureProofData.CreateProof(setup.Retained.Parents, setup.Ordinary,
                        input, GuardianAttributionOrdinaryCalculation());
                    // The owner emits canonical JSON inside base64; detached intrinsic input deliberately keeps its original encoding.
                    expectedProof["ordinaryExchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(setup.Ordinary)));
                    for (var index = 0; index < 2; index++)
                    {
                        var expectedParent = WoundMaterializationContract.Parse(setup.Retained.Parents[index].ToJsonString(), "guardian.group.expectedParent");
                        RequireGuardianJourneyIssues(name, "group_expected_parent", expectedParent.Issues);
                        Assert.True(expectedParent.IsValid);
                        expectedProof["parents"]![index]!["parentWoundJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                            WoundMaterializationContract.SerializeCanonical(expectedParent.Wound!)));
                    }
                    Assert.True(JsonNode.DeepEquals(expectedProof, JsonNode.Parse(Assert.IsType<string>(source.StrainPreimageJson))));
                    var prefix = capture.ReadClosedResourceCandidatePrefix(lease, interval);
                    Assert.Equal(interval.AppliedTransitions, prefix.AppliedTransitions); Assert.Empty(prefix.ReplayTransitions);
                    Assert.True(JsonNode.DeepEquals(actualRoot, prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
                    var restored = prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath].DeepClone();
                    restored["activeConflict"]!["exchangeLog"]![0]!["after"]!["oppositionSideStrain"] = "fractured";
                    restored["activeConflict"]!["oppositionSideStrain"] = "fractured";
                    Assert.True(JsonNode.DeepEquals(setup.Copy.Conflict, restored));
                    var balance = AfterlifeConflictActionPointProjectionService.Resolve(prefix.DefinitionsJson, prefix.StateJson,
                        actualRoot["activeConflict"]!.AsObject());
                    RequireGuardianJourneyIssues(name, "group_actual_ap", balance.Issues);
                    Assert.Equal((0m, 0m), (balance.Projection!.Player.Current, balance.Projection.Opposition.Current));
                    var candidate = await capture.ReadC1CandidateOwnerImagesAsync(lease, interval);
                    Assert.True(JsonNode.DeepEquals(actualRoot, ReadGuardianAttributionJson(candidate[AfterlifeSpiritualConflictState.StatePath].Bytes!)));
                    var profiles = ReadGuardianAttributionJson(candidate[AfterlifeEntityProfileState.StatePath].Bytes!);
                    var guardian = ReadGuardianAttributionProfile(profiles);
                    Assert.True(JsonNode.DeepEquals(new JsonArray(setup.Retained.Parents.Select(row => row.DeepClone()).ToArray()), guardian["activeWounds"]));
                    var beforeGuardian = ReadGuardianAttributionProfile(ReadGuardianAttributionJson(files[AfterlifeEntityProfileState.StatePath]));
                    for (var index = 0; index < 2; index++)
                        foreach (var preserved in new[] { "components", "triggers", "links", "stacking", "source", "target", "display", "state" })
                            Assert.True(JsonNode.DeepEquals(beforeGuardian["activeEffects"]![index]![preserved], guardian["activeEffects"]![index]![preserved]), preserved);
                    foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, SpiritualWoundOpportunityReceiptState.StatePath })
                        Assert.Equal(inventory.BeforeImages[path].Bytes, candidate.TryGetValue(path, out var image) ? image.Bytes : inventory.BeforeImages[path].Bytes);
                    var admitted = await capture.AdmitLatestClosedWoundSourceAsync(lease, "opposition");
                    RequireGuardianJourneyIssues(name, "group_source_admission", admitted.Issues);
                    var afterAdmission = ReadPhaseCounts("source_admission");
                    Assert.Equal((afterActual.OrdinaryEvaluations, afterActual.ActualEvaluations + 1, afterActual.SignedParentReads),
                        (afterAdmission.OrdinaryEvaluations, afterAdmission.ActualEvaluations, afterAdmission.SignedParentReads));
                    Assert.Equal(computations with { ResourceBatchComputations = 2 }, capture.ReadExistingPressureGroupComputationCounts());
                    var admission = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.WoundSourceAdmission>(admitted.Admission);
                    Assert.True(capture.OwnsWoundAdmission(admission)); Assert.Same(interval, admission.Interval); Assert.Equal(source, admission.Source);
                    var offered = await MeasurePressureOwnerPhaseAsync("guardian_group_c1", name,
                        () => capture.ComposeC1FirstOfferAsync(lease, interval));
                    RequireGuardianJourneyIssues(name, "group_c1", offered.Issues);
                    var afterC1 = ReadPhaseCounts("c1_first_offer");
                    Assert.Equal((afterAdmission.OrdinaryEvaluations, afterAdmission.ActualEvaluations + 1, afterAdmission.SignedParentReads),
                        (afterC1.OrdinaryEvaluations, afterC1.ActualEvaluations, afterC1.SignedParentReads));
                    Assert.Equal(computations with { ResourceBatchComputations = 2 }, capture.ReadExistingPressureGroupComputationCounts());
                    var packet = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(
                        Assert.IsType<SpiritualWoundDecisionPendingState>(offered.Pending)))!["pending"]!.AsObject();
                    var witness = Assert.Single(packet["sources"]!.AsArray())!.AsObject();
                    Assert.True(JsonNode.DeepEquals(expectedProof, witness["strainPreimage"]));
                    Assert.Equal(GuardianGroupOwnerActualCalculation(), SpiritualWoundSourceWitness.ValidateAndReadStrainData(witness, out var strain));
                    Assert.NotNull(strain); Assert.Equal(7, strain.Version); Assert.Equal(2, strain.Parents.Count);
                    Assert.Equal(GuardianAttributionOrdinaryCalculation(), strain.OrdinaryCalculation);
                    Assert.Equal(GuardianGroupOwnerActualCalculation(), strain.ActualCalculation);
                    Assert.Equal(new CanonicalBeforeImage(true, raw).Fingerprint, strain.OriginalInputFingerprint);
                    Assert.Equal("/activeConflict/exchangeLog/0", strain.OriginalExchangePointer);
                    Assert.True(JsonNode.DeepEquals(expectedActual, strain.ReadActualExchange()));
                    Assert.True(JsonNode.DeepEquals(setup.Ordinary["after"], strain.ReadOrdinaryAfter(expectedActual)));
                    for (var index = 0; index < 2; index++)
                        Assert.True(JsonNode.DeepEquals(setup.Retained.Parents[index], JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(strain.Parents[index].ParentWound))));
                    var counts = capture.ReadExistingStrainReuseCounts();
                    Assert.Equal(afterC1, counts); // Detached witness assertions do not evaluate the owning source again.
                    _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_owner_complete", name,
                        source.StrainPreimageJson, witness, ordinary.OrdinaryCalculation, source.Calculation,
                        counts = new { counts.Queries, counts.SignedParentReads, counts.OrdinaryEvaluations, counts.ActualEvaluations,
                            counts.FreshInputChecks, counts.FreshDependencyChecks }, sourceCounts = capture.ReadExistingPressureGroupComputationCounts() }));
                    AssertExistingPressureInputInert(context, lease, files, generation); AssertPressureNoPrivatePair(context, lease);
                    Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));

                    (int Queries, int SignedParentReads, int OrdinaryEvaluations, int ActualEvaluations,
                        int FreshInputChecks, int FreshDependencyChecks) ReadPhaseCounts(string stage)
                    {
                        var observed = capture.ReadExistingStrainReuseCounts();
                        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_owner_phase_counts", stage,
                            counts = new { observed.Queries, observed.SignedParentReads, observed.OrdinaryEvaluations, observed.ActualEvaluations,
                                observed.FreshInputChecks, observed.FreshDependencyChecks }, sourceCounts = capture.ReadExistingPressureGroupComputationCounts() }));
                        return observed;
                    }

                    async Task<ValidationService.SpiritualOrdinaryPressureComparisonData> Query(string phase)
                    {
                        var result = await MeasurePressureOwnerPhaseAsync("guardian_group_ordinary_" + phase, name,
                            () => ExecutePressureGroupComparisonStagesAsync(() => capture.ReadExistingPressureOrdinaryComparisonAsync(lease),
                                () => capture.IsCurrentOwner, async () =>
                                {
                                    AssertPressureOrdinaryFrontiersEqual(frontier, await ReadPressureOwnerSnapshotAsync(capture, lease, name + "_" + phase));
                                    AssertExistingPressureInputInert(context, lease, files, generation); AssertPressureNoPrivatePair(context, lease);
                                }));
                        var observed = capture.ReadExistingStrainReuseCounts();
                        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_ordinary_result", query = phase,
                            result.PrefixBlocked, comparisonReturned = result.Comparison is not null,
                            issues = result.Issues, currentOwner = capture.IsCurrentOwner,
                            counts = new { observed.Queries, observed.SignedParentReads, observed.OrdinaryEvaluations, observed.ActualEvaluations,
                                observed.FreshInputChecks, observed.FreshDependencyChecks }, sourceCounts = capture.ReadExistingPressureGroupComputationCounts(),
                            reached = "ordinary query returned; batch/resolution counts above are actual observations, not assumed N resolutions" }));
                        RequireGuardianJourneyIssues(name, "group_" + phase + "_ordinary_desired_success", result.Issues);
                        Assert.False(result.PrefixBlocked); Assert.True(capture.IsCurrentOwner);
                        var comparison = Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(result.Comparison);
                        Assert.Equal(GuardianAttributionOrdinaryCalculation(), comparison.OrdinaryCalculation);
                        Assert.True(JsonNode.DeepEquals(setup.Ordinary, comparison.ReadOrdinaryExchange()));
                        Assert.Equal("/activeConflict/exchangeLog/0", comparison.ExchangePointer);
                        Assert.Equal(new CanonicalBeforeImage(true, raw).Fingerprint, comparison.RawInputFingerprint);
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
                        return comparison;
                    }
                },
                HasWindow: () => files is not null, HasCapture: () => capture is not null, HasLease: () => lease is not null,
                HasContext: () => false,
                AssertWindowInert: () => { AssertExistingPressureInputInert(context, lease!, files!, generation!); return Task.CompletedTask; },
                CaptureDisposalFiles: () => disposalFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath),
                DisposeCaptureExplicitly: () => capture!.Dispose(),
                AssertDisposedCaptureInert: () =>
                {
                    Assert.False(capture!.IsCurrentOwner);
                    if (disposalFiles is not null && generation is not null) AssertExistingPressureInputInert(context, lease!, disposalFiles, generation);
                },
                DisposeCaptureImplicitly: () => capture!.Dispose(), AssertNoPrivatePair: () => AssertPressureNoPrivatePair(context, lease!),
                DisposeLease: async () => await lease!.DisposeAsync(), DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
        }, groupContour: GuardianGroupPressureCase.MixedPair));
    }

    /// <summary>Authors the full mixed group's ordinaryII after genuine original signing, retaining explicit exact incoming aliases.</summary>
    private static JsonObject CreateGuardianGroupOwnerOrdinary()
    {
        var ordinary = SpiritualGuardianAttributionTestData.CreateExchange("exchange_guardian_attribution_46", 46,
            "strained", "fractured", declaration: true);
        ordinary["incomingAction"]!["ownerActorType"] = "guardian";
        foreach (var alias in new[] { "actorRef", "ownerActorId", "guardianId", "id" }) ordinary["incomingAction"]![alias] = "guardian_frame";
        return ordinary;
    }

    /// <summary>Independent actualIV literal, not an expectation inferred from returned group contribution counts.</summary>
    private static SpiritualWoundCalculation GuardianGroupOwnerActualCalculation() =>
        new(new(8, 0, 0, "strained", "broken", "hostile", 4), 1, 4, 2, 20, 3, 4, 4, true, 3);

    /// <summary>Records genuine signed original inputs before ordinary raw authoring and before any group owner query.</summary>
    /// <param name="name">Exact per-case request identity.</param>
    /// <param name="signed">Fresh reader of the physically signed original.</param>
    /// <param name="retained">All independent pre-sign parents and creation records.</param>
    /// <param name="ordinarySource">Full comparison-free ordinary source with the signed request/dice identity.</param>
    /// <param name="actualSource">Separate comparison-free actual source, never a substitute for owner eligibility.</param>
    private void WriteGuardianGroupSignedPreflight(string name, PendingTurnSnapshotReadAuthority signed,
        RetainedPressureGroupData retained, JsonObject ordinarySource, JsonObject actualSource) =>
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_genuine_signed_preflight", name,
            signed.SessionId, signed.RequestId, signed.SnapshotToken, signed.TurnNumber, signed.AcceptedD20EventValues,
            originalConflictBase64 = Convert.ToBase64String(signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath)),
            originalProfilesBase64 = Convert.ToBase64String(signed.ReadRequiredBytes(AfterlifeEntityProfileState.StatePath)),
            retained, ordinarySource, actualSource, chronology = "original group/profile/lead signed before ordinary candidate authoring" }));
}
