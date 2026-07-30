using System.Text.Json;

namespace BookOfEternityClient.Services;

internal enum ActorMaterializationFamily
{
    Mortal,
    Afterlife
}

internal sealed record ActorMaterializationEvidence(
    string ActorType,
    string ActorId,
    IReadOnlyDictionary<string, bool> SectionHasContent,
    IReadOnlyDictionary<string, bool> CapabilityEvidence,
    IReadOnlySet<string>? DeferredCapabilityEvidence = null);

internal static class ActorMaterializationContract
{
    internal const string PropertyName = "materialization";
    internal const string SystemActorType = "system_actor";
    internal const int SchemaVersion = 1;

    private static readonly HashSet<string> EnvelopeFields = new(StringComparer.Ordinal)
    {
        "schemaVersion",
        "materializationId",
        "actorType",
        "actorId",
        "materializedAtTurn",
        "state",
        "capabilities",
        "sections"
    };

    private static readonly HashSet<string> MortalCapabilities = new(StringComparer.Ordinal)
    {
        "canFight",
        "canTeach",
        "canTrade",
        "ownsItems"
    };

    private static readonly HashSet<string> AfterlifeCapabilities = new(StringComparer.Ordinal)
    {
        "canFight",
        "canTeach",
        "canTrade"
    };

    private static readonly HashSet<string> MortalSections = new(StringComparer.Ordinal)
    {
        "skills",
        "inventory",
        "fateCards",
        "personalQuests",
        "relationships"
    };

    private static readonly HashSet<string> AfterlifeSections = new(StringComparer.Ordinal)
    {
        "standardArts",
        "specialArts",
        "customStates",
        "fateCards",
        "relationships",
        "agency",
        "progressionHistory"
    };

    private static readonly HashSet<string> AfterlifeActorTypes = new(StringComparer.Ordinal)
    {
        "guardian",
        "resident",
        "shining_resident",
        "shining_faction_head",
        "radiant_actor",
        "saref_agent",
        SystemActorType,
        "custom_afterlife_actor"
    };

    private static readonly string[] InventoryIdentityFields =
    {
        "itemId",
        "existedId",
        "initialId",
        "id"
    };

