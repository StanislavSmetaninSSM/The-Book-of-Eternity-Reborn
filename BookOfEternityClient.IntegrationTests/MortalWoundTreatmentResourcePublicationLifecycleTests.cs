using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private const string CompensatedTreatmentPublicationExceptionName =
        "CompensatedTreatmentPublicationException";
    private const string CompensatedTreatmentPublicationBuilderName =
        "BuildCompensatedTreatmentPublicationExceptionAsync";
    private const string UnsafeTreatmentPublicationSettlementExceptionName =
        "UnsafeTreatmentPublicationSettlementException";
    private const string TreatmentPublicationSettlementMethodName =
        "SettleTreatmentResourcePublicationCompletionFailureAsync";

    [Fact]
    public async Task GuaranteedItemConsumption_HeldLeaseRawAdmissionDoesNotReacquireCanonicalLock()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(),
            composePublication: false);
        var before = await CaptureExplicitTreatmentEnvelopeBytesAsync(context);

        await RestoreHeldTreatmentAndComposeSameSemanticPlanAsync(context);

        await AssertExactTreatmentTransactionBytesAsync(context, before);
        Assert.True(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            context.Lease));
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
    }

    [Fact]
    public async Task GuaranteedItemConsumption_RepeatedRawItemAdmissionPreservesExactSealedSnapshotAndPlan()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack());
        var originalPlan = context.Plan;
        Assert.True(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
            context.FileSystem,
            context.Lease,
            context.OriginalBinding.SessionId,
            context.OriginalBinding.SnapshotToken,
            context.OriginalBinding.Turn,
            out var sealedBefore));
        Assert.True(sealedBefore.HasFinalPublicationBaseline);
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);

        await context.ReleaseLeaseAsync();
        var issues = await new ValidationService(
                context.FileSystem,
                NullLogger<ValidationService>.Instance)
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

        Assert.True(
            issues.All(static issue => issue.Severity != IssueSeverity.Error),
            DescribeValidationIssues(issues));
        await context.AcquireLeaseAsync();
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out var repeatedBinding,
            out var repeated));
        Assert.True(repeated.Success, DescribeValidationIssues(repeated.Issues));
        Assert.Same(originalPlan, repeated.Plan);
        Assert.Equal(
            AcceptedMechanicsPlanFingerprints.ComputeInput(context.OriginalBinding),
            AcceptedMechanicsPlanFingerprints.ComputeInput(repeatedBinding));
        Assert.True(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
            context.FileSystem,
            context.Lease,
            context.OriginalBinding.SessionId,
            context.OriginalBinding.SnapshotToken,
            context.OriginalBinding.Turn,
            out var sealedAfter));
        Assert.True(sealedAfter.HasFinalPublicationBaseline);
        Assert.True(sealedBefore.MatchesFinalPublicationBaseline(sealedAfter));
        var itemPublication = Assert.IsType<MortalWoundTreatmentItemPublicationAuthority>(
            originalPlan.TreatmentResourcePublicationAuthority?.ItemPublicationAuthority);
        Assert.True(itemPublication.MatchesNormalizationSnapshot(
            sealedAfter,
            originalPlan.OwnerAuthority));
        await AssertExactTreatmentTransactionBytesAsync(context, before);
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
    }

    [Fact]
    public async Task GuaranteedResourceQuantity_RepeatedRawItemAdmissionPreservesExactSealedSnapshotAndPlan()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null);
        var originalPlan = context.Plan;
        Assert.Null(originalPlan.TreatmentResourcePublicationAuthority?
            .ItemPublicationAuthority);
        Assert.True(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
            context.FileSystem,
            context.Lease,
            context.OriginalBinding.SessionId,
            context.OriginalBinding.SnapshotToken,
            context.OriginalBinding.Turn,
            out var sealedBefore));
        Assert.True(sealedBefore.HasFinalPublicationBaseline);
        Assert.True(sealedBefore.MatchesAcceptedOwnerAuthority(
            originalPlan.OwnerAuthority));
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);

        await context.ReleaseLeaseAsync();
        var issues = await new ValidationService(
                context.FileSystem,
                NullLogger<ValidationService>.Instance)
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

        Assert.True(
            issues.All(static issue => issue.Severity != IssueSeverity.Error),
            DescribeValidationIssues(issues));
        await context.AcquireLeaseAsync();
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out var repeatedBinding,
            out var repeated));
        Assert.True(repeated.Success, DescribeValidationIssues(repeated.Issues));
        Assert.Same(originalPlan, repeated.Plan);
        Assert.Equal(
            AcceptedMechanicsPlanFingerprints.ComputeInput(context.OriginalBinding),
            AcceptedMechanicsPlanFingerprints.ComputeInput(repeatedBinding));
        Assert.True(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
            context.FileSystem,
            context.Lease,
            context.OriginalBinding.SessionId,
            context.OriginalBinding.SnapshotToken,
            context.OriginalBinding.Turn,
            out var sealedAfter));
        Assert.True(sealedAfter.HasFinalPublicationBaseline);
        Assert.True(sealedBefore.MatchesFinalPublicationBaseline(sealedAfter));
        Assert.True(sealedAfter.MatchesAcceptedOwnerAuthority(
            originalPlan.OwnerAuthority));
        await AssertExactTreatmentTransactionBytesAsync(context, before);
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
    }

    [Fact]
    public async Task GuaranteedResourceQuantity_RepeatedRawItemAdmissionRejectsChangedItemRoot()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null);
        Assert.Null(context.Plan.TreatmentResourcePublicationAuthority?
            .ItemPublicationAuthority);
        Assert.True(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
            context.FileSystem,
            context.Lease,
            context.OriginalBinding.SessionId,
            context.OriginalBinding.SnapshotToken,
            context.OriginalBinding.Turn,
            out var sealedBefore));
        Assert.True(sealedBefore.HasFinalPublicationBaseline);
        var playerItems = await ReadJsonObjectAsync(
            context,
            InventoryEquipmentService.ItemsPath);
        var changedItem = Assert.Single(
            playerItems["items"]!.AsArray().OfType<JsonObject>());
        changedItem["count"] = 2;
        MortalItemTestFixture.ResealCanonical(changedItem);
        await context.FileSystem.WriteFileAtomicAsync(
            context.Lease,
            InventoryEquipmentService.ItemsPath,
            playerItems.ToJsonString());
        var afterDrift = await CaptureExactTreatmentTransactionBytesAsync(context);
        var resourceSpendsBefore = CountHeldTreatmentEnergySpends(
            await ReadTreatmentResourceHistoryAsync(context.FileSystem));

        await context.ReleaseLeaseAsync();
        var issues = await new ValidationService(
                context.FileSystem,
                NullLogger<ValidationService>.Instance)
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

        Assert.Contains(issues, static issue =>
            issue.Severity == IssueSeverity.Error &&
            string.Equals(
                issue.Code,
                "accepted_mechanics_wound_live_before_image_mismatch",
                StringComparison.Ordinal));
        await context.AcquireLeaseAsync();
        Assert.False(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
            context.FileSystem,
            context.Lease,
            context.OriginalBinding.SessionId,
            context.OriginalBinding.SnapshotToken,
            context.OriginalBinding.Turn,
            out _));
        await AssertExactTreatmentTransactionBytesAsync(context, afterDrift);
        Assert.Equal(resourceSpendsBefore, CountHeldTreatmentEnergySpends(
            await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
    }

    [Fact]
    public async Task GuaranteedItemConsumption_RepeatedRawItemAdmissionRejectsChangedLiveItemBeforeImage()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack());
        await ApplySelectedItemPostSealDriftAsync(context, "count");
        var afterDrift = await CaptureExactTreatmentTransactionBytesAsync(context);
        var resourceSpendsBefore = CountHeldTreatmentEnergySpends(
            await ReadTreatmentResourceHistoryAsync(context.FileSystem));

        await context.ReleaseLeaseAsync();
        var issues = await new ValidationService(
                context.FileSystem,
                NullLogger<ValidationService>.Instance)
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

        Assert.Contains(issues, static issue =>
            issue.Severity == IssueSeverity.Error);
        await context.AcquireLeaseAsync();
        Assert.False(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
            context.FileSystem,
            context.Lease,
            context.OriginalBinding.SessionId,
            context.OriginalBinding.SnapshotToken,
            context.OriginalBinding.Turn,
            out _));
        await AssertExactTreatmentTransactionBytesAsync(context, afterDrift);
        Assert.Equal(resourceSpendsBefore, CountHeldTreatmentEnergySpends(
            await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
    }

    [Fact]
    [Trait("Category", "FullValidation")]
    public async Task GuaranteedItemConsumption_ExactPublishedNpcInventoryPassesFullStateAndRetry()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack());
        var probe = await CreatePublishedTreatmentFullStateProbeAsync(context);
        try
        {
            var filtered = await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(
                probe.Transaction,
                "FilterExactPublishedItemValidationIssuesAsync",
                context.FileSystem,
                probe.Issues);

            Assert.DoesNotContain(filtered, static issue =>
                issue.Severity == IssueSeverity.Error);
        }
        finally
        {
            await Assert.IsAssignableFrom<IAsyncDisposable>(probe.Transaction)
                .DisposeAsync();
        }

        var retry = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            probe.Engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "exact published NPC inventory retry oracle",
            probe.SnapshotContext,
            null,
            HeldTreatmentPipelineContext.Turn,
            null);

        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, retry);
        Assert.Equal(1, await ReadSelectedNpcItemCountAsync(context));
    }

    [Theory]
    [InlineData("no_open_receipt")]
    [InlineData("root_drift")]
    [InlineData("foreign_actor")]
    [InlineData("foreign_path")]
    [InlineData("extra_inventory_mutation")]
    [Trait("Category", "FullValidation")]
    public async Task GuaranteedItemConsumption_FullStateInventoryIssueRequiresExactOpenPublicationProof(
        string adversarialAxis)
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack());
        var probe = await CreatePublishedTreatmentFullStateProbeAsync(context);
        try
        {
            var issue = Assert.Single(probe.Issues, static candidate =>
                string.Equals(
                    candidate.Code,
                    "npc_existing_inventory_resend_forbidden",
                    StringComparison.Ordinal));
            IReadOnlyList<ValidationIssue> supplied = probe.Issues;
            switch (adversarialAxis)
            {
                case "no_open_receipt":
                    _ = await InvokePrivateTaskResultAsync(
                        probe.Transaction,
                        "CompleteAsync",
                        context.FileSystem);
                    break;
                case "root_drift":
                {
                    var root = await ReadJsonObjectAsync(
                        context.FileSystem,
                        NpcCoreChangesContract.NpcCorePath);
                    ReadProvider(root)["displayName"] = "unplanned root drift";
                    await context.FileSystem.WriteFileAtomicAsync(
                        NpcCoreChangesContract.NpcCorePath,
                        root.ToJsonString());
                    break;
                }
                case "foreign_actor":
                    supplied = new[]
                    {
                        CloneValidationIssue(
                            issue,
                            actor: "mortal_npc:unrelated_actor")
                    };
                    break;
                case "foreign_path":
                    supplied = new[]
                    {
                        CloneValidationIssue(
                            issue,
                            filePath:
                                NpcCoreChangesContract.NpcCorePath +
                                ".NPCsInScene[1].inventory")
                    };
                    break;
                case "extra_inventory_mutation":
                {
                    var root = await ReadJsonObjectAsync(
                        context.FileSystem,
                        NpcCoreChangesContract.NpcCorePath);
                    var provider = ReadProvider(root);
                    var inventory = provider["inventory"]!.AsArray();
                    var selected = Assert.IsType<JsonObject>(Assert.Single(inventory));
                    selected["count"] = 0;
                    await context.FileSystem.WriteFileAtomicAsync(
                        NpcCoreChangesContract.NpcCorePath,
                        root.ToJsonString());
                    supplied = new[]
                    {
                        CloneValidationIssue(
                            issue,
                            actual: inventory.ToJsonString())
                    };
                    break;
                }
                default:
                    throw new InvalidOperationException(
                        $"Unknown adversarial axis '{adversarialAxis}'.");
            }

            var filtered = await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(
                probe.Transaction,
                "FilterExactPublishedItemValidationIssuesAsync",
                context.FileSystem,
                supplied);

            Assert.Contains(filtered, candidate =>
                string.Equals(
                    candidate.Code,
                    "npc_existing_inventory_resend_forbidden",
                    StringComparison.Ordinal));
        }
        finally
        {
            await Assert.IsAssignableFrom<IAsyncDisposable>(probe.Transaction)
                .DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "FullValidation")]
    public async Task GuaranteedItemConsumption_FullStateInventoryIssueFilteringRequiresExactlyOneProvenMatch()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack());
        var probe = await CreatePublishedTreatmentFullStateProbeAsync(context);
        try
        {
            var issue = Assert.Single(probe.Issues, static candidate =>
                string.Equals(
                    candidate.Code,
                    "npc_existing_inventory_resend_forbidden",
                    StringComparison.Ordinal));

            IReadOnlyList<ValidationIssue> repeatedInstance = new[] { issue, issue };
            var repeatedInstanceResult =
                await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(
                    probe.Transaction,
                    "FilterExactPublishedItemValidationIssuesAsync",
                    context.FileSystem,
                    repeatedInstance);
            Assert.Same(repeatedInstance, repeatedInstanceResult);
            Assert.Collection(
                repeatedInstanceResult,
                candidate => Assert.Same(issue, candidate),
                candidate => Assert.Same(issue, candidate));

            var equivalentFirst = CloneValidationIssue(issue);
            var equivalentSecond = CloneValidationIssue(issue);
            IReadOnlyList<ValidationIssue> equivalentInstances =
                new[] { equivalentFirst, equivalentSecond };
            var equivalentInstancesResult =
                await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(
                    probe.Transaction,
                    "FilterExactPublishedItemValidationIssuesAsync",
                    context.FileSystem,
                    equivalentInstances);
            Assert.Same(equivalentInstances, equivalentInstancesResult);
            Assert.Collection(
                equivalentInstancesResult,
                candidate => Assert.Same(equivalentFirst, candidate),
                candidate => Assert.Same(equivalentSecond, candidate));

            var unrelated = new ValidationIssue(
                "game_state/unrelated.json",
                IssueSeverity.Error,
                "An unrelated validation error must remain.",
                code: "unrelated_validation_error");
            IReadOnlyList<ValidationIssue> oneProvenMatch = new[] { unrelated, issue };
            var oneProvenMatchResult =
                await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(
                    probe.Transaction,
                    "FilterExactPublishedItemValidationIssuesAsync",
                    context.FileSystem,
                    oneProvenMatch);
            Assert.Same(unrelated, Assert.Single(oneProvenMatchResult));
        }
        finally
        {
            await Assert.IsAssignableFrom<IAsyncDisposable>(probe.Transaction)
                .DisposeAsync();
        }
    }

    [Theory]
    [InlineData("runtime_refresh", 1)]
    [InlineData("wound_post_seal", 1)]
    [InlineData("wound_output", 1)]
    [InlineData("critical_validation", 1)]
    [InlineData("full_state_validation", 1)]
    [InlineData("cleanup", 1)]
    [InlineData("runtime_refresh", 2)]
    public async Task GuaranteedMixedItemAndResourceConsumption_EveryPipelineFailureRestoresExpandedRootsAndExactRetryCommitsOnce(
        string boundary,
        int occurrence)
    {
        var fault = new AcceptedTreatmentPipelineFault(boundary, occurrence);
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault,
            itemScenario: HeldTreatmentItemScenario.SelectedStack());
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);
        AssertTreatmentItemTransactionEnvelopeCaptured(before);
        var itemTransitionsBefore = await CountSelectedItemConsumeTransitionsAsync(context);
        var resourceSpendsBefore = CountHeldTreatmentEnergySpends(
            await ReadTreatmentResourceHistoryAsync(context.FileSystem));
        var itemCapacityTransitionsBefore =
            CountSelectedItemDurabilityCapacityTransitions(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem));
        var originalPlan = context.Plan;
        var originalBindingFingerprint =
            AcceptedMechanicsPlanFingerprints.ComputeInput(context.OriginalBinding);
        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) = await CreateHeldTreatmentValidationEngineAsync(context);

        fault.Arm(context);
        AcceptedTurnValidationDisposition? firstDisposition = null;
        var firstException = await Record.ExceptionAsync(async () =>
        {
            firstDisposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "selected item rollback oracle",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);
        });

        Assert.True(
            fault.Fired,
            $"Observed={string.Join(", ", fault.ObservedPhases)}; " +
            $"Disposition={firstDisposition?.ToString() ?? "null"}; " +
            $"Exception={firstException}");
        if (boundary is "critical_validation" or
            "full_state_validation" or
            "cleanup" ||
            string.Equals(boundary, "runtime_refresh", StringComparison.Ordinal) &&
            occurrence == 2)
        {
            Assert.IsType<IOException>(firstException);
            Assert.Null(firstDisposition);
        }
        else
        {
            Assert.Null(firstException);
            Assert.Equal(
                AcceptedTurnValidationDisposition.RetryablePublicationRearmed,
                firstDisposition);
        }
        await AssertExactTreatmentTransactionBytesAsync(context, before);
        Assert.Equal(
            itemTransitionsBefore,
            await CountSelectedItemConsumeTransitionsAsync(context));
        Assert.Equal(
            resourceSpendsBefore,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(
            itemCapacityTransitionsBefore,
            CountSelectedItemDurabilityCapacityTransitions(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));

        await context.AcquireLeaseAsync();
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out var rearmedBinding,
            out var rearmed));
        Assert.True(rearmed.Success, DescribeValidationIssues(rearmed.Issues));
        Assert.Same(originalPlan, rearmed.Plan);
        Assert.Equal(
            originalBindingFingerprint,
            AcceptedMechanicsPlanFingerprints.ComputeInput(rearmedBinding));
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        await context.ReleaseLeaseAsync();

        var retryDisposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "selected item exact retry oracle",
            snapshotContext,
            null,
            HeldTreatmentPipelineContext.Turn,
            null);

        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, retryDisposition);
        Assert.Equal(1, await ReadSelectedNpcItemCountAsync(context));
        Assert.Equal(itemTransitionsBefore + 1,
            await CountSelectedItemConsumeTransitionsAsync(context));
        Assert.Equal(resourceSpendsBefore + 1,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(itemCapacityTransitionsBefore + 1,
            CountSelectedItemDurabilityCapacityTransitions(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        var durability = await ReadSelectedItemDurabilityAsync(context);
        Assert.Equal(4m, durability.Current);
        Assert.Equal(4m, durability.Maximum);
    }

    [Fact]
    public async Task GuaranteedItemConsumption_AcceptedNormalizerWritesOnlySealedItemPhaseBeforeOrdinaryTail()
    {
        var probe = new MortalItemFinalBaselineFault("observe_item_phase");
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(),
            hooks: probe.Hooks,
            baselineFixture:
                MortalItemPublicationBaselineFixture.ItemJournalTailDelta |
                MortalItemPublicationBaselineFixture.CanonicalNpcTradeReceipts);
        var itemPublication = Assert.IsType<
            MortalWoundTreatmentItemPublicationAuthority>(
            context.Plan.TreatmentResourcePublicationAuthority?
                .ItemPublicationAuthority);
        const string tailPath = "game_state/npcs/item_journals.json";
        Assert.False(JsonNode.DeepEquals(
            itemPublication.ItemPhase.ItemPhaseAfterImages[tailPath],
            itemPublication.Baseline.FinalCarrierRoots[tailPath]));

        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);
        probe.Arm(context);

        var disposition =
            await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "sealed item-phase boundary oracle",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);

        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, disposition);
        Assert.True(probe.Fired);
        Assert.True(probe.ItemPhaseDiffersFromFinalBaseline);
        Assert.True(probe.LiveItemPhaseMatchedBeforeTail);
    }

    [Theory]
    [InlineData("pass_through_presence")]
    [InlineData("pass_through_content")]
    [InlineData("identity_index")]
    [InlineData("vehicle_topology")]
    public async Task GuaranteedItemConsumption_PostTailFinalBaselineDriftRejectsBeforeCommonWriteAndExactRetryCommitsOnce(
        string driftAxis)
    {
        var fixture = driftAxis switch
        {
            "pass_through_content" =>
                MortalItemPublicationBaselineFixture.PassThroughRecipeObject,
            "vehicle_topology" =>
                MortalItemPublicationBaselineFixture.LegacyVehicleObject,
            _ => MortalItemPublicationBaselineFixture.None
        };
        var fault = new MortalItemFinalBaselineFault(driftAxis);
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(),
            hooks: fault.Hooks,
            baselineFixture: fixture);
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);
        AssertTreatmentItemTransactionEnvelopeCaptured(before);
        var itemTransitionsBefore =
            await CountSelectedItemConsumeTransitionsAsync(context);
        var resourceSpendsBefore = CountHeldTreatmentEnergySpends(
            await ReadTreatmentResourceHistoryAsync(context.FileSystem));
        var itemCapacityTransitionsBefore =
            CountSelectedItemDurabilityCapacityTransitions(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem));
        var originalPlan = context.Plan;
        var originalBindingFingerprint =
            AcceptedMechanicsPlanFingerprints.ComputeInput(
                context.OriginalBinding);

        await context.ReleaseLeaseAsync();
        fault.Arm(context);
        var firstException = await Record.ExceptionAsync(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem,
                new CanonicalStateNormalizer(
                    context.FileSystem,
                    NullLogger<CanonicalStateNormalizer>.Instance),
                new ValidationService(
                    context.FileSystem,
                    NullLogger<ValidationService>.Instance),
                new Dictionary<string, string>()));

        Assert.True(
            firstException is InvalidDataException,
            firstException?.ToString());
        var mismatch = (InvalidDataException)firstException!;
        Assert.StartsWith(
            "mortal_wound_treatment_publication_live_item_baseline_mismatch:",
            mismatch.Message,
            StringComparison.Ordinal);
        Assert.Contains(fault.DriftPath, mismatch.Message, StringComparison.Ordinal);
        Assert.True(fault.Fired);
        Assert.True(fault.FinalBaselineMatchedBeforeDrift);
        Assert.False(fault.CommonPublicationMutationObserved);
        await AssertExactTreatmentTransactionBytesAsync(context, before);
        Assert.Equal(
            itemTransitionsBefore,
            await CountSelectedItemConsumeTransitionsAsync(context));
        Assert.Equal(
            resourceSpendsBefore,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(
            itemCapacityTransitionsBefore,
            CountSelectedItemDurabilityCapacityTransitions(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));

        await context.AcquireLeaseAsync();
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out var rearmedBinding,
            out var rearmed));
        Assert.True(rearmed.Success, DescribeValidationIssues(rearmed.Issues));
        Assert.Same(originalPlan, rearmed.Plan);
        Assert.Equal(
            originalBindingFingerprint,
            AcceptedMechanicsPlanFingerprints.ComputeInput(rearmedBinding));
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        await context.ReleaseLeaseAsync();

        fault.Disarm();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);
        var retryDisposition =
            await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "exact live item baseline retry oracle",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);

        Assert.Equal(
            AcceptedTurnValidationDisposition.Accepted,
            retryDisposition);
        Assert.Equal(1, await ReadSelectedNpcItemCountAsync(context));
        Assert.Equal(
            itemTransitionsBefore + 1,
            await CountSelectedItemConsumeTransitionsAsync(context));
        Assert.Equal(
            resourceSpendsBefore + 1,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(
            itemCapacityTransitionsBefore + 1,
            CountSelectedItemDurabilityCapacityTransitions(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
    }

    [Theory]
    [InlineData(
        "count",
        "mortal_wound_treatment_publication_item_authority_changed",
        "game_state/wounds/accepted_turn_plan",
        "the exact already validated item projection authority",
        "missing or changed item cache proof")]
    [InlineData(
        "carrier",
        "mortal_wound_treatment_publication_item_authority_changed",
        "game_state/wounds/accepted_turn_plan",
        "the exact already validated item projection authority",
        "missing or changed item cache proof")]
    [InlineData(
        "index",
        "mortal_wound_treatment_publication_item_authority_changed",
        "game_state/wounds/accepted_turn_plan",
        "the exact already validated item projection authority",
        "missing or changed item cache proof")]
    [InlineData(
        "resource_capacity",
        "resource_state_history_snapshot_mismatch",
        ResourceMaterializationContract.StatePath,
        "live snapshot equals latest immutable history after-state",
        "transition_selected_dressing_durability_initialize")]
    [InlineData(
        "resource_current",
        "resource_state_history_snapshot_mismatch",
        ResourceMaterializationContract.StatePath,
        "live snapshot equals latest immutable history after-state",
        "transition_selected_dressing_durability_initialize")]
    [InlineData(
        "shared_npc_mirror",
        "mortal_wound_treatment_publication_item_authority_changed",
        "game_state/wounds/accepted_turn_plan",
        "the exact already validated item projection authority",
        "missing or changed item cache proof")]
    public async Task GuaranteedItemConsumption_PostSealDriftRejectsBeforeAnySpend(
        string driftAxis,
        string expectedCode,
        string expectedPath,
        string expected,
        string actual)
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(
                publishNpcSkillChange: string.Equals(
                    driftAxis,
                    "shared_npc_mirror",
                    StringComparison.Ordinal)));
        var resourceSpendsBefore = CountHeldTreatmentEnergySpends(
            await ReadTreatmentResourceHistoryAsync(context.FileSystem));
        await ApplySelectedItemPostSealDriftAsync(context, driftAxis);
        var itemTransitionsAfterDrift =
            await CountSelectedItemConsumeTransitionsAsync(context);
        var postDrift = await CaptureExactTreatmentTransactionBytesAsync(context);
        AssertTreatmentItemTransactionEnvelopeCaptured(postDrift);
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        AcceptedMechanicsPlanAuthority.InvalidateValidated(
            context.FileSystem,
            context.Lease);

        var rejected = WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(
            context.FileSystem,
            context.Lease,
            context.CreateMechanicsOnlyTreatmentProposal(),
            context.AcceptedState,
            context.Request,
            context.Resolution);

        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Plan);
        var driftIssue = Assert.Single(rejected.Issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            string.Equals(issue.Code, expectedCode, StringComparison.Ordinal) &&
            string.Equals(issue.FilePath, expectedPath, StringComparison.Ordinal));
        Assert.Equal(expected, driftIssue.Expected);
        Assert.Equal(actual, driftIssue.Actual);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            context.Lease));
        await AssertExactTreatmentTransactionBytesAsync(context, postDrift);
        Assert.Equal(itemTransitionsAfterDrift,
            await CountSelectedItemConsumeTransitionsAsync(context));
        Assert.Equal(resourceSpendsBefore,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
        Assert.Equal(2, await ReadCurrentTreatmentEnergyAsync(context));
        Assert.True(await ContainsCurrentExactTreatmentRequestAsync(context));
        Assert.Equal("held", context.Request.ResourceAuthority.ReservationDisposition);
    }

    [Fact]
    public async Task GuaranteedItemConsumption_NpcSkillAndItemShareOneComposedNpcCoreRoot()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(
                publishNpcSkillChange: true));
        var npcAfterImage = Assert.Single(
            context.Plan.OwnerCompanionAfterImages,
            pair => string.Equals(
                pair.Key,
                NpcCoreChangesContract.NpcCorePath,
                StringComparison.Ordinal)).Value;
        Assert.Equal(1, ReadNpcItemCount(npcAfterImage, HeldTreatmentPipelineContext.SelectedItemId));
        Assert.Contains(
            ReadProvider(npcAfterImage)["activeSkills"]!.AsArray().OfType<JsonObject>(),
            skill => string.Equals(
                skill["displayName"]?.GetValue<string>(),
                HeldTreatmentPipelineContext.ChangedSkillDisplayName,
                StringComparison.Ordinal));
        var itemTransitionsBefore = await CountSelectedItemConsumeTransitionsAsync(context);
        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) = await CreateHeldTreatmentValidationEngineAsync(context);

        var disposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "selected item and npc skill shared root oracle",
            snapshotContext,
            null,
            HeldTreatmentPipelineContext.Turn,
            null);

        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, disposition);
        var persistedNpcCore = await ReadJsonObjectAsync(
            context.FileSystem,
            NpcCoreChangesContract.NpcCorePath);
        Assert.True(JsonNode.DeepEquals(npcAfterImage, persistedNpcCore));
        Assert.Equal(1, ReadNpcItemCount(
            persistedNpcCore,
            HeldTreatmentPipelineContext.SelectedItemId));
        Assert.Equal(itemTransitionsBefore + 1,
            await CountSelectedItemConsumeTransitionsAsync(context));
    }

    [Fact]
    public async Task GuaranteedItemConsumption_ColdAcceptedReplayDoesNotAppendItemOrResourceTransitions()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack());
        await context.RestartAsync(hooks: null);
        await RestoreHeldTreatmentAndComposeSameSemanticPlanAsync(context);
        var itemTransitionsBefore = await CountSelectedItemConsumeTransitionsAsync(context);
        var resourceSpendsBefore = CountHeldTreatmentEnergySpends(
            await ReadTreatmentResourceHistoryAsync(context.FileSystem));
        await context.ReleaseLeaseAsync();
        var validator = new ValidationService(
            context.FileSystem,
            NullLogger<ValidationService>.Instance);
        var rawItemIssues = await validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.True(
            rawItemIssues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeValidationIssues(rawItemIssues));
        var rawResourceIssues = await validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(
            rawResourceIssues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeValidationIssues(rawResourceIssues));
        var (engine, snapshotContext) = await CreateHeldTreatmentValidationEngineAsync(context);
        await InvokePrivateTaskAsync(
            engine,
            "EnsureClientOwnedSystemFilesHealthyAsync");
        var aggregateRawIssues = await InvokePrivateAsync<List<ValidationIssue>>(
            engine,
            "CollectAcceptedTurnRawStateIssuesAsync");
        Assert.True(
            aggregateRawIssues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeValidationIssues(aggregateRawIssues));
        var disposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "selected item cold commit oracle",
            snapshotContext,
            null,
            HeldTreatmentPipelineContext.Turn,
            null);
        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, disposition);

        var committedNpcCore = await context.FileSystem.ReadFileBytesAsync(
            NpcCoreChangesContract.NpcCorePath);
        var committedItemIdentity = await context.FileSystem.ReadFileBytesAsync(
            MortalItemIdentityState.StatePath);
        var committedResourceState = await context.FileSystem.ReadFileBytesAsync(
            ResourceMaterializationContract.StatePath);
        var committedResourceHistory = await context.FileSystem.ReadFileBytesAsync(
            ResourceMaterializationContract.HistoryPath);
        Assert.Equal(1, await ReadSelectedNpcItemCountAsync(context));
        Assert.Equal(itemTransitionsBefore + 1,
            await CountSelectedItemConsumeTransitionsAsync(context));
        Assert.Equal(resourceSpendsBefore + 1,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));

        await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(
            new LiveTurnPreparationOptions
            {
                SessionId = "session_t070b4_item_replay",
                RequestId = "request_t070b4_item_replay",
                TurnNumber = HeldTreatmentPipelineContext.Turn,
                PlayerAction = "Replay the already accepted mixed treatment.",
                CurrentRealm = "Mortal World",
                PreGeneratedDices1d20 = new[] { 17, 4, 1, 20 }
            });
        await context.RestartAsync(hooks: null);
        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var recovered = acceptedState.RestorePersistedTreatmentRequests(history);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        Assert.Empty(recovered.HeldRequests);
        var finalized = Assert.Single(recovered.FinalizedRequests);
        var replay = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(
            finalized,
            history,
            ReadCurrentTreatmentWound(context.FileSystem),
            acceptedState);

        Assert.Equal("ExactReplay", replay.Disposition);
        Assert.NotNull(replay.ReplayReceipt);
        Assert.Equal(committedNpcCore, await context.FileSystem.ReadFileBytesAsync(
            NpcCoreChangesContract.NpcCorePath));
        Assert.Equal(committedItemIdentity, await context.FileSystem.ReadFileBytesAsync(
            MortalItemIdentityState.StatePath));
        Assert.Equal(committedResourceState, await context.FileSystem.ReadFileBytesAsync(
            ResourceMaterializationContract.StatePath));
        Assert.Equal(committedResourceHistory, await context.FileSystem.ReadFileBytesAsync(
            ResourceMaterializationContract.HistoryPath));
        Assert.Equal(itemTransitionsBefore + 1,
            await CountSelectedItemConsumeTransitionsAsync(context));
        Assert.Equal(resourceSpendsBefore + 1,
            CountHeldTreatmentEnergySpends(
                await ReadTreatmentResourceHistoryAsync(context.FileSystem)));
    }

    [Fact]
    [Trait("Category", "FullValidation")]
    public async Task GuaranteedItemConsumption_SameTurnItemNormalizationSurvivesPublication()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(
                createUnrelatedSameTurnItem: true));
        Assert.DoesNotContain(
            InventoryEquipmentService.ItemsPath,
            context.Plan.OwnerCompanionAfterImages.Keys);
        Assert.True(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
            context.FileSystem,
            context.Lease,
            context.OriginalBinding.SessionId,
            context.OriginalBinding.SnapshotToken,
            context.OriginalBinding.Turn,
            out var sealedItemSnapshot));
        Assert.True(sealedItemSnapshot.TryGetAllocatedItemId(
            HeldTreatmentPipelineContext.UnrelatedCreationRef,
            out var reservedUnrelatedItemId));
        Assert.True(sealedItemSnapshot.TryGetRootReceiptId(
            HeldTreatmentPipelineContext.UnrelatedCreationRef,
            out var reservedUnrelatedReceiptId));
        Assert.True(sealedItemSnapshot.TryGetCreateTransitionId(
            HeldTreatmentPipelineContext.UnrelatedCreationRef,
            out var reservedUnrelatedTransitionId));
        var rawPlayerItems = await ReadJsonObjectAsync(
            context,
            InventoryEquipmentService.ItemsPath);
        var rawUnrelated = Assert.Single(
            rawPlayerItems["UpdateInventory"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(
            HeldTreatmentPipelineContext.UnrelatedCreationRef,
            rawUnrelated["creationRef"]!.GetValue<string>());
        Assert.Equal(
            HeldTreatmentPipelineContext.UnrelatedCreationRef,
            rawUnrelated["materialization"]!["creationRef"]!.GetValue<string>());
        Assert.Null(rawUnrelated["itemId"]);
        Assert.Equal(1, ReadNpcItemCount(
            context.Plan.OwnerCompanionAfterImages[NpcCoreChangesContract.NpcCorePath],
            HeldTreatmentPipelineContext.SelectedItemId));
        await context.ReleaseLeaseAsync();
        var validationEngine = CreateGameEngine(
            new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Escape) }),
            fileSystem: context.FileSystem);
        await InvokePrivateTaskAsync(
            validationEngine,
            "EnsureClientOwnedSystemFilesHealthyAsync");
        var rawIssues = await InvokePrivateAsync<List<ValidationIssue>>(
            validationEngine,
            "CollectAcceptedTurnRawStateIssuesAsync");
        Assert.True(
            rawIssues.All(static issue => issue.Severity != IssueSeverity.Error),
            DescribeValidationIssues(rawIssues));
        var probe = await CreatePublishedTreatmentFullStateProbeAsync(context);
        try
        {
            var filtered = await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(
                probe.Transaction,
                "FilterExactPublishedItemValidationIssuesAsync",
                context.FileSystem,
                probe.Issues);
            Assert.True(
                filtered.All(static issue => issue.Severity != IssueSeverity.Error),
                DescribeValidationIssues(filtered));
        }
        finally
        {
            await Assert.IsAssignableFrom<IAsyncDisposable>(probe.Transaction)
                .DisposeAsync();
        }

        var disposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            probe.Engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "same-turn item plus selected treatment item oracle",
            probe.SnapshotContext,
            null,
            HeldTreatmentPipelineContext.Turn,
            null);

        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, disposition);
        var playerItems = await ReadJsonObjectAsync(
            context.FileSystem,
            InventoryEquipmentService.ItemsPath);
        var persistedUnrelated = Assert.Single(
            playerItems["items"]!.AsArray().OfType<JsonObject>(),
            HasUnrelatedCreationReceipt);
        var persistedUnrelatedId = persistedUnrelated["itemId"]!.GetValue<string>();
        Assert.Equal(reservedUnrelatedItemId, persistedUnrelatedId);
        var receipt = Assert.IsType<JsonObject>(
            persistedUnrelated["materializationReceipt"]);
        Assert.Equal(
            reservedUnrelatedReceiptId,
            receipt["receiptId"]!.GetValue<string>());
        Assert.Equal(
            HeldTreatmentPipelineContext.UnrelatedCreationRef,
            receipt["creationRef"]!.GetValue<string>());
        var identity = await ReadTreatmentItemIdentityAsync(context.FileSystem);
        var identityEntry = identity.EntriesByItemId[persistedUnrelatedId];
        Assert.Equal("active", identityEntry["state"]!.GetValue<string>());
        Assert.Equal(
            reservedUnrelatedReceiptId,
            identityEntry["receiptId"]!.GetValue<string>());
        Assert.Equal(
            HeldTreatmentPipelineContext.UnrelatedCreationRef,
            Assert.Single(identityEntry["originCreationRefs"]!.AsArray())!
                .GetValue<string>());
        var createTransition = Assert.IsType<JsonObject>(
            Assert.Single(identityEntry["transitions"]!.AsArray()));
        Assert.Equal(
            reservedUnrelatedTransitionId,
            createTransition["transitionId"]!.GetValue<string>());
        Assert.Equal("create", createTransition["kind"]!.GetValue<string>());
        Assert.Equal(HeldTreatmentPipelineContext.Turn,
            createTransition["turn"]!.GetValue<int>());
        Assert.Equal(0, createTransition["quantityBefore"]!.GetValue<int>());
        Assert.Equal(1, createTransition["quantityAfter"]!.GetValue<int>());
        Assert.Equal(1, await ReadSelectedNpcItemCountAsync(context));
        Assert.Equal(1, await CountSelectedItemConsumeTransitionsAsync(context));
    }

    [Fact]
    public async Task GuaranteedItemConsumption_UnsupportedCompanionReferenceRejectsBeforeAnyWrite()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(
                initialCount: 1,
                companion: HeldTreatmentItemCompanion.Container),
            composePublication: false);
        await context.ReleaseLeaseAsync();
        var itemIssues = await new ValidationService(
                context.FileSystem,
                NullLogger<ValidationService>.Instance)
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(itemIssues, issue =>
            issue.Severity == IssueSeverity.Error);
        await context.AcquireLeaseAsync();
        var authorities = RestoreHeldTreatmentAuthorities(context);
        var before = await CaptureExplicitTreatmentEnvelopeBytesAsync(context);

        var publication = WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(
            context.FileSystem,
            context.Lease,
            new GameResponse(),
            authorities.AcceptedState,
            authorities.Request,
            authorities.Resolution);

        Assert.False(publication.IsValid);
        Assert.Null(publication.Plan);
        Assert.Contains(publication.Issues, issue =>
            issue.Code?.Contains("companion", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(publication.Issues, issue =>
            issue.Code?.Contains("unsupported_boundary", StringComparison.OrdinalIgnoreCase) == true);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            context.Lease));
        await AssertExactTreatmentTransactionBytesAsync(context, before);
        Assert.Equal(1, await ReadSelectedNpcItemCountAsync(context));
        Assert.Equal(2, await ReadCurrentTreatmentEnergyAsync(context));
        Assert.Equal(0, await CountSelectedItemConsumeTransitionsAsync(context));
    }

    [Fact]
    public async Task GuaranteedResourceQuantity_ResourceFreeCommonPlanKeepsOpenTransactionCarrierEmpty()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.WriteExactJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.CommandPath,
            ResourceMaterializationValidationTests.DefinitionAndInitializationCommand()
                .ToJsonString());

        var itemIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        var resourceIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(itemIssues, static issue =>
            issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(resourceIssues, static issue =>
            issue.Severity == IssueSeverity.Error);

        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem,
            context.Normalizer,
            context.Validator,
            new Dictionary<string, string>(StringComparer.Ordinal));
        var carrier = RequireOpenTreatmentTransactionCarrier();

        Assert.NotNull(result.MechanicsPlan);
        Assert.Null(carrier.GetValue(result));
    }

    [Fact]
    public void GuaranteedResourceQuantity_CanonicalRefreshReturnsOneOpaqueOpenTransactionCarrier()
    {
        var property = RequireOpenTreatmentTransactionCarrier();
        var carrierType = property.PropertyType;
        var gameEngineProperty = RequireGameEngineRefreshTransactionCarrier(carrierType);
        RequireCarrierForwardingDataFlow(property, gameEngineProperty);

        Assert.False(
            carrierType.IsPublic || carrierType.IsNestedPublic,
            "The held-treatment publication transaction must remain an internal production authority.");
        Assert.True(
            carrierType.IsSealed,
            "The held-treatment publication transaction must be a closed, non-extensible authority.");
        Assert.Empty(carrierType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Contains(typeof(IAsyncDisposable), carrierType.GetInterfaces());

        var completion = carrierType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.DeclaringType == carrierType)
            .Where(static method => IsCompletionMethodName(method.Name))
            .ToArray();
        var compensation = carrierType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.DeclaringType == carrierType)
            .Where(static method => IsCompensationMethodName(method.Name))
            .ToArray();

        var complete = Assert.Single(completion);
        var compensate = Assert.Single(compensation);
        Assert.False(complete.IsPublic);
        Assert.False(compensate.IsPublic);
        Assert.True(IsAwaitableReturn(complete.ReturnType));
        Assert.True(IsAwaitableReturn(compensate.ReturnType));
        Assert.Equal(carrierType, gameEngineProperty.PropertyType);
    }

    [Fact]
    public void GuaranteedResourceQuantity_FullPipelineOwnsCompensationScopeAndCompletesAfterFinalRefresh()
    {
        var analysis = ReadMethodAnalysis(
            "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync");
        _ = RequireControlFlowGraph(analysis);
        _ = RequireOpenTreatmentTransactionCarrier();
        var method = analysis.Method;
        var invocations = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToArray();

        var canonicalRefresh = RequireInvocation(
            invocations,
            "RefreshAcceptedTurnCanonicalStateForValidationAsync");
        var criticalValidation = RequireInvocation(
            invocations,
            "ValidateCriticalCanonicalStateAsync");
        var fullStateValidation = RequireInvocation(
            invocations,
            "ValidateCurrentGameStateOrShowErrorsAsync");
        var cleanupInvocations = RequireInvocations(
            invocations,
            "CleanupAcceptedTurnCommandSurfacesAsync");
        var runtimeRefreshes = RequireInvocations(invocations, "RefreshRuntimeStateAsync");
        var finalRuntimeRefresh = runtimeRefreshes.MaxBy(static invocation => invocation.SpanStart)!;

        var compensationScope = RequireCanonicalRefreshTransactionScope(analysis);
        var completion = RequireDirectAwaitedTypedCompletion(
            analysis,
            compensationScope);
        var notificationAssignments = method.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(static assignment => assignment.Left.ToString().Contains(
                "_acceptedTurnWoundNotifications",
                StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(notificationAssignments);
        var notificationAssignment = notificationAssignments.MaxBy(
            static assignment => assignment.SpanStart)!;
        var acceptedReturn = Assert.Single(method.DescendantNodes()
            .OfType<ReturnStatementSyntax>(),
            static statement => string.Equals(
                statement.Expression?.ToString(),
                "AcceptedTurnValidationDisposition.Accepted",
                StringComparison.Ordinal));

        Assert.True(canonicalRefresh.SpanStart < compensationScope.Statement.SpanStart);
        var refreshTry = method.DescendantNodes()
            .OfType<TryStatementSyntax>()
            .Where(statement => statement.Block.Span.Contains(canonicalRefresh.Span))
            .MinBy(static statement => statement.Block.Span.Length);
        Assert.NotNull(refreshTry);
        var firstPostRefreshExit = method.DescendantNodes()
            .Where(static node => node is ContinueStatementSyntax or ReturnStatementSyntax)
            .Where(node => node.SpanStart > refreshTry!.Span.End)
            .MinBy(static node => node.SpanStart);
        Assert.NotNull(firstPostRefreshExit);
        Assert.True(
            compensationScope.Statement.SpanStart < firstPostRefreshExit!.SpanStart,
            "Every post-helper continue/non-accepted/exception path must already be owned by the compensation scope.");
        var compensationBlock = Assert.IsType<BlockSyntax>(
            compensationScope.Statement.Parent);
        var postCarrierExits = method.DescendantNodes()
            .Where(static node =>
                node is ContinueStatementSyntax or ThrowStatementSyntax ||
                node is ReturnStatementSyntax returnStatement &&
                !string.Equals(
                     returnStatement.Expression?.ToString(),
                     "AcceptedTurnValidationDisposition.Accepted",
                     StringComparison.Ordinal))
            .Where(node => node.SpanStart > compensationScope.Statement.SpanStart &&
                           node.SpanStart < completion.SpanStart)
            .ToArray();
        Assert.NotEmpty(postCarrierExits);
        Assert.All(postCarrierExits, exit =>
        {
            Assert.True(
                exit.SpanStart > compensationScope.Statement.SpanStart,
                $"Post-carrier exit '{exit}' occurs before the exact transaction scope.");
            Assert.True(
                compensationBlock.Span.Contains(exit.Span),
                $"Post-carrier exit '{exit}' is outside the exact transaction scope's lifetime.");
        });

        Assert.True(compensationScope.Statement.SpanStart < criticalValidation.SpanStart);
        Assert.True(criticalValidation.SpanStart < fullStateValidation.SpanStart);
        Assert.True(fullStateValidation.SpanStart < cleanupInvocations.Min(static value => value.SpanStart));
        Assert.True(cleanupInvocations.Max(static value => value.SpanStart) < finalRuntimeRefresh.SpanStart);
        Assert.True(finalRuntimeRefresh.SpanStart < completion.SpanStart);
        Assert.True(completion.SpanStart < notificationAssignment.SpanStart);
        Assert.True(notificationAssignment.SpanStart < acceptedReturn.SpanStart);
        RequireTypedCompletionSuccessDominatesAcceptedExit(
            analysis,
            completion,
            notificationAssignment,
            acceptedReturn);

        var postCompletionAwaits = method.DescendantNodes()
            .OfType<AwaitExpressionSyntax>()
            .Where(value => value.SpanStart > completion.SpanStart)
            .ToArray();
        var completionFailureBranch = Assert.Single(
            method.DescendantNodes().OfType<IfStatementSyntax>(),
            statement => statement.SpanStart > completion.SpanStart &&
                         postCompletionAwaits.Any(value =>
                             statement.Statement.Span.Contains(value.Span)));
        Assert.All(postCompletionAwaits, value => Assert.True(
            completionFailureBranch.Statement.Span.Contains(value.Span),
            $"Post-completion await '{value}' is reachable outside the typed failure branch."));
        var completionFailureInvocations = completionFailureBranch.Statement
            .DescendantNodesAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .Select(static invocation => invocation.Expression.ToString())
            .ToArray();
        Assert.Contains(
            completionFailureInvocations,
            static name => name.EndsWith(
                "SettleTreatmentResourcePublicationCompletionFailureAsync",
                StringComparison.Ordinal));
        Assert.Contains(
            completionFailureInvocations,
            static name => name.EndsWith(
                "MapTreatmentPublicationSettlementToDisposition",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(
        "BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs",
        "RefreshCanonicalStateAsync",
        "RefreshRuntimeStateAsync",
        false)]
    [InlineData(
        "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
        "RefreshAcceptedTurnCanonicalStateForValidationAsync",
        "ValidateAcceptedWoundPostPublicationAuthorityAsync",
        false)]
    [InlineData(
        "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
        "RefreshAcceptedTurnCanonicalStateForValidationAsync",
        "BindPublishedAcceptedWoundOutputAsync",
        false)]
    [InlineData(
        "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
        "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
        "ValidateCriticalCanonicalStateAsync",
        false)]
    [InlineData(
        "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
        "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
        "ValidateCurrentGameStateOrShowErrorsAsync",
        false)]
    [InlineData(
        "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
        "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
        "CleanupAcceptedTurnCommandSurfacesAsync",
        true)]
    [InlineData(
        "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
        "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
        "RefreshRuntimeStateAsync",
        true)]
    public void GuaranteedResourceQuantity_PostHelperFailureBoundaryRemainsInsideCompensationFence(
        string relativePath,
        string methodName,
        string boundaryInvocationName,
        bool useLastOccurrence)
    {
        var analysis = ReadMethodAnalysis(relativePath, methodName);
        _ = RequireControlFlowGraph(analysis);
        var carrierProperty = RequireOpenTreatmentTransactionCarrier();
        var method = analysis.Method;
        var invocations = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .ToArray();
        var boundaries = RequireInvocations(invocations, boundaryInvocationName);
        var boundary = useLastOccurrence
            ? boundaries.MaxBy(static value => value.SpanStart)!
            : boundaries.MinBy(static value => value.SpanStart)!;

        if (!string.Equals(
                methodName,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                StringComparison.Ordinal))
        {
            var upstreamInvocationName = string.Equals(
                methodName,
                "RefreshCanonicalStateAsync",
                StringComparison.Ordinal)
                ? "NormalizeAndValidateWithPlanAsync"
                : "RefreshCanonicalStateAsync";
            RequireExactCarrierCompensationForBoundary(
                analysis,
                boundary,
                upstreamInvocationName,
                carrierProperty.Name);
            return;
        }

        var scope = RequireCanonicalRefreshTransactionScope(analysis);
        var completion = RequireDirectAwaitedTypedCompletion(analysis, scope);
        Assert.True(scope.Statement.SpanStart < boundary.SpanStart);
        Assert.True(boundary.SpanStart < completion.SpanStart);
    }

    [Theory]
    [InlineData("runtime_refresh", 1)]
    [InlineData("wound_post_seal", 1)]
    [InlineData("wound_output", 1)]
    [InlineData("critical_validation", 1)]
    [InlineData("runtime_refresh", 2)]
    public Task GuaranteedResourceQuantity_RealGameEngineBoundaryFailureCompensatesExactHeldPlan(
        string boundary, int occurrence) => RunKnownTreatmentBoundaryAsync(boundary, occurrence);

    [Theory]
    [InlineData("full_state_validation", 1)]
    [InlineData("cleanup", 1)]
    public Task GuaranteedResourceQuantity_RemainingEngineBoundaryFailureRetainsOriginalContract(
        string boundary, int occurrence) => RunKnownTreatmentBoundaryAsync(boundary, occurrence);

    [Fact]
    public Task GuaranteedResourceQuantity_ForeignPlanRetainedWhileOriginalPublicationBlocked() =>
        RunKnownTreatmentBoundaryAsync("transaction_commit_conflict", 1);

    private async Task RunKnownTreatmentBoundaryAsync(string boundary, int occurrence)
    {
        var fault = new AcceptedTreatmentPipelineFault(boundary, occurrence);
        await using var context = await CreateHeldTreatmentPipelineContextAsync(fault);
        await context.ReleaseLeaseAsync();
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Escape) }),
            fileSystem: context.FileSystem);
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var snapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);
        var before = await CaptureExactTreatmentTransactionBytesAsync(context);
        var commandBefore = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(
            AcceptedMechanicsPlan.WoundCommandPath));

        fault.Arm(context);
        AcceptedTurnValidationDisposition? accepted = null;
        var exception = await Record.ExceptionAsync(async () =>
        {
            accepted = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "held treatment integration oracle",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);
        });

        WriteTreatmentNeighborDiagnostic(context, fault, boundary, exception, accepted, commandBefore);
        Assert.True(
            fault.Fired,
            $"The existing filesystem hook did not reach '{boundary}' occurrence {occurrence}. " +
            $"Observed phases: {string.Join(", ", fault.ObservedPhases)}");
        Assert.True(
            exception is not null || accepted !=
                AcceptedTurnValidationDisposition.Accepted,
            "A pre-finalization failure must not return an accepted turn. " +
            $"Disposition={accepted?.ToString() ?? "null"}; " +
            $"exception={exception?.GetType().Name ?? "none"}.");
        if (string.Equals(
                boundary,
                "transaction_commit_conflict",
                StringComparison.Ordinal))
        {
            Assert.Null(exception);
            Assert.Equal(
                AcceptedTurnValidationDisposition.RetryablePublicationHeldBlocked,
                accepted);
        }
        else if (boundary is "critical_validation" or
                 "full_state_validation" or
                 "cleanup" ||
                 string.Equals(
                     boundary,
                     "runtime_refresh",
                     StringComparison.Ordinal) && occurrence == 2)
        {
            Assert.IsType<IOException>(exception);
            Assert.Null(accepted);
        }
        else
        {
            Assert.Null(exception);
            Assert.Equal(
                AcceptedTurnValidationDisposition.RetryablePublicationRearmed,
                accepted);
        }
        if (boundary == "full_state_validation")
        {
            Assert.Equal(3, fault.MatchedHealthRefreshes);
            Assert.Equal(3, fault.HealthVisits.Count);
            Assert.True(fault.PublishedTreatmentMembers > 0);
            Assert.NotNull(fault.OriginalReceipt);
        }
        await AssertExactTreatmentTransactionBytesAsync(context, before);
        Assert.Equal(
            commandBefore,
            await context.FileSystem.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));

        await context.AcquireLeaseAsync();
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        if (string.Equals(
                boundary,
                "transaction_commit_conflict",
                StringComparison.Ordinal))
        {
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(context.FileSystem, context.Lease,
                out var retainedForeignBinding, out var retainedForeign));
            Assert.True(retainedForeign.Success);
            Assert.Same(fault.ForeignPlan, retainedForeign.Plan);
            Assert.Equal(fault.ForeignBindingFingerprint, AcceptedMechanicsPlanFingerprints.ComputeInput(retainedForeignBinding));
            AssertTreatmentPublicationRestartBlocked(context);
        }
        else
        {
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
                context.FileSystem,
                context.Lease,
                out var rearmedBinding,
                out var rearmed));
            Assert.True(rearmed.Success, DescribeValidationIssues(rearmed.Issues));
            Assert.Same(context.Plan, rearmed.Plan);
            Assert.Equal(
                AcceptedMechanicsPlanFingerprints.ComputeInput(context.OriginalBinding),
                AcceptedMechanicsPlanFingerprints.ComputeInput(rearmedBinding));
        }
        AssertConfirmedHeldBlocksCompetingTreatment(
            context,
            string.Equals(
                boundary,
                "transaction_commit_conflict",
                StringComparison.Ordinal)
                ? "mortal_wound_treatment_resource_reservation_authority_invalid"
                : "mortal_wound_treatment_resource_reservation_overbooked");
    }

    [Fact]
    public async Task GuaranteedResourceQuantity_PostPublicationCompensationFailureSurfacesUnsafeSettlementWithoutTerminalDisposition()
    {
        var fault = new AcceptedTreatmentPipelineFault(
            "wound_output_compensation_restore_failure",
            1);
        await using var context = await CreateHeldTreatmentPipelineContextAsync(fault);
        await context.ReleaseLeaseAsync();
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Escape) }),
            fileSystem: context.FileSystem);
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var snapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);
        var commandBefore = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));

        var before = await CaptureExactTreatmentTransactionBytesAsync(context);
        fault.Arm(context);
        AcceptedTurnValidationDisposition? disposition = null;
        var exception = await Record.ExceptionAsync(async () =>
        {
            disposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
                "unsafe held-treatment settlement oracle",
                snapshotContext,
                null,
                HeldTreatmentPipelineContext.Turn,
                null);
        });

        WriteTreatmentNeighborDiagnostic(context, fault, "unsafe", exception, disposition, commandBefore);
        Assert.True(
            fault.Fired,
            "The post-publication failure and its compensation-restoration failure must both fire.");
        var aggregate = Assert.IsAssignableFrom<AggregateException>(exception);
        Assert.Equal(
            UnsafeTreatmentPublicationSettlementExceptionName,
            aggregate.GetType().Name);
        Assert.Null(disposition);
        Assert.NotNull(commandBefore);
        Assert.Null(await context.FileSystem.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
        await AssertExactTreatmentTransactionBytesAsync(context,
            before.Where(pair => pair.Key != AcceptedMechanicsPlan.WoundCommandPath)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        Assert.Contains("wound_output", aggregate.ToString(), StringComparison.Ordinal);
        Assert.Contains("restoration failure", aggregate.ToString(), StringComparison.Ordinal);

        await context.AcquireLeaseAsync();
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        Assert.True(AcceptedTurnAuthorityRegistry.HasExactMortalWoundTreatmentPublicationRestartBlocker(
            context.FileSystem, context.Lease, Assert.IsType<MortalWoundTreatmentPublicationTakeReceipt>(fault.OriginalReceipt)));
    }

    [Fact]
    public async Task GuaranteedResourceQuantity_PreCanonicalTerminalRestorationFailureSurfacesAggregateInsteadOfHeldBlockedDisposition()
    {
        var fault = new AcceptedTreatmentPipelineFault(
            "pre_canonical_terminal_quarantine_restore_failure",
            1);
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault,
            withRollbackAuthority: true);
        var pendingRequest = new JsonObject
        {
            ["operationKey"] = context.Request.Coordinates.OperationKey,
            ["attemptId"] = context.Request.Coordinates.AttemptId,
            ["requestFingerprint"] = context.Request.RequestFingerprint,
            ["request"] = new JsonObject
            {
                ["coordinates"] = new JsonObject
                {
                    ["operationKey"] = context.Request.Coordinates.OperationKey,
                    ["attemptId"] = context.Request.Coordinates.AttemptId
                },
                ["requestFingerprint"] = context.Request.RequestFingerprint
            }
        };
        await context.FileSystem.WriteFileAtomicAsync(
            context.Lease,
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
            new JsonObject
            {
                ["submittedTreatmentRequests"] = new JsonArray(pendingRequest)
            }.ToJsonString());
        await context.ReleaseLeaseAsync();
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Escape) }),
            fileSystem: context.FileSystem);
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "GetValidatedRollbackSnapshotAsync",
            manifest);
        var commandBefore = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));

        WriteTreatmentNeighborDiagnostic(context, fault, "terminal_restore_precondition", null, null, commandBefore, rollbackSnapshot);
        Assert.NotNull(rollbackSnapshot);
        Assert.True(ReadTreatmentRollbackCount(rollbackSnapshot, "BackupFiles") > 0 ||
                    ReadTreatmentRollbackCount(rollbackSnapshot, "BaselineFiles") > 0);
        fault.Arm(context);
        AcceptedTurnValidationDisposition? disposition = null;
        var exception = await Record.ExceptionAsync(async () =>
        {
            disposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "SettlePreCanonicalTreatmentPublicationBeforeTerminalRollbackAsync",
                rollbackSnapshot,
                "validation_failed");
        });

        WriteTreatmentNeighborDiagnostic(context, fault, "terminal_restore", exception, disposition, commandBefore);
        Assert.True(
            fault.Fired,
            "The command quarantine, pending quarantine failure, and command restoration failure must all be observed. " +
            $"Observed phases: {string.Join(", ", fault.ObservedPhases)}.");
        Assert.IsAssignableFrom<AggregateException>(exception);
        Assert.Null(disposition);
        Assert.NotEqual(
            commandBefore,
            await context.FileSystem.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));

        await context.AcquireLeaseAsync();
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        Assert.True(AcceptedTurnAuthorityRegistry.HasExactMortalWoundTreatmentPublicationRestartBlocker(
            context.FileSystem, context.Lease, Assert.IsType<MortalWoundTreatmentPublicationTakeReceipt>(fault.OriginalReceipt)));
    }

    [Fact]
    public async Task GuaranteedResourceQuantity_PreCanonicalTerminalCleanupFailureMapsHeldBlockedOnlyAfterExactSafetyProof()
    {
        var fault = new AcceptedTreatmentPipelineFault(
            "pre_canonical_terminal_quarantine_safe_failure",
            1);
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault,
            withRollbackAuthority: true);
        var commandBefore = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));
        await context.ReleaseLeaseAsync();
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Escape) }),
            fileSystem: context.FileSystem);
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "GetValidatedRollbackSnapshotAsync",
            manifest);

        WriteTreatmentNeighborDiagnostic(context, fault, "terminal_safe_precondition", null, null, commandBefore, rollbackSnapshot);
        Assert.NotNull(rollbackSnapshot);
        Assert.True(ReadTreatmentRollbackCount(rollbackSnapshot, "BackupFiles") > 0 ||
                    ReadTreatmentRollbackCount(rollbackSnapshot, "BaselineFiles") > 0);
        fault.Arm(context);
        var disposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            engine,
            "SettlePreCanonicalTreatmentPublicationBeforeTerminalRollbackAsync",
            rollbackSnapshot,
            "validation_failed");

        WriteTreatmentNeighborDiagnostic(context, fault, "terminal_safe", null, disposition, commandBefore);
        Assert.True(fault.Fired);
        Assert.Equal(
            AcceptedTurnValidationDisposition.RetryablePublicationHeldBlocked,
            disposition);
        Assert.Equal(
            commandBefore,
            await context.FileSystem.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));
        await context.AcquireLeaseAsync();
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        Assert.True(AcceptedTurnAuthorityRegistry.HasExactMortalWoundTreatmentPublicationRestartBlocker(
            context.FileSystem, context.Lease, Assert.IsType<MortalWoundTreatmentPublicationTakeReceipt>(fault.OriginalReceipt)));
    }

    [Theory]
    [InlineData(
        "pre_canonical_terminal_quarantine_cancellation",
        nameof(OperationCanceledException))]
    [InlineData(
        "pre_canonical_terminal_quarantine_session_replaced",
        nameof(SessionReplacedException))]
    public async Task GuaranteedResourceQuantity_PreCanonicalTerminalControlFlowExceptionsPropagateAfterSafeBlocking(
        string boundary,
        string expectedExceptionName)
    {
        var fault = new AcceptedTreatmentPipelineFault(boundary, 1);
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault,
            withRollbackAuthority: true);
        var commandBefore = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));
        await context.ReleaseLeaseAsync();
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Escape) }),
            fileSystem: context.FileSystem);
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "GetValidatedRollbackSnapshotAsync",
            manifest);

        fault.Arm(context);
        AcceptedTurnValidationDisposition? disposition = null;
        var exception = await Record.ExceptionAsync(async () =>
        {
            disposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
                engine,
                "SettlePreCanonicalTreatmentPublicationBeforeTerminalRollbackAsync",
                rollbackSnapshot,
                "validation_failed");
        });

        Assert.True(fault.Fired);
        Assert.NotNull(exception);
        Assert.Equal(expectedExceptionName, exception.GetType().Name);
        Assert.Null(disposition);
        Assert.Equal(
            commandBefore,
            await context.FileSystem.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));
        await context.AcquireLeaseAsync();
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        AssertTreatmentPublicationRestartBlocked(context);
    }

    [Fact]
    public void GuaranteedResourceQuantity_UnsafeSettlementCannotFallThroughTerminalCatchAndPreCanonicalSettlementHasNoCatchAll()
    {
        const string relativePath =
            "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs";
        var validation = ReadMethodAnalysis(
            relativePath,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync");
        var refreshTry = Assert.Single(
            validation.Method.DescendantNodes().OfType<TryStatementSyntax>(),
            statement => statement.Block.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation => string.Equals(
                    ReadInvocationName(invocation),
                    "RefreshAcceptedTurnCanonicalStateForValidationAsync",
                    StringComparison.Ordinal)));
        var unsafeCatch = Assert.Single(
            refreshTry.Catches,
            clause => string.Equals(
                clause.Declaration?.Type.ToString(),
                UnsafeTreatmentPublicationSettlementExceptionName,
                StringComparison.Ordinal));
        var catchAll = Assert.Single(
            refreshTry.Catches,
            clause => IsUnfilteredCatchAll(validation, clause));
        Assert.True(unsafeCatch.SpanStart < catchAll.SpanStart);
        Assert.Collection(
            unsafeCatch.Block.Statements,
            statement => Assert.IsType<ThrowStatementSyntax>(statement));
        Assert.Null(
            Assert.IsType<ThrowStatementSyntax>(unsafeCatch.Block.Statements[0])
                .Expression);

        var preCanonicalSettlement = ReadMethodAnalysis(
            relativePath,
            "SettlePreCanonicalTreatmentPublicationBeforeTerminalRollbackAsync");
        Assert.Empty(
            preCanonicalSettlement.Method.DescendantNodes()
                .OfType<CatchClauseSyntax>());
    }

    [Fact]
    public async Task GuaranteedResourceQuantity_WaitLifecyclePreservesRearmedPublicationForSameTurnRetry()
    {
        var fault = new AcceptedTreatmentPipelineFault("runtime_refresh", 1);
        await using var context = await CreateHeldTreatmentPipelineContextAsync(fault);
        await context.ReleaseLeaseAsync();
        var commandBefore = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));
        var inputBefore = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(
                "input/turn_request.json"));
        var snapshotBefore = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
        var outputBefore = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(
                "output/narrative_response.json"));
        await context.FileSystem.WriteFileAtomicAsync(
            "ready/turn_complete.json",
            new JsonObject
            {
                ["sessionId"] = "session_t070b3_game_engine",
                ["requestId"] = "request_t070b3_game_engine",
                ["turnNumber"] = HeldTreatmentPipelineContext.Turn,
                ["timestamp"] = "2026-09-02T10:00:00Z",
                ["status"] = "success",
                ["filesModified"] = new JsonArray(
                    "output/narrative_response.json",
                    AcceptedMechanicsPlan.WoundCommandPath)
            }.ToJsonString());
        fault.Arm(context);
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource([]),
            fileSystem: context.FileSystem);

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForGmResponse");

        Assert.False(accepted);
        Assert.True(fault.Fired);
        Assert.False(context.FileSystem.FileExists("ready/turn_complete.json"));
        Assert.Equal(
            commandBefore,
            await context.FileSystem.ReadFileBytesAsync(
                AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Equal(
            inputBefore,
            await context.FileSystem.ReadFileBytesAsync(
                "input/turn_request.json"));
        Assert.Equal(
            snapshotBefore,
            await context.FileSystem.ReadFileBytesAsync(
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
        Assert.Equal(
            outputBefore,
            await context.FileSystem.ReadFileBytesAsync(
                "output/narrative_response.json"));
        await context.AcquireLeaseAsync();
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out _,
            out var rearmed));
        Assert.True(rearmed.Success, DescribeValidationIssues(rearmed.Issues));
        Assert.Same(context.Plan, rearmed.Plan);
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
    }

    [Fact]
    public async Task GuaranteedResourceQuantity_PreTokenRawValidationTerminalRollbackQuarantinesExactHeldRequestAndReleasesHold()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            withRollbackAuthority: true);
        await context.ReleaseLeaseAsync();
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Escape) }),
            fileSystem: context.FileSystem);
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var snapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "GetValidatedRollbackSnapshotAsync",
            manifest);
        var rollbackAuthorityCollections =
            new[] { "BackupFiles", "BaselineFiles" }.Where(name =>
            {
                var value = rollbackSnapshot.GetType().GetProperty(name)?.GetValue(
                    rollbackSnapshot);
                return value is System.Collections.IEnumerable entries &&
                       entries.Cast<object>().Any();
            }).ToArray();
        Assert.NotEmpty(rollbackAuthorityCollections);
        var resourceStateBefore = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.StatePath));
        var resourceHistoryBefore = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.HistoryPath));

        const string npcCorePath = "game_state/npcs/npc_core.json";
        var npcBefore = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(npcCorePath));
        var npcRoot = ParseJsonObjectBytes(npcBefore);
        var provider = Assert.IsType<JsonObject>(Assert.Single(
            Assert.IsType<JsonArray>(npcRoot["NPCsInScene"])));
        var originalWorldview = provider["worldview"]?.GetValue<string>();
        provider["worldview"] =
            "This rejected direct mutation must survive until the outer rollback.";
        await context.FileSystem.WriteFileAtomicAsync(
            npcCorePath,
            npcRoot.ToJsonString());
        var rejectedNpcBytes = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(npcCorePath));
        Assert.NotEqual(npcBefore, rejectedNpcBytes);

        var rawIssuesObject = await InvokePrivateTaskResultAsync(
            engine,
            "CollectAcceptedTurnRawStateIssuesAsync");
        var rawIssues = Assert.IsAssignableFrom<IEnumerable<ValidationIssue>>(
            rawIssuesObject);
        Assert.Contains(rawIssues, static issue => string.Equals(
            issue.Code,
            "npc_existing_core_direct_mutation_forbidden",
            StringComparison.Ordinal));
        await context.AcquireLeaseAsync();
        Assert.True(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            context.Lease));
        Assert.False(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
            context.FileSystem,
            context.Lease,
            context.OriginalBinding.SessionId,
            context.OriginalBinding.SnapshotToken,
            context.OriginalBinding.Turn,
            out _));
        await AssertConfirmedHeldLiveRegistryProbeAsync(context);
        await context.ReleaseLeaseAsync();

        var disposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "pre-token terminal raw-validation oracle",
            snapshotContext,
            rollbackSnapshot,
            HeldTreatmentPipelineContext.Turn,
            new ProgressionControl { CurrentRealm = "Mortal World" });

        Assert.Equal(
            AcceptedTurnValidationDisposition.TerminalRejected,
            disposition);
        Assert.Equal(
            rejectedNpcBytes,
            await context.FileSystem.ReadFileBytesAsync(npcCorePath));
        Assert.Equal(
            resourceStateBefore,
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.StatePath));
        Assert.Equal(
            resourceHistoryBefore,
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.HistoryPath));
        Assert.False(await ContainsCurrentExactTreatmentRequestAsync(context));

        await context.AcquireLeaseAsync();
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            context.Lease));
        var releasedProbe = await ProbeLiveRegistryAsync(context);
        Assert.False(Assert.IsType<bool>(ReadRequiredProbeProperty(
            releasedProbe,
            "IsValid")));
        var releasedStateProperty = releasedProbe.GetType().GetProperty(
            "State",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(releasedStateProperty);
        Assert.NotEqual("ConfirmedHeld", Convert.ToString(
            releasedStateProperty.GetValue(releasedProbe)));
        await context.ReleaseLeaseAsync();

        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            "[yellow]rollback oracle[/]");

        var rolledBackNpc = ParseJsonObjectBytes(Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(npcCorePath)));
        var rolledBackProvider = Assert.IsType<JsonObject>(Assert.Single(
            Assert.IsType<JsonArray>(rolledBackNpc["NPCsInScene"])));
        Assert.Equal(
            originalWorldview,
            rolledBackProvider["worldview"]?.GetValue<string>());
        Assert.False(await ContainsCurrentExactTreatmentRequestAsync(context));
        Assert.Equal(
            resourceStateBefore,
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.StatePath));
        Assert.Equal(
            resourceHistoryBefore,
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.HistoryPath));

        await context.RestartAsync(hooks: null);
        await context.ReleaseLeaseAsync();
        _ = await new LiveTurnPreparationService(context.FileSystem)
            .PrepareAsync(new LiveTurnPreparationOptions
            {
                SessionId = "session_t070b3_after_terminal_release",
                RequestId = "request_t070b3_after_terminal_release",
                TurnNumber = HeldTreatmentPipelineContext.Turn + 1,
                PlayerAction =
                    "Prepare a fresh treatment after the rejected held request was released.",
                CurrentRealm = "Mortal World",
                PreGeneratedDices1d20 = new[] { 16, 5, 2, 19 }
            });
        await context.AcquireLeaseAsync();
        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var recovered = acceptedState.RestorePersistedTreatmentRequests(history);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        Assert.Empty(recovered.HeldRequests);
        Assert.Empty(recovered.FinalizedRequests);
        var competing = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(
            acceptedState,
            history,
            ReadCurrentTreatmentWound(context.FileSystem),
            HeldTreatmentPipelineContext.OperationKey + "_after_release",
            HeldTreatmentPipelineContext.RouteId,
            Assert.Single(acceptedState.Binding.AcceptedEvents).EventRef);
        Assert.True(competing.IsValid, DescribeValidationIssues(competing.Issues));
        Assert.NotNull(competing.Request);
    }

    [Fact]
    public async Task GuaranteedResourceQuantity_AuthorizedProgressionMutationJoinsFinalPublicationAgreement()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(fault: null);
        await context.ReleaseLeaseAsync();
        var scheduleBefore = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(
                ProgressionScheduleService.SchedulePath));
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Escape) }),
            fileSystem: context.FileSystem);
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var snapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);

        var accepted = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "held treatment progression agreement oracle",
            snapshotContext,
            null,
            HeldTreatmentPipelineContext.Turn,
            new ProgressionControl { CurrentRealm = "Mortal World" });

        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, accepted);
        var scheduleAfter = Assert.IsType<byte[]>(
            await context.FileSystem.ReadFileBytesAsync(
                ProgressionScheduleService.SchedulePath));
        Assert.NotEqual(scheduleBefore, scheduleAfter);
        await context.AcquireLeaseAsync();
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            context.Lease));
    }

    [Fact]
    public async Task GuaranteedResourceQuantity_ColdConfirmedCommandRealPipelineCommitThenRestartReplaySpendsOnce()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(fault: null);
        await context.RestartAsync(hooks: null);
        await RestoreHeldTreatmentAndComposeSameSemanticPlanAsync(context);
        var plannedResourceEvent = Assert.Single(
            context.Plan.ResourceEvents,
            static resourceEvent =>
                string.Equals(
                    resourceEvent.EventKind,
                    "resource_spent",
                    StringComparison.Ordinal) &&
                string.Equals(
                    resourceEvent.Coordinate.ResourceKey,
                    "energy",
                    StringComparison.Ordinal));
        Assert.Equal("energy", plannedResourceEvent.Coordinate.ResourceKey);
        var historyBefore = await ReadTreatmentResourceHistoryAsync(context.FileSystem);
        var spendCountBefore = CountHeldTreatmentEnergySpends(historyBefore);
        var plannedDefinitions = ResourceDefinitionCatalog.ParseCanonical(
            context.Plan.DefinitionAfterImage.ToJsonString(),
            allowMissingPristine: false);
        Assert.True(
            plannedDefinitions.IsValid,
            DescribeValidationIssues(plannedDefinitions.Issues));
        var plannedHistory = ResourceHistoryState.ParseCanonical(
            context.Plan.HistoryAfterImage.ToJsonString(),
            Assert.IsType<ResourceDefinitionCatalog>(plannedDefinitions.Catalog),
            allowMissingPristine: false);
        Assert.True(
            plannedHistory.IsValid,
            DescribeValidationIssues(plannedHistory.Issues));
        Assert.Equal(
            spendCountBefore + 1,
            CountHeldTreatmentEnergySpends(
                Assert.IsType<ResourceHistoryState>(plannedHistory.History)));
        var resourceHistoryBeforeImage = Assert.IsType<byte[]>(
            context.Plan.BeforeImages[ResourceMaterializationContract.HistoryPath]
                .Bytes);
        Assert.False(JsonNode.DeepEquals(
            JsonNode.Parse(
                Encoding.UTF8.GetString(resourceHistoryBeforeImage)
                    .TrimStart('\uFEFF')),
            context.Plan.HistoryAfterImage));

        await context.ReleaseLeaseAsync();
        var rawValidationIssues = await new ValidationService(
                context.FileSystem,
                NullLogger<ValidationService>.Instance)
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(
            rawValidationIssues,
            static issue => issue.Severity == IssueSeverity.Error);
        await context.AcquireLeaseAsync();
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out _,
            out var rawValidated));
        Assert.True(
            rawValidated.Success,
            DescribeValidationIssues(rawValidated.Issues));
        Assert.Same(
            context.Plan,
            rawValidated.Plan);
        var treatmentOutput = GameEngine.BindAcceptedWoundOutput(
            context.Plan,
            HeldTreatmentPipelineContext.FinalSceneText);
        Assert.True(
            treatmentOutput.Success,
            DescribeValidationIssues(treatmentOutput.Issues));
        Assert.Empty(treatmentOutput.Notifications);
        await context.ReleaseLeaseAsync();
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Escape) }),
            fileSystem: context.FileSystem);
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var snapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);

        var accepted = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            engine,
            "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "cold held treatment integration oracle",
            snapshotContext,
            null,
            HeldTreatmentPipelineContext.Turn,
            null);

        await context.AcquireLeaseAsync();
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            context.Lease));
        await context.ReleaseLeaseAsync();
        Assert.Equal(AcceptedTurnValidationDisposition.Accepted, accepted);
        var stateAfterCommit = await context.FileSystem.ReadFileBytesAsync(
            ResourceMaterializationContract.StatePath);
        var historyAfterCommit = await context.FileSystem.ReadFileBytesAsync(
            ResourceMaterializationContract.HistoryPath);
        Assert.NotNull(stateAfterCommit);
        Assert.NotNull(historyAfterCommit);
        var publishedResourceHistory =
            await ReadTreatmentResourceHistoryAsync(context.FileSystem);
        var spendCountAfter = CountHeldTreatmentEnergySpends(
            publishedResourceHistory);
        Assert.True(
            spendCountAfter == spendCountBefore + 1,
            "Expected one accepted treatment energy spend. Published transitions: " +
            string.Join(
                " | ",
                publishedResourceHistory.Transitions.Select(static transition =>
                    $"{transition.EventRef}:{transition.Phase}:" +
                    $"{transition.Operation}:{transition.Coordinate.ResourceKey}:" +
                    $"{transition.AppliedAmount}")));

        var replaySnapshot = await new LiveTurnPreparationService(
                context.FileSystem)
            .PrepareAsync(new LiveTurnPreparationOptions
            {
                SessionId = "session_t070b3_game_engine",
                RequestId = "request_t070b3_game_engine_replay",
                TurnNumber = HeldTreatmentPipelineContext.Turn,
                PlayerAction =
                    "Verify the finalized treatment remains an exact replay.",
                CurrentRealm = "Mortal World",
                PreGeneratedDices1d20 = new[] { 17, 4, 1, 20 }
            });
        Assert.Equal(
            HeldTreatmentPipelineContext.Turn,
            replaySnapshot.TurnNumber);
        await context.RestartAsync(hooks: null);
        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var recovered = acceptedState.RestorePersistedTreatmentRequests(history);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        Assert.Empty(recovered.HeldRequests);
        var finalized = Assert.Single(recovered.FinalizedRequests);
        var wound = ReadCurrentTreatmentWound(context.FileSystem);
        var replay = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(
            finalized,
            history,
            wound,
            acceptedState);

        Assert.Equal("ExactReplay", replay.Disposition);
        Assert.NotNull(replay.ReplayReceipt);
        Assert.Equal(
            stateAfterCommit,
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.StatePath));
        Assert.Equal(
            historyAfterCommit,
            await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.HistoryPath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            context.FileSystem,
            context.Lease));
    }

    private static void RequireExactCarrierCompensationForBoundary(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax boundary,
        string upstreamInvocationName,
        string carrierPropertyName)
    {
        var method = analysis.Method;
        var upstream = Assert.Single(
            method.DescendantNodes().OfType<InvocationExpressionSyntax>(),
            invocation => string.Equals(
                ReadInvocationName(invocation),
                upstreamInvocationName,
                StringComparison.Ordinal));
        var resultSymbol = RequireAssignedLocalSymbol(analysis, upstream);
        var resultStatement = upstream.Ancestors()
            .OfType<StatementSyntax>()
            .First();
        var carrierAliases = BuildExactCarrierAliases(
            analysis,
            method,
            resultSymbol,
            carrierPropertyName);
        var exactCompensationMethodName = RequireExactCarrierCompensationMethod().Name;
        RequireCompensatedTreatmentPublicationBuilder(exactCompensationMethodName);
        var controlFlow = RequireControlFlowGraph(analysis);
        var dominators = ComputeDominators(controlFlow);

        var owningTry = method.DescendantNodes()
            .OfType<TryStatementSyntax>()
            .Where(statement => statement.Block.Span.Contains(boundary.Span))
            .MinBy(static statement => statement.Block.Span.Length);
        Assert.NotNull(owningTry);

        Assert.True(
            owningTry!.Catches.Count > 0 || owningTry.Finally is not null,
            $"Failure boundary '{boundary}' after the helper returned its carrier " +
            "must be enclosed by an explicit compensation catch/finally.");
        foreach (var clause in owningTry.Catches)
        {
            Assert.False(
                clause.Filter?.DescendantNodesAndSelf().Any(static node =>
                    node is InvocationExpressionSyntax or AwaitExpressionSyntax or
                        ObjectCreationExpressionSyntax) == true,
                   "A post-carrier catch filter cannot run fallible work before exact " +
                   "carrier compensation owns the failure path.");
            if (IsPureCompensatedTreatmentPublicationPassThrough(analysis, clause))
                continue;
            RequireUnconditionalExactCarrierCompensation(
                analysis,
                clause.Block,
                resultSymbol,
                carrierPropertyName,
                carrierAliases,
                exactCompensationMethodName,
                $"catch {clause.Declaration?.Type}");
        }
        if (owningTry.Finally is { } finallyClause)
        {
            RequireUnconditionalExactCarrierCompensation(
                analysis,
                finallyClause.Block,
                resultSymbol,
                carrierPropertyName,
                carrierAliases,
                exactCompensationMethodName,
                "finally");
        }

        var guardedCarrierRegion = owningTry.Ancestors()
            .OfType<IfStatementSyntax>()
            .FirstOrDefault(statement => statement.Statement.Span.Contains(owningTry.Span))
            ?.Statement ?? owningTry;
        var postCarrierExits = guardedCarrierRegion.DescendantNodesAndSelf()
            .Where(static node => node is ReturnStatementSyntax or
                                  ThrowStatementSyntax or
                                  ContinueStatementSyntax)
            .Where(node => node.SpanStart > resultStatement.Span.End)
            .Where(node => ExitCanObserveReturnedCarrier(node, resultStatement))
            .ToArray();
        Assert.True(
            postCarrierExits.Length > 0,
            "The helper oracle must inspect each ownership-transfer or compensation exit.");
        foreach (var exit in postCarrierExits)
        {
            if (exit is ThrowStatementSyntax &&
                exit.Ancestors().OfType<CatchClauseSyntax>().Any(clause =>
                    IsPureCompensatedTreatmentPublicationPassThrough(
                        analysis,
                        clause)))
            {
                // This carrier can only be created after exact compensation. A
                // side-effect-free rethrow preserves that already-settled ownership.
                continue;
            }
            if (exit is ThrowStatementSyntax &&
                owningTry.Block.Span.Contains(exit.Span) &&
                (owningTry.Finally is not null ||
                 owningTry.Catches.Any(clause =>
                     IsUnfilteredCatchAll(analysis, clause))))
            {
                // The enclosing catch/finally was proven above to compensate the
                // exact returned carrier before doing any other fallible work.
                continue;
            }
            if (exit is ReturnStatementSyntax returnStatement &&
                ReturnTransfersExactCarrier(
                    analysis,
                    returnStatement,
                    resultSymbol,
                    carrierPropertyName,
                    carrierAliases))
            {
                continue;
            }
            Assert.True(
                IsExitDominatedByExactCarrierCompensation(
                    analysis,
                    controlFlow,
                    dominators,
                    exit,
                    resultSymbol,
                    carrierPropertyName,
                    carrierAliases,
                    exactCompensationMethodName),
                $"Post-carrier exit '{exit}' in {method.Identifier.ValueText} must " +
                "either transfer the exact carrier or be dominated by unconditional " +
                "compensation of that exact carrier.");
        }
    }

    private static IReadOnlySet<ILocalSymbol> BuildExactCarrierAliases(
        SourceMethodAnalysis analysis,
        SyntaxNode scope,
        ILocalSymbol resultSymbol,
        string carrierPropertyName)
    {
        var aliases = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
        bool changed;
        do
        {
            changed = false;
            foreach (var variable in scope.DescendantNodes()
                         .OfType<VariableDeclaratorSyntax>()
                         .Where(static variable => variable.Initializer is not null))
            {
                var symbol = analysis.Model.GetDeclaredSymbol(variable) as ILocalSymbol;
                if (symbol is null || aliases.Contains(symbol) ||
                    !ExpressionIsExactCarrierIdentity(
                        analysis,
                        variable.Initializer!.Value,
                        resultSymbol,
                        carrierPropertyName,
                        aliases) ||
                    !IsImmutableLocal(analysis, scope, symbol, variable))
                {
                    continue;
                }
                aliases.Add(symbol);
                changed = true;
            }
        } while (changed);
        return aliases;
    }

    private static void RequireUnconditionalExactCarrierCompensation(
        SourceMethodAnalysis analysis,
        BlockSyntax block,
        ILocalSymbol resultSymbol,
        string carrierPropertyName,
        IReadOnlySet<ILocalSymbol> aliases,
        string exactCompensationMethodName,
        string label)
    {
        var lifecycle = block.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => IsExactCarrierCompensationLifecycleName(
                ReadInvocationName(invocation),
                exactCompensationMethodName))
            .ToArray();
        Assert.True(
            lifecycle.Length > 0,
            $"Every {label} handling a post-carrier failure needs exact-carrier compensation.");
        Assert.All(lifecycle, invocation => Assert.True(
            IsResolvedInstanceInvocation(analysis, invocation),
            $"Lifecycle call '{invocation}' in {label} must resolve to an instance " +
            "method on the exact carrier flow, not a similarly named helper."));
        Assert.All(lifecycle, invocation => Assert.True(
            InvocationUsesExactCarrierDataFlow(
                analysis,
                invocation,
                resultSymbol,
                carrierPropertyName,
                aliases),
            $"Lifecycle call '{invocation}' in {label} targets an unrelated authority."));
        Assert.All(lifecycle, invocation => Assert.True(
            IsDirectlyAwaited(invocation),
            $"Exact-carrier lifecycle call '{invocation}' in {label} must be directly " +
            "awaited so compensation cannot be lost or reordered."));
        var unconditional = lifecycle
            .Where(invocation => invocation.Ancestors()
                .OfType<StatementSyntax>()
                .FirstOrDefault(statement => ReferenceEquals(statement.Parent, block)) is
                    ExpressionStatementSyntax or LocalDeclarationStatementSyntax or
                    ThrowStatementSyntax)
            .ToArray();
        Assert.True(
            unconditional.Length > 0,
            $"Exact-carrier compensation in {label} must be unconditional, not nested " +
            "behind a branch that can bypass it.");
        var firstCompensationIndex = unconditional.Min(invocation =>
            TopLevelStatementIndex(block, invocation));
        Assert.All(
            block.Statements.Take(firstCompensationIndex),
            statement => Assert.False(
                IsPotentiallyFallibleOrExitingStatement(statement),
                $"Exact-carrier compensation in {label} must precede every fallible " +
                $"top-level statement or exit, but found '{statement}'."));
        foreach (var exit in block.DescendantNodes().Where(static node =>
                     node is ReturnStatementSyntax or ThrowStatementSyntax or
                         ContinueStatementSyntax))
        {
            Assert.True(
                unconditional.Any(invocation =>
                {
                    var invocationIndex = TopLevelStatementIndex(block, invocation);
                    var exitIndex = TopLevelStatementIndex(block, exit);
                    return invocationIndex < exitIndex ||
                           invocationIndex == exitIndex &&
                           exit is ThrowStatementSyntax throwStatement &&
                           throwStatement.Expression?.Span.Contains(invocation.Span) == true;
                }),
                $"Exact-carrier compensation must dominate exit '{exit}' in {label}.");
        }
    }

    private static bool IsPureCompensatedTreatmentPublicationPassThrough(
        SourceMethodAnalysis analysis,
        CatchClauseSyntax clause)
    {
        if (clause.Filter is not null || clause.Declaration?.Type is not { } type)
            return false;
        var caughtType = analysis.Model.GetTypeInfo(type).Type;
        return string.Equals(
                   caughtType?.Name,
                   CompensatedTreatmentPublicationExceptionName,
                   StringComparison.Ordinal) &&
               clause.Block.Statements is
               [ThrowStatementSyntax { Expression: null }];
    }

    private static bool IsUnfilteredCatchAll(
        SourceMethodAnalysis analysis,
        CatchClauseSyntax clause)
    {
        if (clause.Filter is not null)
            return false;
        if (clause.Declaration is null)
            return true;
        return string.Equals(
            analysis.Model.GetTypeInfo(clause.Declaration.Type).Type?.ToDisplayString(),
            "System.Exception",
            StringComparison.Ordinal);
    }

    private static void RequireCompensatedTreatmentPublicationBuilder(
        string exactCompensationMethodName)
    {
        const string relativePath =
            "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs";
        var builder = ReadMethodAnalysis(
            relativePath,
            CompensatedTreatmentPublicationBuilderName);
        var builderCarrier = Assert.IsAssignableFrom<IParameterSymbol>(
            builder.Model.GetDeclaredSymbol(builder.Method.ParameterList.Parameters[0]));
        var settlement = Assert.Single(
            builder.Method.DescendantNodes().OfType<InvocationExpressionSyntax>(),
            invocation => string.Equals(
                ReadInvocationName(invocation),
                TreatmentPublicationSettlementMethodName,
                StringComparison.Ordinal));
        Assert.True(
            IsDirectlyAwaited(settlement),
            "The compensated-exception builder must await settlement before publishing " +
            "the already-compensated carrier.");
        Assert.Contains(
            settlement.ArgumentList.Arguments,
            argument => ExpressionIsExactParameterReference(
                builder,
                argument.Expression,
                builderCarrier));

        var settlementMethod = ReadMethodAnalysis(
            relativePath,
            TreatmentPublicationSettlementMethodName);
        var settlementCarrier = Assert.IsAssignableFrom<IParameterSymbol>(
            settlementMethod.Model.GetDeclaredSymbol(
                settlementMethod.Method.ParameterList.Parameters[0]));
        var exactCompensation = Assert.Single(
            settlementMethod.Method.DescendantNodes()
                .OfType<InvocationExpressionSyntax>(),
            invocation => string.Equals(
                ReadInvocationName(invocation),
                exactCompensationMethodName,
                StringComparison.Ordinal));
        Assert.True(
            IsDirectlyAwaited(exactCompensation),
            "The settlement helper must directly await the exact carrier compensation.");
        var receiver = Assert.IsType<MemberAccessExpressionSyntax>(
            exactCompensation.Expression).Expression;
        Assert.True(
            ExpressionIsExactParameterReference(
                settlementMethod,
                receiver,
                settlementCarrier),
            "The settlement helper must compensate its exact transaction parameter.");
    }

    private static bool ExpressionIsExactParameterReference(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression,
        IParameterSymbol parameter) =>
        UnwrapIdentityPreservingOperation(
            analysis.Model.GetOperation(expression)) is
                IParameterReferenceOperation reference &&
        SymbolEqualityComparer.Default.Equals(reference.Parameter, parameter);

    private static int TopLevelStatementIndex(BlockSyntax block, SyntaxNode node)
    {
        var statement = node.AncestorsAndSelf()
            .OfType<StatementSyntax>()
            .First(candidate => ReferenceEquals(candidate.Parent, block));
        return block.Statements.IndexOf(statement);
    }

    private static bool IsDirectlyAwaited(
        InvocationExpressionSyntax invocation) =>
        invocation.Parent is AwaitExpressionSyntax awaited &&
        ReferenceEquals(awaited.Expression, invocation);

    private static bool IsPotentiallyFallibleOrExitingStatement(
        StatementSyntax statement) =>
        statement.DescendantNodesAndSelf().Any(static node =>
            node is InvocationExpressionSyntax or AwaitExpressionSyntax or
                ObjectCreationExpressionSyntax or
                ImplicitObjectCreationExpressionSyntax or ThrowExpressionSyntax or
                ThrowStatementSyntax or ReturnStatementSyntax or
                ContinueStatementSyntax or BreakStatementSyntax or
                GotoStatementSyntax or YieldStatementSyntax or
                UsingStatementSyntax or LockStatementSyntax ||
            node is LocalDeclarationStatementSyntax declaration &&
                !declaration.UsingKeyword.IsKind(SyntaxKind.None));

    private static bool ExitCanObserveReturnedCarrier(
        SyntaxNode exit,
        StatementSyntax creationStatement)
    {
        foreach (var clause in exit.Ancestors().OfType<CatchClauseSyntax>())
        {
            var owningTry = Assert.IsType<TryStatementSyntax>(clause.Parent);
            if (!owningTry.Block.Span.Contains(creationStatement.Span))
                continue;
            var creationTopLevel = creationStatement.AncestorsAndSelf()
                .OfType<StatementSyntax>()
                .First(statement => ReferenceEquals(
                    statement.Parent,
                    owningTry.Block));
            var index = owningTry.Block.Statements.IndexOf(creationTopLevel);
            var hasFallibleWorkAfterCreation = owningTry.Block.Statements
                .Skip(index + 1)
                .Any(statement => statement.DescendantNodesAndSelf().Any(node =>
                    node is InvocationExpressionSyntax or AwaitExpressionSyntax));
            if (!hasFallibleWorkAfterCreation)
                return false;
        }
        return true;
    }

    private static bool ReturnTransfersExactCarrier(
        SourceMethodAnalysis analysis,
        ReturnStatementSyntax statement,
        ILocalSymbol resultSymbol,
        string carrierPropertyName,
        IReadOnlySet<ILocalSymbol> aliases,
        string? targetCarrierPropertyName = null)
    {
        if (statement.Expression is null)
            return false;
        if (UnwrapIdentityPreservingOperation(
                analysis.Model.GetOperation(statement.Expression)) is
                    ILocalReferenceOperation local &&
            SymbolEqualityComparer.Default.Equals(local.Local, resultSymbol) &&
            SymbolEqualityComparer.Default.Equals(
                local.Type,
                GetEffectiveMethodReturnType(analysis)))
        {
            var declaration = analysis.Method.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .SingleOrDefault(variable => SymbolEqualityComparer.Default.Equals(
                    analysis.Model.GetDeclaredSymbol(variable),
                    resultSymbol));
            return declaration is not null &&
                   IsImmutableLocal(
                       analysis,
                       analysis.Method,
                       resultSymbol,
                       declaration);
        }
        return ReturnExpressionPlacesExactCarrierInResultSlot(
            analysis,
            statement.Expression,
            resultSymbol,
            carrierPropertyName,
            aliases,
            targetCarrierPropertyName ?? carrierPropertyName);
    }

    private static ITypeSymbol? GetEffectiveMethodReturnType(
        SourceMethodAnalysis analysis)
    {
        var method = analysis.Model.GetDeclaredSymbol(analysis.Method) as IMethodSymbol;
        var returnType = method?.ReturnType;
        if (returnType is INamedTypeSymbol
            {
                IsGenericType: true,
                TypeArguments.Length: 1
            } awaitable &&
            awaitable.Name is nameof(Task) or nameof(ValueTask) &&
            string.Equals(
                awaitable.ContainingNamespace?.ToDisplayString(),
                "System.Threading.Tasks",
                StringComparison.Ordinal))
        {
            return awaitable.TypeArguments[0];
        }
        return returnType;
    }

    private static bool ReturnExpressionPlacesExactCarrierInResultSlot(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression,
        ILocalSymbol sourceResultSymbol,
        string sourceCarrierPropertyName,
        IReadOnlySet<ILocalSymbol> sourceCarrierAliases,
        string targetCarrierPropertyName)
    {
        if (UnwrapIdentityPreservingOperation(
                analysis.Model.GetOperation(expression)) is not
                    IObjectCreationOperation creation ||
            creation.Type is not INamedTypeSymbol createdType ||
            !SymbolEqualityComparer.Default.Equals(
                createdType,
                GetEffectiveMethodReturnType(analysis)))
        {
            return false;
        }
        var targetProperties = createdType.GetMembers(targetCarrierPropertyName)
            .OfType<IPropertySymbol>()
            .Where(static property => !property.IsStatic)
            .ToArray();
        if (targetProperties.Length != 1)
            return false;
        var targetProperty = targetProperties[0];
        var sameTypedProperties = createdType.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static property => !property.IsStatic)
            .Where(property => SymbolEqualityComparer.Default.Equals(
                property.Type,
                targetProperty.Type))
            .ToArray();
        if (sameTypedProperties.Length != 1)
            return false;

        var constructorSlots = creation.Constructor?.Parameters
            .Where(parameter =>
                SymbolEqualityComparer.Default.Equals(
                    parameter.Type,
                    targetProperty.Type))
            .ToArray() ?? Array.Empty<IParameterSymbol>();
        IParameterSymbol? targetParameter = null;
        var namedSlots = constructorSlots
            .Where(parameter => string.Equals(
                parameter.Name,
                targetProperty.Name,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (namedSlots.Length == 1)
            targetParameter = namedSlots[0];
        else if (namedSlots.Length > 1)
            return false;

        var targetSlotWrites = 0;
        var exactSlotWrites = 0;
        if (targetParameter is not null)
        {
            var targetArguments = creation.Arguments
                .Where(argument => SymbolEqualityComparer.Default.Equals(
                    argument.Parameter,
                    targetParameter))
                .ToArray();
            targetSlotWrites += targetArguments.Length;
            exactSlotWrites += targetArguments.Count(argument =>
                OperationIsExactCarrierIdentity(
                    argument.Value,
                    sourceResultSymbol,
                    sourceCarrierPropertyName,
                    sourceCarrierAliases));
        }
        if (creation.Initializer is not null)
        {
            var targetAssignments = creation.Initializer.Initializers
                .OfType<ISimpleAssignmentOperation>()
                .Where(assignment =>
                    UnwrapIdentityPreservingOperation(assignment.Target) is
                        IPropertyReferenceOperation property &&
                    SymbolEqualityComparer.Default.Equals(
                        property.Property,
                        targetProperty))
                .ToArray();
            targetSlotWrites += targetAssignments.Length;
            exactSlotWrites += targetAssignments.Count(assignment =>
                OperationIsExactCarrierIdentity(
                    assignment.Value,
                    sourceResultSymbol,
                    sourceCarrierPropertyName,
                    sourceCarrierAliases));
        }
        return targetSlotWrites == 1 && exactSlotWrites == 1;
    }

    private static bool IsExitDominatedByExactCarrierCompensation(
        SourceMethodAnalysis analysis,
        ControlFlowGraph controlFlow,
        IReadOnlyDictionary<int, HashSet<int>> dominators,
        SyntaxNode exit,
        ILocalSymbol resultSymbol,
        string carrierPropertyName,
        IReadOnlySet<ILocalSymbol> aliases,
        string exactCompensationMethodName)
    {
        var exitBlock = TryFindBlockContaining(controlFlow, exit);
        if (exitBlock is not null)
        {
            foreach (var invocation in analysis.Method.DescendantNodes()
                         .OfType<InvocationExpressionSyntax>()
                         .Where(invocation =>
                             IsExactCarrierCompensationLifecycleName(
                                 ReadInvocationName(invocation),
                                 exactCompensationMethodName) &&
                             IsDirectlyAwaited(invocation) &&
                             IsResolvedInstanceInvocation(analysis, invocation) &&
                             InvocationUsesExactCarrierDataFlow(
                                 analysis,
                                 invocation,
                                 resultSymbol,
                                 carrierPropertyName,
                                 aliases)))
            {
                var compensationBlock = TryFindBlockContaining(
                    controlFlow,
                    invocation);
                if (compensationBlock is null ||
                    !Dominates(dominators, compensationBlock, exitBlock))
                {
                    continue;
                }
                if (compensationBlock.Ordinal != exitBlock.Ordinal ||
                    invocation.SpanStart < exit.SpanStart ||
                    exit is ThrowStatementSyntax throwStatement &&
                    throwStatement.Expression?.Span.Contains(invocation.Span) == true)
                {
                    return true;
                }
            }
        }

        foreach (var block in exit.Ancestors().OfType<BlockSyntax>())
        {
            var exitIndex = TopLevelStatementIndex(block, exit);
            if (block.Statements.Take(exitIndex).Any(statement =>
                    statement is ExpressionStatementSyntax or LocalDeclarationStatementSyntax &&
                    statement.DescendantNodesAndSelf()
                        .OfType<InvocationExpressionSyntax>()
                        .Any(invocation =>
                            IsExactCarrierCompensationLifecycleName(
                                ReadInvocationName(invocation),
                                exactCompensationMethodName) &&
                            IsDirectlyAwaited(invocation) &&
                            IsResolvedInstanceInvocation(analysis, invocation) &&
                            InvocationUsesExactCarrierDataFlow(
                                analysis,
                                invocation,
                                resultSymbol,
                                carrierPropertyName,
                                aliases))))
            {
                return true;
            }
        }
        foreach (var tryStatement in analysis.Method.DescendantNodes()
                     .OfType<TryStatementSyntax>()
                     .Where(statement =>
                         statement.Block.Span.Contains(exit.Span) &&
                         statement.Finally is not null))
        {
            if (tryStatement.Finally!.Block.Statements.Any(statement =>
                    statement is ExpressionStatementSyntax or
                        LocalDeclarationStatementSyntax &&
                    statement.DescendantNodesAndSelf()
                        .OfType<InvocationExpressionSyntax>()
                        .Any(invocation =>
                            IsExactCarrierCompensationLifecycleName(
                                ReadInvocationName(invocation),
                                exactCompensationMethodName) &&
                            IsDirectlyAwaited(invocation) &&
                            IsResolvedInstanceInvocation(analysis, invocation) &&
                            InvocationUsesExactCarrierDataFlow(
                                analysis,
                                invocation,
                                resultSymbol,
                                carrierPropertyName,
                                aliases))))
            {
                return true;
            }
        }
        return false;
    }

    private static bool InvocationUsesExactCarrierDataFlow(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax invocation,
        ILocalSymbol resultSymbol,
        string carrierPropertyName,
        IReadOnlySet<ILocalSymbol> aliases)
    {
        var receivers = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => new[] { member.Expression },
            MemberBindingExpressionSyntax => invocation.Ancestors()
                .OfType<ConditionalAccessExpressionSyntax>()
                .Where(candidate => candidate.WhenNotNull.Span.Contains(invocation.Span))
                .Select(static candidate => candidate.Expression),
            _ => Array.Empty<ExpressionSyntax>()
        };
        var carrierExpressions = receivers.Concat(
            invocation.ArgumentList.Arguments.Select(static argument =>
                argument.Expression));
        return carrierExpressions.Any(expression => ExpressionIsExactCarrierIdentity(
                analysis,
                expression,
                resultSymbol,
                carrierPropertyName,
                aliases));
    }

    private static void RequireCarrierForwardingDataFlow(
        PropertyInfo helperCarrier,
        PropertyInfo gameEngineCarrier)
    {
        var canonicalAnalysis = ReadMethodAnalysis(
            "BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs",
            "RefreshCanonicalStateAsync");
        var canonicalMethod = canonicalAnalysis.Method;
        var canonicalInvocation = Assert.Single(canonicalMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>(),
            static invocation => string.Equals(
                ReadInvocationName(invocation),
                "NormalizeAndValidateWithPlanAsync",
                StringComparison.Ordinal));
        var canonicalResultSymbol = RequireAssignedLocalSymbol(
            canonicalAnalysis,
            canonicalInvocation);
        var canonicalAliases = BuildExactCarrierAliases(
            canonicalAnalysis,
            canonicalMethod,
            canonicalResultSymbol,
            helperCarrier.Name);
        Assert.Contains(
            canonicalMethod.DescendantNodes().OfType<ReturnStatementSyntax>(),
            statement => ReturnTransfersExactCarrier(
                canonicalAnalysis,
                statement,
                canonicalResultSymbol,
                helperCarrier.Name,
                canonicalAliases));

        var gameEngineAnalysis = ReadMethodAnalysis(
            "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
            "RefreshAcceptedTurnCanonicalStateForValidationAsync");
        var gameEngineMethod = gameEngineAnalysis.Method;
        var helperInvocation = Assert.Single(gameEngineMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>(),
            static invocation => string.Equals(
                ReadInvocationName(invocation),
                "RefreshCanonicalStateAsync",
                StringComparison.Ordinal));
        var helperResultSymbol = RequireAssignedLocalSymbol(
            gameEngineAnalysis,
            helperInvocation);
        var helperAliases = BuildExactCarrierAliases(
            gameEngineAnalysis,
            gameEngineMethod,
            helperResultSymbol,
            helperCarrier.Name);
        var resultType = gameEngineCarrier.DeclaringType;
        Assert.NotNull(resultType);
        var matchingTransfers = gameEngineMethod.DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .Where(statement => statement.Expression is not null)
            .Where(statement => ReturnTransfersExactCarrier(
                gameEngineAnalysis,
                statement,
                helperResultSymbol,
                helperCarrier.Name,
                helperAliases,
                gameEngineCarrier.Name))
            .ToArray();
        Assert.True(
            matchingTransfers.Length == 1,
            "AcceptedTurnCanonicalRefreshResult must receive the exact carrier property " +
            "flowing from AcceptedTurnCanonicalStateRefresh.Result exactly once. " +
            $"Matching ownership transfers: {matchingTransfers.Length}.");
    }

    private async Task<HeldTreatmentPipelineContext>
        CreateHeldTreatmentPipelineContextAsync(
        AcceptedTreatmentPipelineFault? fault,
        bool withRollbackAuthority = false,
        HeldTreatmentItemScenario? itemScenario = null,
        bool composePublication = true,
        FileSystemManagerHooks? hooks = null,
        MortalItemPublicationBaselineFixture baselineFixture =
            MortalItemPublicationBaselineFixture.None,
        Func<FileSystemManager, Task>? configureBeforePreparation = null)
    {
        Assert.True(
            fault is null || hooks is null,
            "A held-treatment fixture accepts either its pipeline fault hooks or " +
            "its Mortal-item baseline hooks, never both.");
        var effectiveHooks = hooks ?? fault?.Hooks;
        var root = Path.Combine(
            Path.GetTempPath(),
            "boe-held-treatment-pipeline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var fileSystem = new FileSystemManager(
            root,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            effectiveHooks);
        fileSystem.EnsureDirectoryStructure();
        HeldTreatmentPipelineContext? context = null;
        try
        {
            CopyDirectory(TestRepoPaths.BaseSessionRoot, fileSystem.GameSessionPath);
            var wound = CreateGuaranteedResourceTreatmentWound(
                includeItemRequirement: itemScenario is not null);
            await SeedHeldTreatmentAuthorityAsync(fileSystem, wound, itemScenario);
            await SeedMortalItemPublicationBaselineFixtureAsync(
                fileSystem,
                baselineFixture);
            if (configureBeforePreparation is not null)
                await configureBeforePreparation(fileSystem);
            var prepared = await new LiveTurnPreparationService(fileSystem)
                .PrepareAsync(new LiveTurnPreparationOptions
                {
                    SessionId = "session_t070b3_game_engine",
                    RequestId = "request_t070b3_game_engine",
                    TurnNumber = 42,
                    PlayerAction =
                        "Stabilize the accepted wound with the exact held energy resource.",
                    CurrentRealm = "Mortal World",
                    PreGeneratedDices1d20 = new[] { 17, 4, 1, 20 }
                });
            Assert.Equal("input/turn_request.json", prepared.TurnRequestPath);
            if (withRollbackAuthority)
            {
                var rollbackEngine = CreateGameEngine(
                    new QueuedConsoleInputSource([]),
                    fileSystem: fileSystem);
                var rollbackSnapshot = await InvokePrivateTaskResultAsync(
                    rollbackEngine,
                    "CreatePreTurnBackup",
                    "t070b3_pre_token_raw_validation");
                var preparedTurnRequest = JsonSerializer.Deserialize<TurnRequest>(
                    Assert.IsType<string>(await fileSystem.ReadFileAsync(
                        LiveTurnPreparationService.TurnRequestPath)),
                    LiveTurnPreparationService.ManifestJsonOptions);
                Assert.NotNull(preparedTurnRequest);
                _ = await InvokePrivateTaskResultAsync(
                    rollbackEngine,
                    "CreateCanonicalBaselineSnapshotAsync",
                    preparedTurnRequest,
                    rollbackSnapshot,
                    "T070-B.3 pre-token raw-validation rollback oracle");
            }
            var parsedContext = MortalWoundTreatmentAuthority.ParseContext(
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["realm"] = "mortal_world",
                    ["targetKind"] = "player",
                    ["targetId"] = "player_current",
                    ["providerKind"] = "npc",
                    ["providerId"] = "field_medic_01",
                    ["currentLocationId"] = "loc_field_clinic_001"
                }.ToJsonString(),
                "treatmentContext");
            Assert.True(
                parsedContext.IsValid,
                DescribeValidationIssues(parsedContext.Issues));
            context = new HeldTreatmentPipelineContext(
                root,
                fileSystem,
                Assert.IsType<MortalWoundTreatmentAuthority.Context>(
                    parsedContext.Context),
                effectiveHooks,
                itemScenario);
            await context.AcquireLeaseAsync();

            var acceptedState = ExportCurrentTreatmentAcceptedState(context);
            var history = ReadCurrentTreatmentHistory(fileSystem);
            var currentWound = ReadCurrentTreatmentWound(fileSystem);
            var preparedRequest = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(
                acceptedState,
                history,
                currentWound,
                HeldTreatmentPipelineContext.OperationKey,
                HeldTreatmentPipelineContext.RouteId,
                Assert.Single(acceptedState.Binding.AcceptedEvents).EventRef);
            Assert.True(
                preparedRequest.IsValid,
                DescribeValidationIssues(preparedRequest.Issues));
            var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
                preparedRequest.Request);
            AssertHeldTreatmentClaims(request, itemScenario);
            var resolved = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(
                request,
                history,
                currentWound,
                acceptedState);
            Assert.Equal("Resolved", resolved.Disposition);
            Assert.Empty(resolved.Issues);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(
                resolved.Resolution);
            context.SetResolvedAuthorities(acceptedState, request, resolution);
            var commandRoot = WoundResponseInputComposer
                .ComposeMortalWoundTreatmentCommandRoot(
                    acceptedState.Binding,
                    resolution,
                    HeldTreatmentPipelineContext.FinalSceneText);
            var parsedCommand = WoundResponseInputComposer.ParseCommandRoot(
                JsonSerializer.SerializeToElement(commandRoot));
            Assert.True(
                parsedCommand.Success,
                DescribeValidationIssues(parsedCommand.Issues));
            var recomposed = WoundResponseInputComposer.RecomposeCommandRoot(
                acceptedState.Binding,
                parsedCommand,
                Array.Empty<WoundOpportunityDecisionReceipt>());
            Assert.True(
                recomposed.Success,
                DescribeValidationIssues(recomposed.Issues));

            var resourceStateBeforeDistribution =
                await fileSystem.ReadFileBytesAsync(
                    ResourceMaterializationContract.StatePath);
            var treatmentProposal = itemScenario?.CreateTreatmentProposal() ??
                                    new GameResponse();
            Assert.Null(treatmentProposal.Response);
            treatmentProposal.Response = HeldTreatmentPipelineContext.FinalSceneText;
            await context.ReleaseLeaseAsync();
            await new StateDistributor(
                    fileSystem,
                    NullLogger<StateDistributor>.Instance)
                .DistributeAsync(
                    treatmentProposal,
                    recomposed);
            Assert.Equal(
                resourceStateBeforeDistribution,
                await fileSystem.ReadFileBytesAsync(
                    ResourceMaterializationContract.StatePath));
            await AddGuardianCleanupCommandSurfaceAsync(fileSystem);
            await new ProgressionScheduleService(
                    fileSystem,
                    NullLogger<ProgressionScheduleService>.Instance)
                .EnsureInitializedAsync();
            await context.AcquireLeaseAsync();
            if (composePublication)
                await RestoreHeldTreatmentAndComposeSameSemanticPlanAsync(context);
            fault?.CaptureOriginalAuthority(context);
            return context;
        }
        catch
        {
            if (context is not null)
                await context.DisposeAsync();
            else
                DeleteHeldTreatmentRootBestEffort(root);
            throw;
        }
    }

    private static async Task RestoreHeldTreatmentAndComposeSameSemanticPlanAsync(
        HeldTreatmentPipelineContext context)
    {
        var authorities = RestoreHeldTreatmentAuthorities(context);
        var publication = WoundAcceptedTurnPlanner
            .ComposeMortalWoundTreatmentPublication(
                context.FileSystem,
                context.Lease,
                context.CreateMechanicsOnlyTreatmentProposal(),
                authorities.AcceptedState,
                authorities.Request,
                authorities.Resolution);
        Assert.True(
            publication.IsValid,
            DescribeValidationIssues(publication.Issues));
        Assert.Empty(publication.Issues);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(publication.Plan);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            context.Lease,
            out var binding,
            out var cached));
        Assert.True(cached.Success, DescribeValidationIssues(cached.Issues));
        Assert.Same(plan, cached.Plan);
        context.SetCurrentAuthorities(
            authorities.AcceptedState,
            authorities.Request,
            authorities.Resolution,
            plan,
            binding);
    }

    private static HeldTreatmentAuthorities RestoreHeldTreatmentAuthorities(
        HeldTreatmentPipelineContext context)
    {
        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var recovered = acceptedState.RestorePersistedTreatmentRequests(history);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        var request = Assert.Single(recovered.HeldRequests);
        Assert.Equal("held", request.ResourceAuthority.ReservationDisposition);
        AssertHeldTreatmentClaims(request, context.ItemScenario);
        var resolved = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(
            request,
            history,
            ReadCurrentTreatmentWound(context.FileSystem),
            acceptedState);
        Assert.Equal("Resolved", resolved.Disposition);
        Assert.Empty(resolved.Issues);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(
            resolved.Resolution);
        context.SetResolvedAuthorities(acceptedState, request, resolution);
        return new HeldTreatmentAuthorities(acceptedState, request, resolution);
    }

    private static void AssertHeldTreatmentClaims(
        MortalWoundTreatmentAttemptRequest request,
        HeldTreatmentItemScenario? itemScenario)
    {
        Assert.Contains(request.ResourceAuthority.Claims, claim =>
            string.Equals(claim.Kind, "resource_quantity", StringComparison.Ordinal) &&
            string.Equals(claim.AuthorityRef, "energy", StringComparison.Ordinal) &&
            string.Equals(claim.OwnerKind, "player", StringComparison.Ordinal) &&
            string.Equals(claim.OwnerId, "player_current", StringComparison.Ordinal) &&
            claim.Quantity == 2);
        if (itemScenario is null)
        {
            Assert.Single(request.ResourceAuthority.Claims);
            return;
        }

        Assert.Equal(2, request.ResourceAuthority.Claims.Count);
        Assert.Contains(request.ResourceAuthority.Claims, claim =>
            string.Equals(claim.Kind, "item_quantity", StringComparison.Ordinal) &&
            string.Equals(
                claim.AuthorityRef,
                HeldTreatmentPipelineContext.SelectedItemId,
                StringComparison.Ordinal) &&
            string.Equals(claim.OwnerKind, "npc", StringComparison.Ordinal) &&
            string.Equals(
                claim.OwnerId,
                HeldTreatmentPipelineContext.ProviderId,
                StringComparison.Ordinal) &&
            claim.Quantity == 1);
    }

    private static MortalWoundTreatmentAcceptedStateAuthority
        ExportCurrentTreatmentAcceptedState(HeldTreatmentPipelineContext context)
    {
        var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(
            context.FileSystem,
            context.Lease,
            context.TreatmentContext,
            HeldTreatmentPipelineContext.WoundId);
        Assert.True(exported.IsValid, DescribeValidationIssues(exported.Issues));
        return Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            exported.Authority);
    }

    private static WoundMaterializationEnvelope ReadCurrentTreatmentWound(
        FileSystemManager fileSystem)
    {
        var root = JsonNode.Parse(File.ReadAllText(fileSystem.ResolvePath(
            WoundCarrierCatalog.PlayerPath)))!.AsObject();
        var woundRoot = Assert.IsType<JsonObject>(Assert.Single(
            root["activeWounds"]!.AsArray()));
        var parsed = WoundMaterializationContract.Parse(
            woundRoot.ToJsonString(),
            WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
        Assert.True(parsed.IsValid, DescribeValidationIssues(parsed.Issues));
        return Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
    }

    private static WoundHistoryParseResult ReadCurrentTreatmentHistory(
        FileSystemManager fileSystem)
    {
        var parsed = WoundHistoryState.Parse(
            File.ReadAllText(fileSystem.ResolvePath(WoundHistoryState.HistoryPath)),
            WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, DescribeValidationIssues(parsed.Issues));
        return parsed;
    }

    private static async Task<ResourceHistoryState> ReadTreatmentResourceHistoryAsync(
        FileSystemManager fileSystem)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await fileSystem.ReadFileAsync(
                ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false);
        Assert.True(definitions.IsValid, DescribeValidationIssues(definitions.Issues));
        var history = ResourceHistoryState.ParseCanonical(
            await fileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            Assert.IsType<ResourceDefinitionCatalog>(definitions.Catalog),
            allowMissingPristine: false);
        Assert.True(history.IsValid, DescribeValidationIssues(history.Issues));
        return Assert.IsType<ResourceHistoryState>(history.History);
    }

    private static int CountHeldTreatmentEnergySpends(ResourceHistoryState history) =>
        history.Transitions.Count(static transition =>
            transition.Operation == ResourceTransitionOperation.Spend &&
            transition.Phase == ResourceMutationPhase.RegisteredSystemOutcome &&
            transition.Coordinate.OwnerKind == ResourceOwnerKind.Player &&
            string.Equals(
                transition.Coordinate.ResourceOwnerId,
                "player_current",
                StringComparison.Ordinal) &&
            string.Equals(
                transition.Coordinate.ResourceKey,
                "energy",
                StringComparison.Ordinal));

    private static async Task<bool> ContainsCurrentExactTreatmentRequestAsync(
        HeldTreatmentPipelineContext context)
    {
        var commandBytes = await context.ReadFileBytesAsync(
            AcceptedMechanicsPlan.WoundCommandPath);
        var pendingBytes = await context.ReadFileBytesAsync(
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        var command = commandBytes is null
            ? new JsonObject()
            : ParseJsonObjectBytes(commandBytes);
        var pending = pendingBytes is null
            ? null
            : ParseJsonObjectBytes(pendingBytes);
        return MortalWoundTreatmentDurableSurfaceQuarantine.ContainsExactRequestRows(
            command,
            pending,
            context.Request.Coordinates.OperationKey,
            context.Request.Coordinates.AttemptId,
            context.Request.RequestFingerprint);
    }

    private static JsonObject ParseJsonObjectBytes(byte[] bytes) =>
        Assert.IsType<JsonObject>(JsonNode.Parse(
            Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF')));

    private static async Task AssertConfirmedHeldLiveRegistryProbeAsync(
        HeldTreatmentPipelineContext context)
    {
        var finalized = MortalWoundTreatmentResourceComposer.Finalize(
            context.Resolution);
        Assert.True(finalized.IsValid, DescribeValidationIssues(finalized.Issues));
        var finalization = Assert.IsType<MortalWoundTreatmentResourceFinalization>(
            finalized.Finalization);
        var typed = await ProbeLiveRegistryAsync(context);

        Assert.True(Assert.IsType<bool>(ReadRequiredProbeProperty(
            typed,
            "IsValid")));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ValidationIssue>>(
            ReadRequiredProbeProperty(typed, "Issues")));
        Assert.Equal(0, Assert.IsType<int>(ReadRequiredProbeProperty(
            typed,
            "ChangedCount")));
        Assert.Equal("ConfirmedHeld", Convert.ToString(
            ReadRequiredProbeProperty(typed, "State")));
        Assert.Equal(context.Request.Coordinates.OperationKey, Convert.ToString(
            ReadRequiredProbeProperty(typed, "OperationKey")));
        Assert.Equal(context.Request.RequestFingerprint, Convert.ToString(
            ReadRequiredProbeProperty(typed, "RequestFingerprint")));
        Assert.Equal(finalization.ResourceAuthorityFingerprint, Convert.ToString(
            ReadRequiredProbeProperty(typed, "ResourceAuthorityFingerprint")));
        Assert.Equal(finalization.FinalizationFingerprint, Convert.ToString(
            ReadRequiredProbeProperty(typed, "FinalizationFingerprint")));
        Assert.Equal(context.OriginalSessionGeneration, Convert.ToString(
            ReadRequiredProbeProperty(typed, "SessionGeneration")));
        Assert.Equal(context.OriginalSessionGenerationRevision, Convert.ToInt64(
            ReadRequiredProbeProperty(typed, "SessionGenerationRevision")));
    }

    private static async Task<object> ProbeLiveRegistryAsync(
        HeldTreatmentPipelineContext context)
    {
        var finalized = MortalWoundTreatmentResourceComposer.Finalize(
            context.Resolution);
        Assert.True(finalized.IsValid, DescribeValidationIssues(finalized.Issues));
        var finalization = Assert.IsType<MortalWoundTreatmentResourceFinalization>(
            finalized.Finalization);
        var candidates = typeof(AcceptedTurnAuthorityRegistry).Assembly
            .GetTypes()
            .SelectMany(static type => type.GetMethods(
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic))
            .Where(static method => method.Name.Contains(
                "probe",
                StringComparison.OrdinalIgnoreCase))
            .Where(static method =>
                method.Name.Contains("treatment", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("publication", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("resource", StringComparison.OrdinalIgnoreCase))
            .Select(method => new
            {
                Method = method,
                Arguments = TryBindLiveRegistryProbeArguments(
                    method,
                    context,
                    finalization)
            })
            .Where(static candidate => candidate.Arguments is not null)
            .ToArray();
        Assert.True(
            candidates.Length == 1,
            "Production must expose exactly one non-mutating typed live-registry probe " +
            "for the held-treatment publication authority. Candidates: " +
            string.Join(", ", candidates.Select(static value =>
                value.Method.DeclaringType?.FullName + "." + value.Method.Name)));
        var candidate = candidates[0];
        var raw = candidate.Method.Invoke(null, candidate.Arguments);
        var result = await AwaitReflectedResultAsync(raw);
        var typed = Assert.IsAssignableFrom<object>(result);
        return typed;
    }

    private static object?[]? TryBindLiveRegistryProbeArguments(
        MethodInfo method,
        HeldTreatmentPipelineContext context,
        MortalWoundTreatmentResourceFinalization finalization)
    {
        if (!method.IsStatic || method.ContainsGenericParameters ||
            method.GetParameters().Any(static parameter =>
                parameter.ParameterType.IsByRef || parameter.IsOut))
        {
            return null;
        }

        object[] authorities =
        {
            context.FileSystem,
            context.Lease,
            context.AcceptedState,
            context.Request,
            context.Resolution,
            finalization,
            context.Plan,
            context.OriginalBinding
        };
        var arguments = new object?[method.GetParameters().Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            var parameter = method.GetParameters()[index];
            var matches = authorities
                .Where(parameter.ParameterType.IsInstanceOfType)
                .ToArray();
            if (matches.Length == 1)
            {
                arguments[index] = matches[0];
                continue;
            }
            if (parameter.HasDefaultValue)
            {
                arguments[index] = parameter.DefaultValue;
                continue;
            }
            return null;
        }
        return arguments;
    }

    private static async Task<object?> AwaitReflectedResultAsync(object? value)
    {
        if (value is null)
            return null;
        if (value is Task task)
        {
            await task;
            return task.GetType().GetProperty(
                "Result",
                BindingFlags.Instance | BindingFlags.Public)?.GetValue(task);
        }
        var type = value.GetType();
        if (type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var asTask = Assert.IsAssignableFrom<Task>(type.GetMethod(
                "AsTask",
                BindingFlags.Instance | BindingFlags.Public)!.Invoke(value, null));
            await asTask;
            return asTask.GetType().GetProperty(
                "Result",
                BindingFlags.Instance | BindingFlags.Public)?.GetValue(asTask);
        }
        return value;
    }

    private static object ReadRequiredProbeProperty(object value, string name)
    {
        var property = value.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<object>(property.GetValue(value));
    }

    private static void AssertConfirmedHeldBlocksCompetingTreatment(
        HeldTreatmentPipelineContext context,
        string expectedIssueCode =
            "mortal_wound_treatment_resource_reservation_overbooked")
    {
        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        var history = ReadCurrentTreatmentHistory(context.FileSystem);
        var recovered = acceptedState.RestorePersistedTreatmentRequests(history);
        Assert.True(recovered.IsValid, DescribeValidationIssues(recovered.Issues));
        var durableHeld = Assert.Single(recovered.HeldRequests);
        Assert.Equal(
            context.Request.Coordinates.OperationKey,
            durableHeld.Coordinates.OperationKey);
        Assert.Equal(
            context.Request.Coordinates.AttemptId,
            durableHeld.Coordinates.AttemptId);
        Assert.Equal(context.Request.RequestFingerprint, durableHeld.RequestFingerprint);
        Assert.Equal("held", durableHeld.ResourceAuthority.ReservationDisposition);
        var wound = ReadCurrentTreatmentWound(context.FileSystem);
        var competing = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(
            acceptedState,
            history,
            wound,
            HeldTreatmentPipelineContext.OperationKey + "_competing",
            HeldTreatmentPipelineContext.RouteId,
            Assert.Single(acceptedState.Binding.AcceptedEvents).EventRef);
        Assert.False(competing.IsValid);
        Assert.Null(competing.Request);
        Assert.Equal(
            expectedIssueCode,
            Assert.Single(competing.Issues).Code);
    }

    private static void AssertTreatmentPublicationRestartBlocked(
        HeldTreatmentPipelineContext context)
    {
        var acceptedState = ExportCurrentTreatmentAcceptedState(context);
        var blocked = WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(
            context.FileSystem,
            context.Lease,
            new GameResponse(),
            acceptedState,
            context.Request,
            context.Resolution);
        Assert.False(blocked.IsValid);
        Assert.Null(blocked.Plan);
        Assert.Equal(
            "mortal_wound_treatment_publication_compensation_restart_required",
            Assert.Single(blocked.Issues).Code);
    }

    private static async Task<IReadOnlyDictionary<string, ExactFileImage>>
        CaptureExactTreatmentTransactionBytesAsync(
        HeldTreatmentPipelineContext context)
    {
        var paths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
            .Concat(context.Plan.TouchedPaths)
            .Append(AcceptedMechanicsPlan.WoundCommandPath)
            .Concat(new[]
            {
                NpcCoreChangesContract.NpcCorePath,
                MortalItemAcceptedTransferCatalog.NpcCommandsPath,
                InventoryEquipmentService.ItemsPath,
                MortalItemIdentityState.StatePath,
                ResourceMaterializationContract.DefinitionsPath,
                ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath,
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                ResourceMaterializationContract.CommandPath,
                WoundCarrierCatalog.PlayerPath,
                WoundIdentityState.StatePath,
                WoundHistoryState.HistoryPath,
                "output/narrative_response.json"
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var images = new Dictionary<string, ExactFileImage>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var bytes = await context.ReadFileBytesAsync(path);
            images.Add(path, new ExactFileImage(bytes is not null, bytes?.ToArray()));
        }
        return images;
    }

    private static async Task AssertExactTreatmentTransactionBytesAsync(
        HeldTreatmentPipelineContext context,
        IReadOnlyDictionary<string, ExactFileImage> expected)
    {
        foreach (var pair in expected)
        {
            var actual = await context.ReadFileBytesAsync(pair.Key);
            Assert.Equal(pair.Value.Exists, actual is not null);
            if (pair.Value.Exists)
                Assert.Equal(pair.Value.Bytes, actual);
        }
    }

    private async Task<(GameEngine Engine, object SnapshotContext)>
        CreateHeldTreatmentValidationEngineAsync(HeldTreatmentPipelineContext context)
    {
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Escape) }),
            fileSystem: context.FileSystem);
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var snapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);
        return (engine, snapshotContext);
    }

    private async Task<PublishedTreatmentFullStateProbe>
        CreatePublishedTreatmentFullStateProbeAsync(
            HeldTreatmentPipelineContext context)
    {
        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) =
            await CreateHeldTreatmentValidationEngineAsync(context);
        var refresh = await InvokePrivateTaskResultAsync(
            engine,
            "RefreshAcceptedTurnCanonicalStateForValidationAsync",
            HeldTreatmentPipelineContext.Turn,
            snapshotContext);
        var baselineUsable = Assert.IsType<bool>(refresh.GetType().GetProperty(
            "BaselineUsable",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(refresh));
        Assert.True(baselineUsable);
        var postSealIssues = Assert.IsAssignableFrom<IEnumerable<ValidationIssue>>(
            refresh.GetType().GetProperty(
                "PostSealIssues",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .GetValue(refresh));
        Assert.DoesNotContain(postSealIssues, static issue =>
            issue.Severity == IssueSeverity.Error);
        var transaction = Assert.IsAssignableFrom<IAsyncDisposable>(
            refresh.GetType().GetProperty(
                "TreatmentResourcePublicationTransaction",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .GetValue(refresh));
        await InvokePrivateTaskAsync(
            engine,
            "EnsureClientOwnedSystemFilesHealthyAsync");
        var fullStateIssues = await new ValidationService(
                context.FileSystem,
                NullLogger<ValidationService>.Instance)
            .ValidateGameStateAsync();
        var errors = fullStateIssues.Where(static issue =>
            issue.Severity == IssueSeverity.Error).ToArray();
        var continuityIssue = Assert.Single(errors);
        Assert.Equal(
            "npc_existing_inventory_resend_forbidden",
            continuityIssue.Code);
        return new PublishedTreatmentFullStateProbe(
            engine,
            snapshotContext,
            transaction,
            fullStateIssues);
    }

    private static ValidationIssue CloneValidationIssue(
        ValidationIssue issue,
        string? filePath = null,
        string? actor = null,
        string? actual = null) => new(
        filePath ?? issue.FilePath,
        issue.Severity,
        issue.Message,
        code: issue.Code,
        actor: actor ?? issue.Actor,
        section: issue.Section,
        expected: issue.Expected,
        actual: actual ?? issue.Actual,
        repairHint: issue.RepairHint,
        category: issue.Category,
        repairTargetFiles: issue.RepairTargetFiles);

    private sealed record PublishedTreatmentFullStateProbe(
        GameEngine Engine,
        object SnapshotContext,
        object Transaction,
        IReadOnlyList<ValidationIssue> Issues);

    private static async Task<IReadOnlyDictionary<string, ExactFileImage>>
        CaptureExplicitTreatmentEnvelopeBytesAsync(
            HeldTreatmentPipelineContext context)
    {
        var paths = new[]
        {
            NpcCoreChangesContract.NpcCorePath,
            MortalItemAcceptedTransferCatalog.NpcCommandsPath,
            InventoryEquipmentService.ItemsPath,
            MortalItemIdentityState.StatePath,
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            ResourceMaterializationContract.CommandPath,
            WoundCarrierCatalog.PlayerPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath,
            AcceptedMechanicsPlan.WoundCommandPath,
            "output/narrative_response.json"
        };
        var images = new Dictionary<string, ExactFileImage>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var bytes = await context.ReadFileBytesAsync(path);
            images.Add(path, new ExactFileImage(bytes is not null, bytes?.ToArray()));
        }
        return images;
    }

    private static void AssertTreatmentItemTransactionEnvelopeCaptured(
        IReadOnlyDictionary<string, ExactFileImage> images)
    {
        foreach (var requiredPath in new[]
                 {
                     NpcCoreChangesContract.NpcCorePath,
                     MortalItemAcceptedTransferCatalog.NpcCommandsPath,
                     InventoryEquipmentService.ItemsPath,
                     MortalItemIdentityState.StatePath,
                     ResourceMaterializationContract.DefinitionsPath,
                     ResourceMaterializationContract.StatePath,
                     ResourceMaterializationContract.HistoryPath,
                     CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                     ResourceMaterializationContract.CommandPath,
                     WoundCarrierCatalog.PlayerPath,
                     WoundIdentityState.StatePath,
                     WoundHistoryState.HistoryPath,
                     AcceptedMechanicsPlan.WoundCommandPath,
                     "output/narrative_response.json"
                 })
        {
            Assert.True(images.ContainsKey(requiredPath),
                $"Rollback envelope omitted '{requiredPath}'.");
        }
    }

    private static async Task<JsonObject> ReadJsonObjectAsync(
        FileSystemManager fileSystem,
        string path) => Assert.IsType<JsonObject>(JsonNode.Parse(
        Assert.IsType<string>(await fileSystem.ReadFileAsync(path))));

    private static async Task<JsonObject> ReadJsonObjectAsync(
        HeldTreatmentPipelineContext context,
        string path) => Assert.IsType<JsonObject>(JsonNode.Parse(
        Encoding.UTF8.GetString(Assert.IsType<byte[]>(
            await context.ReadFileBytesAsync(path))).TrimStart('\uFEFF')));

    private static JsonObject ReadProvider(JsonObject npcCore) => Assert.Single(
        npcCore["NPCsInScene"]!.AsArray().OfType<JsonObject>(),
        npc => string.Equals(
            npc["NPCId"]?.GetValue<string>(),
            HeldTreatmentPipelineContext.ProviderId,
            StringComparison.Ordinal));

    private static int ReadNpcItemCount(JsonObject npcCore, string itemId)
    {
        var item = Assert.Single(
            ReadProvider(npcCore)["inventory"]!.AsArray().OfType<JsonObject>(),
            candidate => string.Equals(
                candidate["itemId"]?.GetValue<string>(),
                itemId,
                StringComparison.Ordinal));
        return item["count"]!.GetValue<int>();
    }

    private static async Task<int> ReadSelectedNpcItemCountAsync(
        HeldTreatmentPipelineContext context) => ReadNpcItemCount(
        await ReadJsonObjectAsync(
            context,
            NpcCoreChangesContract.NpcCorePath),
        HeldTreatmentPipelineContext.SelectedItemId);

    private static async Task<MortalItemIdentityParseResult>
        ReadTreatmentItemIdentityAsync(FileSystemManager fileSystem)
    {
        var parsed = MortalItemIdentityState.Parse(
            await fileSystem.ReadFileAsync(MortalItemIdentityState.StatePath));
        Assert.Empty(parsed.Issues);
        return parsed;
    }

    private static async Task<MortalItemIdentityParseResult>
        ReadTreatmentItemIdentityAsync(HeldTreatmentPipelineContext context)
    {
        var bytes = Assert.IsType<byte[]>(await context.ReadFileBytesAsync(
            MortalItemIdentityState.StatePath));
        var parsed = MortalItemIdentityState.Parse(
            Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
        Assert.Empty(parsed.Issues);
        return parsed;
    }

    private static async Task<int> CountSelectedItemConsumeTransitionsAsync(
        HeldTreatmentPipelineContext context)
    {
        var index = await ReadTreatmentItemIdentityAsync(context);
        if (!index.EntriesByItemId.TryGetValue(
                HeldTreatmentPipelineContext.SelectedItemId,
                out var entry))
        {
            return 0;
        }
        return entry["transitions"]!.AsArray().OfType<JsonObject>().Count(
            transition => string.Equals(
                transition["kind"]?.GetValue<string>(),
                "consume",
                StringComparison.Ordinal));
    }

    private static async Task<int> ReadCurrentTreatmentEnergyAsync(
        HeldTreatmentPipelineContext context)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            Encoding.UTF8.GetString(Assert.IsType<byte[]>(
                await context.ReadFileBytesAsync(
                    ResourceMaterializationContract.DefinitionsPath))).TrimStart('\uFEFF'),
            allowMissingPristine: false);
        Assert.True(definitions.IsValid, DescribeValidationIssues(definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(
            Encoding.UTF8.GetString(Assert.IsType<byte[]>(
                await context.ReadFileBytesAsync(
                    ResourceMaterializationContract.StatePath))).TrimStart('\uFEFF'),
            Assert.IsType<ResourceDefinitionCatalog>(definitions.Catalog),
            allowMissingPristine: false);
        Assert.True(state.IsValid, DescribeValidationIssues(state.Issues));
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Player,
            "player_current",
            "energy");
        Assert.True(state.Ledger!.TryResolveExact(coordinate, out var entry));
        return decimal.ToInt32(Assert.IsType<ResourceStateEntry>(entry).Current);
    }

    private static int CountSelectedItemDurabilityCapacityTransitions(
        ResourceHistoryState history) => history.Transitions.Count(transition =>
        transition.Coordinate.OwnerKind == ResourceOwnerKind.Item &&
        string.Equals(
            transition.Coordinate.ResourceOwnerId,
            HeldTreatmentPipelineContext.SelectedItemId,
            StringComparison.Ordinal) &&
        string.Equals(
            transition.Coordinate.ResourceKey,
            "durability",
            StringComparison.Ordinal) &&
        transition.Operation == ResourceTransitionOperation.Reconfigure);

    private static async Task<ResourceStateEntry> ReadSelectedItemDurabilityAsync(
        HeldTreatmentPipelineContext context)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            Encoding.UTF8.GetString(Assert.IsType<byte[]>(
                await context.ReadFileBytesAsync(
                    ResourceMaterializationContract.DefinitionsPath))).TrimStart('\uFEFF'),
            allowMissingPristine: false);
        Assert.True(definitions.IsValid, DescribeValidationIssues(definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(
            Encoding.UTF8.GetString(Assert.IsType<byte[]>(
                await context.ReadFileBytesAsync(
                    ResourceMaterializationContract.StatePath))).TrimStart('\uFEFF'),
            Assert.IsType<ResourceDefinitionCatalog>(definitions.Catalog),
            allowMissingPristine: false);
        Assert.True(state.IsValid, DescribeValidationIssues(state.Issues));
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Item,
            HeldTreatmentPipelineContext.SelectedItemId,
            "durability");
        Assert.True(state.Ledger!.TryResolveExact(coordinate, out var entry));
        return Assert.IsType<ResourceStateEntry>(entry);
    }

    private static bool HasUnrelatedCreationReceipt(JsonObject item) =>
        string.Equals(
            item["materializationReceipt"]?["creationRef"]?.GetValue<string>(),
            HeldTreatmentPipelineContext.UnrelatedCreationRef,
            StringComparison.Ordinal);

    private static async Task ApplySelectedItemPostSealDriftAsync(
        HeldTreatmentPipelineContext context,
        string driftAxis)
    {
        switch (driftAxis)
        {
            case "count":
            {
                var npcCore = await ReadJsonObjectAsync(
                    context,
                    NpcCoreChangesContract.NpcCorePath);
                var item = Assert.Single(
                    ReadProvider(npcCore)["inventory"]!.AsArray().OfType<JsonObject>());
                item["count"] = 3;
                MortalItemTestFixture.ResealCanonical(item);
                await context.FileSystem.WriteFileAtomicAsync(
                    context.Lease,
                    NpcCoreChangesContract.NpcCorePath,
                    npcCore.ToJsonString());
                var identity = await ReadTreatmentItemIdentityAsync(context);
                var entry = identity.EntriesByItemId[
                    HeldTreatmentPipelineContext.SelectedItemId];
                var lastTransition = Assert.IsType<JsonObject>(
                    entry["transitions"]!.AsArray()[^1]);
                lastTransition["quantityAfter"] = 3;
                var reparsed = MortalItemIdentityState.Parse(identity.Root);
                Assert.Empty(reparsed.Issues);
                await context.FileSystem.WriteFileAtomicAsync(
                    context.Lease,
                    MortalItemIdentityState.StatePath,
                    identity.Root.ToJsonString());
                break;
            }
            case "carrier":
            {
                var npcCore = await ReadJsonObjectAsync(
                    context,
                    NpcCoreChangesContract.NpcCorePath);
                var inventory = ReadProvider(npcCore)["inventory"]!.AsArray();
                var item = Assert.Single(inventory.OfType<JsonObject>());
                inventory.Clear();
                var playerItems = await ReadJsonObjectAsync(
                    context,
                    InventoryEquipmentService.ItemsPath);
                playerItems["items"] ??= new JsonArray();
                playerItems["items"]!.AsArray().Add(item.DeepClone());
                await context.FileSystem.WriteFileAtomicAsync(
                    context.Lease,
                    NpcCoreChangesContract.NpcCorePath,
                    npcCore.ToJsonString());
                await context.FileSystem.WriteFileAtomicAsync(
                    context.Lease,
                    InventoryEquipmentService.ItemsPath,
                    playerItems.ToJsonString());
                var identity = await ReadTreatmentItemIdentityAsync(context);
                var entry = identity.EntriesByItemId[
                    HeldTreatmentPipelineContext.SelectedItemId];
                var sourceCarrier = Assert.IsType<JsonObject>(
                    entry["currentCarrier"]!.DeepClone());
                var destinationCarrier = new JsonObject
                {
                    ["kind"] = "player_inventory",
                    ["ownerId"] = "player",
                    ["containerId"] = null,
                    ["containerPath"] = new JsonArray()
                };
                MortalItemIdentityState.AppendTransition(
                    entry,
                    MortalItemIdentityState.CreateTransition(
                        "transfer",
                        HeldTreatmentPipelineContext.Turn,
                        new[] { HeldTreatmentPipelineContext.SelectedItemId },
                        sourceCarrier,
                        destinationCarrier,
                        quantityBefore: 2,
                        quantityAfter: 2,
                        authorityKind: "accepted_turn_transfer",
                        authorityId: "t070b4_post_seal_carrier_drift"));
                entry["currentCarrier"] = destinationCarrier.DeepClone();
                var reparsed = MortalItemIdentityState.Parse(identity.Root);
                Assert.Empty(reparsed.Issues);
                await context.FileSystem.WriteFileAtomicAsync(
                    context.Lease,
                    MortalItemIdentityState.StatePath,
                    identity.Root.ToJsonString());
                break;
            }
            case "index":
            {
                var index = await ReadTreatmentItemIdentityAsync(context);
                var entry = index.EntriesByItemId[
                    HeldTreatmentPipelineContext.SelectedItemId];
                var carrier = Assert.IsType<JsonObject>(
                    entry["currentCarrier"]!.DeepClone());
                MortalItemIdentityState.AppendTransition(
                    entry,
                    MortalItemIdentityState.CreateTransition(
                        "consume",
                        HeldTreatmentPipelineContext.Turn,
                        new[] { HeldTreatmentPipelineContext.SelectedItemId },
                        carrier,
                        carrier.DeepClone().AsObject(),
                        quantityBefore: 2,
                        quantityAfter: 1,
                        authorityKind: "test_post_seal_drift",
                        authorityId: "t070b4_index_drift"));
                var reparsed = MortalItemIdentityState.Parse(index.Root);
                Assert.Empty(reparsed.Issues);
                await context.FileSystem.WriteFileAtomicAsync(
                    context.Lease,
                    MortalItemIdentityState.StatePath,
                    index.Root.ToJsonString());
                break;
            }
            case "resource_capacity":
            case "resource_current":
            {
                var state = await ReadJsonObjectAsync(
                    context,
                    ResourceMaterializationContract.StatePath);
                var durability = Assert.Single(
                    state["entries"]!.AsArray().OfType<JsonObject>(),
                    entry => string.Equals(
                                 entry["ownerKind"]?.GetValue<string>(),
                                 "item",
                                 StringComparison.Ordinal) &&
                             string.Equals(
                                 entry["resourceOwnerId"]?.GetValue<string>(),
                                 HeldTreatmentPipelineContext.SelectedItemId,
                                 StringComparison.Ordinal) &&
                             string.Equals(
                                 entry["resourceKey"]?.GetValue<string>(),
                                 "durability",
                                 StringComparison.Ordinal));
                durability[string.Equals(
                    driftAxis,
                    "resource_capacity",
                    StringComparison.Ordinal)
                        ? "maximum"
                        : "current"] = 7;
                if (string.Equals(
                        driftAxis,
                        "resource_capacity",
                        StringComparison.Ordinal))
                {
                    durability["maximum"] = 9;
                }
                await context.FileSystem.WriteFileAtomicAsync(
                    context.Lease,
                    ResourceMaterializationContract.StatePath,
                    state.ToJsonString());
                break;
            }
            case "shared_npc_mirror":
            {
                var npcCore = await ReadJsonObjectAsync(
                    context,
                    NpcCoreChangesContract.NpcCorePath);
                var provider = ReadProvider(npcCore);
                var skill = Assert.Single(
                    provider["activeSkills"]!.AsArray().OfType<JsonObject>());
                skill["displayName"] = "Drifted shared NPC skill/item root";
                await context.FileSystem.WriteFileAtomicAsync(
                    context.Lease,
                    NpcCoreChangesContract.NpcCorePath,
                    npcCore.ToJsonString());
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(driftAxis), driftAxis, null);
        }
    }

    private static async Task SeedHeldTreatmentAuthorityAsync(
        FileSystemManager fileSystem,
        JsonObject wound,
        HeldTreatmentItemScenario? itemScenario = null)
    {
        await fileSystem.WriteFileAtomicAsync(
            WoundCarrierCatalog.PlayerPath,
            WoundContractTestData.CreatePlayerCarrier(wound).ToJsonString());
        var emptyWoundSnapshotRoots = new Dictionary<string, JsonObject>(
            StringComparer.Ordinal)
        {
            [WoundCarrierCatalog.NpcPath] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            },
            [WoundCarrierCatalog.EnemiesPath] = new JsonObject
            {
                ["enemiesData"] = new JsonArray()
            },
            [WoundCarrierCatalog.AlliesPath] = new JsonObject
            {
                ["alliesData"] = new JsonArray()
            },
            [WoundCarrierCatalog.AfterlifeProfilesPath] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["profiles"] = new JsonArray()
            },
            [MortalWoundOccurrenceState.StatePath] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["occurrences"] = new JsonArray()
            },
            [MortalWoundOpportunityReceiptState.StatePath] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["nextOrdinal"] = 1,
                ["receipts"] = new JsonArray()
            }
        };
        foreach (var pair in emptyWoundSnapshotRoots)
        {
            await fileSystem.WriteFileAtomicAsync(
                pair.Key,
                pair.Value.ToJsonString());
        }
        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
        Assert.True(parsed.IsValid, DescribeValidationIssues(parsed.Issues));
        var fingerprint = WoundIdentityState.ComputeSemanticFingerprint(
            Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound));
        await fileSystem.WriteFileAtomicAsync(
            WoundIdentityState.StatePath,
            WoundContractTestData.CreateIdentityIndex(
                WoundContractTestData.CreateIdentityEntry(
                    semanticFingerprint: fingerprint)).ToJsonString());
        var createTransition = WoundContractTestData.CreateTransition();
        createTransition["beforeFingerprint"] =
            WoundHistoryState.ComputeNonexistentBeforeFingerprint(
                HeldTreatmentPipelineContext.WoundId);
        createTransition["afterFingerprint"] = fingerprint;
        createTransition["sourceFingerprint"] =
            WoundAcceptedTurnFingerprintWriter.Compute(
                new[] { "t070b3-held-treatment-fixture-create" });
        createTransition["attemptId"] = null;
        await fileSystem.WriteFileAtomicAsync(
            WoundHistoryState.HistoryPath,
            WoundContractTestData.CreateHistory(createTransition).ToJsonString());

        var provider = MortalActorTestFixtures.CreateActor(
            "field_medic_01",
            "loc_field_clinic_001",
            "Field clinic");
        provider["displayName"] = "Field medic";
        provider["activeSkills"] = new JsonArray(
            CreateGuaranteedTreatmentSkill());
        provider["passiveSkills"] = new JsonArray();
        JsonObject? selectedItem = null;
        JsonObject? companionItem = null;
        if (itemScenario is not null)
        {
            selectedItem = MortalItemTestFixture.CreateCanonicalRootAtTurn(
                HeldTreatmentPipelineContext.SelectedItemId,
                acceptedAtTurn: 41,
                route: "npc_acquisition",
                authorityKind: "npc_inventory_add",
                authorityId:
                    "npc_inventory_add:41:0:" + HeldTreatmentPipelineContext.ProviderId,
                name: "Sealed field dressing");
            selectedItem["quality"] = "Rare";
            selectedItem["rarity"] = "Rare";
            selectedItem["count"] = itemScenario.InitialCount;
            if (itemScenario.Companion == HeldTreatmentItemCompanion.Container)
            {
                selectedItem["isContainer"] = true;
                selectedItem["capacity"] = 10;
                selectedItem["materialization"]!["sections"]!["container"] =
                    new JsonObject
                    {
                        ["state"] = "populated",
                        ["reason"] = null
                    };
                companionItem = MortalItemTestFixture.CreateCanonicalRootAtTurn(
                    HeldTreatmentPipelineContext.CompanionChildItemId,
                    acceptedAtTurn: 41,
                    route: "npc_acquisition",
                    authorityKind: "npc_inventory_add",
                    authorityId:
                        "npc_inventory_add:41:1:" + HeldTreatmentPipelineContext.ProviderId,
                    name: "Sealed dressing insert");
                companionItem["quality"] = "Rare";
                companionItem["rarity"] = "Rare";
                companionItem["contentsPath"] = new JsonArray(
                    HeldTreatmentPipelineContext.SelectedItemId);
                MortalItemTestFixture.ResealCanonical(companionItem);
            }
            MortalItemTestFixture.ResealCanonical(selectedItem);
        }
        provider["inventory"] = selectedItem is null
            ? new JsonArray()
            : companionItem is null
                ? new JsonArray(selectedItem.DeepClone())
                : new JsonArray(selectedItem.DeepClone(), companionItem.DeepClone());
        await fileSystem.WriteFileAtomicAsync(
            NpcCoreChangesContract.NpcCorePath,
            new JsonObject
            {
                [GuardianPolicyContracts.NpcCoreSceneSectionName] =
                    new JsonArray(provider)
            }.ToJsonString());
        if (itemScenario?.PublishNpcSkillChange == true)
        {
            await fileSystem.WriteFileAtomicAsync(
                "game_state/npcs/npc_skills.json",
                new JsonObject
                {
                    ["NPCActiveSkillChanges"] = new JsonArray(new JsonObject
                    {
                        ["NPCId"] = HeldTreatmentPipelineContext.ProviderId,
                        ["skillChanges"] = new JsonArray(
                            CreateGuaranteedTreatmentSkill())
                    }),
                    ["NPCPassiveSkillChanges"] = new JsonArray(),
                    ["NPCSkillMasteryChanges"] = new JsonArray(new JsonObject
                    {
                        ["NPCId"] = HeldTreatmentPipelineContext.ProviderId,
                        ["skillName"] = "Guaranteed Care",
                        ["newMasteryLevel"] = 3,
                        ["newCurrentMasteryProgress"] = 0,
                        ["newMasteryProgressNeeded"] = 9
                    }),
                    ["NPCPassiveSkillMasteryChanges"] = new JsonArray()
                }.ToJsonString());
        }
        if (selectedItem is not null)
        {
            var currentIndex = JsonNode.Parse(
                    await fileSystem.ReadFileAsync(MortalItemIdentityState.StatePath) ??
                    MortalItemIdentityState.CreateEmptyRoot().ToJsonString())
                ?.AsObject() ?? MortalItemIdentityState.CreateEmptyRoot();
            var selectedIndex = MortalItemTestFixture.CreateIndexForCarrier(
                selectedItem,
                "npc_inventory",
                HeldTreatmentPipelineContext.ProviderId);
            currentIndex["entries"] ??= new JsonArray();
            currentIndex["entries"]!.AsArray().Add(
                Assert.Single(selectedIndex["entries"]!.AsArray())!.DeepClone());
            if (companionItem is not null)
            {
                var companionIndex = MortalItemTestFixture.CreateIndexForCarrier(
                    companionItem,
                    "npc_inventory",
                    HeldTreatmentPipelineContext.ProviderId,
                    containerPath: new JsonArray(
                        HeldTreatmentPipelineContext.SelectedItemId));
                currentIndex["entries"]!.AsArray().Add(
                    Assert.Single(companionIndex["entries"]!.AsArray())!.DeepClone());
            }
            var parsedItemIndex = MortalItemIdentityState.Parse(currentIndex);
            Assert.Empty(parsedItemIndex.Issues);
            await fileSystem.WriteFileAtomicAsync(
                MortalItemIdentityState.StatePath,
                currentIndex.ToJsonString());
            await fileSystem.WriteFileAtomicAsync(
                MortalItemAcceptedTransferCatalog.NpcCommandsPath,
                new JsonObject
                {
                    ["NPCInventoryAdds"] = new JsonArray(),
                    ["NPCInventoryUpdates"] = new JsonArray(),
                    ["NPCInventoryRemovals"] = new JsonArray(),
                    ["NPCEquipmentChanges"] = new JsonArray()
                }.ToJsonString());
        }
        await fileSystem.WriteFileAtomicAsync(
            "game_state/npcs/npc_journals.json",
            new JsonObject
            {
                ["npcJournals"] = new JsonArray()
            }.ToJsonString());

        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_field_clinic_001",
            "T070-B.3 field clinic");
        await fileSystem.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap(location).ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationTestFixture.CreateCurrentProjection(location).ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationTestFixture.CreateIdentityIndex(location).ToJsonString());
        var worldTimePath = "game_state/world/world_time.json";
        var worldTime = JsonNode.Parse(
                await fileSystem.ReadFileAsync(worldTimePath) ?? "{}")
            ?.AsObject() ?? new JsonObject();
        worldTime["currentTimeInMinutes"] = 1_260L;
        await fileSystem.WriteFileAtomicAsync(
            worldTimePath,
            worldTime.ToJsonString());

        var soulPath = "game_state/meta/soul_state.json";
        var soul = JsonNode.Parse(await fileSystem.ReadFileAsync(soulPath) ?? "{}")
            ?.AsObject() ?? new JsonObject();
        soul["currentRealm"] = "Mortal World";
        await fileSystem.WriteFileAtomicAsync(soulPath, soul.ToJsonString());

        var woundEffects = CreateCanonicalHeldTreatmentWoundEffects(wound);
        await fileSystem.WriteFileAtomicAsync(
            EffectCarrierCatalog.PlayerPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(
                    woundEffects.Select(static effect =>
                        (JsonNode)effect).ToArray())
            }.ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            EffectIdentityState.StatePath,
            EffectMaterializationTestFixture.CreateIdentityIndex(
                woundEffects.ToArray()).ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            "game_state/player/skills_active.json",
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray()
            }.ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            "game_state/player/skills_passive.json",
            new JsonObject
            {
                ["passiveSkillChanges"] = new JsonArray()
            }.ToJsonString());
        await fileSystem.WriteFileAtomicAsync(
            "game_state/player/skill_mastery.json",
            new JsonObject
            {
                ["skillMasteryChanges"] = new JsonArray()
            }.ToJsonString());
        await WriteCanonicalPlayerTreatmentResourceAuthorityAsync(
            fileSystem,
            currentEnergy: 2,
            itemResourceOwnerId: itemScenario is null
                ? null
                : HeldTreatmentPipelineContext.SelectedItemId);
    }

    private static async Task SeedMortalItemPublicationBaselineFixtureAsync(
        FileSystemManager fileSystem,
        MortalItemPublicationBaselineFixture fixture)
    {
        if (fixture.HasFlag(
                MortalItemPublicationBaselineFixture.ItemJournalTailDelta))
        {
            const string path = "game_state/npcs/item_journals.json";
            var root = JsonNode.Parse(await fileSystem.ReadFileAsync(path) ?? "{}")
                ?.AsObject() ?? new JsonObject();
            root["itemJournalUpdates"] = new JsonArray(new JsonObject
            {
                ["itemId"] = HeldTreatmentPipelineContext.SelectedItemId,
                ["itemName"] = "Sealed field dressing",
                ["entryToAppend"] = "Sealed ordinary tail marker"
            });
            await fileSystem.WriteFileAtomicAsync(path, root.ToJsonString());
        }

        if (fixture.HasFlag(
                MortalItemPublicationBaselineFixture.PassThroughRecipeObject))
        {
            await fileSystem.WriteFileAtomicAsync(
                "game_state/inventory/recipes.json",
                new JsonObject
                {
                    ["addOrUpdateRecipes"] = new JsonArray()
                }.ToJsonString());
        }

        if (fixture.HasFlag(
                MortalItemPublicationBaselineFixture.LegacyVehicleObject))
        {
            await fileSystem.WriteFileAtomicAsync(
                StorageTransportMoveService.VehiclesPath,
                new JsonObject
                {
                    ["vehicles"] = new JsonArray()
                }.ToJsonString());
        }

        if (fixture.HasFlag(
                MortalItemPublicationBaselineFixture.CanonicalNpcTradeReceipts))
        {
            var npcRoot = JsonNode.Parse(await fileSystem.ReadFileAsync(
                    NpcCoreChangesContract.NpcCorePath) ?? "{}")
                ?.AsObject() ?? new JsonObject();
            var provider = ReadProvider(npcRoot);
            provider["tradeInventoryReceipts"] = new JsonArray();
            await fileSystem.WriteFileAtomicAsync(
                NpcCoreChangesContract.NpcCorePath,
                npcRoot.ToJsonString());
        }
    }

    private static IReadOnlyList<JsonObject>
        CreateCanonicalHeldTreatmentWoundEffects(JsonObject woundRoot)
    {
        var parsed = WoundMaterializationContract.Parse(
            woundRoot.ToJsonString(),
            "t070b3.integration.woundEffectSeed");
        Assert.True(parsed.IsValid, DescribeValidationIssues(parsed.Issues));
        var wound = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        var definitions = wound.Consequences.OwnedEffectSources.Definitions
            .Select(static definition =>
                JsonNode.Parse(definition.GetRawText())!.AsObject())
            .ToDictionary(
                static definition =>
                    definition["definitionKey"]!.GetValue<string>(),
                StringComparer.Ordinal);
        var effects = new List<JsonObject>();
        foreach (var binding in wound.Consequences.OwnedEffectSources.RootBindings)
        {
            effects.Add(CreateCanonicalHeldTreatmentWoundEffect(
                wound,
                definitions[binding.DefinitionKey],
                binding.EffectId));
        }
        return effects;
    }

    private static JsonObject CreateCanonicalHeldTreatmentWoundEffect(
        WoundMaterializationEnvelope wound,
        JsonObject definition,
        string effectId)
    {
        var profile = definition["components"]![0]!["profile"]!
            .GetValue<string>();
        var targetKind = wound.Owner.OwnerKind switch
        {
            "player" => "player",
            "npc" => "npc",
            "combatant" => "combatant",
            _ => throw new ArgumentOutOfRangeException(
                nameof(wound),
                wound.Owner.OwnerKind,
                "Unsupported severity-reduction effect owner.")
        };
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            targetKind,
            profile);
        effect["effectId"] = effectId;
        effect["realm"] = wound.Owner.Realm;
        effect["target"] = new JsonObject
        {
            ["kind"] = targetKind,
            ["targetId"] = wound.Owner.OwnerId
        };
        effect["display"] = definition["display"]!.DeepClone();
        effect["source"] = new JsonObject
        {
            ["kind"] = "wound",
            ["sourceId"] = wound.WoundId,
            ["definitionKey"] = definition["definitionKey"]!.DeepClone()
        };
        effect["components"] = definition["components"]!.DeepClone();
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["linkKind"] = "wound",
            ["targetId"] = wound.WoundId,
            ["activePredicate"] =
                definition["lifetime"]!["activePredicate"]!.DeepClone(),
            ["onSourceLoss"] =
                definition["lifetime"]!["onSourceLoss"]!.DeepClone()
        };
        effect["stacking"] = new JsonObject
        {
            ["stackKey"] = definition["stacking"]!["stackKey"]!.DeepClone(),
            ["policy"] = definition["stacking"]!["policy"]!.DeepClone(),
            ["maxStacks"] =
                definition["stacking"]!["maxStacks"]!.DeepClone(),
            ["currentStacks"] = 1,
            ["refreshMode"] =
                definition["stacking"]!["refreshMode"]?.DeepClone(),
            ["mergeRule"] =
                definition["stacking"]!["mergeRule"]?.DeepClone()
        };
        effect["triggers"] = definition["triggers"]!.DeepClone();
        effect["removal"] = definition["removal"]!.DeepClone();
        effect["links"] = definition["links"]!.DeepClone();
        return effect;
    }

    private static JsonObject CreateGuaranteedResourceTreatmentWound(
        bool includeItemRequirement = false)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["consequences"]!["ownedEffectSources"]!["definitions"]!
            .AsArray().RemoveAt(1);
        wound["consequences"]!["ownedEffectSources"]!["rootBindings"]!
            .AsArray().RemoveAt(1);
        wound["consequences"]!["entries"]!.AsArray().RemoveAt(1);
        wound["consequences"]!["slotsUsed"] = 1;
        var requirements = new JsonArray(
            new JsonObject
            {
                ["kind"] = "source_capability",
                ["capabilityRef"] = "exact_materialized_healing_source",
                ["actorRole"] = "provider"
            },
            new JsonObject
            {
                ["kind"] = "resource_quantity",
                ["resourceRef"] = "energy",
                ["quantity"] = 2,
                ["ownerRole"] = "target"
            });
        var mutations = new JsonArray(new JsonObject
        {
            ["kind"] = "consume_requirement",
            ["scope"] = "common",
            ["milestoneOrdinal"] = null,
            ["requirementIndex"] = 1
        });
        if (includeItemRequirement)
        {
            requirements.Add(new JsonObject
            {
                ["kind"] = "item_quantity",
                ["itemRef"] = HeldTreatmentPipelineContext.SelectedItemId,
                ["quantity"] = 1,
                ["ownerRole"] = "provider"
            });
            mutations.Add(new JsonObject
            {
                ["kind"] = "consume_requirement",
                ["scope"] = "common",
                ["milestoneOrdinal"] = null,
                ["requirementIndex"] = 2
            });
        }
        var route = new JsonObject
        {
            ["routeId"] = "guaranteed_t070b3_resource",
            ["displayName"] = "Guaranteed resource stabilization",
            ["visibility"] = "known_to_player",
            ["mode"] = "guaranteed",
            ["requirements"] = requirements,
            ["resourcePolicy"] = new JsonObject
            {
                ["reserveBeforeResolution"] = true,
                ["consumeOn"] = new JsonArray("success"),
                ["refundOn"] = new JsonArray(
                    "cancelled",
                    "validation_failed",
                    "rolled_back"),
                ["mutations"] = mutations
            },
            ["resolution"] = new JsonObject
            {
                ["capabilityRef"] = "exact_materialized_healing_source",
                ["actorRole"] = "provider"
            },
            ["outcomes"] = new JsonArray(new JsonObject
            {
                ["category"] = "success",
                ["result"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "stabilize"
                })
            }),
            ["interruption"] = null
        };
        wound["treatment"]!["routes"] = new JsonArray(route);
        wound["treatment"]!["knownRouteIds"] = new JsonArray(
            "guaranteed_t070b3_resource");
        return wound;
    }

    private static JsonObject CreateGuaranteedTreatmentSkill() => new()
    {
        ["skillId"] = "skill_guaranteed_care_t070b3",
        ["displayName"] = "Guaranteed Care",
        ["currentMasteryLevel"] = 3,
        ["skillName"] = "Guaranteed Care",
        ["skillDescription"] = "Provides exact materialized healing authority.",
        ["rarity"] = "Common",
        ["actionCost"] = "Main",
        ["combatEffect"] = new JsonObject
        {
            ["isActivatedEffect"] = true,
            ["actionName"] = "Field treatment",
            ["effects"] = new JsonArray(new JsonObject
            {
                ["effectType"] = "Damage",
                ["value"] = "10%",
                ["targetType"] = "Enemy",
                ["effectDescription"] = "A controlled intervention.",
                ["poiseDamage"] = "5%"
            })
        },
        ["mortalWoundTreatmentCapabilities"] = new JsonArray(new JsonObject
        {
            ["schemaVersion"] = 1,
            ["capabilityRef"] = "exact_materialized_healing_source",
            ["woundDomain"] = "physical",
            ["minimumSeverityRank"] = 1,
            ["maximumSeverityRank"] = 4,
            ["operationLimits"] = new JsonObject
            {
                ["mayStabilize"] = true,
                ["maximumRecoveryPoints"] = 2,
                ["maximumSeverityReductionSteps"] = 1,
                ["removableComplicationKinds"] = new JsonArray("infection"),
                ["mayHealAtSeverityI"] = true,
                ["maximumCosmeticHealLegacies"] = 1,
                ["maximumMechanicalEffectHealLegacies"] = 4
            }
        })
    };

    private static async Task WriteCanonicalPlayerTreatmentResourceAuthorityAsync(
        FileSystemManager fileSystem,
        int currentEnergy,
        string? itemResourceOwnerId = null)
    {
        const string initializeFingerprint =
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string spendFingerprint =
            "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string healthInitializeFingerprint =
            "sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        const string itemInitializeFingerprint =
            "sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("energy", out var definition));
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Player,
            "player_current",
            "energy");
        var binding = new ResourceCapacityBinding(
            definition!.CapacityPolicy.Kind,
            definition.CapacityPolicy.FormulaKey!,
            initializeFingerprint);
        var initialized = new ResourceStateSnapshot(
            10,
            10,
            binding,
            ResourceLifecycleState.Active);
        var initialize = new ResourceTransition(
            "transition_energy_initialize_t070b3",
            "operation_energy_initialize_t070b3",
            "turn_1:resource:t070b3",
            "bootstrap_materialization",
            "mortal_incarnation_1",
            ResourceMutationPhase.RegisteredSystemOutcome,
            40,
            0,
            coordinate,
            ResourceTransitionOperation.Initialize,
            0,
            0,
            ResourceTransitionOutcome.Applied,
            ResourceCapacityDisposition.InitializeFromDefinition,
            null,
            initialized,
            new ResourceSourceEvidence(
                "bootstrap_materialization",
                "mortal_incarnation_1",
                initializeFingerprint),
            initializeFingerprint,
            null,
            1);
        var spent = new ResourceTransition(
            "transition_energy_spend_t070b3",
            "operation_energy_spend_t070b3",
            "turn_2:resource:t070b3",
            "action_cost",
            "wound_fixture_energy_t070b3",
            ResourceMutationPhase.DirectOutcome,
            100,
            0,
            coordinate,
            ResourceTransitionOperation.Spend,
            10 - currentEnergy,
            10 - currentEnergy,
            ResourceTransitionOutcome.Applied,
            null,
            initialized,
            initialized with { Current = currentEnergy },
            new ResourceSourceEvidence(
                "action_cost",
                "wound_fixture_energy_t070b3",
                spendFingerprint),
            spendFingerprint,
            null,
            2);
        Assert.True(definitions.TryResolveExact("health", out var healthDefinition));
        var healthCoordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Player,
            "player_current",
            "health");
        var healthBinding = new ResourceCapacityBinding(
            healthDefinition!.CapacityPolicy.Kind,
            healthDefinition.CapacityPolicy.FormulaKey!,
            healthInitializeFingerprint);
        var healthInitialized = new ResourceStateSnapshot(
            10,
            10,
            healthBinding,
            ResourceLifecycleState.Active);
        var healthInitialize = new ResourceTransition(
            "transition_health_initialize_t070b3",
            "operation_health_initialize_t070b3",
            "turn_1:resource:health_t070b3",
            "bootstrap_materialization",
            "mortal_incarnation_1",
            ResourceMutationPhase.RegisteredSystemOutcome,
            40,
            1,
            healthCoordinate,
            ResourceTransitionOperation.Initialize,
            0,
            0,
            ResourceTransitionOutcome.Applied,
            ResourceCapacityDisposition.InitializeFromDefinition,
            null,
            healthInitialized,
            new ResourceSourceEvidence(
                "bootstrap_materialization",
                "mortal_incarnation_1",
                healthInitializeFingerprint),
            healthInitializeFingerprint,
            null,
            1);
        var transitions = new List<ResourceTransition>
        {
            initialize,
            spent,
            healthInitialize
        };
        var entries = new List<ResourceStateEntry>
        {
            new(
                coordinate,
                currentEnergy,
                10,
                binding,
                ResourceLifecycleState.Active,
                new ResourceChronology(
                    1,
                    initialize.EventRef,
                    spent.TransitionId,
                    spent.EventRef,
                    spent.Turn)),
            new(
                healthCoordinate,
                10,
                10,
                healthBinding,
                ResourceLifecycleState.Active,
                new ResourceChronology(
                    1,
                    healthInitialize.EventRef,
                    healthInitialize.TransitionId,
                    healthInitialize.EventRef,
                    healthInitialize.Turn))
        };
        if (itemResourceOwnerId is not null)
        {
            Assert.True(definitions.TryResolveExact(
                "durability",
                out var durabilityDefinition));
            Assert.Equal(
                ResourceCapacityKind.InstanceFixed,
                durabilityDefinition!.CapacityPolicy.Kind);
            var durabilityCoordinate = new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Item,
                itemResourceOwnerId,
                "durability");
            var durabilityBinding = new ResourceCapacityBinding(
                ResourceCapacityKind.InstanceFixed,
                itemResourceOwnerId,
                itemInitializeFingerprint);
            var durabilityInitialized = new ResourceStateSnapshot(
                8,
                8,
                durabilityBinding,
                ResourceLifecycleState.Active);
            var durabilityInitialize = new ResourceTransition(
                "transition_selected_dressing_durability_initialize",
                "operation_selected_dressing_durability_initialize",
                "turn_3:resource:selected_dressing:durability",
                "bootstrap_materialization",
                "selected_dressing_durability_fixture",
                ResourceMutationPhase.RegisteredSystemOutcome,
                40,
                2,
                durabilityCoordinate,
                ResourceTransitionOperation.Initialize,
                0,
                0,
                ResourceTransitionOutcome.Applied,
                ResourceCapacityDisposition.InitializeFromDefinition,
                null,
                durabilityInitialized,
                new ResourceSourceEvidence(
                    "bootstrap_materialization",
                    "selected_dressing_durability_fixture",
                    itemInitializeFingerprint),
                itemInitializeFingerprint,
                null,
                3);
            transitions.Add(durabilityInitialize);
            entries.Add(new ResourceStateEntry(
                durabilityCoordinate,
                8,
                8,
                durabilityBinding,
                ResourceLifecycleState.Active,
                new ResourceChronology(
                    3,
                    durabilityInitialize.EventRef,
                    durabilityInitialize.TransitionId,
                    durabilityInitialize.EventRef,
                    durabilityInitialize.Turn)));
        }
        var historyResult = ResourceHistoryState.CreateValidated(
            transitions,
            definitions);
        Assert.True(
            historyResult.IsValid,
            DescribeValidationIssues(historyResult.Issues));
        var state = new ResourceStateLedger(entries);
        Assert.Empty(historyResult.History!.ValidateStateAgreement(state));
        await fileSystem.WriteFileAtomicAsync(
            ResourceMaterializationContract.DefinitionsPath,
            definitions.ToCanonicalJson());
        await fileSystem.WriteFileAtomicAsync(
            ResourceMaterializationContract.StatePath,
            state.ToCanonicalJson());
        await fileSystem.WriteFileAtomicAsync(
            ResourceMaterializationContract.HistoryPath,
            historyResult.History.ToCanonicalJson());
        var composed = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            definitions,
            fileSystem.ReadFileAsync,
            state,
            historyResult.History,
            CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap);
        Assert.True(composed.IsValid, DescribeValidationIssues(composed.Issues));
        await fileSystem.WriteFileAtomicAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            Assert.IsType<string>(composed.CanonicalAuthorityJson));
    }

    private static async Task AddGuardianCleanupCommandSurfaceAsync(
        FileSystemManager fileSystem)
    {
        const string path = "game_state/meta/guardians.json";
        var root = JsonNode.Parse(await fileSystem.ReadFileAsync(path) ?? "{}")
            ?.AsObject() ?? new JsonObject();
        root[GuardianProjectState.QuestProgressUpdatesProperty] = new JsonArray();
        await fileSystem.WriteFileAtomicAsync(path, root.ToJsonString());
    }

    private static string DescribeValidationIssues(
        IEnumerable<ValidationIssue> issues) => string.Join(
        " | ",
        issues.Select(static issue =>
            $"{issue.Code}@{issue.FilePath}: {issue.Actual}"));

    private static void DeleteHeldTreatmentRootBestEffort(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        var tempPrefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullRoot.StartsWith(tempPrefix, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullRoot).StartsWith(
                "boe-held-treatment-pipeline-",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to remove unexpected held-treatment test root '{fullRoot}'.");
        }
        try
        {
            if (Directory.Exists(fullRoot))
                Directory.Delete(fullRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record ExactFileImage(bool Exists, byte[]? Bytes);

    [Flags]
    private enum MortalItemPublicationBaselineFixture
    {
        None = 0,
        ItemJournalTailDelta = 1,
        PassThroughRecipeObject = 2,
        LegacyVehicleObject = 4,
        CanonicalNpcTradeReceipts = 8
    }

    private enum HeldTreatmentItemCompanion
    {
        None,
        Container
    }

    private sealed record HeldTreatmentItemScenario(
        int InitialCount,
        HeldTreatmentItemCompanion Companion,
        bool PublishNpcSkillChange,
        bool CreateUnrelatedSameTurnItem)
    {
        internal static HeldTreatmentItemScenario SelectedStack(
            int initialCount = 2,
            HeldTreatmentItemCompanion companion = HeldTreatmentItemCompanion.None,
            bool publishNpcSkillChange = false,
            bool createUnrelatedSameTurnItem = false) => new(
            initialCount,
            companion,
            publishNpcSkillChange,
            createUnrelatedSameTurnItem);

        internal GameResponse CreateTreatmentProposal()
        {
            var proposal = new GameResponse();
            if (PublishNpcSkillChange)
            {
                var skill = CreateGuaranteedTreatmentSkill().DeepClone().AsObject();
                skill["displayName"] =
                    HeldTreatmentPipelineContext.ChangedSkillDisplayName;
                proposal.NPCActiveSkillChanges = new[]
                {
                    JsonSerializer.SerializeToElement(new JsonObject
                    {
                        ["npcId"] = HeldTreatmentPipelineContext.ProviderId,
                        ["skillChanges"] = new JsonArray(skill)
                    })
                };
            }
            if (CreateUnrelatedSameTurnItem)
            {
                var item = MortalItemTestFixture.CreateRawRoot(
                    route: "player_acquisition",
                    authorityKind: "turn_outcome",
                    authorityId: "turn_42",
                    sourceTurn: HeldTreatmentPipelineContext.Turn,
                    creationRef: HeldTreatmentPipelineContext.UnrelatedCreationRef,
                    materializationId:
                        HeldTreatmentPipelineContext.UnrelatedMaterializationId);
                item["name"] = "Unrelated same-turn suture case";
                item["quality"] = "Rare";
                item["rarity"] = "Rare";
                proposal.UpdateInventory = new[]
                {
                    JsonSerializer.SerializeToElement(item)
                };
            }
            Assert.Null(proposal.Response);
            return proposal;
        }
    }

    private sealed record HeldTreatmentAuthorities(
        MortalWoundTreatmentAcceptedStateAuthority AcceptedState,
        MortalWoundTreatmentAttemptRequest Request,
        MortalWoundTreatmentResolution Resolution);

    private sealed record SourceMethodAnalysis(
        MethodDeclarationSyntax Method,
        SemanticModel Model);

    private sealed record CarrierOwnershipScope(
        StatementSyntax Statement,
        SyntaxNode Lifetime,
        ILocalSymbol RefreshResultSymbol,
        ILocalSymbol CarrierSymbol,
        string CarrierPropertyName);

    private static readonly Lazy<IReadOnlyList<MetadataReference>>
        SourceGuardMetadataReferences = new(CreateSourceGuardMetadataReferences);

    private static IReadOnlyList<MetadataReference>
        CreateSourceGuardMetadataReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trustedPlatformAssemblies = AppContext.GetData(
            "TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            foreach (var path in trustedPlatformAssemblies.Split(
                         Path.PathSeparator,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                paths.Add(path);
            }
        }
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic ||
                assembly == typeof(GameEngineTurnLifecycleTests).Assembly ||
                string.IsNullOrWhiteSpace(assembly.Location))
                continue;
            paths.Add(assembly.Location);
        }
        paths.Add(typeof(GameEngine).Assembly.Location);
        return paths
            .Where(File.Exists)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    private sealed class HeldTreatmentPipelineContext : IAsyncDisposable
    {
        private FileSystemManager.CanonicalWriteLease? _lease;
        private readonly FileSystemManagerHooks? _hooks;

        internal HeldTreatmentPipelineContext(
            string root,
            FileSystemManager fileSystem,
            MortalWoundTreatmentAuthority.Context treatmentContext,
            FileSystemManagerHooks? hooks,
            HeldTreatmentItemScenario? itemScenario)
        {
            Root = root;
            FileSystem = fileSystem;
            TreatmentContext = treatmentContext;
            _hooks = hooks;
            ItemScenario = itemScenario;
        }

        internal const int Turn = 42;
        internal const string WoundId = "wound_test_torn_side";
        internal const string RouteId = "guaranteed_t070b3_resource";
        internal const string OperationKey = "operation_t070b3_game_engine";
        internal const string FinalSceneText =
            "The exact treatment stabilizes the wound without losing its held resource.";
        internal const string ProviderId = "field_medic_01";
        internal const string SelectedItemId = "itm_t070b4_selected_dressing";
        internal const string CompanionChildItemId =
            "itm_t070b4_selected_dressing_contents";
        internal const string ChangedSkillDisplayName =
            "Guaranteed Care — publication marker";
        internal const string UnrelatedCreationRef =
            "new_item_t070b4_unrelated_same_turn";
        internal const string UnrelatedMaterializationId =
            "mat_item_t070b4_unrelated_same_turn";

        internal string Root { get; }
        internal FileSystemManager FileSystem { get; private set; }
        internal MortalWoundTreatmentAuthority.Context TreatmentContext { get; }
        internal HeldTreatmentItemScenario? ItemScenario { get; }
        internal FileSystemManager.CanonicalWriteLease Lease =>
            _lease ?? throw new InvalidOperationException(
                "The held-treatment fixture lease is not active.");
        internal MortalWoundTreatmentAcceptedStateAuthority AcceptedState { get; private set; } = null!;
        internal MortalWoundTreatmentAttemptRequest Request { get; private set; } = null!;
        internal MortalWoundTreatmentResolution Resolution { get; private set; } = null!;
        internal AcceptedMechanicsPlan Plan { get; private set; } = null!;
        internal AcceptedMechanicsPlanBinding OriginalBinding { get; private set; } = null!;
        internal string OriginalSessionGeneration { get; private set; } = string.Empty;
        internal long OriginalSessionGenerationRevision { get; private set; } = -1;

        internal GameResponse CreateMechanicsOnlyTreatmentProposal()
        {
            var proposal = ItemScenario?.CreateTreatmentProposal() ??
                           new GameResponse();
            Assert.Null(proposal.Response);
            return proposal;
        }

        internal void SetResolvedAuthorities(
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            MortalWoundTreatmentAttemptRequest request,
            MortalWoundTreatmentResolution resolution)
        {
            AcceptedState = acceptedState;
            Request = request;
            Resolution = resolution;
        }

        internal void SetCurrentAuthorities(
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            MortalWoundTreatmentAttemptRequest request,
            MortalWoundTreatmentResolution resolution,
            AcceptedMechanicsPlan plan,
            AcceptedMechanicsPlanBinding binding)
        {
            SetResolvedAuthorities(acceptedState, request, resolution);
            Plan = plan;
            if (OriginalBinding is null)
            {
                OriginalBinding = binding;
                OriginalSessionGeneration = FileSystem.GetOrCreateSessionGeneration(Lease);
                OriginalSessionGenerationRevision = FileSystem
                    .CanonicalRootAuthorityIdentity
                    .SessionGenerationRevision;
            }
        }

        internal async Task AcquireLeaseAsync()
        {
            Assert.Null(_lease);
            _lease = await FileSystem.AcquireCanonicalWriteLeaseAsync();
        }

        internal async Task ReleaseLeaseAsync()
        {
            if (_lease is null)
                return;
            await _lease.DisposeAsync();
            _lease = null;
        }

        internal Task<byte[]?> ReadFileBytesAsync(string path) =>
            _lease is null
                ? FileSystem.ReadFileBytesAsync(path)
                : FileSystem.ReadFileBytesAsync(_lease, path);

        internal async Task RestartAsync(FileSystemManagerHooks? hooks)
        {
            await ReleaseLeaseAsync();
            FileSystem = new FileSystemManager(
                Root,
                NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                hooks ?? _hooks);
            FileSystem.EnsureDirectoryStructure();
            await AcquireLeaseAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await ReleaseLeaseAsync();
            DeleteHeldTreatmentRootBestEffort(Root);
        }
    }

    private sealed class MortalItemFinalBaselineFault
    {
        private const string ItemJournalPath =
            "game_state/npcs/item_journals.json";
        private const string RecipePath =
            "game_state/inventory/recipes.json";
        private readonly string _axis;
        private bool _armed;
        private HeldTreatmentPipelineContext? _context;
        private MortalItemCanonicalProjectionResult? _itemPhase;
        private MortalItemPublicationBaselineResult? _finalBaseline;

        internal MortalItemFinalBaselineFault(string axis)
        {
            _axis = axis;
            DriftPath = axis switch
            {
                "observe_item_phase" => ItemJournalPath,
                "pass_through_presence" or "pass_through_content" => RecipePath,
                "identity_index" => MortalItemIdentityState.StatePath,
                "vehicle_topology" => StorageTransportMoveService.VehiclesPath,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(axis),
                    axis,
                    null)
            };
            Hooks = new FileSystemManagerHooks
            {
                AfterCanonicalReadInitialValidationAsync = OnCanonicalReadAsync,
                BeforeCanonicalMutationAsync = OnCanonicalMutationAsync
            };
        }

        internal FileSystemManagerHooks Hooks { get; }
        internal string DriftPath { get; }
        internal bool Fired { get; private set; }
        internal bool ItemPhaseDiffersFromFinalBaseline { get; private set; }
        internal bool LiveItemPhaseMatchedBeforeTail { get; private set; }
        internal bool FinalBaselineMatchedBeforeDrift { get; private set; }
        internal bool CommonPublicationMutationObserved { get; private set; }

        internal void Arm(HeldTreatmentPipelineContext context)
        {
            _context = context;
            var authority = Assert.IsType<
                MortalWoundTreatmentItemPublicationAuthority>(
                context.Plan.TreatmentResourcePublicationAuthority?
                    .ItemPublicationAuthority);
            _itemPhase = authority.ItemPhase;
            _finalBaseline = authority.Baseline;
            _armed = true;
        }

        internal void Disarm() => _armed = false;

        private Task OnCanonicalReadAsync(string path)
        {
            if (!_armed || Fired)
                return Task.CompletedTask;

            if (string.Equals(_axis, "observe_item_phase", StringComparison.Ordinal))
            {
                if (!string.Equals(
                        path,
                        "game_state/meta/guardians.json",
                        StringComparison.Ordinal) ||
                    !StackContains("NormalizeGuardiansAsync"))
                {
                    return Task.CompletedTask;
                }

                var itemPhase = Assert.IsType<MortalItemCanonicalProjectionResult>(
                    _itemPhase);
                var finalBaseline =
                    Assert.IsType<MortalItemPublicationBaselineResult>(
                        _finalBaseline);
                ItemPhaseDiffersFromFinalBaseline = !JsonNode.DeepEquals(
                    itemPhase.ItemPhaseAfterImages[DriftPath],
                    finalBaseline.FinalCarrierRoots[DriftPath]);
                LiveItemPhaseMatchedBeforeTail = JsonNode.DeepEquals(
                    itemPhase.ItemPhaseAfterImages[DriftPath],
                    ReadPhysicalProjectionRoot(DriftPath));
                Fired = true;
                return Task.CompletedTask;
            }

            if (!string.Equals(
                    path,
                    "game_state/player/skills_active.json",
                    StringComparison.Ordinal) ||
                !StackContains("NormalizePlayerSkillStateAsync"))
            {
                return Task.CompletedTask;
            }

            AssertFinalBaselineMatchesLive();
            FinalBaselineMatchedBeforeDrift = true;
            ApplyDrift();
            Fired = true;
            return Task.CompletedTask;
        }

        private Task OnCanonicalMutationAsync(string path)
        {
            if (_armed &&
                StackContains("PublishAcceptedMechanicsAsync"))
            {
                CommonPublicationMutationObserved = true;
            }
            return Task.CompletedTask;
        }

        private void AssertFinalBaselineMatchesLive()
        {
            var baseline = Assert.IsType<MortalItemPublicationBaselineResult>(
                _finalBaseline);
            Assert.Equal(
                MortalItemCanonicalProjectionPlanner.ProjectionRootPaths.Count,
                baseline.FinalCarrierRoots.Count);
            foreach (var path in
                     MortalItemCanonicalProjectionPlanner.ProjectionRootPaths)
            {
                Assert.True(baseline.FinalCarrierRoots.ContainsKey(path));
                var actual = ReadPhysicalProjectionRoot(path);
                Assert.True(
                    JsonNode.DeepEquals(
                        baseline.FinalCarrierRoots[path],
                        actual),
                    $"Expected the sealed final baseline at '{path}' before drift. " +
                    $"Expected={baseline.FinalCarrierRoots[path]?.ToJsonString() ?? "<missing>"}; " +
                    $"Actual={actual?.ToJsonString() ?? "<missing>"}.");
            }
        }

        private JsonNode? ReadPhysicalProjectionRoot(string path)
        {
            var context = Assert.IsType<HeldTreatmentPipelineContext>(_context);
            var fullPath = context.FileSystem.ResolvePath(path);
            if (!File.Exists(fullPath))
                return null;
            var json = new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true)
                .GetString(File.ReadAllBytes(fullPath))
                .TrimStart('\uFEFF');
            var parsed = MortalItemProjectionRootParser.Parse(json, path);
            Assert.True(parsed.IsValid, DescribeValidationIssues(parsed.Issues));
            return parsed.Root;
        }

        private void ApplyDrift()
        {
            var baseline = Assert.IsType<MortalItemPublicationBaselineResult>(
                _finalBaseline);
            var expected = baseline.FinalCarrierRoots[DriftPath];
            JsonNode drifted = _axis switch
            {
                "pass_through_presence" when expected is null => new JsonObject
                {
                    ["recipes"] = new JsonArray()
                },
                "pass_through_content" when expected is JsonObject obj =>
                    AddFaultMarker(obj),
                "identity_index" when expected is JsonObject obj =>
                    AddFaultMarker(obj),
                "vehicle_topology" when expected is JsonObject obj &&
                                                obj["vehicles"] is JsonArray vehicles =>
                    vehicles.DeepClone(),
                _ => throw new InvalidOperationException(
                    $"The '{_axis}' baseline fixture has unexpected topology.")
            };

            var context = Assert.IsType<HeldTreatmentPipelineContext>(_context);
            var fullPath = context.FileSystem.ResolvePath(DriftPath);
            var parentPath = Path.GetDirectoryName(fullPath);
            Assert.NotNull(parentPath);
            Assert.True(Directory.Exists(parentPath));
            File.WriteAllText(
                fullPath,
                drifted.ToJsonString(),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static JsonObject AddFaultMarker(JsonObject source)
        {
            var result = source.DeepClone().AsObject();
            result["postTailFaultMarker"] = true;
            return result;
        }

        private static bool StackContains(string marker) =>
            new StackTrace().GetFrames().Any(frame =>
            {
                var method = frame.GetMethod();
                return method?.Name.Contains(marker, StringComparison.Ordinal) == true ||
                       method?.DeclaringType?.FullName?.Contains(
                           marker,
                           StringComparison.Ordinal) == true;
            });
    }

    private static int ReadTreatmentRollbackCount(object? snapshot, string property) =>
        snapshot?.GetType().GetProperty(property)?.GetValue(snapshot) is System.Collections.ICollection collection
            ? collection.Count : (snapshot?.GetType().GetProperty(property)?.GetValue(snapshot) is IEnumerable<string> items ? items.Count() : 0);

    private void WriteTreatmentNeighborDiagnostic(HeldTreatmentPipelineContext context,
        AcceptedTreatmentPipelineFault fault, string stage, Exception? exception, object? disposition,
        byte[] commandBefore, object? rollbackSnapshot = null)
    {
        var commandPath = context.FileSystem.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath);
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new
        {
            TreatmentNeighbor = stage, fault.Fired, fault.ObservedPhases, fault.MatchedHealthRefreshes,
            fault.MutationPaths, fault.LeaseStacks, fault.FaultMutationImages, fault.HealthVisits,
            fault.PublishedTreatmentMembers, OriginalReceiptCaptured = fault.OriginalReceipt is not null,
            Failure = exception?.ToString(), Disposition = disposition?.ToString(),
            CommandBefore = Convert.ToBase64String(commandBefore),
            CommandAfter = File.Exists(commandPath) ? Convert.ToBase64String(File.ReadAllBytes(commandPath)) : null,
            RollbackPresent = rollbackSnapshot is not null,
            RollbackBackups = ReadTreatmentRollbackCount(rollbackSnapshot, "BackupFiles"),
            RollbackBaseline = ReadTreatmentRollbackCount(rollbackSnapshot, "BaselineFiles"),
            ForeignPlanCaptured = fault.ForeignPlan is not null, fault.ForeignBindingFingerprint
        }));
    }

    private sealed class AcceptedTreatmentPipelineFault
    {
        private readonly string _boundary;
        private readonly int _targetOccurrence;
        private readonly HashSet<string> _observedPhases = new(StringComparer.Ordinal);
        private int _matchingOccurrences;
        private int _acceptedPipelineHealthRefreshes;
        private int _compoundFailureStage;
        private bool _armed;
        private HeldTreatmentPipelineContext? _context;
        private object? _registryState;
        private byte[]? _originalCommand;
        private byte[]? _originalPending;

        internal AcceptedTreatmentPipelineFault(
            string boundary,
            int targetOccurrence)
        {
            _boundary = boundary;
            _targetOccurrence = targetOccurrence;
            Hooks = new FileSystemManagerHooks
            {
                BeforeCanonicalWriteLockOpenAsync =
                    OnBeforeCanonicalWriteLockOpenAsync,
                AfterCanonicalReadInitialValidationAsync = OnCanonicalReadAsync,
                BeforeCanonicalMutationAsync = OnCanonicalMutationAsync,
                LocalPublicationObserver = OnPublished
            };
        }

        internal FileSystemManagerHooks Hooks { get; }
        internal bool Fired { get; private set; }
        internal AcceptedMechanicsPlan? ForeignPlan { get; private set; }
        internal string? ForeignBindingFingerprint { get; private set; }
        internal IReadOnlyCollection<string> ObservedPhases => _observedPhases;
        internal List<string> MutationPaths { get; } = new();
        internal List<string> LeaseStacks { get; } = new();
        internal List<object> FaultMutationImages { get; } = new();
        internal int MatchedHealthRefreshes => _acceptedPipelineHealthRefreshes;
        internal int PublishedTreatmentMembers { get; private set; }
        internal MortalWoundTreatmentPublicationTakeReceipt? OriginalReceipt { get; private set; }
        internal List<object> HealthVisits { get; } = new();

        internal void CaptureOriginalAuthority(HeldTreatmentPipelineContext context)
        {
            _context = context;
            _registryState = typeof(AcceptedTurnAuthorityRegistry).GetMethod("GetState",
                BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [context.FileSystem, context.Lease]);
        }
        private MortalWoundTreatmentPublicationTakeReceipt? OpenReceipt =>
            _registryState?.GetType().GetField("_openTreatmentPublicationReceipt",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_registryState)
                as MortalWoundTreatmentPublicationTakeReceipt;
        private static bool BytesEqual(byte[]? left, byte[]? right) =>
            left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
        private bool ObserveOriginalReceipt()
        {
            var receipt = OpenReceipt;
            if (receipt is null) return false;
            OriginalReceipt ??= receipt;
            Assert.Same(OriginalReceipt, receipt);
            Assert.Same(_context!.Plan, receipt.Plan);
            return true;
        }
        private void OnPublished(TrustedLocalPublicationPhase phase, int index)
        {
            if (!_armed || _boundary is not ("full_state_validation" or "cleanup" or "wound_output_compensation_restore_failure") ||
                phase != TrustedLocalPublicationPhase.MemberPublished || !ObserveOriginalReceipt()) return;
            var journal = Path.Combine(_context!.FileSystem.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
            using var document = ReadJournalMetadata(File.ReadAllBytes(journal));
            var path = document.RootElement.GetProperty("Members")[index].GetProperty("Path").GetString();
            if (new[] { ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                WoundCarrierCatalog.PlayerPath, WoundHistoryState.HistoryPath }.Any(relative =>
                    _context.FileSystem.ResolvePath(relative) == path)) PublishedTreatmentMembers++;
        }

        internal void Arm(HeldTreatmentPipelineContext? context = null)
        {
            _context = context;
            if (context is not null)
            {
                _originalCommand = ReadOptionalTreatmentMember(context.FileSystem.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath));
                _originalPending = ReadOptionalTreatmentMember(context.FileSystem.ResolvePath(WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
            }
            _armed = true;
        }

        private Task OnCanonicalReadAsync(string path)
        {
            if (!_armed)
                return Task.CompletedTask;
            if (string.Equals(
                    _boundary,
                    "wound_output_compensation_restore_failure",
                    StringComparison.Ordinal))
            {
                if (_compoundFailureStage != 0 ||
                    !string.Equals(
                        ClassifyCurrentPhase(path, isMutation: false),
                        "wound_output",
                        StringComparison.Ordinal))
                {
                    return Task.CompletedTask;
                }

                _observedPhases.Add("wound_output");
                _compoundFailureStage = 1;
                return Task.FromException(new IOException(
                    $"Injected held-treatment pipeline failure at 'wound_output' ({path})."));
            }
            if (Fired)
                return Task.CompletedTask;
            if (_boundary is "runtime_refresh" or
                "full_state_validation" or
                "transaction_commit_conflict")
            {
                return Task.CompletedTask;
            }
            var phase = ClassifyCurrentPhase(path, isMutation: false);
            if (phase is null)
                return Task.CompletedTask;
            _observedPhases.Add(phase);
            if (!string.Equals(phase, _boundary, StringComparison.Ordinal))
                return Task.CompletedTask;
            _matchingOccurrences++;
            if (_matchingOccurrences != _targetOccurrence)
                return Task.CompletedTask;
            Fired = true;
            return Task.FromException(new IOException(
                $"Injected held-treatment pipeline failure at '{phase}' ({path})."));
        }

        private Task OnBeforeCanonicalWriteLockOpenAsync()
        {
            if (!_armed || Fired)
                return Task.CompletedTask;
            if (LeaseStacks.Count < 8) LeaseStacks.Add(new StackTrace().ToString());

            if (string.Equals(
                    _boundary,
                    "runtime_refresh",
                    StringComparison.Ordinal) &&
                StackContains("RefreshRuntimeStateAsync") &&
                StackContains("RefreshGameStateAsync"))
            {
                return FireCanonicalWriteLockFailure("runtime_refresh");
            }

            if (string.Equals(
                    _boundary,
                    "full_state_validation",
                    StringComparison.Ordinal) &&
                StackContains("EnsureClientOwnedSystemFilesHealthyCoreAsync") &&
                StackContains("RefreshGameStateAsync"))
            {
                _acceptedPipelineHealthRefreshes++;
                HealthVisits.Add(new { Visit = _acceptedPipelineHealthRefreshes,
                    ReceiptOpen = OpenReceipt is not null, PublishedTreatmentMembers,
                    Stack = new StackTrace().ToString() });
                if (_acceptedPipelineHealthRefreshes == 3)
                {
                    Assert.True(PublishedTreatmentMembers > 0);
                    Assert.True(ObserveOriginalReceipt());
                    return FireCanonicalWriteLockFailure("full_state_validation");
                }
                return Task.CompletedTask;
            }

            if (!string.Equals(
                    _boundary,
                    "transaction_commit_conflict",
                    StringComparison.Ordinal) ||
                !StackContains(nameof(MortalWoundTreatmentResourcePublicationTransaction)) ||
                !StackContains("CompleteAsync"))
            {
                return Task.CompletedTask;
            }

            _observedPhases.Add(_boundary);
            _matchingOccurrences++;
            if (_matchingOccurrences != _targetOccurrence)
                return Task.CompletedTask;
            Fired = true;
            return InvalidateAcceptedPlanFenceAsync();
        }

        private Task FireCanonicalWriteLockFailure(string phase)
        {
            _observedPhases.Add(phase);
            _matchingOccurrences++;
            if (_matchingOccurrences != _targetOccurrence)
                return Task.CompletedTask;
            Fired = true;
            return Task.FromException(new IOException(
                $"Injected held-treatment pipeline failure before '{phase}' canonical lease acquisition."));
        }

        private Task OnCanonicalMutationAsync(string path)
        {
            if (!_armed)
                return Task.CompletedTask;
            MutationPaths.Add(path);
            if (_context is not null && path is AcceptedMechanicsPlan.WoundCommandPath or
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath or ResourceMaterializationContract.StatePath)
            {
                var absolute = _context!.FileSystem.ResolvePath(path);
                FaultMutationImages.Add(new { path, stage = _compoundFailureStage,
                    Before = File.Exists(absolute) ? Convert.ToBase64String(File.ReadAllBytes(absolute)) : null });
            }
            if (string.Equals(
                    _boundary,
                    "wound_output_compensation_restore_failure",
                    StringComparison.Ordinal))
            {
                if (_compoundFailureStage != 1 ||
                    !StackContains("RestoreExactBeforeImagesAsync") ||
                    !StackContains(nameof(MortalWoundTreatmentResourcePublicationTransaction)))
                {
                    return Task.CompletedTask;
                }

                _compoundFailureStage = 2;
                _observedPhases.Add("compensation_restore");
                Fired = true;
                return Task.FromException(new IOException(
                    $"Injected held-treatment compensation restoration failure ({path})."));
            }
            if (_boundary is
                    "pre_canonical_terminal_quarantine_safe_failure" or
                    "pre_canonical_terminal_quarantine_cancellation" or
                    "pre_canonical_terminal_quarantine_session_replaced" &&
                _compoundFailureStage == 0 &&
                (_boundary == "pre_canonical_terminal_quarantine_safe_failure"
                    ? ObserveOriginalReceipt() && BytesEqual(_originalCommand, ReadOptionalTreatmentMember(_context!.FileSystem.ResolvePath(path)))
                    : StackContains("WriteQuarantinedDurableSurfacesAsync")) &&
                string.Equals(
                    path,
                    AcceptedMechanicsPlan.WoundCommandPath,
                    StringComparison.Ordinal))
            {
                _compoundFailureStage = 1;
                _observedPhases.Add(_boundary);
                Fired = true;
                return _boundary switch
                {
                    "pre_canonical_terminal_quarantine_cancellation" =>
                        Task.FromException(new OperationCanceledException(
                            "Injected pre-canonical terminal cancellation.")),
                    "pre_canonical_terminal_quarantine_session_replaced" =>
                        Task.FromException(new SessionReplacedException(
                            "Injected pre-canonical terminal session replacement.",
                            "expected_generation",
                            "replacement_generation")),
                    _ => Task.FromException(new IOException(
                        "Injected safely restorable pre-canonical command quarantine failure."))
                };
            }
            if (string.Equals(
                    _boundary,
                    "pre_canonical_terminal_quarantine_restore_failure",
                    StringComparison.Ordinal))
            {
                if (_compoundFailureStage == 0 &&
                    ObserveOriginalReceipt() &&
                    BytesEqual(_originalCommand, ReadOptionalTreatmentMember(_context!.FileSystem.ResolvePath(path))) &&
                    string.Equals(
                        path,
                        AcceptedMechanicsPlan.WoundCommandPath,
                        StringComparison.Ordinal))
                {
                    _compoundFailureStage = 1;
                    _observedPhases.Add("pre_canonical_command_quarantine");
                    return Task.CompletedTask;
                }
                if (_compoundFailureStage == 1 &&
                    ObserveOriginalReceipt() &&
                    BytesEqual(_originalPending, ReadOptionalTreatmentMember(_context!.FileSystem.ResolvePath(path))) &&
                    !BytesEqual(_originalCommand, ReadOptionalTreatmentMember(_context.FileSystem.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath))) &&
                    string.Equals(
                        path,
                        WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                        StringComparison.Ordinal))
                {
                    _compoundFailureStage = 2;
                    _observedPhases.Add("pre_canonical_pending_quarantine");
                    return Task.FromException(new IOException(
                        "Injected pre-canonical pending quarantine failure."));
                }
                if (_compoundFailureStage == 2 &&
                    ObserveOriginalReceipt() &&
                    !BytesEqual(_originalCommand, ReadOptionalTreatmentMember(_context!.FileSystem.ResolvePath(path))) &&
                    string.Equals(
                        path,
                        AcceptedMechanicsPlan.WoundCommandPath,
                        StringComparison.Ordinal))
                {
                    _compoundFailureStage = 3;
                    _observedPhases.Add("pre_canonical_command_restore");
                    Fired = true;
                    return Task.FromException(new IOException(
                        "Injected pre-canonical command restoration failure."));
                }
                return Task.CompletedTask;
            }
            if (Fired)
                return Task.CompletedTask;
            var phase = ClassifyCurrentPhase(path, isMutation: true);
            if (phase is null)
                return Task.CompletedTask;
            _observedPhases.Add(phase);
            if (!string.Equals(phase, _boundary, StringComparison.Ordinal))
                return Task.CompletedTask;
            _matchingOccurrences++;
            if (_matchingOccurrences != _targetOccurrence)
                return Task.CompletedTask;
            Fired = true;
            return Task.FromException(new IOException(
                $"Injected held-treatment pipeline failure at '{phase}' ({path})."));
        }

        private async Task InvalidateAcceptedPlanFenceAsync()
        {
            var context = Assert.IsType<HeldTreatmentPipelineContext>(_context);
            var competingFileSystem = new FileSystemManager(
                context.Root,
                NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance);
            competingFileSystem.EnsureDirectoryStructure();
            await using var lease = await competingFileSystem
                .AcquireCanonicalWriteLeaseAsync();
            AcceptedMechanicsPlanAuthority.InvalidateValidated(
                competingFileSystem,
                lease);
            var foreignInput = CreateForeignAcceptedMechanicsInput();
            var foreign = AcceptedMechanicsPlanAuthority.GetOrBuildValidated(
                competingFileSystem,
                lease,
                foreignInput);
            Assert.True(
                foreign.Success,
                DescribeValidationIssues(foreign.Issues));
            ForeignPlan = Assert.IsType<AcceptedMechanicsPlan>(foreign.Plan);
            ForeignBindingFingerprint =
                AcceptedMechanicsPlanFingerprints.ComputeInput(
                    foreignInput.CreateBinding());
        }

        private static AcceptedMechanicsInput CreateForeignAcceptedMechanicsInput()
        {
            var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
            var history = ResourceHistoryState.CreateValidated(
                Array.Empty<ResourceTransition>(),
                definitions).History!;
            var owners = ResourceOwnerAuthority.Build(
                new ResourceOwnerAuthorityInput(
                    Array.Empty<ResourceOwnerExport>(),
                    Array.Empty<ResourceOwnerExport>(),
                    Array.Empty<ResourceOwnerKey>()));
            var sources = ResourceMutationSourceCatalog.Create(
                Array.Empty<ResourceMutationSourceExport>()).Catalog!;
            var commands = ResourceAcceptedTurnInputComposer.Parse(null);
            Assert.Empty(commands.Issues);
            var context = new AcceptedMechanicsPlanningContext(
                definitions.ToCanonicalRoot(),
                definitions,
                new ResourceStateLedger(Array.Empty<ResourceStateEntry>()),
                history,
                owners,
                sources,
                commands,
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["entries"] = new JsonArray()
                },
                effectPlan: null);
            const string hash =
                "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
            return new AcceptedMechanicsInput(
                SessionId: "session_t070b6_foreign_common_plan",
                RequestId: "request_t070b6_foreign_common_plan",
                SnapshotToken: "snapshot_t070b6_foreign_common_plan",
                Realm: "mortal_world",
                Turn: 777,
                AcceptedEvents: new JsonObject
                {
                    ["acceptedEvents"] = new JsonArray()
                },
                ResourceCommands: commands.Root,
                EffectCommands: new JsonObject(),
                PendingInput: new JsonObject(),
                InternalInputs: new JsonObject(),
                AuthorityFingerprints: new AcceptedMechanicsAuthorityFingerprints(
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash,
                    hash),
                BeforeImages: new Dictionary<string, CanonicalBeforeImage>(
                    StringComparer.Ordinal)
                {
                    ["game_state/effects/effect_commands.json"] =
                        new(true, new byte[] { 1 }),
                    ["game_state/effects/effect_identity_index.json"] =
                        new(true, new byte[] { 2 }),
                    ["game_state/effects/effects.json"] =
                        new(true, new byte[] { 3 }),
                    ["game_state/resources/resource_commands.json"] =
                        new(true, new byte[] { 4 }),
                    ["game_state/resources/resource_definitions.json"] =
                        new(true, new byte[] { 5 }),
                    ["game_state/resources/resource_history.json"] =
                        new(true, new byte[] { 6 }),
                    [CanonicalResourceOwnerAuthorityComposer.AuthorityPath] =
                        new(true, new byte[] { 7 }),
                    ["game_state/resources/resource_state.json"] =
                        new(true, new byte[] { 8 })
                },
                ValidationIssues: Array.Empty<ValidationIssue>(),
                PlanningContext: context);
        }

        private string? ClassifyCurrentPhase(string path, bool isMutation)
        {
            if (isMutation)
            {
                return string.Equals(
                           path,
                           "game_state/meta/guardians.json",
                           StringComparison.Ordinal) &&
                       _observedPhases.Contains("critical_validation") && PublishedTreatmentMembers > 0 &&
                       ObserveOriginalReceipt() &&
                       ParseJsonObjectBytes(File.ReadAllBytes(_context!.FileSystem.ResolvePath(path))) is { } guardian &&
                       guardian.ContainsKey(GuardianProjectState.QuestProgressUpdatesProperty)
                    ? "cleanup"
                    : null;
            }

            if (StackContains("BindPublishedAcceptedWoundOutputAsync"))
                return "wound_output";
            if (StackContains("ValidateAcceptedWoundPostPublicationAuthorityAsync"))
            {
                return "wound_post_seal";
            }
            if (StackContains("ValidateCriticalCanonicalStateAsync"))
                return "critical_validation";
            if (StackContains("ValidateGameStateAsync"))
                return "full_state_validation";
            if (string.Equals(
                    path,
                    ResourceMaterializationContract.DefinitionsPath,
                    StringComparison.Ordinal) &&
                StackContains("RefreshGameStateCoreAsync") &&
                (StackContains("RefreshCanonicalStateAsync") ||
                 StackContains("ValidateAcceptedTurnOutcomeWithRepairLoopAsync") &&
                 !StackContains("EnsureClientOwnedSystemFilesHealthyAsync")))
            {
                return "runtime_refresh";
            }
            return null;
        }

        private static bool StackContains(string marker) =>
            new StackTrace().GetFrames().Any(frame =>
            {
                var method = frame.GetMethod();
                return method?.Name.Contains(marker, StringComparison.Ordinal) == true ||
                       method?.DeclaringType?.FullName?.Contains(
                           marker,
                           StringComparison.Ordinal) == true;
            });
    }

    private static PropertyInfo RequireOpenTreatmentTransactionCarrier()
    {
        var resultType = typeof(AcceptedTurnCanonicalStateRefresh.Result);
        var candidates = resultType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(static property =>
                property.PropertyType != typeof(AcceptedMechanicsPlan) &&
                property.PropertyType != typeof(IReadOnlyList<ValidationIssue>))
            .Where(static property =>
                property.Name.Contains("transaction", StringComparison.OrdinalIgnoreCase) ||
                HasTransactionLifecycle(property.PropertyType))
            .ToArray();

        Assert.True(
            candidates.Length == 1,
            "AcceptedTurnCanonicalStateRefresh.Result must carry exactly one internal opaque " +
            "open held-treatment transaction. Found: " +
            string.Join(", ", candidates.Select(static value =>
                value.Name + ":" + value.PropertyType.FullName)));
        return candidates[0];
    }

    private static bool HasTransactionLifecycle(Type type)
    {
        var methods = type.GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return methods.Any(static method => IsCompletionMethodName(method.Name)) &&
               methods.Any(static method => IsCompensationMethodName(method.Name));
    }

    private static PropertyInfo RequireGameEngineRefreshTransactionCarrier(Type carrierType)
    {
        var resultType = typeof(GameEngine).GetNestedType(
            "AcceptedTurnCanonicalRefreshResult",
            BindingFlags.NonPublic);
        Assert.NotNull(resultType);
        var candidates = resultType!
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.PropertyType == carrierType)
            .ToArray();
        Assert.True(
            candidates.Length == 1,
            "AcceptedTurnCanonicalRefreshResult must carry the exact open transaction " +
            "returned by AcceptedTurnCanonicalStateRefresh.Result. Found: " +
            string.Join(", ", candidates.Select(static value =>
                value.Name + ":" + value.PropertyType.FullName)));
        return candidates[0];
    }

    private static bool IsCompletionMethodName(string name) =>
        name.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("commit", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("finalize", StringComparison.OrdinalIgnoreCase);

    private static bool IsCompensationMethodName(string name) =>
        name.Contains("compensate", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("rollback", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("abort", StringComparison.OrdinalIgnoreCase);

    private static MethodInfo RequireExactCarrierCompensationMethod()
    {
        var carrierType = RequireOpenTreatmentTransactionCarrier().PropertyType;
        return Assert.Single(
            carrierType.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.DeclaringType == carrierType &&
                      IsCompensationMethodName(method.Name));
    }

    private static bool IsExactCarrierCompensationLifecycleName(
        string name,
        string exactCompensationMethodName) =>
        string.Equals(
            name,
            exactCompensationMethodName,
            StringComparison.Ordinal) ||
        string.Equals(
            name,
            CompensatedTreatmentPublicationBuilderName,
            StringComparison.Ordinal) ||
        string.Equals(
            name,
            nameof(IAsyncDisposable.DisposeAsync),
            StringComparison.Ordinal);

    private static bool IsResolvedInstanceInvocation(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax invocation) =>
        analysis.Model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
        !method.IsStatic &&
        string.Equals(
            method.Name,
            ReadInvocationName(invocation),
            StringComparison.Ordinal);

    private static bool IsAwaitableReturn(Type type) =>
        type == typeof(Task) ||
        type == typeof(ValueTask) ||
        (type.IsGenericType &&
         (type.GetGenericTypeDefinition() == typeof(Task<>) ||
          type.GetGenericTypeDefinition() == typeof(ValueTask<>)));

    private static MethodDeclarationSyntax ReadMethod(
        string relativePath,
        string methodName) => ReadMethodAnalysis(relativePath, methodName).Method;

    private static SourceMethodAnalysis ReadMethodAnalysis(
        string relativePath,
        string methodName)
    {
        var absolutePath = Path.Combine(
            TestRepoPaths.RepoRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        var source = File.ReadAllText(absolutePath);
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, absolutePath);
        var projectGlobalUsings = CSharpSyntaxTree.ParseText(
            """
            global using System;
            global using System.Collections.Generic;
            global using System.IO;
            global using System.Linq;
            global using System.Net.Http;
            global using System.Threading;
            global using System.Threading.Tasks;
            """,
            parseOptions,
            "BookOfEternityClient.ProjectGlobalUsings.g.cs");
        IReadOnlyList<SyntaxTree> sourceTrees =
            IsGameEngineSourceGuardPath(absolutePath)
                ? EnumerateGameEngineSourceGuardPaths()
                    .Select(path => string.Equals(
                            path,
                            absolutePath,
                            StringComparison.OrdinalIgnoreCase)
                        ? tree
                        : CSharpSyntaxTree.ParseText(
                            File.ReadAllText(path),
                            parseOptions,
                            path))
                    .ToArray()
                : new[] { tree };
        var compilationTrees = sourceTrees
            .Prepend(projectGlobalUsings)
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "BookOfEternityClient.IntegrationTests",
            compilationTrees,
            SourceGuardMetadataReferences.Value,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        var root = tree.GetRoot();
        var method = Assert.Single(root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>(),
            method => string.Equals(
                method.Identifier.ValueText,
                methodName,
                StringComparison.Ordinal));
        return new SourceMethodAnalysis(
            method,
            compilation.GetSemanticModel(tree, ignoreAccessibility: false));
    }

    private static bool IsGameEngineSourceGuardPath(string absolutePath)
    {
        var corePath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Core");
        return string.Equals(
                   absolutePath,
                   Path.Combine(corePath, "GameEngine.cs"),
                   StringComparison.OrdinalIgnoreCase) ||
               absolutePath.StartsWith(
                   Path.Combine(corePath, "GameEngine") +
                   Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> EnumerateGameEngineSourceGuardPaths()
    {
        var corePath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Core");
        return new[] { Path.Combine(corePath, "GameEngine.cs") }
            .Concat(Directory.EnumerateFiles(
                Path.Combine(corePath, "GameEngine"),
                "*.cs",
                SearchOption.TopDirectoryOnly))
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static CarrierOwnershipScope RequireCanonicalRefreshTransactionScope(
        SourceMethodAnalysis analysis)
    {
        var method = analysis.Method;
        var carrierType = RequireOpenTreatmentTransactionCarrier().PropertyType;
        var gameEngineCarrier = RequireGameEngineRefreshTransactionCarrier(carrierType);
        var refreshInvocation = Assert.Single(method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>(),
            static invocation => string.Equals(
                ReadInvocationName(invocation),
                "RefreshAcceptedTurnCanonicalStateForValidationAsync",
                StringComparison.Ordinal));
        var refreshResultSymbol = RequireAssignedLocalSymbol(
            analysis,
            refreshInvocation);
        Assert.Equal(
            "AcceptedTurnCanonicalRefreshResult",
            refreshResultSymbol.Type.Name);

        var assignmentStatement = refreshInvocation.Ancestors()
            .OfType<StatementSyntax>()
            .First();
        StatementSyntax ownershipAnchor = assignmentStatement;
        var refreshTry = refreshInvocation.Ancestors()
            .OfType<TryStatementSyntax>()
            .Where(statement => statement.Block.Span.Contains(refreshInvocation.Span))
            .MinBy(static statement => statement.Block.Span.Length);
        if (refreshTry is not null)
        {
            Assert.Same(refreshTry.Block, assignmentStatement.Parent);
            var assignmentIndex = refreshTry.Block.Statements.IndexOf(
                assignmentStatement);
            Assert.True(assignmentIndex >= 0);
            Assert.DoesNotContain(
                refreshTry.Block.Statements.Skip(assignmentIndex + 1),
                IsExecutableSourceStatement);
            Assert.False(
                refreshTry.Finally?.Block.Statements.Any(
                    IsExecutableSourceStatement) == true,
                "A finally body would execute after canonical refresh returned its " +
                "carrier but before the exact await-using ownership capture.");
            ownershipAnchor = refreshTry;
        }
        var owningBlock = Assert.IsType<BlockSyntax>(ownershipAnchor.Parent);
        var anchorIndex = owningBlock.Statements.IndexOf(ownershipAnchor);
        Assert.True(anchorIndex >= 0);
        Assert.True(
            anchorIndex + 1 < owningBlock.Statements.Count,
            "The exact transaction returned by canonical refresh must be captured " +
            "before the accepted-turn loop executes any other statement.");
        var scopeStatement = owningBlock.Statements[anchorIndex + 1];

        VariableDeclaratorSyntax carrierVariable;
        SyntaxNode lifetime;
        if (scopeStatement is LocalDeclarationStatementSyntax localDeclaration)
        {
            Assert.False(localDeclaration.AwaitKeyword.IsKind(SyntaxKind.None));
            Assert.False(localDeclaration.UsingKeyword.IsKind(SyntaxKind.None));
            carrierVariable = Assert.Single(localDeclaration.Declaration.Variables);
            lifetime = owningBlock;
        }
        else
        {
            var usingStatement = Assert.IsType<UsingStatementSyntax>(scopeStatement);
            Assert.False(usingStatement.AwaitKeyword.IsKind(SyntaxKind.None));
            Assert.NotNull(usingStatement.Declaration);
            carrierVariable = Assert.Single(usingStatement.Declaration!.Variables);
            lifetime = usingStatement.Statement;
        }
        var initializer = Assert.IsAssignableFrom<ExpressionSyntax>(
            carrierVariable.Initializer?.Value);
        Assert.True(
            ExpressionIsExactCarrierIdentity(
                analysis,
                initializer,
                refreshResultSymbol,
                gameEngineCarrier.Name,
                new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default)),
            "The first post-refresh executable statement must await-use the exact " +
            "transaction carrier flowing from AcceptedTurnCanonicalRefreshResult " +
            "without a conditional, coalesce, or wrapper invocation.");
        var carrierSymbol = Assert.IsAssignableFrom<ILocalSymbol>(
            analysis.Model.GetDeclaredSymbol(carrierVariable));
        return new CarrierOwnershipScope(
            scopeStatement,
            lifetime,
            refreshResultSymbol,
            carrierSymbol,
            gameEngineCarrier.Name);
    }

    private static bool IsExecutableSourceStatement(StatementSyntax statement) =>
        statement is not EmptyStatementSyntax and not LocalFunctionStatementSyntax;

    private static ILocalSymbol RequireAssignedLocalSymbol(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax invocation)
    {
        var declarator = invocation.AncestorsAndSelf()
            .OfType<VariableDeclaratorSyntax>()
            .FirstOrDefault(candidate => candidate.Initializer?.Value.Span.Contains(
                invocation.Span) == true);
        if (declarator is not null)
        {
            return Assert.IsAssignableFrom<ILocalSymbol>(
                analysis.Model.GetDeclaredSymbol(declarator));
        }
        var assignment = invocation.AncestorsAndSelf()
            .OfType<AssignmentExpressionSyntax>()
            .FirstOrDefault(candidate => candidate.Right.Span.Contains(invocation.Span));
        Assert.NotNull(assignment);
        return Assert.IsAssignableFrom<ILocalSymbol>(
            analysis.Model.GetSymbolInfo(assignment!.Left).Symbol);
    }

    private static InvocationExpressionSyntax RequireDirectAwaitedTypedCompletion(
        SourceMethodAnalysis analysis,
        CarrierOwnershipScope scope)
    {
        var carrierType = RequireOpenTreatmentTransactionCarrier().PropertyType;
        var complete = Assert.Single(
            carrierType.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.DeclaringType == carrierType &&
                      IsCompletionMethodName(method.Name));
        var aliases = BuildImmutableLocalAliases(
            analysis,
            scope.Lifetime,
            new[] { scope.CarrierSymbol });
        var completions = scope.Lifetime.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation =>
                string.Equals(
                    ReadInvocationName(invocation),
                    complete.Name,
                    StringComparison.Ordinal) &&
                IsResolvedInstanceInvocation(analysis, invocation) &&
                InvocationUsesCarrierSymbol(analysis, invocation, aliases))
            .ToArray();
        Assert.True(
            completions.Length == 1,
            "The accepted-turn coordinator must invoke completion exactly once on " +
            "the exact await-using transaction authority.");
        var completion = completions[0];
        var awaited = completion.Parent as AwaitExpressionSyntax;
        Assert.NotNull(awaited);
        Assert.True(
            ReferenceEquals(completion, awaited!.Expression),
            "Transaction completion must be directly awaited, not hidden behind " +
            "a fire-and-forget or unrelated wrapper invocation.");
        _ = RequireAssignedLocalSymbol(analysis, completion);

        var resultType = RequireAwaitedResultType(complete.ReturnType);
        Assert.Equal(typeof(bool), RequireResultProperty(resultType, "IsValid").PropertyType);
        Assert.True(typeof(IEnumerable<ValidationIssue>).IsAssignableFrom(
            RequireResultProperty(resultType, "Issues").PropertyType));
        Assert.Equal(typeof(int), RequireResultProperty(
            resultType,
            "ChangedCount").PropertyType);
        var outcomeType = RequireResultProperty(resultType, "Outcome").PropertyType;
        Assert.True(
            outcomeType == typeof(string) || outcomeType.IsEnum,
            "Typed completion Outcome may be a string or closed enum, but it must " +
            "represent Finalized explicitly.");
        return completion;
    }

    private static Type RequireAwaitedResultType(Type awaitableType)
    {
        Assert.True(
            awaitableType.IsGenericType &&
            (awaitableType.GetGenericTypeDefinition() == typeof(Task<>) ||
             awaitableType.GetGenericTypeDefinition() == typeof(ValueTask<>)),
            "Transaction completion must return an awaited typed result; Task/ValueTask " +
            "without IsValid/Issues/ChangedCount/Outcome cannot gate accepted success.");
        return awaitableType.GetGenericArguments()[0];
    }

    private static PropertyInfo RequireResultProperty(Type resultType, string name)
    {
        var property = resultType.GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return property!;
    }

    private static void RequireTypedCompletionSuccessDominatesAcceptedExit(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax completion,
        AssignmentExpressionSyntax notificationAssignment,
        ReturnStatementSyntax acceptedReturn)
    {
        var resultSymbol = RequireAssignedLocalSymbol(analysis, completion);
        var resultAliases = BuildImmutableLocalAliases(
            analysis,
            analysis.Method,
            new[] { resultSymbol });
        var graph = RequireControlFlowGraph(analysis);
        var completionBlock = RequireBlockContaining(graph, completion);
        var notificationBlock = RequireBlockContaining(graph, notificationAssignment);
        var acceptedReturnBlock = RequireBlockContaining(graph, acceptedReturn);
        var dominators = ComputeDominators(graph);
        var requiredProperties = new[]
        {
            "IsValid",
            "Issues",
            "ChangedCount",
            "Outcome"
        };

        foreach (var propertyName in requiredProperties)
        {
            var guards = graph.Blocks
                .Where(block => block.BranchValue is not null)
                .Where(block => OperationTree(block.BranchValue!).Any(operation =>
                    operation is IPropertyReferenceOperation property &&
                    string.Equals(
                        property.Property.Name,
                        propertyName,
                        StringComparison.Ordinal) &&
                    OperationIsIdentityLocalReference(
                        property.Instance,
                        resultAliases)))
                .Select(block => new
                {
                    Block = block,
                    ExpectedConditionValue = CompletionConditionExpectedValue(
                        analysis,
                        block.BranchValue!.Syntax,
                        propertyName,
                        resultAliases)
                })
                .Where(candidate => candidate.ExpectedConditionValue.HasValue)
                .Where(candidate => Dominates(
                                        dominators,
                                        candidate.Block,
                                        notificationBlock) &&
                                    Dominates(
                                        dominators,
                                        candidate.Block,
                                        acceptedReturnBlock))
                .Where(candidate => BranchRejectsUnexpectedCompletionValue(
                    candidate.Block,
                    candidate.ExpectedConditionValue.GetValueOrDefault(),
                    completionBlock,
                    notificationBlock,
                    acceptedReturnBlock))
                .Select(static candidate => candidate.Block)
                .ToArray();
            Assert.True(
                guards.Length > 0,
                $"Typed completion property '{propertyName}' must be checked for its " +
                "successful value on every path before notifications and return true.");
            Assert.All(guards, guard => Assert.True(
                Dominates(dominators, completionBlock, guard),
                $"Completion result guard '{propertyName}' cannot execute before " +
                "the direct awaited completion."));
        }
    }

    private static bool? CompletionConditionExpectedValue(
        SourceMethodAnalysis analysis,
        SyntaxNode condition,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases)
    {
        return propertyName switch
        {
            "IsValid" => ExactBooleanCompletionPredicateExpectedValue(
                analysis,
                condition,
                propertyName,
                resultAliases),
            "Issues" => ExactEmptyIssuesPredicateExpectedValue(
                analysis,
                condition,
                resultAliases),
            "ChangedCount" => ExactCompletionComparisonExpectedValue(
                analysis,
                condition,
                propertyName,
                resultAliases,
                expression => IsInt32Constant(analysis, expression, 1)),
            "Outcome" => ExactCompletionComparisonExpectedValue(
                analysis,
                condition,
                propertyName,
                resultAliases,
                expression => IsFinalizedCompletionValue(analysis, expression)),
            _ => null
        };
    }

    private static bool? ExactBooleanCompletionPredicateExpectedValue(
        SourceMethodAnalysis analysis,
        SyntaxNode condition,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases)
    {
        if (condition is not ExpressionSyntax expression)
            return null;
        var negated = UnwrapLogicalNegation(ref expression);
        if (IsDirectCompletionPropertyReference(
                analysis,
                expression,
                propertyName,
                resultAliases))
        {
            return !negated;
        }
        expression = StripParentheses(expression);
        if (expression is not BinaryExpressionSyntax binary ||
            !binary.IsKind(SyntaxKind.EqualsExpression) &&
            !binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            return null;
        }
        ExpressionSyntax constant;
        if (IsDirectCompletionPropertyReference(
                analysis,
                binary.Left,
                propertyName,
                resultAliases))
        {
            constant = binary.Right;
        }
        else if (IsDirectCompletionPropertyReference(
                     analysis,
                     binary.Right,
                     propertyName,
                     resultAliases))
        {
            constant = binary.Left;
        }
        else
        {
            return null;
        }
        if (analysis.Model.GetConstantValue(constant) is not
            { HasValue: true, Value: bool comparedValue })
        {
            return null;
        }
        var valueWhenExpected = binary.IsKind(SyntaxKind.EqualsExpression)
            ? comparedValue
            : !comparedValue;
        return negated ? !valueWhenExpected : valueWhenExpected;
    }

    private static bool? ExactEmptyIssuesPredicateExpectedValue(
        SourceMethodAnalysis analysis,
        SyntaxNode condition,
        IReadOnlySet<ILocalSymbol> resultAliases)
    {
        if (condition is not ExpressionSyntax expression)
            return null;
        var negated = UnwrapLogicalNegation(ref expression);
        expression = StripParentheses(expression);
        if (expression is InvocationExpressionSyntax anyInvocation &&
            string.Equals(
                ReadInvocationName(anyInvocation),
                "Any",
                StringComparison.Ordinal) &&
            InvocationDirectlyReadsCompletionProperty(
                analysis,
                anyInvocation,
                "Issues",
                resultAliases))
        {
            return negated;
        }
        if (expression is not BinaryExpressionSyntax binary ||
            !binary.IsKind(SyntaxKind.EqualsExpression) &&
            !binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            return null;
        }
        var exactCountComparison =
            IsIssuesCountSide(analysis, binary.Left, resultAliases) &&
            IsInt32Constant(analysis, binary.Right, 0) ||
            IsIssuesCountSide(analysis, binary.Right, resultAliases) &&
            IsInt32Constant(analysis, binary.Left, 0);
        if (!exactCountComparison)
            return null;
        var valueWhenExpected = binary.IsKind(SyntaxKind.EqualsExpression);
        return negated ? !valueWhenExpected : valueWhenExpected;
    }

    private static bool IsIssuesCountSide(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression,
        IReadOnlySet<ILocalSymbol> resultAliases) =>
        StripParentheses(expression) is MemberAccessExpressionSyntax member &&
        member.Name.Identifier.ValueText is "Count" or "Length" &&
        IsDirectCompletionPropertyReference(
            analysis,
            member.Expression,
            "Issues",
            resultAliases);

    private static bool? ExactCompletionComparisonExpectedValue(
        SourceMethodAnalysis analysis,
        SyntaxNode condition,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases,
        Func<ExpressionSyntax, bool> isExpected)
    {
        if (condition is not ExpressionSyntax expression)
            return null;
        var negated = UnwrapLogicalNegation(ref expression);
        expression = StripParentheses(expression);
        bool? valueWhenExpected = expression switch
        {
            BinaryExpressionSyntax binary
                when binary.IsKind(SyntaxKind.EqualsExpression) ||
                     binary.IsKind(SyntaxKind.NotEqualsExpression) =>
                ExactBinaryCompletionComparisonExpectedValue(
                    analysis,
                    binary,
                    propertyName,
                    resultAliases,
                    isExpected),
            InvocationExpressionSyntax invocation
                when string.Equals(
                    ReadInvocationName(invocation),
                    nameof(object.Equals),
                    StringComparison.Ordinal) &&
                     InvocationExactlyComparesCompletionProperty(
                         analysis,
                         invocation,
                         propertyName,
                         resultAliases,
                         isExpected) => true,
            IsPatternExpressionSyntax pattern
                when IsDirectCompletionPropertyReference(
                         analysis,
                         pattern.Expression,
                         propertyName,
                         resultAliases) &&
                     pattern.Pattern is ConstantPatternSyntax constant &&
                     isExpected(constant.Expression) => true,
            _ => null
        };
        return valueWhenExpected.HasValue && negated
            ? !valueWhenExpected.Value
            : valueWhenExpected;
    }

    private static bool? ExactBinaryCompletionComparisonExpectedValue(
        SourceMethodAnalysis analysis,
        BinaryExpressionSyntax binary,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases,
        Func<ExpressionSyntax, bool> isExpected)
    {
        var exactOperands =
            IsDirectCompletionPropertyReference(
                analysis,
                binary.Left,
                propertyName,
                resultAliases) && isExpected(binary.Right) ||
            IsDirectCompletionPropertyReference(
                analysis,
                binary.Right,
                propertyName,
                resultAliases) && isExpected(binary.Left);
        if (!exactOperands)
            return null;
        return binary.IsKind(SyntaxKind.EqualsExpression);
    }

    private static bool InvocationExactlyComparesCompletionProperty(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax invocation,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases,
        Func<ExpressionSyntax, bool> isExpected)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax member &&
            IsDirectCompletionPropertyReference(
                analysis,
                member.Expression,
                propertyName,
                resultAliases))
        {
            return invocation.ArgumentList.Arguments is [{ Expression: var argument }] &&
                   isExpected(argument);
        }
        if (invocation.ArgumentList.Arguments is not
            [{ Expression: var left }, { Expression: var right }])
        {
            return false;
        }
        return IsDirectCompletionPropertyReference(
                   analysis,
                   left,
                   propertyName,
                   resultAliases) && isExpected(right) ||
               IsDirectCompletionPropertyReference(
                   analysis,
                   right,
                   propertyName,
                   resultAliases) && isExpected(left);
    }

    private static bool InvocationDirectlyReadsCompletionProperty(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax invocation,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax member &&
            IsDirectCompletionPropertyReference(
                analysis,
                member.Expression,
                propertyName,
                resultAliases))
        {
            return true;
        }
        return invocation.ArgumentList.Arguments.Any(argument =>
            IsDirectCompletionPropertyReference(
                analysis,
                argument.Expression,
                propertyName,
                resultAliases));
    }

    private static bool IsDirectCompletionPropertyReference(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases) =>
        OperationIsDirectCompletionPropertyReference(
            analysis.Model.GetOperation(StripParentheses(expression)),
            propertyName,
            resultAliases);

    private static bool OperationIsDirectCompletionPropertyReference(
        IOperation? operation,
        string propertyName,
        IReadOnlySet<ILocalSymbol> resultAliases) =>
        UnwrapIdentityPreservingOperation(operation) is
            IPropertyReferenceOperation property &&
        string.Equals(
            property.Property.Name,
            propertyName,
            StringComparison.Ordinal) &&
        OperationIsIdentityLocalReference(property.Instance, resultAliases);

    private static bool UnwrapLogicalNegation(ref ExpressionSyntax expression)
    {
        var negated = false;
        expression = StripParentheses(expression);
        while (expression is PrefixUnaryExpressionSyntax prefix &&
               prefix.IsKind(SyntaxKind.LogicalNotExpression))
        {
            negated = !negated;
            expression = StripParentheses(prefix.Operand);
        }
        return negated;
    }

    private static bool IsInt32Constant(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression,
        int expected) => analysis.Model.GetConstantValue(expression) is
            { HasValue: true, Value: int value } && value == expected;

    private static bool IsFinalizedCompletionValue(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression)
    {
        if (analysis.Model.GetConstantValue(expression) is
                { HasValue: true, Value: string value } &&
            string.Equals(value, "Finalized", StringComparison.Ordinal))
        {
            return true;
        }
        return analysis.Model.GetSymbolInfo(expression).Symbol is IFieldSymbol field &&
               field.ContainingType?.TypeKind == TypeKind.Enum &&
               string.Equals(field.Name, "Finalized", StringComparison.Ordinal);
    }

    private static ExpressionSyntax StripParentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parentheses)
            expression = parentheses.Expression;
        return expression;
    }

    private static bool BranchRejectsUnexpectedCompletionValue(
        BasicBlock guard,
        bool expectedConditionValue,
        BasicBlock completion,
        BasicBlock notification,
        BasicBlock acceptedReturn)
    {
        var fallThrough = guard.FallThroughSuccessor?.Destination;
        var conditional = guard.ConditionalSuccessor?.Destination;
        if (fallThrough is null || conditional is null ||
            fallThrough.Ordinal == conditional.Ordinal)
        {
            return false;
        }
        BasicBlock expected;
        BasicBlock unexpected;
        switch (guard.ConditionKind)
        {
            case ControlFlowConditionKind.WhenTrue:
                expected = expectedConditionValue ? conditional : fallThrough;
                unexpected = expectedConditionValue ? fallThrough : conditional;
                break;
            case ControlFlowConditionKind.WhenFalse:
                expected = expectedConditionValue ? fallThrough : conditional;
                unexpected = expectedConditionValue ? conditional : fallThrough;
                break;
            default:
                return false;
        }
        return CanReachBefore(expected, notification, completion) &&
               CanReachBefore(expected, acceptedReturn, completion) &&
               !CanReachBefore(unexpected, notification, completion) &&
               !CanReachBefore(unexpected, acceptedReturn, completion);
    }

    private static IEnumerable<BasicBlock> EnumerateSuccessors(BasicBlock block)
    {
        if (block.FallThroughSuccessor?.Destination is { } fallThrough)
            yield return fallThrough;
        if (block.ConditionalSuccessor?.Destination is { } conditional)
            yield return conditional;
    }

    private static bool CanReachBefore(
        BasicBlock source,
        BasicBlock target,
        BasicBlock barrier)
    {
        var visited = new HashSet<int>();
        var pending = new Queue<BasicBlock>();
        pending.Enqueue(source);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (!visited.Add(current.Ordinal))
                continue;
            if (current.Ordinal == target.Ordinal)
                return true;
            if (current.Ordinal == barrier.Ordinal)
                continue;
            foreach (var successor in EnumerateSuccessors(current))
                pending.Enqueue(successor);
        }
        return false;
    }

    private static bool ExpressionIsExactCarrierIdentity(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression,
        ILocalSymbol resultSymbol,
        string carrierPropertyName,
        IReadOnlySet<ILocalSymbol> aliases) =>
        OperationIsExactCarrierIdentity(
            analysis.Model.GetOperation(expression),
            resultSymbol,
            carrierPropertyName,
            aliases);

    private static bool OperationIsExactCarrierIdentity(
        IOperation? input,
        ILocalSymbol resultSymbol,
        string carrierPropertyName,
        IReadOnlySet<ILocalSymbol> aliases)
    {
        var operation = UnwrapIdentityPreservingOperation(input);
        if (operation is ILocalReferenceOperation local)
        {
            return aliases.Contains(local.Local);
        }
        return operation is IPropertyReferenceOperation property &&
               string.Equals(
                   property.Property.Name,
                   carrierPropertyName,
                   StringComparison.Ordinal) &&
               UnwrapIdentityPreservingOperation(property.Instance) is
                   ILocalReferenceOperation owner &&
               SymbolEqualityComparer.Default.Equals(owner.Local, resultSymbol);
    }

    private static IReadOnlySet<ILocalSymbol> BuildImmutableLocalAliases(
        SourceMethodAnalysis analysis,
        SyntaxNode scope,
        IEnumerable<ILocalSymbol> roots)
    {
        var aliases = new HashSet<ILocalSymbol>(
            roots,
            SymbolEqualityComparer.Default);
        bool changed;
        do
        {
            changed = false;
            foreach (var variable in scope.DescendantNodes()
                         .OfType<VariableDeclaratorSyntax>()
                         .Where(static variable => variable.Initializer is not null))
            {
                var symbol = analysis.Model.GetDeclaredSymbol(variable) as ILocalSymbol;
                if (symbol is null || aliases.Contains(symbol) ||
                    !ExpressionIsIdentityLocalAlias(
                        analysis,
                        variable.Initializer!.Value,
                        aliases) ||
                    !IsImmutableLocal(analysis, scope, symbol, variable))
                {
                    continue;
                }
                aliases.Add(symbol);
                changed = true;
            }
        } while (changed);
        return aliases;
    }

    private static bool IsImmutableLocal(
        SourceMethodAnalysis analysis,
        SyntaxNode scope,
        ILocalSymbol symbol,
        VariableDeclaratorSyntax declaration) =>
        !scope.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier => identifier.SpanStart > declaration.Span.End)
            .Where(identifier => SymbolEqualityComparer.Default.Equals(
                analysis.Model.GetSymbolInfo(identifier).Symbol,
                symbol))
            .Any(static identifier =>
                identifier.Parent is AssignmentExpressionSyntax assignment &&
                assignment.Left.Span.Contains(identifier.Span) ||
                identifier.Parent is PrefixUnaryExpressionSyntax prefix &&
                (prefix.IsKind(SyntaxKind.PreIncrementExpression) ||
                 prefix.IsKind(SyntaxKind.PreDecrementExpression)) ||
                identifier.Parent is PostfixUnaryExpressionSyntax postfix &&
                (postfix.IsKind(SyntaxKind.PostIncrementExpression) ||
                 postfix.IsKind(SyntaxKind.PostDecrementExpression)) ||
                identifier.Parent is ArgumentSyntax argument &&
                !argument.RefKindKeyword.IsKind(SyntaxKind.None));

    private static bool ExpressionReferencesAnyLocal(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression,
        IReadOnlySet<ILocalSymbol> locals) =>
        expression.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier => analysis.Model.GetSymbolInfo(identifier).Symbol is
                ILocalSymbol local && locals.Contains(local));

    private static bool ExpressionIsIdentityLocalAlias(
        SourceMethodAnalysis analysis,
        ExpressionSyntax expression,
        IReadOnlySet<ILocalSymbol> locals) =>
        UnwrapIdentityPreservingOperation(
            analysis.Model.GetOperation(expression)) is
                ILocalReferenceOperation local &&
        locals.Contains(local.Local);

    private static IOperation? UnwrapIdentityPreservingOperation(
        IOperation? operation)
    {
        while (operation is IConversionOperation conversion &&
               !conversion.Conversion.IsUserDefined &&
               (conversion.Conversion.IsIdentity ||
                conversion.Conversion.IsReference))
        {
            operation = conversion.Operand;
        }
        return operation;
    }

    private static bool InvocationUsesCarrierSymbol(
        SourceMethodAnalysis analysis,
        InvocationExpressionSyntax invocation,
        IReadOnlySet<ILocalSymbol> aliases)
    {
        var receivers = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => new[] { member.Expression },
            MemberBindingExpressionSyntax => invocation.Ancestors()
                .OfType<ConditionalAccessExpressionSyntax>()
                .Where(candidate => candidate.WhenNotNull.Span.Contains(invocation.Span))
                .Select(static candidate => candidate.Expression),
            _ => Array.Empty<ExpressionSyntax>()
        };
        return receivers.Any(input => ExpressionIsIdentityLocalAlias(
            analysis,
            input,
            aliases));
    }

    private static ControlFlowGraph RequireControlFlowGraph(
        SourceMethodAnalysis analysis)
    {
        var body = Assert.IsAssignableFrom<IMethodBodyOperation>(
            analysis.Model.GetOperation(analysis.Method));
        return ControlFlowGraph.Create(body);
    }

    private static BasicBlock RequireBlockContaining(
        ControlFlowGraph graph,
        SyntaxNode syntax)
    {
        var block = TryFindBlockContaining(graph, syntax);
        Assert.NotNull(block);
        return block!;
    }

    private static BasicBlock? TryFindBlockContaining(
        ControlFlowGraph graph,
        SyntaxNode syntax)
    {
        var candidates = graph.Blocks
            .Select(block => new
            {
                Block = block,
                Operations = block.Operations.SelectMany(OperationTree)
                    .Concat(block.BranchValue is null
                        ? Array.Empty<IOperation>()
                        : OperationTree(block.BranchValue))
                    .ToArray()
            })
            .Where(candidate => candidate.Operations.Any(operation =>
                operation.Syntax.Span == syntax.Span))
            .Select(static candidate => candidate.Block)
            .DistinctBy(static block => block.Ordinal)
            .ToArray();
        if (candidates.Length == 1)
            return candidates[0];
        candidates = graph.Blocks
            .Where(block => block.Operations.SelectMany(OperationTree)
                .Concat(block.BranchValue is null
                    ? Array.Empty<IOperation>()
                    : OperationTree(block.BranchValue))
                .Any(operation =>
                    operation.Syntax.Span.Contains(syntax.Span) ||
                    syntax.Span.Contains(operation.Syntax.Span)))
            .DistinctBy(static block => block.Ordinal)
            .ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static IEnumerable<IOperation> OperationTree(IOperation root)
    {
        yield return root;
        foreach (var child in root.ChildOperations)
        foreach (var nested in OperationTree(child))
            yield return nested;
    }

    private static bool OperationReferencesAnyLocal(
        IOperation? operation,
        IReadOnlySet<ILocalSymbol> locals) =>
        operation is not null && OperationTree(operation).Any(candidate =>
            candidate is ILocalReferenceOperation local &&
            locals.Contains(local.Local));

    private static bool OperationIsIdentityLocalReference(
        IOperation? operation,
        IReadOnlySet<ILocalSymbol> locals) =>
        UnwrapIdentityPreservingOperation(operation) is
            ILocalReferenceOperation local &&
        locals.Contains(local.Local);

    private static IReadOnlyDictionary<int, HashSet<int>> ComputeDominators(
        ControlFlowGraph graph)
    {
        var ordinals = graph.Blocks.Select(static block => block.Ordinal).ToHashSet();
        var entry = graph.Blocks.Single(static block =>
            block.Kind == BasicBlockKind.Entry);
        var dominators = graph.Blocks.ToDictionary(
            static block => block.Ordinal,
            block => block.Ordinal == entry.Ordinal
                ? new HashSet<int> { entry.Ordinal }
                : new HashSet<int>(ordinals));
        bool changed;
        do
        {
            changed = false;
            foreach (var block in graph.Blocks.Where(block =>
                         block.Ordinal != entry.Ordinal))
            {
                var predecessors = block.Predecessors
                    .Select(static branch => branch.Source.Ordinal)
                    .Distinct()
                    .ToArray();
                var next = predecessors.Length == 0
                    ? new HashSet<int>()
                    : new HashSet<int>(dominators[predecessors[0]]);
                foreach (var predecessor in predecessors.Skip(1))
                    next.IntersectWith(dominators[predecessor]);
                next.Add(block.Ordinal);
                if (dominators[block.Ordinal].SetEquals(next))
                    continue;
                dominators[block.Ordinal] = next;
                changed = true;
            }
        } while (changed);
        return dominators;
    }

    private static bool Dominates(
        IReadOnlyDictionary<int, HashSet<int>> dominators,
        BasicBlock candidate,
        BasicBlock target) => dominators[target.Ordinal].Contains(candidate.Ordinal);

    private static InvocationExpressionSyntax RequireInvocation(
        IEnumerable<InvocationExpressionSyntax> invocations,
        string methodName) => Assert.Single(invocations, invocation =>
            string.Equals(
                ReadInvocationName(invocation),
                methodName,
                StringComparison.Ordinal));

    private static InvocationExpressionSyntax[] RequireInvocations(
        IEnumerable<InvocationExpressionSyntax> invocations,
        string methodName)
    {
        var matches = invocations.Where(invocation => string.Equals(
                ReadInvocationName(invocation),
                methodName,
                StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(matches);
        return matches;
    }

    private static string ReadInvocationName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            _ => string.Empty
        };
}
