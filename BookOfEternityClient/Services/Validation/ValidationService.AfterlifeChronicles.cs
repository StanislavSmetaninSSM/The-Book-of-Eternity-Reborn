using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private void ValidateAfterlifeChronicleStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                AfterlifeChronicleState.StatePath,
                IssueSeverity.Error,
                "afterlife_chronicles.json должен быть JSON object.",
                code: "afterlife_chronicle_invalid_root",
                section: "AfterlifeChronicles",
                expected: "object with chronicles[] or afterlifeChronicleUpdates[]",
                actual: root.ValueKind.ToString()));
            return;
        }

        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "schemaVersion", "AfterlifeChronicles");

        var hasChronicles = root.TryGetProperty(AfterlifeChronicleState.ChroniclesProperty, out var chronicles);
        var hasUpdates = root.TryGetProperty(AfterlifeChronicleState.UpdateProperty, out var updates);
        var hasInvalidUpdate = root.TryGetProperty(AfterlifeChronicleState.LastInvalidUpdateProperty, out _);
        if (!hasChronicles && !hasUpdates)
        {
            issues.Add(new ValidationIssue(
                contextPrefix,
                IssueSeverity.Error,
                "afterlife_chronicles.json должен содержать chronicles[] или afterlifeChronicleUpdates[].",
                code: "afterlife_chronicle_missing_chronicles",
                section: "AfterlifeChronicles",
                expected: "chronicles[] or afterlifeChronicleUpdates[]"));
        }

        if (hasInvalidUpdate)
        {
            var reason = root.TryGetProperty(AfterlifeChronicleState.LastInvalidUpdateReasonProperty, out var reasonNode)
                ? reasonNode.ToString()
                : "invalid update";
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{AfterlifeChronicleState.LastInvalidUpdateProperty}",
                IssueSeverity.Error,
                "afterlifeChronicleUpdates не был применён: форма команды повреждена.",
                code: "afterlife_chronicle_update_invalid_authority",
                section: "AfterlifeChronicles",
                expected: "valid afterlifeChronicleUpdates object/array",
                actual: reason));
        }

        ValidateAfterlifeChronicleArrayIfPresent(
            chronicles,
            hasChronicles,
            $"{contextPrefix}.{AfterlifeChronicleState.ChroniclesProperty}",
            isUpdate: false,
            issues);
        ValidateAfterlifeChronicleArrayIfPresent(
            updates,
            hasUpdates,
            $"{contextPrefix}.{AfterlifeChronicleState.UpdateProperty}",
            isUpdate: true,
            issues);
    }

    private void ValidateAfterlifeChronicleArrayIfPresent(
        JsonElement node,
        bool hasNode,
        string context,
        bool isUpdate,
        List<ValidationIssue> issues)
    {
        if (!hasNode)
            return;

        if (node.ValueKind == JsonValueKind.Object && isUpdate)
        {
            ValidateAfterlifeChronicleEntry(node, context, isUpdate, new HashSet<string>(StringComparer.OrdinalIgnoreCase), issues);
            return;
        }

        if (node.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                isUpdate
                    ? "afterlifeChronicleUpdates должен быть array или single object."
                    : "chronicles должен быть array.",
                code: isUpdate ? "afterlife_chronicle_updates_not_array_or_object" : "afterlife_chronicle_chronicles_not_array",
                section: "AfterlifeChronicles",
                expected: isUpdate ? "array or object" : "array",
                actual: node.ValueKind.ToString()));
            return;
        }

        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var entry in node.EnumerateArray())
            ValidateAfterlifeChronicleEntry(entry, $"{context}[{index++}]", isUpdate, identities, issues);
    }

    private void ValidateAfterlifeChronicleEntry(
        JsonElement entry,
        string context,
        bool isUpdate,
        HashSet<string> identities,
        List<ValidationIssue> issues)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Элемент afterlife-хроники должен быть object.",
                code: "afterlife_chronicle_entry_not_object",
                section: "AfterlifeChronicles",
                expected: "object",
                actual: entry.ValueKind.ToString()));
            return;
        }

        var chronicleId = RequireAfterlifeChronicleString(entry, context, "chronicleId", "afterlife_chronicle_missing_id", issues);
        var scopeType = RequireAfterlifeChronicleString(entry, context, "scopeType", "afterlife_chronicle_missing_scope_type", issues);
        RequireAfterlifeChronicleString(entry, context, "scopeId", "afterlife_chronicle_missing_scope_id", issues);
        ValidateAfterlifeChroniclePlayerText(
            RequireAfterlifeChronicleString(entry, context, "displayName", "afterlife_chronicle_missing_display_name", issues),
            $"{context}.displayName",
            issues);
        ValidateAfterlifeChroniclePlayerText(
            RequireAfterlifeChronicleString(entry, context, "lastEventsDescription", "afterlife_chronicle_missing_last_events_description", issues),
            $"{context}.lastEventsDescription",
            issues);

        if (!string.IsNullOrWhiteSpace(scopeType) && !AfterlifeChronicleState.ScopeTypes.Contains(scopeType))
        {
            issues.Add(new ValidationIssue(
                $"{context}.scopeType",
                IssueSeverity.Error,
                "scopeType не поддерживается для afterlife-хроники.",
                code: "afterlife_chronicle_invalid_scope_type",
                section: "AfterlifeChronicles",
                expected: string.Join("/", AfterlifeChronicleState.ScopeTypes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                actual: scopeType));
        }

        if (!string.IsNullOrWhiteSpace(chronicleId) && !identities.Add(chronicleId))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Дубликат chronicleId в afterlife-хрониках.",
                code: "afterlife_chronicle_duplicate_id",
                section: "AfterlifeChronicles",
                expected: "unique chronicleId",
                actual: chronicleId));
        }

        if (isUpdate && entry.TryGetProperty("eventDescriptions", out _))
        {
            issues.Add(new ValidationIssue(
                $"{context}.eventDescriptions",
                IssueSeverity.Error,
                "afterlifeChronicleUpdates не должен напрямую писать eventDescriptions[]: архив является read-only памятью, ГМ пишет только lastEventsDescription.",
                code: "afterlife_chronicle_update_event_descriptions_forbidden",
                section: "AfterlifeChronicles",
                expected: "omit eventDescriptions from updates"));
        }
        else if (!isUpdate)
        {
            ValidateAfterlifeChronicleStringArray(entry, context, "eventDescriptions", required: true, issues);
        }

        ValidateAfterlifeChronicleStringArray(entry, context, "persistentConsequences", required: false, issues);
        ValidateAfterlifeChronicleStringArray(entry, context, "openThreads", required: false, issues);
        ValidateAfterlifeChronicleParticipantsPlayerText(entry, context, issues);
        ValidateNonNegativeIntegerField(entry, context, issues, "lastUpdatedTurn", "AfterlifeChronicles");
    }

    private static string? RequireAfterlifeChronicleString(
        JsonElement entry,
        string context,
        string propertyName,
        string code,
        List<ValidationIssue> issues)
    {
        if (!entry.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"afterlife-хроника должна содержать непустой {propertyName}.",
                code: code,
                section: "AfterlifeChronicles",
                expected: $"non-empty {propertyName}"));
            return null;
        }

        return property.GetString();
    }

    private void ValidateAfterlifeChronicleStringArray(
        JsonElement entry,
        string context,
        string propertyName,
        bool required,
        List<ValidationIssue> issues)
    {
        if (!entry.TryGetProperty(propertyName, out var property))
        {
            if (required)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{propertyName}",
                    IssueSeverity.Error,
                    $"afterlife-хроника должна содержать {propertyName}[].",
                    code: $"afterlife_chronicle_missing_{ToSnakeCase(propertyName)}",
                    section: "AfterlifeChronicles",
                    expected: $"{propertyName}[]"));
            }
            return;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"{propertyName} должен быть массивом строк.",
                code: $"afterlife_chronicle_{ToSnakeCase(propertyName)}_not_array",
                section: "AfterlifeChronicles",
                expected: "array",
                actual: property.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{propertyName}[{index}]",
                    IssueSeverity.Error,
                    $"{propertyName} entries должны быть непустыми строками.",
                    code: $"afterlife_chronicle_{ToSnakeCase(propertyName)}_entry_invalid",
                    section: "AfterlifeChronicles",
                    expected: "non-empty string",
                    actual: item.ValueKind.ToString()));
            }
            else
            {
                ValidateAfterlifeChroniclePlayerText(
                    item.GetString(),
                    $"{context}.{propertyName}[{index}]",
                    issues);
            }

            index++;
        }
    }

    private static void ValidateAfterlifeChronicleParticipantsPlayerText(
        JsonElement entry,
        string context,
        List<ValidationIssue> issues)
    {
        if (!entry.TryGetProperty("participants", out var participants) ||
            participants.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var participant in participants.EnumerateArray())
        {
            if (participant.ValueKind == JsonValueKind.Object &&
                participant.TryGetProperty("displayName", out var displayName) &&
                displayName.ValueKind == JsonValueKind.String)
            {
                ValidateAfterlifeChroniclePlayerText(
                    displayName.GetString(),
                    $"{context}.participants[{index}].displayName",
                    issues);
            }

            index++;
        }
    }

    private static void ValidateAfterlifeChroniclePlayerText(
        string? value,
        string context,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var term = FindInternalAfterlifeChronicleTerm(value);
        if (term == null)
            return;

        issues.Add(new ValidationIssue(
            context,
            IssueSeverity.Error,
            "Игроковый текст afterlife-хроники содержит внутренний английский термин.",
            code: "afterlife_chronicle_player_text_internal_term",
            section: "AfterlifeChronicles",
            expected: "русский внутриигровой текст: Море Хаоса, Сияющая Обитель, посмертие, смертный мир",
            actual: term,
            repairHint: "Замени внутренние английские термины в видимой прозе хроники: afterlife -> посмертие/посмертный, Chaos Sea -> Море Хаоса, Shining Abode -> Сияющая Обитель, Mortal World -> смертный мир."));
    }

    private static string? FindInternalAfterlifeChronicleTerm(string value)
    {
        foreach (var term in new[] { "afterlife", "ChaosSea", "Chaos Sea", "ShiningAbode", "Shining Abode", "MortalWorld", "Mortal World" })
        {
            if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
                return term;
        }

        return null;
    }

    private static string ToSnakeCase(string value)
    {
        var chars = new List<char>();
        for (var index = 0; index < value.Length; index++)
        {
            var ch = value[index];
            if (char.IsUpper(ch) && index > 0)
                chars.Add('_');
            chars.Add(char.ToLowerInvariant(ch));
        }

        return new string(chars.ToArray());
    }
}
