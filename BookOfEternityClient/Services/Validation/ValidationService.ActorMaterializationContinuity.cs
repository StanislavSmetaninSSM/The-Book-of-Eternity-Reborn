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
        string? HistoricalEnvelopeJson,
        IReadOnlySet<string> HistoricalEnvelopeSections)
    {
        public MortalActorMaterializationPreTurnState Merge(MortalActorMaterializationPreTurnState other) =>
            new(
                PromotionSignals.Merge(other.PromotionSignals),
                HistoricalEnvelopeJson ?? other.HistoricalEnvelopeJson,
                MergeActorMaterializationSectionSets(
                    HistoricalEnvelopeSections,
                    other.HistoricalEnvelopeSections));
    }

    private readonly record struct AfterlifeActorMaterializationPreTurnState(
        AfterlifeActorMaterializationPromotionSignals PromotionSignals,
        string? HistoricalEnvelopeJson,
        string ActorType,
        string ActorId)
    {
        public AfterlifeActorMaterializationPreTurnState Merge(AfterlifeActorMaterializationPreTurnState other) =>
            new(
                PromotionSignals.Merge(other.PromotionSignals),
                HistoricalEnvelopeJson ?? other.HistoricalEnvelopeJson,
                ActorType,
                ActorId);
    }

    private static IReadOnlySet<string> MergeActorMaterializationSectionSets(
        IReadOnlySet<string> left,
        IReadOnlySet<string> right)
    {
        var result = new HashSet<string>(left, StringComparer.Ordinal);
        result.UnionWith(right);
        return result;
    }

    private static void AddExistingMortalActorMaterializationResendIssue(
        string context,
        string actorId,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            $"{context}.{ActorMaterializationContract.PropertyName}",
            IssueSeverity.Error,
            "UpdateNPCs не должен повторно пересылать first-materialization envelope персонажа, уже материализованного в validated pre-turn authority.",
            code: "actor_materialization_existing_resend_forbidden",
            actor: $"mortal_npc:{actorId}",
            section: "ActorMaterialization",
            expected: "dedicated NPC delta without materialization",
            actual: "materialization resent for validated pre-turn actor",
            repairHint: "Убери materialization из UpdateNPCs и сохрани исторический envelope в canonical NPCsInScene record без изменений."));
    }

    private static void AddMissingHistoricalActorMaterializationEnvelopeIssue(
        string statePath,
        string actorType,
        string actorId,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            $"{statePath}.{ActorMaterializationContract.PropertyName}",
            IssueSeverity.Error,
            "Исторический materialization envelope существующего персонажа нельзя удалять вместе с его canonical carrier.",
            code: "actor_materialization_historical_envelope_changed",
            actor: $"{actorType}:{actorId}",
            section: "ActorMaterialization",
            expected: "canonical actor carrier with validated pre-turn materialization envelope",
            actual: "canonical actor carrier missing",
            repairHint: "Восстанови canonical actor record и materialization из validated pre-turn authority; отдельный delta без canonical carrier не сохраняет историческую authority."));
    }

    private async Task ValidateAcceptedTurnActorMaterializationCompletenessAsync(List<ValidationIssue> issues)
    {
        await ValidateAcceptedTurnMortalActorMaterializationCompletenessAsync(issues);
        await ValidateAcceptedTurnAfterlifeActorMaterializationCompletenessAsync(issues);
    }

    private async Task ValidateAcceptedTurnMortalActorMaterializationCompletenessAsync(List<ValidationIssue> issues)
    {
        var currentJson = await _fs.ReadFileAsync(MortalActorMaterializationStatePath);
        if (string.IsNullOrWhiteSpace(currentJson))
            return;
        var preTurnJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(MortalActorMaterializationStatePath);

        try
        {
            using var currentDocument = JsonDocument.Parse(currentJson);
            if (currentDocument.RootElement.ValueKind != JsonValueKind.Object)
                return;

            issues.AddRange(ValidateCurrentMortalMaterializationIds(currentDocument.RootElement));
            if (preTurnJson == null)
                return;

            using var preTurnDocument = JsonDocument.Parse(preTurnJson);
            if (!TryReadCanonicalMortalActorStates(preTurnDocument.RootElement, out var preTurnActors))
                return;

            var evaluatedHistoricalActors = new HashSet<string>(StringComparer.Ordinal);
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
                            var hasCurrentEnvelope = actor.TryGetProperty(
                                ActorMaterializationContract.PropertyName,
                                out _);
                            var isUpdateSection = string.Equals(
                                sectionName,
                                GuardianPolicyContracts.NpcCoreUpdateSectionName,
                                StringComparison.Ordinal);
                            var wasHistoricalCarrierInSameSection =
                                previousState.HistoricalEnvelopeSections.Contains(sectionName);
                            if (isUpdateSection && !wasHistoricalCarrierInSameSection)
                            {
                                if (!hasCurrentEnvelope)
                                    continue;

                                AddExistingMortalActorMaterializationResendIssue(
                                    context,
                                    actorId,
                                    issues);
                                continue;
                            }

                            evaluatedHistoricalActors.Add(actorId);
                            issues.AddRange(ActorMaterializationContract.ValidateHistoricalMortalNpc(
                                actor,
                                context));
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

            foreach (var (actorId, previousState) in preTurnActors)
            {
                if (previousState.HistoricalEnvelopeJson != null &&
                    !evaluatedHistoricalActors.Contains(actorId))
                {
                    AddMissingHistoricalActorMaterializationEnvelopeIssue(
                        MortalActorMaterializationStatePath,
                        "mortal_npc",
                        actorId,
                        issues);
                }
            }

        }
        catch (JsonException)
        {
            // Ordinary canonical-state validation reports malformed current or pre-turn JSON.
        }
    }

    private static IReadOnlyList<ValidationIssue> ValidateCurrentMortalMaterializationIds(JsonElement root)
    {
        var currentActors = new List<(JsonElement Actor, string Context, string ActorType, string ActorId)>();
        foreach (var sectionName in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (!root.TryGetProperty(sectionName, out var actors) || actors.ValueKind != JsonValueKind.Array)
                continue;

            var index = 0;
            foreach (var actor in actors.EnumerateArray())
            {
                var context = $"{MortalActorMaterializationStatePath}.{sectionName}[{index++}]";
                var actorId = ReadCanonicalMortalActorId(actor);
                if (actorId != null)
                    currentActors.Add((actor, context, "mortal_npc", actorId));
            }
        }

        return ActorMaterializationContract.ValidateUniqueMaterializationIds(currentActors);
    }

    private static bool TryReadCanonicalMortalActorStates(
        JsonElement root,
        out Dictionary<string, MortalActorMaterializationPreTurnState> result)
    {
        result = new Dictionary<string, MortalActorMaterializationPreTurnState>(StringComparer.Ordinal);
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

                var historicalEnvelopeJson = ReadHistoricalActorMaterializationEnvelopeJson(actor);
                var state = new MortalActorMaterializationPreTurnState(
                    ReadMortalActorMaterializationPromotionSignals(actor),
                    historicalEnvelopeJson,
                    historicalEnvelopeJson == null
                        ? new HashSet<string>(StringComparer.Ordinal)
                        : new HashSet<string>(StringComparer.Ordinal) { sectionName });
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
                return actorId;
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
            CanTeach: ActorMaterializationContract.HasUsableMortalTeacherAuthority(actor),
            CanTrade: ActorMaterializationContract.HasExplicitMortalTradeAuthority(actor),
            CanFight: ActorMaterializationContract.HasUsableMortalCombatSkill(actor),
            HasActorBrainScope: hasPlans ||
                                hasCurrentActivity ||
                                HasActorMaterializationArrayEntries(actor, "completedActivities"));
    }

    private static bool HasActorMaterializationArrayEntries(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var array) &&
        array.ValueKind == JsonValueKind.Array &&
        array.GetArrayLength() > 0;

    private async Task ValidateAcceptedTurnAfterlifeActorMaterializationCompletenessAsync(
        List<ValidationIssue> issues)
    {
        var currentJson = await _fs.ReadFileAsync(AfterlifeActorMaterializationStatePath);
        if (string.IsNullOrWhiteSpace(currentJson))
            return;
        var preTurnJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(AfterlifeActorMaterializationStatePath);

        try
        {
            using var currentDocument = JsonDocument.Parse(currentJson);
            if (currentDocument.RootElement.ValueKind != JsonValueKind.Object)
                return;

            issues.AddRange(ValidateCurrentAfterlifeMaterializationIds(currentDocument.RootElement));
            if (preTurnJson == null)
                return;

            using var preTurnDocument = JsonDocument.Parse(preTurnJson);
            if (!TryReadCanonicalAfterlifeActorStates(preTurnDocument.RootElement, out var preTurnActors) ||
                !currentDocument.RootElement.TryGetProperty(
                    AfterlifeEntityProfileState.ProfilesProperty,
                    out var profiles) ||
                profiles.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var evaluatedHistoricalActors = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var profile in profiles.EnumerateArray())
            {
                var context = $"{AfterlifeActorMaterializationStatePath}.{AfterlifeEntityProfileState.ProfilesProperty}[{index++}]";
                if (!TryReadCanonicalAfterlifeActorIdentity(
                        profile,
                        out var actorType,
                        out var actorId,
                        out var identityKey) ||
                    (string.Equals(actorType, "player_soul", StringComparison.Ordinal) &&
                     string.Equals(actorId, "player_soul", StringComparison.Ordinal)))
                {
                    continue;
                }

                var currentSignals = ReadAfterlifeActorMaterializationPromotionSignals(profile);
                if (preTurnActors.TryGetValue(identityKey, out var previousState))
                {
                    if (previousState.HistoricalEnvelopeJson != null)
                    {
                        evaluatedHistoricalActors.Add(identityKey);
                        issues.AddRange(ActorMaterializationContract.ValidateHistoricalAfterlifeProfile(
                            profile,
                            context));
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

            foreach (var (identityKey, previousState) in preTurnActors)
            {
                if (previousState.HistoricalEnvelopeJson != null &&
                    !evaluatedHistoricalActors.Contains(identityKey))
                {
                    AddMissingHistoricalActorMaterializationEnvelopeIssue(
                        AfterlifeActorMaterializationStatePath,
                        previousState.ActorType,
                        previousState.ActorId,
                        issues);
                }
            }

        }
        catch (JsonException)
        {
            // Ordinary canonical-state validation reports malformed current or pre-turn JSON.
        }
    }

    private static IReadOnlyList<ValidationIssue> ValidateCurrentAfterlifeMaterializationIds(JsonElement root)
    {
        var currentActors = new List<(JsonElement Actor, string Context, string ActorType, string ActorId)>();
        if (!root.TryGetProperty(AfterlifeEntityProfileState.ProfilesProperty, out var profiles) ||
            profiles.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ValidationIssue>();
        }

        var index = 0;
        foreach (var profile in profiles.EnumerateArray())
        {
            var context = $"{AfterlifeActorMaterializationStatePath}.{AfterlifeEntityProfileState.ProfilesProperty}[{index++}]";
            if (!TryReadCanonicalAfterlifeActorIdentity(
                    profile,
                    out var actorType,
                    out var actorId,
                    out _) ||
                (string.Equals(actorType, "player_soul", StringComparison.Ordinal) &&
                 string.Equals(actorId, "player_soul", StringComparison.Ordinal)))
            {
                continue;
            }

            currentActors.Add((profile, context, actorType, actorId));
        }

        return ActorMaterializationContract.ValidateUniqueMaterializationIds(currentActors);
    }

    private static bool TryReadCanonicalAfterlifeActorStates(
        JsonElement root,
        out Dictionary<string, AfterlifeActorMaterializationPreTurnState> result)
    {
        result = new Dictionary<string, AfterlifeActorMaterializationPreTurnState>(
            StringComparer.Ordinal);
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
                    out var actorType,
                    out var actorId,
                    out var identityKey) ||
                (string.Equals(actorType, "player_soul", StringComparison.Ordinal) &&
                 string.Equals(actorId, "player_soul", StringComparison.Ordinal)))
            {
                continue;
            }

            var state = new AfterlifeActorMaterializationPreTurnState(
                ReadAfterlifeActorMaterializationPromotionSignals(profile),
                ReadHistoricalActorMaterializationEnvelopeJson(profile),
                actorType,
                actorId);
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
                return text;
        }

        return null;
    }

    private static AfterlifeActorMaterializationPromotionSignals
        ReadAfterlifeActorMaterializationPromotionSignals(JsonElement profile)
    {
        var hasActorBrainScope = ActorMaterializationContract.HasAfterlifeAgency(profile) ||
            HasActorMaterializationArrayEntries(profile, "ledger") ||
            HasActorMaterializationArrayEntries(profile, "progressionLedger");

        return new AfterlifeActorMaterializationPromotionSignals(
            CanTeach: ActorMaterializationContract.HasUsableAfterlifeMentorAuthority(profile),
            CanFight: ActorMaterializationContract.HasUsableAfterlifeCombatArt(profile),
            HasActorBrainScope: hasActorBrainScope);
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
