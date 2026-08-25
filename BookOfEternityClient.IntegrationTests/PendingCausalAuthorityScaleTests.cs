using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PendingCausalAuthorityScaleTests
{
    private const string FingerprintA =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FingerprintB =
        "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void PendingCausalAuthority_DoubledBoundedOutputsVisitEachFingerprintOutputOnce()
    {
        var thirtyTwo = PlanSingleCandidateWithPendingOutputs(32);
        var sixtyFour = PlanSingleCandidateWithPendingOutputs(64);

        Assert.True(thirtyTwo.IsValid, Format(thirtyTwo.Issues));
        Assert.True(sixtyFour.IsValid, Format(sixtyFour.Issues));
        Assert.Equal(32, thirtyTwo.AcceptedPendingResolutions.Count);
        Assert.Equal(64, sixtyFour.AcceptedPendingResolutions.Count);

        var thirtyTwoVisits = ReadWorkCounter(
            thirtyTwo.Statistics,
            "PendingCandidateFingerprintOutputVisitCount");
        var sixtyFourVisits = ReadWorkCounter(
            sixtyFour.Statistics,
            "PendingCandidateFingerprintOutputVisitCount");

        Assert.Equal(32, thirtyTwoVisits);
        Assert.Equal(64, sixtyFourVisits);
        Assert.True(
            sixtyFourVisits <= thirtyTwoVisits * 2.5,
            $"Expected one candidate-fingerprint output visit per bounded output, " +
            $"but {thirtyTwoVisits} visits became {sixtyFourVisits}.");
    }

    [Fact]
    public void PendingCausalAuthority_DoubledAcceptedActivationsExtendsEachTranscriptPrefixOnce()
    {
        var thirtyTwo = PlanCandidatesWithOnePendingOutputEach(32);
        var sixtyFour = PlanCandidatesWithOnePendingOutputEach(64);

        Assert.True(thirtyTwo.IsValid, Format(thirtyTwo.Issues));
        Assert.True(sixtyFour.IsValid, Format(sixtyFour.Issues));
        Assert.Equal(32, thirtyTwo.AcceptedPendingResolutions.Count);
        Assert.Equal(64, sixtyFour.AcceptedPendingResolutions.Count);

        var thirtyTwoVisits = ReadWorkCounter(
            thirtyTwo.Statistics,
            "PendingTranscriptPrefixStampVisitCount");
        var sixtyFourVisits = ReadWorkCounter(
            sixtyFour.Statistics,
            "PendingTranscriptPrefixStampVisitCount");

        Assert.Equal(32, thirtyTwoVisits);
        Assert.Equal(64, sixtyFourVisits);
        Assert.True(
            sixtyFourVisits <= thirtyTwoVisits * 2.5,
            $"Expected one rolling transcript-prefix extension per accepted stamp, " +
            $"but {thirtyTwoVisits} visits became {sixtyFourVisits}.");
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("dependencies")]
    [InlineData("event_requirements")]
    [InlineData("derived_amount")]
    [InlineData("result_constraint")]
    [InlineData("source_authority_fingerprint")]
    [InlineData("source_same_turn_state")]
    public void PendingCausalAuthority_CandidateFingerprintBindsPlannedMutationSemantics(
        string changedField)
    {
        var canonical = PlanCandidateMutationSemantics(
            changedField,
            changed: false);
        var changed = PlanCandidateMutationSemantics(
            changedField,
            changed: true);

        Assert.True(canonical.Result.IsValid, Format(canonical.Result.Issues));
        Assert.True(changed.Result.IsValid, Format(changed.Result.Issues));
        Assert.Equal(canonical.MutationKey, changed.MutationKey);
        Assert.NotEqual(
            Assert.Single(canonical.Result.AcceptedPendingResolutions)
                .CausalAuthority.CandidateFingerprint,
            Assert.Single(changed.Result.AcceptedPendingResolutions)
                .CausalAuthority.CandidateFingerprint);
    }

    [Fact]
    public void ConditionalPending_ZeroAppliedPredecessorDoesNotCreatePendingFrontier()
    {
        var result = PlanCandidateMutationSemantics(
            "amount",
            changed: false,
            conditionalPending: true).Result;

        Assert.True(result.IsValid, Format(result.Issues));
        var transition = Assert.Single(
            result.AppliedTransitions,
            value => string.Equals(
                value.EventRef,
                "turn_2:pending_fingerprint:candidate_gain",
                StringComparison.Ordinal));
        Assert.Equal(0m, transition.AppliedAmount);
        Assert.True(result.EffectBoundaryTranscript.IsComplete);
        Assert.Null(
            result.EffectBoundaryTranscript.PendingFrontierBoundaryOrdinal);
        Assert.Empty(result.AcceptedPendingResolutions);
        Assert.Empty(result.AcceptedReactionExecutions);
        Assert.Empty(result.AcceptedResolvedPendingRequestIds);
        Assert.Single(result.EffectBoundaryTranscript.AcceptedActivations);
    }

    [Fact]
    public void PendingReplayBind_ReportsFingerprintVisitsAndBuildResourcesPreservesActualWork()
    {
        var identity = Identity(index: 0);
        var baseResolution = new
            EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
                Array.Empty<ResourceMutationSourceExport>(),
                Array.Empty<ResourceMutationIntent>(),
                Array.Empty<ValidationIssue>())
            {
                TriggerCandidates = new[]
                {
                    Candidate(
                        identity,
                        Enumerable.Range(0, 4)
                            .Select(index => PendingOutput(identity, index))
                            .ToArray())
                }
            };

        var discovery = BindPendingReplay(baseResolution);
        var actual = BindPendingReplay(baseResolution);

        Assert.Equal(
            4,
            ReadWorkCounter(
                discovery.Work,
                "PendingCandidateFingerprintOutputVisitCount"));
        Assert.Equal(
            4,
            ReadWorkCounter(
                actual.Work,
                "PendingCandidateFingerprintOutputVisitCount"));

        var finalResult = Plan(
            actual.TriggerCandidates,
            actual.Work);

        Assert.True(finalResult.IsValid, Format(finalResult.Issues));
        Assert.Equal(4, finalResult.AcceptedPendingResolutions.Count);
        Assert.Equal(
            8,
            finalResult.Statistics.PendingCandidateFingerprintOutputVisitCount);
    }

    [Fact]
    public void PendingReplayProjection_DoubledReverseDependencyChainVisitsLinearly()
    {
        var thirtyTwo = BindReversePendingDependencyChain(32);
        var sixtyFour = BindReversePendingDependencyChain(64);

        Assert.True(thirtyTwo.IsValid, Format(thirtyTwo.Issues));
        Assert.True(sixtyFour.IsValid, Format(sixtyFour.Issues));
        var thirtyTwoVisits = ReadWorkCounter(
            thirtyTwo.Work,
            "PendingProjectionDependencyVisitCount");
        var sixtyFourVisits = ReadWorkCounter(
            sixtyFour.Work,
            "PendingProjectionDependencyVisitCount");

        Assert.Equal(63, thirtyTwoVisits);
        Assert.Equal(127, sixtyFourVisits);
        Assert.True(
            sixtyFourVisits <= thirtyTwoVisits * 2.1,
            $"Expected one projection visit per resolved output and dependency, " +
            $"but {thirtyTwoVisits} visits became {sixtyFourVisits}.");
    }

    private static AcceptedMechanicsResourcePlanningResult
        PlanSingleCandidateWithPendingOutputs(int outputCount)
    {
        var identity = Identity(index: 0);
        var outputs = Enumerable.Range(0, outputCount)
            .Select(index => PendingOutput(identity, index))
            .ToArray();
        return Plan(new[] { Candidate(identity, outputs) });
    }

    private static EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution
        BindReversePendingDependencyChain(int outputCount)
    {
        var identity = Identity(index: 0);
        var outputs = Enumerable.Range(0, outputCount)
            .Reverse()
            .Select(index => PendingOutput(identity, index) with
            {
                AfterComponentId = index == 0
                    ? null
                    : PendingComponentId(index - 1)
            })
            .ToArray();
        var candidate = Candidate(identity, outputs);
        var candidateFingerprint =
            AcceptedMechanicsPlanner.CreateCandidateFingerprint(candidate);
        var bindings = outputs.Select((output, index) =>
            ResolvedBinding(
                candidate,
                output,
                candidateFingerprint,
                "pending_scale_resolution_" + index.ToString(
                    "D4",
                    CultureInfo.InvariantCulture))).ToArray();
        var resolution = new
            EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
                Array.Empty<ResourceMutationSourceExport>(),
                Array.Empty<ResourceMutationIntent>(),
                Array.Empty<ValidationIssue>())
            {
                TriggerCandidates = new[] { candidate }
            };

        return BindPendingReplay(resolution, bindings);
    }

    private static AcceptedMechanicsResourcePlanningResult
        PlanCandidatesWithOnePendingOutputEach(int candidateCount)
    {
        var candidates = Enumerable.Range(0, candidateCount)
            .Select(index =>
            {
                var identity = Identity(index);
                return Candidate(identity, new[] { PendingOutput(identity, index) });
            })
            .ToArray();
        return Plan(candidates);
    }

    private static AcceptedMechanicsResourcePlanningResult Plan(
        IReadOnlyList<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>
            candidates,
        EffectAcceptedTurnPlanner.EffectResourceResolutionWork? initialWork = null)
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var history = ResourceHistoryState.CreateValidated(
            Array.Empty<ResourceTransition>(),
            definitions);
        Assert.True(history.IsValid, Format(history.Issues));
        var sources = ResourceMutationSourceCatalog.Create(new[]
        {
            new ResourceMutationSourceExport(
                "registered_system_outcome",
                "pending_scale_system",
                FingerprintA,
                ResourceMutationSourceState.Active,
                SameTurn: false)
        });
        Assert.True(sources.IsValid, Format(sources.Issues));

        return AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 2,
                Definitions: definitions,
                State: new ResourceStateLedger(
                    Array.Empty<ResourceStateEntry>()),
                History: history.History!,
                Sources: sources.Catalog!,
                Mutations: Array.Empty<ResourceMutationIntent>(),
                InitialTriggerCandidates: candidates,
                InitialEffectResolutionWork: initialWork),
            IdentityFactory());
    }

    private static CandidateMutationFingerprintResult
        PlanCandidateMutationSemantics(
            string changedField,
            bool changed,
            bool conditionalPending = false)
    {
        var baseline = CreateCandidateMutationBaseline();
        var directCost = new ResourceMutationIntent(
            EventRef: "turn_2:pending_fingerprint:direct_cost",
            Coordinate: baseline.Coordinate,
            Amount: 2m,
            Source: new ResourceMutationSourceRequest(
                "action_cost",
                "pending_fingerprint_action",
                ResourceOperation.Spend),
            Dependencies: Array.Empty<ResourceOperationKey>(),
            EventRequirements: Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);

        var amount = changedField == "derived_amount"
            ? 0m
            : changedField == "amount" && changed
                ? 2m
                : 1m;
        var dependencies = changedField == "dependencies" && changed
            ? new[] { directCost.Key }
            : Array.Empty<ResourceOperationKey>();
        var requirements = changedField == "event_requirements" && changed
            ? new[]
            {
                new ResourceMutationEventRequirement(
                    directCost.Key,
                    "resource_spent")
            }
            : Array.Empty<ResourceMutationEventRequirement>();
        var derivedAmount = changedField == "derived_amount"
            ? new ResourceLossRecoveryPolicy(changed ? 100 : 50)
            : null;
        var resultConstraint = changedField == "result_constraint" && changed
            ? new ResourceMutationResultConstraint(
                RejectBelow: 0m,
                RejectAbove: 11m)
            : null;
        var sourceFingerprint =
            changedField == "source_authority_fingerprint" && changed
                ? FingerprintB
                : FingerprintA;
        var sourceSameTurn = changedField == "source_same_turn_state" && changed;

        var candidateMutation = new ResourceMutationIntent(
            EventRef: "turn_2:pending_fingerprint:candidate_gain",
            Coordinate: baseline.Coordinate,
            Amount: amount,
            Source: new ResourceMutationSourceRequest(
                "registered_system_outcome",
                "pending_fingerprint_recovery",
                ResourceOperation.Gain),
            Dependencies: dependencies,
            EventRequirements: requirements,
            ReceiptId: null,
            DerivedAmount: derivedAmount,
            ResultConstraint: resultConstraint);
        var directCostSource = new ResourceMutationSourceExport(
            "action_cost",
            "pending_fingerprint_action",
            FingerprintA,
            ResourceMutationSourceState.Active,
            SameTurn: false,
            BoundOwner: baseline.Owner);
        var candidateSource = new ResourceMutationSourceExport(
            "registered_system_outcome",
            "pending_fingerprint_recovery",
            sourceFingerprint,
            ResourceMutationSourceState.Active,
            SameTurn: sourceSameTurn);
        var sources = ResourceMutationSourceCatalog.Create(new[]
        {
            directCostSource,
            candidateSource
        });
        Assert.True(sources.IsValid, Format(sources.Issues));
        var identity = new EffectActivationCandidateIdentity(
            "pending_fingerprint_effect",
            "pending_fingerprint_trigger",
            "owner_turn_end",
            "turn_2:pending_fingerprint:activation",
            "turn_2:pending_fingerprint:boundary");
        const string componentId = "pending_fingerprint_component";
        var candidateComponentMap =
            new Dictionary<ResourceOperationKey, string>
            {
                [candidateMutation.Key] = componentId
            };
        var candidate = new
            EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
                new EffectActivationCandidate(
                    identity,
                    Priority: 100,
                    ConsumesUse: false,
                    EffectAuthority: new ResourcePendingAuthorityBinding(
                        "permanent",
                        identity.EffectId)),
                useSeed: null,
                producer: null,
                plannedMutationKeys: new[] { candidateMutation.Key },
                plannedComponentIds: new[] { componentId },
                plannedComponentIdsByMutation: candidateComponentMap,
                pendingOutputs: new[]
                {
                    PendingOutput(identity, index: 0) with
                    {
                        AfterComponentId = conditionalPending
                            ? componentId
                            : null
                    }
                },
                reactionOutputs: Array.Empty<EffectReactionExecution>(),
                origin: new EffectAcceptedTurnPlanner
                    .EffectResourceCandidateOrigin(
                        new[] { candidateMutation },
                        new[] { componentId },
                        candidateComponentMap,
                        new[] { candidateSource }));

        var result = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 2,
                Definitions: baseline.Definitions,
                State: baseline.State,
                History: baseline.History,
                Sources: sources.Catalog!,
                Mutations: new[] { directCost, candidateMutation },
                InitialTriggerCandidates: new[] { candidate }),
            IdentityFactory());
        return new CandidateMutationFingerprintResult(
            candidateMutation.Key,
            result);
    }

    private static CandidateMutationBaseline CreateCandidateMutationBaseline()
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("energy", out var definition));
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Player,
            "player_current",
            "energy");
        var owner = new ResourceOwnerKey(
            coordinate.Realm,
            coordinate.OwnerKind,
            coordinate.ResourceOwnerId);
        var binding = new ResourceCapacityBinding(
            definition!.CapacityPolicy.Kind,
            definition.CapacityPolicy.FormulaKey!,
            FingerprintA);
        var snapshot = new ResourceStateSnapshot(
            Current: 10m,
            Maximum: 10m,
            binding,
            ResourceLifecycleState.Active);
        var initialize = new ResourceTransition(
            TransitionId: "pending_fingerprint_initialize_transition",
            OperationId: "pending_fingerprint_initialize_operation",
            EventRef: "turn_1:pending_fingerprint:initialize",
            OriginKind: "setting_materialization",
            OriginId: coordinate.ResourceOwnerId,
            Phase: ResourceMutationPhase.RegisteredSystemOutcome,
            Priority: 50,
            ExecutionSequence: 0,
            Coordinate: coordinate,
            Operation: ResourceTransitionOperation.Initialize,
            RequestedAmount: 0m,
            AppliedAmount: 0m,
            Outcome: ResourceTransitionOutcome.Applied,
            CapacityDisposition:
                ResourceCapacityDisposition.InitializeFromDefinition,
            BeforeState: null,
            AfterState: snapshot,
            SourceEvidence: new ResourceSourceEvidence(
                "setting_materialization",
                coordinate.ResourceOwnerId,
                FingerprintA),
            PolicyFingerprint: FingerprintA,
            ReceiptId: null,
            Turn: 1);
        var history = ResourceHistoryState.CreateValidated(
            new[] { initialize },
            definitions);
        Assert.True(history.IsValid, Format(history.Issues));
        var state = new ResourceStateLedger(new[]
        {
            new ResourceStateEntry(
                coordinate,
                snapshot.Current,
                snapshot.Maximum,
                binding,
                snapshot.State,
                new ResourceChronology(
                    CreatedAtTurn: 1,
                    CreatedEventRef: initialize.EventRef,
                    LastTransitionId: initialize.TransitionId,
                    LastEventRef: initialize.EventRef,
                    LastTransitionTurn: 1))
        });
        Assert.Empty(history.History!.ValidateStateAgreement(state));
        return new CandidateMutationBaseline(
            definitions,
            state,
            history.History,
            coordinate,
            owner);
    }

    private static EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution
        BindPendingReplay(
            EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution resolution,
            IReadOnlyList<ResourcePendingResolvedBinding>? bindings = null)
    {
        var sessionType = typeof(AcceptedMechanicsPlanner).GetNestedType(
            "ResolvedPendingReplaySession",
            BindingFlags.NonPublic);
        Assert.NotNull(sessionType);
        var constructor = sessionType!.GetConstructors(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 1);
        var session = constructor.Invoke(new object[]
        {
            bindings ?? Array.Empty<ResourcePendingResolvedBinding>()
        });
        var bind = sessionType.GetMethod(
            "Bind",
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);
        Assert.NotNull(bind);
        return Assert.IsType<
            EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution>(
                bind!.Invoke(session, new object[] { resolution }));
    }

    private static ResourcePendingResolvedBinding ResolvedBinding(
        EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate candidate,
        EffectAcceptedTurnPlanner.EffectBoundedResourceResolution output,
        string candidateFingerprint,
        string requestId)
    {
        var authority = new ResourcePendingCausalAuthority(
            candidate.Activation.Identity.EffectId,
            candidate.Activation.Identity.TriggerId,
            candidate.Activation.Identity.EventRef,
            candidate.Activation.Identity.TriggerEventRef,
            ResourceProducerOperationKey: null,
            candidate.Activation.Priority,
            ActivationOrdinal: 0,
            candidate.Activation.ConsumesUse,
            UsesBefore: null,
            output.ComponentId,
            output.AfterComponentId,
            candidateFingerprint,
            FingerprintA,
            WaveOrdinal: 0);
        var request = new ResourcePendingRequest(
            requestId,
            "pending_scale_session",
            "pending_scale_request",
            2,
            output.EventRef,
            output.EffectId,
            output.EffectAuthority,
            output.Source,
            output.SourceAuthority,
            output.Target,
            output.TargetAuthority,
            output.TriggerId,
            output.Coordinate,
            output.ResourceAuthority,
            output.Operation,
            output.MinimumAmount,
            output.MaximumAmount,
            output.SourceAuthorityFingerprint,
            output.PolicyFingerprint,
            FingerprintA,
            FingerprintA,
            Array.Empty<string>(),
            "2026-08-23T00:00:00.0000000+00:00",
            FingerprintA,
            output.SafeSourceLabel,
            output.SafeTargetLabel,
            output.SafeResourceLabel,
            output.SafeOperationLabel,
            authority);
        return new ResourcePendingResolvedBinding(
            request,
            "resource_delta",
            1m,
            "resolved",
            FingerprintB,
            ResolvedAtTurn: 2);
    }

    private static EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate
        Candidate(
            EffectActivationCandidateIdentity identity,
            IReadOnlyList<
                EffectAcceptedTurnPlanner.EffectBoundedResourceResolution> outputs) =>
        new(
            new EffectActivationCandidate(
                identity,
                Priority: 100,
                ConsumesUse: false,
                EffectAuthority: new ResourcePendingAuthorityBinding(
                    "permanent",
                    identity.EffectId)),
            useSeed: null,
            producer: null,
            plannedMutationKeys: Array.Empty<ResourceOperationKey>(),
            plannedComponentIds: Array.Empty<string>(),
            plannedComponentIdsByMutation:
                new Dictionary<ResourceOperationKey, string>(),
            pendingOutputs: outputs,
            reactionOutputs: Array.Empty<EffectReactionExecution>(),
            origin: EffectAcceptedTurnPlanner
                .EffectResourceCandidateOrigin.Empty);

    private static EffectAcceptedTurnPlanner.EffectBoundedResourceResolution
        PendingOutput(EffectActivationCandidateIdentity identity, int index)
    {
        var suffix = index.ToString("D4", CultureInfo.InvariantCulture);
        return new EffectAcceptedTurnPlanner.EffectBoundedResourceResolution(
            EventRef: identity.EventRef + ":pending:" + suffix,
            EffectId: identity.EffectId,
            EffectAuthority: new ResourcePendingAuthorityBinding(
                "permanent",
                identity.EffectId),
            Source: new JsonObject
            {
                ["kind"] = "wound",
                ["sourceId"] = "pending_scale_wound_" + suffix,
                ["definitionKey"] = "bleeding_consequence"
            },
            SourceAuthority: new ResourcePendingAuthorityBinding(
                "permanent",
                "pending_scale_wound_" + suffix),
            Target: new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            TargetAuthority: new ResourcePendingAuthorityBinding(
                "permanent",
                "player_current"),
            TriggerId: identity.TriggerId,
            ComponentId: PendingComponentId(index),
            TriggerEventRef: identity.TriggerEventRef,
            EventKind: identity.EventKind,
            Coordinate: new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "health"),
            ResourceAuthority: new ResourcePendingAuthorityBinding(
                "permanent",
                "player_current"),
            Operation: ResourceOperation.Damage,
            MinimumAmount: 0m,
            MaximumAmount: 5m,
            SourceAuthorityFingerprint: FingerprintA,
            PolicyFingerprint: FingerprintB,
            Dependencies: Array.Empty<ResourceOperationKey>(),
            EventRequirements:
                Array.Empty<ResourceMutationEventRequirement>(),
            ResultConstraint: null,
            RemainingUseBudget: null,
            SafeSourceLabel: "Рана",
            SafeTargetLabel: "герой",
            SafeResourceLabel: "Здоровье",
            SafeOperationLabel: "урон");
    }

    private static string PendingComponentId(int index) =>
        "pending_scale_component_" + index.ToString(
            "D4",
            CultureInfo.InvariantCulture);

    private static EffectActivationCandidateIdentity Identity(int index)
    {
        var suffix = index.ToString("D4", CultureInfo.InvariantCulture);
        return new EffectActivationCandidateIdentity(
            "pending_scale_effect_" + suffix,
            "pending_scale_trigger_" + suffix,
            "owner_turn_end",
            "turn_2:pending_scale:activation:" + suffix,
            "turn_2:pending_scale:boundary");
    }

    private static long ReadWorkCounter(object statistics, string propertyName)
    {
        var property = statistics.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.True(
            property != null,
            $"Planner statistics must expose deterministic '{propertyName}' work.");
        return Convert.ToInt64(
            property!.GetValue(statistics),
            CultureInfo.InvariantCulture);
    }

    private static AcceptedMechanicsIdentityFactory IdentityFactory()
    {
        var next = 1;
        return new AcceptedMechanicsIdentityFactory(() =>
            new Guid(next++, 0, 0, new byte[8]));
    }

    private static string Format(IEnumerable<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(static issue =>
            $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}"));

    private sealed record CandidateMutationFingerprintResult(
        ResourceOperationKey MutationKey,
        AcceptedMechanicsResourcePlanningResult Result);

    private sealed record CandidateMutationBaseline(
        ResourceDefinitionCatalog Definitions,
        ResourceStateLedger State,
        ResourceHistoryState History,
        ResourceCoordinate Coordinate,
        ResourceOwnerKey Owner);
}
