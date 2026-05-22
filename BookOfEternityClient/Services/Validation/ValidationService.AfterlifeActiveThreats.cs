using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private void ValidateAfterlifeActiveThreatStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                AfterlifeActiveThreatState.StatePath,
                IssueSeverity.Error,
                "afterlife_active_threats.json должен быть JSON object.",
                code: "afterlife_threat_invalid_root",
                section: "AfterlifeActiveThreats",
                expected: "object with threats[] or afterlife threat command surfaces",
                actual: root.ValueKind.ToString()));
            return;
        }

        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "schemaVersion", "AfterlifeActiveThreats");

        var hasThreats = root.TryGetProperty(AfterlifeActiveThreatState.ThreatsProperty, out var threats);
        var hasAdds = root.TryGetProperty(AfterlifeActiveThreatState.AddsProperty, out var adds);
        var hasUpdates = root.TryGetProperty(AfterlifeActiveThreatState.UpdatesProperty, out var updates);
        var hasCompletions = root.TryGetProperty(AfterlifeActiveThreatState.CompleteActivitiesProperty, out var completions);
        var hasRemovals = root.TryGetProperty(AfterlifeActiveThreatState.RemovalsProperty, out var removals);
        var hasInvalidCommand = root.TryGetProperty(AfterlifeActiveThreatState.LastInvalidCommandProperty, out _);

        if (!hasThreats && !hasAdds && !hasUpdates && !hasCompletions && !hasRemovals)
        {
            issues.Add(new ValidationIssue(
                contextPrefix,
                IssueSeverity.Error,
                "afterlife_active_threats.json должен содержать threats[] или documented afterlife threat command surface.",
                code: "afterlife_threat_missing_surface",
                section: "AfterlifeActiveThreats",
                expected: "threats[], afterlifeThreatsToAdd[], afterlifeThreatsToUpdate[], completeAfterlifeThreatActivities[], or afterlifeThreatsToRemove[]"));
        }

        if (hasInvalidCommand)
        {
            var reason = root.TryGetProperty(AfterlifeActiveThreatState.LastInvalidCommandReasonProperty, out var reasonNode)
                ? reasonNode.ToString()
                : "invalid command";
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{AfterlifeActiveThreatState.LastInvalidCommandProperty}",
                IssueSeverity.Error,
                "Команда afterlife active threats не была применена: форма или цель команды повреждена.",
                code: "afterlife_threat_command_invalid_authority",
                section: "AfterlifeActiveThreats",
                expected: "valid afterlife threat command target and payload",
                actual: reason));
        }

        ValidateAfterlifeThreatArrayIfPresent(threats, hasThreats, $"{contextPrefix}.{AfterlifeActiveThreatState.ThreatsProperty}", isAdd: false, issues);
        ValidateAfterlifeThreatArrayIfPresent(adds, hasAdds, $"{contextPrefix}.{AfterlifeActiveThreatState.AddsProperty}", isAdd: true, issues);
        ValidateAfterlifeThreatUpdates(updates, hasUpdates, $"{contextPrefix}.{AfterlifeActiveThreatState.UpdatesProperty}", issues);
        ValidateAfterlifeThreatCompletions(completions, hasCompletions, $"{contextPrefix}.{AfterlifeActiveThreatState.CompleteActivitiesProperty}", issues);
        ValidateAfterlifeThreatRemovals(removals, hasRemovals, $"{contextPrefix}.{AfterlifeActiveThreatState.RemovalsProperty}", issues);
    }

    private async Task ValidateAfterlifeActiveThreatPreTurnContinuityAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(AfterlifeActiveThreatState.StatePath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        var currentJson = await _fs.ReadFileAsync(AfterlifeActiveThreatState.StatePath);
        if (string.IsNullOrWhiteSpace(currentJson))
            return;

        using var preTurnDocument = TryParseJsonDocument(preTurnJson);
        using var currentDocument = TryParseJsonDocument(currentJson);
        if (preTurnDocument == null || currentDocument == null)
            return;

        if (preTurnDocument.RootElement.ValueKind != JsonValueKind.Object ||
            currentDocument.RootElement.ValueKind != JsonValueKind.Object ||
            !preTurnDocument.RootElement.TryGetProperty(AfterlifeActiveThreatState.ThreatsProperty, out var preThreats) ||
            preThreats.ValueKind != JsonValueKind.Array ||
            !currentDocument.RootElement.TryGetProperty(AfterlifeActiveThreatState.ThreatsProperty, out var currentThreats) ||
            currentThreats.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var preThreat in preThreats.EnumerateArray())
        {
            if (preThreat.ValueKind != JsonValueKind.Object ||
                !preThreat.TryGetProperty("currentActivity", out var preActivity) ||
                preActivity.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var threatId = GetAfterlifeThreatString(preThreat, "threatId");
            if (string.IsNullOrWhiteSpace(threatId))
                continue;

            var currentThreat = FindAfterlifeThreat(currentThreats, threatId);
            if (currentThreat == null ||
                !currentThreat.Value.TryGetProperty("currentActivity", out var currentActivity) ||
                currentActivity.ValueKind != JsonValueKind.Null)
            {
                continue;
            }

            if (HasAfterlifeThreatCompletionLedger(currentThreat.Value, preActivity))
                continue;

            issues.Add(new ValidationIssue(
                $"{AfterlifeActiveThreatState.StatePath}.{AfterlifeActiveThreatState.ThreatsProperty}",
                IssueSeverity.Error,
                "Активность afterlife threat была удалена без terminal proof в ledger[].",
                code: "afterlife_threat_activity_removed_without_completion",
                section: "AfterlifeActiveThreats",
                expected: "completeAfterlifeThreatActivities projection with matching ledger entry before currentActivity is cleared",
                actual: threatId,
                repairHint: "Завершай currentActivity угрозы через completeAfterlifeThreatActivities; normalizer перенесет активность в ledger[] и только затем очистит currentActivity."));
        }
    }

    private void ValidateAfterlifeThreatArrayIfPresent(
        JsonElement node,
        bool hasNode,
        string context,
        bool isAdd,
        List<ValidationIssue> issues)
    {
        if (!hasNode)
            return;

        if (node.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                isAdd ? "afterlifeThreatsToAdd должен быть array." : "threats должен быть array.",
                code: isAdd ? "afterlife_threat_adds_not_array" : "afterlife_threats_not_array",
                section: "AfterlifeActiveThreats",
                expected: "array",
                actual: node.ValueKind.ToString()));
            return;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var threat in node.EnumerateArray())
            ValidateAfterlifeThreatObject(threat, $"{context}[{index++}]", ids, issues);
    }

    private void ValidateAfterlifeThreatObject(
        JsonElement threat,
        string context,
        HashSet<string> ids,
        List<ValidationIssue> issues)
    {
        if (!RequireObject(threat, context, issues))
            return;

        var threatId = RequireAfterlifeThreatString(threat, context, "threatId", "afterlife_threat_missing_id", issues);
        if (!string.IsNullOrWhiteSpace(threatId) && !ids.Add(threatId))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Дубликат threatId в afterlife active threats.",
                code: "afterlife_threat_duplicate_id",
                section: "AfterlifeActiveThreats",
                expected: "unique threatId",
                actual: threatId));
        }

        var realm = RequireAfterlifeThreatString(threat, context, "realm", "afterlife_threat_missing_realm", issues);
        if (!string.IsNullOrWhiteSpace(realm) && !AfterlifeActiveThreatState.Realms.Contains(realm))
        {
            issues.Add(new ValidationIssue(
                $"{context}.realm",
                IssueSeverity.Error,
                "realm не поддерживается для afterlife active threat.",
                code: "afterlife_threat_invalid_realm",
                section: "AfterlifeActiveThreats",
                expected: string.Join("/", AfterlifeActiveThreatState.Realms.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                actual: realm));
        }

        RequireAfterlifeThreatString(threat, context, "scopeId", "afterlife_threat_missing_scope_id", issues);
        RequireAfterlifeThreatString(threat, context, "displayName", "afterlife_threat_missing_display_name", issues);
        ValidateNonNegativeIntegerField(threat, context, issues, "intensity", "AfterlifeActiveThreats");
        ValidateAfterlifeThreatArchetype(threat, context, issues);
        ValidateAfterlifeThreatCurrentActivity(threat, context, issues);
        ValidateAfterlifeThreatImpactProfile(threat, context, issues);
        ValidateAfterlifeThreatVisibleFlag(threat, context, issues);
        ValidateOptionalNullableStringField(threat, context, issues, "linkedFactionId");
        ValidateOptionalNullableStringField(threat, context, issues, "linkedGuardianId");
        ValidateAfterlifeThreatSarefLink(threat, context, issues);
        ValidateAfterlifeThreatLedger(threat, context, issues);
    }

    private void ValidateAfterlifeThreatArchetype(JsonElement threat, string context, List<ValidationIssue> issues)
    {
        if (!threat.TryGetProperty("threatArchetype", out var archetype) ||
            !RequireObject(archetype, $"{context}.threatArchetype", issues))
        {
            issues.Add(new ValidationIssue(
                $"{context}.threatArchetype",
                IssueSeverity.Error,
                "Afterlife threat должен содержать threatArchetype object.",
                code: "afterlife_threat_missing_archetype",
                section: "AfterlifeActiveThreats",
                expected: "threatArchetype object with motivation/method"));
            return;
        }

        var motivation = RequireAfterlifeThreatString(archetype, $"{context}.threatArchetype", "motivation", "afterlife_threat_missing_motivation", issues);
        var method = RequireAfterlifeThreatString(archetype, $"{context}.threatArchetype", "method", "afterlife_threat_missing_method", issues);
        if (!string.IsNullOrWhiteSpace(motivation) && !AllowedThreatMotivations.Contains(motivation))
        {
            issues.Add(new ValidationIssue(
                $"{context}.threatArchetype.motivation",
                IssueSeverity.Error,
                "threatArchetype.motivation должен быть одним из canonical значений Active Threat.",
                code: "afterlife_threat_invalid_motivation",
                section: "AfterlifeActiveThreats",
                expected: string.Join(" | ", AllowedThreatMotivations),
                actual: motivation));
        }

        if (!string.IsNullOrWhiteSpace(method) && !AllowedThreatMethods.Contains(method))
        {
            issues.Add(new ValidationIssue(
                $"{context}.threatArchetype.method",
                IssueSeverity.Error,
                "threatArchetype.method должен быть одним из canonical значений Active Threat.",
                code: "afterlife_threat_invalid_method",
                section: "AfterlifeActiveThreats",
                expected: string.Join(" | ", AllowedThreatMethods),
                actual: method));
        }

        if (string.Equals(motivation, "Custom", StringComparison.OrdinalIgnoreCase))
            RequireAfterlifeThreatString(archetype, $"{context}.threatArchetype", "customMotivation", "afterlife_threat_missing_custom_motivation", issues);
        else if (archetype.TryGetProperty("customMotivation", out _))
            ValidateOptionalNullableStringField(archetype, $"{context}.threatArchetype", issues, "customMotivation");

        if (string.Equals(method, "Custom", StringComparison.OrdinalIgnoreCase))
            RequireAfterlifeThreatString(archetype, $"{context}.threatArchetype", "customMethod", "afterlife_threat_missing_custom_method", issues);
        else if (archetype.TryGetProperty("customMethod", out _))
            ValidateOptionalNullableStringField(archetype, $"{context}.threatArchetype", issues, "customMethod");
    }

    private void ValidateAfterlifeThreatCurrentActivity(JsonElement threat, string context, List<ValidationIssue> issues)
    {
        if (!threat.TryGetProperty("currentActivity", out var currentActivity))
        {
            issues.Add(new ValidationIssue(
                $"{context}.currentActivity",
                IssueSeverity.Error,
                "Afterlife threat должен содержать currentActivity object или null.",
                code: "afterlife_threat_missing_current_activity",
                section: "AfterlifeActiveThreats",
                expected: "currentActivity object or null"));
            return;
        }

        if (currentActivity.ValueKind == JsonValueKind.Null)
            return;

        ValidateNpcCurrentActivityObject(currentActivity, $"{context}.currentActivity", issues);
        var activeState = GetAfterlifeThreatString(currentActivity, "activeState");
        if (!string.IsNullOrWhiteSpace(activeState) && AfterlifeActiveThreatState.TerminalActivityStates.Contains(activeState))
        {
            issues.Add(new ValidationIssue(
                $"{context}.currentActivity.activeState",
                IssueSeverity.Error,
                "Canonical currentActivity угрозы не должен быть terminal; завершённую активность перенеси в ledger[].",
                code: "afterlife_threat_terminal_current_activity_forbidden",
                section: "AfterlifeActiveThreats",
                expected: "non-terminal active currentActivity or null",
                actual: activeState));
        }
    }

    private void ValidateAfterlifeThreatImpactProfile(JsonElement threat, string context, List<ValidationIssue> issues)
    {
        if (!threat.TryGetProperty("impactProfile", out var impact) ||
            !RequireObject(impact, $"{context}.impactProfile", issues))
        {
            issues.Add(new ValidationIssue(
                $"{context}.impactProfile",
                IssueSeverity.Error,
                "Afterlife threat должен содержать impactProfile object.",
                code: "afterlife_threat_missing_impact_profile",
                section: "AfterlifeActiveThreats",
                expected: "impactProfile object"));
            return;
        }

        var primaryTargetType = RequireAfterlifeThreatString(impact, $"{context}.impactProfile", "primaryTargetType", "afterlife_threat_missing_primary_target_type", issues);
        if (!string.IsNullOrWhiteSpace(primaryTargetType) && !AfterlifeActiveThreatState.ImpactTargetTypes.Contains(primaryTargetType))
        {
            issues.Add(new ValidationIssue(
                $"{context}.impactProfile.primaryTargetType",
                IssueSeverity.Error,
                "impactProfile.primaryTargetType не поддерживается для afterlife threat.",
                code: "afterlife_threat_invalid_primary_target_type",
                section: "AfterlifeActiveThreats",
                expected: string.Join(" | ", AfterlifeActiveThreatState.ImpactTargetTypes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                actual: primaryTargetType));
        }

        ValidateRequiredNullableStringField(impact, $"{context}.impactProfile", issues, "primaryTargetId");
        RequireAfterlifeThreatString(impact, $"{context}.impactProfile", "primaryTargetName", "afterlife_threat_missing_primary_target_name", issues);
        var primaryImpact = RequireAfterlifeThreatString(impact, $"{context}.impactProfile", "primaryImpact", "afterlife_threat_missing_primary_impact", issues);
        if (!string.IsNullOrWhiteSpace(primaryImpact) && !AfterlifeActiveThreatState.ImpactTypes.Contains(primaryImpact))
        {
            issues.Add(new ValidationIssue(
                $"{context}.impactProfile.primaryImpact",
                IssueSeverity.Error,
                "impactProfile.primaryImpact не поддерживается для afterlife threat.",
                code: "afterlife_threat_invalid_primary_impact",
                section: "AfterlifeActiveThreats",
                expected: string.Join(" | ", AfterlifeActiveThreatState.ImpactTypes.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                actual: primaryImpact));
        }

        ValidateIntegerField(impact, $"{context}.impactProfile", issues, "baseImpactValue");
    }

    private void ValidateAfterlifeThreatVisibleFlag(JsonElement threat, string context, List<ValidationIssue> issues)
    {
        if (!threat.TryGetProperty("visibleToPlayer", out var visible) || visible.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            issues.Add(new ValidationIssue(
                $"{context}.visibleToPlayer",
                IssueSeverity.Error,
                "Afterlife threat должен явно указывать visibleToPlayer boolean.",
                code: "afterlife_threat_missing_visible_to_player",
                section: "AfterlifeActiveThreats",
                expected: "boolean visibleToPlayer",
                actual: visible.ValueKind.ToString()));
        }
    }

    private void ValidateAfterlifeThreatSarefLink(JsonElement threat, string context, List<ValidationIssue> issues)
    {
        if (!threat.TryGetProperty("sarefLink", out var link) || link.ValueKind == JsonValueKind.Null)
            return;

        if (!RequireObject(link, $"{context}.sarefLink", issues))
            return;

        ValidateOptionalNullableStringField(link, $"{context}.sarefLink", issues, "role");
        ValidateOptionalNullableStringField(link, $"{context}.sarefLink", issues, "evidenceLevel");
        ValidateOptionalNullableStringField(link, $"{context}.sarefLink", issues, "notes");
    }

    private void ValidateAfterlifeThreatLedger(JsonElement threat, string context, List<ValidationIssue> issues)
    {
        if (!threat.TryGetProperty("ledger", out var ledger))
        {
            issues.Add(new ValidationIssue(
                $"{context}.ledger",
                IssueSeverity.Error,
                "Afterlife threat должен содержать ledger[].",
                code: "afterlife_threat_missing_ledger",
                section: "AfterlifeActiveThreats",
                expected: "ledger array"));
            return;
        }

        if (ledger.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{context}.ledger",
                IssueSeverity.Error,
                "ledger должен быть array.",
                code: "afterlife_threat_ledger_not_array",
                section: "AfterlifeActiveThreats",
                expected: "array",
                actual: ledger.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var entry in ledger.EnumerateArray())
        {
            var entryContext = $"{context}.ledger[{index++}]";
            if (!RequireObject(entry, entryContext, issues))
                continue;

            if (entry.TryGetProperty("turnNumber", out _))
                ValidateNonNegativeIntegerField(entry, entryContext, issues, "turnNumber", "AfterlifeActiveThreats");
            if (entry.TryGetProperty("completedAtTurn", out _))
                ValidateNonNegativeIntegerField(entry, entryContext, issues, "completedAtTurn", "AfterlifeActiveThreats");

            var hasSummary = !string.IsNullOrWhiteSpace(GetAfterlifeThreatString(entry, "summary"));
            var hasCompletionSummary = !string.IsNullOrWhiteSpace(GetAfterlifeThreatString(entry, "completionSummary"));
            if (!hasSummary && !hasCompletionSummary)
            {
                issues.Add(new ValidationIssue(
                    entryContext,
                    IssueSeverity.Error,
                    "ledger entry должен содержать summary или completionSummary.",
                    code: "afterlife_threat_ledger_missing_summary",
                    section: "AfterlifeActiveThreats",
                    expected: "summary or completionSummary"));
            }
        }
    }

    private void ValidateAfterlifeThreatUpdates(JsonElement node, bool hasNode, string context, List<ValidationIssue> issues)
    {
        if (!hasNode)
            return;

        if (!TryGetAfterlifeThreatCommandArray(node, context, "afterlife_threat_updates_not_array", issues, out var array))
            return;

        var index = 0;
        foreach (var update in array.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(update, itemContext, issues))
                continue;

            RequireAfterlifeThreatString(update, itemContext, "threatId", "afterlife_threat_update_missing_id", issues);
            if (update.TryGetProperty("currentActivity", out var currentActivity))
            {
                if (currentActivity.ValueKind == JsonValueKind.Null)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.currentActivity",
                        IssueSeverity.Error,
                        "afterlifeThreatsToUpdate не должен обнулять currentActivity через null.",
                        code: "afterlife_threat_update_null_current_activity_forbidden",
                        section: "AfterlifeActiveThreats",
                        expected: "non-null partial currentActivity update or completeAfterlifeThreatActivities command",
                        actual: "null"));
                }
                else if (currentActivity.ValueKind == JsonValueKind.Object)
                {
                    if (!currentActivity.EnumerateObject().Any())
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.currentActivity",
                            IssueSeverity.Error,
                            "afterlifeThreatsToUpdate.currentActivity не должен быть пустым.",
                            code: "afterlife_threat_update_empty_current_activity",
                            section: "AfterlifeActiveThreats",
                            expected: "at least one changed currentActivity field"));
                    }

                    var activeState = GetAfterlifeThreatString(currentActivity, "activeState");
                    if (!string.IsNullOrWhiteSpace(activeState) && AfterlifeActiveThreatState.TerminalActivityStates.Contains(activeState))
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.currentActivity.activeState",
                            IssueSeverity.Error,
                            "afterlifeThreatsToUpdate не должен завершать активность через currentActivity.activeState.",
                            code: "afterlife_threat_update_terminal_activity_state_forbidden",
                            section: "AfterlifeActiveThreats",
                            expected: "non-terminal currentActivity patch; terminal completion belongs to completeAfterlifeThreatActivities",
                            actual: activeState));
                    }
                }
                else
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.currentActivity",
                        IssueSeverity.Error,
                        "afterlifeThreatsToUpdate.currentActivity должен быть object.",
                        code: "afterlife_threat_update_current_activity_not_object",
                        section: "AfterlifeActiveThreats",
                        expected: "object",
                        actual: currentActivity.ValueKind.ToString()));
                }
            }

            if (update.TryGetProperty("ledgerEntry", out var ledgerEntry) && !RequireObject(ledgerEntry, $"{itemContext}.ledgerEntry", issues))
                continue;
        }
    }

    private void ValidateAfterlifeThreatCompletions(JsonElement node, bool hasNode, string context, List<ValidationIssue> issues)
    {
        if (!hasNode)
            return;

        if (!TryGetAfterlifeThreatCommandArray(node, context, "afterlife_threat_completions_not_array", issues, out var array))
            return;

        var index = 0;
        foreach (var completion in array.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(completion, itemContext, issues))
                continue;

            RequireAfterlifeThreatString(completion, itemContext, "threatId", "afterlife_threat_completion_missing_id", issues);
            if (completion.TryGetProperty("activityId", out _))
                ValidateOptionalNullableStringField(completion, itemContext, issues, "activityId");
            var finalState = RequireAfterlifeThreatString(completion, itemContext, "finalState", "afterlife_threat_completion_missing_final_state", issues);
            if (!string.IsNullOrWhiteSpace(finalState) && !AfterlifeActiveThreatState.TerminalActivityStates.Contains(finalState))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.finalState",
                    IssueSeverity.Error,
                    "completeAfterlifeThreatActivities.finalState должен быть terminal.",
                    code: "afterlife_threat_completion_invalid_final_state",
                    section: "AfterlifeActiveThreats",
                    expected: string.Join(" | ", AfterlifeActiveThreatState.TerminalActivityStates.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                    actual: finalState));
            }

            RequireAfterlifeThreatString(completion, itemContext, "completionSummary", "afterlife_threat_completion_missing_summary", issues);
            ValidateNonNegativeIntegerField(completion, itemContext, issues, "completedAtTurn", "AfterlifeActiveThreats");
        }
    }

    private void ValidateAfterlifeThreatRemovals(JsonElement node, bool hasNode, string context, List<ValidationIssue> issues)
    {
        if (!hasNode)
            return;

        if (!TryGetAfterlifeThreatCommandArray(node, context, "afterlife_threat_removals_not_array", issues, out var array))
            return;

        var index = 0;
        foreach (var removal in array.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(removal, itemContext, issues))
                continue;

            RequireAfterlifeThreatString(removal, itemContext, "threatId", "afterlife_threat_removal_missing_id", issues);
            if (removal.TryGetProperty("removalReason", out _))
                ValidateOptionalNullableStringField(removal, itemContext, issues, "removalReason");
            if (removal.TryGetProperty("removedAtTurn", out _))
                ValidateNonNegativeIntegerField(removal, itemContext, issues, "removedAtTurn", "AfterlifeActiveThreats");
        }
    }

    private bool TryGetAfterlifeThreatCommandArray(
        JsonElement node,
        string context,
        string code,
        List<ValidationIssue> issues,
        out JsonElement array)
    {
        array = node;
        if (node.ValueKind == JsonValueKind.Array)
            return true;

        issues.Add(new ValidationIssue(
            context,
            IssueSeverity.Error,
            "Afterlife threat command surface должен быть array.",
            code: code,
            section: "AfterlifeActiveThreats",
            expected: "array",
            actual: node.ValueKind.ToString()));
        return false;
    }

    private static string? RequireAfterlifeThreatString(
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
                $"Afterlife threat должен содержать непустой {propertyName}.",
                code: code,
                section: "AfterlifeActiveThreats",
                expected: $"non-empty {propertyName}"));
            return null;
        }

        return property.GetString();
    }

    private static string? GetAfterlifeThreatString(JsonElement entry, string propertyName)
    {
        return entry.ValueKind == JsonValueKind.Object &&
               entry.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static JsonElement? FindAfterlifeThreat(JsonElement threats, string threatId)
    {
        foreach (var threat in threats.EnumerateArray())
        {
            if (threat.ValueKind == JsonValueKind.Object &&
                string.Equals(GetAfterlifeThreatString(threat, "threatId"), threatId, StringComparison.OrdinalIgnoreCase))
            {
                return threat;
            }
        }

        return null;
    }

    private static bool HasAfterlifeThreatCompletionLedger(JsonElement threat, JsonElement preActivity)
    {
        if (!threat.TryGetProperty("ledger", out var ledger) || ledger.ValueKind != JsonValueKind.Array)
            return false;

        var preActivityId = GetAfterlifeThreatString(preActivity, "activityId");
        foreach (var entry in ledger.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            var finalState = GetAfterlifeThreatString(entry, "finalState");
            var hasTerminalState = !string.IsNullOrWhiteSpace(finalState) &&
                                   AfterlifeActiveThreatState.TerminalActivityStates.Contains(finalState);
            if (!hasTerminalState)
                continue;

            if (string.IsNullOrWhiteSpace(preActivityId))
                return true;

            var ledgerActivityId = GetAfterlifeThreatString(entry, "activityId");
            if (string.Equals(ledgerActivityId, preActivityId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static JsonDocument? TryParseJsonDocument(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch
        {
            return null;
        }
    }
}
