using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record ResourceRegisteredSystemOutcomeProjectionResult(
    IReadOnlyDictionary<string, JsonObject> CompanionAfterImages,
    IReadOnlyList<AcceptedMechanicsOwnerTransition> OwnerTransitions,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Issues.Count == 0;
}

/// <summary>
/// A validated client-owned outcome contributes source authority and mutations
/// before planning, then derives companion after-images from that same resource
/// result. It never invokes a second reducer or publishes independently.
/// </summary>
internal interface IResourceRegisteredSystemOutcomeDraft
{
    string Fingerprint { get; }
    IReadOnlyList<ResourceMutationSourceExport> SourceExports { get; }
    IReadOnlyList<ResourceMutationIntent> Mutations { get; }
    IReadOnlyDictionary<string, CanonicalBeforeImage> ExpectedBeforeImages { get; }
    ResourceRegisteredSystemOutcomeProjectionResult Project(
        AcceptedMechanicsResourcePlanningResult resourceResult);
}

internal sealed record ResourceLossRecoveryTarget(
    ResourceCoordinate Coordinate,
    ResourceOperation Operation);