    internal static IReadOnlyList<ValidationIssue> Validate(
        JsonElement actor,
        string context,
        ActorMaterializationFamily family,
        ActorMaterializationEvidence evidence,
        bool requireEnvelope,
        bool deferEvidenceConsistency = false)
    {
        var issues = new List<ValidationIssue>();
        if (actor.ValueKind == JsonValueKind.Object &&
            actor.EnumerateObject().Count(property =>
                string.Equals(property.Name, PropertyName, StringComparison.Ordinal)) > 1)
        {
            issues.Add(CreateIssue(
                $"{context}.{PropertyName}",
                "actor_materialization_duplicate_property",
                "Персонаж должен содержать ровно один materialization envelope.",
                evidence,
                expected: "exactly one materialization property",
                actual: "duplicate materialization properties",
                repairHint: "Оставь один exact materialization envelope этого персонажа; не объединяй конфликтующие envelopes и не выбирай значение по порядку JSON properties."));
        }

        var envelope = default(JsonElement);
        var hasEnvelope = actor.ValueKind == JsonValueKind.Object &&
                           actor.TryGetProperty(PropertyName, out envelope);
        if (requireEnvelope || hasEnvelope)
            ValidateCanonicalActorType(family, context, evidence, issues);

        if (!hasEnvelope)
        {
            if (requireEnvelope)
            {
                issues.Add(CreateIssue(
                    $"{context}.{PropertyName}",
                    "actor_materialization_missing",
                    "Первичная материализация значимого персонажа должна содержать закрытый контракт materialization.",
                    evidence,
                    expected: "complete actor-bound materialization envelope",
                    actual: "missing",
                    repairHint: "Добавь materialization только для этого персонажа: exact actorType/actorId, capabilities и dispositions всех обязательных sections. Не переписывай уже валидные данные персонажа."));
            }

            return issues;
        }

        if (envelope.ValueKind != JsonValueKind.Object)
        {
            issues.Add(CreateInvalidEnvelopeIssue(
                $"{context}.{PropertyName}",
                evidence,
                "materialization должен быть JSON object.",
                "object",
                envelope.ValueKind.ToString()));
            return issues;
        }

        ValidateExactFields(envelope, EnvelopeFields, $"{context}.{PropertyName}", evidence, issues);
        ValidateEnvelopeScalars(envelope, context, evidence, issues);

        var capabilities = family == ActorMaterializationFamily.Mortal
            ? MortalCapabilities
            : AfterlifeCapabilities;
        ValidateCapabilities(
            envelope,
            context,
            capabilities,
            evidence,
            issues,
            deferEvidenceConsistency);

        var sections = family == ActorMaterializationFamily.Mortal
            ? MortalSections
            : AfterlifeSections;
        ValidateSections(
            envelope,
            context,
            sections,
            evidence,
            issues,
            deferEvidenceConsistency);

        return issues;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateUniqueMaterializationIds(
        IReadOnlyList<(JsonElement Actor, string Context, string ActorType, string ActorId)> actors)
    {
        var issues = new List<ValidationIssue>();
        var firstContextById = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (actor, context, actorType, actorId) in actors)
        {
            if (actor.ValueKind != JsonValueKind.Object ||
                !actor.TryGetProperty(PropertyName, out var envelope) ||
                envelope.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var materializationId = ReadNonEmptyString(envelope, "materializationId");
            if (materializationId == null)
                continue;

            if (firstContextById.TryAdd(materializationId, context))
                continue;

            var evidence = new ActorMaterializationEvidence(
                actorType,
                actorId,
                new Dictionary<string, bool>(StringComparer.Ordinal),
                new Dictionary<string, bool>(StringComparer.Ordinal));
            issues.Add(CreateIssue(
                $"{context}.{PropertyName}.materializationId",
                "actor_materialization_duplicate_id",
                "materializationId должен быть уникален в пределах проверяемого набора персонажей.",
                evidence,
                expected: $"unique value; first declared at {firstContextById[materializationId]}",
                actual: materializationId,
                repairHint: "Назначь этой первичной материализации новый стабильный materializationId; не меняй идентификатор уже принятого исторического envelope."));
        }

        return issues;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateMortalNpc(
        JsonElement npc,
        string context,
        string sectionName)
    {
        _ = sectionName;
        return ValidateMortalNpc(
            npc,
            context,
            requireEnvelopeOverride: null,
            deferEvidenceConsistencyOverride: null);
    }

    internal static IReadOnlyList<ValidationIssue> ValidateCanonicalMortalNpc(
        JsonElement npc,
        string context,
        bool requireEnvelope)
    {
        return ValidateMortalNpc(
            npc,
            context,
            requireEnvelopeOverride: requireEnvelope,
            deferEvidenceConsistencyOverride: false);
    }

    internal static IReadOnlyList<ValidationIssue> ValidateHistoricalMortalNpc(
        JsonElement npc,
        string context) =>
        ValidateMortalNpc(
            npc,
            context,
            requireEnvelopeOverride: true,
            deferEvidenceConsistencyOverride: true);

    private static IReadOnlyList<ValidationIssue> ValidateMortalNpc(
        JsonElement npc,
        string context,
        bool? requireEnvelopeOverride,
        bool? deferEvidenceConsistencyOverride)
    {
        var hasIdentityNode = TryGetFirstProperty(npc, out var identityNode, "NPCId", "npcId", "id");
        var initialId = ReadFirstNonEmptyString(npc, "initialId");
        var isNewNpc = (hasIdentityNode && identityNode.ValueKind == JsonValueKind.Null) ||
                       (!hasIdentityNode && !string.IsNullOrWhiteSpace(initialId));
        var permanentId = hasIdentityNode && identityNode.ValueKind == JsonValueKind.String
            ? identityNode.GetString()
            : null;
        var actorId = isNewNpc ? initialId : permanentId ?? initialId;
        if (string.IsNullOrWhiteSpace(actorId))
            return Array.Empty<ValidationIssue>();

        var hasSkills = HasUsableMortalCombatSkill(npc);
        var inventoryItemIds = ReadStructuredInventoryItemIds(npc);
        var hasInventory = inventoryItemIds.Count > 0;
        var hasRelationships = npc.TryGetProperty("relationshipLevel", out var relationshipLevel) &&
                               relationshipLevel.ValueKind == JsonValueKind.Number &&
                               !string.IsNullOrWhiteSpace(ReadFirstNonEmptyString(npc, "attitude")) &&
                               npc.TryGetProperty("relationshipLock", out var relationshipLock) &&
                               relationshipLock.ValueKind == JsonValueKind.Object;
        var canTeach = HasUsableMortalTeacherAuthority(npc);
        var canTrade = HasExplicitMortalTradeAuthority(npc);

        var evidence = new ActorMaterializationEvidence(
            "mortal_npc",
            actorId,
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["skills"] = hasSkills,
                ["inventory"] = hasInventory,
                ["fateCards"] = HasObjectArrayEntries(npc, "fateCards"),
                ["personalQuests"] = HasObjectArrayEntries(npc, "personalQuests"),
                ["relationships"] = hasRelationships
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["canFight"] = hasSkills,
                ["canTeach"] = canTeach,
                ["canTrade"] = canTrade,
                ["ownsItems"] = hasInventory
            });

        var requireEnvelope = requireEnvelopeOverride ?? isNewNpc;
        var issues = Validate(
                npc,
                context,
                ActorMaterializationFamily.Mortal,
                evidence,
                requireEnvelope,
                deferEvidenceConsistency: deferEvidenceConsistencyOverride ??
                                          (!requireEnvelopeOverride.HasValue && !isNewNpc))
            .ToList();

        if (requireEnvelope || npc.TryGetProperty(PropertyName, out _))
            ValidateEquippedItemReferences(npc, context, inventoryItemIds, evidence, issues);

        return issues;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateAfterlifeProfile(
        JsonElement profile,
        string context,
        bool requireEnvelope,
        bool? canTradeEvidence)
    {
        return ValidateAfterlifeProfileCore(
            profile,
            context,
            requireEnvelope,
            canTradeEvidence,
            deferEvidenceConsistency: !requireEnvelope);
    }

    internal static IReadOnlyList<ValidationIssue> ValidateCanonicalAfterlifeProfile(
        JsonElement profile,
        string context,
        bool requireEnvelope)
    {
        return ValidateAfterlifeProfileCore(
            profile,
            context,
            requireEnvelope,
            canTradeEvidence: false,
            deferEvidenceConsistency: false);
    }

    internal static IReadOnlyList<ValidationIssue> ValidateHistoricalAfterlifeProfile(
        JsonElement profile,
        string context) =>
        ValidateAfterlifeProfileCore(
            profile,
            context,
            requireEnvelope: true,
            canTradeEvidence: null,
            deferEvidenceConsistency: true);

    private static IReadOnlyList<ValidationIssue> ValidateAfterlifeProfileCore(
        JsonElement profile,
        string context,
        bool requireEnvelope,
        bool? canTradeEvidence,
        bool deferEvidenceConsistency)
    {
        var actorType = ReadFirstNonEmptyString(profile, "actorType");
        var canonicalActorId = ReadNonEmptyString(profile, "actorId");
        var legacyActorRef = ReadNonEmptyString(profile, "actorRef");
        var hasExactCanonicalActorType = TryReadExactNonEmptyString(profile, "actorType", out _);
        var hasExactCanonicalActorId = TryReadExactNonEmptyString(profile, "actorId", out _);
        var actorId = canonicalActorId ?? legacyActorRef;
        var hasMaterializationEnvelope = profile.ValueKind == JsonValueKind.Object &&
                                         profile.TryGetProperty(PropertyName, out _);
        if (string.IsNullOrWhiteSpace(actorType) || string.IsNullOrWhiteSpace(actorId))
        {
            if (!requireEnvelope && !hasMaterializationEnvelope)
                return Array.Empty<ValidationIssue>();

            var incompleteEvidence = new ActorMaterializationEvidence(
                actorType ?? "<missing>",
                actorId ?? "<missing>",
                new Dictionary<string, bool>(StringComparer.Ordinal),
                new Dictionary<string, bool>(StringComparer.Ordinal));
            var incompleteIssues = Validate(
                    profile,
                    context,
                    ActorMaterializationFamily.Afterlife,
                    incompleteEvidence,
                    requireEnvelope,
                    deferEvidenceConsistency: true)
                .ToList();
            incompleteIssues.Add(CreateIssue(
                $"{context}.actorId",
                "actor_materialization_actor_binding_mismatch",
                "Профиль с Actor Materialization должен иметь exact canonical actorType и actorId во внешнем объекте.",
                incompleteEvidence,
                expected: "one exact non-empty actorType and actorId",
                actual: DescribeAfterlifeProfileIdentity(profile),
                repairHint: "Восстанови exact canonical actorType и actorId внешнего профиля из его текущей канонической authority; не копируй identity из другого персонажа и не используй имя/описание."));
            return incompleteIssues;
        }

        if (string.Equals(actorType, "player_soul", StringComparison.Ordinal) &&
            string.Equals(actorId, "player_soul", StringComparison.Ordinal) &&
            !profile.TryGetProperty(PropertyName, out _))
        {
            return Array.Empty<ValidationIssue>();
        }

        var hasStandardArts = HasStructuredStandardArt(profile);
        var hasSpecialArts = HasStructuredSpecialArt(profile);
        var canFight = HasUsableAfterlifeCombatArt(profile);
        var canTeach = HasUsableAfterlifeMentorAuthority(profile);
        var hasAgency = HasAfterlifeAgency(profile);
        var hasProgressionHistory = HasObjectArrayEntries(profile, "ledger") ||
                                    HasObjectArrayEntries(profile, "progressionLedger");

        var capabilityEvidence = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["canFight"] = canFight,
            ["canTeach"] = canTeach
        };
        IReadOnlySet<string>? deferredCapabilityEvidence = null;
        if (canTradeEvidence.HasValue)
        {
            capabilityEvidence["canTrade"] = canTradeEvidence.Value;
        }
        else
        {
            deferredCapabilityEvidence = new HashSet<string>(StringComparer.Ordinal) { "canTrade" };
        }

        var evidence = new ActorMaterializationEvidence(
            actorType,
            actorId,
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["standardArts"] = hasStandardArts,
                ["specialArts"] = hasSpecialArts,
                ["customStates"] = HasObjectArrayEntries(profile, "customStates"),
                ["fateCards"] = HasObjectArrayEntries(profile, "fateCards"),
                ["relationships"] = HasObjectArrayEntries(profile, "relationships"),
                ["agency"] = hasAgency,
                ["progressionHistory"] = hasProgressionHistory
            },
            capabilityEvidence,
            deferredCapabilityEvidence);

        var issues = Validate(
            profile,
            context,
            ActorMaterializationFamily.Afterlife,
            evidence,
            requireEnvelope,
            deferEvidenceConsistency).ToList();

        var hasLegacyActorRefProperty = HasPropertyIgnoringCase(profile, "actorRef");
        if ((requireEnvelope || hasMaterializationEnvelope) &&
            (!hasExactCanonicalActorType || !hasExactCanonicalActorId || hasLegacyActorRefProperty))
        {
            issues.Add(CreateIssue(
                $"{context}.actorId",
                "actor_materialization_actor_binding_mismatch",
                "Профиль с Actor Materialization должен использовать единственный канонический actorId без legacy actorRef.",
                evidence,
                expected: $"exclusive actorId={actorId}",
                actual: DescribeAfterlifeProfileIdentity(profile),
                repairHint: "Сохрани exact canonical actorId профиля и удали legacy actorRef только из текущего materialized-профиля; не переименовывай персонажа и не меняй его канонические данные."));
        }

        return issues;
    }

