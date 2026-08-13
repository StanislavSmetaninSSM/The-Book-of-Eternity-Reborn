using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class NpcCoreChangesContract
{
    internal const string PropertyName = "NPCCoreChanges";
    internal const string NpcCorePath = "game_state/npcs/npc_core.json";

    private static readonly HashSet<string> CommandKeys = new(StringComparer.Ordinal)
    {
        "NPCId",
        "reason",
        "profile",
        "location",
        "progression",
        "characteristicValues",
        "factionAffiliationsToUpsert",
        "fateCardsToAdd",
        "fateCardIdsToRemove"
    };

    private static readonly HashSet<string> ProfileKeys = new(StringComparer.Ordinal)
    {
        "worldview", "race", "history"
    };

    private static readonly HashSet<string> LocationKeys = new(StringComparer.Ordinal)
    {
        "currentLocationId", "initialLocationId"
    };

    private static readonly HashSet<string> ProgressionKeys = new(StringComparer.Ordinal)
    {
        "level",
        "experience",
        "experienceForNextLevel",
        "progressionType",
        "lastPlayerXPValueOnSync"
    };

    private static readonly HashSet<string> FactionAffiliationKeys = new(StringComparer.Ordinal)
    {
        "factionId", "factionName", "rank", "branch", "membershipStatus"
    };

    private static readonly HashSet<string> AllowedMembershipStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active", "Former", "Exiled", "Undercover", "Ally", "Enemy"
    };

    private static readonly HashSet<string> FateCardKeys = new(StringComparer.Ordinal)
    {
        "cardId", "name", "image_prompt", "description", "unlockConditions", "rewards", "isUnlocked"
    };

    private static readonly HashSet<string> FateCardUnlockConditionKeys = new(StringComparer.Ordinal)
    {
        "requiredRelationshipLevel", "plotConditionDescription", "conjunction"
    };

    private static readonly HashSet<string> FateCardRewardKeys = new(StringComparer.Ordinal)
    {
        "description",
        "newActiveSkills",
        "newPassiveSkills",
        "statBoosts",
        "newServices",
        "otherNarrativeRewards",
        "tacticalTriggers"
    };

    private static readonly HashSet<string> TacticalTriggerKeys = new(StringComparer.Ordinal)
    {
        "triggerCondition", "newTargetPriority", "description", "newActionPreference"
    };

    private static readonly HashSet<string> ActiveSkillKeys = new(StringComparer.Ordinal)
    {
        "skillId", "id", "skillName", "displayName", "skillDescription", "description", "rarity",
        "actionCost", "combatEffect", "scalingCharacteristic", "scalesValue", "scalesDuration",
        "scalesChance", "energyCost", "cooldownTurns", "timeCost", "masteryLevel",
        "currentMasteryLevel", "maxMasteryLevel", "currentMasteryProgress", "masteryProgressNeeded",
        "image_prompt", "category", "tags"
    };

    private static readonly HashSet<string> PassiveSkillKeys = new(StringComparer.Ordinal)
    {
        "skillId", "id", "skillName", "displayName", "skillDescription", "description", "rarity",
        "type", "group", "masteryLevel", "currentMasteryLevel", "maxMasteryLevel",
        "currentMasteryProgress", "masteryProgressNeeded", "structuredBonuses", "playerStatBonus",
        "combatEffect", "effectDetails", "knowledgeDomain", "unlockedActiveSkillsCount",
        "maxUnlockableActiveSkills", "image_prompt", "category", "tags"
    };

    private static readonly HashSet<string> CombatEffectKeys = new(StringComparer.Ordinal)
    {
        "isActivatedEffect", "actionName", "actionCost", "effects", "targetPriority", "scalingCharacteristic"
    };

    private static readonly HashSet<string> CombatEffectItemKeys = new(StringComparer.Ordinal)
    {
        "effectType", "value", "targetType", "effectDescription", "targetTypeDisplayName", "targetsCount",
        "duration", "poiseDamage", "damageThreshold", "chance", "source", "sourceSkill"
    };

    private static readonly HashSet<string> StructuredBonusKeys = new(StringComparer.Ordinal)
    {
        "characteristic", "characteristicName", "stat", "value", "bonus", "amount", "type",
        "condition", "description", "isPercentage"
    };

    private static readonly HashSet<string> AllowedCombatActionCosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Main", "Fast", "Free"
    };

    private static readonly HashSet<string> AllowedPassiveSkillTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "KnowledgeBased", "CharacteristicBonus", "BodyModification", "CombatEnhancement", "Utility"
    };

    private static readonly HashSet<string> ActorFieldsWithDedicatedContinuityValidation =
        new(StringComparer.Ordinal)
        {
            "inventory"
        };

    internal sealed record Authority(
        HashSet<string> KnownPermanentLocationIds,
        HashSet<string> SameTurnLocationInitialIds,
        Dictionary<string, string> FactionNamesById,
        HashSet<string> WorldCharacteristicKeys);

    internal sealed record ChangePlan(string NpcId, JsonObject Command);

    internal sealed class Evaluation
    {
        internal bool HasCommand { get; init; }
        internal List<ValidationIssue> Issues { get; } = [];
        internal List<ChangePlan> Plans { get; } = [];
        internal bool CanApply => HasCommand && Issues.Count == 0 && Plans.Count > 0;
    }

    private sealed record ActorReference(string Section, int Index, string? NpcId, JsonObject Actor)
    {
        internal string Path => $"{NpcCorePath}.{Section}[{Index}]";
    }

    internal static Evaluation Evaluate(
        JsonObject currentRoot,
        JsonObject preTurnRoot,
        Authority authority,
        Func<JsonArray, string, IReadOnlyList<ValidationIssue>> validateProductionFateCards,
        bool detectDirectMutations,
        MortalActorAcceptedTurnAuthority? acceptedTurnAuthority = null)
    {
        var evaluation = new Evaluation { HasCommand = HasCommandLikeProperty(currentRoot) };
        var currentActors = CollectActors(currentRoot);
        var preTurnActors = CollectActors(preTurnRoot);

        if (detectDirectMutations)
        {
            ValidateDirectCoreMutationBypass(
                currentActors,
                preTurnActors,
                acceptedTurnAuthority,
                evaluation.Issues);
        }

        evaluation.Issues.AddRange(ValidateCommandTopLevelNames(currentRoot));
        if (evaluation.Issues.Any(issue => issue.Code == "npc_core_changes_invalid_top_level_name"))
            return evaluation;

        if (!evaluation.HasCommand)
            return evaluation;

        if (currentRoot[PropertyName] is not JsonArray commands)
        {
            evaluation.Issues.Add(Error(
                $"{NpcCorePath}.{PropertyName}",
                "npc_core_changes_invalid_shape",
                "NPCCoreChanges must be an array of bounded existing-NPC core changes."));
            return evaluation;
        }

        if (commands.Count == 0)
        {
            evaluation.Issues.Add(Error(
                $"{NpcCorePath}.{PropertyName}",
                "npc_core_changes_empty_mutation",
                "NPCCoreChanges must contain at least one mutation entry."));
            return evaluation;
        }

        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < commands.Count; index++)
        {
            var context = $"{NpcCorePath}.{PropertyName}[{index}]";
            if (commands[index] is not JsonObject command)
            {
                evaluation.Issues.Add(Error(
                    context,
                    "npc_core_changes_invalid_shape",
                    "Each NPCCoreChanges entry must be an object."));
                continue;
            }

            var issueStart = evaluation.Issues.Count;
            ValidateUnknownProperties(command, CommandKeys, context, evaluation.Issues);

            var npcId = ReadRequiredString(command, "NPCId");
            if (npcId == null)
            {
                evaluation.Issues.Add(Error(
                    $"{context}.NPCId",
                    "npc_core_changes_invalid_identity",
                    "NPCCoreChanges requires one exact permanent NPCId string."));
            }
            else if (!seenTargets.Add(npcId))
            {
                evaluation.Issues.Add(Error(
                    $"{context}.NPCId",
                    "npc_core_changes_duplicate_target",
                    "Only one NPCCoreChanges entry may target a permanent NPCId.",
                    npcId));
            }

            ActorReference? baselineActor = null;
            if (npcId != null)
            {
                baselineActor = ValidateTargetIdentity(
                    npcId,
                    context,
                    currentActors,
                    preTurnActors,
                    evaluation.Issues);
            }

            if (ReadRequiredString(command, "reason") == null)
            {
                evaluation.Issues.Add(Error(
                    $"{context}.reason",
                    "npc_core_changes_reason_required",
                    "NPCCoreChanges requires a non-empty in-world or mechanical reason.",
                    npcId));
            }

            var hasMutation = false;
            if (command.TryGetPropertyValue("profile", out var profileNode))
            {
                hasMutation |= IsNonEmptyContainer(profileNode);
                ValidateProfile(profileNode, $"{context}.profile", npcId, evaluation.Issues);
            }

            if (command.TryGetPropertyValue("location", out var locationNode))
            {
                hasMutation |= IsNonEmptyContainer(locationNode);
                ValidateLocation(
                    locationNode,
                    baselineActor?.Actor,
                    authority,
                    $"{context}.location",
                    npcId,
                    evaluation.Issues);
            }

            if (command.TryGetPropertyValue("progression", out var progressionNode))
            {
                hasMutation |= IsNonEmptyContainer(progressionNode);
                ValidateProgression(
                    progressionNode,
                    baselineActor?.Actor,
                    $"{context}.progression",
                    npcId,
                    evaluation.Issues);
            }

            if (command.TryGetPropertyValue("characteristicValues", out var characteristicNode))
            {
                hasMutation |= IsNonEmptyContainer(characteristicNode);
                ValidateCharacteristicValues(
                    characteristicNode,
                    baselineActor?.Actor,
                    authority,
                    $"{context}.characteristicValues",
                    npcId,
                    evaluation.Issues);
            }

            if (command.TryGetPropertyValue("factionAffiliationsToUpsert", out var affiliationNode))
            {
                hasMutation |= IsNonEmptyContainer(affiliationNode);
                ValidateFactionAffiliations(
                    affiliationNode,
                    authority,
                    $"{context}.factionAffiliationsToUpsert",
                    npcId,
                    evaluation.Issues);
            }

            if (command.TryGetPropertyValue("fateCardsToAdd", out var cardsToAddNode))
            {
                hasMutation |= IsNonEmptyContainer(cardsToAddNode);
                ValidateFateCardsToAdd(
                    cardsToAddNode,
                    baselineActor?.Actor,
                    $"{context}.fateCardsToAdd",
                    npcId,
                    validateProductionFateCards,
                    evaluation.Issues);
            }

            if (command.TryGetPropertyValue("fateCardIdsToRemove", out var cardsToRemoveNode))
            {
                hasMutation |= IsNonEmptyContainer(cardsToRemoveNode);
                ValidateFateCardRemovals(
                    cardsToRemoveNode,
                    baselineActor?.Actor,
                    command["fateCardsToAdd"] as JsonArray,
                    $"{context}.fateCardIdsToRemove",
                    npcId,
                    evaluation.Issues);
            }

            if (!hasMutation)
            {
                evaluation.Issues.Add(Error(
                    context,
                    "npc_core_changes_empty_mutation",
                    "NPCCoreChanges requires at least one non-empty mutation group.",
                    npcId));
            }

            if (npcId != null && evaluation.Issues.Count == issueStart)
                evaluation.Plans.Add(new ChangePlan(npcId, command.DeepClone().AsObject()));
        }

        return evaluation;
    }

    internal static bool HasCommandLikeProperty(JsonObject root) =>
        root.Any(property =>
            string.Equals(property.Key, PropertyName, StringComparison.OrdinalIgnoreCase));

    internal static IReadOnlyList<ValidationIssue> ValidateCommandTopLevelNames(JsonObject root) =>
        root
            .Where(property =>
                string.Equals(property.Key, PropertyName, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(property.Key, PropertyName, StringComparison.Ordinal))
            .Select(property => Error(
                $"{NpcCorePath}.{property.Key}",
                "npc_core_changes_invalid_top_level_name",
                $"The command name must be exactly {PropertyName}."))
            .ToList();

    internal static void Apply(JsonObject root, Evaluation evaluation)
    {
        if (!evaluation.CanApply)
            return;

        foreach (var plan in evaluation.Plans)
        {
            var targets = CollectActors(root)
                .Where(actor => string.Equals(actor.NpcId, plan.NpcId, StringComparison.Ordinal))
                .ToList();
            foreach (var target in targets)
                ApplyCommand(target.Actor, plan.Command);
        }

        root.Remove(PropertyName);
    }

    internal static async Task<Authority> ReadAuthorityAsync(FileSystemManager fs)
    {
        var permanentLocationIds = new HashSet<string>(StringComparer.Ordinal);
        var sameTurnLocationIds = new HashSet<string>(StringComparer.Ordinal);
        await ReadExactLocationAuthorityAsync(
            fs,
            permanentLocationIds,
            sameTurnLocationIds);

        var factionNamesById = new Dictionary<string, string>(StringComparer.Ordinal);
        var factionRoot = await ReadObjectAsync(fs, "game_state/factions/faction_core.json");
        if (factionRoot != null)
            CollectFactionAuthority(factionRoot, factionNamesById);

        var characteristicKeys = new HashSet<string>(StringComparer.Ordinal);
        var characteristicsRoot = await ReadObjectAsync(fs, "game_state/misc/characteristics.json");
        if (characteristicsRoot != null)
        {
            foreach (var property in characteristicsRoot)
            {
                if (!property.Key.StartsWith("_", StringComparison.Ordinal) &&
                    property.Value is JsonValue)
                {
                    characteristicKeys.Add(property.Key);
                }
            }
        }

        return new Authority(
            permanentLocationIds,
            sameTurnLocationIds,
            factionNamesById,
            characteristicKeys);
    }

    private static ActorReference? ValidateTargetIdentity(
        string npcId,
        string context,
        IReadOnlyList<ActorReference> currentActors,
        IReadOnlyList<ActorReference> preTurnActors,
        List<ValidationIssue> issues)
    {
        if (HasInvalidPermanentAliasCandidate(currentActors, npcId, StringComparison.Ordinal) ||
            HasInvalidPermanentAliasCandidate(preTurnActors, npcId, StringComparison.Ordinal))
        {
            issues.Add(Error(
                $"{context}.NPCId",
                "npc_core_changes_ambiguous_target",
                "NPCCoreChanges cannot target an actor with conflicting or invalid permanent identity aliases.",
                npcId));
            return null;
        }

        var exactPreTurn = preTurnActors
            .Where(actor => string.Equals(actor.NpcId, npcId, StringComparison.Ordinal))
            .ToList();
        if (exactPreTurn.Count == 0)
        {
            var hasCaseVariant = preTurnActors.Any(actor =>
                    string.Equals(actor.NpcId, npcId, StringComparison.OrdinalIgnoreCase)) ||
                HasInvalidPermanentAliasCandidate(preTurnActors, npcId, StringComparison.OrdinalIgnoreCase);
            issues.Add(Error(
                $"{context}.NPCId",
                hasCaseVariant ? "npc_core_changes_target_not_exact" : "npc_core_changes_target_not_existing",
                hasCaseVariant
                    ? "NPCCoreChanges NPCId must match validated pre-turn permanent identity exactly, including case."
                    : "NPCCoreChanges may target only an actor present in validated pre-turn permanent NPC state.",
                npcId));
            return null;
        }

        var exactCurrent = currentActors
            .Where(actor => string.Equals(actor.NpcId, npcId, StringComparison.Ordinal))
            .ToList();
        if (exactCurrent.Count == 0)
        {
            var hasCaseVariant = currentActors.Any(actor =>
                    string.Equals(actor.NpcId, npcId, StringComparison.OrdinalIgnoreCase)) ||
                HasInvalidPermanentAliasCandidate(currentActors, npcId, StringComparison.OrdinalIgnoreCase);
            issues.Add(Error(
                $"{context}.NPCId",
                hasCaseVariant ? "npc_core_changes_target_not_exact" : "npc_core_changes_target_not_existing",
                "NPCCoreChanges target must exist under the exact permanent identity in current canonical carriers.",
                npcId));
            return exactPreTurn[0];
        }

        if (HasAmbiguousCarrierCopies(exactPreTurn) || HasAmbiguousCarrierCopies(exactCurrent))
        {
            issues.Add(Error(
                $"{context}.NPCId",
                "npc_core_changes_ambiguous_target",
                "NPCCoreChanges cannot target duplicate actor copies within one canonical carrier.",
                npcId));
        }

        var caseVariantCopies = currentActors.Concat(preTurnActors).Any(actor =>
            !string.Equals(actor.NpcId, npcId, StringComparison.Ordinal) &&
            string.Equals(actor.NpcId, npcId, StringComparison.OrdinalIgnoreCase));
        if (caseVariantCopies)
        {
            issues.Add(Error(
                $"{context}.NPCId",
                "npc_core_changes_target_not_exact",
                "Case-variant canonical identities make an NPCCoreChanges target ambiguous.",
                npcId));
        }

        if (ActorsDiverge(exactPreTurn) || ActorsDiverge(exactCurrent))
        {
            issues.Add(Error(
                $"{context}.NPCId",
                "npc_core_changes_divergent_mirrors",
                "NPCCoreChanges requires all canonical copies of the target actor to be semantically identical before reduction.",
                npcId));
        }

        return exactPreTurn[0];
    }

    private static void ValidateDirectCoreMutationBypass(
        IReadOnlyList<ActorReference> currentActors,
        IReadOnlyList<ActorReference> preTurnActors,
        MortalActorAcceptedTurnAuthority? acceptedTurnAuthority,
        List<ValidationIssue> issues)
    {
        foreach (var preTurnGroup in preTurnActors
                     .Where(actor => actor.NpcId != null)
                     .GroupBy(actor => actor.NpcId!, StringComparer.Ordinal))
        {
            var candidates = preTurnGroup.ToList();
            foreach (var current in currentActors.Where(actor =>
                         string.Equals(actor.NpcId, preTurnGroup.Key, StringComparison.Ordinal)))
            {
                var baseline = ResolveDirectMutationBaseline(current, candidates);
                if (baseline == null ||
                    !HasDirectActorMutation(
                        baseline.Actor,
                        current.Actor,
                        current.NpcId!,
                        acceptedTurnAuthority))
                    continue;

                issues.Add(Error(
                    current.Path,
                    "npc_existing_core_direct_mutation_forbidden",
                    "Existing NPC actor-owned fields must change through their exact dedicated command, not by rewriting a canonical full carrier.",
                    current.NpcId!));
            }
        }
    }

    private static ActorReference? ResolveDirectMutationBaseline(
        ActorReference current,
        IReadOnlyList<ActorReference> preTurnCandidates)
    {
        var sameSection = preTurnCandidates
            .Where(candidate => string.Equals(candidate.Section, current.Section, StringComparison.Ordinal))
            .ToList();
        if (sameSection.Count == 1)
            return sameSection[0];
        if (sameSection.Count > 1 ||
            HasAmbiguousCarrierCopies(preTurnCandidates) ||
            ActorsDiverge(preTurnCandidates))
        {
            return null;
        }

        return preTurnCandidates.Count > 0 ? preTurnCandidates[0] : null;
    }

    private static bool HasDirectActorMutation(
        JsonObject baseline,
        JsonObject current,
        string actorId,
        MortalActorAcceptedTurnAuthority? acceptedTurnAuthority)
    {
        var promotionFields =
            MortalActorLegacyPromotionAuthority.ResolveAuthorizedFields(baseline, current);
        if (acceptedTurnAuthority?.AuthorizesDedicatedTrainingPatch(
                actorId,
                baseline,
                current) == true)
        {
            return false;
        }

        foreach (var field in baseline.Select(property => property.Key)
                     .Concat(current.Select(property => property.Key))
                     .Distinct(StringComparer.Ordinal))
        {
            if (ActorFieldsWithDedicatedContinuityValidation.Contains(field))
                continue;

            if (JsonNode.DeepEquals(baseline[field], current[field]))
                continue;
            if (promotionFields.Contains(field))
                continue;
            if (acceptedTurnAuthority?.AuthorizesFieldMutation(
                    actorId,
                    baseline,
                    current,
                    field) == true)
                continue;

            return true;
        }

        return false;
    }

    private static void ValidateProfile(
        JsonNode? node,
        string context,
        string? npcId,
        List<ValidationIssue> issues)
    {
        if (node is not JsonObject profile || profile.Count == 0)
        {
            issues.Add(Error(context, "npc_core_changes_profile_invalid", "profile must be a non-empty object.", npcId));
            return;
        }

        ValidateUnknownProperties(profile, ProfileKeys, context, issues, npcId);
        foreach (var property in profile)
        {
            if (!TryReadNonEmptyString(property.Value, out _))
            {
                issues.Add(Error(
                    $"{context}.{property.Key}",
                    "npc_core_changes_profile_invalid",
                    "Profile replacement values must be non-empty strings.",
                    npcId));
            }
        }
    }

    private static void ValidateLocation(
        JsonNode? node,
        JsonObject? baselineActor,
        Authority authority,
        string context,
        string? npcId,
        List<ValidationIssue> issues)
    {
        if (node is not JsonObject location || location.Count == 0)
        {
            issues.Add(Error(context, "npc_core_changes_location_invalid", "location must be a non-empty object.", npcId));
            return;
        }

        ValidateUnknownProperties(location, LocationKeys, context, issues, npcId);
        if (!location.ContainsKey("currentLocationId") || !location.ContainsKey("initialLocationId"))
        {
            issues.Add(Error(
                context,
                "npc_core_changes_location_invalid",
                "location must carry both currentLocationId and initialLocationId.",
                npcId));
            return;
        }

        var currentIsNull = location["currentLocationId"] == null;
        var initialIsNull = location["initialLocationId"] == null;
        var currentId = ReadNullableString(location["currentLocationId"], out var currentTypeValid);
        var initialId = ReadNullableString(location["initialLocationId"], out var initialTypeValid);
        if (!currentTypeValid || !initialTypeValid || currentIsNull == initialIsNull)
        {
            issues.Add(Error(
                context,
                "npc_core_changes_location_invalid",
                "location must select exactly one permanent currentLocationId or same-turn initialLocationId.",
                npcId));
            return;
        }

        if (currentId != null)
        {
            var baselineLocation = ReadString(baselineActor?["currentLocationId"]);
            if (!authority.KnownPermanentLocationIds.Contains(currentId) &&
                !string.Equals(currentId, baselineLocation, StringComparison.Ordinal))
            {
                issues.Add(Error(
                    $"{context}.currentLocationId",
                    "npc_core_changes_location_invalid",
                    "currentLocationId must resolve to exact current-world permanent location authority.",
                    npcId));
            }
        }
        else if (initialId == null || !authority.SameTurnLocationInitialIds.Contains(initialId))
        {
            issues.Add(Error(
                $"{context}.initialLocationId",
                "npc_core_changes_location_invalid",
                "initialLocationId must resolve to an exact same-turn location initialId.",
                npcId));
        }
    }

    private static void ValidateProgression(
        JsonNode? node,
        JsonObject? baselineActor,
        string context,
        string? npcId,
        List<ValidationIssue> issues)
    {
        if (node is not JsonObject progression || progression.Count == 0)
        {
            issues.Add(Error(context, "npc_core_changes_progression_invalid", "progression must be a non-empty object.", npcId));
            return;
        }

        ValidateUnknownProperties(progression, ProgressionKeys, context, issues, npcId);
        var tupleKeys = new[] { "level", "experience", "experienceForNextLevel" };
        if (tupleKeys.Any(progression.ContainsKey))
        {
            foreach (var key in tupleKeys)
            {
                if (!TryReadNonNegativeInteger(progression[key], out _))
                {
                    issues.Add(Error(
                        $"{context}.{key}",
                        "npc_core_changes_progression_invalid",
                        "A progression tuple requires non-negative integer level, experience, and experienceForNextLevel values.",
                        npcId));
                }
            }
        }

        string? requestedProgressionType = null;
        if (progression.ContainsKey("progressionType") &&
            !TryReadNonEmptyString(progression["progressionType"], out requestedProgressionType))
        {
            issues.Add(Error(
                $"{context}.progressionType",
                "npc_core_changes_progression_invalid",
                "progressionType must be a non-empty absolute string value.",
                npcId));
        }

        if (progression.ContainsKey("lastPlayerXPValueOnSync") &&
            !TryReadNonNegativeInteger(progression["lastPlayerXPValueOnSync"], out _))
        {
            issues.Add(Error(
                $"{context}.lastPlayerXPValueOnSync",
                "npc_core_changes_progression_invalid",
                "lastPlayerXPValueOnSync must be a non-negative integer.",
                npcId));
        }

        var baselineType = ReadString(baselineActor?["progressionType"]);
        if (requestedProgressionType != null &&
            !string.Equals(requestedProgressionType, baselineType, StringComparison.Ordinal) &&
            !progression.ContainsKey("lastPlayerXPValueOnSync"))
        {
            issues.Add(Error(
                $"{context}.lastPlayerXPValueOnSync",
                "npc_core_changes_progression_invalid",
                "A progressionType transition requires lastPlayerXPValueOnSync authority.",
                npcId));
        }
    }

    private static void ValidateCharacteristicValues(
        JsonNode? node,
        JsonObject? baselineActor,
        Authority authority,
        string context,
        string? npcId,
        List<ValidationIssue> issues)
    {
        if (node is not JsonObject characteristicValues || characteristicValues.Count == 0)
        {
            issues.Add(Error(
                context,
                "npc_core_changes_characteristic_value_invalid",
                "characteristicValues must be a non-empty object.",
                npcId));
            return;
        }

        var actorCharacteristics = baselineActor?["characteristics"] as JsonObject;
        foreach (var property in characteristicValues)
        {
            if (string.IsNullOrWhiteSpace(property.Key) || !TryReadFiniteNumber(property.Value, out _))
            {
                issues.Add(Error(
                    $"{context}.{property.Key}",
                    "npc_core_changes_characteristic_value_invalid",
                    "Characteristic values must be finite JSON numbers.",
                    npcId));
                continue;
            }

            if (actorCharacteristics?.ContainsKey(property.Key) != true &&
                !authority.WorldCharacteristicKeys.Contains(property.Key))
            {
                issues.Add(Error(
                    $"{context}.{property.Key}",
                    "npc_core_changes_characteristic_not_authorized",
                    "Characteristic keys must already belong to the actor or explicit current-world authority.",
                    npcId));
            }
        }
    }

    private static void ValidateFactionAffiliations(
        JsonNode? node,
        Authority authority,
        string context,
        string? npcId,
        List<ValidationIssue> issues)
    {
        if (node is not JsonArray affiliations || affiliations.Count == 0)
        {
            issues.Add(Error(context, "npc_core_changes_faction_invalid", "Faction upserts must be a non-empty array.", npcId));
            return;
        }

        var seenFactionIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < affiliations.Count; index++)
        {
            var itemContext = $"{context}[{index}]";
            if (affiliations[index] is not JsonObject affiliation)
            {
                issues.Add(Error(itemContext, "npc_core_changes_faction_invalid", "Faction affiliation must be an object.", npcId));
                continue;
            }

            ValidateUnknownProperties(affiliation, FactionAffiliationKeys, itemContext, issues, npcId);
            var factionId = ReadRequiredString(affiliation, "factionId");
            var factionName = ReadRequiredString(affiliation, "factionName");
            var rank = ReadRequiredString(affiliation, "rank");
            var membershipStatus = ReadRequiredString(affiliation, "membershipStatus");
            var branchValid = affiliation.ContainsKey("branch") &&
                              (affiliation["branch"] == null || TryReadNonEmptyString(affiliation["branch"], out _));
            if (factionId == null || factionName == null || rank == null || membershipStatus == null || !branchValid ||
                !AllowedMembershipStatuses.Contains(membershipStatus) || !seenFactionIds.Add(factionId) ||
                !authority.FactionNamesById.TryGetValue(factionId, out var canonicalFactionName) ||
                !string.Equals(factionName, canonicalFactionName, StringComparison.Ordinal))
            {
                issues.Add(Error(
                    itemContext,
                    "npc_core_changes_faction_invalid",
                    "Faction upserts require exact current-world faction identity and the complete affiliation shape.",
                    npcId));
            }
        }
    }

    private static void ValidateFateCardsToAdd(
        JsonNode? node,
        JsonObject? baselineActor,
        string context,
        string? npcId,
        Func<JsonArray, string, IReadOnlyList<ValidationIssue>> validateProductionFateCards,
        List<ValidationIssue> issues)
    {
        if (node is not JsonArray cards || cards.Count == 0)
        {
            issues.Add(Error(context, "npc_core_changes_fate_card_invalid", "fateCardsToAdd must be a non-empty array.", npcId));
            return;
        }

        issues.AddRange(validateProductionFateCards(cards, context));

        var existingCardIds = ReadFateCards(baselineActor)
            .Select(card => ReadRequiredString(card, "cardId"))
            .Where(id => id != null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        var addedIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < cards.Count; index++)
        {
            var cardContext = $"{context}[{index}]";
            if (cards[index] is not JsonObject card)
            {
                issues.Add(Error(cardContext, "npc_core_changes_fate_card_invalid", "Fate Card additions must be objects.", npcId));
                continue;
            }

            ValidateFateCardShape(card, cardContext, npcId, issues);
            var cardId = ReadRequiredString(card, "cardId");
            var startsLocked = card["isUnlocked"] is JsonValue unlockedValue &&
                               unlockedValue.TryGetValue<bool>(out var unlocked) &&
                               !unlocked;
            if (cardId == null || !addedIds.Add(cardId) || existingCardIds.Contains(cardId) || !startsLocked)
            {
                issues.Add(Error(
                    cardContext,
                    "npc_core_changes_fate_card_invalid",
                    "Added Fate Cards require a new unique cardId and isUnlocked=false.",
                    npcId));
            }
        }
    }

    private static void ValidateFateCardRemovals(
        JsonNode? node,
        JsonObject? baselineActor,
        JsonArray? cardsToAdd,
        string context,
        string? npcId,
        List<ValidationIssue> issues)
    {
        if (node is not JsonArray removals || removals.Count == 0)
        {
            issues.Add(Error(context, "npc_core_changes_fate_card_invalid", "fateCardIdsToRemove must be a non-empty array.", npcId));
            return;
        }

        var existingCards = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var ambiguousExistingIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in ReadFateCards(baselineActor))
        {
            var cardId = ReadRequiredString(card, "cardId");
            if (cardId != null && !existingCards.TryAdd(cardId, card))
                ambiguousExistingIds.Add(cardId);
        }
        var addedIds = cardsToAdd?
            .OfType<JsonObject>()
            .Select(card => ReadRequiredString(card, "cardId"))
            .Where(id => id != null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < removals.Count; index++)
        {
            var removalId = ReadString(removals[index]);
            var valid = removalId != null &&
                        seen.Add(removalId) &&
                        !addedIds.Contains(removalId) &&
                        !ambiguousExistingIds.Contains(removalId) &&
                        existingCards.TryGetValue(removalId, out var card) &&
                        card["isUnlocked"] is JsonValue unlockedValue &&
                        unlockedValue.TryGetValue<bool>(out var unlocked) &&
                        !unlocked;
            if (!valid)
            {
                issues.Add(Error(
                    $"{context}[{index}]",
                    "npc_core_changes_fate_card_invalid",
                    "Fate Card removal may target only a unique validated pre-turn locked cardId.",
                    npcId));
            }
        }
    }

    private static void ValidateFateCardShape(
        JsonObject card,
        string context,
        string? npcId,
        List<ValidationIssue> issues)
    {
        ValidateUnknownProperties(card, FateCardKeys, context, issues, npcId);
        foreach (var field in new[] { "cardId", "name", "image_prompt", "description" })
        {
            if (ReadRequiredString(card, field) == null)
                issues.Add(Error($"{context}.{field}", "npc_core_changes_fate_card_invalid", $"Fate Card {field} is required.", npcId));
        }

        var imagePrompt = ReadRequiredString(card, "image_prompt");
        if (imagePrompt != null && !LooksLikeEnglishImagePrompt(imagePrompt))
        {
            issues.Add(Error(
                $"{context}.image_prompt",
                "npc_core_changes_fate_card_invalid",
                "Fate Card image_prompt must be English-only and no longer than 150 characters.",
                npcId));
        }

        if (card["isUnlocked"] is not JsonValue unlockedValue || !unlockedValue.TryGetValue<bool>(out _))
            issues.Add(Error($"{context}.isUnlocked", "npc_core_changes_fate_card_invalid", "Fate Card isUnlocked must be boolean.", npcId));

        if (card.TryGetPropertyValue("unlockConditions", out var unlockNode) && unlockNode != null)
        {
            if (unlockNode is not JsonObject unlockConditions)
            {
                issues.Add(Error($"{context}.unlockConditions", "npc_core_changes_fate_card_invalid", "unlockConditions must be an object or null.", npcId));
            }
            else
            {
                ValidateUnknownProperties(unlockConditions, FateCardUnlockConditionKeys, $"{context}.unlockConditions", issues, npcId);
                if (unlockConditions.ContainsKey("requiredRelationshipLevel") &&
                    unlockConditions["requiredRelationshipLevel"] != null &&
                    !TryReadInteger(unlockConditions["requiredRelationshipLevel"], out _))
                {
                    issues.Add(Error(
                        $"{context}.unlockConditions.requiredRelationshipLevel",
                        "npc_core_changes_fate_card_invalid",
                        "requiredRelationshipLevel must be an integer or null.",
                        npcId));
                }

                if (unlockConditions.ContainsKey("plotConditionDescription") &&
                    (unlockConditions["plotConditionDescription"] is not JsonValue plotDescriptionValue ||
                     !plotDescriptionValue.TryGetValue<string>(out _)))
                {
                    issues.Add(Error(
                        $"{context}.unlockConditions.plotConditionDescription",
                        "npc_core_changes_fate_card_invalid",
                        "plotConditionDescription must be a string when supplied.",
                        npcId));
                }

                if (unlockConditions.ContainsKey("conjunction") &&
                    (!TryReadNonEmptyString(unlockConditions["conjunction"], out var conjunction) ||
                     (!string.Equals(conjunction, "AND", StringComparison.OrdinalIgnoreCase) &&
                      !string.Equals(conjunction, "OR", StringComparison.OrdinalIgnoreCase))))
                {
                    issues.Add(Error(
                        $"{context}.unlockConditions.conjunction",
                        "npc_core_changes_fate_card_invalid",
                        "unlockConditions.conjunction must be AND or OR.",
                        npcId));
                }
            }
        }

        if (card["rewards"] is not JsonObject rewards)
        {
            issues.Add(Error($"{context}.rewards", "npc_core_changes_fate_card_invalid", "Fate Card rewards object is required.", npcId));
            return;
        }

        ValidateUnknownProperties(rewards, FateCardRewardKeys, $"{context}.rewards", issues, npcId);
        if (ReadRequiredString(rewards, "description") == null)
            issues.Add(Error($"{context}.rewards.description", "npc_core_changes_fate_card_invalid", "Fate Card rewards.description is required.", npcId));

        ValidateClosedObjectArray(rewards["newActiveSkills"], ActiveSkillKeys, $"{context}.rewards.newActiveSkills", npcId, issues, ValidateActiveFateCardSkill);
        ValidateClosedObjectArray(rewards["newPassiveSkills"], PassiveSkillKeys, $"{context}.rewards.newPassiveSkills", npcId, issues, ValidatePassiveFateCardSkill);
        ValidateStringArray(rewards["statBoosts"], $"{context}.rewards.statBoosts", npcId, issues);
        ValidateStringArray(rewards["newServices"], $"{context}.rewards.newServices", npcId, issues);
        if (rewards.ContainsKey("otherNarrativeRewards") &&
            (rewards["otherNarrativeRewards"] is not JsonValue narrativeValue ||
             !narrativeValue.TryGetValue<string>(out _)))
        {
            issues.Add(Error(
                $"{context}.rewards.otherNarrativeRewards",
                "npc_core_changes_fate_card_invalid",
                "otherNarrativeRewards must be a string when supplied.",
                npcId));
        }

        ValidateClosedObjectArray(
            rewards["tacticalTriggers"],
            TacticalTriggerKeys,
            $"{context}.rewards.tacticalTriggers",
            npcId,
            issues,
            ValidateTacticalTrigger);
    }

    private static void ValidateActiveFateCardSkill(
        JsonObject skill,
        string context,
        string? npcId,
        List<ValidationIssue> issues)
    {
        foreach (var field in new[] { "skillName", "skillDescription", "rarity" })
        {
            if (ReadRequiredString(skill, field) == null)
                issues.Add(Error($"{context}.{field}", "npc_core_changes_fate_card_invalid", $"Active skill {field} is required.", npcId));
        }

        if (skill.ContainsKey("actionCost") &&
            (!TryReadNonEmptyString(skill["actionCost"], out var actionCost) ||
             !AllowedCombatActionCosts.Contains(actionCost!)))
        {
            issues.Add(Error(
                $"{context}.actionCost",
                "npc_core_changes_fate_card_invalid",
                "Active skill actionCost must be Main, Fast, or Free when supplied.",
                npcId));
        }

        if (skill["combatEffect"] is not JsonObject combatEffect)
        {
            issues.Add(Error(
                $"{context}.combatEffect",
                "npc_core_changes_fate_card_invalid",
                "Active skill combatEffect object is required.",
                npcId));
            return;
        }

        ValidateSkillNestedObjects(skill, context, npcId, issues);
        if (combatEffect["isActivatedEffect"] is not JsonValue activationValue ||
            !activationValue.TryGetValue<bool>(out var isActivated) ||
            !isActivated)
        {
            issues.Add(Error(
                $"{context}.combatEffect.isActivatedEffect",
                "npc_core_changes_fate_card_invalid",
                "Active skill combatEffect.isActivatedEffect must be true.",
                npcId));
        }

        if (ReadRequiredString(combatEffect, "actionName") == null ||
            combatEffect["effects"] is not JsonArray effects ||
            effects.Count == 0)
        {
            issues.Add(Error(
                $"{context}.combatEffect",
                "npc_core_changes_fate_card_invalid",
                "Active skill combatEffect requires actionName and a non-empty effects array.",
                npcId));
        }
    }

    private static void ValidatePassiveFateCardSkill(
        JsonObject skill,
        string context,
        string? npcId,
        List<ValidationIssue> issues)
    {
        foreach (var field in new[] { "skillName", "skillDescription", "rarity", "type", "group" })
        {
            if (ReadRequiredString(skill, field) == null)
                issues.Add(Error($"{context}.{field}", "npc_core_changes_fate_card_invalid", $"Passive skill {field} is required.", npcId));
        }

        var passiveType = ReadRequiredString(skill, "type");
        if (passiveType != null && !AllowedPassiveSkillTypes.Contains(passiveType))
        {
            issues.Add(Error(
                $"{context}.type",
                "npc_core_changes_fate_card_invalid",
                "Passive skill type is not canonical.",
                npcId));
        }

        foreach (var field in new[] { "masteryLevel", "maxMasteryLevel" })
        {
            if (!TryReadInteger(skill[field], out _))
                issues.Add(Error($"{context}.{field}", "npc_core_changes_fate_card_invalid", $"Passive skill {field} integer is required.", npcId));
        }

        if (!skill.ContainsKey("structuredBonuses") ||
            skill["structuredBonuses"] is not null and not JsonArray)
        {
            issues.Add(Error(
                $"{context}.structuredBonuses",
                "npc_core_changes_fate_card_invalid",
                "Passive skill structuredBonuses must be an array or explicit null.",
                npcId));
        }

        ValidateSkillNestedObjects(skill, context, npcId, issues);
    }

    private static void ValidateTacticalTrigger(
        JsonObject trigger,
        string context,
        string? npcId,
        List<ValidationIssue> issues)
    {
        foreach (var field in new[] { "triggerCondition", "newTargetPriority", "description" })
        {
            if (ReadRequiredString(trigger, field) == null)
                issues.Add(Error($"{context}.{field}", "npc_core_changes_fate_card_invalid", $"Tactical trigger {field} is required.", npcId));
        }

        ValidateStringArray(trigger["newActionPreference"], $"{context}.newActionPreference", npcId, issues);
    }

    private static bool LooksLikeEnglishImagePrompt(string value) =>
        value.Length <= 150 &&
        !value.Any(character => character is >= '\u0400' and <= '\u052f');

    private static void ValidateSkillNestedObjects(JsonObject skill, string context, string? npcId, List<ValidationIssue> issues)
    {
        if (skill["combatEffect"] is JsonObject combatEffect)
        {
            ValidateUnknownProperties(combatEffect, CombatEffectKeys, $"{context}.combatEffect", issues, npcId);
            ValidateClosedObjectArray(
                combatEffect["effects"],
                CombatEffectItemKeys,
                $"{context}.combatEffect.effects",
                npcId,
                issues,
                ValidateCombatEffect);
        }

        ValidateClosedObjectArray(skill["structuredBonuses"], StructuredBonusKeys, $"{context}.structuredBonuses", npcId, issues);
    }

    private static void ValidateCombatEffect(
        JsonObject effect,
        string context,
        string? npcId,
        List<ValidationIssue> issues)
    {
        foreach (var field in new[] { "effectType", "targetType", "effectDescription" })
        {
            if (ReadRequiredString(effect, field) == null)
            {
                issues.Add(Error(
                    $"{context}.{field}",
                    "npc_core_changes_fate_card_invalid",
                    $"Combat effect {field} is required.",
                    npcId));
            }
        }
    }

    private static void ApplyCommand(JsonObject actor, JsonObject command)
    {
        if (command["profile"] is JsonObject profile)
        {
            foreach (var property in profile)
                actor[property.Key] = property.Value?.DeepClone();
        }

        if (command["location"] is JsonObject location)
        {
            actor["currentLocationId"] = location["currentLocationId"]?.DeepClone();
            actor["initialLocationId"] = location["initialLocationId"]?.DeepClone();
        }

        if (command["progression"] is JsonObject progression)
        {
            foreach (var field in new[] { "level", "experience", "experienceForNextLevel", "progressionType" })
            {
                if (progression.ContainsKey(field))
                    actor[field] = progression[field]?.DeepClone();
            }

            if (progression.ContainsKey("lastPlayerXPValueOnSync"))
            {
                var trackers = actor["progressionTrackers"] as JsonObject ?? new JsonObject();
                actor["progressionTrackers"] = trackers;
                trackers["lastPlayerXPValueOnSync"] = progression["lastPlayerXPValueOnSync"]?.DeepClone();
            }
        }

        if (command["characteristicValues"] is JsonObject characteristicValues)
        {
            var characteristics = actor["characteristics"] as JsonObject ?? new JsonObject();
            actor["characteristics"] = characteristics;
            foreach (var property in characteristicValues)
                characteristics[property.Key] = property.Value?.DeepClone();
        }

        if (command["factionAffiliationsToUpsert"] is JsonArray affiliationsToUpsert)
        {
            var affiliations = actor["factionAffiliations"] as JsonArray ?? new JsonArray();
            actor["factionAffiliations"] = affiliations;
            foreach (var upsert in affiliationsToUpsert.OfType<JsonObject>())
            {
                var factionId = ReadRequiredString(upsert, "factionId")!;
                var existingIndex = -1;
                for (var index = 0; index < affiliations.Count; index++)
                {
                    if (affiliations[index] is JsonObject existing &&
                        string.Equals(ReadRequiredString(existing, "factionId"), factionId, StringComparison.Ordinal))
                    {
                        existingIndex = index;
                        break;
                    }
                }

                if (existingIndex >= 0)
                    affiliations[existingIndex] = upsert.DeepClone();
                else
                    affiliations.Add(upsert.DeepClone());
            }
        }

        if (command["fateCardIdsToRemove"] is JsonArray removals && actor["fateCards"] is JsonArray existingCards)
        {
            var removalIds = removals
                .Select(ReadString)
                .Where(id => id != null)
                .Cast<string>()
                .ToHashSet(StringComparer.Ordinal);
            for (var index = existingCards.Count - 1; index >= 0; index--)
            {
                if (existingCards[index] is JsonObject card &&
                    removalIds.Contains(ReadRequiredString(card, "cardId") ?? string.Empty))
                {
                    existingCards.RemoveAt(index);
                }
            }
        }

        if (command["fateCardsToAdd"] is JsonArray cardsToAdd)
        {
            var fateCards = actor["fateCards"] as JsonArray ?? new JsonArray();
            actor["fateCards"] = fateCards;
            foreach (var card in cardsToAdd)
                fateCards.Add(card?.DeepClone());
        }
    }

    private static IReadOnlyList<ActorReference> CollectActors(JsonObject root)
    {
        var result = new List<ActorReference>();
        foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (root[section] is not JsonArray actors)
                continue;

            for (var index = 0; index < actors.Count; index++)
            {
                if (actors[index] is not JsonObject actor)
                    continue;

                var hasPermanentId = GuardianPolicyContracts.TryResolveStrictPermanentNpcId(actor, out var npcId);
                result.Add(new ActorReference(section, index, hasPermanentId ? npcId : null, actor));
            }
        }

        return result;
    }

    private static bool HasInvalidPermanentAliasCandidate(
        IEnumerable<ActorReference> actors,
        string npcId,
        StringComparison comparison) =>
        actors.Any(actor => actor.NpcId == null && actor.Actor.Any(property =>
            IsExactPermanentAlias(property.Key) &&
            TryReadNonEmptyString(property.Value, out var aliasValue) &&
            string.Equals(aliasValue, npcId, comparison)));

    private static bool IsExactPermanentAlias(string propertyName) =>
        string.Equals(propertyName, "NPCId", StringComparison.Ordinal) ||
        string.Equals(propertyName, "npcId", StringComparison.Ordinal) ||
        string.Equals(propertyName, "id", StringComparison.Ordinal);

    private static bool HasAmbiguousCarrierCopies(IEnumerable<ActorReference> actors) =>
        actors.GroupBy(actor => actor.Section, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1);

    private static bool ActorsDiverge(IReadOnlyList<ActorReference> actors)
    {
        if (actors.Count <= 1)
            return false;

        var first = actors[0].Actor;
        return actors.Skip(1).Any(actor => !JsonNode.DeepEquals(first, actor.Actor));
    }

    private static IReadOnlyList<JsonObject> ReadFateCards(JsonObject? actor) =>
        actor?["fateCards"] is JsonArray cards
            ? cards.OfType<JsonObject>().ToList()
            : [];

    private static void ValidateUnknownProperties(
        JsonObject value,
        HashSet<string> allowed,
        string context,
        List<ValidationIssue> issues,
        string? npcId = null)
    {
        foreach (var property in value)
        {
            if (allowed.Contains(property.Key))
                continue;

            issues.Add(Error(
                $"{context}.{property.Key}",
                "npc_core_changes_unknown_member",
                $"Unknown or protected NPCCoreChanges member: {property.Key}.",
                npcId));
        }
    }

    private static void ValidateClosedObjectArray(
        JsonNode? node,
        HashSet<string> allowed,
        string context,
        string? npcId,
        List<ValidationIssue> issues,
        Action<JsonObject, string, string?, List<ValidationIssue>>? nestedValidator = null)
    {
        if (node == null)
            return;
        if (node is not JsonArray array)
        {
            issues.Add(Error(context, "npc_core_changes_fate_card_invalid", "Expected an array.", npcId));
            return;
        }

        for (var index = 0; index < array.Count; index++)
        {
            var itemContext = $"{context}[{index}]";
            if (array[index] is not JsonObject item)
            {
                issues.Add(Error(itemContext, "npc_core_changes_fate_card_invalid", "Expected an object.", npcId));
                continue;
            }

            ValidateUnknownProperties(item, allowed, itemContext, issues, npcId);
            nestedValidator?.Invoke(item, itemContext, npcId, issues);
        }
    }

    private static void ValidateStringArray(
        JsonNode? node,
        string context,
        string? npcId,
        List<ValidationIssue> issues)
    {
        if (node == null)
            return;
        if (node is not JsonArray array || array.Any(item => ReadString(item) == null))
            issues.Add(Error(context, "npc_core_changes_fate_card_invalid", "Expected an array of non-empty strings.", npcId));
    }

    private static bool IsNonEmptyContainer(JsonNode? node) => node switch
    {
        JsonObject obj => obj.Count > 0,
        JsonArray array => array.Count > 0,
        _ => false
    };

    private static string? ReadRequiredString(JsonObject value, string propertyName) =>
        TryReadNonEmptyString(value[propertyName], out var result) ? result : null;

    private static string? ReadString(JsonNode? node) =>
        TryReadNonEmptyString(node, out var result) ? result : null;

    private static bool TryReadNonEmptyString(JsonNode? node, out string? value)
    {
        value = null;
        if (node is not JsonValue jsonValue ||
            !jsonValue.TryGetValue<string>(out var candidate) ||
            string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        value = candidate;
        return true;
    }

    private static string? ReadNullableString(JsonNode? node, out bool valid)
    {
        if (node == null)
        {
            valid = true;
            return null;
        }

        valid = TryReadNonEmptyString(node, out var value);
        return value;
    }

    private static bool TryReadNonNegativeInteger(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue jsonValue &&
               jsonValue.TryGetValue<int>(out value) &&
               value >= 0;
    }

    private static bool TryReadInteger(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue jsonValue && jsonValue.TryGetValue<int>(out value);
    }

    private static bool TryReadFiniteNumber(JsonNode? node, out double value)
    {
        value = 0;
        return node is JsonValue jsonValue &&
               jsonValue.TryGetValue<double>(out value) &&
               double.IsFinite(value);
    }

    private static ValidationIssue Error(string path, string code, string message, string? npcId = null) =>
        new(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            actor: npcId == null ? null : $"mortal_npc:{npcId}",
            section: "NPCCoreChanges",
            repairHint: "Use one exact permanent NPCId and only the bounded NPCCoreChanges mutation groups; preserve every unrelated actor field.");

    private static async Task<JsonObject?> ReadObjectAsync(FileSystemManager fs, string path)
    {
        try
        {
            var json = await fs.ReadFileAsync(path);
            return string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static async Task ReadExactLocationAuthorityAsync(
        FileSystemManager fs,
        HashSet<string> permanentIds,
        HashSet<string> sameTurnIds)
    {
        var sameTurnCandidates = new List<string>();
        var worldMapJson = await fs.ReadFileAsync(MortalLocationMaterializationContract.WorldMapPath);
        if (!string.IsNullOrWhiteSpace(worldMapJson))
        {
            try
            {
                using var document = JsonDocument.Parse(worldMapJson);
                permanentIds.UnionWith(
                    ValidationService.ReadExactCanonicalWorldMapLocationIds(document.RootElement));
                CollectExactRawWorldMapLocationInitialIds(document.RootElement, sameTurnCandidates);
            }
            catch (JsonException)
            {
                // Ordinary state validation owns malformed world-map JSON.
            }
        }

        var currentJson = await fs.ReadFileAsync(MortalLocationMaterializationContract.CurrentLocationPath);
        if (!string.IsNullOrWhiteSpace(currentJson))
        {
            try
            {
                using var document = JsonDocument.Parse(currentJson);
                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("currentLocationData", out var candidate) &&
                    candidate.ValueKind == JsonValueKind.Object)
                {
                    CollectValidatedRawLocationInitialId(
                        candidate,
                        "currentLocationData",
                        "current_scene_creation",
                        sameTurnCandidates);
                }
            }
            catch (JsonException)
            {
                // Ordinary state validation owns malformed current-location JSON.
            }
        }

        foreach (var group in sameTurnCandidates.GroupBy(
                     MortalLocationIdentityState.BuildConfusableKey,
                     StringComparer.Ordinal))
        {
            if (group.Count() == 1)
                sameTurnIds.Add(group.Single());
        }
    }

    private static void CollectExactRawWorldMapLocationInitialIds(
        JsonElement root,
        ICollection<string> sameTurnCandidates)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("worldMapUpdates", out var updates) ||
            updates.ValueKind != JsonValueKind.Object ||
            !updates.TryGetProperty("newLocations", out var locations) ||
            locations.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var candidate in locations.EnumerateArray())
        {
            CollectValidatedRawLocationInitialId(
                candidate,
                $"worldMapUpdates.newLocations[{index}]",
                "world_map_creation",
                sameTurnCandidates);
            index++;
        }
    }

    private static void CollectValidatedRawLocationInitialId(
        JsonElement candidate,
        string context,
        string route,
        ICollection<string> sameTurnCandidates)
    {
        if (candidate.ValueKind != JsonValueKind.Object ||
            MortalLocationMaterializationContract.ValidateRawLocation(candidate, context, route)
                .Any(static issue => issue.Severity == IssueSeverity.Error) ||
            !candidate.TryGetProperty("initialId", out var initialId) ||
            initialId.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var value = initialId.GetString();
        if (!string.IsNullOrEmpty(value) && string.Equals(value, value.Trim(), StringComparison.Ordinal))
            sameTurnCandidates.Add(value);
    }

    private static void CollectFactionAuthority(JsonNode? node, Dictionary<string, string> factionNamesById)
    {
        switch (node)
        {
            case JsonObject obj:
                if (TryReadNonEmptyString(obj["factionId"], out var factionId) &&
                    (TryReadNonEmptyString(obj["name"], out var factionName) ||
                     TryReadNonEmptyString(obj["factionName"], out factionName)))
                {
                    factionNamesById.TryAdd(factionId!, factionName!);
                }

                foreach (var property in obj)
                    CollectFactionAuthority(property.Value, factionNamesById);
                break;
            case JsonArray array:
                foreach (var item in array)
                    CollectFactionAuthority(item, factionNamesById);
                break;
        }
    }
}
