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
    IReadOnlyDictionary<string, bool> CapabilityEvidence);

internal static class ActorMaterializationContract
{
    internal const string PropertyName = "materialization";
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

    internal static IReadOnlyList<ValidationIssue> Validate(
        JsonElement actor,
        string context,
        ActorMaterializationFamily family,
        ActorMaterializationEvidence evidence,
        bool requireEnvelope)
    {
        var issues = new List<ValidationIssue>();
        if (actor.ValueKind != JsonValueKind.Object ||
            !actor.TryGetProperty(PropertyName, out var envelope))
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
        ValidateCapabilities(envelope, context, capabilities, evidence, issues);

        var sections = family == ActorMaterializationFamily.Mortal
            ? MortalSections
            : AfterlifeSections;
        ValidateSections(envelope, context, sections, evidence, issues);

        return issues;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateMortalNpc(
        JsonElement npc,
        string context,
        string sectionName)
    {
        var hasIdentityNode = TryGetFirstProperty(npc, out var identityNode, "NPCId", "npcId", "id");
        var isNewNpc = hasIdentityNode && identityNode.ValueKind == JsonValueKind.Null;
        var permanentId = hasIdentityNode && identityNode.ValueKind == JsonValueKind.String
            ? identityNode.GetString()
            : null;
        var initialId = ReadFirstNonEmptyString(npc, "initialId");
        var actorId = isNewNpc ? initialId : permanentId ?? initialId;
        if (string.IsNullOrWhiteSpace(actorId))
            return Array.Empty<ValidationIssue>();

        var hasSkills = HasArrayEntries(npc, "activeSkills") || HasArrayEntries(npc, "passiveSkills");
        var hasInventory = HasArrayEntries(npc, "inventory");
        var hasRelationships = npc.TryGetProperty("relationshipLevel", out var relationshipLevel) &&
                               relationshipLevel.ValueKind == JsonValueKind.Number &&
                               !string.IsNullOrWhiteSpace(ReadFirstNonEmptyString(npc, "attitude")) &&
                               npc.TryGetProperty("relationshipLock", out var relationshipLock) &&
                               relationshipLock.ValueKind == JsonValueKind.Object;
        var canTeach = npc.TryGetProperty("teacherProfile", out var teacherProfile) &&
                       teacherProfile.ValueKind == JsonValueKind.Object &&
                       teacherProfile.TryGetProperty("canTeach", out var canTeachNode) &&
                       canTeachNode.ValueKind == JsonValueKind.True &&
                       HasArrayEntries(teacherProfile, "skills");
        var canTrade = npc.TryGetProperty("tradeState", out var tradeState) &&
                       tradeState.ValueKind == JsonValueKind.Object &&
                       tradeState.TryGetProperty("canTrade", out var canTradeNode) &&
                       canTradeNode.ValueKind == JsonValueKind.True;

        var evidence = new ActorMaterializationEvidence(
            "mortal_npc",
            actorId,
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["skills"] = hasSkills,
                ["inventory"] = hasInventory,
                ["fateCards"] = HasArrayEntries(npc, "fateCards"),
                ["personalQuests"] = HasArrayEntries(npc, "personalQuests"),
                ["relationships"] = hasRelationships
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["canFight"] = hasSkills,
                ["canTeach"] = canTeach,
                ["canTrade"] = canTrade,
                ["ownsItems"] = hasInventory
            });

        var issues = Validate(
                npc,
                context,
                ActorMaterializationFamily.Mortal,
                evidence,
                requireEnvelope: isNewNpc)
            .ToList();

        if (!isNewNpc &&
            string.Equals(sectionName, "UpdateNPCs", StringComparison.OrdinalIgnoreCase) &&
            npc.TryGetProperty(PropertyName, out _))
        {
            issues.Add(CreateIssue(
                $"{context}.{PropertyName}",
                "actor_materialization_existing_resend_forbidden",
                "UpdateNPCs не должен повторно пересылать first-materialization envelope существующего NPC.",
                evidence,
                expected: "dedicated NPC delta commands without materialization",
                actual: "materialization resent in UpdateNPCs",
                repairHint: "Убери materialization из UpdateNPCs и меняй существующего NPC через соответствующие dedicated delta commands. Не удаляй materialization из его canonical NPCsInScene record."));
        }

        return issues;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateAfterlifeProfile(
        JsonElement profile,
        string context,
        bool requireEnvelope,
        bool canTradeEvidence)
    {
        var actorType = ReadFirstNonEmptyString(profile, "actorType");
        var actorId = ReadFirstNonEmptyString(profile, "actorId", "actorRef");
        if (string.IsNullOrWhiteSpace(actorType) || string.IsNullOrWhiteSpace(actorId))
            return Array.Empty<ValidationIssue>();

        var hasStandardArts = HasPositiveNumericObjectValue(profile, "standardArts");
        var hasSpecialArts = HasUsableSpecialArt(profile);
        var hasAnyArt = hasStandardArts || hasSpecialArts;
        var hasMentorAuthority = HasTrueBoolean(profile, "canTeachPlayer") ||
                                 HasNestedTrueBoolean(profile, "mentorProfile", "canTeach") ||
                                 HasTeachableSpecialArt(profile);
        var hasAgency = HasObjectWithNonEmptyString(profile, "goals", "goalId") ||
                        HasArrayEntries(profile, "personalQuests") ||
                        HasNonNullObject(profile, "currentActivity") ||
                        HasArrayEntries(profile, "completedActivities");
        var hasProgressionHistory = HasArrayEntries(profile, "ledger") ||
                                    HasArrayEntries(profile, "progressionLedger");

        var evidence = new ActorMaterializationEvidence(
            actorType,
            actorId,
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["standardArts"] = hasStandardArts,
                ["specialArts"] = hasSpecialArts,
                ["customStates"] = HasArrayEntries(profile, "customStates"),
                ["fateCards"] = HasArrayEntries(profile, "fateCards"),
                ["relationships"] = HasArrayEntries(profile, "relationships"),
                ["agency"] = hasAgency,
                ["progressionHistory"] = hasProgressionHistory
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["canFight"] = hasAnyArt,
                ["canTeach"] = hasMentorAuthority && hasAnyArt,
                ["canTrade"] = canTradeEvidence
            });

        return Validate(
            profile,
            context,
            ActorMaterializationFamily.Afterlife,
            evidence,
            requireEnvelope);
    }

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
        List<ValidationIssue> issues)
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
        List<ValidationIssue> issues)
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

        foreach (var property in sections.EnumerateObject())
        {
            if (!requiredSections.Contains(property.Name))
            {
                issues.Add(CreateInvalidEnvelopeIssue(
                    $"{sectionsContext}.{property.Name}",
                    evidence,
                    "materialization.sections содержит неизвестную секцию.",
                    string.Join(", ", requiredSections),
                    property.Name));
            }
        }

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

            ValidateSectionDisposition(disposition, sectionsContext, section, evidence, issues);
        }
    }

    private static void ValidateSectionDisposition(
        JsonElement disposition,
        string sectionsContext,
        string section,
        ActorMaterializationEvidence evidence,
        List<ValidationIssue> issues)
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
        foreach (var property in value.EnumerateObject())
        {
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

    private static bool HasArrayEntries(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.Array &&
        property.GetArrayLength() > 0;

    private static bool HasPositiveNumericObjectValue(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
            return false;

        return property.EnumerateObject().Any(entry =>
            entry.Value.ValueKind == JsonValueKind.Number &&
            entry.Value.TryGetInt32(out var number) &&
            number > 0);
    }

    private static bool HasUsableSpecialArt(JsonElement profile)
    {
        if (!profile.TryGetProperty("specialArts", out var specialArts) || specialArts.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var specialArt in specialArts.EnumerateArray())
        {
            if (specialArt.ValueKind != JsonValueKind.Object)
                continue;

            if (specialArt.TryGetProperty("tier", out var tier) &&
                tier.ValueKind == JsonValueKind.Number &&
                tier.TryGetInt32(out var tierValue) &&
                tierValue > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasTeachableSpecialArt(JsonElement profile)
    {
        if (!profile.TryGetProperty("specialArts", out var specialArts) || specialArts.ValueKind != JsonValueKind.Array)
            return false;

        return specialArts.EnumerateArray().Any(specialArt =>
            specialArt.ValueKind == JsonValueKind.Object &&
            HasTrueBoolean(specialArt, "canTeachPlayer"));
    }

    private static bool HasTrueBoolean(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;

    private static bool HasNestedTrueBoolean(JsonElement value, string objectPropertyName, string booleanPropertyName) =>
        value.TryGetProperty(objectPropertyName, out var nested) &&
        nested.ValueKind == JsonValueKind.Object &&
        HasTrueBoolean(nested, booleanPropertyName);

    private static bool HasObjectWithNonEmptyString(
        JsonElement value,
        string objectPropertyName,
        string stringPropertyName) =>
        value.TryGetProperty(objectPropertyName, out var nested) &&
        nested.ValueKind == JsonValueKind.Object &&
        !string.IsNullOrWhiteSpace(ReadNonEmptyString(nested, stringPropertyName));

    private static bool HasNonNullObject(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Object;

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
