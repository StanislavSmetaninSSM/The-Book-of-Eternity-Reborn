using System.Text.Json.Nodes;
using System.Reflection;
using System.Runtime.ExceptionServices;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    private const string DeferredItemConsumptionCode =
        "mortal_wound_treatment_publication_item_consumption_unsupported";
    private const string TransactionTokenMismatchCode =
        "mortal_wound_treatment_publication_transaction_token_mismatch";
    private const string TransactionTokenReplayedCode =
        "mortal_wound_treatment_publication_transaction_token_replayed";
    private const string TransactionStaleCode =
        "mortal_wound_treatment_publication_transaction_stale";
    private const string TransactionReservationChangedCode =
        "mortal_wound_treatment_publication_transaction_reservation_changed";
    private const string PublicationBlockedCode =
        "mortal_wound_treatment_publication_compensation_restart_required";
    private const string TerminalReleaseFailureCode =
        "mortal_wound_treatment_publication_terminal_release_failed";
    private const string PublishedAgreementChangedCode =
        "mortal_wound_treatment_publication_published_agreement_changed";

    [Fact]
    public void GuaranteedResourceQuantity_PersistedConfirmedPublicationSpendsOnceAndCommitsAtFullPipelineEnd()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "confirmed_spend_once");
        var beforeComposition = CaptureResolverFixtureTree(fixture.Root);
        var itemCarrierBefore = CaptureItemCarrierBytes(fixture);
        var definitionsBefore = ReadCanonicalBytes(
            fixture,
            ResourceMaterializationContract.DefinitionsPath);
        var historyBefore = ReadResourceHistory(fixture);

        var plan = ComposeResourcePublication(fixture, flow);

        AssertResolverFixtureTreeUnchanged(fixture.Root, beforeComposition);
        Assert.Same(plan, PeekCachedPlan(fixture));
        AssertResourcePlan(
            plan,
            expectedCurrent: 8,
            historyBefore.Transitions.Count,
            expectedSpendQuantities: new decimal[] { 2 });
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
        {
            Assert.Same(plan, publication.Plan);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
                fixture.FileSystem,
                fixture.Lease));
            Assert.Equal(8, ReadPlayerEnergy(fixture));
            AssertItemCarrierBytesEqual(fixture, itemCarrierBefore);
            Assert.Equal(definitionsBefore, ReadCanonicalBytes(
                fixture,
                ResourceMaterializationContract.DefinitionsPath));
            Assert.Equal(
                historyBefore.Transitions.Count + 1,
                ReadResourceHistory(fixture).Transitions.Count);
            Assert.Equal(
                "held",
                Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request)
                    .ResourceAuthority.ReservationDisposition);

            publication.CompleteAtFullPipelineEnd();
        }

        fixture.PrepareFreshSnapshot("resource_publication_history_replay");
        fixture.RestartForReplay();
        var recovered = Assert.IsType<MortalWoundTreatmentPersistedRequestCatalogResult>(
            RestoreCurrentPersistedTreatmentCatalog(fixture));
        Assert.True(recovered.IsValid, DescribeIssues(recovered.Issues));
        Assert.Empty(recovered.HeldRequests);
        Assert.Single(recovered.FinalizedRequests);
        var replay = ProbePublishedTreatment(fixture, flow.Request);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(
            replay,
            "Status")));
        Assert.Equal(8, ReadPlayerEnergy(fixture));
        Assert.Equal(
            historyBefore.Transitions.Count + 1,
            ReadResourceHistory(fixture).Transitions.Count);
    }

    [Theory]
    [InlineData("provisional")]
    [InlineData("released")]
    [InlineData("changed_live_quantity")]
    [InlineData("changed_finalization")]
    public void GuaranteedResourceQuantity_ProvisionalOrStaleHoldRejectsBeforeCacheAndWrite(
        string axis)
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = ResolveCurrentTreatment(
            fixture,
            "guaranteed",
            scenario.OperationKey + "_" + axis,
            scenario.RouteId);
        TreatmentFlow supplied = flow;
        if (string.Equals(axis, "released", StringComparison.Ordinal))
        {
            var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                flow.AcceptedState);
            var released = acceptedState.ReleaseTreatmentResources(
                ResourceLifecycleCapability(),
                new[] { Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request) },
                "validation_failed");
            Assert.True(released.IsValid, DescribeIssues(released.Issues));
            Assert.Equal(1, released.ChangedCount);
        }
        else if (string.Equals(axis, "changed_live_quantity", StringComparison.Ordinal))
        {
            supplied = PersistAndRehydrateResourcePublication(
                fixture,
                scenario,
                axis);
            WriteCanonicalPlayerEnergyAuthority(fixture.FileSystem, current: 9);
        }
        else if (string.Equals(axis, "changed_finalization", StringComparison.Ordinal))
        {
            var confirmed = PersistAndRehydrateResourcePublication(
                fixture,
                scenario,
                axis);
            var foreign = ResolveCurrentTreatment(
                fixture,
                "guaranteed",
                scenario.OperationKey + "_foreign_finalization",
                scenario.RouteId);
            var changedFinalization = MortalWoundTreatmentResourceComposer.Finalize(
                Assert.IsType<MortalWoundTreatmentResolution>(foreign.Resolution));
            Assert.True(
                changedFinalization.IsValid,
                DescribeIssues(changedFinalization.Issues));
            Assert.Equal("consume", changedFinalization.Finalization!.Disposition);
            supplied = new TreatmentFlow(
                confirmed.AcceptedState,
                confirmed.Request,
                foreign.Resolution,
                confirmed.Before,
                confirmed.History);
        }

        var treeBefore = CaptureResolverFixtureTree(fixture.Root);
        var result = ComposeResourcePublicationResult(fixture, supplied);

        Assert.False(result.IsValid);
        Assert.Null(result.Plan);
        var expectedCode = axis switch
        {
            "provisional" =>
                "mortal_wound_treatment_publication_resource_reservation_not_confirmed",
            "released" =>
                "mortal_wound_treatment_publication_resource_reservation_missing",
            "changed_live_quantity" =>
                "mortal_wound_treatment_publication_resource_state_changed",
            "changed_finalization" =>
                "mortal_wound_treatment_publication_resource_finalization_mismatch",
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
        };
        Assert.Equal(expectedCode, Assert.Single(result.Issues).Code);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
    }

    [Fact]
    public void GuaranteedResourceQuantity_SelectedItemConsumptionFailsClosedWithoutPartialSpend()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true,
            selectReusableItem: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "mixed_item_resource");
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);
        var itemCarrierBefore = CaptureItemCarrierBytes(fixture);

        var result = ComposeResourcePublicationResult(fixture, flow);

        Assert.False(result.IsValid);
        Assert.Null(result.Plan);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(DeferredItemConsumptionCode, issue.Code);
        Assert.Contains("T070-B.4", issue.Expected, StringComparison.Ordinal);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
        Assert.Equal(10, ReadPlayerEnergy(fixture));
        AssertItemCarrierBytesEqual(fixture, itemCarrierBefore);
        Assert.Equal(1, fixture.ReadNpcItemCount("reusable_field_kit"));
    }

    [Fact]
    public void GuaranteedResourceQuantity_ReleaseOnlyPublishesNoMutationAndFinalizesReservation()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: Array.Empty<int>(),
            includeReusableItem: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "release_only");
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var finalization = MortalWoundTreatmentResourceComposer.Finalize(resolution);
        Assert.True(finalization.IsValid, DescribeIssues(finalization.Issues));
        Assert.Equal("release_only", finalization.Finalization!.Disposition);
        var resourceBytes = CaptureResourceBytes(fixture);
        var itemCarrierBytes = CaptureItemCarrierBytes(fixture);

        var plan = ComposeResourcePublication(fixture, flow);
        Assert.Empty(plan.ResourceEvents);
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
        {
            Assert.Same(plan, publication.Plan);
            AssertResourceBytesEqual(fixture, resourceBytes);
            AssertItemCarrierBytesEqual(fixture, itemCarrierBytes);
            Assert.Equal(10, ReadPlayerEnergy(fixture));
            Assert.Equal(
                "held",
                Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request)
                    .ResourceAuthority.ReservationDisposition);

            publication.CompleteAtFullPipelineEnd();
        }

        fixture.PrepareFreshSnapshot("release_only_history_replay");
        fixture.RestartForReplay();
        var recovered = Assert.IsType<MortalWoundTreatmentPersistedRequestCatalogResult>(
            RestoreCurrentPersistedTreatmentCatalog(fixture));
        Assert.True(recovered.IsValid, DescribeIssues(recovered.Issues));
        Assert.Empty(recovered.HeldRequests);
        Assert.Single(recovered.FinalizedRequests);
    }

    [Fact]
    public void GuaranteedResourceQuantity_DirectNormalizerRejectsBeforeTakingOrWriting()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "direct_normalizer_fence");
        var plan = ComposeResourcePublication(fixture, flow);
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);

        var exception = Record.Exception(() => new CanonicalStateNormalizer(
                fixture.FileSystem,
                NullLogger<CanonicalStateNormalizer>.Instance)
            .BindTo(fixture.Lease)
            .NormalizeAcceptedMechanicsAsync(backups: null)
            .GetAwaiter()
            .GetResult());

        Assert.NotNull(exception);
        Assert.Contains("top-level accepted-turn transaction", exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Same(plan, PeekCachedPlan(fixture));
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
        Assert.Equal(10, ReadPlayerEnergy(fixture));

        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        Assert.Same(plan, publication.Plan);
        Assert.Equal(8, ReadPlayerEnergy(fixture));
        publication.Compensate();
        Assert.Same(plan, PeekCachedPlan(fixture));
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("accumulated")]
    public void GuaranteedResourceQuantity_HeldCoordinatorFencePrecedesGenericStalePreflight(
        string entrypoint)
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "early_coordinator_fence_" + entrypoint);
        var plan = ComposeResourcePublication(fixture, flow);
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);
        WriteCanonicalPlayerEnergyAuthority(fixture.FileSystem, current: 9);

        var normalizer = new CanonicalStateNormalizer(
                fixture.FileSystem,
                NullLogger<CanonicalStateNormalizer>.Instance)
            .BindTo(fixture.Lease);
        var exception = Record.Exception(() =>
        {
            if (string.Equals(entrypoint, "direct", StringComparison.Ordinal))
            {
                _ = normalizer.NormalizeAcceptedMechanicsAsync(backups: null)
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                _ = normalizer.NormalizeAccumulatedStateWithPlanAsync(backups: null)
                    .GetAwaiter()
                    .GetResult();
            }
        });

        Assert.NotNull(exception);
        Assert.Contains(
            "top-level accepted-turn transaction",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Same(plan, PeekCachedPlan(fixture));

        RestoreResolverFixtureTree(fixture, treeBefore);
        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        Assert.Same(plan, publication.Plan);
        publication.Compensate();
        AssertExactCachedPlanAndBinding(
            fixture,
            plan,
            publication.OriginalBinding);
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
    }

    [Fact]
    public void GuaranteedResourceQuantity_PostWriteFailureRestoresAllRootsRetainsHoldAndExactRetryCommitsOnce()
    {
        var fault = new ResourcePublicationFailureInjection();
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(
            scenario,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationAsync = fault.BeforeCanonicalMutationAsync
            });
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(2);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "rollback_retry");
        var plan = ComposeResourcePublication(fixture, flow);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out var originalBinding,
            out var originalCached));
        Assert.Same(plan, originalCached.Plan);
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);
        var commandBefore = ReadCanonicalBytes(
            fixture,
            AcceptedMechanicsPlan.WoundCommandPath);
        fault.Arm(
            WoundHistoryState.HistoryPath,
            ResourceMaterializationContract.StatePath,
            ReadCanonicalBytes(fixture, ResourceMaterializationContract.StatePath),
            fixture.FileSystem);

        var exception = Assert.Throws<CanonicalStateWriteException>(() =>
            PublishCachedResourcePlanOpen(fixture, flow, plan));

        Assert.Equal(WoundHistoryState.HistoryPath, exception.RelativePath);
        Assert.True(fault.Fired);
        Assert.True(fault.ObservedEarlierResourceWrite);
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
        AssertExactCachedPlanAndBinding(fixture, plan, originalBinding);
        Assert.Equal(commandBefore, ReadCanonicalBytes(
            fixture,
            AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Equal(2, ReadPlayerEnergy(fixture));

        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var competing = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(
            acceptedState,
            fixture.ReadCurrentHistory(),
            fixture.ReadCurrentWound(),
            scenario.OperationKey + "_rollback_competing",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.False(competing.IsValid);
        Assert.Contains(competing.Issues, static issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_resource_reservation_overbooked",
            StringComparison.Ordinal));

        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        Assert.Same(plan, publication.Plan);
        Assert.Equal(0, ReadPlayerEnergy(fixture));
        publication.CompleteAtFullPipelineEnd();
        Assert.Single(ReadTreatmentResourceSpendTransitions(fixture));
    }

    [Fact]
    public void GuaranteedResourceQuantity_SameCoordinateMutationsFollowFinalizationOrder()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2, 3 },
            selectedResourceOrder: new[] { 1, 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "same_coordinate_order");
        using var coldFixture = CreateColdRootCopy(fixture);
        var coldRestored = Assert.Single(AssertValidPersistedCatalog(
            RestoreCurrentPersistedTreatmentCatalog(coldFixture),
            "same-coordinate cold composition"));
        var coldFlow = RehydratePersistedTreatment(
            coldFixture,
            "guaranteed",
            coldRestored);

        var plan = ComposeResourcePublication(fixture, flow);
        var coldPlan = ComposeResourcePublication(coldFixture, coldFlow);

        AssertResourcePlan(
            plan,
            expectedCurrent: 5,
            ReadResourceHistory(fixture).Transitions.Count,
            expectedSpendQuantities: new decimal[] { 3, 2 });
        Assert.Equal(new decimal[] { 10, 7 },
            plan.ResourceEvents.Select(static row => row.Before));
        Assert.Equal(new decimal[] { 7, 5 },
            plan.ResourceEvents.Select(static row => row.After));
        Assert.Equal(2, plan.ResourceEvents.Select(static row => row.OperationId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, plan.ResourceEvents.Select(static row => row.EventRef)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            plan.ResourceEvents[0].ExecutionSequence + 1,
            plan.ResourceEvents[1].ExecutionSequence);
        Assert.Equal(
            plan.ResourceEvents.Select(static row => row.EventRef),
            coldPlan.ResourceEvents.Select(static row => row.EventRef));
        Assert.Equal(
            plan.ResourceEvents.Select(static row => row.OperationId),
            coldPlan.ResourceEvents.Select(static row => row.OperationId));
        Assert.Equal(
            plan.ResourceEvents.Select(static row => row.SourceFingerprint),
            coldPlan.ResourceEvents.Select(static row => row.SourceFingerprint));
        Assert.Equal(
            plan.ResourceEvents.Select(static row => row.ExecutionSequence),
            coldPlan.ResourceEvents.Select(static row => row.ExecutionSequence));
        Assert.Equal(
            plan.ResourceEvents.Select(static row => row.Before),
            coldPlan.ResourceEvents.Select(static row => row.Before));
        Assert.Equal(
            plan.ResourceEvents.Select(static row => row.After),
            coldPlan.ResourceEvents.Select(static row => row.After));
        Assert.Equal(
            CanonicalValue(plan.StateAfterImage),
            CanonicalValue(coldPlan.StateAfterImage));
        Assert.Equal(
            CanonicalValue(plan.HistoryAfterImage),
            CanonicalValue(coldPlan.HistoryAfterImage));
    }

    [Fact]
    public void GuaranteedResourceQuantity_UnmarkedAccumulatedNormalizerRejectsBeforeTakingOrWriting()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "unmarked_accumulated_fence");
        var plan = ComposeResourcePublication(fixture, flow);
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);

        var exception = Record.Exception(() => new CanonicalStateNormalizer(
                fixture.FileSystem,
                NullLogger<CanonicalStateNormalizer>.Instance)
            .BindTo(fixture.Lease)
            .NormalizeAccumulatedStateWithPlanAsync(
                new Dictionary<string, string>(StringComparer.Ordinal))
            .GetAwaiter()
            .GetResult());

        Assert.NotNull(exception);
        Assert.Contains("transaction", exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Same(plan, PeekCachedPlan(fixture));
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
        Assert.Equal(10, ReadPlayerEnergy(fixture));

        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        Assert.Same(plan, publication.Plan);
        publication.Compensate();
        Assert.Same(plan, PeekCachedPlan(fixture));
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
    }

    [Fact]
    public void GuaranteedResourceQuantity_OpenTransactionTokenRejectsSwapAndReplay()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var firstFixture = AcceptedStateFixture.Create(scenario);
        using var secondFixture = AcceptedStateFixture.Create(scenario);
        firstFixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        secondFixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var firstFlow = PersistAndRehydrateResourcePublication(
            firstFixture,
            scenario,
            "token_first");
        var secondFlow = PersistAndRehydrateResourcePublication(
            secondFixture,
            scenario,
            "token_second");
        var firstPlan = ComposeResourcePublication(firstFixture, firstFlow);
        var secondPlan = ComposeResourcePublication(secondFixture, secondFlow);
        var firstHistoryCount = ReadResourceHistory(firstFixture).Transitions.Count;
        IReadOnlyDictionary<string, byte[]> firstPublishedTree =
            new Dictionary<string, byte[]>(StringComparer.Ordinal);
        IReadOnlyDictionary<string, byte[]> secondPublishedTree =
            new Dictionary<string, byte[]>(StringComparer.Ordinal);

        using var firstPublication = PublishCachedResourcePlanOpen(
            firstFixture,
            firstFlow,
            firstPlan);
        using (var secondPublication = PublishCachedResourcePlanOpen(
                   secondFixture,
                   secondFlow,
                   secondPlan))
        {
            firstPublishedTree = CaptureResolverFixtureTree(firstFixture.Root);
            secondPublishedTree = CaptureResolverFixtureTree(secondFixture.Root);

            AssertResourceTransactionOperationRejected(
                firstFixture,
                secondPublication.TransactionToken,
                "complete",
                TransactionTokenMismatchCode,
                "TokenMismatch");

            AssertResolverFixtureTreeUnchanged(firstFixture.Root, firstPublishedTree);
            AssertResolverFixtureTreeUnchanged(secondFixture.Root, secondPublishedTree);
            firstPublication.CompleteAtFullPipelineEnd();
            Assert.Equal(8, ReadPlayerEnergy(firstFixture));
            Assert.Equal(
                firstHistoryCount + 1,
                ReadResourceHistory(firstFixture).Transitions.Count);

            var committedTree = CaptureResolverFixtureTree(firstFixture.Root);
            AssertResourceTransactionOperationRejected(
                firstFixture,
                firstPublication.TransactionToken,
                "complete",
                TransactionTokenReplayedCode,
                "TokenReplayed");
            AssertResolverFixtureTreeUnchanged(firstFixture.Root, committedTree);
            Assert.Equal(
                firstHistoryCount + 1,
                ReadResourceHistory(firstFixture).Transitions.Count);
        }

        Assert.Equal(10, ReadPlayerEnergy(secondFixture));
        Assert.Same(secondPlan, PeekCachedPlan(secondFixture));
    }

    [Theory]
    [InlineData("receipt_fence_invalidation")]
    [InlineData("released_hold")]
    public void GuaranteedResourceQuantity_CommitConflictRollsBackAndDoesNotExposePartialState(
        string axis)
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(2);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "commit_conflict_" + axis);
        var plan = ComposeResourcePublication(fixture, flow);
        var canonicalBefore = CaptureResolverFixtureTree(fixture.Root);
        var commandBefore = ReadCanonicalBytes(
            fixture,
            AcceptedMechanicsPlan.WoundCommandPath);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);

        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        if (string.Equals(axis, "receipt_fence_invalidation", StringComparison.Ordinal))
        {
            AcceptedMechanicsPlanAuthority.InvalidateValidated(
                fixture.FileSystem,
                fixture.Lease);
        }
        else
        {
            var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                fixture.GetAcceptedState());
            var released = acceptedState.ReleaseTreatmentResources(
                ResourceLifecycleCapability(),
                new[] { request },
                "cancelled");
            Assert.True(released.IsValid, DescribeIssues(released.Issues));
            Assert.Equal(1, released.ChangedCount);
        }

        AssertResourceTransactionOperationRejected(
            fixture,
            publication.TransactionToken,
            "complete",
            string.Equals(axis, "receipt_fence_invalidation", StringComparison.Ordinal)
                ? TransactionStaleCode
                : TransactionReservationChangedCode,
            string.Equals(axis, "receipt_fence_invalidation", StringComparison.Ordinal)
                ? "Stale"
                : "ReservationChanged");

        if (string.Equals(axis, "receipt_fence_invalidation", StringComparison.Ordinal))
        {
            publication.CompensateWithOutcome("HeldBlocked");
            Assert.Equal(commandBefore, ReadCanonicalBytes(
                fixture,
                AcceptedMechanicsPlan.WoundCommandPath));
            AssertResolverFixtureTreeUnchanged(fixture.Root, canonicalBefore);
            AssertConfirmedHeldResourceAgreement(fixture, flow.Request);
            AssertTreatmentHoldBlocksCompetingReservation(
                fixture,
                scenario,
                "invalidated_fence_hold_proof");
            AssertPublicationBlocked(fixture, flow);
        }
        else
        {
            publication.CompensateWithOutcome("CommandQuarantined");
            AssertDurableTreatmentSurfacesQuarantined(fixture, request);
            AssertCanonicalTreeEqualExceptDurableTreatmentSurfaces(
                fixture,
                canonicalBefore);
        }

        Assert.Equal(2, ReadPlayerEnergy(fixture));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
    }

    [Fact]
    public void GuaranteedResourceQuantity_FinalCommitRejectsDriftedPublishedAgreementBeforeReservationCommit()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "final_commit_published_agreement_drift");
        var plan = ComposeResourcePublication(fixture, flow);
        var canonicalBefore = CaptureResolverFixtureTree(fixture.Root);

        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        Assert.Equal(8, ReadPlayerEnergy(fixture));
        WriteCanonicalPlayerEnergyAuthority(fixture.FileSystem, current: 7);

        AssertResourceTransactionOperationRejected(
            fixture,
            publication.TransactionToken,
            "complete",
            PublishedAgreementChangedCode,
            "PublishedAgreementChanged");
        AssertConfirmedHeldResourceAgreement(fixture, flow.Request);

        publication.Compensate();
        Assert.Equal(10, ReadPlayerEnergy(fixture));
        AssertExactCachedPlanAndBinding(
            fixture,
            plan,
            publication.OriginalBinding);
        AssertResolverFixtureTreeUnchanged(fixture.Root, canonicalBefore);
    }

    [Fact]
    public void GuaranteedResourceQuantity_CompetingOrdinaryAdmissionSurvivesStaleCompensationAndBlocksRestart()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(2);
        fixture.EnsureRawResourceMaterializationSnapshotCoverage();
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "competing_ordinary_admission");
        var plan = ComposeResourcePublication(fixture, flow);
        var canonicalBefore = CaptureResolverFixtureTree(fixture.Root);
        var durableBefore = CaptureDurableTreatmentSurfaceBytes(fixture);

        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        AcceptedMechanicsPlanAuthority.InvalidateValidated(
            fixture.FileSystem,
            fixture.Lease);
        RestoreResolverFixtureTree(fixture, canonicalBefore);
        RemoveDurableTreatmentSurfaces(fixture, durableBefore.Keys);
        var competing = AdmitCompetingCommonPlanFromLiveState(fixture);
        Assert.NotSame(plan, competing.Plan);
        RestoreDurableTreatmentSurfaceBytes(fixture, durableBefore);

        publication.CompensateWithOutcome("HeldBlocked");

        AssertDurableTreatmentSurfaceBytesEqual(fixture, durableBefore);
        AssertResolverFixtureTreeUnchanged(fixture.Root, canonicalBefore);
        AssertConfirmedHeldResourceAgreement(fixture, flow.Request);
        AssertExactCachedPlanAndBinding(
            fixture,
            competing.Plan,
            competing.Binding);
        AssertPublicationBlocked(fixture, flow);
    }

    [Fact]
    public void GuaranteedResourceQuantity_ColdConfirmedHeldCommandPublishesBeforeAnyAcceptedHistory()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var warmFixture = AcceptedStateFixture.Create(scenario);
        warmFixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        _ = PersistAndRehydrateResourcePublication(
            warmFixture,
            scenario,
            "cold_confirmed_before_publication");
        using var coldFixture = CreateColdRootCopy(warmFixture);
        var catalog = Assert.IsType<MortalWoundTreatmentPersistedRequestCatalogResult>(
            RestoreCurrentPersistedTreatmentCatalog(coldFixture));
        var restored = Assert.Single(AssertValidPersistedCatalog(
            catalog,
            "cold confirmed B.3 command"));
        Assert.Single(catalog.HeldRequests);
        Assert.Empty(catalog.FinalizedRequests);
        var coldFlow = RehydratePersistedTreatment(
            coldFixture,
            "guaranteed",
            restored);
        AssertConfirmedHeldResourceAgreement(coldFixture, coldFlow.Request);
        var historyBefore = ReadResourceHistory(coldFixture).Transitions.Count;

        var plan = ComposeResourcePublication(coldFixture, coldFlow);
        using var publication = PublishCachedResourcePlanOpen(
            coldFixture,
            coldFlow,
            plan);
        Assert.Equal(8, ReadPlayerEnergy(coldFixture));
        publication.CompleteAtFullPipelineEnd();

        var finalizedBytes = CaptureResourceBytes(coldFixture);
        Assert.Equal(historyBefore + 1, ReadResourceHistory(coldFixture).Transitions.Count);
        coldFixture.PrepareFreshSnapshot("cold_resource_publication_history_replay");
        using var replayFixture = CreateColdRootCopy(coldFixture);
        var replayCatalog = Assert.IsType<MortalWoundTreatmentPersistedRequestCatalogResult>(
            RestoreCurrentPersistedTreatmentCatalog(replayFixture));
        Assert.True(replayCatalog.IsValid, DescribeIssues(replayCatalog.Issues));
        Assert.Empty(replayCatalog.HeldRequests);
        Assert.Single(replayCatalog.FinalizedRequests);
        var replay = ProbePublishedTreatment(replayFixture, coldFlow.Request);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(
            replay,
            "Status")));
        AssertResourceBytesEqual(replayFixture, finalizedBytes);
        Assert.Equal(historyBefore + 1, ReadResourceHistory(replayFixture).Transitions.Count);
    }

    [Fact]
    public void GuaranteedResourceQuantity_DurableCommandProofFailureQuarantinesCommandAndNeverRearms()
    {
        var fault = new DurableCommandProofFailureInjection();
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(
            scenario,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationAsync = fault.BeforeCanonicalMutationAsync,
                AfterPhysicalFilePublishedAsync = fault.AfterPhysicalFilePublishedAsync
            });
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(2);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "durable_command_proof_failure");
        var plan = ComposeResourcePublication(fixture, flow);
        var canonicalBefore = CaptureResolverFixtureTree(fixture.Root);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var durableBefore = CaptureDurableTreatmentSurfaceBytes(fixture);
        AssertDurableTreatmentSurfacesContainRequest(durableBefore, request);

        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        fault.Arm(fixture.FileSystem);
        publication.CompensateWithOutcome("CommandQuarantined");

        Assert.True(fault.Fired);
        Assert.Equal(2, ReadPlayerEnergy(fixture));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        AssertDurableTreatmentSurfacesQuarantined(fixture, request);
        AssertCanonicalTreeEqualExceptDurableTreatmentSurfaces(
            fixture,
            canonicalBefore);
        AssertTreatmentReservationReleased(
            fixture,
            request,
            "validation_failed");
        AssertTreatmentHoldReleasedAllowsCompetingReservation(
            fixture,
            scenario,
            "durable_command_proof_terminal_release");
    }

    [Fact]
    public void GuaranteedResourceQuantity_TerminalValidationFailureRemovesDurableRequestReleasesHoldAndNeverRearms()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(2);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "terminal_release_success");
        var plan = ComposeResourcePublication(fixture, flow);
        var canonicalBefore = CaptureResolverFixtureTree(fixture.Root);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        AssertDurableTreatmentSurfacesContainRequest(
            CaptureDurableTreatmentSurfaceBytes(fixture),
            request);

        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
            publication.ReleaseTerminal("validation_failed");

        Assert.Equal(2, ReadPlayerEnergy(fixture));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        AssertDurableTreatmentSurfacesQuarantined(fixture, request);
        AssertCanonicalTreeEqualExceptDurableTreatmentSurfaces(
            fixture,
            canonicalBefore);
        AssertTreatmentReservationReleased(
            fixture,
            request,
            "validation_failed");
        AssertTreatmentHoldReleasedAllowsCompetingReservation(
            fixture,
            scenario,
            "terminal_release_success");
    }

    [Fact]
    public void GuaranteedResourceQuantity_RetryableRepairCompensationCanStillSettleTerminalRollback()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(2);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "repair_then_terminal_release");
        var plan = ComposeResourcePublication(fixture, flow);
        var canonicalBefore = CaptureResolverFixtureTree(fixture.Root);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);

        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
        {
            publication.CompensateForRepair();
            AssertExactCachedPlanAndBinding(
                fixture,
                plan,
                publication.OriginalBinding);
            AssertConfirmedHeldResourceAgreement(fixture, request);

            publication.ReleaseTerminal("validation_failed");
        }

        Assert.Equal(2, ReadPlayerEnergy(fixture));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        AssertDurableTreatmentSurfacesQuarantined(fixture, request);
        AssertCanonicalTreeEqualExceptDurableTreatmentSurfaces(
            fixture,
            canonicalBefore);
        AssertTreatmentReservationReleased(
            fixture,
            request,
            "validation_failed");
        AssertTreatmentHoldReleasedAllowsCompetingReservation(
            fixture,
            scenario,
            "repair_then_terminal_release");
    }

    [Fact]
    public void GuaranteedResourceQuantity_QuarantineSelectorPreservesNeighborTextContainingTargetCoordinates()
    {
        const string operationKey = "operation_t070b3_quarantine_target";
        const string attemptId = "attempt_t070b3_quarantine_target";
        const string requestFingerprint =
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string neighborFingerprint =
            "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var targetCommand = CreateQuarantineCommandRow(
            operationKey,
            attemptId,
            requestFingerprint,
            "Target treatment scene.");
        var neighborCommand = CreateQuarantineCommandRow(
            "operation_t070b3_quarantine_neighbor",
            "attempt_t070b3_quarantine_neighbor",
            neighborFingerprint,
            operationKey);
        var command = new JsonObject
        {
            ["commands"] = new JsonArray(targetCommand, neighborCommand)
        };
        var targetPending = CreateQuarantinePendingRow(
            operationKey,
            attemptId,
            requestFingerprint);
        var neighborPending = CreateQuarantinePendingRow(
            "operation_t070b3_quarantine_neighbor",
            "attempt_t070b3_quarantine_neighbor",
            neighborFingerprint);
        neighborPending["note"] = operationKey;
        var pending = new JsonObject
        {
            ["submittedTreatmentRequests"] = new JsonArray(
                targetPending,
                neighborPending)
        };

        var result = MortalWoundTreatmentDurableSurfaceQuarantine.RemoveExactRequestRows(
            command,
            pending,
            operationKey,
            attemptId,
            requestFingerprint);

        Assert.True(result.CommandChanged);
        Assert.True(result.PendingChanged);
        var retainedCommand = Assert.IsType<JsonObject>(Assert.Single(
            command["commands"]!.AsArray()));
        Assert.Equal(
            "operation_t070b3_quarantine_neighbor",
            retainedCommand["operationKey"]!.GetValue<string>());
        Assert.Equal(operationKey, retainedCommand["finalSceneText"]!.GetValue<string>());
        var retainedPending = Assert.IsType<JsonObject>(Assert.Single(
            pending["submittedTreatmentRequests"]!.AsArray()));
        Assert.Equal(
            "operation_t070b3_quarantine_neighbor",
            retainedPending["operationKey"]!.GetValue<string>());
        Assert.Equal(operationKey, retainedPending["note"]!.GetValue<string>());
    }

    [Fact]
    public void GuaranteedResourceQuantity_TerminalLifecycleReleaseRefusalRestoresDurableRequestRetainsHoldAndRequiresRestart()
    {
        var observer = new TerminalDurableSurfaceRemovalObserver();
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(
            scenario,
            new FileSystemManagerHooks
            {
                AfterPhysicalFilePublishedAsync = observer.AfterPhysicalFilePublishedAsync
            });
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(2);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "terminal_lifecycle_release_refusal");
        var plan = ComposeResourcePublication(fixture, flow);
        var canonicalBefore = CaptureResolverFixtureTree(fixture.Root);
        var durableBefore = CaptureDurableTreatmentSurfaceBytes(fixture);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        AssertDurableTreatmentSurfacesContainRequest(
            durableBefore,
            request);

        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
        {
            observer.Arm(fixture.FileSystem, request);
            publication.ExpectTerminalReleaseFailure("rolled_back");
            Assert.True(observer.ObservedCommandRemoval);
            Assert.Equal(
                observer.PendingContainedRequest,
                observer.ObservedPendingRemoval);
            AssertDurableTreatmentSurfaceBytesEqual(fixture, durableBefore);
            AssertResolverFixtureTreeUnchanged(fixture.Root, canonicalBefore);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
                fixture.FileSystem,
                fixture.Lease));
            AssertConfirmedHeldResourceAgreement(fixture, flow.Request);
            AssertTreatmentHoldBlocksCompetingReservation(
                fixture,
                scenario,
                "terminal_release_failure_hold_proof");
            AssertPublicationBlocked(fixture, flow);
        }

        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        AssertPublicationBlocked(fixture, flow);
    }

    [Fact]
    public void GuaranteedResourceQuantity_OpenAcceptedStateRebindKeepsConfirmedHoldThenFinalizesOnceAndColdReplays()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "finalized_history_rebind");
        var plan = ComposeResourcePublication(fixture, flow);
        var historyBefore = ReadResourceHistory(fixture).Transitions.Count;
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
        {
            _ = fixture.GetAcceptedState();
            AssertConfirmedHeldResourceAgreement(fixture, flow.Request);
            Assert.Equal(
                historyBefore + 1,
                ReadResourceHistory(fixture).Transitions.Count);
            publication.CompleteAtFullPipelineEnd();
        }
        var finalizedBytes = CaptureResourceBytes(fixture);

        fixture.PrepareFreshSnapshot("accepted_state_rebind_history_replay");
        using var coldFixture = CreateColdRootCopy(fixture);
        var catalog = Assert.IsType<MortalWoundTreatmentPersistedRequestCatalogResult>(
            RestoreCurrentPersistedTreatmentCatalog(coldFixture));
        Assert.True(catalog.IsValid, DescribeIssues(catalog.Issues));
        Assert.Empty(catalog.HeldRequests);
        Assert.Single(catalog.FinalizedRequests);
        var replay = ProbePublishedTreatment(coldFixture, flow.Request);

        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(
            replay,
            "Status")));
        AssertResourceBytesEqual(coldFixture, finalizedBytes);
        Assert.Equal(
            historyBefore + 1,
            ReadResourceHistory(coldFixture).Transitions.Count);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            coldFixture.FileSystem,
            coldFixture.Lease));
    }

    private static ResolverScenario CreateGuaranteedResourcePublicationScenario(
        IReadOnlyList<int> resourceQuantities,
        IReadOnlyList<int> selectedResourceOrder,
        bool includeReusableItem = false,
        bool selectReusableItem = false)
    {
        var source = CreateScenario(
            "guaranteed_current_capability_proof_stabilizes",
            "guaranteed");
        var before = source.Before.DeepClone().AsObject();
        var woundId = before["woundId"]!.GetValue<string>();
        before["consequences"] = new JsonObject
        {
            ["slotBudget"] = 2,
            ["slotsUsed"] = 1,
            ["ownedEffectSources"] = WoundContractTestData.CreateOwnedEffectSources(
                woundId,
                "mortal_world",
                ("effect_wound_test_pain", "definition_wound_test_pain", "action_control")),
            ["entries"] = new JsonArray(new JsonObject
            {
                ["slot"] = 1,
                ["profileKey"] = "action_control",
                ["effectId"] = "effect_wound_test_pain",
                ["readableSummary"] = "Резкие движения затруднены."
            })
        };
        var route = before["treatment"]!["routes"]![0]!.AsObject();
        var requirements = route["requirements"]!.AsArray();
        foreach (var quantity in resourceQuantities)
        {
            requirements.Add(new JsonObject
            {
                ["kind"] = "resource_quantity",
                ["resourceRef"] = "energy",
                ["quantity"] = quantity,
                ["ownerRole"] = "target"
            });
        }

        int? reusableIndex = null;
        if (includeReusableItem)
        {
            reusableIndex = requirements.Count;
            requirements.Add(new JsonObject
            {
                ["kind"] = "item_quantity",
                ["itemRef"] = "reusable_field_kit",
                ["quantity"] = 1,
                ["ownerRole"] = "provider"
            });
            source.AcceptedState["reusableToolCount"] = 1;
        }

        var mutations = new JsonArray();
        foreach (var resourceOrdinal in selectedResourceOrder)
        {
            mutations.Add(new JsonObject
            {
                ["kind"] = "consume_requirement",
                ["scope"] = "common",
                ["milestoneOrdinal"] = null,
                ["requirementIndex"] = checked(resourceOrdinal + 1)
            });
        }
        if (selectReusableItem)
        {
            mutations.Add(new JsonObject
            {
                ["kind"] = "consume_requirement",
                ["scope"] = "common",
                ["milestoneOrdinal"] = null,
                ["requirementIndex"] = Assert.IsType<int>(reusableIndex)
            });
        }
        route["resourcePolicy"]!["mutations"] = mutations;
        return source with
        {
            Before = before,
            History = CreateCurrentWoundHistory(before),
            SeedCanonicalWoundEffects = true,
            OperationKey = source.OperationKey + "_resource_publication"
        };
    }

    private static TreatmentFlow PersistAndRehydrateResourcePublication(
        AcceptedStateFixture fixture,
        ResolverScenario scenario,
        string suffix)
    {
        var initial = ResolveCurrentTreatment(
            fixture,
            "guaranteed",
            scenario.OperationKey + "_" + suffix,
            scenario.RouteId);
        PersistTreatmentCommand(
            fixture,
            ComposeTreatmentCommand(
                initial,
                "The accepted guaranteed treatment retains its exact resource hold."));
        var restored = Assert.Single(AssertValidPersistedCatalog(
            RestoreCurrentPersistedTreatmentCatalog(fixture),
            suffix + " persisted resource publication"));
        var rehydrated = RehydratePersistedTreatment(
            fixture,
            "guaranteed",
            restored);
        Assert.Equal(CanonicalValue(initial.Request), CanonicalValue(rehydrated.Request));
        Assert.Equal(CanonicalValue(initial.Resolution), CanonicalValue(rehydrated.Resolution));
        return rehydrated;
    }

    private static WoundAcceptedTurnPlanner.MortalWoundTreatmentPublicationResult
        ComposeResourcePublicationResult(
            AcceptedStateFixture fixture,
            TreatmentFlow flow) =>
        WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(
            fixture.FileSystem,
            fixture.Lease,
            new GameResponse(),
            Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(flow.AcceptedState),
            Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request),
            Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution));

    private static AcceptedMechanicsPlan ComposeResourcePublication(
        AcceptedStateFixture fixture,
        TreatmentFlow flow)
    {
        Assert.Contains(
            "effect_wound_test_pain",
            fixture.ReadActivePlayerEffectIds());
        var result = ComposeResourcePublicationResult(fixture, flow);
        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        Assert.Empty(result.Issues);
        return Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
    }

    private static AcceptedMechanicsPlan PeekCachedPlan(AcceptedStateFixture fixture)
    {
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out var cached));
        Assert.True(cached.Success, DescribeIssues(cached.Issues));
        return Assert.IsType<AcceptedMechanicsPlan>(cached.Plan);
    }

    private static void AssertExactCachedPlanAndBinding(
        AcceptedStateFixture fixture,
        AcceptedMechanicsPlan expectedPlan,
        AcceptedMechanicsPlanBinding expectedBinding)
    {
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out var actualBinding,
            out var cached));
        Assert.True(cached.Success, DescribeIssues(cached.Issues));
        Assert.Same(expectedPlan, cached.Plan);
        Assert.Equal(
            AcceptedMechanicsPlanFingerprints.ComputeInput(expectedBinding),
            AcceptedMechanicsPlanFingerprints.ComputeInput(actualBinding));
    }

    private static (AcceptedMechanicsPlan Plan, AcceptedMechanicsPlanBinding Binding)
        AdmitCompetingCommonPlanFromLiveState(AcceptedStateFixture fixture)
    {
        fixture.ReleaseLeaseForExternalDistribution();
        IReadOnlyList<ValidationIssue> issues;
        try
        {
            issues = new ValidationService(
                    fixture.FileSystem,
                    NullLogger<ValidationService>.Instance)
                .ValidateAcceptedTurnRawResourceMaterializationAsync()
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            fixture.ReacquireLeaseAfterExternalDistribution();
        }

        Assert.False(
            issues.Any(static issue => issue.Severity == IssueSeverity.Error),
            DescribeIssues(issues));
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out var binding,
            out var cached));
        Assert.True(cached.Success, DescribeIssues(cached.Issues));
        return (Assert.IsType<AcceptedMechanicsPlan>(cached.Plan), binding);
    }

    private static ResourcePublicationTransactionScope PublishCachedResourcePlanOpen(
        AcceptedStateFixture fixture,
        TreatmentFlow flow,
        AcceptedMechanicsPlan expectedPlan)
    {
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out var originalBinding,
            out var cached));
        Assert.Same(expectedPlan, cached.Plan);
        fixture.ReleaseLeaseForExternalDistribution();
        AcceptedTurnCanonicalStateRefresh.Result result;
        try
        {
            result = AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    fixture.FileSystem,
                    new CanonicalStateNormalizer(
                        fixture.FileSystem,
                        NullLogger<CanonicalStateNormalizer>.Instance),
                    new ValidationService(
                        fixture.FileSystem,
                        NullLogger<ValidationService>.Instance),
                    new Dictionary<string, string>(StringComparer.Ordinal))
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            fixture.ReacquireLeaseAfterExternalDistribution();
        }

        var carrier = FindOpenTreatmentTransactionCarrier(result);
        var scope = carrier.TransactionToken is null
            ? null
            : new ResourcePublicationTransactionScope(
                fixture,
                expectedPlan,
                originalBinding,
                carrier.TransactionToken);
        try
        {
            Assert.True(
                carrier.CandidateProperties.Count == 1,
                "AcceptedTurnCanonicalStateRefresh.Result must carry exactly one " +
                "internal opaque open held-treatment transaction. Found: " +
                string.Join(", ", carrier.CandidateProperties.Select(static property =>
                    property.Name + ":" + property.PropertyType.FullName)));
            Assert.False(carrier.HasMultipleNonNullTokens);
            Assert.NotNull(scope);
            var transaction = scope.TransactionToken;
            Assert.Empty(result.Issues);
            var plan = Assert.IsType<AcceptedMechanicsPlan>(result.MechanicsPlan);
            Assert.Same(expectedPlan, plan);
            Assert.Equal(
                "held",
                Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request)
                    .ResourceAuthority.ReservationDisposition);
            Assert.Empty(transaction.GetType().GetConstructors(
                BindingFlags.Instance | BindingFlags.Public));
            var publishedTree = CaptureResolverFixtureTree(fixture.Root);
            var probe = InvokeResourcePublicationTransactionOperation(
                fixture,
                transaction,
                "probe");
            AssertConfirmedHeldTransactionProbe(flow, probe);
            AssertResolverFixtureTreeUnchanged(fixture.Root, publishedTree);
            return scope;
        }
        catch (Exception original)
        {
            try
            {
                scope?.Dispose();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(
                    "Open treatment publication failed and compensation also failed.",
                    original,
                    cleanup);
            }
            throw;
        }
    }

    private static OpenTreatmentTransactionCarrierProbe
        FindOpenTreatmentTransactionCarrier(object result)
    {
        var transactionProperties = result.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(static property =>
                property.PropertyType != typeof(AcceptedMechanicsPlan) &&
                property.PropertyType != typeof(IReadOnlyList<ValidationIssue>))
            .Where(static property =>
                property.Name.Contains("transaction", StringComparison.OrdinalIgnoreCase) ||
                HasTransactionLifecycle(property.PropertyType))
            .ToArray();
        var nonNullTokens = transactionProperties
            .Select(property => property.GetValue(result))
            .Where(static value => value is not null)
            .ToArray();
        return new OpenTreatmentTransactionCarrierProbe(
            transactionProperties,
            nonNullTokens.FirstOrDefault(),
            nonNullTokens.Length > 1);
    }

    private static bool HasTransactionLifecycle(Type type)
    {
        var methods = type.GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return methods.Any(static method => IsCompletionMethodName(method.Name)) &&
               methods.Any(static method => IsCompensationMethodName(method.Name));
    }

    private static bool IsCompletionMethodName(string name) =>
        name.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("commit", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("finalize", StringComparison.OrdinalIgnoreCase);

    private static bool IsCompensationMethodName(string name) =>
        name.Contains("compensate", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("rollback", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("abort", StringComparison.OrdinalIgnoreCase);

    private static object? InvokeResourcePublicationTransactionOperation(
        AcceptedStateFixture fixture,
        object transactionToken,
        string operation)
    {
        var expectedWord = operation switch
        {
            "probe" => "Probe",
            "complete" => "Complete",
            "compensate" => "Compensate",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
        var tokenType = transactionToken.GetType();
        var candidates = typeof(AcceptedTurnCanonicalStateRefresh)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.Name.Contains(expectedWord, StringComparison.Ordinal))
            .Where(method =>
                method.Name.Contains("Transaction", StringComparison.Ordinal) ||
                method.Name.Contains("Publication", StringComparison.Ordinal))
            .Where(method =>
            {
                var parameters = method.GetParameters();
                return parameters.Length == 2 &&
                       parameters[0].ParameterType == typeof(FileSystemManager) &&
                       parameters[1].ParameterType == tokenType;
            })
            .ToArray();
        var seam = Assert.Single(candidates);
        Assert.False(seam.IsPublic);

        fixture.ReleaseLeaseForExternalDistribution();
        try
        {
            object? invoked;
            try
            {
                invoked = seam.Invoke(null, new[]
                {
                    fixture.FileSystem,
                    transactionToken
                });
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
            return AwaitReflectedTransactionOperation(invoked);
        }
        finally
        {
            fixture.ReacquireLeaseAfterExternalDistribution();
        }
    }

    private static object? InvokeResourcePublicationTerminalRelease(
        AcceptedStateFixture fixture,
        object transactionToken,
        string reason)
    {
        var tokenType = transactionToken.GetType();
        var candidates = typeof(AcceptedTurnCanonicalStateRefresh)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.Name.Contains("Terminal", StringComparison.Ordinal))
            .Where(method => method.Name.Contains("Release", StringComparison.Ordinal))
            .Where(method =>
            {
                var parameters = method.GetParameters();
                return parameters.Length == 3 &&
                       parameters[0].ParameterType == typeof(FileSystemManager) &&
                       parameters[1].ParameterType == tokenType &&
                       parameters[2].ParameterType == typeof(string);
            })
            .ToArray();
        var seam = Assert.Single(candidates);
        Assert.False(seam.IsPublic);

        fixture.ReleaseLeaseForExternalDistribution();
        try
        {
            object? invoked;
            try
            {
                invoked = seam.Invoke(null, new object?[]
                {
                    fixture.FileSystem,
                    transactionToken,
                    reason
                });
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
            return AwaitReflectedTransactionOperation(invoked);
        }
        finally
        {
            fixture.ReacquireLeaseAfterExternalDistribution();
        }
    }

    private static object? AwaitReflectedTransactionOperation(object? value)
    {
        if (value is null)
            return null;
        if (value is Task task)
        {
            task.GetAwaiter().GetResult();
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }
        if (value is ValueTask valueTask)
        {
            valueTask.AsTask().GetAwaiter().GetResult();
            return null;
        }
        var asTask = value.GetType().GetMethod(
            "AsTask",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        if (asTask?.Invoke(value, null) is Task reflectedTask)
        {
            reflectedTask.GetAwaiter().GetResult();
            return reflectedTask.GetType().GetProperty("Result")?.GetValue(reflectedTask);
        }
        return value;
    }

    private static object ReadRequiredTransactionProperty(object result, string name)
    {
        var property = result.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<object>(property.GetValue(result));
    }

    private static void AssertTypedTransactionSuccess(
        object? result,
        int expectedChangedCount,
        string expectedOutcome)
    {
        var typed = Assert.IsAssignableFrom<object>(result);
        var issues = Assert.IsAssignableFrom<IEnumerable<ValidationIssue>>(
            ReadRequiredTransactionProperty(typed, "Issues")).ToArray();
        var outcome = Convert.ToString(
            ReadRequiredTransactionProperty(typed, "Outcome"));
        Assert.True(
            Assert.IsType<bool>(ReadRequiredTransactionProperty(typed, "IsValid")),
            $"transaction outcome={outcome}; issues={DescribeIssues(issues)}");
        Assert.Empty(issues);
        Assert.Equal(expectedChangedCount, Assert.IsType<int>(
            ReadRequiredTransactionProperty(typed, "ChangedCount")));
        Assert.Equal(expectedOutcome, outcome);
    }

    private static void AssertConfirmedHeldTransactionProbe(
        TreatmentFlow flow,
        object? result)
    {
        var typed = Assert.IsAssignableFrom<object>(result);
        Assert.True(Assert.IsType<bool>(ReadRequiredTransactionProperty(
            typed,
            "IsValid")));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ValidationIssue>>(
            ReadRequiredTransactionProperty(typed, "Issues")));
        Assert.Equal(0, Assert.IsType<int>(ReadRequiredTransactionProperty(
            typed,
            "ChangedCount")));
        Assert.Equal("ConfirmedHeld", Convert.ToString(
            ReadRequiredTransactionProperty(typed, "State")));
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var finalized = MortalWoundTreatmentResourceComposer.Finalize(resolution);
        Assert.True(finalized.IsValid, DescribeIssues(finalized.Issues));
        Assert.Equal(request.RequestFingerprint, Convert.ToString(
            ReadRequiredTransactionProperty(typed, "RequestFingerprint")));
        Assert.Equal(finalized.Finalization!.FinalizationFingerprint, Convert.ToString(
            ReadRequiredTransactionProperty(typed, "FinalizationFingerprint")));
    }

    private static void AssertResourceTransactionOperationRejected(
        AcceptedStateFixture fixture,
        object transactionToken,
        string operation,
        string expectedCode,
        string expectedOutcome)
    {
        var result = InvokeResourcePublicationTransactionOperation(
            fixture,
            transactionToken,
            operation);
        AssertTypedTransactionFailure(result, expectedCode, expectedOutcome);
    }

    private static void AssertTypedTransactionFailure(
        object? result,
        string expectedCode,
        string expectedOutcome)
    {
        var typed = Assert.IsAssignableFrom<object>(result);
        Assert.False(Assert.IsType<bool>(ReadRequiredTransactionProperty(
            typed,
            "IsValid")));
        Assert.Equal(0, Assert.IsType<int>(ReadRequiredTransactionProperty(
            typed,
            "ChangedCount")));
        var issues = Assert.IsAssignableFrom<IEnumerable<ValidationIssue>>(
            ReadRequiredTransactionProperty(typed, "Issues")).ToArray();
        Assert.Contains(issues, issue => string.Equals(
            expectedCode,
            issue.Code,
            StringComparison.Ordinal));
        Assert.Equal(expectedOutcome, Convert.ToString(
            ReadRequiredTransactionProperty(typed, "Outcome")));
    }

    private sealed record OpenTreatmentTransactionCarrierProbe(
        IReadOnlyList<PropertyInfo> CandidateProperties,
        object? TransactionToken,
        bool HasMultipleNonNullTokens);

    private static JsonObject CreateQuarantineCommandRow(
        string operationKey,
        string attemptId,
        string requestFingerprint,
        string finalSceneText)
    {
        var request = CreateQuarantineRequestCoordinates(
            operationKey,
            attemptId,
            requestFingerprint);
        return new JsonObject
        {
            ["kind"] = "accepted_transition",
            ["transitionKind"] = "treat",
            ["commandRef"] = "command_ref_" + operationKey,
            ["operationKey"] = operationKey,
            ["authority"] = new JsonObject
            {
                ["request"] = request.DeepClone()
            },
            ["result"] = new JsonObject
            {
                ["requestAuthority"] = request.DeepClone()
            },
            ["finalSceneText"] = finalSceneText
        };
    }

    private static JsonObject CreateQuarantinePendingRow(
        string operationKey,
        string attemptId,
        string requestFingerprint) => new()
    {
        ["operationKey"] = operationKey,
        ["attemptId"] = attemptId,
        ["requestFingerprint"] = requestFingerprint,
        ["request"] = CreateQuarantineRequestCoordinates(
            operationKey,
            attemptId,
            requestFingerprint)
    };

    private static JsonObject CreateQuarantineRequestCoordinates(
        string operationKey,
        string attemptId,
        string requestFingerprint) => new()
    {
        ["coordinates"] = new JsonObject
        {
            ["operationKey"] = operationKey,
            ["attemptId"] = attemptId
        },
        ["requestFingerprint"] = requestFingerprint
    };

    private sealed class ResourcePublicationTransactionScope : IDisposable
    {
        private readonly AcceptedStateFixture _fixture;
        private bool _open = true;

        internal ResourcePublicationTransactionScope(
            AcceptedStateFixture fixture,
            AcceptedMechanicsPlan plan,
            AcceptedMechanicsPlanBinding originalBinding,
            object transactionToken)
        {
            _fixture = fixture;
            Plan = plan;
            OriginalBinding = originalBinding;
            TransactionToken = transactionToken;
        }

        internal AcceptedMechanicsPlan Plan { get; }
        internal AcceptedMechanicsPlanBinding OriginalBinding { get; }
        internal object TransactionToken { get; }

        internal void CompleteAtFullPipelineEnd()
        {
            Assert.True(_open);
            object? result;
            try
            {
                result = InvokeResourcePublicationTransactionOperation(
                    _fixture,
                    TransactionToken,
                    "complete");
            }
            finally
            {
                _open = false;
            }
            AssertTypedTransactionSuccess(result, 1, "Finalized");
        }

        internal void Compensate()
        {
            Assert.True(_open);
            object? result;
            try
            {
                result = InvokeResourcePublicationTransactionOperation(
                    _fixture,
                    TransactionToken,
                    "compensate");
            }
            finally
            {
                _open = false;
            }
            AssertTypedTransactionSuccess(result, 1, "Rearmed");
            AssertExactRearmedPlan();
        }

        internal void CompensateForRepair()
        {
            Assert.True(_open);
            var result = InvokeResourcePublicationTransactionOperation(
                _fixture,
                TransactionToken,
                "compensate");
            AssertTypedTransactionSuccess(result, 1, "Rearmed");
            AssertExactRearmedPlan();
        }

        internal void CompensateWithOutcome(string expectedOutcome)
        {
            Assert.True(_open);
            object? result;
            try
            {
                result = InvokeResourcePublicationTransactionOperation(
                    _fixture,
                    TransactionToken,
                    "compensate");
            }
            finally
            {
                _open = false;
            }
            AssertTypedTransactionSuccess(result, 1, expectedOutcome);
        }

        internal void ReleaseTerminal(string reason)
        {
            Assert.True(_open);
            object? result;
            try
            {
                result = InvokeResourcePublicationTerminalRelease(
                    _fixture,
                    TransactionToken,
                    reason);
            }
            finally
            {
                _open = false;
            }
            AssertTypedTransactionSuccess(result, 1, "Released");
        }

        internal void ExpectTerminalReleaseFailure(string reason)
        {
            Assert.True(_open);
            object? result;
            try
            {
                result = InvokeResourcePublicationTerminalRelease(
                    _fixture,
                    TransactionToken,
                    reason);
            }
            finally
            {
                _open = false;
            }
            AssertTypedTransactionFailure(
                result,
                TerminalReleaseFailureCode,
                "ReleaseFailed");
        }

        public void Dispose()
        {
            if (!_open)
                return;
            var result = InvokeResourcePublicationTransactionOperation(
                _fixture,
                TransactionToken,
                "compensate");
            _open = false;
            var outcome = Convert.ToString(ReadRequiredTransactionProperty(
                Assert.IsAssignableFrom<object>(result),
                "Outcome"));
            if (string.Equals(outcome, "HeldBlocked", StringComparison.Ordinal))
            {
                AssertTypedTransactionSuccess(result, 1, "HeldBlocked");
                return;
            }
            AssertTypedTransactionSuccess(result, 1, "Rearmed");
            AssertExactRearmedPlan();
        }

        private void AssertExactRearmedPlan()
        {
            AssertExactCachedPlanAndBinding(_fixture, Plan, OriginalBinding);
        }
    }

    private static void AssertResourcePlan(
        AcceptedMechanicsPlan plan,
        decimal expectedCurrent,
        int historyBeforeCount,
        IReadOnlyList<decimal> expectedSpendQuantities)
    {
        var definitions = ParseResourceDefinitions(plan.DefinitionAfterImage);
        var state = ResourceStateContract.ParseCanonical(
            plan.StateAfterImage.ToJsonString(),
            definitions,
            allowMissingPristine: false);
        Assert.True(state.IsValid, DescribeIssues(state.Issues));
        var entry = Assert.Single(state.Ledger!.Entries, IsPlayerEnergy);
        Assert.Equal(expectedCurrent, entry.Current);
        var history = ResourceHistoryState.ParseCanonical(
            plan.HistoryAfterImage.ToJsonString(),
            definitions,
            allowMissingPristine: false);
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.Equal(
            historyBeforeCount + expectedSpendQuantities.Count,
            history.History!.Transitions.Count);
        Assert.Equal(expectedSpendQuantities,
            plan.ResourceEvents.Select(static row => row.AppliedAmount));
        Assert.All(plan.ResourceEvents, static row =>
        {
            Assert.Equal("resource_spent", row.EventKind);
            Assert.Equal(ResourceOwnerKind.Player, row.Coordinate.OwnerKind);
            Assert.Equal("player_current", row.Coordinate.ResourceOwnerId);
            Assert.Equal("energy", row.Coordinate.ResourceKey);
            Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
                row.SourceFingerprint));
        });
        Assert.Contains(ResourceMaterializationContract.StatePath, plan.TouchedPaths);
        Assert.Contains(ResourceMaterializationContract.HistoryPath, plan.TouchedPaths);
        Assert.Contains(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            plan.TouchedPaths);
        Assert.True(plan.BeforeImages.ContainsKey(ResourceMaterializationContract.StatePath));
        Assert.True(plan.BeforeImages.ContainsKey(ResourceMaterializationContract.HistoryPath));
    }

    private static ResourceDefinitionCatalog ParseResourceDefinitions(JsonObject root)
    {
        var result = ResourceDefinitionCatalog.ParseCanonical(
            root.ToJsonString(),
            allowMissingPristine: false);
        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        return Assert.IsType<ResourceDefinitionCatalog>(result.Catalog);
    }

    private static ResourceHistoryState ReadResourceHistory(AcceptedStateFixture fixture)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            File.ReadAllText(fixture.FileSystem.ResolvePath(
                ResourceMaterializationContract.DefinitionsPath)),
            allowMissingPristine: false);
        Assert.True(definitions.IsValid, DescribeIssues(definitions.Issues));
        var history = ResourceHistoryState.ParseCanonical(
            File.ReadAllText(fixture.FileSystem.ResolvePath(
                ResourceMaterializationContract.HistoryPath)),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        return Assert.IsType<ResourceHistoryState>(history.History);
    }

    private static decimal ReadPlayerEnergy(AcceptedStateFixture fixture)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            File.ReadAllText(fixture.FileSystem.ResolvePath(
                ResourceMaterializationContract.DefinitionsPath)),
            allowMissingPristine: false);
        Assert.True(definitions.IsValid, DescribeIssues(definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(
            File.ReadAllText(fixture.FileSystem.ResolvePath(
                ResourceMaterializationContract.StatePath)),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(state.IsValid, DescribeIssues(state.Issues));
        return Assert.Single(state.Ledger!.Entries, IsPlayerEnergy).Current;
    }

    private static bool IsPlayerEnergy(ResourceStateEntry entry) =>
        entry.Coordinate.OwnerKind == ResourceOwnerKind.Player &&
        string.Equals(
            entry.Coordinate.ResourceOwnerId,
            "player_current",
            StringComparison.Ordinal) &&
        string.Equals(entry.Coordinate.ResourceKey, "energy", StringComparison.Ordinal);

    private static IReadOnlyList<ResourceTransition> ReadTreatmentResourceSpendTransitions(
        AcceptedStateFixture fixture) => ReadResourceHistory(fixture).Transitions
        .Where(static transition =>
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
                StringComparison.Ordinal))
        .ToArray();

    private static void WriteCanonicalPlayerEnergyAuthority(
        FileSystemManager fileSystem,
        int current)
    {
        const string fingerprintA =
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string fingerprintB =
            "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
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
            fingerprintA);
        var initialized = new ResourceStateSnapshot(
            10,
            10,
            binding,
            ResourceLifecycleState.Active);
        var initialize = new ResourceTransition(
            "transition_energy_initialize",
            "operation_energy_initialize",
            "turn_1:resource:1",
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
                fingerprintA),
            fingerprintA,
            null,
            1);
        var transitions = new List<ResourceTransition> { initialize };
        ResourceTransition? latest = null;
        if (current != 10)
        {
            latest = new ResourceTransition(
                "transition_energy_spend",
                "operation_energy_spend",
                "turn_2:resource:1",
                "action_cost",
                "wound_fixture_energy",
                ResourceMutationPhase.DirectOutcome,
                100,
                0,
                coordinate,
                ResourceTransitionOperation.Spend,
                10 - current,
                10 - current,
                ResourceTransitionOutcome.Applied,
                null,
                initialized,
                initialized with { Current = current },
                new ResourceSourceEvidence(
                    "action_cost",
                    "wound_fixture_energy",
                    fingerprintB),
                fingerprintB,
                null,
                2);
            transitions.Add(latest);
        }
        var historyResult = ResourceHistoryState.CreateValidated(
            transitions,
            definitions);
        Assert.True(historyResult.IsValid, DescribeIssues(historyResult.Issues));
        var state = new ResourceStateLedger(new[]
        {
            new ResourceStateEntry(
                coordinate,
                current,
                10,
                binding,
                ResourceLifecycleState.Active,
                new ResourceChronology(
                    1,
                    initialize.EventRef,
                    (latest ?? initialize).TransitionId,
                    (latest ?? initialize).EventRef,
                    (latest ?? initialize).Turn))
        });
        Assert.Empty(historyResult.History!.ValidateStateAgreement(state));
        File.WriteAllText(
            fileSystem.ResolvePath(ResourceMaterializationContract.DefinitionsPath),
            definitions.ToCanonicalJson());
        File.WriteAllText(
            fileSystem.ResolvePath(ResourceMaterializationContract.StatePath),
            state.ToCanonicalJson());
        File.WriteAllText(
            fileSystem.ResolvePath(ResourceMaterializationContract.HistoryPath),
            historyResult.History.ToCanonicalJson());
        var composed = CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
                definitions,
                path => Task.FromResult<string?>(File.Exists(fileSystem.ResolvePath(path))
                    ? File.ReadAllText(fileSystem.ResolvePath(path))
                    : null),
                state,
                historyResult.History,
                CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap)
            .GetAwaiter()
            .GetResult();
        Assert.True(composed.IsValid, DescribeIssues(composed.Issues));
        Assert.False(string.IsNullOrWhiteSpace(composed.CanonicalAuthorityJson));
        File.WriteAllText(
            fileSystem.ResolvePath(CanonicalResourceOwnerAuthorityComposer.AuthorityPath),
            composed.CanonicalAuthorityJson);
    }

    private sealed partial class AcceptedStateFixture
    {
        internal void SetCanonicalPlayerEnergyForResourcePublicationTest(int current)
        {
            WriteCanonicalPlayerEnergyAuthority(FileSystem, current);
            PrepareFreshSnapshot("t070b_energy_" + current);
        }

        internal void EnsureRawResourceMaterializationSnapshotCoverage()
        {
            var missingRoots = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
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
            foreach (var pair in missingRoots)
            {
                if (!File.Exists(FileSystem.ResolvePath(pair.Key)))
                    WriteObject(pair.Key, pair.Value);
            }
            PrepareFreshSnapshot("t070b_raw_resource_coverage");
        }
    }

    private static byte[] ReadCanonicalBytes(
        AcceptedStateFixture fixture,
        string path) => File.ReadAllBytes(fixture.FileSystem.ResolvePath(path));

    private static IReadOnlyDictionary<string, byte[]> CaptureItemCarrierBytes(
        AcceptedStateFixture fixture) => new Dictionary<string, byte[]>(StringComparer.Ordinal)
    {
        ["game_state/npcs/npc_core.json"] = ReadCanonicalBytes(
            fixture,
            "game_state/npcs/npc_core.json"),
        ["game_state/npcs/npc_inventory.json"] = ReadCanonicalBytes(
            fixture,
            "game_state/npcs/npc_inventory.json"),
        ["game_state/inventory/item_identity_index.json"] = ReadCanonicalBytes(
            fixture,
            "game_state/inventory/item_identity_index.json")
    };

    private static void AssertItemCarrierBytesEqual(
        AcceptedStateFixture fixture,
        IReadOnlyDictionary<string, byte[]> expected)
    {
        foreach (var pair in expected)
            Assert.Equal(pair.Value, ReadCanonicalBytes(fixture, pair.Key));
    }

    private static IReadOnlyDictionary<string, CanonicalBeforeImage>
        CaptureDurableTreatmentSurfaceBytes(AcceptedStateFixture fixture) =>
        new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
        {
            [AcceptedMechanicsPlan.WoundCommandPath] = CaptureCanonicalBeforeImage(
                fixture,
                AcceptedMechanicsPlan.WoundCommandPath),
            [WoundAcceptedTurnSnapshotContract.PendingResolutionPath] =
                CaptureCanonicalBeforeImage(
                    fixture,
                    WoundAcceptedTurnSnapshotContract.PendingResolutionPath)
        };

    private static CanonicalBeforeImage CaptureCanonicalBeforeImage(
        AcceptedStateFixture fixture,
        string path)
    {
        var fullPath = fixture.FileSystem.ResolvePath(path);
        return File.Exists(fullPath)
            ? new CanonicalBeforeImage(existed: true, File.ReadAllBytes(fullPath))
            : new CanonicalBeforeImage(existed: false, bytes: null);
    }

    private static void AssertDurableTreatmentSurfaceBytesEqual(
        AcceptedStateFixture fixture,
        IReadOnlyDictionary<string, CanonicalBeforeImage> expected)
    {
        foreach (var pair in expected)
        {
            var actual = CaptureCanonicalBeforeImage(fixture, pair.Key);
            Assert.Equal(pair.Value.Existed, actual.Existed);
            Assert.Equal(pair.Value.Bytes, actual.Bytes);
        }
    }

    private static void RemoveDurableTreatmentSurfaces(
        AcceptedStateFixture fixture,
        IEnumerable<string> paths)
    {
        foreach (var path in paths)
            fixture.FileSystem.DeleteFile(fixture.Lease, path);
    }

    private static void RestoreDurableTreatmentSurfaceBytes(
        AcceptedStateFixture fixture,
        IReadOnlyDictionary<string, CanonicalBeforeImage> expected)
    {
        foreach (var pair in expected)
        {
            if (pair.Value.Existed)
            {
                fixture.FileSystem.WriteFileAtomicBytesAsync(
                        fixture.Lease,
                        pair.Key,
                        Assert.IsType<byte[]>(pair.Value.Bytes))
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                fixture.FileSystem.DeleteFile(fixture.Lease, pair.Key);
            }
        }
    }

    private static void AssertDurableTreatmentSurfacesContainRequest(
        IReadOnlyDictionary<string, CanonicalBeforeImage> surfaces,
        MortalWoundTreatmentAttemptRequest request)
    {
        var commandImage = surfaces[AcceptedMechanicsPlan.WoundCommandPath];
        Assert.True(commandImage.Existed);
        Assert.NotNull(commandImage.Bytes);
        var command = System.Text.Encoding.UTF8.GetString(
            commandImage.Bytes);
        Assert.Contains(request.RequestFingerprint, command, StringComparison.Ordinal);
        Assert.Contains(
            request.Coordinates.OperationKey,
            command,
            StringComparison.Ordinal);

        var pendingImage =
            surfaces[WoundAcceptedTurnSnapshotContract.PendingResolutionPath];
        if (!pendingImage.Existed)
        {
            Assert.Null(pendingImage.Bytes);
            return;
        }
        Assert.NotNull(pendingImage.Bytes);
        var pending = System.Text.Encoding.UTF8.GetString(pendingImage.Bytes);
        Assert.Equal(
            pending.Contains(request.RequestFingerprint, StringComparison.Ordinal),
            pending.Contains(request.Coordinates.OperationKey, StringComparison.Ordinal));
    }

    private static void AssertDurableTreatmentSurfacesQuarantined(
        AcceptedStateFixture fixture,
        MortalWoundTreatmentAttemptRequest request)
    {
        foreach (var relativePath in new[]
                 {
                     AcceptedMechanicsPlan.WoundCommandPath,
                     WoundAcceptedTurnSnapshotContract.PendingResolutionPath
                 })
        {
            var path = fixture.FileSystem.ResolvePath(relativePath);
            if (!File.Exists(path))
                continue;
            var text = File.ReadAllText(path);
            Assert.DoesNotContain(
                request.RequestFingerprint,
                text,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                request.Coordinates.OperationKey,
                text,
                StringComparison.Ordinal);
        }
    }

    private static void AssertTreatmentHoldBlocksCompetingReservation(
        AcceptedStateFixture fixture,
        ResolverScenario scenario,
        string suffix)
    {
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var competing = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(
            acceptedState,
            fixture.ReadCurrentHistory(),
            fixture.ReadCurrentWound(),
            scenario.OperationKey + "_" + suffix,
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.False(competing.IsValid);
        Assert.Null(competing.Request);
        Assert.Equal(
            "mortal_wound_treatment_resource_reservation_overbooked",
            Assert.Single(competing.Issues).Code);
    }

    private static void AssertTreatmentHoldReleasedAllowsCompetingReservation(
        AcceptedStateFixture fixture,
        ResolverScenario scenario,
        string suffix)
    {
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var competing = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(
            acceptedState,
            fixture.ReadCurrentHistory(),
            fixture.ReadCurrentWound(),
            scenario.OperationKey + "_" + suffix,
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.True(competing.IsValid, DescribeIssues(competing.Issues));
        Assert.Empty(competing.Issues);
        Assert.NotNull(competing.Request);
    }

    private static void AssertTreatmentReservationReleased(
        AcceptedStateFixture fixture,
        MortalWoundTreatmentAttemptRequest request,
        string reason)
    {
        var agreements = ReadTreatmentResourceAgreements(fixture);
        Assert.False(agreements.ContainsKey(request.Coordinates.OperationKey));
        var tombstone = ReadTreatmentResourceReleaseTombstone(
            fixture,
            request.Coordinates.OperationKey);
        Assert.Equal(
            request.RequestFingerprint,
            ReadRequiredProperty(tombstone, "RequestFingerprint"));
        Assert.Equal(
            ComputeTreatmentResourceAgreementFingerprint(request),
            ReadRequiredProperty(tombstone, "AgreementFingerprint"));
        Assert.Equal(reason, ReadRequiredProperty(tombstone, "Reason"));
    }

    private static object ReadTreatmentResourceReleaseTombstone(
        AcceptedStateFixture fixture,
        string operationKey)
    {
        var registry = ReadTreatmentResourceReservationRegistry(fixture);
        var released = Assert.IsAssignableFrom<System.Collections.IDictionary>(
            registry.GetType().GetField(
                    "_releasedByOperationKey",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(registry));
        Assert.True(released.Contains(operationKey));
        return Assert.IsAssignableFrom<object>(released[operationKey]);
    }

    private static string ComputeTreatmentResourceAgreementFingerprint(
        MortalWoundTreatmentAttemptRequest request)
    {
        var method = typeof(MortalWoundTreatmentResourceReservationAgreement)
            .GetMethod(
                "ComputeAgreementFingerprint",
                BindingFlags.Static | BindingFlags.NonPublic);
        return Assert.IsType<string>(method?.Invoke(
            null,
            new object[]
            {
                request.Coordinates,
                request.Mode,
                request.ResourceAuthority
            }));
    }

    private static void AssertConfirmedHeldResourceAgreement(
        AcceptedStateFixture fixture,
        object requestCandidate)
    {
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            requestCandidate);
        var getState = typeof(AcceptedTurnAuthorityRegistry).GetMethod(
            "GetState",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[]
            {
                typeof(FileSystemManager),
                typeof(FileSystemManager.CanonicalWriteLease)
            },
            modifiers: null);
        var state = Assert.IsAssignableFrom<object>(getState?.Invoke(
            null,
            new object[] { fixture.FileSystem, fixture.Lease }));
        var registry = Assert.IsType<MortalWoundTreatmentResourceReservationRegistry>(
            state.GetType().GetField(
                    "_treatmentResources",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(state));
        var agreements = ReadTreatmentResourceAgreements(registry);
        Assert.True(agreements.TryGetValue(
            request.Coordinates.OperationKey,
            out var agreement));
        Assert.Equal(
            MortalWoundTreatmentResourceReservationState.ConfirmedHeld,
            agreement.State);
        Assert.Equal(request.RequestFingerprint, agreement.PersistedRequestFingerprint);
        Assert.Equal(
            request.ResourceAuthority.AuthorityFingerprint,
            agreement.AuthorityFingerprint);
    }

    private static IReadOnlyDictionary<
        string,
        MortalWoundTreatmentResourceReservationAgreement>
        ReadTreatmentResourceAgreements(AcceptedStateFixture fixture)
        => ReadTreatmentResourceAgreements(
            ReadTreatmentResourceReservationRegistry(fixture));

    private static MortalWoundTreatmentResourceReservationRegistry
        ReadTreatmentResourceReservationRegistry(AcceptedStateFixture fixture)
    {
        var getState = typeof(AcceptedTurnAuthorityRegistry).GetMethod(
            "GetState",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[]
            {
                typeof(FileSystemManager),
                typeof(FileSystemManager.CanonicalWriteLease)
            },
            modifiers: null);
        var state = Assert.IsAssignableFrom<object>(getState?.Invoke(
            null,
            new object[] { fixture.FileSystem, fixture.Lease }));
        return Assert.IsType<MortalWoundTreatmentResourceReservationRegistry>(
            state.GetType().GetField(
                    "_treatmentResources",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(state));
    }

    private static IReadOnlyDictionary<
        string,
        MortalWoundTreatmentResourceReservationAgreement>
        ReadTreatmentResourceAgreements(
            MortalWoundTreatmentResourceReservationRegistry registry) =>
        Assert.IsAssignableFrom<IReadOnlyDictionary<
            string,
            MortalWoundTreatmentResourceReservationAgreement>>(
            registry.GetType().GetField(
                    "_byOperationKey",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(registry));

    private static void AssertPublicationBlocked(
        AcceptedStateFixture fixture,
        TreatmentFlow flow)
    {
        var rebound = new TreatmentFlow(
            fixture.GetAcceptedState(),
            flow.Request,
            flow.Resolution,
            flow.Before,
            flow.History);
        var blocked = ComposeResourcePublicationResult(fixture, rebound);
        Assert.False(blocked.IsValid);
        Assert.Null(blocked.Plan);
        Assert.Equal(PublicationBlockedCode, Assert.Single(blocked.Issues).Code);
    }

    private static void AssertCanonicalTreeEqualExceptDurableTreatmentSurfaces(
        AcceptedStateFixture fixture,
        IReadOnlyDictionary<string, byte[]> before)
    {
        var after = CaptureResolverFixtureTree(fixture.Root);
        var excluded = new[]
            {
                AcceptedMechanicsPlan.WoundCommandPath,
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath
            }
            .Select(path => Path.GetRelativePath(
                    fixture.Root,
                    fixture.FileSystem.ResolvePath(path))
                .Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            before.Keys.Where(path => !excluded.Contains(path))
                .OrderBy(static path => path, StringComparer.Ordinal),
            after.Keys.Where(path => !excluded.Contains(path))
                .OrderBy(static path => path, StringComparer.Ordinal));
        foreach (var pair in before)
        {
            if (excluded.Contains(pair.Key))
                continue;
            Assert.True(after.TryGetValue(pair.Key, out var bytes), pair.Key);
            Assert.True(pair.Value.AsSpan().SequenceEqual(bytes), pair.Key);
        }
    }

    private static void RestoreResolverFixtureTree(
        AcceptedStateFixture fixture,
        IReadOnlyDictionary<string, byte[]> before)
    {
        var root = fixture.Root;
        var current = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path)
                .StartsWith(".boe_runtime", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                static path => path,
                StringComparer.Ordinal);
        foreach (var extra in current.Where(pair => !before.ContainsKey(pair.Key)))
        {
            fixture.FileSystem.DeleteFile(
                fixture.Lease,
                ResolverFixtureManagerPath(extra.Key));
        }
        foreach (var pair in before)
        {
            var path = Path.Combine(
                root,
                pair.Key.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path) &&
                File.ReadAllBytes(path).AsSpan().SequenceEqual(pair.Value))
            {
                continue;
            }
            fixture.FileSystem.WriteFileAtomicBytesAsync(
                    fixture.Lease,
                    ResolverFixtureManagerPath(pair.Key),
                    pair.Value)
                .GetAwaiter()
                .GetResult();
        }
    }

    private static string ResolverFixtureManagerPath(string capturedPath)
    {
        const string sessionPrefix = "game_session/";
        Assert.StartsWith(sessionPrefix, capturedPath, StringComparison.Ordinal);
        return capturedPath[sessionPrefix.Length..];
    }

    private static IReadOnlyDictionary<string, byte[]> CaptureResourceBytes(
        AcceptedStateFixture fixture) => new Dictionary<string, byte[]>(StringComparer.Ordinal)
    {
        [ResourceMaterializationContract.DefinitionsPath] = ReadCanonicalBytes(
            fixture,
            ResourceMaterializationContract.DefinitionsPath),
        [ResourceMaterializationContract.StatePath] = ReadCanonicalBytes(
            fixture,
            ResourceMaterializationContract.StatePath),
        [ResourceMaterializationContract.HistoryPath] = ReadCanonicalBytes(
            fixture,
            ResourceMaterializationContract.HistoryPath),
        [CanonicalResourceOwnerAuthorityComposer.AuthorityPath] = ReadCanonicalBytes(
            fixture,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath)
    };

    private static void AssertResourceBytesEqual(
        AcceptedStateFixture fixture,
        IReadOnlyDictionary<string, byte[]> expected)
    {
        foreach (var pair in expected)
            Assert.Equal(pair.Value, ReadCanonicalBytes(fixture, pair.Key));
    }

    private sealed class TerminalDurableSurfaceRemovalObserver
    {
        private string? _commandPath;
        private string? _pendingPath;
        private string? _requestFingerprint;

        internal bool ObservedCommandRemoval { get; private set; }
        internal bool ObservedPendingRemoval { get; private set; }
        internal bool PendingContainedRequest { get; private set; }

        internal void Arm(
            FileSystemManager fileSystem,
            MortalWoundTreatmentAttemptRequest request)
        {
            _commandPath = fileSystem.ResolvePath(
                AcceptedMechanicsPlan.WoundCommandPath);
            _pendingPath = fileSystem.ResolvePath(
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            _requestFingerprint = request.RequestFingerprint;
            PendingContainedRequest = File.Exists(_pendingPath) &&
                File.ReadAllText(_pendingPath).Contains(
                    request.RequestFingerprint,
                    StringComparison.Ordinal);
        }

        internal Task AfterPhysicalFilePublishedAsync(string path)
        {
            if (_requestFingerprint is null)
                return Task.CompletedTask;

            if (string.Equals(path, _commandPath, StringComparison.OrdinalIgnoreCase))
            {
                ObservedCommandRemoval = true;
            }
            if (string.Equals(path, _pendingPath, StringComparison.OrdinalIgnoreCase))
            {
                ObservedPendingRemoval = true;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class DurableCommandProofFailureInjection
    {
        private string? _commandPath;
        private bool _commandPublicationObserved;

        internal bool Fired { get; private set; }

        internal void Arm(FileSystemManager fileSystem)
        {
            _commandPath = fileSystem.ResolvePath(
                AcceptedMechanicsPlan.WoundCommandPath);
        }

        internal Task AfterPhysicalFilePublishedAsync(string path)
        {
            if (Fired ||
                _commandPath is null ||
                !string.Equals(path, _commandPath, StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            _commandPublicationObserved = true;
            return Task.CompletedTask;
        }

        internal Task BeforeCanonicalMutationAsync(string path)
        {
            if (Fired || !_commandPublicationObserved || _commandPath is null)
                return Task.CompletedTask;

            Fired = true;
            File.AppendAllText(_commandPath, " ");
            return Task.CompletedTask;
        }
    }

    private sealed class ResourcePublicationFailureInjection
    {
        private string? _failurePath;
        private string? _earlierResourcePath;
        private byte[]? _earlierResourceBytes;
        private FileSystemManager? _fileSystem;

        internal bool Fired { get; private set; }
        internal bool ObservedEarlierResourceWrite { get; private set; }

        internal void Arm(
            string failurePath,
            string earlierResourcePath,
            byte[] earlierResourceBytes,
            FileSystemManager fileSystem)
        {
            _failurePath = failurePath;
            _earlierResourcePath = earlierResourcePath;
            _earlierResourceBytes = earlierResourceBytes.ToArray();
            _fileSystem = fileSystem;
        }

        internal Task BeforeCanonicalMutationAsync(string path)
        {
            if (Fired || !string.Equals(path, _failurePath, StringComparison.Ordinal))
                return Task.CompletedTask;

            Fired = true;
            var resourcePath = Assert.IsType<string>(_earlierResourcePath);
            var before = Assert.IsType<byte[]>(_earlierResourceBytes);
            var fileSystem = Assert.IsType<FileSystemManager>(_fileSystem);
            var physical = fileSystem.ResolvePath(resourcePath);
            ObservedEarlierResourceWrite = File.Exists(physical) &&
                !before.AsSpan().SequenceEqual(File.ReadAllBytes(physical));
            return Task.FromException(new IOException(
                $"Injected B.3 publication failure at '{path}'."));
        }
    }
}
