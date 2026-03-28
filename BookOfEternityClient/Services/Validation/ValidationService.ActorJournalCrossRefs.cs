using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private async Task ValidateActorJournalCrossReferencesAsync(List<ValidationIssue> issues)
    {
        var knownGuardianIds = await ReadKnownGuardianIdsAsync();
        var knownNpcReferences = await ReadKnownNpcReferencesAsync();

        var knownResidentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var residentJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (!string.IsNullOrWhiteSpace(residentJson))
        {
            using var residentDoc = JsonDocument.Parse(residentJson);
            if (residentDoc.RootElement.ValueKind == JsonValueKind.Object &&
                residentDoc.RootElement.TryGetProperty(GuardianAbodeResidentState.EntriesProperty, out var residents) &&
                residents.ValueKind == JsonValueKind.Array)
            {
                foreach (var resident in residents.EnumerateArray())
                {
                    var residentId = GetFirstNonEmptyString(resident, "residentId");
                    if (!string.IsNullOrWhiteSpace(residentId))
                        knownResidentIds.Add(residentId);
                }
            }

            if (residentDoc.RootElement.ValueKind == JsonValueKind.Object &&
                residentDoc.RootElement.TryGetProperty(GuardianAbodeResidentState.ThoughtJournalProperty, out var thoughtJournal) &&
                thoughtJournal.ValueKind == JsonValueKind.Array)
            {
                ValidateResidentJournalCrossReferences(thoughtJournal, $"{GuardianAbodeResidentState.StatePath}.{GuardianAbodeResidentState.ThoughtJournalProperty}", issues, knownResidentIds, "resident_thought_unknown_resident_id");
            }

            if (residentDoc.RootElement.ValueKind == JsonValueKind.Object &&
                residentDoc.RootElement.TryGetProperty(GuardianAbodeResidentState.InteractionLogProperty, out var interactionLog) &&
                interactionLog.ValueKind == JsonValueKind.Array)
            {
                ValidateResidentJournalCrossReferences(interactionLog, $"{GuardianAbodeResidentState.StatePath}.{GuardianAbodeResidentState.InteractionLogProperty}", issues, knownResidentIds, "resident_interaction_log_unknown_resident_id");
            }
        }

        await ValidateGuardianJournalCrossReferencesAsync(GuardianThoughtJournalState.StatePath, "guardianId", knownGuardianIds, "guardian_thought_unknown_guardian_id", issues);
        await ValidateGuardianJournalCrossReferencesAsync(GuardianSocialJournalState.StatePath, "guardianId", knownGuardianIds, "guardian_social_unknown_guardian_id", issues);
        await ValidateNpcInteractionJournalCrossReferencesAsync(knownNpcReferences, issues);
    }

    private async Task ValidateGuardianJournalCrossReferencesAsync(string path, string actorIdField, HashSet<string> knownGuardianIds, string code, List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(ActorJournalState.EntriesProperty, out var entries) || entries.ValueKind != JsonValueKind.Array)
                return;

            var index = 0;
            foreach (var entry in entries.EnumerateArray())
            {
                var guardianId = GetFirstNonEmptyString(entry, actorIdField);
                if (string.IsNullOrWhiteSpace(guardianId) || knownGuardianIds.Contains(guardianId))
                {
                    index++;
                    continue;
                }

                issues.Add(new ValidationIssue(
                    $"{path}.{ActorJournalState.EntriesProperty}[{index}].{actorIdField}",
                    IssueSeverity.Error,
                    $"Journal entry ссылается на неизвестного guardian '{guardianId}'",
                    code: code,
                    section: "GuardianJournals",
                    expected: "existing guardianId from guardians.json",
                    actual: guardianId,
                    repairHint: "Для guardian journal используй существующий guardianId из game_state/meta/guardians.json."));
                index++;
            }
        }
        catch
        {
            // integrity is handled elsewhere
        }
    }

    private async Task ValidateNpcInteractionJournalCrossReferencesAsync((HashSet<string> Ids, HashSet<string> Names) knownNpcReferences, List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(NpcInteractionJournalState.StatePath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(ActorJournalState.EntriesProperty, out var entries) || entries.ValueKind != JsonValueKind.Array)
                return;

            var index = 0;
            foreach (var entry in entries.EnumerateArray())
            {
                var npcId = GetFirstNonEmptyString(entry, "npcId");
                if (string.IsNullOrWhiteSpace(npcId) || knownNpcReferences.Ids.Contains(npcId) || knownNpcReferences.Names.Contains(npcId))
                {
                    index++;
                    continue;
                }

                issues.Add(new ValidationIssue(
                    $"{NpcInteractionJournalState.StatePath}.{ActorJournalState.EntriesProperty}[{index}].npcId",
                    IssueSeverity.Error,
                    $"NPC interaction journal entry ссылается на неизвестного NPC '{npcId}'",
                    code: "npc_interaction_journal_unknown_npc_id",
                    section: "NpcInteractionJournal",
                    expected: "existing npcId from npc_core.json",
                    actual: npcId,
                    repairHint: "Для npc interaction journal используй существующий NPCId из canonical npc_core state."));
                index++;
            }
        }
        catch
        {
            // integrity is handled elsewhere
        }
    }

    private void ValidateResidentJournalCrossReferences(JsonElement entries, string contextPrefix, List<ValidationIssue> issues, HashSet<string> knownResidentIds, string code)
    {
        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            var residentId = GetFirstNonEmptyString(entry, "residentId");
            if (string.IsNullOrWhiteSpace(residentId) || knownResidentIds.Contains(residentId))
            {
                index++;
                continue;
            }

            issues.Add(new ValidationIssue(
                $"{contextPrefix}[{index}].residentId",
                IssueSeverity.Error,
                $"Resident journal entry ссылается на неизвестного resident '{residentId}'",
                code: code,
                section: "AfterlifeResidents",
                expected: $"existing residentId from {GuardianAbodeResidentState.StatePath}",
                actual: residentId,
                repairHint: "Для resident thought/event journal используй существующий residentId из guardian_abode_residents.json."));
            index++;
        }
    }
}
