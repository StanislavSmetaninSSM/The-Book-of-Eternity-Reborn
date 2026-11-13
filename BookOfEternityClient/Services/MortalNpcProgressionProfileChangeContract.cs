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

    internal static Result Evaluate(JsonObject originalActor, JsonObject command)
    {
        if (!Only(command, "NPCId", "reason", "progressionProfileChange") ||
            !ExactText(command["NPCId"]) || !ExactText(command["reason"]) ||
            command["progressionProfileChange"] is not JsonObject change ||
            !Only(change, "source", "operation", "profile", "level", "experience", "experienceForNextLevel") ||
            change["source"] is not JsonObject || change["operation"] is not JsonValue operationNode ||
            !operationNode.TryGetValue<string>(out var operation) || operation is not ("install" or "replace"))
            return Reject("npc_profile_change_command_invalid", "progressionProfileChange");

        // Presence is exact, including malformed/null values. A lookalike is never permission to install.
        var profileKey = MortalLocationIdentityState.BuildConfusableKey("progressionProfile");
        var hasProfileLike = originalActor.Any(pair => MortalLocationIdentityState.BuildConfusableKey(pair.Key) == profileKey);
        if (operation == "install" && hasProfileLike)
            return Reject("npc_profile_change_install_requires_absence", "progressionProfile");
        if (operation == "replace")
        {
            if (originalActor.Any(pair => pair.Key != "progressionProfile" &&
                    MortalLocationIdentityState.BuildConfusableKey(pair.Key) == profileKey) ||
                originalActor["progressionProfile"] is not JsonObject originalProfile ||
                !MortalNpcProgressionContract.ValidateCanonicalActor(originalActor).IsValid ||
                !MortalNpcProgressionConsequences.ParseProfile(originalProfile).IsValid)
                return Reject("npc_profile_change_original_invalid", "progressionProfile");
        }
        if (!Integer(originalActor["level"], out var level) || level < 0 ||
            !Integer(originalActor["experience"], out var experience) || experience < 0 ||
            !Integer(originalActor["experienceForNextLevel"], out var originalThreshold) || originalThreshold < 0 ||
            (originalThreshold == 0 ? experience != 0 : experience >= originalThreshold))
            return Reject("npc_profile_change_original_invalid", "originalTuple");
        if (!Integer(change["level"], out var resultLevel) || !Integer(change["experience"], out var resultExperience) ||
            resultLevel != level || resultExperience != experience)
            return Reject("npc_profile_change_assets_changed", "progressionProfileChange");
        if (change["profile"] is not JsonObject profile || !MortalNpcProgressionConsequences.ParseProfile(profile).IsValid)
            return Reject("npc_profile_change_result_invalid", "progressionProfileChange.profile");

        var candidate = originalActor.DeepClone().AsObject();
        candidate["progressionProfile"] = profile.DeepClone();
        foreach (var field in new[] { "level", "experience", "experienceForNextLevel" })
            candidate[field] = change[field]?.DeepClone();
        var numeric = MortalNpcProgressionContract.ValidateCanonicalActor(candidate);
        if (!numeric.IsValid)
            return new(null, numeric.Problems.Select(problem =>
                new Problem("npc_profile_change_result_invalid", "progressionProfileChange." +
                    (problem.Field == "progressionProfile" ? "profile" :
                     problem.Field.StartsWith("progressionProfile.", StringComparison.Ordinal)
                         ? "profile." + problem.Field["progressionProfile.".Length..] : problem.Field))).ToArray());
        return new(candidate, []);
    }

    private static bool Only(JsonObject root, params string[] fields) =>
        root.All(pair => fields.Contains(pair.Key, StringComparer.Ordinal));
    private static bool ExactText(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) &&
        !string.IsNullOrWhiteSpace(text) && text == text.Trim();
    private static bool Integer(JsonNode? node, out int result)
    {
        result = 0;
        return node is JsonValue value && value.TryGetValue(out result);
    }
    private static Result Reject(string code, string field) => new(null, [new(code, field)]);
}
