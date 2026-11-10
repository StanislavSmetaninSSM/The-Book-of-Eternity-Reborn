using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnInputComposer
{
    /// <summary>
    /// Validates NPC this-turn profile writes using the already retained source
    /// roots. Baselines are exact-owner local, never a global skill-name lookup.
    /// Carrier moves and unrelated actor changes do not revalidate carry-over.
    /// </summary>
    /// <param name="preTurnRoot">
    /// The validated pre-turn NPC source root.
    /// </param>
    /// <param name="acceptedRoot">
    /// Actual current canonical carriers and unconsumed bounded commands.
    /// </param>
    /// <param name="issues">
    /// Receives bounded composition diagnostics at their exact profile fields.
    /// </param>
    private static void ValidateAcceptedNpcSkillProfiles(
        JsonNode? preTurnRoot,
        JsonNode? acceptedRoot,
        List<ValidationIssue> issues)
    {
        if (acceptedRoot is not JsonObject accepted || JsonNode.DeepEquals(preTurnRoot, acceptedRoot))
            return;

        var baselineActors = EnumerateCanonicalNpcActors(preTurnRoot).ToArray();
        foreach (var actor in EnumerateCanonicalNpcActors(accepted))
        {
            var baseline = FindExactProfileBaselineActor(actor, baselineActors);
            var actorId = ReadFirstExact(actor, "NPCId", "npcId", "id");
            var path = NpcCoreChangesContract.NpcCorePath + actor.GetPath()[1..];
            ValidateChangedProfileArray(actor["activeSkills"], baseline?["activeSkills"],
                path + ".activeSkills", false, actorId, issues);
            ValidateChangedProfileArray(actor["passiveSkills"], baseline?["passiveSkills"],
                path + ".passiveSkills", true, actorId, issues);
            ValidateFateCardProfileWrites(actor["fateCards"], baseline?["fateCards"],
                path + ".fateCards", actorId, issues);
        }

        // NPCCoreChanges is not a canonical actor carrier. Inspect only its
        // explicitly authored reward additions, not untouched actor state.
        if (accepted[NpcCoreChangesContract.PropertyName] is not JsonArray commands)
            return;
        for (var index = 0; index < commands.Count; index++)
        {
            if (commands[index] is not JsonObject command)
                continue;
            ValidateFateCardProfileWrites(command["fateCardsToAdd"], null,
                $"{NpcCoreChangesContract.NpcCorePath}.{NpcCoreChangesContract.PropertyName}[{index}].fateCardsToAdd",
                ReadFirstExact(command, "NPCId"), issues);
        }
    }

    /// <summary>
    /// Finds a unique strict permanent owner in pre-turn canonical carriers.
    /// Existing actor identity validators remain responsible for ambiguity.
    /// </summary>
    /// <param name="actor">
    /// Actor whose unchanged profiles may be carried over.
    /// </param>
    /// <param name="baselineActors">
    /// Already parsed pre-turn actors, with identical mirrors deduplicated.
    /// </param>
    /// <returns>
    /// The unique exact owner, or null without borrowing another actor's state.
    /// </returns>
    private static JsonObject? FindExactProfileBaselineActor(
        JsonObject actor,
        IReadOnlyList<JsonObject> baselineActors)
    {
        if (!GuardianPolicyContracts.TryResolveStrictPermanentNpcId(actor, out var actorId))
            return null;
        var matches = baselineActors.Where(baseline =>
            GuardianPolicyContracts.TryResolveStrictPermanentNpcId(baseline, out var baselineId) &&
            string.Equals(actorId, baselineId, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    /// <summary>
    /// Checks only new or structurally changed profiles in one exact owner slot.
    /// Complete equality includes every identity and profile field, so an update
    /// cannot reuse validation merely because its name or skillId is unchanged.
    /// </summary>
    /// <param name="current">
    /// Current full profiles in the accepted owner slot.
    /// </param>
    /// <param name="baseline">
    /// Pre-turn profiles from that same exact owner and slot.
    /// </param>
    /// <param name="path">
    /// Exact path of the authored current array.
    /// </param>
    /// <param name="passive">
    /// Whether these are passive profiles.
    /// </param>
    /// <param name="actorId">
    /// Actor identity used for diagnostic attribution only.
    /// </param>
    /// <param name="issues">
    /// Receives changed-profile diagnostics.
    /// </param>
    private static void ValidateChangedProfileArray(
        JsonNode? current,
        JsonNode? baseline,
        string path,
        bool passive,
        string? actorId,
        List<ValidationIssue> issues)
    {
        if (current is not JsonArray profiles || JsonNode.DeepEquals(current, baseline))
            return;
        var oldProfiles = (baseline as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        for (var index = 0; index < profiles.Count; index++)
        {
            if (profiles[index] is not JsonObject profile ||
                oldProfiles.Any(old => JsonNode.DeepEquals(old, profile)))
                continue;
            ValidateSkillProfileWrite(profile, $"{path}[{index}]", passive, issues, actorId);
        }
    }

    /// <summary>
    /// Applies the same profile contract to NPC Fate Card reward writes, including
    /// new NPC creation, full actor updates and bounded Fate Card additions.
    /// </summary>
    /// <param name="current">
    /// Authored current Fate Cards.
    /// </param>
    /// <param name="baseline">
    /// Pre-turn cards of the same exact actor, if any.
    /// </param>
    /// <param name="path">
    /// Exact authored Fate Card array path.
    /// </param>
    /// <param name="actorId">
    /// Owning actor diagnostic identity.
    /// </param>
    /// <param name="issues">
    /// Receives changed reward profile diagnostics.
    /// </param>
    private static void ValidateFateCardProfileWrites(
        JsonNode? current,
        JsonNode? baseline,
        string path,
        string? actorId,
        List<ValidationIssue> issues)
    {
        if (current is not JsonArray cards || JsonNode.DeepEquals(current, baseline))
            return;
        var oldCards = (baseline as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        for (var index = 0; index < cards.Count; index++)
        {
            if (cards[index] is not JsonObject card || card["rewards"] is not JsonObject rewards)
                continue;
            var cardId = ReadFirstExact(card, "cardId");
            var matches = cardId == null ? [] : oldCards.Where(old =>
                string.Equals(ReadFirstExact(old, "cardId"), cardId, StringComparison.Ordinal)).ToArray();
            var previousRewards = matches.Length == 1 ? matches[0]["rewards"] as JsonObject : null;
            ValidateChangedProfileArray(rewards["newActiveSkills"], previousRewards?["newActiveSkills"],
                $"{path}[{index}].rewards.newActiveSkills", false, actorId, issues);
            ValidateChangedProfileArray(rewards["newPassiveSkills"], previousRewards?["newPassiveSkills"],
                $"{path}[{index}].rewards.newPassiveSkills", true, actorId, issues);
        }
    }
}