    private static string DescribeAfterlifeProfileIdentity(JsonElement profile) =>
        $"actorId={Describe(profile, "actorId")}; actorRef={Describe(profile, "actorRef")}";

    private static void ValidateEnvelopeScalars(
        JsonElement envelope,
        string context,
        ActorMaterializationEvidence evidence,
        List<ValidationIssue> issues)
    {
        var envelopeContext = $"{context}.{PropertyName}";
        if (!envelope.TryGetProperty("schemaVersion", out var schemaVersion) ||
            schemaVersion.ValueKind != JsonValueKind.Number ||
            !schemaVersion.TryGetInt32(out var version) ||
            version != SchemaVersion)
        {
            issues.Add(CreateInvalidEnvelopeIssue(
                $"{envelopeContext}.schemaVersion",
                evidence,
                "materialization.schemaVersion должен точно соответствовать поддерживаемой версии.",
                SchemaVersion.ToString(),
                Describe(envelope, "schemaVersion")));
        }

        ValidateNonEmptyString(envelope, "materializationId", envelopeContext, evidence, issues);

        var actorType = ReadNonEmptyString(envelope, "actorType");
        var actorId = ReadNonEmptyString(envelope, "actorId");
        if (!string.Equals(actorType, evidence.ActorType, StringComparison.Ordinal) ||
            !string.Equals(actorId, evidence.ActorId, StringComparison.Ordinal))
        {
            issues.Add(CreateIssue(
                envelopeContext,
                "actor_materialization_actor_binding_mismatch",
                "materialization привязан не к тому персонажу.",
                evidence,
                expected: $"{evidence.ActorType}:{evidence.ActorId}",
                actual: $"{actorType ?? "missing"}:{actorId ?? "missing"}",
                repairHint: "Используй exact canonical actorType и actorId текущего персонажа; не копируй materialization другого персонажа."));
        }

        if (!envelope.TryGetProperty("materializedAtTurn", out var materializedAtTurn) ||
            materializedAtTurn.ValueKind != JsonValueKind.Number ||
            !materializedAtTurn.TryGetInt32(out var turnNumber) ||
            turnNumber < 0)
        {
            issues.Add(CreateInvalidEnvelopeIssue(
                $"{envelopeContext}.materializedAtTurn",
                evidence,
                "materializedAtTurn должен быть неотрицательным номером принятого хода.",
                "integer >= 0",
                Describe(envelope, "materializedAtTurn")));
        }

        var state = ReadNonEmptyString(envelope, "state");
        if (!string.Equals(state, "complete", StringComparison.Ordinal))
        {
            issues.Add(CreateInvalidEnvelopeIssue(
                $"{envelopeContext}.state",
                evidence,
                "materialization.state должен быть complete.",
                "complete",
                state ?? "missing"));
        }
    }

