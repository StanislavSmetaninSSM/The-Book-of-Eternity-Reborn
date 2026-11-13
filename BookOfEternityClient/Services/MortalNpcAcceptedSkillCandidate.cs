using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Pure ordinary NPC candidate DATA. Callers authenticate originals/current inputs and
/// retain responsibility for realm/source fences, leases, publication and rollback.
/// </summary>
internal static class MortalNpcAcceptedSkillCandidate
{
    internal sealed record Result(JsonObject? Core, JsonObject? Sidecar, IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool IsValid => Core is not null && Sidecar is not null && Issues.Count == 0;
    }

    // Causal RED baseline: pending ordinary NPC skill commands are not yet reduced.
    internal static Result Compose(
        JsonObject originalCore,
        JsonObject originalSidecar,
        JsonObject currentCore,
        JsonObject currentSidecar,
        NpcCoreChangesContract.Authority authority) =>
        new(currentCore.DeepClone().AsObject(), currentSidecar.DeepClone().AsObject(), []);
}
