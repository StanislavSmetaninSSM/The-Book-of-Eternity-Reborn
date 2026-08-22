using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourcePlayerPrivacyTests
{
    private const string Fingerprint =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Theory]
    [InlineData("game_state/resources/resource_state.json")]
    [InlineData("transitionId: transition_secret")]
    [InlineData("receiptId=receipt_secret")]
    [InlineData("repair agent fingerprint")]
    public void Project_PlayerVisibleDefinitionWithProtectedDisplayTextFailsClosed(
        string displayName)
    {
        var definition = new ResourceDefinition(
            "safe_reserve",
            1,
            displayName,
            ResourceNumericKind.Integer,
            "point",
            1m,
            new ResourceMinimumPolicy(ResourceMinimumKind.DefinitionFixed, 0m),
            new ResourceCapacityPolicy(ResourceCapacityKind.InstanceFixed, null, null),
            new ResourceInitializationPolicy(ResourceInitializationKind.Maximum, null, null),
            new HashSet<ResourceOwnerKind> { ResourceOwnerKind.Player },
            new HashSet<ResourceOperation> { ResourceOperation.Spend, ResourceOperation.Gain },
            ResourceBoundPolicy.RejectBelowMinimum,
            ResourceBoundPolicy.ClampToMaximum,
            ResourceVisibility.PlayerVisible,
            new ResourceDefinitionMaterialization(
                1,
                "definition_safe_reserve",
                "seal_safe_reserve",
                1,
                "turn_1"));
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn().With(definition);
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Player,
            "player_internal_42",
            definition.ResourceKey);
        var binding = new ResourceCapacityBinding(
            ResourceCapacityKind.InstanceFixed,
            "capacity_safe_reserve",
            Fingerprint);
        var snapshot = new ResourceStateSnapshot(
            5m,
            5m,
            binding,
            ResourceLifecycleState.Active);
        var transition = new ResourceTransition(
            "transition_initialize_safe_reserve",
            "operation_initialize_safe_reserve",
            "turn_1:resource:1",
            "owner_materialization",
            "owner_materialization_safe_reserve",
            ResourceMutationPhase.RegisteredSystemOutcome,
            50,
            0,
            coordinate,
            ResourceTransitionOperation.Initialize,
            0m,
            0m,
            ResourceTransitionOutcome.Applied,
            ResourceCapacityDisposition.InitializeFromDefinition,
            null,
            snapshot,
            new ResourceSourceEvidence(
                "owner_materialization",
                "owner_materialization_safe_reserve",
                Fingerprint),
            Fingerprint,
            null,
            1);
        var historyResult = ResourceHistoryState.CreateValidated(
            new[] { transition },
            definitions);
        Assert.True(historyResult.IsValid, string.Join(Environment.NewLine, historyResult.Issues));
        var state = new ResourceStateLedger(new[]
        {
            new ResourceStateEntry(
                coordinate,
                5m,
                5m,
                binding,
                ResourceLifecycleState.Active,
                new ResourceChronology(
                    1,
                    transition.EventRef,
                    transition.TransitionId,
                    transition.EventRef,
                    1))
        });
        var input = new ResourceProjectionInput(
            definitions.ToCanonicalRoot(),
            JsonNode.Parse(state.ToCanonicalJson())!.AsObject(),
            JsonNode.Parse(historyResult.History!.ToCanonicalJson())!.AsObject(),
            Fingerprint);

        var result = ResourceProjectionService.Project(
            new ResourceProjectionRequest(
                input,
                new[]
                {
                    new ResourceProjectionOwnerScope(
                        new ResourceOwnerKey(
                            coordinate.Realm,
                            coordinate.OwnerKind,
                            coordinate.ResourceOwnerId),
                        "герой",
                        IsOwningPlayer: true)
                }),
            ResourceProjectionAudience.Player);

        Assert.False(result.IsAvailable);
        Assert.Empty(result.Rows);
        Assert.Equal(ResourcePlayerFailureMessages.Unavailable, result.UnavailableMessage);
        Assert.DoesNotContain(displayName, result.UnavailableMessage, StringComparison.OrdinalIgnoreCase);
    }
}
