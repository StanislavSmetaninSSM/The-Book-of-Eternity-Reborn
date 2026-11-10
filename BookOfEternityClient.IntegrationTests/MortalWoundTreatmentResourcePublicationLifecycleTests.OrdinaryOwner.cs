using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("request_quantity")]
    [InlineData("resolved_result")]
    public async Task GuaranteedTreatment_ComponentOrdinaryOwnerRefusesNestedPersistedAuthorityTamper(string mutation)
    {
        await using var held = await CreateHeldTreatmentPipelineContextAsync(
            fault: null, withRollbackAuthority: true, composePublication: false,
            configureBeforePreparation: PrepareGuaranteedNativeNpcReceiptShapeAsync);
        using var owned = new CleanupOwnedFixture(held.Root, line => _directGachaOutput?.WriteLine(line));
        try
        {
            var root = ParseJsonObjectBytes(Assert.IsType<byte[]>(await held.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath)));
            var command = root["commands"]![0]!.AsObject();
            if (mutation == "request_quantity")
            {
                command["authority"]!["request"]!["resourceAuthority"]!["claims"]![0]!["quantity"] = 1;
                command["result"]!["requestAuthority"] = command["authority"]!["request"]!.DeepClone();
            }
            else
            {
                Assert.Equal("success", command["result"]!["resultCategory"]!.GetValue<string>());
                command["result"]!["resultCategory"] = "partial_success";
                // Keep every original fingerprint/seal: the resolved payload itself now disagrees.
            }
            await held.FileSystem.WriteFileAtomicAsync(held.Lease, AcceptedMechanicsPlan.WoundCommandPath, root.ToJsonString());
            var before = await CaptureOrdinaryTreatmentOwnerBytesAsync(held);
            await held.ReleaseLeaseAsync();
            var (engine, snapshot) = await CreateHeldTreatmentValidationEngineAsync(held);
            var issues = await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(engine,
                "ValidateResourceAndMortalGuaranteedTreatmentAsync", true, snapshot, 42);
            WritePhysicalInjuryDiagnostic("fr063_component_ordinary_authority_tamper", new { mutation, Issues = issues });
            Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
            await held.AcquireLeaseAsync();
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(held.FileSystem, held.Lease));
            await AssertOrdinaryTreatmentOwnerBytesAsync(held, before);
        }
        finally { await held.ReleaseLeaseAsync(); }
    }

    [Fact]
    public async Task GuaranteedTreatment_ComponentOrdinaryOwnerReadsCurrentRequiredCapabilityInsteadOfHeldWarmAuthority()
    {
        await using var held = await CreateHeldTreatmentPipelineContextAsync(
            fault: null, withRollbackAuthority: true, composePublication: false,
            configureBeforePreparation: PrepareGuaranteedNativeNpcReceiptShapeAsync);
        using var owned = new CleanupOwnedFixture(held.Root, line => _directGachaOutput?.WriteLine(line));
        try
        {
            var npc = ParseJsonObjectBytes(Assert.IsType<byte[]>(await held.ReadFileBytesAsync(NpcCoreChangesContract.NpcCorePath)));
            var provider = Assert.IsType<JsonObject>(Assert.Single(npc[GuardianPolicyContracts.NpcCoreSceneSectionName]!.AsArray()));
            var skill = Assert.IsType<JsonObject>(Assert.Single(provider["activeSkills"]!.AsArray()));
            Assert.Single(skill["mortalWoundTreatmentCapabilities"]!.AsArray());
            skill["mortalWoundTreatmentCapabilities"] = new JsonArray();
            await held.FileSystem.WriteFileAtomicAsync(held.Lease, NpcCoreChangesContract.NpcCorePath, npc.ToJsonString());
            var before = await CaptureOrdinaryTreatmentOwnerBytesAsync(held);
            await held.ReleaseLeaseAsync();
            var (engine, snapshot) = await CreateHeldTreatmentValidationEngineAsync(held);
            var issues = await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(engine,
                "ValidateResourceAndMortalGuaranteedTreatmentAsync", true, snapshot, 42);
            WritePhysicalInjuryDiagnostic("fr063_component_ordinary_current_capability", new { Issues = issues });
            Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
            await held.AcquireLeaseAsync();
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(held.FileSystem, held.Lease));
            await AssertOrdinaryTreatmentOwnerBytesAsync(held, before);
            Assert.Equal(2, await ReadCurrentTreatmentEnergyAsync(held));
        }
        finally { await held.ReleaseLeaseAsync(); }
    }

    [Fact]
    public async Task GuaranteedTreatment_ComponentOrdinaryOwnerPostSealCompensationRetainsFullHeldRequestAndExactRetry()
    {
        var fault = new AcceptedTreatmentPipelineFault("wound_post_seal", 1);
        await using var held = await CreateHeldTreatmentPipelineContextAsync(
            fault, withRollbackAuthority: true, composePublication: false,
            configureBeforePreparation: PrepareGuaranteedNativeNpcReceiptShapeAsync);
        using var owned = new CleanupOwnedFixture(held.Root, line => _directGachaOutput?.WriteLine(line));
        try
        {
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(held.FileSystem, held.Lease));
            await held.ReleaseLeaseAsync();
            var (engine, snapshot) = await CreateHeldTreatmentValidationEngineAsync(held);
            Assert.Empty(await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(engine,
                "ValidateResourceAndMortalGuaranteedTreatmentAsync", true, snapshot, 42));
            await held.AcquireLeaseAsync();
            var admitted = AssertOrdinaryTreatmentOwnerPlan(held);
            var before = await CaptureExactTreatmentTransactionBytesAsync(held);
            var fence = AcceptedMechanicsPlanFingerprints.ComputeInput(held.OriginalBinding);
            await held.ReleaseLeaseAsync();
            fault.Arm(held);
            var first = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync", "ordinary post-seal component", snapshot, null, 42, null);
            WritePhysicalInjuryDiagnostic("fr063_component_ordinary_post_seal", new { first, fault.Fired, fault.ObservedPhases, fence });
            Assert.True(fault.Fired);
            Assert.Equal(AcceptedTurnValidationDisposition.RetryablePublicationRearmed, first);
            await AssertExactTreatmentTransactionBytesAsync(held, before);
            await held.AcquireLeaseAsync();
            Assert.Same(admitted, AssertOrdinaryTreatmentOwnerPlan(held));
            Assert.Equal(fence, AcceptedMechanicsPlanFingerprints.ComputeInput(held.OriginalBinding));
            await AssertConfirmedHeldLiveRegistryProbeAsync(held);
            Assert.True(await ContainsCurrentExactTreatmentRequestAsync(held));
            await held.ReleaseLeaseAsync();
            Assert.Equal(AcceptedTurnValidationDisposition.Accepted,
                await InvokePrivateAsync<AcceptedTurnValidationDisposition>(engine,
                    "ValidateAcceptedTurnOutcomeWithRepairLoopAsync", "ordinary exact retry component", snapshot, null, 42, null));
            await AssertOrdinaryTreatmentOwnerPublishedAsync(held);
        }
        finally { await held.ReleaseLeaseAsync(); }
    }

    [Theory]
    [InlineData("resource")]
    [InlineData("effect")]
    [InlineData("party")]
    public async Task GuaranteedTreatment_ComponentOrdinaryOwnerRefusesActualConcurrentMechanicalWork(string surface)
    {
        await using var held = await CreateHeldTreatmentPipelineContextAsync(
            fault: null, withRollbackAuthority: true, composePublication: false,
            configureBeforePreparation: async files =>
            {
                await PrepareGuaranteedNativeNpcReceiptShapeAsync(files);
                if (surface != "effect") return;
                // A lawful public skill effect source is canonical BEFORE original/request
                // creation; no source or accepted authority is fabricated after admission.
                var npc = ParseJsonObjectBytes(Assert.IsType<byte[]>(await files.ReadFileBytesAsync(NpcCoreChangesContract.NpcCorePath)));
                var provider = Assert.IsType<JsonObject>(Assert.Single(npc[GuardianPolicyContracts.NpcCoreSceneSectionName]!.AsArray()));
                var skill = Assert.IsType<JsonObject>(Assert.Single(provider["activeSkills"]!.AsArray()));
                Assert.False(skill.ContainsKey("activeEffectDefinitions"));
                skill["activeEffectDefinitions"] = new JsonArray(EffectMaterializationTestFixture.CreateDefinition());
                await files.WriteFileAtomicAsync(NpcCoreChangesContract.NpcCorePath, npc.ToJsonString());
            });
        using var owned = new CleanupOwnedFixture(held.Root, line => _directGachaOutput?.WriteLine(line));
        try
        {
            var change = new JsonObject
            {
                ["operation"] = surface == "party" ? "spend" : "damage",
                ["target"] = new JsonObject { ["kind"] = "player", ["targetId"] = "player_current" },
                ["resourceKey"] = surface == "party" ? "energy" : "health", ["amount"] = 1,
                ["source"] = new JsonObject { ["kind"] = surface == "party" ? "action_cost" : "narrative_outcome" },
                ["eventRef"] = "turn_42:resource:1", ["reason"] = "Actual concurrent work must not disappear behind treatment."
            };
            var resource = new JsonObject { ["resourceDefinitionCreations"] = new JsonArray(),
                ["resourceCapacityChanges"] = new JsonArray(), ["resourceChanges"] = new JsonArray(change) };
            Assert.True(ResourceAcceptedTurnInputComposer.Parse(resource.ToJsonString()).IsValid);
            string path;
            JsonObject submitted;
            if (surface == "resource") { path = ResourceMaterializationContract.CommandPath; submitted = resource; }
            else if (surface == "party")
            {
                path = "game_state/misc/player_interactions.json";
                submitted = new JsonObject { ["otherPlayersInteractions"] = new JsonObject
                {
                    ["player_remote"] = new JsonArray(new JsonObject { ["resourceChanges"] = new JsonArray(change.DeepClone()) })
                }, ["_lastUpdated"] = "2026-10-13T00:00:00Z" };
            }
            else
            {
                path = EffectAcceptedTurnPlan.CommandPath;
                var npc = ParseJsonObjectBytes(Assert.IsType<byte[]>(await held.ReadFileBytesAsync(NpcCoreChangesContract.NpcCorePath)));
                var provider = Assert.IsType<JsonObject>(Assert.Single(npc[GuardianPolicyContracts.NpcCoreSceneSectionName]!.AsArray()));
                var skill = Assert.IsType<JsonObject>(Assert.Single(provider["activeSkills"]!.AsArray()));
                var definition = Assert.IsType<JsonObject>(Assert.Single(skill["activeEffectDefinitions"]!.AsArray()));
                var apply = EffectMaterializationTestFixture.CreateApplyCommand();
                apply["source"] = new JsonObject { ["kind"] = "skill", ["sourceId"] = skill["skillId"]!.DeepClone(),
                    ["definitionKey"] = definition["definitionKey"]!.DeepClone() };
                Assert.Equal(definition["definitionKey"]!.GetValue<string>(), apply["source"]!["definitionKey"]!.GetValue<string>());
                Assert.Equal("skill_guaranteed_care_t070b3", apply["source"]!["sourceId"]!.GetValue<string>());
                submitted = EffectMaterializationTestFixture.CreateCommandRoot(apply);
            }
            await held.FileSystem.WriteFileAtomicAsync(held.Lease, path, submitted.ToJsonString());
            var before = await CaptureOrdinaryTreatmentOwnerBytesAsync(held);
            await held.ReleaseLeaseAsync();
            var (engine, snapshot) = await CreateHeldTreatmentValidationEngineAsync(held);
            var issues = await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(engine,
                "ValidateResourceAndMortalGuaranteedTreatmentAsync", true, snapshot, 42);
            WritePhysicalInjuryDiagnostic("fr063_component_ordinary_concurrent_work", new { surface, path, Submitted = submitted, Issues = issues });
            var refusal = Assert.Single(issues.Where(issue => issue.Severity == IssueSeverity.Error));
            Assert.Equal("mortal_wound_treatment_ordinary_concurrent_work_unsupported", refusal.Code);
            await held.AcquireLeaseAsync();
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(held.FileSystem, held.Lease));
            await AssertOrdinaryTreatmentOwnerBytesAsync(held, before);
            Assert.True(await ContainsCurrentExactTreatmentRequestAsync(held));
            Assert.Equal(2, await ReadCurrentTreatmentEnergyAsync(held));
            Assert.Single(ReadCurrentTreatmentHistory(held.FileSystem).State!.Transitions);
            AssertConfirmedHeldBlocksCompetingTreatment(held);
        }
        finally { await held.ReleaseLeaseAsync(); }
    }

    [Fact]
    public async Task GuaranteedTreatment_ComponentOrdinaryOwnerGuardsAbsentInputAcrossLeaseTurnoverBeforePublication()
    {
        await using var held = await CreateHeldTreatmentPipelineContextAsync(
            fault: null, withRollbackAuthority: true, composePublication: false,
            configureBeforePreparation: PrepareGuaranteedNativeNpcReceiptShapeAsync);
        using var owned = new CleanupOwnedFixture(held.Root, line => _directGachaOutput?.WriteLine(line));
        try
        {
            const string path = "game_state/npcs/npc_skills.json";
            Assert.Null(await held.ReadFileBytesAsync(path));
            await held.ReleaseLeaseAsync();
            var (engine, snapshot) = await CreateHeldTreatmentValidationEngineAsync(held);
            Assert.Empty(await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(engine,
                "ValidateResourceAndMortalGuaranteedTreatmentAsync", true, snapshot, 42));
            await held.AcquireLeaseAsync();
            var plan = AssertOrdinaryTreatmentOwnerPlan(held);
            Assert.Contains(path, plan.BeforeImages.Keys);
            Assert.False(plan.BeforeImages[path].Existed);
            await held.ReleaseLeaseAsync(); // The absence proof must survive this real owner lease turnover.
            await held.FileSystem.WriteFileAtomicAsync(path, new JsonObject
                { ["NPCActiveSkillChanges"] = new JsonArray(), ["NPCPassiveSkillChanges"] = new JsonArray() }.ToJsonString());
            var before = await CaptureOrdinaryTreatmentOwnerBytesAsync(held);
            var validator = GetPrivateField<ValidationService>(engine, "_validator");
            var issues = await validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
            Assert.Contains(issues, issue => issue.FilePath == path && issue.Code == "accepted_mechanics_wound_live_before_image_mismatch");
            await held.AcquireLeaseAsync();
            var refusal = await Assert.ThrowsAsync<InvalidDataException>(() => GetPrivateField<CanonicalStateNormalizer>(engine, "_normalizer")
                .BindTo(held.Lease).PrevalidateTreatmentResourcePublicationTransactionAsync());
            Assert.Equal($"Accepted mechanics authority at '{path}' changed after validation.", refusal.Message);
            await AssertOrdinaryTreatmentOwnerBytesAsync(held, before);
            var generationBeforeInspection = held.FileSystem.ReadLocalGenerationSnapshot(held.Lease);
            var finalized = MortalWoundTreatmentResourceComposer.Finalize(held.Resolution);
            Assert.True(finalized.IsValid, DescribeValidationIssues(finalized.Issues));
            var finalization = Assert.IsType<MortalWoundTreatmentResourceFinalization>(finalized.Finalization);
            var inspection = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(
                held.FileSystem, held.Lease, held.Request, finalization);
            WriteConfirmedClaimInspection("post_preflight", inspection);
            Assert.True(inspection.IsValid, DescribeValidationIssues(inspection.Issues));
            Assert.Empty(inspection.Issues);
            Assert.Equal(0, inspection.ChangedCount);
            Assert.Equal(MortalWoundTreatmentResourceReservationState.ConfirmedHeld, inspection.State);
            Assert.Equal(held.Request.Coordinates.OperationKey, inspection.OperationKey);
            Assert.Equal(held.Request.Coordinates.AttemptId, inspection.AttemptId);
            Assert.Equal(held.Request.RequestFingerprint, inspection.RequestFingerprint);
            Assert.Equal(finalization.ResourceAuthorityFingerprint, inspection.ResourceAuthorityFingerprint);
            Assert.Equal(finalization.FinalizationFingerprint, inspection.FinalizationFingerprint);
            Assert.False(string.IsNullOrWhiteSpace(inspection.AgreementFingerprint));
            Assert.Equal(held.OriginalSessionGeneration, inspection.SessionGeneration);
            Assert.Equal(held.OriginalSessionGenerationRevision, inspection.SessionGenerationRevision);
            Assert.Null(inspection.GenerationFingerprint);
            var cancelledPublication = AcceptedTurnAuthorityRegistry.ProbeMortalWoundTreatmentResourcePublicationHold(
                held.FileSystem, held.Lease, held.AcceptedState, held.Request, finalization);
            Assert.False(cancelledPublication.IsValid);
            Assert.Equal("mortal_wound_treatment_publication_transaction_token_mismatch",
                Assert.Single(cancelledPublication.Issues).Code);
            await AssertOrdinaryTreatmentOwnerBytesAsync(held, before);
            Assert.Equal(generationBeforeInspection.Bytes,
                held.FileSystem.ReadLocalGenerationSnapshot(held.Lease).Bytes);
            Assert.True(await ContainsCurrentExactTreatmentRequestAsync(held));
            Assert.Equal(2, await ReadCurrentTreatmentEnergyAsync(held));
            Assert.Single(ReadCurrentTreatmentHistory(held.FileSystem).State!.Transitions);
            WritePhysicalInjuryDiagnostic("fr063_component_ordinary_guard_lease_turnover", new
                {
                    path, GuardBefore = plan.BeforeImages[path].Existed, Issues = issues,
                    PreflightRefusal = refusal.Message, inspection.IsValid, inspection.State,
                    inspection.OperationKey, inspection.AttemptId, inspection.RequestFingerprint,
                    inspection.ResourceAuthorityFingerprint, inspection.FinalizationFingerprint,
                    inspection.AgreementFingerprint, inspection.SessionGeneration,
                    inspection.SessionGenerationRevision, inspection.GenerationFingerprint,
                    CancelledPublicationIssues = cancelledPublication.Issues,
                    MechanicsAndRequestUnchanged = true
                });
        }
        finally { await held.ReleaseLeaseAsync(); }
    }

    [Theory]
    [InlineData("generation_absent")]
    [InlineData("owner_absent")]
    [InlineData("owner_stale")]
    [InlineData("foreign_lease")]
    [InlineData("disposed_lease")]
    public async Task GuaranteedTreatment_ComponentConfirmedClaimInspectionRefusesAbsentStaleOrInvalidLease(string mutation)
    {
        // No rollback evidence: only this separate negative fixture may rotate its generation.
        await using var held = await CreateHeldTreatmentPipelineContextAsync(fault: null, withRollbackAuthority: false);
        using var owned = new CleanupOwnedFixture(held.Root, line => _directGachaOutput?.WriteLine(line));
        var finalized = MortalWoundTreatmentResourceComposer.Finalize(held.Resolution);
        Assert.True(finalized.IsValid, DescribeValidationIssues(finalized.Issues));
        var finalization = Assert.IsType<MortalWoundTreatmentResourceFinalization>(finalized.Finalization);
        try
        {
            if (mutation == "owner_stale")
            {
                var oldGeneration = Assert.IsType<string>(held.FileSystem.ReadExistingSessionGeneration(held.Lease));
                var oldRevision = held.FileSystem.CanonicalRootAuthorityIdentity.SessionGenerationRevision;
                await held.ReleaseLeaseAsync();
                string newGeneration;
                await using (var lifecycle = await held.FileSystem.AcquireSessionLifecycleLeaseAsync())
                await using (var replacement = await held.FileSystem.AcquireSessionReplacementWriteLeaseAsync(lifecycle))
                    newGeneration = held.FileSystem.RotateSessionGeneration(replacement);
                await held.AcquireLeaseAsync();
                Assert.NotEqual(oldGeneration, newGeneration);
                Assert.NotEqual(oldRevision, held.FileSystem.CanonicalRootAuthorityIdentity.SessionGenerationRevision);
                Assert.Equal(newGeneration, held.FileSystem.ReadExistingSessionGeneration(held.Lease));
                var before = await CaptureConfirmedClaimReadImagesAsync(held.FileSystem, held.Lease);
                var inspection = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(
                    held.FileSystem, held.Lease, held.Request, finalization);
                WriteConfirmedClaimInspection(mutation, inspection);
                AssertConfirmedClaimReadRefusal(inspection, "mortal_wound_treatment_confirmed_claim_owner_stale");
                await AssertConfirmedClaimReadImagesAsync(held.FileSystem, held.Lease, before);
                Assert.True(await ContainsCurrentExactTreatmentRequestAsync(held));
                return;
            }

            if (mutation == "disposed_lease")
            {
                var before = await CaptureConfirmedClaimReadImagesAsync(held.FileSystem, held.Lease);
                var disposed = held.Lease;
                await held.ReleaseLeaseAsync();
                var failure = Assert.Throws<InvalidOperationException>(() =>
                    AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(
                        held.FileSystem, disposed, held.Request, finalization));
                Assert.Equal("Canonical write lease is not active for this game session.", failure.Message);
                await held.AcquireLeaseAsync(); // Readback admission is separate from the refused inspector.
                await AssertConfirmedClaimReadImagesAsync(held.FileSystem, held.Lease, before);
                WritePhysicalInjuryDiagnostic("fr063_confirmed_claim_inspection_invalid_lease", new
                    { mutation, FailureType = failure.GetType().FullName, failure.Message, BytesUnchanged = true });
                return;
            }

            Assert.Contains(mutation, new[] { "generation_absent", "owner_absent", "foreign_lease" });
            var targetRoot = Path.Combine(Path.GetTempPath(), "boe-confirmed-claim-read-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(targetRoot);
            using var targetOwned = new CleanupOwnedFixture(targetRoot, line => _directGachaOutput?.WriteLine(line));
            var target = new FileSystemManager(targetRoot,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemManager>.Instance);
            target.EnsureDirectoryStructure();
            await using var targetLease = await target.AcquireCanonicalWriteLeaseAsync();
            if (mutation == "owner_absent")
                Assert.False(string.IsNullOrWhiteSpace(target.GetOrCreateSessionGeneration(targetLease)));
            else
                Assert.Null(target.ReadExistingSessionGeneration(targetLease));
            var sourceBefore = await CaptureConfirmedClaimReadImagesAsync(held.FileSystem, held.Lease);
            var targetBefore = await CaptureConfirmedClaimReadImagesAsync(target, targetLease);
            if (mutation == "foreign_lease")
            {
                var failure = Assert.Throws<InvalidOperationException>(() =>
                    AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(
                        held.FileSystem, targetLease, held.Request, finalization));
                Assert.Equal("Canonical write lease is not active for this game session.", failure.Message);
                WritePhysicalInjuryDiagnostic("fr063_confirmed_claim_inspection_invalid_lease", new
                    { mutation, FailureType = failure.GetType().FullName, failure.Message });
            }
            else
            {
                var expectedCode = mutation == "generation_absent"
                    ? "mortal_wound_treatment_confirmed_claim_generation_absent"
                    : "mortal_wound_treatment_confirmed_claim_owner_absent";
                var inspection = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(
                    target, targetLease, held.Request, finalization);
                WriteConfirmedClaimInspection(mutation, inspection);
                AssertConfirmedClaimReadRefusal(inspection, expectedCode);
                AssertConfirmedClaimReadRefusal(
                    AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(
                        target, targetLease, held.Request, finalization), expectedCode);
            }
            await AssertConfirmedClaimReadImagesAsync(held.FileSystem, held.Lease, sourceBefore);
            await AssertConfirmedClaimReadImagesAsync(target, targetLease, targetBefore);
        }
        finally { await held.ReleaseLeaseAsync(); }
    }

    private static void AssertConfirmedClaimReadRefusal(MortalWoundTreatmentPublicationProbeResult inspection, string code)
    {
        Assert.False(inspection.IsValid);
        Assert.Equal(code, Assert.Single(inspection.Issues).Code);
        Assert.Equal(0, inspection.ChangedCount);
        Assert.Null(inspection.State);
        Assert.Null(inspection.OperationKey);
        Assert.Null(inspection.AttemptId);
        Assert.Null(inspection.RequestFingerprint);
        Assert.Null(inspection.ResourceAuthorityFingerprint);
        Assert.Null(inspection.FinalizationFingerprint);
        Assert.Null(inspection.AgreementFingerprint);
        Assert.Null(inspection.GenerationFingerprint);
    }

    private void WriteConfirmedClaimInspection(string mutation, MortalWoundTreatmentPublicationProbeResult inspection) =>
        WritePhysicalInjuryDiagnostic("fr063_confirmed_claim_inspection", new
        {
            mutation, inspection.IsValid, inspection.Issues, inspection.State, inspection.ChangedCount,
            inspection.OperationKey, inspection.AttemptId, inspection.RequestFingerprint,
            inspection.ResourceAuthorityFingerprint, inspection.FinalizationFingerprint,
            inspection.AgreementFingerprint, inspection.SessionGeneration,
            inspection.SessionGenerationRevision, inspection.GenerationFingerprint
        });

    private static async Task<Dictionary<string, byte[]?>> CaptureConfirmedClaimReadImagesAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(files.GameSessionPath, "*", SearchOption.AllDirectories)
                     .Select(path => Path.GetRelativePath(files.GameSessionPath, path).Replace('\\', '/'))
                     .OrderBy(path => path, StringComparer.Ordinal))
            result.Add(path, await files.ReadFileBytesAsync(lease, path));
        result.Add("$sessionGeneration", files.ReadLocalGenerationSnapshot(lease).Bytes);
        return result;
    }

    private static async Task AssertConfirmedClaimReadImagesAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, IReadOnlyDictionary<string, byte[]?> before)
    {
        var after = await CaptureConfirmedClaimReadImagesAsync(files, lease);
        Assert.Equal(before.Keys.OrderBy(path => path, StringComparer.Ordinal),
            after.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
    }

    private static AcceptedMechanicsPlan AssertOrdinaryTreatmentOwnerPlan(HeldTreatmentPipelineContext held)
    {
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(held.FileSystem, held.Lease, out var binding, out var result));
        Assert.True(result.Success, DescribeValidationIssues(result.Issues));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
        var authority = Assert.IsType<MortalWoundTreatmentResourcePublicationAuthority>(plan.TreatmentResourcePublicationAuthority);
        Assert.Equal(held.Request.RequestFingerprint, authority.RequestFingerprint);
        Assert.Equal(held.Resolution.ResultFingerprint, authority.ResultFingerprint);
        held.SetCurrentAuthorities(authority.AcceptedStateAuthority, authority.RequestAuthority,
            authority.ResolutionAuthority, plan, binding);
        Assert.Equal(42, binding.Turn);
        return plan;
    }

    private static async Task AssertOrdinaryTreatmentOwnerPublishedAsync(HeldTreatmentPipelineContext held)
    {
        Assert.Equal(0, await ReadCurrentTreatmentEnergyAsync(held));
        Assert.Equal(1, CountHeldTreatmentEnergySpends(await ReadTreatmentResourceHistoryAsync(held.FileSystem)));
        var history = ReadCurrentTreatmentHistory(held.FileSystem);
        Assert.Equal(2, history.State!.Transitions.Count);
        var treatment = Assert.Single(history.State.Transitions, row => row.OperationKey == HeldTreatmentPipelineContext.OperationKey);
        Assert.NotNull(treatment.TreatmentResult);
        Assert.Equal(held.Request.RequestFingerprint, treatment.TreatmentResult!.Request.RequestFingerprint);
        Assert.Equal("stabilized", ReadCurrentTreatmentWound(held.FileSystem).Care!.State);
        var pendingBytes = await held.FileSystem.ReadFileBytesAsync(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        var catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(null,
            pendingBytes is null ? null : JsonSerializer.SerializeToElement(ParseJsonObjectBytes(pendingBytes)), history);
        Assert.True(catalog.IsValid, DescribeValidationIssues(catalog.Issues));
        Assert.Empty(catalog.HeldRequests);
        Assert.Single(catalog.FinalizedRequests);
        Assert.Null(await held.FileSystem.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
    }

    private static async Task<Dictionary<string, byte[]?>> CaptureOrdinaryTreatmentOwnerBytesAsync(HeldTreatmentPipelineContext held)
    {
        var paths = GuaranteedNativeMechanicalPaths
            .Concat(AcceptedMechanicsWoundCommonInputComposer.TreatmentOrdinaryGuardPaths)
            .Concat(new[]
            {
                AcceptedMechanicsPlan.WoundCommandPath, NpcCoreChangesContract.NpcCorePath,
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath
            }).Distinct(StringComparer.Ordinal);
        var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in paths) result.Add(path, await held.ReadFileBytesAsync(path));
        return result;
    }

    private static async Task AssertOrdinaryTreatmentOwnerBytesAsync(HeldTreatmentPipelineContext held, IReadOnlyDictionary<string, byte[]?> before)
    {
        foreach (var pair in before) Assert.Equal(pair.Value, await held.ReadFileBytesAsync(pair.Key));
    }
}
