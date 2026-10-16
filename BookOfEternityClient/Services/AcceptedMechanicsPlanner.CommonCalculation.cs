using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    // Detached calculation admission only; not a pending/publication capability.
    // Unwired until the bounded behavioral RED packet has been preserved.
    internal static IReadOnlyList<ValidationIssue> ValidateUnboundedCommonBoundary(
        string sessionId, string requestId, int turn,
        ResourcePendingResolutionState? pendingState, JsonObject effectCommands,
        IReadOnlyList<AcceptedEffectBoundedResourceResolution> candidates) =>
        throw new NotImplementedException();
}
