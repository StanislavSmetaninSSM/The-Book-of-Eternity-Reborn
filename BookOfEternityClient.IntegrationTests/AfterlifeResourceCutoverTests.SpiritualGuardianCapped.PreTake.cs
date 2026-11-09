using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>One genuine positive IV firstC2/cold-none/C3 prefix registers current authority, then physical raw drift blocks normalizer pre-take with zero writes.</summary>
    [Fact]
    public Task SpiritualGuardianCapped_PreTakeDriftPreservesAuthoredInput()
    {
        var armed = false; var writes = new List<string>();
        var hooks = new FileSystemManagerHooks
        { BeforeCanonicalMutationAsync = path => { if (armed) writes.Add(path); return Task.CompletedTask; } };
        const string name = "guardian_capped_pre_take";
        return WithSingletonIvJourneyFailureAsync(name, () => WithGuardianAttributionJourneyAsync(name, async setup =>
        {
            var context = setup.Copy.Context;
            Assert.Equal(4, setup.ExpectedRank); Assert.Equal(CappedControlCalculation(), setup.OrdinaryCalculation);
            Assert.Equal(CappedControlCalculation(), setup.ActualCalculation);
            Assert.True(JsonNode.DeepEquals(setup.Copy.Conflict, setup.ActualRoot));
            // Real positive firstC2 -> warm disposal -> fresh validator cold Open -> current public-ref none -> C3.
            // The existing helper checks exactly one commit/Open/none and positive decision receipt, not source-only zero DATA.
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_pre_take_before_positive_prefix", name,
                setup.Retained, signedOriginalTracked = setup.Origin.Tracked, setup.Authored, ordinaryRoot = setup.Copy.Conflict,
                setup.OrdinaryCalculation, setup.ActualCalculation }));
            var completed = await CompleteGuardianAttributionColdC3Async(setup, context.Validator);
            var witness = JsonNode.Parse(completed.WitnessJson)!.AsObject(); AssertGuardianAttributionWitness(setup, witness);
            var receipt = JsonNode.Parse(completed.ReceiptJson)!.AsObject();
            AssertGuardianAttributionReceiptAppend(setup, receipt, witness);
            Assert.Equal(1, receipt["schemaVersion"]!.GetValue<int>()); Assert.Equal("none", receipt["decisions"]![1]!["decision"]!.GetValue<string>());
            RequireGuardianJourneyIssues(name, "capped_pre_take_real_raw_resource_registration",
                await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            var registeredPlan = await PeekPlanAsync(context); AssertGuardianAttributionRegisteredPlan(setup, completed, registeredPlan);
            var backups = await ReadSpiritualC4BackupsAsync(context);
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? authority = null;
            IReadOnlyDictionary<string, byte[]>? edited = null; byte[]? generation = null;
            IReadOnlyDictionary<string, byte[]>? transportBefore = null; JsonObject? transportBaseline = null;
            await ExecuteGuardianLeaseStagesAsync(context, lease =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out var first));
                authority = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority>(first);
                AssertGuardianAttributionRegisteredPlan(setup, completed, authority.Plan);
                return Task.CompletedTask;
            });
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out var current));
                Assert.Same(authority, current); Assert.True(authority!.HasValidSeal());
                Assert.True(authority.CurrentInputsAgree(context.FileSystem, lease));
                Assert.Empty(await authority.ValidateCurrentInputsAsync(context.FileSystem, lease));
                Assert.True(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                Assert.True(JsonNode.DeepEquals(receipt, authority.Plan.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath]));
                var before = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                // Pin the already captured positive C2 DATA, not a live owner or a new cold reconstruction.
                var retained = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                             SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
                {
                    Assert.True(before.TryGetValue(path, out var bytes), path); Assert.NotEmpty(bytes!);
                    retained.Add(path, bytes!); // This Fact never mutates its detached before snapshot.
                }
                transportBefore = retained;
                var checkpointData = ReadGuardianAttributionJson(retained[SpiritualWoundCaptureCheckpointState.StatePath])["checkpoint"]!.AsObject();
                var pendingData = ReadGuardianAttributionJson(retained[SpiritualWoundDecisionPendingState.StatePath])["pending"]!.AsObject();
                var stagedNone = Assert.Single(pendingData["stagedDecisions"]!.AsArray())!.AsObject();
                var command = Assert.Single(ReadGuardianAttributionJson(retained[AcceptedMechanicsPlan.WoundCommandPath])["commands"]!.AsArray())!.AsObject();
                Assert.Equal(1, checkpointData["committedAdvance"]!.GetValue<int>());
                Assert.Equal(checkpointData["expectedPendingPacketFingerprint"]!.GetValue<string>(), pendingData["packetFingerprint"]!.GetValue<string>());
                Assert.Equal((1, 1, 0), (pendingData["cursor"]!["exchangeOrdinal"]!.GetValue<int>(),
                    pendingData["cursor"]!["nextSourceOrdinal"]!.GetValue<int>(), pendingData["cursor"]!["waveOrdinal"]!.GetValue<int>()));
                Assert.Equal("none", stagedNone["decision"]!.GetValue<string>());
                Assert.Equal((0, 0), (stagedNone["sourceOrdinal"]!.GetValue<int>(), stagedNone["waveOrdinal"]!.GetValue<int>()));
                Assert.Equal(witness["sourceId"]!.GetValue<string>(), stagedNone["sourceId"]!.GetValue<string>());
                foreach (var field in new[] { "selectedSeverityRank", "woundDraftBase64", "woundDraftFingerprint" }) Assert.Null(stagedNone[field]);
                Assert.Equal("opportunity_decision", command["kind"]!.GetValue<string>());
                Assert.Equal("none", command["decision"]!["decision"]!.GetValue<string>());
                Assert.Equal(stagedNone["opportunityRef"]!.GetValue<string>(), command["decision"]!["opportunityRef"]!.GetValue<string>());
                Assert.Equal(stagedNone["opportunityRef"]!.GetValue<string>(), command["opportunity"]!["publicRef"]!.GetValue<string>());
                transportBaseline = new JsonObject
                {
                    ["committedAdvance"] = checkpointData["committedAdvance"]!.DeepClone(),
                    ["checkpointExpectedPendingPacketFingerprint"] = checkpointData["expectedPendingPacketFingerprint"]!.DeepClone(),
                    ["pendingPacketFingerprint"] = pendingData["packetFingerprint"]!.DeepClone(),
                    ["pendingCursor"] = pendingData["cursor"]!.DeepClone(), ["stagedNone"] = stagedNone.DeepClone(),
                    ["commandKind"] = command["kind"]!.DeepClone(), ["commandPublicRef"] = command["opportunity"]!["publicRef"]!.DeepClone(),
                    ["commandDecision"] = command["decision"]!.DeepClone()
                };
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_pre_take_current_positive_authority",
                    setup.Name, actualRoot = setup.ActualRoot, witness, receipt, before, generation,
                    transportPaths = transportBefore.Keys.ToArray(), transportBaseline,
                    validSeal = authority.HasValidSeal(), currentInputsAgree = authority.CurrentInputsAgree(context.FileSystem, lease),
                    registeredExactAuthority = ReferenceEquals(authority, current), scope = "One IV negative pre-take; no normal C4/signed47 qualification." }));
                await AppendPressureRawWhitespaceAsync(context, lease);
                edited = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                Assert.Equal(before.Keys.OrderBy(p => p, StringComparer.Ordinal), edited.Keys.OrderBy(p => p, StringComparer.Ordinal));
                foreach (var (path, bytes) in before.Where(row => row.Key.Replace('\\', '/') != AfterlifeSpiritualConflictState.StatePath)) Assert.Equal(bytes, edited[path]);
                Assert.NotEqual(before[AfterlifeSpiritualConflictState.StatePath], edited[AfterlifeSpiritualConflictState.StatePath]);
            });
            armed = true;
            InvalidDataException failure;
            try
            {
                failure = await Assert.ThrowsAsync<InvalidDataException>(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    context.FileSystem, context.Normalizer, context.Validator, backups));
                Assert.Contains("spiritual_c4_publication_input_changed", failure.Message);
                Assert.Contains(AfterlifeSpiritualConflictState.StatePath, failure.Message);
            }
            finally { armed = false; }
            Assert.Empty(writes);
            await ExecuteGuardianLeaseStagesAsync(context, lease =>
            {
                AssertExistingPressureInputInert(context, lease, edited!, generation!);
                Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, authority!, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                Assert.False(authority!.HasValidSeal());
                var actualFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                foreach (var (path, expectedBytes) in transportBefore!)
                {
                    Assert.True(actualFiles.TryGetValue(path, out var actualBytes), path); Assert.NotEmpty(actualBytes!);
                    Assert.Equal(expectedBytes, actualBytes);
                }
                AssertExistingPressureInputInert(context, lease, edited!, generation!);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_pre_take_refusal_preserves_raw_edit", name,
                    error = failure.ToString(), canonicalWrites = writes, edited, generation, actualFiles,
                    transportPaths = transportBefore.Keys.ToArray(), transportBaseline, retainedCompletedTransportPreserved = true,
                    oldAuthoritySealValid = authority.HasValidSeal(), publicationRetired = true }));
                return Task.CompletedTask;
            });
        }, hooks, retainedRank: 4, contour: GuardianSingletonPressureContour.CappedBroken));
    }
}
