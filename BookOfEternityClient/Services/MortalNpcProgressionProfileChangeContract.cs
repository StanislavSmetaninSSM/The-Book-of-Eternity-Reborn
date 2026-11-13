using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Pure explicit profile-change DATA validation; caller owns actor/source/realm/publication authority.</summary>
internal static class MortalNpcProgressionProfileChangeContract
{
    internal sealed record Problem(string Code, string Field);
    internal sealed record Result(JsonObject? CandidateActor, IReadOnlyList<Problem> Problems)
    {
        internal bool IsValid => CandidateActor is not null && Problems.Count == 0;
    }

    // Causal RED compile scaffold only. No transition is implemented yet.
    internal static Result Evaluate(JsonObject originalActor, JsonObject command) =>
        new(originalActor.DeepClone().AsObject(), []);
}