    private static void ValidateCapabilities(
        JsonElement envelope,
        string context,
        HashSet<string> requiredCapabilities,
        ActorMaterializationEvidence evidence,
        List<ValidationIssue> issues,
        bool deferEvidenceConsistency)
    {
        var capabilityContext = $"{context}.{PropertyName}.capabilities";
        if (!envelope.TryGetProperty("capabilities", out var capabilities) ||
            capabilities.ValueKind != JsonValueKind.Object)
        {
            issues.Add(CreateInvalidEnvelopeIssue(
                capabilityContext,
                evidence,
                "materialization.capabilities должен быть object с точным набором логических флагов.",
                string.Join(", ", requiredCapabilities),
                Describe(envelope, "capabilities")));
            return;
        }

        ValidateExactFields(capabilities, requiredCapabilities, capabilityContext, evidence, issues);
        foreach (var capability in requiredCapabilities)
        {
            if (!capabilities.TryGetProperty(capability, out var declared) ||
                declared.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                issues.Add(CreateInvalidEnvelopeIssue(
                    $"{capabilityContext}.{capability}",
                    evidence,
                    $"Capability {capability} должен быть явным boolean.",
                    "true or false",
                    Describe(capabilities, capability)));
                continue;
            }

            if (deferEvidenceConsistency ||
                evidence.DeferredCapabilityEvidence?.Contains(capability) == true)
                continue;

            if (!evidence.CapabilityEvidence.TryGetValue(capability, out var actualEvidence) ||
                declared.GetBoolean() != actualEvidence)
            {
                issues.Add(CreateIssue(
                    $"{capabilityContext}.{capability}",
                    "actor_materialization_capability_mismatch",
                    $"Заявленная возможность {capability} противоречит каноническим данным персонажа.",
                    evidence,
                    section: capability,
                    expected: actualEvidence.ToString(),
                    actual: declared.GetBoolean().ToString(),
                    repairHint: "Синхронизируй capability с существующим каноническим skill/teacher/trade/inventory или spiritual-art/mentor/trade authority. Валидатор не создаёт недостающий контент."));
            }
        }
    }

