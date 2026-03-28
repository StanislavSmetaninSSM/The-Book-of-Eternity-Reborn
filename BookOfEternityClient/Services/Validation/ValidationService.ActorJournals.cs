using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private void ValidateNpcInteractionJournalStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        ValidateActorJournalStateFile(root, contextPrefix, issues, "npcId", "NpcInteractionJournal", NpcInteractionJournalState.UpdateProperty);
    }

    private void ValidateGuardianThoughtJournalStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        ValidateActorJournalStateFile(root, contextPrefix, issues, "guardianId", "GuardianThoughtJournal", GuardianThoughtJournalState.UpdateProperty);
    }

    private void ValidateGuardianSocialJournalStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        ValidateActorJournalStateFile(root, contextPrefix, issues, "guardianId", "GuardianSocialJournal", GuardianSocialJournalState.UpdateProperty);
    }

    private void ValidateActorJournalStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string actorIdField, string section, string updateProperty)
    {
        if (!root.TryGetProperty(ActorJournalState.EntriesProperty, out var entries))
            entries = default;

        if (entries.ValueKind != JsonValueKind.Undefined)
            ValidateActorJournalEntriesArray(entries, $"{contextPrefix}.{ActorJournalState.EntriesProperty}", issues, actorIdField, section);

        if (root.TryGetProperty(updateProperty, out var updates))
            ValidateActorJournalEntriesArray(updates, $"{contextPrefix}.{updateProperty}", issues, actorIdField, section);
    }

    private void ValidateActorJournalEntriesArray(JsonElement entries, string contextPrefix, List<ValidationIssue> issues, string actorIdField, string section)
    {
        RequireArrayOfObjects(entries, contextPrefix, issues);
        if (entries.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            var entryContext = $"{contextPrefix}[{index++}]";
            if (!RequireObject(entry, entryContext, issues))
                continue;

            ValidateActorJournalEntry(entry, entryContext, issues, actorIdField, section);
        }
    }

    private void ValidateActorJournalEntry(JsonElement entry, string contextPrefix, List<ValidationIssue> issues, string actorIdField, string section)
    {
        RequireString(entry, contextPrefix, issues, "entryId");
        RequireString(entry, contextPrefix, issues, actorIdField);
        RequireString(entry, contextPrefix, issues, "title");
        RequireString(entry, contextPrefix, issues, "summary");
        ValidateOptionalString(entry, contextPrefix, issues, "eventType");
        ValidateOptionalString(entry, contextPrefix, issues, "consequence");
        ValidateOptionalString(entry, contextPrefix, issues, "attitude");
        ValidateOptionalString(entry, contextPrefix, issues, "intent");
        if (entry.TryGetProperty("tags", out var tags))
            RequireArrayOfStrings(tags, $"{contextPrefix}.tags", issues);
        ValidateNonNegativeIntegerField(entry, contextPrefix, issues, "turn", section);
        ValidateRequiredIsoTimestampField(
            entry,
            contextPrefix,
            issues,
            "timestamp",
            section,
            $"{section.ToLowerInvariant()}_missing_timestamp",
            $"{section.ToLowerInvariant()}_invalid_timestamp",
            $"{section} entry должен содержать timestamp в ISO 8601 формате.");

        foreach (var propertyName in new[] { "relatedQuestId", "relatedResidentId", "relatedRelicId", "relatedNpcId", "relatedGuardianId" })
        {
            if (entry.TryGetProperty(propertyName, out _))
                ValidateOptionalString(entry, contextPrefix, issues, propertyName);
        }

        if (entry.TryGetProperty("bondLevelAfter", out _))
            ValidateNonNegativeIntegerField(entry, contextPrefix, issues, "bondLevelAfter", section);

        ValidateActorJournalClosureMetadata(entry, contextPrefix, issues, actorIdField, section);
    }

    private void ValidateActorJournalClosureMetadata(JsonElement entry, string contextPrefix, List<ValidationIssue> issues, string actorIdField, string section)
    {
        var hasClosureMetadata =
            entry.TryGetProperty("requestId", out _) ||
            entry.TryGetProperty("interactionType", out _) ||
            entry.TryGetProperty("status", out _) ||
            entry.TryGetProperty("responseMode", out _);

        if (!hasClosureMetadata)
            return;

        var requestId = RequireString(entry, contextPrefix, issues, "requestId");
        var interactionType = RequireString(entry, contextPrefix, issues, "interactionType");
        var status = RequireString(entry, contextPrefix, issues, "status");
        if (entry.TryGetProperty("responseMode", out _))
            ValidateOptionalString(entry, contextPrefix, issues, "responseMode");

        if (!string.IsNullOrWhiteSpace(interactionType))
        {
            var validInteractionType =
                string.Equals(actorIdField, GuardianSocialJournalState.ActorIdProperty, StringComparison.OrdinalIgnoreCase)
                    ? ActorSocialInteractionRequestState.IsSupportedGuardianInteractionType(interactionType)
                    : string.Equals(actorIdField, NpcInteractionJournalState.ActorIdProperty, StringComparison.OrdinalIgnoreCase)
                        ? ActorSocialInteractionRequestState.IsSupportedNpcInteractionType(interactionType)
                        : true;

            if (!validInteractionType)
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.interactionType",
                    IssueSeverity.Error,
                    $"{section} closure entry должен использовать canonical interactionType",
                    code: $"{section.ToLowerInvariant()}_invalid_interaction_type",
                    section: section,
                    expected: string.Equals(actorIdField, GuardianSocialJournalState.ActorIdProperty, StringComparison.OrdinalIgnoreCase)
                        ? "talk | lore"
                        : "talk",
                    actual: interactionType));
            }
        }

        if (!string.IsNullOrWhiteSpace(status) && !ActorSocialInteractionRequestState.IsSupportedResolutionStatus(status))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.status",
                IssueSeverity.Error,
                $"{section} closure entry должен использовать canonical status",
                code: $"{section.ToLowerInvariant()}_invalid_closure_status",
                section: section,
                expected: "accepted | rejected | cancelled",
                actual: status));
        }

        if (entry.TryGetProperty("responseMode", out var responseModeNode) &&
            responseModeNode.ValueKind == JsonValueKind.String)
        {
            var responseMode = responseModeNode.GetString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(responseMode) && !ActorSocialInteractionRequestState.IsSupportedResponseMode(responseMode))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.responseMode",
                    IssueSeverity.Error,
                    $"{section} closure entry должен использовать canonical responseMode",
                    code: $"{section.ToLowerInvariant()}_invalid_response_mode",
                    section: section,
                    expected: "talk_scene | lore_revealed | lore_refused | warning | refusal | trust_shift | attitude_shift",
                    actual: responseMode));
            }
        }
    }
}
