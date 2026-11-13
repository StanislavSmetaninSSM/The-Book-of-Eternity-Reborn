using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class MortalNpcProgressionConsequences
{
    internal sealed record ParseResult<T>(T? Value, IReadOnlyList<Problem> Problems) where T : class
    {
        internal bool IsValid => Value is not null && Problems.Count == 0;
    }

    // Causal RED compile stubs only. Numeric profile fields remain owned by the numeric contract.
    internal static ParseResult<ProfileConsequences> ParseProfile(JsonObject progressionProfile) => new(new(true), []);
    internal static ParseResult<CharacteristicPolicy> ParseWorldPolicy(JsonObject? worldSetting) =>
        new(new(CharacteristicMode.None), []);
}
