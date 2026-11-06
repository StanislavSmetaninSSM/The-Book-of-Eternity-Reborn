using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Requires genuine ordinary-to-actual pressure through cold C2, explicit none/C3, common C4 and signed45.</summary>
    [Fact]
    public async Task SpiritualExistingPressure_OrdinaryInputMustAdvanceOneActualPressureExchange()
        => await WithSignedPressureParentBelowCapAsync([4, 12], "request_existing_pressure_ordinary_44", async context =>
        {
            var started = Stopwatch.GetTimestamp();
            var signed = (await context.ReadJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!;
            var request = (await context.ReadJsonAsync(LiveTurnPreparationService.TurnRequestPath))!;
            Assert.Equal(44, signed["turnNumber"]!.GetValue<int>());
            Assert.Equal(44, request["turnNumber"]!.GetValue<int>());
            Assert.Equal("request_existing_pressure_ordinary_44", signed["requestId"]!.GetValue<string>());
            Assert.Equal(signed["requestId"]!.GetValue<string>(), request["requestId"]!.GetValue<string>());
            Assert.Equal(new[] { 4, 12 }, signed["preGeneratedDices1d20"]!.AsArray().Select(row => row!.GetValue<int>()));
            Assert.Equal(new[] { 4, 12 }, request["preGeneratedDices1d20"]!.AsArray().Select(row => row!.GetValue<int>()));

            // These are the actual published parent/root/resource images, signed before any third draft.
            foreach (var path in new[] { AfterlifeSpiritualConflictState.StatePath, AfterlifeEntityProfileState.StatePath,
                         ValidationService.SpiritualWoundSourceSession.SoulPath, WoundIdentityState.StatePath,
                         WoundHistoryState.HistoryPath, EffectIdentityState.StatePath,
                         ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                         SpiritualWoundOpportunityReceiptState.StatePath })
            {
                var current = await context.FileSystem.ReadFileBytesAsync(path);
                Assert.NotNull(current);
                Assert.Equal(current, await context.FileSystem.ReadFileBytesAsync(signed["files"]![path]!.GetValue<string>()));
            }
            var parent = await ReadCombatHealingWoundAsync(context);
            Assert.Equal(3, parent.Severity.Rank);
            var mechanics = await EffectMechanicsSnapshot.LoadAsync(context.FileSystem);
            Assert.True(mechanics.IsAccepted, string.Join("\n", mechanics.Issues));
            var pressure = Assert.Single(mechanics.Components, row => row.Profile == "spiritual_strain_burden" &&
                row.Payload.GetProperty("operation").GetString() == "pressure");
            Assert.Equal(("chaos_sea", "player", "player_soul", 1),
                (pressure.Realm, pressure.TargetKind, pressure.TargetId, pressure.CurrentStacks));
            Assert.Equal(1, pressure.Payload.GetProperty("magnitude").GetInt32());
            Assert.Equal("wound", pressure.Source!.Kind);
            Assert.Equal(parent.WoundId, pressure.Source.SourceId);
            Assert.Contains(parent.Consequences.OwnedEffectSources.RootBindings, row => row.EffectId == pressure.EffectId);

            var original = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
            var active = original["activeConflict"]!.AsObject();
            Assert.False(original.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
            Assert.Equal(2, active["exchangeLog"]!.AsArray().Count);
            Assert.Equal("strained", active["playerSideStrain"]!.GetValue<string>());
            Assert.Equal("clear", active["oppositionSideStrain"]!.GetValue<string>());
            var definitions = (await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!.ToJsonString();
            var resources = (await context.ReadJsonAsync(ResourceMaterializationContract.StatePath))!.ToJsonString();
            var balance = AfterlifeConflictActionPointProjectionService.Resolve(definitions, resources, active);
            AssertNoConflictFrameErrors(balance.Issues);
            Assert.Equal((3m, 10m, 3m, 9m), (balance.Projection!.Player.Current, balance.Projection.Player.Maximum,
                balance.Projection.Opposition.Current, balance.Projection.Opposition.Maximum));
            var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!["profiles"]!.AsArray();
            var guardian = profiles.Single(row => row!["actorId"]!.GetValue<string>() == "guardian_frame")!;
            var player = profiles.Single(row => row!["actorId"]!.GetValue<string>() == "player_soul")!;
            var soul = (await context.ReadJsonAsync(ValidationService.SpiritualWoundSourceSession.SoulPath))!;
            var appliedTier = guardian["standardArts"]!["pressure"]!.GetValue<int>();
            var resilienceTier = soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]![
                AfterlifeSpiritualConflictState.SpiritualResilienceArtId]!.GetValue<int>();
            Assert.Equal(resilienceTier, player["standardArts"]![AfterlifeSpiritualConflictState.SpiritualResilienceArtId]!.GetValue<int>());
            Assert.Equal((0, 0), (appliedTier, resilienceTier)); // SpiritFocus3 supplies capacity, not either standard-art tier.

            var ordinary = await AppendExistingPressureOrdinaryDraftAsync(context, original);
            var retainedIssues = new List<ValidationIssue>();
            ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "ordinaryPressure", retainedIssues);
            ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "ordinaryPressure", retainedIssues);
            ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary,
                new JsonObject { ["acceptedD20Values"] = signed["preGeneratedDices1d20"]!.DeepClone() }, retainedIssues);
            ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, retainedIssues);
            AssertNoConflictFrameErrors(retainedIssues);
            // Real public shape/dice/rules/cost validation is a precondition, not the missing isolated ordinary owner proof.
            var ordinaryIssues = await ValidatePublishedConflictPhaseAsync(context);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "existing_pressure_ordinary_validation", issues = ordinaryIssues }));
            AssertNoConflictFrameErrors(ordinaryIssues);
            var ordinaryCalculation = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(new(
                8, appliedTier, resilienceTier, "strained", "fractured", active["dangerMode"]!.GetValue<string>(), 4)));
            Assert.Equal((10L, 1), (ordinaryCalculation.TraumaPressure, ordinaryCalculation.MaximumSeverityRank));
            var desiredCalculation = Assert.IsType<SpiritualWoundCalculation>(SpiritualWoundOpportunityMath.Calculate(
                ordinaryCalculation.Input with { NewStrain = "overwhelmed" }));
            Assert.Equal((15L, 2), (desiredCalculation.TraumaPressure, desiredCalculation.MaximumSeverityRank));

            var parentFingerprint = WoundIdentityState.ComputeSemanticFingerprint(parent);
            var parentJson = WoundMaterializationContract.SerializeCanonical(parent);
            var parentRoots = parent.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId).ToArray();
            var preserved = await CaptureExistingStrainCanonicalAsync(context);
            var receiptBefore = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.DeepClone();
            Assert.Single(receiptBefore["sources"]!.AsArray());
            Assert.Single(receiptBefore["decisions"]!.AsArray());
            var identityBefore = (await context.ReadJsonAsync(EffectIdentityState.StatePath))!.DeepClone().AsObject();
            var expectedActualRoot = original.DeepClone().AsObject();
            expectedActualRoot["activeConflict"]!["playerSideStrain"] = "overwhelmed";
            expectedActualRoot["activeConflict"]!["exchangeLog"]![2]!["after"]!["playerSideStrain"] = "overwhelmed";
            var originalOrdinaryBytes = (await context.FileSystem.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath))!;
            var originalOrdinaryFingerprint = new CanonicalBeforeImage(true, originalOrdinaryBytes).Fingerprint;
            string firstSourceJson;
            AcceptedMechanicsResourcePlanningResult completedResources;
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                var absent = new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                    SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath };
                foreach (var path in absent)
                    Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, path));
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                // Reuse the existing exact-file witness helper. Both reads must be fresh across the real owner call;
                // no cached DATA or shared mutable fixture can establish byte/existence inertness.
                var before = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                Assert.Equal(originalOrdinaryBytes, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "existing_pressure_preconditions_ready", turn = 44,
                    parent.WoundId, pressure.EffectId, pressure.ComponentId, ordinaryCalculation, desiredCalculation,
                    absent, files = before.Select(pair => new { path = pair.Key, bytes = pair.Value.Length,
                        sha256 = Convert.ToHexString(SHA256.HashData(pair.Value)) }), generation,
                    elapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds }));

                var ownerStarted = Stopwatch.GetTimestamp();
                var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "existing_pressure_original_intake", issues = recorded.Issues }));
                AssertNoConflictFrameErrors(recorded.Issues);
                using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
                Assert.Empty(capture.ReadClosedExchangeEvidence(lease));
                var begun = await capture.BeginResourceExecutionAsync(lease);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "existing_pressure_resource_begin", issues = begun }));
                AssertNoConflictFrameErrors(begun);
                var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "existing_pressure_owner_advance", issues = advanced.Issues,
                    hasStep = advanced.Step is not null, currentOwner = capture.IsCurrentOwner,
                    elapsedMilliseconds = Stopwatch.GetElapsedTime(ownerStarted).TotalMilliseconds }));
                AssertExistingPressureInputInert(context, lease, before, generation);
                foreach (var path in absent)
                    Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, path));
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));

                var admitted = advanced.Issues.Count == 0 && advanced.Step?.Interval is AcceptedMechanicsPlanner.SpiritualExchangeInterval;
                if (!admitted)
                {
                    // Only this exact refusal qualifies as the causal RED. Any other result fails setup/assumptions first.
                    var refusal = Assert.Single(advanced.Issues);
                    Assert.Equal("spiritual_wound_profile_integration_required", refusal.Code);
                    Assert.Equal("activeConflict.exchangeLog[2]", refusal.FilePath);
                    Assert.Equal(IssueSeverity.Error, refusal.Severity);
                    Assert.Equal("current-generation consumer for spiritual_strain_burden", refusal.Actual);
                    Assert.Null(advanced.Step);
                    Assert.False(capture.IsCurrentOwner); // Ordinal0 refusal revokes the capture; do not read its closed evidence.
                    _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "existing_pressure_causal_refusal_inert",
                        assertion = "ExistingPressureOrdinaryInput_MustAdvanceOneActualPressureExchange", issues = advanced.Issues,
                        canonicalAndInputBytesUnchanged = true, absenceUnchanged = true, generationUnchanged = true }));
                    Assert.True(admitted, "ExistingPressureOrdinaryInput_MustAdvanceOneActualPressureExchange: the current owner must independently prove ordinaryII and admit actualIII.");
                }

                var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step!.Interval);
                var closed = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
                Assert.Equal(0, closed.Ordinal);
                Assert.Equal("exchange_existing_pressure_44", closed.ExchangeId);
                Assert.Equal(new[] { 0, 1 }, closed.DiceClaims.Select(row => row.SourceIndex));
                Assert.Equal(new[] { 4, 12 }, closed.DiceClaims.Select(row => row.Value));
                var expectedActual = ordinary.DeepClone().AsObject();
                expectedActual["after"]!["playerSideStrain"] = "overwhelmed";
                Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(closed.ExchangeJson)));
                var source = Assert.Single(closed.Sources);
                var admissionCounts = capture.ReadExistingStrainReuseCounts();
                Assert.Equal(1, admissionCounts.OrdinaryEvaluations);
                Assert.Equal(1, admissionCounts.ActualEvaluations);
                Assert.True(admissionCounts.FreshInputChecks > 0);
                Assert.True(admissionCounts.FreshDependencyChecks > 0);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "existing_pressure_actual_admission_counts",
                    admissionCounts.Queries, admissionCounts.SignedParentReads, admissionCounts.OrdinaryEvaluations,
                    admissionCounts.ActualEvaluations, admissionCounts.FreshInputChecks, admissionCounts.FreshDependencyChecks }));
                Assert.Equal(("player", "pressure", "guardian:guardian_frame", "player_soul:player_soul"),
                    (source.AffectedSide, source.Operation, source.ActingActor, source.AffectedActor));
                Assert.Equal(desiredCalculation, source.Calculation);
                Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(source.ExchangeJson)));
                Assert.Equal(originalOrdinaryBytes, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
                Assert.Equal("fractured", (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!["playerSideStrain"]!.GetValue<string>());

                var prefix = capture.ReadClosedResourceCandidatePrefix(lease, interval);
                var spends = prefix.AppliedTransitions.Where(row => row.Operation == ResourceTransitionOperation.Spend &&
                    row.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
                Assert.Equal(2, spends.Length);
                Assert.DoesNotContain(prefix.ReplayTransitions, row => row.Operation == ResourceTransitionOperation.Spend);
                foreach (var (kind, maximum) in new[] { (ResourceOwnerKind.AfterlifeActor, 10m), (ResourceOwnerKind.AfterlifeConflictSide, 9m) })
                {
                    var spend = Assert.Single(spends, row => row.Coordinate.OwnerKind == kind);
                    Assert.Equal((44, 3m, 3m, 3m, 0m, maximum, maximum), (spend.Turn, spend.RequestedAmount, spend.AppliedAmount,
                        spend.BeforeState!.Current, spend.AfterState!.Current, spend.BeforeState.Maximum, spend.AfterState.Maximum));
                }
                AssertExistingPressureInputInert(context, lease, before, generation);
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "existing_pressure_first_admission_unpublished",
                    ordinaryStrain = "fractured", actualStrain = "overwhelmed", actualTrauma = source.Calculation.TraumaPressure,
                    actualMaximum = source.Calculation.MaximumSeverityRank, elapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds }));
                var c1Started = Stopwatch.GetTimestamp();
                var candidateImages = await capture.ReadC1CandidateOwnerImagesAsync(lease, interval);
                var c1Conflict = JsonNode.Parse(candidateImages[AfterlifeSpiritualConflictState.StatePath].Bytes!);
                Assert.True(JsonNode.DeepEquals(expectedActualRoot, c1Conflict));
                Assert.True(JsonNode.DeepEquals(expectedActualRoot,
                    prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
                Assert.True(JsonNode.DeepEquals(c1Conflict,
                    prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.StateJson),
                    JsonNode.Parse(candidateImages[ResourceMaterializationContract.StatePath].Bytes!)));
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.HistoryJson),
                    JsonNode.Parse(candidateImages[ResourceMaterializationContract.HistoryPath].Bytes!)));
                var projectedBalance = AfterlifeConflictActionPointProjectionService.Resolve(prefix.DefinitionsJson,
                    prefix.StateJson, expectedActualRoot["activeConflict"]!.AsObject());
                AssertNoConflictFrameErrors(projectedBalance.Issues);
                Assert.Equal((0m, 0m), (projectedBalance.Projection!.Player.Current, projectedBalance.Projection.Opposition.Current));
                AssertExistingPressureInputInert(context, lease, before, generation);
                WritePressureOwnerTiming("actual_c1_full_root_and_resources", "native", c1Started);

                var commitStarted = Stopwatch.GetTimestamp();
                var committed = await capture.CommitC2FirstTransportAsync(lease, interval);
                var expectedCold = ReadExpectedSpiritualColdOfferData(capture, lease, committed, source);
                Assert.Equal(originalOrdinaryBytes, Convert.FromBase64String(expectedCold.OriginalConflictBytesBase64));
                Assert.Equal(originalOrdinaryFingerprint, expectedCold.OriginalConflictFingerprint);
                var firstPacket = JsonNode.Parse(expectedCold.ReconstructedPendingJson)!;
                var firstSource = Assert.Single(firstPacket["pending"]!["sources"]!.AsArray())!.AsObject();
                firstSourceJson = firstSource.ToJsonString();
                Assert.Equal(0, firstSource["sourceOrdinal"]!.GetValue<int>());
                Assert.Equal(0, firstSource["exchangeOrdinal"]!.GetValue<int>());
                Assert.Equal("exchange_existing_pressure_44", firstSource["exchangeId"]!.GetValue<string>());
                Assert.True(JsonNode.DeepEquals(expectedActual, JsonNode.Parse(Convert.FromBase64String(
                    firstSource["exchangeJsonBase64"]!.GetValue<string>()))));
                AssertPressureExistingV3Source(firstSource, expectedActual, ordinary, ordinaryCalculation,
                    desiredCalculation, parentFingerprint, expectedCold.OriginalConflictFingerprint);
                AssertPressurePrivateTransportOnly(context, before, generation);
                WritePressureOwnerTiming("actual_first_c2_commit", "native", commitStarted);
                capture.Dispose();

                // This is the sole explicit cold Open for observation. Observation reads the actual retained replay, not a second classifier.
                var coldStarted = Stopwatch.GetTimestamp();
                var cold = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
                var opened = await cold.OpenC2PrivateSessionAsync(lease);
                AssertNoConflictFrameErrors(opened.Issues);
                Assert.Equal("offer", opened.Disposition);
                using var choice = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
                Assert.Equal(2, choice.Offer!.MaximumSeverityRank);
                var coldFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                var coldGeneration = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                WritePressureOwnerTiming("actual_single_cold_open", "native", coldStarted);
                var observationStarted = Stopwatch.GetTimestamp();
                var observed = await choice.ReadOfferReplayEvidenceAsync(lease);
                AssertSpiritualColdOfferEvidence(expectedCold, observed);
                AssertExistingPressureInputInert(context, lease, coldFiles, coldGeneration);
                Assert.Equal(originalOrdinaryBytes,
                    await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
                WritePressureOwnerTiming("actual_cold_observation", "native", observationStarted);

                var declineStarted = Stopwatch.GetTimestamp();
                var declined = await choice.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
                    { opportunityRef = choice.Offer.OpportunityRef, decision = "none" }), null);
                AssertNoConflictFrameErrors(declined.Issues);
                Assert.Equal("completed_unpublished", declined.Disposition);
                using var completed = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(declined.Session);
                Assert.Null(completed.Offer);
                var reduction = await completed.ReduceCompletedDeclinesAsync(lease);
                AssertNoConflictFrameErrors(reduction.Issues);
                Assert.True(reduction.Success);
                Assert.NotNull(reduction.ReceiptProof);
                completedResources = reduction.Reduction!.Resources;
                Assert.True(JsonNode.DeepEquals(expectedActualRoot,
                    reduction.Reduction.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
                Assert.Equal(2, reduction.ReceiptAfterImage!["sources"]!.AsArray().Count);
                Assert.Equal(2, reduction.ReceiptAfterImage["decisions"]!.AsArray().Count);
                Assert.True(JsonNode.DeepEquals(receiptBefore["sources"]![0], reduction.ReceiptAfterImage["sources"]![0]));
                Assert.True(JsonNode.DeepEquals(receiptBefore["decisions"]![0], reduction.ReceiptAfterImage["decisions"]![0]));
                Assert.Equal("none", reduction.ReceiptAfterImage["decisions"]![1]!["decision"]!.GetValue<string>());
                foreach (var (path, bytes) in preserved)
                    Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
                Assert.Equal(originalOrdinaryBytes,
                    await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
                Assert.Equal(generation, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
                AssertPressurePrivateTransportOnly(context, before, generation);
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                WritePressureOwnerTiming("actual_none_and_c3", "native", declineStarted);
            }

            // Common normalization owns its own lease. The source/decision owners above are already disposed.
            var publicationStarted = Stopwatch.GetTimestamp();
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            var expected = await PeekPlanAsync(context);
            AssertPressureExistingResourcePlan(expected, expectedActualRoot);
            Assert.True(JsonNode.DeepEquals(expectedActualRoot,
                expected.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
            var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
            AssertNoConflictFrameErrors(published.Issues);
            Assert.Same(expected, published.MechanicsPlan);
            foreach (var pair in expected.WoundCarrierAfterImages.Concat(expected.EffectCarrierAfterImages)
                         .Concat(expected.OwnerCompanionAfterImages))
                Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
            Assert.True(JsonNode.DeepEquals(expected.StateAfterImage,
                await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
            Assert.True(JsonNode.DeepEquals(expected.HistoryAfterImage,
                await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
            Assert.True(JsonNode.DeepEquals(expected.EffectIdentityAfterImage,
                await context.ReadJsonAsync(EffectIdentityState.StatePath)));
            Assert.True(JsonNode.DeepEquals(expectedActualRoot,
                await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath)));
            var parentAfter = await ReadCombatHealingWoundAsync(context);
            Assert.Equal(parentJson, WoundMaterializationContract.SerializeCanonical(parentAfter));
            Assert.Equal(parentFingerprint, WoundIdentityState.ComputeSemanticFingerprint(parentAfter));
            foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
                Assert.Equal(preserved[path], await context.FileSystem.ReadFileBytesAsync(path));
            AssertPressureGuardRecoveryIdentityAppend(identityBefore,
                (await context.ReadJsonAsync(EffectIdentityState.StatePath))!.AsObject(), parentRoots,
                completedResources, expected, turn: 44);
            var acceptedMechanics = await EffectMechanicsSnapshot.LoadAsync(context.FileSystem);
            Assert.True(acceptedMechanics.IsAccepted, string.Join("\n", acceptedMechanics.Issues));
            foreach (var root in parentRoots)
                Assert.Contains(acceptedMechanics.Components, row => row.EffectId == root);
            var receipts = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!;
            Assert.Equal(2, receipts["sources"]!.AsArray().Count);
            Assert.Equal(2, receipts["decisions"]!.AsArray().Count);
            Assert.True(JsonNode.DeepEquals(receiptBefore["sources"]![0], receipts["sources"]![0]));
            Assert.True(JsonNode.DeepEquals(receiptBefore["decisions"]![0], receipts["decisions"]![0]));
            Assert.Equal("none", receipts["decisions"]![1]!["decision"]!.GetValue<string>());
            var acceptedWitness = receipts["sources"]![1]!["witness"]!.AsObject();
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(firstSourceJson), acceptedWitness));
            AssertPressureExistingV3Source(acceptedWitness,
                expectedActualRoot["activeConflict"]!["exchangeLog"]![2]!.AsObject(), ordinary,
                ordinaryCalculation, desiredCalculation, parentFingerprint, originalOrdinaryFingerprint);
            var history = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(),
                WoundHistoryState.HistoryPath);
            var ledger = SpiritualWoundOpportunityReceiptState.Parse(receipts.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
            Assert.True(history.IsValid, string.Join("\n", history.Issues));
            Assert.True(ledger.IsValid, string.Join("\n", ledger.Issues));
            Assert.True(ledger.State!.HasExactStrainHistory(history.State!));
            foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                         SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
                Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));
            WritePressureOwnerTiming("actual_common_c4_publication", "native", publicationStarted);
            var signedStarted = Stopwatch.GetTimestamp();
            await context.CaptureValidatedPendingSnapshotAsync(turn: 45, currentRealm: "Chaos Sea",
                preGeneratedDices1d20: [15, 5], requestId: "request_existing_pressure_actual_read_45");
            var signed45 = (await context.ReadJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!;
            Assert.Equal(45, signed45["turnNumber"]!.GetValue<int>());
            foreach (var path in PressureOrdinarySignedPaths)
                Assert.Equal(await context.FileSystem.ReadFileBytesAsync(path),
                    await context.FileSystem.ReadFileBytesAsync(signed45["files"]![path]!.GetValue<string>()));
            AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
            WritePressureOwnerTiming("actual_signed45_validation", "native", signedStarted);
        });

    /// <summary>Allows only the real private pair and command surface to change before common publication.</summary>
    private static void AssertPressurePrivateTransportOnly(ResourceMaterializationTestContext context,
        IReadOnlyDictionary<string, byte[]> before, byte[] generation)
    {
        var privatePaths = new HashSet<string>(StringComparer.Ordinal)
        {
            SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
            AcceptedMechanicsPlan.WoundCommandPath
        };
        var after = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        bool IsPrivate(string path) => privatePaths.Contains(path.Replace('\\', '/'));
        Assert.Equal(before.Keys.Where(path => !IsPrivate(path)).OrderBy(path => path, StringComparer.Ordinal),
            after.Keys.Where(path => !IsPrivate(path)).OrderBy(path => path, StringComparer.Ordinal));
        foreach (var (path, bytes) in before.Where(pair => !IsPrivate(pair.Key)))
            Assert.True(bytes.SequenceEqual(after[path]), path);
        Assert.Equal(generation, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
    }

    /// <summary>Checks complete actual and ordinary v3 DATA independently, without treating the receipt as current authority.</summary>
    private static void AssertPressureExistingV3Source(JsonObject witness, JsonObject actual, JsonObject ordinary,
        SpiritualWoundCalculation ordinaryCalculation, SpiritualWoundCalculation actualCalculation,
        string parentFingerprint, string originalFingerprint)
    {
        Assert.Equal(actualCalculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(witness, out var retained));
        Assert.NotNull(retained);
        Assert.Equal(3, retained.Version);
        Assert.Null(retained.ParentSourceId);
        Assert.Equal(parentFingerprint, retained.ParentSemanticFingerprint);
        Assert.Equal(ordinaryCalculation, retained.OrdinaryCalculation);
        Assert.Equal(actualCalculation, retained.ActualCalculation);
        Assert.Equal(originalFingerprint, retained.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/2", retained.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(actual, JsonNode.Parse(Convert.FromBase64String(
            witness["exchangeJsonBase64"]!.GetValue<string>()))));
        Assert.True(JsonNode.DeepEquals(ordinary, JsonNode.Parse(Convert.FromBase64String(
            witness["strainPreimage"]!["ordinaryExchangeJsonBase64"]!.GetValue<string>()))));
        Assert.True(JsonNode.DeepEquals(ordinary["after"], retained.ReadOrdinaryAfter(actual)));
        var restored = actual.DeepClone().AsObject();
        restored["after"]!["playerSideStrain"] = "fractured";
        Assert.True(JsonNode.DeepEquals(ordinary, restored));
        Assert.Equal((10L, 1, 15L, 2), (ordinaryCalculation.TraumaPressure,
            ordinaryCalculation.MaximumSeverityRank, actualCalculation.TraumaPressure, actualCalculation.MaximumSeverityRank));
    }

    /// <summary>Proves one actual payment per exact genuine owner, with only its corresponding depletion event.</summary>
    private static void AssertPressureExistingResourcePlan(AcceptedMechanicsPlan plan, JsonObject actualRoot)
    {
        var active = actualRoot["activeConflict"]!;
        var actor = active["playerSide"]!["leadContestant"]!;
        Assert.Equal("player_soul:player_soul", ValidationService.SpiritualWoundSourceSession.ActorKey(actor.AsObject()));
        Assert.True(AfterlifeEntityProfileState.TryNormalizeEffectRealm(active["realm"]!.GetValue<string>(), out var realm));
        var coordinates = new[]
        {
            new ResourceCoordinate(realm, ResourceOwnerKind.AfterlifeActor,
                actor["actorId"]!.GetValue<string>(), "spiritual_action_points"),
            new ResourceCoordinate(realm, ResourceOwnerKind.AfterlifeConflictSide,
                active[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]!["opposition"]!["resourceOwnerId"]!
                    .GetValue<string>(), "spiritual_action_points")
        };
        Assert.Equal(4, plan.ResourceEvents.Count);
        foreach (var coordinate in coordinates)
        {
            var spend = Assert.Single(plan.ResourceEvents, row => row.EventKind == "resource_spent" &&
                row.Coordinate == coordinate);
            Assert.Equal((3m, 0m, 3m), (spend.Before, spend.After, spend.AppliedAmount));
            Assert.Equal(spend with { EventKind = "resource_depleted" },
                Assert.Single(plan.ResourceEvents, row => row.EventKind == "resource_depleted" && row.Coordinate == coordinate));
        }
        Assert.Equal(0m, ResolvePlannedActionPoints(plan, ResourceOwnerKind.AfterlifeActor).Current);
        Assert.Equal(0m, ResolvePlannedActionPoints(plan, ResourceOwnerKind.AfterlifeConflictSide).Current);
    }

    /// <summary>Authors only the last direct-root ordinary pressure/setback against the already signed turn44 frontier.</summary>
    /// <param name="context">Independent genuine birth/guard fixture with untouched signed turn44 inputs.</param>
    /// <param name="root">Detached actual published conflict before the ordinary third exchange.</param>
    /// <returns>The complete original ordinary exchange, never a fabricated actual projection.</returns>
    private static async Task<JsonObject> AppendExistingPressureOrdinaryDraftAsync(
        ResourceMaterializationTestContext context, JsonObject root)
    {
        var active = root["activeConflict"]!;
        var log = active["exchangeLog"]!.AsArray();
        Assert.Equal(2, log.Count);
        var exchange = log[1]!.DeepClone().AsObject();
        exchange["exchangeId"] = "exchange_existing_pressure_44";
        exchange["turnNumber"] = 44;
        exchange["exchangeAtTurn"] = 44;
        exchange["operationType"] = "pressure";
        exchange["outcome"] = "setback";
        exchange["summary"] = "Душа встречает новое давление Хранителя.";
        exchange["before"] = log[1]!["after"]!.DeepClone();
        exchange["after"] = exchange["before"]!.DeepClone();
        exchange["after"]!["playerSideStrain"] = "fractured";
        var matchup = exchange["matchupAudit"]!;
        matchup["playerOperation"] = "pressure";
        matchup["oppositionOperation"] = "pressure";
        matchup["primaryResolutionLane"] = "pressure";
        matchup["riskProfile"] = "offensive_pressure";
        matchup["matchupRationale"] = "Давление души сталкивается со встречным давлением Хранителя.";
        exchange["incomingAction"] = new JsonObject { ["operationType"] = "pressure", ["actorType"] = "guardian",
            ["actorId"] = "guardian_frame", ["summary"] = "Хранитель оказывает духовное давление." };
        var playerCost = CostAudit("pressure", 3m, 3m, 0m);
        playerCost["max"] = 10m;
        var oppositionCost = CostAudit("pressure", 3m, 3m, 0m);
        oppositionCost["max"] = 9m;
        exchange["actionCostAudit"] = new JsonObject { ["player"] = playerCost, ["opposition"] = oppositionCost };
        var dice = exchange["diceAudit"]!;
        dice["diceUsed"]![0]!["sourceIndex"] = 0;
        dice["diceUsed"]![0]!["value"] = 4;
        dice["diceUsed"]![1]!["sourceIndex"] = 1;
        dice["diceUsed"]![1]!["value"] = 12;
        dice["playerTotal"] = 4;
        dice["oppositionTotal"] = 12;
        dice["margin"] = -8;
        dice["outcomeBand"] = "decisive_opposition_success";
        foreach (var field in new[] { "oppositionSideStrain", "conflictPosition", "controlState", "tempoAdvantage" })
            Assert.True(JsonNode.DeepEquals(exchange["before"]![field], exchange["after"]![field]), field);
        Assert.Equal("strained", exchange["before"]!["playerSideStrain"]!.GetValue<string>());
        Assert.Equal("clear", exchange["after"]!["oppositionSideStrain"]!.GetValue<string>());
        log.Add(exchange);
        active["playerSideStrain"] = "fractured";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await context.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject
            { ["response"] = "Душа встречает новое давление Хранителя. Противостояние продолжается.",
                ["timestamp"] = "2026-09-30T00:00:00Z" }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        return exchange.DeepClone().AsObject();
    }

    /// <summary>Rechecks every physical input/canonical file and generation, including exact absence through the file inventory.</summary>
    /// <param name="context">Fixture owning the canonical filesystem and its isolated session root.</param>
    /// <param name="lease">Still-active lease used to reject accidental common publication.</param>
    /// <param name="before">Fresh byte/existence witnesses captured before original intake.</param>
    /// <param name="generation">Exact original physical session-generation bytes.</param>
    private static void AssertExistingPressureInputInert(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease, IReadOnlyDictionary<string, byte[]> before, byte[] generation)
    {
        var after = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        Assert.Equal(before.Keys.OrderBy(path => path, StringComparer.Ordinal), after.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var (path, bytes) in before)
            Assert.True(bytes.SequenceEqual(after[path]), path);
        Assert.Equal(generation, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }
}
