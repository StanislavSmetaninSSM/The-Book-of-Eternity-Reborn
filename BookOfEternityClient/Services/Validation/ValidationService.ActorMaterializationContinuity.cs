using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private const string MortalActorMaterializationStatePath = "game_state/npcs/npc_core.json";
    private const string AfterlifeActorMaterializationStatePath = "game_state/meta/afterlife_entity_profiles.json";
    private const int JsonAuthoritySubtreeMaxDepth = 64;
    private const int JsonAuthoritySubtreeMaxNodes = 32768;

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
    }

    private readonly record struct MortalActorMaterializationPromotionAuthority(
        bool? CanTeach,
        bool? CanTrade,
        bool? CanFight,
        bool? HasActorBrainScope)
    {
        public ActorMaterializationPromotionSignals ToSignals() =>
            new(
                CanTeach == true,
                CanTrade == true,
                CanFight == true,
                HasActorBrainScope == true);

        public bool TryMerge(
            MortalActorMaterializationPromotionAuthority other,
            out MortalActorMaterializationPromotionAuthority merged)
        {
            merged = default;
            if (!TryMergeOptionalSignal(CanTeach, other.CanTeach, out var canTeach) ||
                !TryMergeOptionalSignal(CanTrade, other.CanTrade, out var canTrade) ||
                !TryMergeOptionalSignal(CanFight, other.CanFight, out var canFight) ||
                !TryMergeOptionalSignal(
                    HasActorBrainScope,
                    other.HasActorBrainScope,
                    out var hasActorBrainScope))
            {
                return false;
            }

            merged = new MortalActorMaterializationPromotionAuthority(
                canTeach,
                canTrade,
                canFight,
                hasActorBrainScope);
            return true;
        }

        private static bool TryMergeOptionalSignal(
            bool? left,
            bool? right,
            out bool? merged)
        {
            merged = left ?? right;
            return !left.HasValue || !right.HasValue || left.Value == right.Value;
        }
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
        MortalActorMaterializationPromotionAuthority PromotionAuthority,
        string? HistoricalEnvelopeJson,
        IReadOnlySet<string> HistoricalEnvelopeSections,
        IReadOnlyDictionary<string, string> HistoricalActorJsonBySection)
    {
        public ActorMaterializationPromotionSignals PromotionSignals =>
            PromotionAuthority.ToSignals();
    }

    private readonly record struct MortalActorMaterializationPreTurnAuthority(
        ValidatedPendingTurnSnapshotStatus Status,
        IReadOnlyDictionary<string, MortalActorMaterializationPreTurnState>? Actors);

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

    private static void AddUnusableMortalActorMaterializationPreTurnAuthorityIssue(
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            MortalActorMaterializationStatePath,
            IssueSeverity.Error,
            "Validated pre-turn authority персонажей неоднозначна или повреждена; проверка materialization continuity не может быть пропущена.",
            code: "actor_materialization_pre_turn_authority_unusable",
            section: "ActorMaterialization",
            expected: "readable unambiguous validated pre-turn NPC authority",
            actual: "malformed, duplicate, conflicting, or lossy NPC authority",
            repairHint: "Откати текущий ход к client-owned validated snapshot и восстанови snapshot через штатный rollback/recovery; не переписывай pre-turn authority догадками ГМа."));
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
        string? preTurnJson = null;
        var currentDocumentParsed = false;

        try
        {
            using var currentDocument = JsonDocument.Parse(currentJson);
            currentDocumentParsed = true;
            if (currentDocument.RootElement.ValueKind != JsonValueKind.Object)
                return;

            issues.AddRange(ValidateCurrentMortalMaterializationIds(currentDocument.RootElement));
            var snapshotLookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
            if (snapshotLookup.Status == ValidatedPendingTurnSnapshotStatus.Missing)
                return;
            if (snapshotLookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
                snapshotLookup.Manifest == null)
            {
                AddUnusableMortalActorMaterializationPreTurnAuthorityIssue(issues);
                return;
            }

            preTurnJson = await ReadValidatedPendingTurnSnapshotFileAsync(
                snapshotLookup.Manifest,
                MortalActorMaterializationStatePath);
            if (preTurnJson == null)
            {
                AddUnusableMortalActorMaterializationPreTurnAuthorityIssue(issues);
                return;
            }

            using var preTurnDocument = JsonDocument.Parse(preTurnJson);
            if (!TryReadCanonicalMortalActorStates(preTurnDocument.RootElement, out var preTurnActors))
            {
                AddUnusableMortalActorMaterializationPreTurnAuthorityIssue(issues);
                return;
            }

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
                            if (isUpdateSection)
                            {
                                var isUnchangedHistoricalCarrier =
                                    wasHistoricalCarrierInSameSection &&
                                    previousState.HistoricalActorJsonBySection.TryGetValue(
                                        sectionName,
                                        out var historicalActorJson) &&
                                    JsonValuesSemanticallyEqual(
                                        historicalActorJson,
                                        actor.GetRawText());
                                if (!isUnchangedHistoricalCarrier)
                                {
                                    if (!hasCurrentEnvelope)
                                        continue;

                                    AddExistingMortalActorMaterializationResendIssue(
                                        context,
                                        actorId,
                                        issues);
                                    continue;
                                }
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
            if (currentDocumentParsed && preTurnJson != null)
                AddUnusableMortalActorMaterializationPreTurnAuthorityIssue(issues);
            // Ordinary canonical-state validation separately reports malformed current JSON.
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
                var actorId = ReadCurrentMortalActorMaterializationId(actor);
                if (actorId != null)
                    currentActors.Add((actor, context, "mortal_npc", actorId));
            }
        }

        return ActorMaterializationContract.ValidateUniqueMaterializationIds(currentActors);
    }

    private MortalActorMaterializationPreTurnAuthority
        ReadValidatedMortalActorMaterializationPreTurnAuthoritySync()
    {
        var lookup = LoadValidatedPendingTurnSnapshotLookupSync();
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest == null)
            return new MortalActorMaterializationPreTurnAuthority(lookup.Status, null);

        var preTurnJson = ReadValidatedPendingTurnSnapshotFileSync(
            lookup.Manifest,
            MortalActorMaterializationStatePath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
        {
            return new MortalActorMaterializationPreTurnAuthority(
                ValidatedPendingTurnSnapshotStatus.Unusable,
                null);
        }

        try
        {
            using var document = JsonDocument.Parse(preTurnJson);
            return TryReadCanonicalMortalActorStates(document.RootElement, out var states)
                ? new MortalActorMaterializationPreTurnAuthority(
                    ValidatedPendingTurnSnapshotStatus.Usable,
                    states)
                : new MortalActorMaterializationPreTurnAuthority(
                    ValidatedPendingTurnSnapshotStatus.Unusable,
                    null);
        }
        catch (JsonException)
        {
            return new MortalActorMaterializationPreTurnAuthority(
                ValidatedPendingTurnSnapshotStatus.Unusable,
                null);
        }
    }

    private static bool ShouldBlockMortalActorInventoryResend(
        JsonElement actor,
        string actorId,
        MortalActorMaterializationPreTurnAuthority preTurnAuthority)
    {
        if (preTurnAuthority.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
            preTurnAuthority.Actors == null)
        {
            return true;
        }

        if (!preTurnAuthority.Actors.TryGetValue(actorId, out var previousState))
            return false;
        if (previousState.HistoricalEnvelopeJson != null)
            return true;

        var currentSignals = ReadMortalActorMaterializationPromotionSignals(actor);
        var hasMaterializationEnvelope = actor.TryGetProperty(
            ActorMaterializationContract.PropertyName,
            out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object;
        return !hasMaterializationEnvelope ||
               !currentSignals.IsPromotionFrom(previousState.PromotionSignals);
    }

    private static bool TryReadCanonicalMortalActorStates(
        JsonElement root,
        out Dictionary<string, MortalActorMaterializationPreTurnState> result)
    {
        result = new Dictionary<string, MortalActorMaterializationPreTurnState>(StringComparer.Ordinal);
        var actorSections = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var sectionName in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            var carrierOccurrences = 0;
            foreach (var property in root.EnumerateObject())
            {
                if (!string.Equals(property.Name, sectionName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.Equals(property.Name, sectionName, StringComparison.Ordinal))
                    return false;

                carrierOccurrences++;
                if (carrierOccurrences > 1)
                    return false;
            }

            if (!root.TryGetProperty(sectionName, out var actors))
                continue;
            if (actors.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var actor in actors.EnumerateArray())
            {
                if (actor.ValueKind != JsonValueKind.Object)
                    return false;
                if (!IsLosslessJsonAuthoritySubtree(actor))
                    return false;

                if (!TryReadSingleCanonicalMortalActorId(actor, out var actorId))
                    return false;

                var historicalEnvelopeJson = ReadHistoricalActorMaterializationEnvelopeJson(actor);
                var state = new MortalActorMaterializationPreTurnState(
                    ReadMortalActorMaterializationPromotionAuthority(actor),
                    historicalEnvelopeJson,
                    historicalEnvelopeJson == null
                        ? new HashSet<string>(StringComparer.Ordinal)
                        : new HashSet<string>(StringComparer.Ordinal) { sectionName },
                    historicalEnvelopeJson == null
                        ? new Dictionary<string, string>(StringComparer.Ordinal)
                        : new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            [sectionName] = actor.GetRawText()
                        });
                if (!actorSections.TryGetValue(actorId, out var seenSections))
                {
                    actorSections[actorId] = new HashSet<string>(StringComparer.Ordinal)
                    {
                        sectionName
                    };
                    result[actorId] = state;
                    continue;
                }

                if (!seenSections.Add(sectionName) ||
                    !TryMergeCompatibleMortalActorStates(result[actorId], state, out var mergedState))
                {
                    return false;
                }

                result[actorId] = mergedState;
            }
        }

        return true;
    }

    private static bool TryMergeCompatibleMortalActorStates(
        MortalActorMaterializationPreTurnState left,
        MortalActorMaterializationPreTurnState right,
        out MortalActorMaterializationPreTurnState merged)
    {
        merged = default;
        if (!left.PromotionAuthority.TryMerge(
                right.PromotionAuthority,
                out var mergedPromotionAuthority) ||
            !TryMergeHistoricalActorMaterializationEnvelopes(
                left.HistoricalEnvelopeJson,
                right.HistoricalEnvelopeJson,
                out var mergedEnvelopeJson))
        {
            return false;
        }

        merged = new MortalActorMaterializationPreTurnState(
            mergedPromotionAuthority,
            mergedEnvelopeJson,
            MergeActorMaterializationSectionSets(
                left.HistoricalEnvelopeSections,
                right.HistoricalEnvelopeSections),
            MergeActorMaterializationSectionMaps(
                left.HistoricalActorJsonBySection,
                right.HistoricalActorJsonBySection));
        return true;
    }

    private static IReadOnlyDictionary<string, string> MergeActorMaterializationSectionMaps(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        var result = new Dictionary<string, string>(left, StringComparer.Ordinal);
        foreach (var (sectionName, actorJson) in right)
            result[sectionName] = actorJson;
        return result;
    }

    private static bool TryMergeHistoricalActorMaterializationEnvelopes(
        string? leftJson,
        string? rightJson,
        out string? mergedJson)
    {
        mergedJson = leftJson ?? rightJson;
        if (leftJson == null || rightJson == null)
            return true;

        return JsonValuesSemanticallyEqual(leftJson, rightJson);
    }

    private static bool IsLosslessJsonAuthoritySubtree(JsonElement value)
    {
        var remainingNodes = JsonAuthoritySubtreeMaxNodes;
        return IsLosslessJsonAuthoritySubtree(value, depth: 0, ref remainingNodes);
    }

    private static bool IsLosslessJsonAuthoritySubtree(
        JsonElement value,
        int depth,
        ref int remainingNodes)
    {
        if (depth > JsonAuthoritySubtreeMaxDepth || remainingNodes-- <= 0)
            return false;

        if (value.ValueKind == JsonValueKind.Object)
        {
            var propertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var canonicalRootIdentityNames = depth == 0
                ? new HashSet<string>(StringComparer.Ordinal)
                : null;
            foreach (var property in value.EnumerateObject())
            {
                var isCanonicalNpcIdPairMember = depth == 0 &&
                    (string.Equals(property.Name, "NPCId", StringComparison.Ordinal) ||
                     string.Equals(property.Name, "npcId", StringComparison.Ordinal));
                var hasUniquePropertyName = isCanonicalNpcIdPairMember
                    ? canonicalRootIdentityNames!.Add(property.Name)
                    : propertyNames.Add(property.Name);
                if (!hasUniquePropertyName ||
                    !IsLosslessJsonAuthoritySubtree(property.Value, depth + 1, ref remainingNodes))
                {
                    return false;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (!IsLosslessJsonAuthoritySubtree(item, depth + 1, ref remainingNodes))
                    return false;
            }
        }

        return true;
    }

    private static bool TryReadSingleCanonicalMortalActorId(
        JsonElement actor,
        out string actorId)
    {
        actorId = string.Empty;
        string? upperNpcId = null;
        string? lowerNpcId = null;
        string? genericId = null;
        var hasUpperNpcId = false;
        var hasLowerNpcId = false;
        var hasGenericId = false;
        foreach (var property in actor.EnumerateObject())
        {
            var resemblesIdentityAlias =
                string.Equals(property.Name, "NPCId", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase);
            if (!resemblesIdentityAlias)
                continue;
            if (property.Value.ValueKind != JsonValueKind.String)
                return false;

            var value = property.Value.GetString();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (string.Equals(property.Name, "NPCId", StringComparison.Ordinal))
            {
                if (hasUpperNpcId)
                    return false;
                hasUpperNpcId = true;
                upperNpcId = value;
            }
            else if (string.Equals(property.Name, "npcId", StringComparison.Ordinal))
            {
                if (hasLowerNpcId)
                    return false;
                hasLowerNpcId = true;
                lowerNpcId = value;
            }
            else if (string.Equals(property.Name, "id", StringComparison.Ordinal))
            {
                if (hasGenericId)
                    return false;
                hasGenericId = true;
                genericId = value;
            }
            else
            {
                return false;
            }
        }

        if (hasGenericId && (hasUpperNpcId || hasLowerNpcId))
            return false;
        if (hasUpperNpcId && hasLowerNpcId &&
            !string.Equals(upperNpcId, lowerNpcId, StringComparison.Ordinal))
        {
            return false;
        }

        actorId = genericId ?? upperNpcId ?? lowerNpcId ?? string.Empty;
        return actorId.Length > 0;
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

    private static string? ReadCurrentMortalActorMaterializationId(JsonElement actor) =>
        ReadCanonicalMortalActorId(actor) ?? ReadActorMaterializationString(actor, "initialId");

    private static ActorMaterializationPromotionSignals ReadMortalActorMaterializationPromotionSignals(
        JsonElement actor) =>
        ReadMortalActorMaterializationPromotionAuthority(actor).ToSignals();

    private static MortalActorMaterializationPromotionAuthority
        ReadMortalActorMaterializationPromotionAuthority(JsonElement actor)
    {
        var hasPlans = actor.TryGetProperty("plans", out var plans) &&
                       plans.ValueKind == JsonValueKind.String &&
                       !string.IsNullOrWhiteSpace(plans.GetString());
        var hasCurrentActivity = actor.TryGetProperty("currentActivity", out var currentActivity) &&
                                 currentActivity.ValueKind == JsonValueKind.Object;
        var hasPlansAuthority = actor.TryGetProperty("plans", out _);
        var hasCurrentActivityAuthority = actor.TryGetProperty("currentActivity", out _);
        var hasCompletedActivitiesAuthority = actor.TryGetProperty("completedActivities", out _);
        var hasCompletedActivities = HasActorMaterializationArrayEntries(
            actor,
            "completedActivities");
        var hasActorBrainScope = hasPlans || hasCurrentActivity || hasCompletedActivities;

        return new MortalActorMaterializationPromotionAuthority(
            CanTeach: ReadMortalTeacherPromotionAuthority(actor),
            CanTrade: ReadMortalTradePromotionAuthority(actor),
            CanFight: ReadMortalCombatPromotionAuthority(actor),
            HasActorBrainScope: hasActorBrainScope
                ? true
                : hasPlansAuthority &&
                  hasCurrentActivityAuthority &&
                  hasCompletedActivitiesAuthority
                    ? false
                    : null);
    }

    private static bool? ReadMortalTeacherPromotionAuthority(JsonElement actor)
    {
        if (!actor.TryGetProperty("teacherProfile", out var teacherProfile) ||
            teacherProfile.ValueKind != JsonValueKind.Object ||
            !teacherProfile.TryGetProperty("canTeach", out var canTeach) ||
            canTeach.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return null;
        }

        if (canTeach.ValueKind == JsonValueKind.False)
            return false;
        if (!teacherProfile.TryGetProperty("skills", out _))
            return null;

        return ActorMaterializationContract.HasUsableMortalTeacherAuthority(actor);
    }

    private static bool? ReadMortalTradePromotionAuthority(JsonElement actor)
    {
        if (!actor.TryGetProperty("tradeState", out var tradeState) ||
            tradeState.ValueKind != JsonValueKind.Object ||
            !tradeState.TryGetProperty("canTrade", out var canTrade) ||
            canTrade.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return null;
        }

        if (canTrade.ValueKind == JsonValueKind.False)
            return false;
        if (!tradeState.TryGetProperty("merchantProfile", out _))
            return null;

        return ActorMaterializationContract.HasExplicitMortalTradeAuthority(actor);
    }

    private static bool? ReadMortalCombatPromotionAuthority(JsonElement actor)
    {
        var hasActiveSkillsAuthority = actor.TryGetProperty("activeSkills", out _);
        var hasPassiveSkillsAuthority = actor.TryGetProperty("passiveSkills", out _);
        if (ActorMaterializationContract.HasUsableMortalCombatSkill(actor))
            return true;

        return hasActiveSkillsAuthority && hasPassiveSkillsAuthority
            ? false
            : null;
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

        try
        {
            using var currentDocument = JsonDocument.Parse(currentJson);
            if (currentDocument.RootElement.ValueKind != JsonValueKind.Object)
                return;

            issues.AddRange(ValidateCurrentAfterlifeMaterializationIds(currentDocument.RootElement));
            var snapshotLookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
            if (snapshotLookup.Status == ValidatedPendingTurnSnapshotStatus.Missing)
                return;
            if (snapshotLookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
                snapshotLookup.Manifest == null)
            {
                AddUnusableAfterlifeActorMaterializationPreTurnAuthorityIssue(
                    AfterlifeActorMaterializationStatePath,
                    issues);
                return;
            }

            var preTurnJson = await ReadValidatedPendingTurnSnapshotFileAsync(
                snapshotLookup.Manifest,
                AfterlifeActorMaterializationStatePath);
            if (preTurnJson == null)
            {
                AddUnusableAfterlifeActorMaterializationPreTurnAuthorityIssue(
                    AfterlifeActorMaterializationStatePath,
                    issues);
                return;
            }

            using var preTurnDocument = JsonDocument.Parse(preTurnJson);
            if (!TryReadCanonicalAfterlifeActorStates(preTurnDocument.RootElement, out var preTurnActors))
            {
                AddUnusableAfterlifeActorMaterializationPreTurnAuthorityIssue(
                    AfterlifeActorMaterializationStatePath,
                    issues);
                return;
            }
            if (!currentDocument.RootElement.TryGetProperty(
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

            await ValidateAcceptedTurnAfterlifeActorProfileBindingsAsync(
                currentDocument.RootElement,
                snapshotLookup.Manifest,
                issues);

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
            if (profile.ValueKind != JsonValueKind.Object ||
                !TryReadCanonicalAfterlifePreTurnActorIdentity(
                    profile,
                    out var actorType,
                    out var actorId,
                    out var identityKey))
            {
                return false;
            }
            if (string.Equals(actorType, "player_soul", StringComparison.Ordinal) &&
                string.Equals(actorId, "player_soul", StringComparison.Ordinal))
                continue;

            var state = new AfterlifeActorMaterializationPreTurnState(
                ReadAfterlifeActorMaterializationPromotionSignals(profile),
                ReadHistoricalActorMaterializationEnvelopeJson(profile),
                actorType,
                actorId);
            if (!result.TryAdd(identityKey, state))
                return false;
        }

        return true;
    }

    private static bool TryReadCanonicalAfterlifePreTurnActorIdentity(
        JsonElement profile,
        out string actorType,
        out string actorId,
        out string identityKey)
    {
        actorType = string.Empty;
        actorId = string.Empty;
        identityKey = string.Empty;
        if (!TryReadExactNonEmptyString(profile, "actorType", out actorType))
            return false;

        var hasActorId = TryReadExactNonEmptyString(profile, "actorId", out var canonicalActorId);
        var hasActorRef = TryReadExactNonEmptyString(profile, "actorRef", out var legacyActorRef);
        if (hasActorId == hasActorRef)
            return false;

        actorId = hasActorId ? canonicalActorId : legacyActorRef;
        identityKey = $"{actorType}\u001f{actorId}";
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
            JsonValuesSemanticallyEqual(historicalEnvelopeJson, currentEnvelope.GetRawText());
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

    private static bool JsonValuesSemanticallyEqual(string leftJson, string rightJson)
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
