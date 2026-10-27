using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Detached source DATA for the healing check; this type grants no mutation or publication authority.</summary>
internal sealed record SpiritualHealingResolverInput(
    WoundMaterializationEnvelope Before,
    WoundOwnerCoordinate Healer,
    JsonObject HealerProfile,
    JsonObject? PlayerSoul,
    EffectMechanicsSnapshot Effects,
    string SealedRollMode,
    IReadOnlyList<int> SealedRolls);

/// <summary>Recomputable healing decision, separate from accepted-turn authority.</summary>
internal sealed record SpiritualHealingResolution(
    string WoundId,
    WoundOwnerCoordinate Target,
    WoundOwnerCoordinate Healer,
    SpiritualHealingCalculation Calculation,
    IReadOnlyList<int> SourceRolls,
    IReadOnlyList<EffectRollContributionEvidence> RollContributions);

/// <summary>Either a complete healing decision or source validation failures.</summary>
internal sealed record SpiritualHealingResolutionResult(
    SpiritualHealingResolution? Resolution,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Resolution is not null && Issues.Count == 0;
}

/// <summary>Resolves a spiritual healing check from exact actor/art/effect and retained die data.</summary>
internal static class SpiritualHealingResolver
{
    /// <summary>Checks the real source inputs before using the existing healing arithmetic.</summary>
    /// <param name="input">Detached data exported by the current healing authority.</param>
    /// <returns>A recomputable result without permission to publish or mutate.</returns>
    internal static SpiritualHealingResolutionResult Resolve(SpiritualHealingResolverInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        // Test-first scaffold. The executable authority/check implementation follows the semantic RED.
        return new(null, new[] { new ValidationIssue("spiritualHealing", IssueSeverity.Error,
            "Spiritual healing check is not implemented.", code: "spiritual_healing_not_implemented") });
    }
}