    private static void ValidateSections(
        JsonElement envelope,
        string context,
        HashSet<string> requiredSections,
        ActorMaterializationEvidence evidence,
        List<ValidationIssue> issues,
        bool deferEvidenceConsistency)
    {
        var sectionsContext = $"{context}.{PropertyName}.sections";
        if (!envelope.TryGetProperty("sections", out var sections) || sections.ValueKind != JsonValueKind.Object)
        {
            issues.Add(CreateInvalidEnvelopeIssue(
                sectionsContext,
                evidence,
                "materialization.sections должен быть object с точным набором секций этого типа персонажа.",
                string.Join(", ", requiredSections),
                Describe(envelope, "sections")));
            return;
        }

        ValidateExactFields(sections, requiredSections, sectionsContext, evidence, issues);

        foreach (var section in requiredSections)
        {
            if (!sections.TryGetProperty(section, out var disposition))
            {
                issues.Add(CreateIssue(
                    $"{sectionsContext}.{section}",
                    "actor_materialization_section_missing",
                    $"Первичная материализация не объясняет секцию {section}.",
                    evidence,
                    section: section,
                    expected: "populated or empty_by_design with reason",
                    actual: "missing",
                    repairHint: "Добавь disposition только для указанной секции. Используй populated при наличии содержимого или empty_by_design с непустой внутриигровой причиной при его осознанном отсутствии."));
                continue;
            }

            ValidateSectionDisposition(
                disposition,
                sectionsContext,
                section,
                evidence,
                issues,
                deferEvidenceConsistency);
        }
    }

    private static void ValidateSectionDisposition(
        JsonElement disposition,
        string sectionsContext,
        string section,
        ActorMaterializationEvidence evidence,
        List<ValidationIssue> issues,
        bool deferEvidenceConsistency)
    {
        var sectionContext = $"{sectionsContext}.{section}";
        if (disposition.ValueKind != JsonValueKind.Object)
        {
            issues.Add(CreateInvalidEnvelopeIssue(
                sectionContext,
                evidence,
                "Disposition секции должен быть object.",
                "{ state: populated } or { state: empty_by_design, reason: ... }",
                disposition.ValueKind.ToString()));
            return;
        }

        var state = ReadNonEmptyString(disposition, "state");
        var isPopulated = string.Equals(state, "populated", StringComparison.Ordinal);
        var isEmptyByDesign = string.Equals(state, "empty_by_design", StringComparison.Ordinal);
        var allowedFields = isEmptyByDesign
            ? new HashSet<string>(StringComparer.Ordinal) { "state", "reason" }
            : new HashSet<string>(StringComparer.Ordinal) { "state" };
        ValidateExactFields(disposition, allowedFields, sectionContext, evidence, issues);

        if (!isPopulated && !isEmptyByDesign)
        {
            issues.Add(CreateInvalidEnvelopeIssue(
                $"{sectionContext}.state",
                evidence,
                "Disposition секции использует неподдерживаемое состояние.",
                "populated or empty_by_design",
                state ?? "missing"));
            return;
        }

        if (isEmptyByDesign && string.IsNullOrWhiteSpace(ReadNonEmptyString(disposition, "reason")))
        {
            issues.Add(CreateInvalidEnvelopeIssue(
                $"{sectionContext}.reason",
                evidence,
                "empty_by_design требует непустую внутриигровую причину.",
                "non-empty in-world reason",
                "missing or blank"));
        }

        if (deferEvidenceConsistency)
            return;

        var hasContent = evidence.SectionHasContent.TryGetValue(section, out var sectionHasContent) && sectionHasContent;
        if ((isPopulated && !hasContent) || (isEmptyByDesign && hasContent))
        {
            issues.Add(CreateIssue(
                sectionContext,
                "actor_materialization_section_content_mismatch",
                $"Disposition секции {section} противоречит её каноническому содержимому.",
                evidence,
                section: section,
                expected: hasContent ? "populated" : "empty_by_design with reason",
                actual: state,
                repairHint: "Не используй disposition для сокрытия пустой или заполненной секции. Сохрани каноническое содержимое и выбери соответствующее ему состояние."));
        }
    }

