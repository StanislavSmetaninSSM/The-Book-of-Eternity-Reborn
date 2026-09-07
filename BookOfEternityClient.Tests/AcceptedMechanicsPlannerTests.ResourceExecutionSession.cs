using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AcceptedMechanicsPlannerTests
{
    [Fact]
    public void ResourceSession_ClosedPrefixIsConsumedWithoutChangingFinalResultOrAllocations()
    {
        var input = SessionPeriodicInput(bounded: false);
        var expectedCounter = new SessionAllocationCounter();
        var expected = AcceptedMechanicsPlanner.BuildResources(input, expectedCounter.Factory);
        Assert.True(expected.IsValid, SessionIssues(expected));
        Assert.Equal(3, expected.AppliedTransitions.Count);

        var counter = new SessionAllocationCounter();
        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(input, counter.Factory);
        var checkpoint = Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(
            session.AdvanceToClosedBoundary().Checkpoint);
        var prefix = checkpoint.EffectPrefix;
        Assert.Single(prefix.AcceptedActivations);
        Assert.Equal(2, prefix.AppliedComponentEvidence.Count);
        Assert.Equal(2, prefix.ResourceMutations.Count);
        Assert.Equal(checkpoint.ClosedEffectBoundaryOrdinals,
            prefix.BoundaryCloses.Select(value => value.Boundary.BoundaryOrdinal).OrderBy(value => value));
        var before = prefix.Fingerprint;
        var originalMutationIds = prefix.ResourceMutations.Select(value => value.Transition.TransitionId).ToArray();
        var calls = counter.Calls;
        var actual = session.Drain();
        Assert.True(actual.IsValid, SessionIssues(actual));
        Assert.Equal(SessionResultImage(expected), SessionResultImage(actual));
        Assert.Equal(expectedCounter.Ids, counter.Ids);
        Assert.Equal(calls, counter.Calls);
        Assert.Equal(before, prefix.Fingerprint);
        Assert.Equal(originalMutationIds, prefix.ResourceMutations.Select(value => value.Transition.TransitionId));
        Assert.Equal(2, prefix.ResourceMutations.Count);
        Assert.Equal(3, actual.EffectBoundaryTranscript.ResourceMutations.Count);
    }

    [Fact]
    public void ResourceSession_ContractRedHasNonemptyProductionPositiveControl()
    {
        var input = SessionOrdinaryInput();
        var production = AcceptedMechanicsPlanner.BuildResources(input, new SessionAllocationCounter().Factory);
        Assert.True(production.IsValid, SessionIssues(production));
        Assert.Equal(2, production.Events.Count);
        var method = typeof(AcceptedMechanicsPlanner).GetMethod(
            "BeginResourceExecution", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method); // First semantic RED, after real old-production success.
        var counter = new SessionAllocationCounter();
        using var session = Assert.IsAssignableFrom<IDisposable>(
            method!.Invoke(null, new object[] { input, counter.Factory }));
        var drain = session.GetType().GetMethod("Drain", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(drain);
        var result = Assert.IsType<AcceptedMechanicsResourcePlanningResult>(drain!.Invoke(session, null));
        Assert.Equal(SessionResultImage(production), SessionResultImage(result));
    }

    [Fact]
    public void ResourceSession_StopResumeMatchesDrainAndAllocatesOnlyDuringPreparation()
    {
        var input = SessionOrdinaryInput();
        var controlCounter = new SessionAllocationCounter();
        using var controlSession = AcceptedMechanicsPlanner.BeginResourceExecution(input, controlCounter.Factory);
        var control = controlSession.Drain();
        Assert.True(control.IsValid, SessionIssues(control));
        Assert.Equal(0, controlSession.CheckpointCount);
        var counter = new SessionAllocationCounter();
        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(input, counter.Factory);
        Assert.Equal(0, counter.Calls);
        var first = session.AdvanceToClosedBoundary();
        Assert.Null(first.Result);
        var checkpoint = Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(first.Checkpoint);
        Assert.Single(checkpoint.AppliedTransitions);
        Assert.Single(checkpoint.PendingHistoryTransitions);
        Assert.Single(checkpoint.Events);
        Assert.Empty(checkpoint.ReplayTransitions);
        Assert.Equal(1, checkpoint.NextExecutionSequence);
        Assert.Equal(0, checkpoint.Statistics.HistoryFreezeCount);
        Assert.Equal(9m, Assert.Single(checkpoint.State.Entries).Current);
        Assert.Null(session.Result);
        Assert.Equal(controlCounter.Calls, counter.Calls);
        var calls = counter.Calls;
        var frozen = checkpoint.State.ToCanonicalJson();

        var second = session.AdvanceToClosedBoundary();
        Assert.Equal(2, second.Checkpoint!.AppliedTransitions.Count);
        Assert.Equal(8m, Assert.Single(second.Checkpoint.State.Entries).Current);
        var result = session.Drain();
        Assert.Equal(SessionResultImage(control), SessionResultImage(result));
        Assert.Equal(controlCounter.Ids, counter.Ids);
        Assert.Equal(calls, counter.Calls);
        Assert.Equal(frozen, checkpoint.State.ToCanonicalJson());
        Assert.Single(checkpoint.AppliedTransitions);
        Assert.Equal(2, session.CheckpointCount);
        Assert.Same(result, session.Result);
        Assert.Equal(1, result.Statistics.HistoryFreezeCount);
        Assert.Throws<InvalidOperationException>(() => session.AdvanceToClosedBoundary());
        Assert.Throws<InvalidOperationException>(() => session.Drain());
        Assert.Equal(calls, counter.Calls);
    }

    [Fact]
    public void ResourceSession_DoesNotPauseInsideAnOpenTwoComponentEffectBoundary()
    {
        var input = SessionPeriodicInput(bounded: false);
        var controlCounter = new SessionAllocationCounter();
        var control = AcceptedMechanicsPlanner.BuildResources(input, controlCounter.Factory);
        Assert.True(control.IsValid, SessionIssues(control));
        Assert.Equal(3, control.AppliedTransitions.Count);
        var counter = new SessionAllocationCounter();
        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(input, counter.Factory);
        var step = session.AdvanceToClosedBoundary();
        Assert.Null(step.Result);
        var checkpoint = Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(step.Checkpoint);
        Assert.Single(checkpoint.ClosedEffectBoundaryOrdinals);
        Assert.Equal(2, checkpoint.AppliedTransitions.Count);
        Assert.All(checkpoint.AppliedTransitions,
            transition => Assert.Equal(ResourceMutationPhase.EffectTrigger, transition.Phase));
        Assert.Equal(0, checkpoint.Statistics.HistoryFreezeCount);
        var result = session.Drain();
        Assert.True(result.EffectBoundaryTranscript!.IsComplete);
        Assert.Equal(SessionResultImage(control), SessionResultImage(result));
        Assert.Equal(controlCounter.Ids, counter.Ids);
        Assert.Equal(controlCounter.Calls, counter.Calls);
    }

    [Fact]
    public void ResourceSession_RetainsPreparedResolverWorkAcrossCausalChildPause()
    {
        var baseline = BaselineHealth(current: 3m);
        var source = new ResourceMutationSourceExport(
            "combat_outcome", "session_resource_root", FingerprintB,
            ResourceMutationSourceState.Active, false, PlayerOwner);
        var root = new ResourceMutationIntent(
            "turn_43:session_resource_root:1", HealthCoordinate, 3m,
            new ResourceMutationSourceRequest(source.SourceKind, source.SourceId, ResourceOperation.Damage),
            Array.Empty<ResourceOperationKey>(), Array.Empty<ResourceMutationEventRequirement>(), null);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "periodic_restore");
        effect["triggers"]![0]!["triggerId"] = "on_resource_depleted";
        effect["triggers"]![0]!["eventType"] = "resource_depleted";
        AcceptedMechanicsResourceInput CreateInput(Action visit) => new(
            43, baseline.Definitions, baseline.State, baseline.History, CreateCatalog(source), new[] { root },
            EventMutationResolver: (resourceEvent, producer) =>
            {
                visit();
                if (resourceEvent.EventKind != "resource_depleted")
                    return new EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution(
                        Array.Empty<ResourceMutationSourceExport>(), Array.Empty<ResourceMutationIntent>(),
                        Array.Empty<ValidationIssue>());
                var resolved = InvokeResourceEventResolution(effect, "on_resource_depleted", producer,
                    resourceEvent.EventKind, 43, baseline.Targets, baseline.Owners, baseline.Definitions);
                return resolved with
                {
                    TriggerCandidates = new[]
                    {
                        SessionCandidate(
                            resolved,
                            producer,
                            resourceEvent.EventKind,
                            resourceEvent.EventRef)
                    }
                };
            });
        var expectedVisits = 0;
        var expectedCounter = new SessionAllocationCounter();
        var expected = AcceptedMechanicsPlanner.BuildResources(
            CreateInput(() => expectedVisits++), expectedCounter.Factory);
        Assert.True(expected.IsValid, SessionIssues(expected));
        Assert.Equal(2, expected.AppliedTransitions.Count);
        var visits = 0;
        var counter = new SessionAllocationCounter();
        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(
            CreateInput(() => visits++), counter.Factory);
        var checkpoint = session.AdvanceToClosedBoundary().Checkpoint!;
        Assert.NotNull(checkpoint);
        Assert.Equal(2, checkpoint.AppliedTransitions.Count);
        Assert.Single(checkpoint.ClosedEffectBoundaryOrdinals);
        Assert.Equal(expectedVisits, visits);
        Assert.True(visits > 0);
        var result = session.Drain();
        Assert.Equal(expectedVisits, visits);
        Assert.Equal(expectedCounter.Ids, counter.Ids);
        Assert.Equal(SessionResultImage(expected), SessionResultImage(result));
    }

    [Fact]
    public void ResourceSession_UnresolvedReceiptNeverYieldsAClosedCheckpoint()
    {
        var input = SessionPeriodicInput(bounded: true);
        var control = AcceptedMechanicsPlanner.BuildResources(input, new SessionAllocationCounter().Factory);
        Assert.True(control.IsValid, SessionIssues(control));
        Assert.NotEmpty(control.AcceptedPendingResolutions);
        Assert.False(control.EffectBoundaryTranscript!.IsComplete);
        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(input, new SessionAllocationCounter().Factory);
        var step = session.AdvanceToClosedBoundary();
        Assert.Null(step.Checkpoint);
        Assert.NotNull(step.Result);
        Assert.Equal(0, session.CheckpointCount);
        Assert.Equal(SessionResultImage(control), SessionResultImage(step.Result!));
        Assert.Throws<InvalidOperationException>(() => session.AdvanceToClosedBoundary());
    }

    [Fact]
    public void ResourceSession_FailureBeforeExecutionAndAfterCheckpointRemainFailures()
    {
        var ordinary = SessionOrdinaryInput();
        var unknown = new AcceptedMechanicsResourceInput(
            2, ordinary.Definitions, ordinary.State, ordinary.History,
            CreateCatalog(), ordinary.Mutations);
        using var rejected = AcceptedMechanicsPlanner.BeginResourceExecution(
            unknown, new SessionAllocationCounter().Factory);
        var failure = rejected.AdvanceToClosedBoundary();
        Assert.Null(failure.Checkpoint);
        Assert.False(failure.Result!.IsValid);
        Assert.Contains(failure.Result.Issues, issue => issue.Code == "resource_source_unknown");
        Assert.Null(failure.Result.StateAfterImage);
        Assert.Equal(0, rejected.CheckpointCount);
        Assert.Throws<InvalidOperationException>(() => rejected.Drain());

        var invalidSuffix = SessionOrdinaryInput(secondAmount: 100m);
        var expected = AcceptedMechanicsPlanner.BuildResources(
            invalidSuffix, new SessionAllocationCounter().Factory);
        Assert.False(expected.IsValid);
        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(
            invalidSuffix, new SessionAllocationCounter().Factory);
        var prefix = session.AdvanceToClosedBoundary().Checkpoint;
        Assert.NotNull(prefix);
        Assert.Single(prefix!.AppliedTransitions);
        var suffix = session.AdvanceToClosedBoundary();
        Assert.Null(suffix.Checkpoint);
        Assert.False(suffix.Result!.IsValid);
        Assert.Null(suffix.Result.StateAfterImage);
        Assert.Null(suffix.Result.HistoryAfterImage);
        Assert.Equal(SessionResultImage(expected), SessionResultImage(suffix.Result));
        Assert.Single(prefix.AppliedTransitions); // Observation never grants final acceptance.
    }

    [Fact]
    public void ResourceSession_ReplayEvidenceAndNewSuffixRemainDistinctAcrossPause()
    {
        var full = SessionOrdinaryInput();
        var firstInput = new AcceptedMechanicsResourceInput(
            2, full.Definitions, full.State, full.History, full.Sources, new[] { full.Mutations[0] });
        var first = AcceptedMechanicsPlanner.BuildResources(firstInput, new SessionAllocationCounter().Factory);
        Assert.True(first.IsValid, SessionIssues(first));
        var input = new AcceptedMechanicsResourceInput(
            2, full.Definitions, first.StateAfterImage!, first.HistoryAfterImage!, full.Sources, full.Mutations);
        var controlCounter = new SessionAllocationCounter(seed: 50);
        var expected = AcceptedMechanicsPlanner.BuildResources(input, controlCounter.Factory);
        Assert.True(expected.IsValid, SessionIssues(expected));
        var counter = new SessionAllocationCounter(seed: 50);
        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(input, counter.Factory);
        var prefix = session.AdvanceToClosedBoundary().Checkpoint!;
        Assert.Empty(prefix.AppliedTransitions);
        Assert.Empty(prefix.PendingHistoryTransitions);
        Assert.Empty(prefix.Events);
        Assert.Equal(Assert.Single(first.AppliedTransitions), Assert.Single(prefix.ReplayTransitions));
        var result = session.Drain();
        Assert.Single(result.AppliedTransitions);
        Assert.Single(result.ReplayTransitions);
        Assert.Single(result.Events);
        Assert.Equal(SessionResultImage(expected), SessionResultImage(result));
        Assert.Equal(controlCounter.Ids, counter.Ids);
    }

    [Fact]
    public void ResourceSession_DetachesInputArraysAndRetainsCheckpointImagesAfterResume()
    {
        var input = SessionOrdinaryInput();
        var mutations = input.Mutations.ToList();
        var captured = new AcceptedMechanicsResourceInput(
            2, input.Definitions, input.State, input.History, input.Sources, mutations);
        var expected = AcceptedMechanicsPlanner.BuildResources(
            captured, new SessionAllocationCounter().Factory);
        using var session = AcceptedMechanicsPlanner.BeginResourceExecution(
            captured, new SessionAllocationCounter().Factory);
        mutations.Clear();
        var returnedDependencies = Assert.IsType<ResourceOperationKey[]>(captured.Mutations[1].Dependencies);
        returnedDependencies[0] = returnedDependencies[0] with { EventRef = "changed_dependency" };
        var checkpoint = session.AdvanceToClosedBoundary().Checkpoint!;
        var state = checkpoint.State.ToCanonicalJson();
        var history = checkpoint.HistoryBaseline.ToCanonicalJson();
        checkpoint.Definitions.ToCanonicalRoot()["definitions"] = new System.Text.Json.Nodes.JsonArray();
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ResourceTransition>)checkpoint.AppliedTransitions).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ResourceTransition>)checkpoint.PendingHistoryTransitions).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ResourceAppliedEvent>)checkpoint.Events).Clear());
        Assert.Equal(SessionResultImage(expected), SessionResultImage(session.Drain()));
        Assert.Equal(state, checkpoint.State.ToCanonicalJson());
        Assert.Equal(history, checkpoint.HistoryBaseline.ToCanonicalJson());
        Assert.Single(checkpoint.AppliedTransitions);
    }

    [Fact]
    public void ResourceSession_RejectsNullDisposedFaultedAndReentrantUse()
    {
        var input = SessionOrdinaryInput();
        Assert.Throws<ArgumentNullException>(() =>
            AcceptedMechanicsPlanner.BeginResourceExecution(null!, new SessionAllocationCounter().Factory));
        Assert.Throws<ArgumentNullException>(() =>
            AcceptedMechanicsPlanner.BeginResourceExecution(input, null!));
        var disposedCounter = new SessionAllocationCounter();
        var disposed = AcceptedMechanicsPlanner.BeginResourceExecution(input, disposedCounter.Factory);
        disposed.Dispose();
        disposed.Dispose();
        Assert.Throws<ObjectDisposedException>(() => disposed.AdvanceToClosedBoundary());
        Assert.Throws<ObjectDisposedException>(() => disposed.Drain());
        Assert.Equal(0, disposedCounter.Calls);

        var faultCounter = new SessionAllocationCounter
        {
            OnAllocation = () => throw new InvalidOperationException("allocation probe failure")
        };
        using var faulted = AcceptedMechanicsPlanner.BeginResourceExecution(input, faultCounter.Factory);
        var failure = Assert.Throws<InvalidOperationException>(() => faulted.Drain());
        Assert.Equal("allocation probe failure", failure.Message);
        var count = faultCounter.Calls;
        Assert.Throws<InvalidOperationException>(() => faulted.AdvanceToClosedBoundary());
        Assert.Null(faulted.Result);
        Assert.Equal(count, faultCounter.Calls);

        var counter = new SessionAllocationCounter();
        using var active = AcceptedMechanicsPlanner.BeginResourceExecution(input, counter.Factory);
        counter.OnAllocation = () =>
        {
            Assert.Throws<InvalidOperationException>(() => active.AdvanceToClosedBoundary());
            Assert.Throws<InvalidOperationException>(() => active.Dispose());
        };
        Assert.True(active.Drain().IsValid);
        Assert.True(counter.Calls > 0);
    }

    private static AcceptedMechanicsResourceInput SessionOrdinaryInput(decimal secondAmount = 1m)
    {
        var baseline = BaselineCharges();
        var first = Intent("turn_2:session:1", "action_cost", "action_alpha", ResourceOperation.Spend, 1m);
        var second = Intent("turn_2:session:2", "action_cost", "action_alpha", ResourceOperation.Spend,
            secondAmount, dependencies: new[] { first.Key });
        return new AcceptedMechanicsResourceInput(
            2, baseline.Definitions, baseline.State, baseline.History, baseline.Sources, new[] { first, second });
    }

    private static AcceptedMechanicsResourceInput SessionPeriodicInput(bool bounded)
    {
        var baseline = BaselineHealth(current: 10m);
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["triggers"]![0]!["resolutionMode"] = bounded ? "bounded_receipt" : "deterministic";
        if (!bounded)
        {
            var second = effect["components"]![0]!.DeepClone().AsObject();
            second["componentId"] = "component_session_second";
            effect["components"]!.AsArray().Add(second);
            effect["triggers"]![0]!["componentIds"]!.AsArray().Add("component_session_second");
        }
        var resolution = InvokePeriodicResourceResolution(
            effect, "on_owner_turn_end", PeriodicEvent(),
            baseline.Targets, baseline.Owners, baseline.Definitions);
        Assert.Empty(resolution.Issues);
        if (!bounded)
            Assert.Equal(2, resolution.Mutations.Count);
        var sources = resolution.SourceExports.ToList();
        var mutations = resolution.Mutations.ToList();
        if (!bounded)
        {
            var source = new ResourceMutationSourceExport(
                "combat_outcome", "session_ordinary_suffix", FingerprintB,
                ResourceMutationSourceState.Active, false, PlayerOwner);
            sources.Add(source);
            mutations.Add(new ResourceMutationIntent(
                "turn_43:session_suffix:1", HealthCoordinate, 1m,
                new ResourceMutationSourceRequest(source.SourceKind, source.SourceId, ResourceOperation.Damage),
                Array.Empty<ResourceOperationKey>(), Array.Empty<ResourceMutationEventRequirement>(), null));
        }
        var candidate = SessionCandidate(
            resolution,
            producer: null,
            eventKind: "owner_turn_end",
            triggerEventRef: PeriodicEvent().EventRef);
        return new AcceptedMechanicsResourceInput(
            PeriodicEvent().Turn, baseline.Definitions, baseline.State, baseline.History,
            CreateCatalog(sources.ToArray()), mutations,
            InitialTriggerCandidates: new[] { candidate },
            InitialEffectResolutionWork: resolution.Work);
    }

    private static EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate SessionCandidate(
        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution resolution,
        ResourceOperationKey? producer,
        string eventKind,
        string triggerEventRef)
    {
        var eventRef = resolution.Mutations.Count != 0
            ? resolution.Mutations[0].EventRef
            : resolution.PendingResolutions.Count != 0
                ? resolution.PendingResolutions[0].EventRef
                : triggerEventRef;
        var identity = new EffectActivationCandidateIdentity(
            EffectMaterializationTestFixture.EffectId,
            eventKind == "owner_turn_end" ? "on_owner_turn_end" : "on_resource_depleted",
            eventKind,
            eventRef,
            triggerEventRef);
        return new EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate(
            new EffectActivationCandidate(
                identity,
                Priority: 100,
                ConsumesUse: false,
                EffectAuthority: PermanentEffectAuthority(identity.EffectId)),
            useSeed: null,
            producer,
            resolution.Mutations.Select(static mutation => mutation.Key).ToArray(),
            resolution.ExecutedComponentIds,
            resolution.ComponentIdsByMutation,
            resolution.PendingResolutions,
            Array.Empty<EffectReactionExecution>(),
            new EffectAcceptedTurnPlanner.EffectResourceCandidateOrigin(
                resolution.Mutations,
                resolution.ExecutedComponentIds,
                resolution.ComponentIdsByMutation,
                resolution.SourceExports));
    }

    private static string SessionIssues(AcceptedMechanicsResourcePlanningResult result) =>
        string.Join(Environment.NewLine, result.Issues.Select(issue => $"{issue.Code}: {issue.Expected}; {issue.Actual}"));

    private static string SessionResultImage(AcceptedMechanicsResourcePlanningResult result) =>
        JsonSerializer.Serialize(new
        {
            result.IsValid,
            State = result.StateAfterImage?.ToCanonicalJson(),
            History = result.HistoryAfterImage?.ToCanonicalJson(),
            result.AppliedTransitions,
            result.ReplayTransitions,
            result.Events,
            Issues = result.Issues.Select(issue => new
            {
                issue.FilePath, issue.Code, issue.Severity, issue.Message, issue.Expected, issue.Actual,
                issue.Category, issue.Actor, issue.Section, issue.RepairHint, issue.RepairTargetFiles
            }),
            result.Statistics,
            Transcript = result.EffectBoundaryTranscript?.Fingerprint,
            Complete = result.EffectBoundaryTranscript?.IsComplete,
            Pending = result.AcceptedPendingResolutions.Select(pending => new
            {
                Resolution = new
                {
                    pending.Resolution.EventRef, pending.Resolution.EffectId,
                    pending.Resolution.EffectAuthority,
                    Source = pending.Resolution.Source.ToJsonString(),
                    pending.Resolution.SourceAuthority,
                    Target = pending.Resolution.Target.ToJsonString(),
                    pending.Resolution.TargetAuthority,
                    pending.Resolution.TriggerId, pending.Resolution.ComponentId,
                    pending.Resolution.TriggerEventRef, pending.Resolution.EventKind,
                    pending.Resolution.Coordinate, pending.Resolution.ResourceAuthority,
                    pending.Resolution.Operation, pending.Resolution.MinimumAmount,
                    pending.Resolution.MaximumAmount, pending.Resolution.SourceAuthorityFingerprint,
                    pending.Resolution.PolicyFingerprint, pending.Resolution.Dependencies,
                    pending.Resolution.EventRequirements, pending.Resolution.ResultConstraint,
                    pending.Resolution.RemainingUseBudget, pending.Resolution.SafeSourceLabel,
                    pending.Resolution.SafeTargetLabel, pending.Resolution.SafeResourceLabel,
                    pending.Resolution.SafeOperationLabel, pending.Resolution.AfterComponentId
                },
                CausalAuthority = new
                {
                    pending.CausalAuthority.EffectId, pending.CausalAuthority.TriggerId,
                    pending.CausalAuthority.ActivationEventRef, pending.CausalAuthority.TriggerEventRef,
                    pending.CausalAuthority.ResourceProducerOperationKey, pending.CausalAuthority.Priority,
                    pending.CausalAuthority.ActivationOrdinal, pending.CausalAuthority.ConsumesUse,
                    pending.CausalAuthority.UsesBefore, pending.CausalAuthority.ComponentId,
                    pending.CausalAuthority.AfterComponentId, pending.CausalAuthority.CandidateFingerprint,
                    pending.CausalAuthority.TranscriptPrefixFingerprint, pending.CausalAuthority.WaveOrdinal
                }
            }),
            Reactions = result.AcceptedReactionExecutions.Select(reaction => new
            {
                reaction.EventRef, reaction.TriggerEventRef, reaction.CausalEventRef,
                reaction.Turn, reaction.EventKind, reaction.Target, reaction.EffectId,
                reaction.TriggerId, reaction.ComponentId, reaction.ResultKind,
                reaction.Dependency, reaction.AfterComponentId, reaction.MaxExpansion,
                DownstreamSource = reaction.DownstreamSource == null ? null : new
                {
                    reaction.DownstreamSource.Key,
                    Definition = reaction.DownstreamSource.Definition.ToJsonString(),
                    reaction.DownstreamSource.Materializable, reaction.DownstreamSource.Active,
                    reaction.DownstreamSource.SameTurn, reaction.DownstreamSource.SourceRef,
                    SatisfiedPredicates = reaction.DownstreamSource.SatisfiedPredicates
                        .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    reaction.DownstreamSource.RequiredApplicationAuthority
                },
                Parameters = reaction.Parameters?.ToJsonString(),
                reaction.DownstreamSourceKey, reaction.ComponentPriority,
                ReplacementTarget = reaction.ReplacementTarget == null ? null : new
                {
                    reaction.ReplacementTarget.EffectId,
                    Authority = new
                    {
                        reaction.ReplacementTarget.Authority.BindingKind,
                        reaction.ReplacementTarget.Authority.AuthorityId
                    }
                }
            }),
            result.AcceptedResolvedPendingRequestIds,
            Triggers = result.ResourceTriggerExecutions.Select(execution => new
            {
                execution.EffectId, execution.TriggerId, execution.EventKind, execution.EventRef,
                execution.MutationKeys, execution.RemainingUseBudget, execution.ComponentIds, execution.TriggerEventRef,
                Components = execution.ComponentIdsByMutation?.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
                    .Select(pair => new { Key = pair.Key, pair.Value })
            })
        });

    private sealed class SessionAllocationCounter
    {
        private int _next;
        internal SessionAllocationCounter(int seed = 0)
        {
            _next = seed;
            Factory = new AcceptedMechanicsIdentityFactory(() =>
            {
                Calls++;
                OnAllocation?.Invoke();
                var id = new Guid(++_next, 0, 0, new byte[8]);
                Ids.Add(id);
                return id;
            });
        }
        internal Action? OnAllocation { get; set; }
        internal int Calls { get; private set; }
        internal List<Guid> Ids { get; } = new();
        internal AcceptedMechanicsIdentityFactory Factory { get; }
    }
}
