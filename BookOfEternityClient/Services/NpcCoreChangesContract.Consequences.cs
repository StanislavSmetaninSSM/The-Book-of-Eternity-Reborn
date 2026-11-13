using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class NpcCoreChangesContract
{
    private static void ValidateConsequencePreflight(JsonNode? progressionNode, JsonObject? baselineActor,
        Authority authority, string context, string? npcId, Evaluation evaluation)
    {
        // Source, numeric and direct-mutation checks run in the shared core evaluator.
        // Only checking the completed consequence state may be deferred to skill composition.
        if (progressionNode is not JsonObject progression) return;
        if (progression.ContainsKey("skillAwards") && progression["skillAwards"] is not JsonArray)
            evaluation.Issues.Add(Error(context + ".skillAwards", "npc_progression_skill_award_invalid",
                "Present skillAwards must be an array; omission alone means no claims.", npcId));
        foreach (var problem in authority.NpcCharacteristicPolicy.Problems)
            evaluation.Issues.Add(Error(problem.Field, problem.Code, "Invalid current-world NPC progression policy.", npcId));
        if (!authority.NpcCharacteristicPolicy.IsValid && authority.NpcCharacteristicPolicy.Problems.Count == 0)
            evaluation.Issues.Add(Error(context, "npc_progression_consequence_policy_invalid",
                "Current-world NPC progression policy is unavailable.", npcId));
        if (baselineActor is null || npcId is null) return;
        if (baselineActor["progressionProfile"] is not JsonObject profile)
        {
            evaluation.Issues.Add(Error(context, "npc_progression_consequence_policy_invalid",
                "Explicit progression profile is required.", npcId));
            return;
        }
        var parsed = MortalNpcProgressionConsequences.ParseProfile(profile);
        foreach (var problem in parsed.Problems)
            evaluation.Issues.Add(Error(problem.Field, problem.Code, "Invalid NPC progression consequence declaration.", npcId));
        if (parsed.IsValid) evaluation.ConsequenceProfiles[npcId] = parsed.Value!;
    }

    /// <summary>Checks the existing plans against the actual final candidate, without reducing or writing it.</summary>
    internal static IReadOnlyList<ValidationIssue> ValidateFinalConsequences(JsonObject originalRoot,
        JsonObject finalRoot, Authority authority, Evaluation evaluation)
    {
        var issues = new List<ValidationIssue>();
        if (evaluation.Issues.Count != 0) return issues;
        var originals = CollectActors(originalRoot);
        var finals = CollectActors(finalRoot);
        foreach (var plan in evaluation.Plans)
        {
            if (plan.Command["progression"] is not JsonObject progression) continue;
            if (!evaluation.ConsequenceProfiles.TryGetValue(plan.NpcId, out var profile) ||
                !authority.NpcCharacteristicPolicy.IsValid)
            {
                issues.Add(Error(NpcCorePath, "npc_progression_consequence_policy_invalid",
                    "Progression consequence preflight DATA is missing.", plan.NpcId));
                continue;
            }
            var original = originals.FirstOrDefault(actor => actor.NpcId == plan.NpcId);
            var targets = finals.Where(actor => actor.NpcId == plan.NpcId).ToArray();
            if (original is null || targets.Length == 0)
            {
                issues.Add(Error(NpcCorePath, "npc_progression_consequence_owner",
                    "Progression actor disappeared from the final candidate.", plan.NpcId));
                continue;
            }
            // Preflight has distinguished omitted from present-null/non-array claims.
            var awards = progression.ContainsKey("skillAwards") ? progression["skillAwards"]!.AsArray() : null;
            foreach (var target in targets)
                foreach (var problem in MortalNpcProgressionConsequences.Evaluate(original.Actor, target.Actor,
                    awards, profile, authority.NpcCharacteristicPolicy.Value!, authority.WorldCharacteristicKeys))
                    issues.Add(Error(ConsequenceDiagnosticPath(plan, target.Path, problem), problem.Code,
                        "Final NPC progression consequences do not match the declared policy and awards.", plan.NpcId));
        }
        return issues;
    }

    private static string ConsequenceDiagnosticPath(ChangePlan plan, string actorPath,
        MortalNpcProgressionConsequences.Problem problem)
    {
        var commandPath = $"{NpcCorePath}.{PropertyName}[{plan.CommandIndex}]";
        if (problem.Code is "npc_progression_skill_award_count" or "npc_progression_skill_award_invalid" or
            "npc_progression_skill_award_unfulfilled")
            return commandPath + ".progression." + problem.Field;
        if (problem.Code == "npc_progression_characteristic_authority")
            return "lore/current_world/world_setting.json.npcProgression.characteristics";
        if (problem.Code == "npc_progression_characteristic_delta")
            return commandPath + ".characteristicValues" +
                (problem.Field is "characteristics" or "characteristics.total" ? "" : "." + problem.Field);
        return actorPath + "." + problem.Field;
    }
}
