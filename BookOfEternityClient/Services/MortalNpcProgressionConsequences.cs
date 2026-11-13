using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Pure consequence validation over detached original and accepted candidate DATA.</summary>
internal static class MortalNpcProgressionConsequences
{
    internal sealed record Problem(string Code, string Field);
    internal sealed record ProfileConsequences(bool Progresses, int LevelsPerAward = 2);
    internal enum CharacteristicMode { None, FixedIncrements, AllocatedPoints }
    internal sealed record CharacteristicPolicy(CharacteristicMode Mode,
        IReadOnlyDictionary<string, int>? PerLevel = null, int PointsPerLevel = 0,
        IReadOnlySet<string>? AllocationKeys = null);

    // Causal RED baseline only. No validation implementation until observed failing tests.
    internal static IReadOnlyList<Problem> Evaluate(JsonObject originalActor, JsonObject candidateActor,
        JsonArray? skillAwards, ProfileConsequences profile, CharacteristicPolicy worldPolicy,
        IReadOnlySet<string> allowedKeys) => [];
}
