using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuaranteedTreatment_ComponentOrdinaryOwnerCoalescesExactPendingDuplicateOrRefusesChangedGraph(bool changed)
    {
        await using var held = await CreateHeldTreatmentPipelineContextAsync(
            fault: null, withRollbackAuthority: true, composePublication: false,
            configureBeforePreparation: PrepareGuaranteedNativeNpcReceiptShapeAsync);
        using var owned = new CleanupOwnedFixture(held.Root, line => _directGachaOutput?.WriteLine(line));
        try
        {
            var command = ParseJsonObjectBytes(Assert.IsType<byte[]>(await held.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath)));
            var request = command["commands"]![0]!["authority"]!["request"]!.DeepClone().AsObject();
            var pending = ParseJsonObjectBytes(Assert.IsType<byte[]>(await held.ReadFileBytesAsync(WoundAcceptedTurnSnapshotContract.PendingResolutionPath)));
            foreach (var field in new[] { "sessionId", "requestId", "snapshotToken" })
                pending[field] = command[field]!.DeepClone();
            pending["submittedTreatmentRequests"] = new JsonArray(new JsonObject
            {
                ["operationKey"] = held.Request.Coordinates.OperationKey,
                ["attemptId"] = held.Request.Coordinates.AttemptId,
                ["requestFingerprint"] = held.Request.RequestFingerprint,
                ["request"] = request
            });
            if (changed) request["resourceAuthority"]!["claims"]![0]!["quantity"] = 1;
            await held.FileSystem.WriteFileAtomicAsync(held.Lease,
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath, pending.ToJsonString());
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(held.FileSystem, held.Lease));
            var before = await CaptureOrdinaryTreatmentOwnerBytesAsync(held);
            await held.ReleaseLeaseAsync();
            var (engine, snapshot) = await CreateHeldTreatmentValidationEngineAsync(held);
            var issues = await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(engine,
                "ValidateResourceAndMortalGuaranteedTreatmentAsync", true, snapshot, HeldTreatmentPipelineContext.Turn);
            WritePhysicalInjuryDiagnostic("fr063_component_ordinary_pending_duplicate", new
                { changed, Issues = issues, Before = MortalArchiveDigests(before), After = MortalArchiveDigests(await CaptureOrdinaryTreatmentOwnerBytesAsync(held)) });
            await held.AcquireLeaseAsync();
            if (changed)
            {
                Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(held.FileSystem, held.Lease));
                await AssertOrdinaryTreatmentOwnerBytesAsync(held, before);
            }
            else
            {
                Assert.Empty(issues);
                AssertOrdinaryTreatmentOwnerPlan(held);
                await held.ReleaseLeaseAsync();
                Assert.Equal(AcceptedTurnValidationDisposition.Accepted,
                    await InvokePrivateAsync<AcceptedTurnValidationDisposition>(engine,
                        "ValidateAcceptedTurnOutcomeWithRepairLoopAsync", "ordinary duplicate component", snapshot, null, 42, null));
                await AssertOrdinaryTreatmentOwnerPublishedAsync(held);
            }
        }
        finally { await held.ReleaseLeaseAsync(); }
    }

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
            await Assert.ThrowsAsync<InvalidDataException>(() => GetPrivateField<CanonicalStateNormalizer>(engine, "_normalizer")
                .BindTo(held.Lease).NormalizeAcceptedMechanicsAsync(backups: null));
            await AssertOrdinaryTreatmentOwnerBytesAsync(held, before);
            await AssertConfirmedHeldLiveRegistryProbeAsync(held);
            Assert.True(await ContainsCurrentExactTreatmentRequestAsync(held));
            Assert.Equal(2, await ReadCurrentTreatmentEnergyAsync(held));
            Assert.Single(ReadCurrentTreatmentHistory(held.FileSystem).State!.Transitions);
            WritePhysicalInjuryDiagnostic("fr063_component_ordinary_guard_lease_turnover", new
                { path, GuardBefore = plan.BeforeImages[path].Existed, Issues = issues, MechanicsAndRequestUnchanged = true });
        }
        finally { await held.ReleaseLeaseAsync(); }
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
