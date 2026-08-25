using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal sealed record ProjectedResourceSeed(
    string Realm,
    ResourceOwnerKind OwnerKind,
    string OwnerId,
    string ResourceKey,
    decimal Current,
    decimal Maximum,
    ResourceLifecycleState State = ResourceLifecycleState.Active);

internal static class ResourceProjectionFixture
{
    private const string Fingerprint =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    internal static async Task SeedAsync(
        FileSystemManager fs,
        params ProjectedResourceSeed[] resources)
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        if (!bootstrap.IsValid || bootstrap.Definitions == null)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, bootstrap.Issues));
        }

        var stateEntries = new List<ResourceStateEntry>();
        var transitions = new List<ResourceTransition>();
        var sequence = 0;
        foreach (var resource in resources)
        {
            if (!bootstrap.Definitions.TryResolveExact(resource.ResourceKey, out var definition) ||
                definition == null)
            {
                throw new InvalidOperationException(
                    $"Unknown resource definition '{resource.ResourceKey}'.");
            }
            if (resource.Current < definition.MinimumPolicy.Value ||
                resource.Current > resource.Maximum)
            {
                throw new InvalidOperationException(
                    $"Invalid current/maximum for '{resource.ResourceKey}'.");
            }
            if (resource.State is not ResourceLifecycleState.Active and
                not ResourceLifecycleState.Suspended)
            {
                throw new InvalidOperationException(
                    $"Unsupported lifecycle state for '{resource.ResourceKey}'.");
            }

            var coordinate = new ResourceCoordinate(
                resource.Realm,
                resource.OwnerKind,
                resource.OwnerId,
                resource.ResourceKey);
            var binding = new ResourceCapacityBinding(
                definition.CapacityPolicy.Kind,
                definition.CapacityPolicy.FormulaKey ??
                $"capacity_{resource.OwnerId}_{resource.ResourceKey}",
                Fingerprint);
            var initialized = new ResourceStateSnapshot(
                resource.Maximum,
                resource.Maximum,
                binding,
                ResourceLifecycleState.Active);
            var initialize = new ResourceTransition(
                $"transition_fixture_initialize_{sequence + 1}",
                $"operation_fixture_initialize_{sequence + 1}",
                $"turn_1:resource:{sequence + 1}",
                "owner_materialization",
                $"owner_materialization_{sequence + 1}",
                ResourceMutationPhase.RegisteredSystemOutcome,
                50,
                sequence++,
                coordinate,
                ResourceTransitionOperation.Initialize,
                0m,
                0m,
                ResourceTransitionOutcome.Applied,
                ResourceCapacityDisposition.InitializeFromDefinition,
                null,
                initialized,
                new ResourceSourceEvidence(
                    "owner_materialization",
                    $"owner_materialization_{sequence}",
                    Fingerprint),
                Fingerprint,
                null,
                1);
            transitions.Add(initialize);

            var latest = initialize;
            var after = initialized;
            if (resource.Current != resource.Maximum)
            {
                var operation = definition.AllowedOperations.Contains(ResourceOperation.Damage)
                    ? ResourceTransitionOperation.Damage
                    : ResourceTransitionOperation.Spend;
                var amount = resource.Maximum - resource.Current;
                after = initialized with { Current = resource.Current };
                latest = new ResourceTransition(
                    $"transition_fixture_change_{sequence + 1}",
                    $"operation_fixture_change_{sequence + 1}",
                    $"turn_2:resource:{sequence + 1}",
                    "ordinary_action",
                    $"ordinary_action_{sequence + 1}",
                    ResourceMutationPhase.DirectOutcome,
                    100,
                    sequence++,
                    coordinate,
                    operation,
                    amount,
                    amount,
                    ResourceTransitionOutcome.Applied,
                    null,
                    initialized,
                    after,
                    new ResourceSourceEvidence(
                        "ordinary_action",
                        $"ordinary_action_{sequence}",
                        Fingerprint),
                    Fingerprint,
                    null,
                    2);
                transitions.Add(latest);
            }

            if (resource.State == ResourceLifecycleState.Suspended)
            {
                var beforeSuspend = after;
                after = beforeSuspend with { State = ResourceLifecycleState.Suspended };
                latest = new ResourceTransition(
                    $"transition_fixture_suspend_{sequence + 1}",
                    $"operation_fixture_suspend_{sequence + 1}",
                    $"turn_3:resource:{sequence + 1}",
                    "owner_lifecycle",
                    $"owner_lifecycle_{sequence + 1}",
                    ResourceMutationPhase.RegisteredSystemOutcome,
                    50,
                    sequence++,
                    coordinate,
                    ResourceTransitionOperation.Suspend,
                    0m,
                    0m,
                    ResourceTransitionOutcome.Applied,
                    null,
                    beforeSuspend,
                    after,
                    new ResourceSourceEvidence(
                        "owner_lifecycle",
                        $"owner_lifecycle_{sequence}",
                        Fingerprint),
                    Fingerprint,
                    null,
                    3);
                transitions.Add(latest);
            }

            stateEntries.Add(new ResourceStateEntry(
                coordinate,
                after.Current,
                after.Maximum,
                binding,
                after.State,
                new ResourceChronology(
                    1,
                    initialize.EventRef,
                    latest.TransitionId,
                    latest.EventRef,
                    latest.Turn)));
        }

        var history = ResourceHistoryState.CreateValidated(
            transitions,
            bootstrap.Definitions);
        if (!history.IsValid || history.History == null)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, history.Issues));
        }
        var state = new ResourceStateLedger(stateEntries);
        var agreement = history.History.ValidateStateAgreement(state);
        if (agreement.Count != 0)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, agreement));
        }

        await fs.WriteFileAtomicAsync(
            ResourceMaterializationContract.DefinitionsPath,
            bootstrap.Definitions.ToCanonicalJson());
        await fs.WriteFileAtomicAsync(
            ResourceMaterializationContract.StatePath,
            state.ToCanonicalJson());
        await fs.WriteFileAtomicAsync(
            ResourceMaterializationContract.HistoryPath,
            history.History.ToCanonicalJson());
    }
}
