using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private void ValidateAfterlifeGlobalFlagStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                AfterlifeGlobalFlagState.StatePath,
                IssueSeverity.Error,
                "afterlife_global_flags.json должен быть JSON object.",
                code: "afterlife_global_flag_invalid_root",
                section: "AfterlifeGlobalFlags",
                expected: "object with flags[] or afterlifeGlobalFlagUpdates[]",
                actual: root.ValueKind.ToString()));
            return;
        }

        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "schemaVersion", "AfterlifeGlobalFlags");

        var hasFlags = root.TryGetProperty(AfterlifeGlobalFlagState.FlagsProperty, out var flags);
        var hasUpdates = root.TryGetProperty(AfterlifeGlobalFlagState.UpdateProperty, out var updates);
        var hasInvalidUpdate = root.TryGetProperty(AfterlifeGlobalFlagState.LastInvalidUpdateProperty, out _);
        if (!hasFlags && !hasUpdates)
        {
            issues.Add(new ValidationIssue(
                contextPrefix,
                IssueSeverity.Error,
                "afterlife_global_flags.json должен содержать flags[] или afterlifeGlobalFlagUpdates[].",
                code: "afterlife_global_flag_missing_flags",
                section: "AfterlifeGlobalFlags",
                expected: "flags[] or afterlifeGlobalFlagUpdates[]"));
        }

        if (hasInvalidUpdate)
        {
            var reason = root.TryGetProperty(AfterlifeGlobalFlagState.LastInvalidUpdateReasonProperty, out var reasonNode)
                ? reasonNode.ToString()
                : "invalid update";
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{AfterlifeGlobalFlagState.LastInvalidUpdateProperty}",
                IssueSeverity.Error,
                "afterlifeGlobalFlagUpdates не был применён: форма команды повреждена.",
                code: "afterlife_global_flag_update_invalid_authority",
                section: "AfterlifeGlobalFlags",
                expected: "valid afterlifeGlobalFlagUpdates object/array",
                actual: reason));
        }

        ValidateAfterlifeGlobalFlagArrayIfPresent(
            flags,
            hasFlags,
            $"{contextPrefix}.{AfterlifeGlobalFlagState.FlagsProperty}",
            isUpdate: false,
            issues);
        ValidateAfterlifeGlobalFlagArrayIfPresent(
            updates,
            hasUpdates,
            $"{contextPrefix}.{AfterlifeGlobalFlagState.UpdateProperty}",
            isUpdate: true,
            issues);
    }

    private void ValidateAfterlifeGlobalFlagArrayIfPresent(
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
            ValidateAfterlifeGlobalFlagEntry(node, context, isUpdate, new HashSet<string>(StringComparer.OrdinalIgnoreCase), issues);
            return;
        }

        if (node.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                isUpdate
                    ? "afterlifeGlobalFlagUpdates должен быть array или single object."
                    : "flags должен быть array.",
                code: isUpdate ? "afterlife_global_flag_updates_not_array_or_object" : "afterlife_global_flag_flags_not_array",
                section: "AfterlifeGlobalFlags",
                expected: isUpdate ? "array or object" : "array",
                actual: node.ValueKind.ToString()));
            return;
        }

        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var entry in node.EnumerateArray())
            ValidateAfterlifeGlobalFlagEntry(entry, $"{context}[{index++}]", isUpdate, identities, issues);
    }

    private void ValidateAfterlifeGlobalFlagEntry(
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
                "Элемент afterlife global flag должен быть object.",
                code: "afterlife_global_flag_entry_not_object",
                section: "AfterlifeGlobalFlags",
                expected: "object",
                actual: entry.ValueKind.ToString()));
            return;
        }

        var flagId = RequireAfterlifeGlobalFlagString(entry, context, "flagId", "afterlife_global_flag_missing_id", issues);
        var category = RequireAfterlifeGlobalFlagString(entry, context, "category", "afterlife_global_flag_missing_category", issues);
        var state = RequireAfterlifeGlobalFlagString(entry, context, "state", "afterlife_global_flag_missing_state", issues);
        var visibility = RequireAfterlifeGlobalFlagString(entry, context, "visibility", "afterlife_global_flag_missing_visibility", issues);
        RequireAfterlifeGlobalFlagString(entry, context, "reason", "afterlife_global_flag_missing_reason", issues);
        RequireAfterlifeGlobalFlagString(entry, context, "evidence", "afterlife_global_flag_missing_evidence", issues);

        if (!isUpdate)
        {
            ValidateNonNegativeIntegerField(entry, context, issues, "createdAtTurn", "AfterlifeGlobalFlags");
            ValidateAfterlifeGlobalFlagStringArray(entry, context, "linkedActors", required: true, issues);
            ValidateAfterlifeGlobalFlagStringArray(entry, context, "linkedChronicles", required: true, issues);
        }

        ValidateNonNegativeIntegerField(entry, context, issues, "updatedAtTurn", "AfterlifeGlobalFlags");
        ValidateAfterlifeGlobalFlagStringArray(entry, context, "linkedActors", required: false, issues);
        ValidateAfterlifeGlobalFlagStringArray(entry, context, "linkedChronicles", required: false, issues);

        if (!string.IsNullOrWhiteSpace(category) && !AfterlifeGlobalFlagState.Categories.Contains(category))
        {
            issues.Add(new ValidationIssue(
                $"{context}.category",
                IssueSeverity.Error,
                "category не поддерживается для afterlife global flag.",
                code: "afterlife_global_flag_invalid_category",
                section: "AfterlifeGlobalFlags",
                expected: string.Join("/", AfterlifeGlobalFlagState.Categories.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                actual: category));
        }

        if (!string.IsNullOrWhiteSpace(state) && !AfterlifeGlobalFlagState.States.Contains(state))
        {
            issues.Add(new ValidationIssue(
                $"{context}.state",
                IssueSeverity.Error,
                "state не поддерживается для afterlife global flag.",
                code: "afterlife_global_flag_invalid_state",
                section: "AfterlifeGlobalFlags",
                expected: string.Join("/", AfterlifeGlobalFlagState.States.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                actual: state));
        }

        if (!string.IsNullOrWhiteSpace(visibility) && !AfterlifeGlobalFlagState.Visibilities.Contains(visibility))
        {
            issues.Add(new ValidationIssue(
                $"{context}.visibility",
                IssueSeverity.Error,
                "visibility не поддерживается для afterlife global flag.",
                code: "afterlife_global_flag_invalid_visibility",
                section: "AfterlifeGlobalFlags",
                expected: string.Join("/", AfterlifeGlobalFlagState.Visibilities.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                actual: visibility));
        }

        if (!string.IsNullOrWhiteSpace(flagId) && !identities.Add(flagId))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Дубликат flagId в afterlife global flags.",
                code: "afterlife_global_flag_duplicate_id",
                section: "AfterlifeGlobalFlags",
                expected: "unique flagId",
                actual: flagId));
        }

        if (string.Equals(state, "obsolete", StringComparison.OrdinalIgnoreCase))
        {
            RequireAfterlifeGlobalFlagString(
                entry,
                context,
                "obsoleteReason",
                "afterlife_global_flag_obsolete_reason_missing",
                issues);
        }

        if (isUpdate)
        {
            RequireAfterlifeGlobalFlagString(
                entry,
                context,
                "gmThoughtsSummary",
                "afterlife_global_flag_update_missing_gm_thoughts",
                issues);
        }

        if (string.Equals(visibility, "hidden", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(visibility, "gm_only", StringComparison.OrdinalIgnoreCase))
        {
            RejectHiddenAfterlifeGlobalFlagLeakFields(entry, context, issues);
        }
    }

    private async Task ValidateAfterlifeGlobalFlagPreTurnContinuityAsync(List<ValidationIssue> issues)
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest == null)
            return;

        var preTurnJson = await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, AfterlifeGlobalFlagState.StatePath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        var preTurnRoot = TryParseAfterlifeGlobalFlagRoot(preTurnJson);
        if (preTurnRoot?[AfterlifeGlobalFlagState.FlagsProperty] is not JsonArray preTurnFlags)
            return;

        var currentJson = await _fs.ReadFileAsync(AfterlifeGlobalFlagState.StatePath);
        var currentRoot = TryParseAfterlifeGlobalFlagRoot(currentJson);
        if (currentRoot != null && currentRoot.ContainsKey(AfterlifeGlobalFlagState.UpdateProperty))
            currentRoot = AfterlifeGlobalFlagState.ProjectCanonicalRoot(currentRoot, preTurnRoot);

        foreach (var preTurnFlag in preTurnFlags.OfType<JsonObject>())
        {
            var flagId = AfterlifeGlobalFlagState.GetNodeString(preTurnFlag["flagId"]);
            if (string.IsNullOrWhiteSpace(flagId))
                continue;

            var currentFlag = AfterlifeGlobalFlagState.FindFlag(currentRoot, flagId);
            if (currentFlag != null)
                continue;

            issues.Add(new ValidationIssue(
                AfterlifeGlobalFlagState.StatePath,
                IssueSeverity.Error,
                "Pre-turn afterlife global flag нельзя удалить direct full-array replacement: пометь его obsolete с obsoleteReason.",
                code: "afterlife_global_flag_removed_without_obsolete_marker",
                section: "AfterlifeGlobalFlags",
                expected: $"flagId={flagId} retained or state=obsolete with obsoleteReason",
                actual: "flag missing from current afterlife_global_flags.json",
                repairHint: "Верни флаг в flags[] или закрой его через afterlifeGlobalFlagUpdates/state=obsolete с obsoleteReason и gmThoughtsSummary."));
        }
    }

    private static JsonObject? TryParseAfterlifeGlobalFlagRoot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static string? RequireAfterlifeGlobalFlagString(
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
                $"afterlife global flag должен содержать непустой {propertyName}.",
                code: code,
                section: "AfterlifeGlobalFlags",
                expected: $"non-empty {propertyName}"));
            return null;
        }

        return property.GetString();
    }

    private void ValidateAfterlifeGlobalFlagStringArray(
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
                    $"afterlife global flag должен содержать {propertyName}[].",
                    code: $"afterlife_global_flag_missing_{ToSnakeCase(propertyName)}",
                    section: "AfterlifeGlobalFlags",
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
                code: $"afterlife_global_flag_{ToSnakeCase(propertyName)}_not_array",
                section: "AfterlifeGlobalFlags",
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
                    code: $"afterlife_global_flag_{ToSnakeCase(propertyName)}_entry_invalid",
                    section: "AfterlifeGlobalFlags",
                    expected: "non-empty string",
                    actual: item.ValueKind.ToString()));
            }

            index++;
        }
    }

    private static void RejectHiddenAfterlifeGlobalFlagLeakFields(
        JsonElement entry,
        string context,
        List<ValidationIssue> issues)
    {
        foreach (var field in new[] { "playerFacingSummary", "publicSummary", "playerHint" })
        {
            if (!entry.TryGetProperty(field, out _))
                continue;

            issues.Add(new ValidationIssue(
                $"{context}.{field}",
                IssueSeverity.Error,
                "Hidden/gm_only afterlife global flag не должен содержать player-facing summary fields.",
                code: "afterlife_global_flag_hidden_player_summary_forbidden",
                section: "AfterlifeGlobalFlags",
                expected: "omit playerFacingSummary/publicSummary/playerHint when visibility is hidden or gm_only",
                actual: field));
        }
    }
}
