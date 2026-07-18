using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private const string MortalActorMaterializationStatePath = "game_state/npcs/npc_core.json";
    private const string AfterlifeActorMaterializationStatePath = "game_state/meta/afterlife_entity_profiles.json";

    private readonly record struct ActorMaterializationPromotionSignals(
        bool CanTeach,
        bool CanTrade,
        bool CanFight,
        bool HasActorBrainScope)
    {
        public bool IsPromotionFrom(ActorMaterializationPromotionSignals previous) =>
            (CanTeach && !previous.CanTeach) ||
            (CanTrade && !previous.CanTrade) ||
            (CanFight && !previous.CanFight) ||
            (HasActorBrainScope && !previous.HasActorBrainScope);

        public ActorMaterializationPromotionSignals Merge(ActorMaterializationPromotionSignals other) =>
            new(
                CanTeach || other.CanTeach,
                CanTrade || other.CanTrade,
                CanFight || other.CanFight,
                HasActorBrainScope || other.HasActorBrainScope);
    }

    private readonly record struct AfterlifeActorMaterializationPromotionSignals(
        bool CanTeach,
        bool CanFight,
        bool HasActorBrainScope)
    {
        public bool IsPromotionFrom(AfterlifeActorMaterializationPromotionSignals previous) =>
            (CanTeach && !previous.CanTeach) ||
            (CanFight && !previous.CanFight) ||
            (HasActorBrainScope && !previous.HasActorBrainScope);

        public AfterlifeActorMaterializationPromotionSignals Merge(
            AfterlifeActorMaterializationPromotionSignals other) =>
            new(
                CanTeach || other.CanTeach,
                CanFight || other.CanFight,
                HasActorBrainScope || other.HasActorBrainScope);
    }

    private readonly record struct MortalActorMaterializationPreTurnState(
        ActorMaterializationPromotionSignals PromotionSignals,
        string? HistoricalEnvelopeJson)
    {
        public MortalActorMaterializationPreTurnState Merge(MortalActorMaterializationPreTurnState other) =>
            new(
                PromotionSignals.Merge(other.PromotionSignals),
                HistoricalEnvelopeJson ?? other.HistoricalEnvelopeJson);
    }

    private readonly record struct AfterlifeActorMaterializationPreTurnState(
        AfterlifeActorMaterializationPromotionSignals PromotionSignals,
        string? HistoricalEnvelopeJson)
    {
        public AfterlifeActorMaterializationPreTurnState Merge(AfterlifeActorMaterializationPreTurnState other) =>
            new(
                PromotionSignals.Merge(other.PromotionSignals),
                HistoricalEnvelopeJson ?? other.HistoricalEnvelopeJson);
    }

    private async Task ValidateAcceptedTurnActorMaterializationCompletenessAsync(List<ValidationIssue> issues)
    {
        await ValidateAcceptedTurnMortalActorMaterializationCompletenessAsync(issues);
        await ValidateAcceptedTurnAfterlifeActorMaterializationCompletenessAsync(issues);
    }

    private async Task ValidateAcceptedTurnMortalActorMaterializationCompletenessAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(MortalActorMaterializationStatePath);
        if (preTurnJson == null)
            return;

        var currentJson = await _fs.ReadFileAsync(MortalActorMaterializationStatePath);
        if (string.IsNullOrWhiteSpace(currentJson))
            return;

        try
        {
            using var preTurnDocument = JsonDocument.Parse(preTurnJson);
            using var currentDocument = JsonDocument.Parse(currentJson);
            if (currentDocument.RootElement.ValueKind != JsonValueKind.Object ||
                !TryReadCanonicalMortalActorStates(preTurnDocument.RootElement, out var preTurnActors))
            {
                return;
            }

            foreach (var sectionName in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
            {
                if (!currentDocument.RootElement.TryGetProperty(sectionName, out var actors) ||
                    actors.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var index = 0;
                foreach (var actor in actors.EnumerateArray())
                {
                    var context = $"{MortalActorMaterializationStatePath}.{sectionName}[{index++}]";
                    var actorId = ReadCanonicalMortalActorId(actor);
                    if (actorId == null)
                        continue;

                    var currentSignals = ReadMortalActorMaterializationPromotionSignals(actor);
                    if (preTurnActors.TryGetValue(actorId, out var previousState))
                    {
                        if (previousState.HistoricalEnvelopeJson != null)
                        {
                            ValidateHistoricalActorMaterializationEnvelope(
                                actor,
                                context,
                                "mortal_npc",
                                actorId,
                                previousState.HistoricalEnvelopeJson,
                                issues);
                            continue;
                        }

                        if (!actor.TryGetProperty(ActorMaterializationContract.PropertyName, out _) &&
                            !currentSignals.IsPromotionFrom(previousState.PromotionSignals))
                            continue;
                    }

                    issues.AddRange(ActorMaterializationContract.ValidateCanonicalMortalNpc(
                        actor,
                        context,
                        requireEnvelope: true));
                }
            }
        }
        catch (JsonException)
        {
            // Ordinary canonical-state validation reports malformed current or pre-turn JSON.
        }
    }

    private static bool TryReadCanonicalMortalActorStates(
        JsonElement root,
        out Dictionary<string, MortalActorMaterializationPreTurnState> result)
    {
        result = new Dictionary<string, MortalActorMaterializationPreTurnState>(StringComparer.OrdinalIgnoreCase);
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var sectionName in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (!root.TryGetProperty(sectionName, out var actors))
                continue;
            if (actors.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var actor in actors.EnumerateArray())
            {
                var actorId = ReadCanonicalMortalActorId(actor);
                if (actorId == null)
                    continue;

                var state = new MortalActorMaterializationPreTurnState(
                    ReadMortalActorMaterializationPromotionSignals(actor),
                    ReadHistoricalActorMaterializationEnvelopeJson(actor));
                result[actorId] = result.TryGetValue(actorId, out var existing)
                    ? existing.Merge(state)
                    : state;
            }
        }

        return true;
    }

    private static string? ReadCanonicalMortalActorId(JsonElement actor)
    {
        if (actor.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var propertyName in new[] { "NPCId", "npcId", "id" })
        {
            if (!actor.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
                continue;

            var actorId = value.GetString();
            if (!string.IsNullOrWhiteSpace(actorId))
                return actorId.Trim();
        }

        return null;
    }

    private static ActorMaterializationPromotionSignals ReadMortalActorMaterializationPromotionSignals(
        JsonElement actor)
    {
        var hasPlans = actor.TryGetProperty("plans", out var plans) &&
                       plans.ValueKind == JsonValueKind.String &&
                       !string.IsNullOrWhiteSpace(plans.GetString());
        var hasCurrentActivity = actor.TryGetProperty("currentActivity", out var currentActivity) &&
                                 currentActivity.ValueKind == JsonValueKind.Object;

        return new ActorMaterializationPromotionSignals(
            CanTeach: HasActorMaterializationNestedTrueBoolean(actor, "teacherProfile", "canTeach"),
            CanTrade: HasActorMaterializationNestedTrueBoolean(actor, "tradeState", "canTrade"),
            CanFight: HasActorMaterializationArrayEntries(actor, "activeSkills") ||
                      HasActorMaterializationArrayEntries(actor, "passiveSkills"),
            HasActorBrainScope: hasPlans ||
                                hasCurrentActivity ||
                                HasActorMaterializationArrayEntries(actor, "completedActivities"));
    }

    private static bool HasActorMaterializationNestedTrueBoolean(
        JsonElement value,
        string objectPropertyName,
        string booleanPropertyName) =>
        value.TryGetProperty(objectPropertyName, out var nested) &&
        nested.ValueKind == JsonValueKind.Object &&
        nested.TryGetProperty(booleanPropertyName, out var booleanValue) &&
        booleanValue.ValueKind == JsonValueKind.True;

    private static bool HasActorMaterializationArrayEntries(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var array) &&
        array.ValueKind == JsonValueKind.Array &&
        array.GetArrayLength() > 0;

    private async Task ValidateAcceptedTurnAfterlifeActorMaterializationCompletenessAsync(
        List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(AfterlifeActorMaterializationStatePath);
        if (preTurnJson == null)
            return;

        var currentJson = await _fs.ReadFileAsync(AfterlifeActorMaterializationStatePath);
        if (string.IsNullOrWhiteSpace(currentJson))
            return;

        try
        {
            using var preTurnDocument = JsonDocument.Parse(preTurnJson);
            using var currentDocument = JsonDocument.Parse(currentJson);
            if (currentDocument.RootElement.ValueKind != JsonValueKind.Object ||
                !TryReadCanonicalAfterlifeActorStates(preTurnDocument.RootElement, out var preTurnActors) ||
                !currentDocument.RootElement.TryGetProperty(
                    AfterlifeEntityProfileState.ProfilesProperty,
                    out var profiles) ||
                profiles.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var index = 0;
            foreach (var profile in profiles.EnumerateArray())
            {
                var context = $"{AfterlifeActorMaterializationStatePath}.{AfterlifeEntityProfileState.ProfilesProperty}[{index++}]";
                if (!TryReadCanonicalAfterlifeActorIdentity(
                        profile,
                        out var actorType,
                        out var actorId,
                        out var identityKey) ||
                    (string.Equals(actorType, "player_soul", StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(actorId, "player_soul", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var currentSignals = ReadAfterlifeActorMaterializationPromotionSignals(profile);
                if (preTurnActors.TryGetValue(identityKey, out var previousState))
                {
                    if (previousState.HistoricalEnvelopeJson != null)
                    {
                        ValidateHistoricalActorMaterializationEnvelope(
                            profile,
                            context,
                            actorType,
                            actorId,
                            previousState.HistoricalEnvelopeJson,
                            issues);
                        continue;
                    }

                    if (!profile.TryGetProperty(ActorMaterializationContract.PropertyName, out _) &&
                        !currentSignals.IsPromotionFrom(previousState.PromotionSignals))
                        continue;
                }

                issues.AddRange(ActorMaterializationContract.ValidateCanonicalAfterlifeProfile(
                    profile,
                    context,
                    requireEnvelope: true));
            }
        }
        catch (JsonException)
        {
            // Ordinary canonical-state validation reports malformed current or pre-turn JSON.
        }
    }

    private static bool TryReadCanonicalAfterlifeActorStates(
        JsonElement root,
        out Dictionary<string, AfterlifeActorMaterializationPreTurnState> result)
    {
        result = new Dictionary<string, AfterlifeActorMaterializationPreTurnState>(
            StringComparer.OrdinalIgnoreCase);
        if (root.ValueKind != JsonValueKind.Object)
            return false;
        if (!root.TryGetProperty(AfterlifeEntityProfileState.ProfilesProperty, out var profiles))
            return true;
        if (profiles.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var profile in profiles.EnumerateArray())
        {
            if (!TryReadCanonicalAfterlifeActorIdentity(
                    profile,
                    out _,
                    out _,
                    out var identityKey))
            {
                continue;
            }

            var state = new AfterlifeActorMaterializationPreTurnState(
                ReadAfterlifeActorMaterializationPromotionSignals(profile),
                ReadHistoricalActorMaterializationEnvelopeJson(profile));
            result[identityKey] = result.TryGetValue(identityKey, out var existing)
                ? existing.Merge(state)
                : state;
        }

        return true;
    }

    private static bool TryReadCanonicalAfterlifeActorIdentity(
        JsonElement profile,
        out string actorType,
        out string actorId,
        out string identityKey)
    {
        actorType = ReadActorMaterializationString(profile, "actorType") ?? string.Empty;
        actorId = ReadActorMaterializationString(profile, "actorId", "actorRef") ?? string.Empty;
        identityKey = string.Empty;
        if (actorType.Length == 0 || actorId.Length == 0)
            return false;

        identityKey = $"{actorType}\u001f{actorId}";
        return true;
    }

    private static string? ReadActorMaterializationString(JsonElement value, params string[] propertyNames)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var propertyName in propertyNames)
        {
            if (!value.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var text = property.GetString();
            if (!string.IsNullOrWhiteSpace(text))
                return text.Trim();
        }

        return null;
    }

    private static AfterlifeActorMaterializationPromotionSignals
        ReadAfterlifeActorMaterializationPromotionSignals(JsonElement profile)
    {
        var hasMentorAuthority = HasActorMaterializationTrueBoolean(profile, "canTeachPlayer") ||
                                 HasActorMaterializationNestedTrueBoolean(profile, "mentorProfile", "canTeach") ||
                                 HasActorMaterializationTeachableSpecialArt(profile);
        var hasCombatAuthority = HasActorMaterializationPositiveNumericObjectValue(profile, "standardArts") ||
                                 HasActorMaterializationUsableSpecialArt(profile);
        var hasActorBrainScope =
            (profile.TryGetProperty("goals", out var goals) && goals.ValueKind == JsonValueKind.Object) ||
            (profile.TryGetProperty("currentActivity", out var currentActivity) &&
             currentActivity.ValueKind == JsonValueKind.Object) ||
            HasActorMaterializationArrayEntries(profile, "personalQuests") ||
            HasActorMaterializationArrayEntries(profile, "completedActivities") ||
            HasActorMaterializationArrayEntries(profile, "ledger") ||
            HasActorMaterializationArrayEntries(profile, "progressionLedger");

        return new AfterlifeActorMaterializationPromotionSignals(
            CanTeach: hasMentorAuthority,
            CanFight: hasCombatAuthority,
            HasActorBrainScope: hasActorBrainScope);
    }

    private static bool HasActorMaterializationTrueBoolean(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;

    private static bool HasActorMaterializationPositiveNumericObjectValue(
        JsonElement value,
        string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
            return false;

        return property.EnumerateObject().Any(entry =>
            entry.Value.ValueKind == JsonValueKind.Number &&
            entry.Value.TryGetInt32(out var number) &&
            number > 0);
    }

    private static bool HasActorMaterializationUsableSpecialArt(JsonElement profile)
    {
        if (!profile.TryGetProperty("specialArts", out var specialArts) ||
            specialArts.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return specialArts.EnumerateArray().Any(specialArt =>
            specialArt.ValueKind == JsonValueKind.Object &&
            specialArt.TryGetProperty("tier", out var tier) &&
            tier.ValueKind == JsonValueKind.Number &&
            tier.TryGetInt32(out var tierValue) &&
            tierValue > 0);
    }

    private static bool HasActorMaterializationTeachableSpecialArt(JsonElement profile)
    {
        if (!profile.TryGetProperty("specialArts", out var specialArts) ||
            specialArts.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return specialArts.EnumerateArray().Any(specialArt =>
            specialArt.ValueKind == JsonValueKind.Object &&
            HasActorMaterializationTrueBoolean(specialArt, "canTeachPlayer"));
    }

    private static string? ReadHistoricalActorMaterializationEnvelopeJson(JsonElement actor) =>
        actor.ValueKind == JsonValueKind.Object &&
        actor.TryGetProperty(ActorMaterializationContract.PropertyName, out var envelope) &&
        envelope.ValueKind == JsonValueKind.Object
            ? envelope.GetRawText()
            : null;

    private static void ValidateHistoricalActorMaterializationEnvelope(
        JsonElement actor,
        string context,
        string actorType,
        string actorId,
        string historicalEnvelopeJson,
        List<ValidationIssue> issues)
    {
        var hasEquivalentCurrentEnvelope =
            actor.ValueKind == JsonValueKind.Object &&
            actor.TryGetProperty(ActorMaterializationContract.PropertyName, out var currentEnvelope) &&
            currentEnvelope.ValueKind == JsonValueKind.Object &&
            ActorMaterializationEnvelopesSemanticallyEqual(historicalEnvelopeJson, currentEnvelope.GetRawText());
        if (hasEquivalentCurrentEnvelope)
            return;

        issues.Add(new ValidationIssue(
            $"{context}.{ActorMaterializationContract.PropertyName}",
            IssueSeverity.Error,
            "Исторический materialization envelope существующего персонажа нельзя удалять или изменять.",
            code: "actor_materialization_historical_envelope_changed",
            actor: $"{actorType}:{actorId}",
            section: "ActorMaterialization",
            expected: "semantic equality with validated pre-turn materialization envelope",
            actual: actor.TryGetProperty(ActorMaterializationContract.PropertyName, out _)
                ? "changed"
                : "missing",
            repairHint: "Восстанови materialization из validated pre-turn authority без изменений; текущие игровые данные меняй только через dedicated delta contracts."));
    }

    private static bool ActorMaterializationEnvelopesSemanticallyEqual(string leftJson, string rightJson)
    {
        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(leftJson), JsonNode.Parse(rightJson));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
