using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Desired real player v3 IV comparison and actual/C1, preserving the increasing below-broken contour and complete retained graph.</summary>
    [Fact]
    public async Task SpiritualSingletonIv_PlayerRealOwnerAdvancesAndComposesC1()
    {
        try
        {
            await WithPressureGroupCaseAsync(new("singleton_iv_player_owner", Parents: 1, Ranks: new(4, 3), AdvanceExpected: true), window =>
                AssertSingletonIvOwnerAsync(window.Spec.Name, window.Copy, window.Capture, window.Lease, window.Files,
                    window.Generation, window.Frontier, JsonNode.Parse(Assert.Single(window.ExpectedParentJson))!.AsObject(), guardian: false));
        }
        catch (Exception primary)
        {
            WriteSingletonIvOwnerFailure("singleton_iv_player_owner", primary);
            throw;
        }
    }

    /// <summary>Desired real declared-guardian v5 IV comparison and actual/C1, with exact signed/current opposition lead and I→II→III.</summary>
    [Fact]
    public Task SpiritualSingletonIv_GuardianRealOwnerAdvancesAndComposesC1() =>
        AssertGuardianSingletonOwnerAsync("singleton_iv_guardian_owner", 4);

    /// <summary>Owns one exact guardian singleton comparison/actual/C1 check with full unchanged cleanup.</summary>
    /// <param name="name">Independent fixture identity fixed before signing.</param>
    /// <param name="rank">Independent complete parentIII/IV.</param>
    /// <param name="contour">Closed guardian ordinary/actual destination pair.</param>
    private async Task AssertGuardianSingletonOwnerAsync(string name, int rank,
        GuardianSingletonPressureContour contour = GuardianSingletonPressureContour.BelowBroken)
    {
        try
        {
            await WithGuardianAttributionJourneyAsync(name, async setup =>
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
                        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
                        capture = recorded.Capture;
                        RequireGuardianJourneyIssues(setup.Name, "singleton_iv_intake", recorded.Issues); Assert.NotNull(capture);
                        RequireGuardianJourneyIssues(setup.Name, "singleton_iv_resource_begin", await capture.BeginResourceExecutionAsync(lease));
                        var prepared = await capture.PrepareOriginalResourcePrefixAsync(lease);
                        RequireGuardianJourneyIssues(setup.Name, "singleton_iv_prefix", prepared.Issues);
                        Assert.True(prepared.Ready); Assert.False(prepared.PrefixBlocked);
                        files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                        generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                        var frontier = await ReadPressureOwnerSnapshotAsync(capture, lease, setup.Name + "_before");
                        await AssertSingletonIvOwnerAsync(setup.Name, setup.Copy, capture, lease, files, generation, frontier,
                            Assert.Single(setup.Retained.Parents), guardian: true, expectedParentRank: rank,
                            observeActualRefusal: contour == GuardianSingletonPressureContour.ReachingBroken, guardianContour: contour);
                    },
                    HasWindow: () => files is not null, HasCapture: () => capture is not null, HasLease: () => lease is not null,
                    HasContext: () => false,
                    AssertWindowInert: () => { AssertExistingPressureInputInert(context, lease!, files!, generation!); return Task.CompletedTask; },
                    CaptureDisposalFiles: () => disposalFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath),
                    DisposeCaptureExplicitly: () => capture!.Dispose(),
                    AssertDisposedCaptureInert: () =>
                    {
                        if (disposalFiles is not null) AssertExistingPressureInputInert(context, lease!, disposalFiles, generation!);
                    },
                    DisposeCaptureImplicitly: () => capture!.Dispose(), AssertNoPrivatePair: () => AssertPressureNoPrivatePair(context, lease!),
                    DisposeLease: async () => await lease!.DisposeAsync(), DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
            }, retainedRank: rank, contour: contour);
        }
        catch (Exception primary)
        {
            WriteSingletonIvOwnerFailure(name, primary);
            throw;
        }
    }

    /// <summary>Emits singleton IV owner and journey Facts' retained failures after complete fixture cleanup, without replacing the primary.</summary>
    /// <param name="name">The exact singleton IV owner or journey Fact's fixture identity.</param>
    /// <param name="primary">The unchanged failure after all existing fixture boundaries have completed.</param>
    private void WriteSingletonIvOwnerFailure(string name, Exception primary)
    {
        try
        {
            var dataCount = primary.Data.Count;
            var entries = primary.Data.Cast<System.Collections.DictionaryEntry>().Select(entry => new
            {
                Key = entry.Key?.ToString(), KeyType = entry.Key?.GetType().FullName,
                ValueType = entry.Value?.GetType().FullName,
                Value = entry.Value is Exception retained ? retained.ToString() : entry.Value?.ToString()
            }).ToArray();
            _storageOutput.WriteLine(JsonSerializer.Serialize(new
            {
                name, phase = "singleton_iv_owner_failure", PrimaryType = primary.GetType().FullName,
                PrimaryMessage = primary.Message, PrimaryStackTrace = primary.StackTrace, PrimaryException = primary.ToString(),
                DataCount = dataCount, DataEntries = entries, DataEntriesComplete = entries.Length == dataCount
            }));
        }
        catch (Exception diagnosticError)
        {
            try { RetainPressureFixtureFailure(primary, "singleton_iv_diagnostics", diagnosticError); }
            catch (Exception)
            {
                // Even unavailable diagnostic metadata must not replace the original game/test failure.
                // An absent/incomplete failure block cannot establish cleanup-qualified acceptance.
            }
        }
    }

    /// <summary>
    /// Checks both established singleton owners through real ordinary, warm reuse, actual admission and detached first C1.
    /// It commits no C2 or publication. Expected parents are pre-sign independent DATA, never copied from issued proof.
    /// </summary>
    /// <param name="name">Exact independently owned fixture identity.</param>
    /// <param name="copy">Authored ordinary DATA and genuine fixture context.</param>
    /// <param name="capture">Current original/resource source owner.</param>
    /// <param name="lease">Its current canonical lease.</param>
    /// <param name="files">Complete physical authored before-images.</param>
    /// <param name="generation">Exact authored session generation.</param>
    /// <param name="frontier">Complete current source/resource/effect/allocation frontier before ordinary proof.</param>
    /// <param name="expectedParent">Independent complete pre-sign parent, never copied from returned proof.</param>
    /// <param name="guardian">Existing guardian role, separately bound to its closed contour.</param>
    /// <param name="expectedParentRank">Independent III/IV; old IV default is unchanged.</param>
    /// <param name="playerContour">Closed player contour, with old below-broken default unchanged.</param>
    /// <param name="observeActualRefusal">Opt-in existing frozen pre-revoke observation for desired-success causal diagnostics.</param>
    /// <param name="guardianContour">Closed guardian pair; player callers retain its unchanged default.</param>
    private async Task AssertSingletonIvOwnerAsync(string name, PressureOrdinaryOwnerCopy copy,
        ValidationService.SpiritualOriginalTurnCapture capture, FileSystemManager.CanonicalWriteLease lease,
        IReadOnlyDictionary<string, byte[]> files, byte[] generation,
        ValidationService.SpiritualOrdinaryPressureProbeState frontier, JsonObject expectedParent, bool guardian,
        int expectedParentRank = 4, PlayerSingletonPressureContour playerContour = PlayerSingletonPressureContour.BelowBroken,
        bool observeActualRefusal = false,
        GuardianSingletonPressureContour guardianContour = GuardianSingletonPressureContour.BelowBroken)
    {
        if (expectedParentRank is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(expectedParentRank));
        if (guardian && playerContour != PlayerSingletonPressureContour.BelowBroken)
            throw new ArgumentException("Guardian owner cannot select a player contour.", nameof(playerContour));
        if (!guardian && guardianContour != GuardianSingletonPressureContour.BelowBroken)
            throw new ArgumentException("Player owner cannot select a guardian contour.", nameof(guardianContour));
        var ordinaryStrain = guardian ? SpiritualGuardianAttributionTestData.OrdinaryStrain(guardianContour) : SpiritualSingletonIvPlayerDataTests.OrdinaryStrain(playerContour);
        var actualStrain = guardian ? SpiritualGuardianAttributionTestData.ActualStrain(guardianContour) : SpiritualSingletonIvPlayerDataTests.ActualStrain(playerContour);
        var side = guardian ? "opposition" : "player";
        var field = side + "SideStrain";
        var version = guardian ? 5 : 3;
        var exchangeId = guardian ? "exchange_guardian_attribution_46" : "exchange_pressure_group_46";
        var expectedOrdinary = copy.Ordinary;
        var expectedActual = expectedOrdinary.DeepClone().AsObject(); expectedActual["after"]![field] = actualStrain;
        var actualRoot = copy.Conflict.DeepClone().AsObject();
        actualRoot["activeConflict"]!["exchangeLog"]![0] = expectedActual.DeepClone(); actualRoot["activeConflict"]![field] = actualStrain;
        var ordinaryCalculation = guardian ? GuardianAttributionOrdinaryCalculation(guardianContour) : SpiritualSingletonIvPlayerDataTests.OrdinaryCalculation(playerContour);
        var actualCalculation = guardian ? GuardianAttributionActualCalculation(guardianContour) : SpiritualSingletonIvPlayerDataTests.ActualCalculation(playerContour);
        Assert.Equal(ordinaryCalculation, SpiritualWoundOpportunityMath.Calculate(new(8, 0, 0, guardian ? "strained" : SpiritualSingletonIvPlayerDataTests.BeforeStrain(playerContour), ordinaryStrain, "hostile", 4)));
        Assert.Equal(actualCalculation, SpiritualWoundOpportunityMath.Calculate(new(8, 0, 0, guardian ? "strained" : SpiritualSingletonIvPlayerDataTests.BeforeStrain(playerContour), actualStrain, "hostile", 4)));
        Assert.Equal(expectedParentRank, expectedParent["severity"]!["rank"]!.GetValue<int>());
        Assert.Equal(0, frontier.NextOrdinal); Assert.Empty(capture.ReadClosedExchangeEvidence(lease)); Assert.True(capture.IsCurrentOwner);
        var inventory = capture.ReadC1ImageInventory(lease);
        var before = capture.ReadExistingStrainReuseCounts();
        var first = await Query("cold");
        var cold = capture.ReadExistingStrainReuseCounts();
        var computations = capture.ReadExistingPressureGroupComputationCounts();
        Assert.Equal(before.Queries + 1, cold.Queries);
        Assert.True(cold.FreshInputChecks > before.FreshInputChecks); Assert.True(cold.FreshDependencyChecks > before.FreshDependencyChecks);
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 1), computations.CurrentBatch);
        Assert.Equal(new ValidationService.SpiritualPressureBatchReaderCounts(1, 1, 1, 1, 1), computations.SignedBatch);
        Assert.Equal(1, computations.ResourceBatchComputations);
        Assert.Equal((before.SignedParentReads + 1, before.OrdinaryEvaluations + 1, before.ActualEvaluations),
            (cold.SignedParentReads, cold.OrdinaryEvaluations, cold.ActualEvaluations));
        var detached = first.ReadOrdinaryExchange();
        detached["after"]![field] = ordinaryStrain == "broken" ? "clear" : "broken";
        Assert.NotEqual(ordinaryStrain, detached["after"]![field]!.GetValue<string>());
        var warm = await Query("warm");
        var warmCounts = capture.ReadExistingStrainReuseCounts();
        Assert.Equal(cold.Queries + 1, warmCounts.Queries);
        Assert.Equal((cold.SignedParentReads, cold.OrdinaryEvaluations, cold.ActualEvaluations),
            (warmCounts.SignedParentReads, warmCounts.OrdinaryEvaluations, warmCounts.ActualEvaluations));
        Assert.Equal(computations, capture.ReadExistingPressureGroupComputationCounts());
        Assert.True(warmCounts.FreshInputChecks > cold.FreshInputChecks); Assert.True(warmCounts.FreshDependencyChecks > cold.FreshDependencyChecks);
        if (observeActualRefusal)
        {
            Assert.Null(capture.ReadExistingPressureActualRefusalObservation());
            await capture.RequestExistingPressureActualRefusalObservationAsync(lease);
            Assert.Equal(warmCounts, capture.ReadExistingStrainReuseCounts());
            Assert.Equal(computations, capture.ReadExistingPressureGroupComputationCounts());
        }
        var advanced = await MeasurePressureOwnerPhaseAsync("singleton_iv_actual", name, () => capture.AdvanceNextResourceExchangeAsync(lease));
        Exception? refusalObservationFailure = null;
        if (observeActualRefusal && advanced.Issues.Any(row => row.Severity == IssueSeverity.Error))
        {
            // Diagnose the pre-revoke frozen DATA only. Never query a revoked capture as a current owner.
            refusalObservationFailure = await Record.ExceptionAsync(async () =>
            {
                Assert.Null(advanced.Step);
                var retained = Assert.IsType<ValidationService.SpiritualPressureActualRefusalObservationResult>(capture.ReadExistingPressureActualRefusalObservation());
                Assert.Null(retained.UnavailableReason);
                var observation = Assert.IsType<ValidationService.SpiritualPressureActualRefusalObservation>(retained.Observation);
                AssertPressureOrdinaryFrontiersEqual(frontier, observation.Frontier);
                Assert.Equal(new ValidationService.SpiritualPressureProjectionInventory(0, 0, 0), observation.Projections);
                Assert.NotNull(retained.Issues);
                Assert.Equal(advanced.Issues.Count, retained.Issues.Count);
                for (var index = 0; index < advanced.Issues.Count; index++)
                {
                    var issue = advanced.Issues[index]; var frozen = retained.Issues[index];
                    Assert.Equal((issue.FilePath, issue.Severity, issue.Message, issue.Category, issue.Code, issue.Actor,
                        issue.Section, issue.Expected, issue.Actual, issue.RepairHint),
                        (frozen.FilePath, frozen.Severity, frozen.Message, frozen.Category, frozen.Code, frozen.Actor,
                            frozen.Section, frozen.Expected, frozen.Actual, frozen.RepairHint));
                    Assert.Equal(JsonSerializer.Serialize(issue.RepairTargetFiles), frozen.RepairTargetFilesJson);
                }
                Assert.Equal(warmCounts.OrdinaryEvaluations, capture.ReadExistingStrainReuseCounts().OrdinaryEvaluations);
                Assert.Equal(warmCounts.ActualEvaluations, capture.ReadExistingStrainReuseCounts().ActualEvaluations);
                Assert.Equal(computations, capture.ReadExistingPressureGroupComputationCounts());
                Assert.False(capture.IsCurrentOwner);
                AssertExistingPressureInputInert(copy.Context, lease, files, generation);
                AssertPressureNoPrivatePair(copy.Context, lease);
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(copy.Context.FileSystem, lease));
                Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(copy.Context.FileSystem, lease, out _));
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { name, phase = "singleton_broken_pre_revoke_observation",
                    observation.Frontier, observation.Projections, retained.UnavailableReason,
                    counts = capture.ReadExistingStrainReuseCounts(), sourceCounts = capture.ReadExistingPressureGroupComputationCounts() }));
                await Task.CompletedTask;
            });
        }
        try { RequireGuardianJourneyIssues(name, "singleton_iv_actual", advanced.Issues); }
        catch (Exception primary)
        {
            if (refusalObservationFailure is not null) RetainPressureFixtureFailure(primary, "actual_refusal_observation", refusalObservationFailure);
            throw; // Desired success fails at the unchanged production issue; refusal is never blessed as PASS.
        }
        Assert.Null(refusalObservationFailure);
        Assert.Empty(advanced.Issues); AssertExistingPressureInputInert(copy.Context, lease, files, generation); AssertPressureNoPrivatePair(copy.Context, lease);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(copy.Context.FileSystem, lease));
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
        Assert.Equal((0, exchangeId), (interval.Ordinal, interval.ExchangeId)); Assert.Empty(interval.ReplayTransitions);
        Assert.Equal(2, interval.AppliedTransitions.Count);
        foreach (var expected in warm.ExpectedTransitions)
        {
            var spend = Assert.Single(interval.AppliedTransitions, row => row.Coordinate == expected.Coordinate);
            var maximum = expected.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor ? 10m : 9m;
            Assert.Equal(ResourceTransitionOperation.Spend, spend.Operation);
            Assert.Equal((46, 3m, 3m, 3m, 0m, maximum, maximum), (spend.Turn, spend.RequestedAmount, spend.AppliedAmount,
                spend.BeforeState!.Current, spend.AfterState!.Current, spend.BeforeState.Maximum, spend.AfterState.Maximum));
        }
        var closed = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
        Assert.Equal((interval.ConflictId, exchangeId, 0), (closed.ConflictId, closed.ExchangeId, closed.Ordinal));
        Assert.Equal(new[] { 0, 1 }, closed.DiceClaims.Select(row => row.SourceIndex));
        Assert.Equal(guardian ? new[] { 12, 4 } : [4, 12], closed.DiceClaims.Select(row => row.Value));
        Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(closed.ExchangeJson)));
        var source = Assert.Single(closed.Sources);
        Assert.Equal(("activeConflict.exchangeLog[0]", side, "pressure", guardian ? "player_soul:player_soul" : "guardian:guardian_frame",
            guardian ? "guardian:guardian_frame" : "player_soul:player_soul"),
            (source.Coordinate, source.AffectedSide, source.Operation, source.ActingActor, source.AffectedActor));
        Assert.Equal(actualCalculation, source.Calculation); Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(source.ExchangeJson)));
        Assert.Null(source.OriginalSpecialArtJson); Assert.Null(source.GuaranteedSeverityRank); Assert.Null(source.RetraumaWoundId);
        AssertSingletonIvIssuedProof(JsonNode.Parse(Assert.IsType<string>(source.StrainPreimageJson))!.AsObject(), expectedParent,
            expectedOrdinary, version, new CanonicalBeforeImage(true, files[AfterlifeSpiritualConflictState.StatePath]).Fingerprint, ordinaryCalculation);
        var prefix = capture.ReadClosedResourceCandidatePrefix(lease, interval);
        Assert.Equal(interval.AppliedTransitions, prefix.AppliedTransitions); Assert.Empty(prefix.ReplayTransitions);
        Assert.True(JsonNode.DeepEquals(actualRoot, prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
        var restored = prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath].DeepClone();
        restored["activeConflict"]!["exchangeLog"]![0]!["after"]![field] = ordinaryStrain; restored["activeConflict"]![field] = ordinaryStrain;
        Assert.True(JsonNode.DeepEquals(copy.Conflict, restored)); // Only these two paths may differ; all siblings remain exact.
        if (!guardian && playerContour == PlayerSingletonPressureContour.CappedBroken)
        {
            Assert.True(JsonNode.DeepEquals(expectedOrdinary, expectedActual));
            Assert.True(JsonNode.DeepEquals(copy.Conflict, actualRoot));
            Assert.True(JsonNode.DeepEquals(copy.Conflict, prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
            Assert.NotNull(source.StrainPreimageJson);
            Assert.Equal(ordinaryCalculation, actualCalculation);
        }
        var projection = AfterlifeConflictActionPointProjectionService.Resolve(prefix.DefinitionsJson, prefix.StateJson, actualRoot["activeConflict"]!.AsObject());
        RequireGuardianJourneyIssues(name, "singleton_iv_ap", projection.Issues);
        Assert.Equal((0m, 0m), (projection.Projection!.Player.Current, projection.Projection.Opposition.Current));
        var candidate = await capture.ReadC1CandidateOwnerImagesAsync(lease, interval);
        Assert.True(JsonNode.DeepEquals(actualRoot, ReadGuardianAttributionJson(candidate[AfterlifeSpiritualConflictState.StatePath].Bytes!)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.StateJson), JsonNode.Parse(candidate[ResourceMaterializationContract.StatePath].Bytes!)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.HistoryJson), JsonNode.Parse(candidate[ResourceMaterializationContract.HistoryPath].Bytes!)));
        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, SpiritualWoundOpportunityReceiptState.StatePath })
            Assert.Equal(inventory.BeforeImages[path].Bytes, candidate.TryGetValue(path, out var image) ? image.Bytes : inventory.BeforeImages[path].Bytes);
        var beforeProfiles = ReadGuardianAttributionJson(files[AfterlifeEntityProfileState.StatePath]);
        var afterProfiles = ReadGuardianAttributionJson(candidate[AfterlifeEntityProfileState.StatePath].Bytes!);
        var kind = guardian ? "guardian" : "player_soul";
        JsonNode Profile(JsonObject profiles) => Assert.Single(profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(), row =>
            row!["actorType"]!.GetValue<string>() == kind && row["actorId"]!.GetValue<string>() == (guardian ? "guardian_frame" : "player_soul"))!;
        Assert.True(JsonNode.DeepEquals(expectedParent, Assert.Single(Profile(afterProfiles)["activeWounds"]!.AsArray())));
        var oldEffect = Assert.Single(Profile(beforeProfiles)["activeEffects"]!.AsArray())!;
        var newEffect = Assert.Single(Profile(afterProfiles)["activeEffects"]!.AsArray())!;
        foreach (var preserved in new[] { "components", "triggers", "links", "stacking", "source", "target", "display", "state" })
            Assert.True(JsonNode.DeepEquals(oldEffect[preserved], newEffect[preserved]), preserved);
        Assert.Equal(expectedParentRank, newEffect["components"]!.AsArray().Count);
        var afterActual = capture.ReadExistingStrainReuseCounts();
        Assert.Equal(warmCounts.OrdinaryEvaluations, afterActual.OrdinaryEvaluations);
        Assert.Equal(warmCounts.ActualEvaluations + 1, afterActual.ActualEvaluations);
        Assert.Equal(warmCounts.SignedParentReads, afterActual.SignedParentReads);
        Assert.True(afterActual.FreshInputChecks > warmCounts.FreshInputChecks); Assert.True(afterActual.FreshDependencyChecks > warmCounts.FreshDependencyChecks);
        Assert.Equal(computations with { ResourceBatchComputations = 2 }, capture.ReadExistingPressureGroupComputationCounts());
        Assert.True(capture.IsCurrentOwner);
        var admitted = await capture.AdmitLatestClosedWoundSourceAsync(lease, side);
        RequireGuardianJourneyIssues(name, "singleton_iv_source_admission", admitted.Issues);
        var admission = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.WoundSourceAdmission>(admitted.Admission);
        Assert.True(capture.OwnsWoundAdmission(admission)); Assert.Same(interval, admission.Interval); Assert.Equal(source, admission.Source);
        var offered = await capture.ComposeC1FirstOfferAsync(lease, interval);
        RequireGuardianJourneyIssues(name, "singleton_iv_c1", offered.Issues);
        var packet = JsonNode.Parse(SpiritualWoundDecisionPendingState.SerializeCanonical(Assert.IsType<SpiritualWoundDecisionPendingState>(offered.Pending)))!["pending"]!.AsObject();
        var witness = Assert.Single(packet["sources"]!.AsArray())!.AsObject();
        Assert.Equal(actualCalculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(witness, out var strain));
        Assert.NotNull(strain); Assert.Equal(version, strain.Version); Assert.Single(strain.Parents); Assert.Equal(expectedParentRank, strain.ParentWound.Severity.Rank);
        Assert.Equal(ordinaryCalculation, strain.OrdinaryCalculation); Assert.Equal(actualCalculation, strain.ActualCalculation);
        Assert.True(JsonNode.DeepEquals(expectedParent, JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(strain.ParentWound))));
        Assert.True(JsonNode.DeepEquals(expectedActual, strain.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(expectedOrdinary["after"], strain.ReadOrdinaryAfter(expectedActual)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(source.StrainPreimageJson!), witness["strainPreimage"]));
        var signed = capture.ReadVerifiedSignedC1Origin(lease);
        var evidence = witness["turnEvidence"]!;
        foreach (var key in new[] { "sessionId", "requestId", "snapshotToken", "turn", "realm", "originalSnapshotFingerprint", "bounds" })
            Assert.True(JsonNode.DeepEquals(packet[key], evidence[key]), key);
        Assert.Equal(signed.SnapshotFingerprint, evidence["originalSnapshotFingerprint"]!.GetValue<string>());
        Assert.Equal(WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.spiritual_closed_resource_result", "1", interval.ConflictId, exchangeId, "0",
            JsonSerializer.Serialize(interval.AppliedTransitions), JsonSerializer.Serialize(interval.ReplayTransitions), JsonSerializer.Serialize(interval.Events)
        }), witness["resourceResultFingerprint"]!.GetValue<string>());
        Assert.Equal(WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.spiritual_closed_effect_prefix", "1", admission.Draft.ReadBasePlanPayloadFingerprint(), interval.EffectAfter.Fingerprint
        }), witness["effectPlanFingerprint"]!.GetValue<string>());
        Assert.Equal(SpiritualWoundStateJson.Hash(ReadGuardianAttributionJson(signed.Conflict.Bytes!), "closed_conflict"), witness["conflictBeforeFingerprint"]!.GetValue<string>());
        Assert.Equal(SpiritualWoundStateJson.Hash(actualRoot, "closed_conflict"), witness["conflictAfterFingerprint"]!.GetValue<string>());
        if (guardian) Assert.True(JsonNode.DeepEquals(SpiritualGuardianAttributionTestData.FailedAction(), strain.ReadActualExchange()["spiritualWoundFailedAction"]));
        else Assert.False(strain.ReadActualExchange().ContainsKey("spiritualWoundFailedAction"));
        AssertExistingPressureInputInert(copy.Context, lease, files, generation); AssertPressureNoPrivatePair(copy.Context, lease);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(copy.Context.FileSystem, lease));

        async Task<ValidationService.SpiritualOrdinaryPressureComparisonData> Query(string phase)
        {
            var result = await ExecutePressureGroupComparisonStagesAsync(() => capture.ReadExistingPressureOrdinaryComparisonAsync(lease),
                () => capture.IsCurrentOwner, async () =>
                {
                    AssertPressureOrdinaryFrontiersEqual(frontier, await ReadPressureOwnerSnapshotAsync(capture, lease, name + "_" + phase + "_close"));
                    AssertExistingPressureInputInert(copy.Context, lease, files, generation); AssertPressureNoPrivatePair(copy.Context, lease);
                });
            RequireGuardianJourneyIssues(name, "singleton_iv_" + phase + "_ordinary", result.Issues);
            Assert.Empty(result.Issues); Assert.False(result.PrefixBlocked); Assert.True(capture.IsCurrentOwner);
            var comparison = Assert.IsType<ValidationService.SpiritualOrdinaryPressureComparisonData>(result.Comparison);
            Assert.Equal(ordinaryCalculation, comparison.OrdinaryCalculation); Assert.True(JsonNode.DeepEquals(expectedOrdinary, comparison.ReadOrdinaryExchange()));
            Assert.Equal("/activeConflict/exchangeLog/0", comparison.ExchangePointer);
            Assert.Equal(new CanonicalBeforeImage(true, files[AfterlifeSpiritualConflictState.StatePath]).Fingerprint, comparison.RawInputFingerprint);
            Assert.Equal(2, comparison.ExpectedTransitions.Count);
            foreach (var owner in new[] { ResourceOwnerKind.AfterlifeActor, ResourceOwnerKind.AfterlifeConflictSide })
            {
                var transitionSide = owner == ResourceOwnerKind.AfterlifeActor ? "player" : "opposition";
                var id = owner == ResourceOwnerKind.AfterlifeActor ? "player_soul" : copy.Conflict["activeConflict"]!
                    [AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]!["opposition"]!["resourceOwnerId"]!.GetValue<string>();
                Assert.Equal(new AfterlifeSpiritualConflictResourceOutcome.ExpectedTransition(
                    "turn_46:afterlife_conflict:" + exchangeId + ":" + transitionSide, "afterlife_cost", exchangeId,
                    new("chaos_sea", owner, id, "spiritual_action_points"), ResourceTransitionOperation.Spend, 3m, 0m, 3m, 0m),
                    Assert.Single(comparison.ExpectedTransitions, row => row.Coordinate.OwnerKind == owner));
            }
            return comparison;
        }
    }

    /// <summary>Checks the exact existing singleton wire, full independently authored IV parent, ordinary arithmetic and original raw binding.</summary>
    private static void AssertSingletonIvIssuedProof(JsonObject proof, JsonObject expectedParent, JsonObject ordinary,
        int version, string rawFingerprint, SpiritualWoundCalculation calculation)
    {
        Assert.Equal(new[] { "componentId", "effectId", "inputRoleRevision", "ordinaryCalculation", "ordinaryExchangeJsonBase64", "originalInput", "parentWoundJsonBase64", "version" },
            proof.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(version, proof["version"]!.GetValue<int>());
        Assert.Equal(version == 5 ? SpiritualGuardianAttributionTestData.Role : "existing_pressure_ordinary_v1", proof["inputRoleRevision"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(expectedParent, SpiritualExistingPressureTestData.ReadParent(new JsonObject { ["strainPreimage"] = proof.DeepClone() })));
        Assert.True(JsonNode.DeepEquals(ordinary, SpiritualExistingPressureTestData.ReadOrdinary(new JsonObject { ["strainPreimage"] = proof.DeepClone() })));
        var expectedRoot = expectedParent["consequences"]!["ownedEffectSources"]!["rootBindings"]![0]!;
        Assert.Equal(expectedRoot["effectId"]!.GetValue<string>(), proof["effectId"]!.GetValue<string>()); Assert.Equal("component_001", proof["componentId"]!.GetValue<string>());
        var input = proof["originalInput"]!;
        Assert.Equal(new[] { "contentFingerprint", "exchangePointer", "existed", "path" }, input.AsObject().Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(AfterlifeSpiritualConflictState.StatePath, input["path"]!.GetValue<string>()); Assert.True(input["existed"]!.GetValue<bool>());
        Assert.Equal(rawFingerprint, input["contentFingerprint"]!.GetValue<string>()); Assert.Equal("/activeConflict/exchangeLog/0", input["exchangePointer"]!.GetValue<string>());
        Assert.Equal(new[] { "maximumSeverityRank", "traumaPressure" }, proof["ordinaryCalculation"]!.AsObject().Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal((calculation.TraumaPressure, calculation.MaximumSeverityRank),
            (proof["ordinaryCalculation"]!["traumaPressure"]!.GetValue<long>(), proof["ordinaryCalculation"]!["maximumSeverityRank"]!.GetValue<int>()));
    }
}