    private static void ValidateExactFields(
        JsonElement value,
        HashSet<string> allowedFields,
        string context,
        ActorMaterializationEvidence evidence,
        List<ValidationIssue> issues)
    {
        var seenFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!seenFields.Add(property.Name))
            {
                issues.Add(CreateIssue(
                    $"{context}.{property.Name}",
                    "actor_materialization_duplicate_property",
                    "Закрытый materialization contract не допускает повторяющиеся JSON properties.",
                    evidence,
                    expected: "each property appears exactly once",
                    actual: property.Name,
                    repairHint: "Оставь ровно одно поле с этим exact именем в текущем объекте materialization; не объединяй конфликтующие значения эвристически."));
            }

            if (allowedFields.Contains(property.Name))
                continue;

            issues.Add(CreateInvalidEnvelopeIssue(
                $"{context}.{property.Name}",
                evidence,
                "Materialization contract содержит неизвестное поле.",
                string.Join(", ", allowedFields),
                property.Name));
        }
    }

    private static void ValidateCanonicalActorType(
        ActorMaterializationFamily family,
        string context,
        ActorMaterializationEvidence evidence,
        List<ValidationIssue> issues)
    {
        var isValid = family == ActorMaterializationFamily.Mortal
            ? string.Equals(evidence.ActorType, "mortal_npc", StringComparison.Ordinal)
            : AfterlifeActorTypes.Contains(evidence.ActorType);
        if (isValid)
            return;

        var expected = family == ActorMaterializationFamily.Mortal
            ? "mortal_npc"
            : string.Join(", ", AfterlifeActorTypes.OrderBy(value => value, StringComparer.Ordinal));
        issues.Add(CreateIssue(
            $"{context}.actorType",
            "actor_materialization_invalid_actor_type",
            "Actor Materialization использует неподдерживаемый или неканонический actorType.",
            evidence,
            expected: expected,
            actual: evidence.ActorType,
            repairHint: "Используй exact canonical non-player actorType token. player_soul не участвует в Actor Materialization contract."));
    }

    private static void ValidateNonEmptyString(
        JsonElement value,
        string propertyName,
        string context,
        ActorMaterializationEvidence evidence,
        List<ValidationIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(ReadNonEmptyString(value, propertyName)))
            return;

        issues.Add(CreateInvalidEnvelopeIssue(
            $"{context}.{propertyName}",
            evidence,
            $"{propertyName} должен быть непустой строкой.",
            "non-empty string",
            Describe(value, propertyName)));
    }

    private static string? ReadNonEmptyString(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return null;

        var text = property.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool TryReadExactNonEmptyString(
        JsonElement value,
        string propertyName,
        out string result)
    {
        result = string.Empty;
        if (value.ValueKind != JsonValueKind.Object)
            return false;

        var matches = 0;
        foreach (var property in value.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(property.Name, propertyName, StringComparison.Ordinal) ||
                property.Value.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            matches++;
            result = property.Value.GetString() ?? string.Empty;
        }

        return matches == 1 && !string.IsNullOrWhiteSpace(result);
    }

    private static bool HasPropertyIgnoringCase(JsonElement value, string propertyName) =>
        value.ValueKind == JsonValueKind.Object &&
        value.EnumerateObject().Any(property =>
            string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));

    private static string? ReadFirstNonEmptyString(JsonElement value, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var text = ReadNonEmptyString(value, propertyName);
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }

    private static bool TryGetFirstProperty(JsonElement value, out JsonElement property, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (value.TryGetProperty(propertyName, out property))
                return true;
        }

        property = default;
        return false;
    }

    internal static bool HasUsableMortalCombatSkill(JsonElement npc) =>
        HasArrayObjectMatching(npc, "activeSkills", IsUsableMortalActiveCombatSkill) ||
        HasArrayObjectMatching(npc, "passiveSkills", IsUsableMortalPassiveCombatSkill);

    internal static bool HasUsableMortalTeacherAuthority(JsonElement npc)
    {
        if (!npc.TryGetProperty("teacherProfile", out var teacherProfile) ||
            teacherProfile.ValueKind != JsonValueKind.Object ||
            !HasTrueBoolean(teacherProfile, "canTeach"))
        {
            return false;
        }

        return HasArrayObjectMatching(teacherProfile, "skills", IsUsableMortalTeacherSkill);
    }

    internal static bool HasExplicitMortalTradeAuthority(JsonElement npc)
    {
        if (!npc.TryGetProperty("tradeState", out var tradeState) ||
            tradeState.ValueKind != JsonValueKind.Object ||
            !HasTrueBoolean(tradeState, "canTrade"))
        {
            return false;
        }

        return NpcTradeService.IsValidMerchantProfileCode(
            ReadFirstNonEmptyString(tradeState, "merchantProfile"));
    }

    internal static bool HasUsableAfterlifeCombatArt(JsonElement profile) =>
        HasPositiveNumericObjectValue(profile, "standardArts") ||
        HasArrayObjectMatching(profile, "specialArts", specialArt =>
            IsStructuredSpecialArt(specialArt) &&
            specialArt.TryGetProperty("tier", out var tier) &&
            tier.TryGetInt32(out var tierValue) &&
            tierValue > 0);

    internal static bool HasUsableAfterlifeMentorAuthority(JsonElement profile)
    {
        var hasShowcase = HasPositiveMentorShowcase(profile);
        var hasRootAuthority = HasTrueBoolean(profile, "canTeachPlayer");
        var hasMentorProfileAuthority = HasNestedTrueBoolean(profile, "mentorProfile", "canTeach");
        var hasTeachableSpecialArt = HasArrayObjectMatching(profile, "specialArts", specialArt =>
            IsPositiveSpecialArt(specialArt) && HasTrueBoolean(specialArt, "canTeachPlayer"));
        var hasDeclaredAuthority = hasShowcase ||
                                   hasRootAuthority ||
                                   hasMentorProfileAuthority ||
                                   hasTeachableSpecialArt;
        var hasTeachableContent = hasShowcase ||
                                  hasTeachableSpecialArt ||
                                  ((hasRootAuthority || hasMentorProfileAuthority) &&
                                   (HasPositiveNumericObjectValue(profile, "standardArts") ||
                                    HasArrayObjectMatching(profile, "specialArts", IsPositiveSpecialArt)));
        return hasDeclaredAuthority && hasTeachableContent;
    }

    internal static bool HasAfterlifeAgency(JsonElement profile) =>
        HasMeaningfulObject(profile, "goals") ||
        HasObjectArrayEntries(profile, "personalQuests") ||
        HasMeaningfulObject(profile, "currentActivity") ||
        HasObjectArrayEntries(profile, "completedActivities");

    private static HashSet<string> ReadStructuredInventoryItemIds(JsonElement npc)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (!npc.TryGetProperty("inventory", out var inventory) || inventory.ValueKind != JsonValueKind.Array)
            return ids;

        foreach (var item in inventory.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var identityField in InventoryIdentityFields)
            {
                var itemId = ReadNonEmptyString(item, identityField);
                if (itemId != null)
                    ids.Add(itemId);
            }
        }

        return ids;
    }

    private static void ValidateEquippedItemReferences(
        JsonElement npc,
        string context,
        IReadOnlySet<string> inventoryItemIds,
        ActorMaterializationEvidence evidence,
        List<ValidationIssue> issues)
    {
        if (!npc.TryGetProperty("equippedItems", out var equippedItems))
            return;

        if (equippedItems.ValueKind != JsonValueKind.Object)
        {
            issues.Add(CreateIssue(
                $"{context}.equippedItems",
                "actor_materialization_inventory_reference_mismatch",
                "equippedItems первично materialized NPC должен быть object со ссылками на его inventory.",
                evidence,
                section: "inventory",
                expected: "object whose non-null values resolve to this NPC inventory",
                actual: equippedItems.ValueKind.ToString(),
                repairHint: "Исправь equippedItems на object слотов; не прячь ссылку в scalar/array и не создавай предмет через materialization metadata."));
            return;
        }

        foreach (var slot in equippedItems.EnumerateObject())
        {
            if (slot.Value.ValueKind == JsonValueKind.Null)
                continue;

            var reference = slot.Value.ValueKind == JsonValueKind.String
                ? slot.Value.GetString()
                : null;
            if (!string.IsNullOrWhiteSpace(reference) && inventoryItemIds.Contains(reference))
                continue;

            issues.Add(CreateIssue(
                $"{context}.equippedItems.{slot.Name}",
                "actor_materialization_inventory_reference_mismatch",
                "Каждая непустая ссылка equippedItems должна разрешаться в структурированный предмет inventory этого NPC.",
                evidence,
                section: "inventory",
                expected: "exact itemId/existedId/initialId/id from this NPC inventory",
                actual: reference ?? slot.Value.ToString(),
                repairHint: "Исправь equippedItems на exact ID существующего предмета этого NPC либо очисти слот; не создавай предмет в materialization metadata."));
        }
    }

    private static bool HasLegacyMortalSkillIdentity(JsonElement skill) =>
        skill.ValueKind == JsonValueKind.Object &&
        !string.IsNullOrWhiteSpace(ReadFirstNonEmptyString(skill, "skillId", "id"));

    private static bool IsUsableMortalActiveCombatSkill(JsonElement skill) =>
        ValidationService.IsProductionValidMortalActiveSkill(skill);

    private static bool IsUsableMortalPassiveCombatSkill(JsonElement skill) =>
        ValidationService.IsProductionValidMortalPassiveSkill(skill);

    private static bool IsUsableMortalTeacherSkill(JsonElement skill)
    {
        if (!HasLegacyMortalSkillIdentity(skill) ||
            string.IsNullOrWhiteSpace(ReadFirstNonEmptyString(skill, "skillName", "displayName", "name")) ||
            !skill.TryGetProperty("masteryLevel", out var masteryLevel) ||
            masteryLevel.ValueKind != JsonValueKind.Number ||
            !masteryLevel.TryGetInt32(out var mastery) ||
            mastery <= 0)
        {
            return false;
        }

        return true;
    }

    private static bool HasStructuredStandardArt(JsonElement profile)
    {
        if (!profile.TryGetProperty("standardArts", out var standardArts) ||
            standardArts.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return standardArts.EnumerateObject().Any(entry =>
            entry.Value.ValueKind == JsonValueKind.Number &&
            entry.Value.TryGetInt32(out var tier) &&
            tier >= 0);
    }

    private static bool HasStructuredSpecialArt(JsonElement profile) =>
        HasArrayObjectMatching(profile, "specialArts", IsStructuredSpecialArt);

    private static bool IsStructuredSpecialArt(JsonElement specialArt) =>
        specialArt.ValueKind == JsonValueKind.Object &&
        !string.IsNullOrWhiteSpace(ReadFirstNonEmptyString(specialArt, "artId", "specialArtId", "id")) &&
        specialArt.TryGetProperty("tier", out var tier) &&
        tier.ValueKind == JsonValueKind.Number &&
        tier.TryGetInt32(out var tierValue) &&
        tierValue >= 0;

    private static bool IsPositiveSpecialArt(JsonElement specialArt) =>
        IsStructuredSpecialArt(specialArt) &&
        specialArt.TryGetProperty("tier", out var tier) &&
        tier.TryGetInt32(out var tierValue) &&
        tierValue > 0;

    private static bool HasPositiveMentorShowcase(JsonElement profile)
    {
        if (!profile.TryGetProperty("mentorTrainingShowcase", out var showcase) ||
            showcase.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return HasArrayObjectMatching(showcase, "offers", offer =>
            !string.IsNullOrWhiteSpace(ReadFirstNonEmptyString(offer, "offerId")) &&
            !string.IsNullOrWhiteSpace(ReadFirstNonEmptyString(offer, "targetKind")) &&
            !string.IsNullOrWhiteSpace(ReadFirstNonEmptyString(offer, "targetId")) &&
            !string.IsNullOrWhiteSpace(ReadFirstNonEmptyString(offer, "targetName", "displayName", "name")) &&
            offer.TryGetProperty("sourceCap", out var sourceCap) &&
            sourceCap.ValueKind == JsonValueKind.Number &&
            sourceCap.TryGetInt32(out var sourceCapValue) &&
            sourceCapValue > 0);
    }

    private static bool HasObjectArrayEntries(JsonElement value, string propertyName) =>
        HasArrayObjectMatching(value, propertyName, HasMeaningfulJsonValue);

    private static bool HasArrayObjectMatching(
        JsonElement value,
        string propertyName,
        Func<JsonElement, bool> predicate)
    {
        if (!value.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            return false;

        return array.EnumerateArray().Any(item =>
            item.ValueKind == JsonValueKind.Object && predicate(item));
    }

    private static bool HasPositiveNumericObjectValue(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
            return false;

        return property.EnumerateObject().Any(entry =>
            entry.Value.ValueKind == JsonValueKind.Number &&
            entry.Value.TryGetInt32(out var number) &&
            number > 0);
    }

    private static bool HasTrueBoolean(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;

    private static bool HasNestedTrueBoolean(JsonElement value, string objectPropertyName, string booleanPropertyName) =>
        value.TryGetProperty(objectPropertyName, out var nested) &&
        nested.ValueKind == JsonValueKind.Object &&
        HasTrueBoolean(nested, booleanPropertyName);

    private static bool HasMeaningfulObject(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.Object &&
        HasMeaningfulJsonValue(property);

    private static bool HasMeaningfulDisposition(JsonElement profile)
    {
        if (!profile.TryGetProperty("disposition", out var disposition))
            return false;

        return disposition.ValueKind switch
        {
            JsonValueKind.Object => HasMeaningfulJsonValue(disposition),
            JsonValueKind.String => !string.IsNullOrWhiteSpace(disposition.GetString()),
            _ => false
        };
    }

    private static bool HasMeaningfulJsonValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => !string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Number => true,
            JsonValueKind.True or JsonValueKind.False => true,
            JsonValueKind.Object => value.EnumerateObject().Any(entry => HasMeaningfulJsonValue(entry.Value)),
            JsonValueKind.Array => value.EnumerateArray().Any(HasMeaningfulJsonValue),
            _ => false
        };

    private static string Describe(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property)
            ? property.ToString()
            : "missing";

    private static ValidationIssue CreateInvalidEnvelopeIssue(
        string path,
        ActorMaterializationEvidence evidence,
        string message,
        string? expected,
        string? actual) =>
        CreateIssue(
            path,
            "actor_materialization_invalid_envelope",
            message,
            evidence,
            expected: expected,
            actual: actual,
            repairHint: "Исправь только materialization этого персонажа по Actor Materialization v1; не удаляй и не выдумывай канонические данные, чтобы обойти ошибку.");

    private static ValidationIssue CreateIssue(
        string path,
        string code,
        string message,
        ActorMaterializationEvidence evidence,
        string? section = null,
        string? expected = null,
        string? actual = null,
        string? repairHint = null) =>
        new(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            actor: $"{evidence.ActorType}:{evidence.ActorId}",
            section: section ?? "ActorMaterialization",
            expected: expected,
            actual: actual,
            repairHint: repairHint);
}
