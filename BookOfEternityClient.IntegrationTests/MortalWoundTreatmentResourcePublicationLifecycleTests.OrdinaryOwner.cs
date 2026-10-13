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
            var before = await CaptureOrdinaryTreatmentOwnerBytesAsync(held.FileSystem);
            await held.ReleaseLeaseAsync();
            var (engine, snapshot) = await CreateHeldTreatmentValidationEngineAsync(held);
            var issues = await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(engine,
                "ValidateResourceAndMortalQteDecisionAsync", true, snapshot, HeldTreatmentPipelineContext.Turn);
            WritePhysicalInjuryDiagnostic("fr063_component_ordinary_pending_duplicate", new
                { changed, Issues = issues, Before = MortalArchiveDigests(before), After = MortalArchiveDigests(await CaptureOrdinaryTreatmentOwnerBytesAsync(held.FileSystem)) });
            await held.AcquireLeaseAsync();
            if (changed)
            {
                Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(held.FileSystem, held.Lease));
                await AssertOrdinaryTreatmentOwnerBytesAsync(held.FileSystem, before);
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
            else command["result"]!["resultFingerprint"] = "sha256:" + new string('f', 64);
            await held.FileSystem.WriteFileAtomicAsync(held.Lease, AcceptedMechanicsPlan.WoundCommandPath, root.ToJsonString());
            var before = await CaptureOrdinaryTreatmentOwnerBytesAsync(held.FileSystem);
            await held.ReleaseLeaseAsync();
            var (engine, snapshot) = await CreateHeldTreatmentValidationEngineAsync(held);
            var issues = await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(engine,
                "ValidateResourceAndMortalQteDecisionAsync", true, snapshot, 42);
            WritePhysicalInjuryDiagnostic("fr063_component_ordinary_authority_tamper", new { mutation, Issues = issues });
            Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
            await held.AcquireLeaseAsync();
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(held.FileSystem, held.Lease));
            await AssertOrdinaryTreatmentOwnerBytesAsync(held.FileSystem, before);
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
            var before = await CaptureOrdinaryTreatmentOwnerBytesAsync(held.FileSystem);
            await held.ReleaseLeaseAsync();
            var (engine, snapshot) = await CreateHeldTreatmentValidationEngineAsync(held);
            var issues = await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(engine,
                "ValidateResourceAndMortalQteDecisionAsync", true, snapshot, 42);
            WritePhysicalInjuryDiagnostic("fr063_component_ordinary_current_capability", new { Issues = issues });
            Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
            await held.AcquireLeaseAsync();
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(held.FileSystem, held.Lease));
            await AssertOrdinaryTreatmentOwnerBytesAsync(held.FileSystem, before);
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
                "ValidateResourceAndMortalQteDecisionAsync", true, snapshot, 42));
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

    private static Task<Dictionary<string, byte[]?>> CaptureOrdinaryTreatmentOwnerBytesAsync(FileSystemManager files) =>
        CaptureMortalArchiveImagesAsync(files, GuaranteedNativeMechanicalPaths.Concat(new[]
        {
            AcceptedMechanicsPlan.WoundCommandPath, NpcCoreChangesContract.NpcCorePath,
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath
        }));

    private static async Task AssertOrdinaryTreatmentOwnerBytesAsync(FileSystemManager files, IReadOnlyDictionary<string, byte[]?> before)
    {
        foreach (var pair in before) Assert.Equal(pair.Value, await files.ReadFileBytesAsync(pair.Key));
    }
}
