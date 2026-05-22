using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private void ValidateChaosSeaGuardianPoliticsStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            AddChaosGuardianPoliticsIssue(
                issues,
                contextPrefix,
                "chaos_guardian_politics_invalid_root",
                "chaos_sea_guardian_politics.json должен быть JSON object.",
                "object with canonical arrays or guardian political command surfaces",
                root.ValueKind.ToString());
            return;
        }

        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "schemaVersion", "ChaosSeaGuardianPolitics");
        var knownGuardians = LoadKnownChaosGuardianIds();

        var hasRelations = root.TryGetProperty(ChaosSeaGuardianPoliticsState.RelationsProperty, out var relations);
        var hasProjects = root.TryGetProperty(ChaosSeaGuardianPoliticsState.ProjectsProperty, out var projects);
        var hasInfluence = root.TryGetProperty(ChaosSeaGuardianPoliticsState.InfluenceZonesProperty, out var influenceZones);
        var hasChronicle = root.TryGetProperty(ChaosSeaGuardianPoliticsState.ChronicleProperty, out var chronicle);
        var hasRelationUpdates = root.TryGetProperty(ChaosSeaGuardianPoliticsState.RelationUpdatesProperty, out var relationUpdates);
        var hasProjectUpdates = root.TryGetProperty(ChaosSeaGuardianPoliticsState.ProjectUpdatesProperty, out var projectUpdates);
        var hasInfluenceUpdates = root.TryGetProperty(ChaosSeaGuardianPoliticsState.InfluenceUpdatesProperty, out var influenceUpdates);
        var hasChronicleUpdates = root.TryGetProperty(ChaosSeaGuardianPoliticsState.ChronicleUpdatesProperty, out var chronicleUpdates);
        var hasCompletions = root.TryGetProperty(ChaosSeaGuardianPoliticsState.CompleteProjectsProperty, out var completions);

        if (!hasRelations && !hasProjects && !hasInfluence && !hasChronicle &&
            !hasRelationUpdates && !hasProjectUpdates && !hasInfluenceUpdates && !hasChronicleUpdates && !hasCompletions)
        {
            AddChaosGuardianPoliticsIssue(
                issues,
                contextPrefix,
                "chaos_guardian_politics_missing_surface",
                "chaos_sea_guardian_politics.json должен содержать canonical arrays или documented command surface.",
                "relations[], projects[], influenceZones[], chronicle[], guardianPolitical*Updates[], or completeGuardianPoliticalProjects[]");
        }

        if (root.TryGetProperty(ChaosSeaGuardianPoliticsState.LastInvalidCommandProperty, out _))
        {
            var reason = root.TryGetProperty(ChaosSeaGuardianPoliticsState.LastInvalidCommandReasonProperty, out var reasonNode)
                ? reasonNode.ToString()
                : "invalid command";
            AddChaosGuardianPoliticsIssue(
                issues,
                $"{contextPrefix}.{ChaosSeaGuardianPoliticsState.LastInvalidCommandProperty}",
                "chaos_guardian_politics_command_invalid_authority",
                "Команда политики Хранителей не была применена: форма или цель команды повреждена.",
                "valid Guardian politics command target and payload",
                reason);
        }

        ValidateChaosGuardianPoliticalRelations(relations, hasRelations, $"{contextPrefix}.{ChaosSeaGuardianPoliticsState.RelationsProperty}", knownGuardians, issues);
        ValidateChaosGuardianPoliticalRelations(relationUpdates, hasRelationUpdates, $"{contextPrefix}.{ChaosSeaGuardianPoliticsState.RelationUpdatesProperty}", knownGuardians, issues);
        ValidateChaosGuardianPoliticalProjects(projects, hasProjects, $"{contextPrefix}.{ChaosSeaGuardianPoliticsState.ProjectsProperty}", knownGuardians, issues);
        ValidateChaosGuardianPoliticalProjects(projectUpdates, hasProjectUpdates, $"{contextPrefix}.{ChaosSeaGuardianPoliticsState.ProjectUpdatesProperty}", knownGuardians, issues);
        ValidateChaosGuardianInfluenceZones(influenceZones, hasInfluence, $"{contextPrefix}.{ChaosSeaGuardianPoliticsState.InfluenceZonesProperty}", knownGuardians, issues);
        ValidateChaosGuardianInfluenceZones(influenceUpdates, hasInfluenceUpdates, $"{contextPrefix}.{ChaosSeaGuardianPoliticsState.InfluenceUpdatesProperty}", knownGuardians, issues);
        ValidateChaosGuardianPoliticalChronicle(chronicle, hasChronicle, $"{contextPrefix}.{ChaosSeaGuardianPoliticsState.ChronicleProperty}", knownGuardians, issues);
        ValidateChaosGuardianPoliticalChronicle(chronicleUpdates, hasChronicleUpdates, $"{contextPrefix}.{ChaosSeaGuardianPoliticsState.ChronicleUpdatesProperty}", knownGuardians, issues);
        ValidateChaosGuardianPoliticalCompletions(completions, hasCompletions, $"{contextPrefix}.{ChaosSeaGuardianPoliticsState.CompleteProjectsProperty}", issues);
        ValidateChaosGuardianOptionalArrays(root, contextPrefix, issues);
    }

    private void ValidateChaosGuardianPoliticalRelations(
        JsonElement node,
        bool hasNode,
        string context,
        HashSet<string> knownGuardians,
        List<ValidationIssue> issues)
    {
        foreach (var relation in EnumerateChaosGuardianPoliticsObjects(node, hasNode, context, issues))
        {
            var relationId = RequireChaosGuardianPoliticsString(relation, context, "relationId", "chaos_guardian_politics_relation_missing_id", issues);
            var sourceGuardianId = RequireChaosGuardianPoliticsString(relation, context, "sourceGuardianId", "chaos_guardian_politics_relation_missing_source", issues);
            var targetGuardianId = RequireChaosGuardianPoliticsString(relation, context, "targetGuardianId", "chaos_guardian_politics_relation_missing_target", issues);
            var relationType = RequireChaosGuardianPoliticsString(relation, context, "relationType", "chaos_guardian_politics_relation_missing_type", issues);
            var visibility = RequireChaosGuardianPoliticsString(relation, context, "visibility", "chaos_guardian_politics_missing_visibility", issues);
            RequireChaosGuardianPoliticsString(relation, context, "reason", "chaos_guardian_politics_relation_missing_reason", issues);
            ValidateChaosGuardianPoliticsIntegerField(relation, context, "lastChangedTurn", min: 0, max: int.MaxValue, "chaos_guardian_politics_invalid_turn", issues);
            ValidateChaosGuardianPoliticsIntegerField(relation, context, "attitudeScore", min: -100, max: 100, "chaos_guardian_politics_attitude_score_out_of_range", issues);
            ValidateChaosGuardianPoliticsStringArray(relation, context, "effects", required: true, issues);
            ValidateGuardianKnown(sourceGuardianId, $"{context}.sourceGuardianId", knownGuardians, issues);
            ValidateGuardianKnown(targetGuardianId, $"{context}.targetGuardianId", knownGuardians, issues);

            if (!string.IsNullOrWhiteSpace(relationId) &&
                sourceGuardianId != null &&
                targetGuardianId != null &&
                string.Equals(sourceGuardianId, targetGuardianId, StringComparison.OrdinalIgnoreCase))
            {
                AddChaosGuardianPoliticsIssue(
                    issues,
                    context,
                    "chaos_guardian_politics_self_relation_invalid",
                    "Связь политики Хранителей должна соединять двух разных субъектов.",
                    "sourceGuardianId != targetGuardianId",
                    relationId);
            }

            if (!string.IsNullOrWhiteSpace(relationType) && !ChaosSeaGuardianPoliticsState.RelationTypes.Contains(relationType))
            {
                AddChaosGuardianPoliticsIssue(
                    issues,
                    $"{context}.relationType",
                    "chaos_guardian_politics_invalid_relation_type",
                    "relationType политики Хранителей не поддерживается.",
                    string.Join(" | ", ChaosSeaGuardianPoliticsState.RelationTypes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                    relationType);
            }

            ValidateChaosGuardianVisibility(visibility, context, relation, issues);
        }
    }

    private void ValidateChaosGuardianPoliticalProjects(
        JsonElement node,
        bool hasNode,
        string context,
        HashSet<string> knownGuardians,
        List<ValidationIssue> issues)
    {
        foreach (var project in EnumerateChaosGuardianPoliticsObjects(node, hasNode, context, issues))
        {
            var ownerGuardianId = RequireChaosGuardianPoliticsString(project, context, "ownerGuardianId", "chaos_guardian_politics_project_missing_owner", issues);
            var targetGuardianId = RequireChaosGuardianPoliticsString(project, context, "targetGuardianId", "chaos_guardian_politics_project_missing_target", issues);
            var status = RequireChaosGuardianPoliticsString(project, context, "status", "chaos_guardian_politics_project_missing_status", issues);
            var visibility = RequireChaosGuardianPoliticsString(project, context, "visibility", "chaos_guardian_politics_missing_visibility", issues);
            RequireChaosGuardianPoliticsString(project, context, "projectId", "chaos_guardian_politics_project_missing_id", issues);
            RequireChaosGuardianPoliticsString(project, context, "projectType", "chaos_guardian_politics_project_missing_type", issues);
            RequireChaosGuardianPoliticsString(project, context, "summary", "chaos_guardian_politics_project_missing_summary", issues);
            ValidateGuardianKnown(ownerGuardianId, $"{context}.ownerGuardianId", knownGuardians, issues);
            ValidateGuardianKnown(targetGuardianId, $"{context}.targetGuardianId", knownGuardians, issues);
            ValidateChaosGuardianPoliticsIntegerField(project, context, "currentProgress", min: 0, max: int.MaxValue, "chaos_guardian_politics_project_progress_invalid", issues);
            ValidateChaosGuardianPoliticsIntegerField(project, context, "requiredProgress", min: 1, max: int.MaxValue, "chaos_guardian_politics_project_progress_invalid", issues);
            ValidateChaosGuardianPoliticsIntegerField(project, context, "lastUpdatedTurn", min: 0, max: int.MaxValue, "chaos_guardian_politics_invalid_turn", issues);
            ValidateChaosGuardianVisibility(visibility, context, project, issues);

            if (!string.IsNullOrWhiteSpace(status) && !ChaosSeaGuardianPoliticsState.ProjectStatuses.Contains(status))
            {
                AddChaosGuardianPoliticsIssue(
                    issues,
                    $"{context}.status",
                    "chaos_guardian_politics_project_invalid_status",
                    "status политического проекта Хранителей не поддерживается.",
                    string.Join(" | ", ChaosSeaGuardianPoliticsState.ProjectStatuses.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                    status);
            }

            if (TryGetInt(project, "currentProgress", out var currentProgress) &&
                TryGetInt(project, "requiredProgress", out var requiredProgress) &&
                currentProgress > requiredProgress)
            {
                AddChaosGuardianPoliticsIssue(
                    issues,
                    context,
                    "chaos_guardian_politics_project_progress_invalid",
                    "currentProgress политического проекта не должен превышать requiredProgress.",
                    "currentProgress <= requiredProgress",
                    $"{currentProgress}/{requiredProgress}");
            }
        }
    }

    private void ValidateChaosGuardianInfluenceZones(
        JsonElement node,
        bool hasNode,
        string context,
        HashSet<string> knownGuardians,
        List<ValidationIssue> issues)
    {
        foreach (var zone in EnumerateChaosGuardianPoliticsObjects(node, hasNode, context, issues))
        {
            var guardianId = RequireChaosGuardianPoliticsString(zone, context, "guardianId", "chaos_guardian_politics_zone_missing_guardian", issues);
            var visibility = RequireChaosGuardianPoliticsString(zone, context, "visibility", "chaos_guardian_politics_missing_visibility", issues);
            RequireChaosGuardianPoliticsString(zone, context, "zoneId", "chaos_guardian_politics_zone_missing_id", issues);
            RequireChaosGuardianPoliticsString(zone, context, "scopeType", "chaos_guardian_politics_zone_missing_scope_type", issues);
            RequireChaosGuardianPoliticsString(zone, context, "scopeId", "chaos_guardian_politics_zone_missing_scope_id", issues);
            RequireChaosGuardianPoliticsString(zone, context, "displayName", "chaos_guardian_politics_zone_missing_display_name", issues);
            ValidateGuardianKnown(guardianId, $"{context}.guardianId", knownGuardians, issues);
            ValidateChaosGuardianVisibility(visibility, context, zone, issues);
            ValidateChaosGuardianPoliticsIntegerField(zone, context, "influenceValue", min: 0, max: 100, "chaos_guardian_politics_influence_value_out_of_range", issues);
            ValidateChaosGuardianPoliticsIntegerField(zone, context, "controlLevel", min: 0, max: 100, "chaos_guardian_politics_control_level_out_of_range", issues);
            ValidateChaosGuardianPoliticsIntegerField(zone, context, "updatedAtTurn", min: 0, max: int.MaxValue, "chaos_guardian_politics_invalid_turn", issues);
        }
    }

    private void ValidateChaosGuardianPoliticalChronicle(
        JsonElement node,
        bool hasNode,
        string context,
        HashSet<string> knownGuardians,
        List<ValidationIssue> issues)
    {
        foreach (var entry in EnumerateChaosGuardianPoliticsObjects(node, hasNode, context, issues))
        {
            RequireChaosGuardianPoliticsString(entry, context, "entryId", "chaos_guardian_politics_chronicle_missing_id", issues);
            RequireChaosGuardianPoliticsString(entry, context, "eventType", "chaos_guardian_politics_chronicle_missing_event_type", issues);
            RequireChaosGuardianPoliticsString(entry, context, "summary", "chaos_guardian_politics_chronicle_missing_summary", issues);
            var visibility = RequireChaosGuardianPoliticsString(entry, context, "visibility", "chaos_guardian_politics_missing_visibility", issues);
            ValidateChaosGuardianPoliticsIntegerField(entry, context, "turnNumber", min: 0, max: int.MaxValue, "chaos_guardian_politics_invalid_turn", issues);
            ValidateChaosGuardianPoliticsStringArray(entry, context, "consequences", required: true, issues);
            ValidateChaosGuardianVisibility(visibility, context, entry, issues);

            if (!entry.TryGetProperty("guardianIds", out var guardianIds) || guardianIds.ValueKind != JsonValueKind.Array)
            {
                AddChaosGuardianPoliticsIssue(
                    issues,
                    $"{context}.guardianIds",
                    "chaos_guardian_politics_chronicle_guardians_invalid",
                    "chronicle entry политики Хранителей должен содержать guardianIds[].",
                    "array of guardian ids",
                    guardianIds.ValueKind.ToString());
                continue;
            }

            var index = 0;
            foreach (var guardianIdNode in guardianIds.EnumerateArray())
            {
                if (guardianIdNode.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(guardianIdNode.GetString()))
                {
                    AddChaosGuardianPoliticsIssue(
                        issues,
                        $"{context}.guardianIds[{index}]",
                        "chaos_guardian_politics_chronicle_guardians_invalid",
                        "guardianIds[] должен содержать непустые строки.",
                        "non-empty guardian id",
                        guardianIdNode.ValueKind.ToString());
                }
                else
                {
                    ValidateGuardianKnown(guardianIdNode.GetString(), $"{context}.guardianIds[{index}]", knownGuardians, issues);
                }

                index++;
            }
        }
    }

    private void ValidateChaosGuardianPoliticalCompletions(JsonElement node, bool hasNode, string context, List<ValidationIssue> issues)
    {
        foreach (var completion in EnumerateChaosGuardianPoliticsObjects(node, hasNode, context, issues))
        {
            RequireChaosGuardianPoliticsString(completion, context, "projectId", "chaos_guardian_politics_completion_missing_project", issues);
            RequireChaosGuardianPoliticsString(completion, context, "completionSummary", "chaos_guardian_politics_completion_missing_summary", issues);
            ValidateChaosGuardianPoliticsIntegerField(completion, context, "completedAtTurn", min: 0, max: int.MaxValue, "chaos_guardian_politics_invalid_turn", issues);
        }
    }

    private void ValidateChaosGuardianOptionalArrays(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        foreach (var propertyName in new[] { "sarefLinks", "openConflicts" })
        {
            if (!root.TryGetProperty(propertyName, out var array))
                continue;

            if (array.ValueKind != JsonValueKind.Array)
            {
                AddChaosGuardianPoliticsIssue(
                    issues,
                    $"{contextPrefix}.{propertyName}",
                    "chaos_guardian_politics_optional_array_invalid",
                    $"{propertyName} должен быть array.",
                    "array",
                    array.ValueKind.ToString());
                continue;
            }

            var index = 0;
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    AddChaosGuardianPoliticsIssue(
                        issues,
                        $"{contextPrefix}.{propertyName}[{index}]",
                        "chaos_guardian_politics_optional_array_item_invalid",
                        $"{propertyName} entries должны быть object.",
                        "object",
                        item.ValueKind.ToString());
                }

                index++;
            }
        }

        if (root.TryGetProperty("playerRole", out var playerRole) &&
            playerRole.ValueKind is not JsonValueKind.Object and not JsonValueKind.Null)
        {
            AddChaosGuardianPoliticsIssue(
                issues,
                $"{contextPrefix}.playerRole",
                "chaos_guardian_politics_player_role_invalid",
                "playerRole должен быть object или null.",
                "object or null",
                playerRole.ValueKind.ToString());
        }
    }

    private IEnumerable<JsonElement> EnumerateChaosGuardianPoliticsObjects(
        JsonElement node,
        bool hasNode,
        string context,
        List<ValidationIssue> issues)
    {
        if (!hasNode)
            yield break;

        if (node.ValueKind == JsonValueKind.Object)
        {
            yield return node;
            yield break;
        }

        if (node.ValueKind != JsonValueKind.Array)
        {
            AddChaosGuardianPoliticsIssue(
                issues,
                context,
                "chaos_guardian_politics_array_invalid",
                "Политический surface Хранителей должен быть array или single object.",
                "array or object",
                node.ValueKind.ToString());
            yield break;
        }

        var index = 0;
        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                yield return item;
            }
            else
            {
                AddChaosGuardianPoliticsIssue(
                    issues,
                    $"{context}[{index}]",
                    "chaos_guardian_politics_entry_not_object",
                    "Элемент политики Хранителей должен быть object.",
                    "object",
                    item.ValueKind.ToString());
            }

            index++;
        }
    }

    private static string? RequireChaosGuardianPoliticsString(
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
            AddChaosGuardianPoliticsIssue(
                issues,
                $"{context}.{propertyName}",
                code,
                $"Политика Хранителей должна содержать непустой {propertyName}.",
                $"non-empty {propertyName}",
                property.ValueKind.ToString());
            return null;
        }

        return property.GetString()?.Trim();
    }

    private static void ValidateChaosGuardianPoliticsIntegerField(
        JsonElement entry,
        string context,
        string propertyName,
        int min,
        int max,
        string code,
        List<ValidationIssue> issues)
    {
        if (!TryGetInt(entry, propertyName, out var value) || value < min || value > max)
        {
            var actual = entry.TryGetProperty(propertyName, out var property)
                ? property.ToString()
                : "missing";
            AddChaosGuardianPoliticsIssue(
                issues,
                $"{context}.{propertyName}",
                code,
                $"{propertyName} политики Хранителей вне допустимого диапазона.",
                $"{min}..{max}",
                actual);
        }
    }

    private static void ValidateChaosGuardianPoliticsStringArray(
        JsonElement entry,
        string context,
        string propertyName,
        bool required,
        List<ValidationIssue> issues)
    {
        if (!entry.TryGetProperty(propertyName, out var array))
        {
            if (required)
            {
                AddChaosGuardianPoliticsIssue(
                    issues,
                    $"{context}.{propertyName}",
                    "chaos_guardian_politics_string_array_missing",
                    $"{propertyName} должен быть array строк.",
                    "array of non-empty strings");
            }

            return;
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            AddChaosGuardianPoliticsIssue(
                issues,
                $"{context}.{propertyName}",
                "chaos_guardian_politics_string_array_invalid",
                $"{propertyName} должен быть array строк.",
                "array",
                array.ValueKind.ToString());
            return;
        }

        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                AddChaosGuardianPoliticsIssue(
                    issues,
                    $"{context}.{propertyName}[{index}]",
                    "chaos_guardian_politics_string_array_invalid",
                    $"{propertyName} entries должны быть непустыми строками.",
                    "non-empty string",
                    item.ValueKind.ToString());
            }

            index++;
        }
    }

    private static void ValidateChaosGuardianVisibility(
        string? visibility,
        string context,
        JsonElement entry,
        List<ValidationIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(visibility) && !ChaosSeaGuardianPoliticsState.Visibilities.Contains(visibility))
        {
            AddChaosGuardianPoliticsIssue(
                issues,
                $"{context}.visibility",
                "chaos_guardian_politics_invalid_visibility",
                "visibility политики Хранителей не поддерживается.",
                string.Join(" | ", ChaosSeaGuardianPoliticsState.Visibilities.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                visibility);
        }

        if (string.Equals(visibility, "hidden", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(visibility, "gm_only", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var leakFlag in new[] { "isPlayerVisible", "playerVisible" })
            {
                if (entry.TryGetProperty(leakFlag, out var flag) && flag.ValueKind == JsonValueKind.True)
                {
                    AddChaosGuardianPoliticsIssue(
                        issues,
                        $"{context}.{leakFlag}",
                        "chaos_guardian_politics_hidden_relation_player_visible",
                        "Hidden/gm_only политика Хранителей не должна быть помечена как видимая игроку.",
                        "false or omitted",
                        "true");
                }
            }
        }
    }

    private void ValidateGuardianKnown(string? guardianId, string context, HashSet<string> knownGuardians, List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(guardianId) ||
            knownGuardians.Contains(guardianId) ||
            IsSystemGuardianId(guardianId))
        {
            return;
        }

        AddChaosGuardianPoliticsIssue(
            issues,
            context,
            "chaos_guardian_politics_unknown_guardian",
            "Политика Хранителей ссылается на неизвестного Хранителя.",
            "guardianId from guardians.json or explicit system_* guardian id",
            guardianId);
    }

    private HashSet<string> LoadKnownChaosGuardianIds()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var json = _fs.ReadFileAsync("game_state/meta/guardians.json").GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return result;

            AddGuardianIdsFromObject(doc.RootElement, result);
            if (doc.RootElement.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array)
            {
                foreach (var guardian in guardians.EnumerateArray())
                    AddGuardianIdsFromObject(guardian, result);
            }

            if (doc.RootElement.TryGetProperty("chaosSeaNavigation", out var navigation) &&
                navigation.ValueKind == JsonValueKind.Object &&
                navigation.TryGetProperty("knownAbodes", out var knownAbodes) &&
                knownAbodes.ValueKind == JsonValueKind.Array)
            {
                foreach (var abode in knownAbodes.EnumerateArray())
                    AddGuardianIdsFromObject(abode, result);
            }
        }
        catch
        {
            return result;
        }

        return result;
    }

    private static void AddGuardianIdsFromObject(JsonElement node, HashSet<string> result)
    {
        if (node.ValueKind != JsonValueKind.Object)
            return;

        foreach (var propertyName in new[] { "guardianId", "id", "activeGuardianId" })
        {
            if (node.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(property.GetString()))
            {
                result.Add(property.GetString()!.Trim());
            }
        }

        if (node.TryGetProperty("activeGuardian", out var activeGuardian))
            AddGuardianIdsFromObject(activeGuardian, result);
    }

    private static bool IsSystemGuardianId(string guardianId) =>
        guardianId.StartsWith("system_", StringComparison.OrdinalIgnoreCase) ||
        guardianId.StartsWith("system_guardian_", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetInt(JsonElement entry, string propertyName, out int value)
    {
        value = 0;
        return entry.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out value);
    }

    private static void AddChaosGuardianPoliticsIssue(
        List<ValidationIssue> issues,
        string context,
        string code,
        string message,
        string expected,
        string? actual = null)
    {
        issues.Add(new ValidationIssue(
            context,
            IssueSeverity.Error,
            message,
            code: code,
            section: "ChaosSeaGuardianPolitics",
            expected: expected,
            actual: actual,
            repairHint: "Исправь game_state/meta/chaos_sea_guardian_politics.json или используй documented guardianPolitical*Updates command surfaces."));
    }
}
