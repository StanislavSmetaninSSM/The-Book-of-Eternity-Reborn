using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    public async Task<IReadOnlyList<ValidationIssue>> ValidateNpcCoreChangesBeforeNormalizationAsync()
    {
        var issues = new List<ValidationIssue>();
        var currentJson = await _fs.ReadFileAsync(NpcCoreChangesContract.NpcCorePath);
        if (string.IsNullOrWhiteSpace(currentJson))
            return issues;

        JsonObject currentRoot;
        try
        {
            using var currentDocument = JsonDocument.Parse(currentJson);
            if (currentDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(CreateInvalidNpcCoreChangesAuthorityIssue("non-object root"));
                return issues;
            }

            if (TryFindDuplicateJsonProperty(currentDocument.RootElement, out var duplicatePath))
            {
                issues.Add(new ValidationIssue(
                    $"{NpcCoreChangesContract.NpcCorePath}{duplicatePath}",
                    IssueSeverity.Error,
                    "Current npc_core authority contains a duplicate JSON property.",
                    code: "npc_core_changes_duplicate_property",
                    section: "NPCCoreChanges",
                    expected: "unique JSON property names",
                    actual: duplicatePath,
                    repairHint: "Rewrite npc_core.json with one authoritative value for every property before retrying the turn."));
                return issues;
            }

            currentRoot = JsonNode.Parse(currentDocument.RootElement.GetRawText())!.AsObject();
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            issues.Add(CreateInvalidNpcCoreChangesAuthorityIssue("malformed"));
            return issues;
        }

        var topLevelNameIssues = NpcCoreChangesContract.ValidateCommandTopLevelNames(currentRoot);
        if (topLevelNameIssues.Count > 0)
        {
            issues.AddRange(topLevelNameIssues);
            return issues;
        }

        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest == null)
        {
            if (NpcCoreChangesContract.HasCommandLikeProperty(currentRoot))
            {
                issues.Add(new ValidationIssue(
                    $"{NpcCoreChangesContract.NpcCorePath}.{NpcCoreChangesContract.PropertyName}",
                    IssueSeverity.Error,
                    "NPCCoreChanges requires usable validated pre-turn NPC authority before normalization.",
                    code: "npc_core_changes_pre_turn_authority_unavailable",
                    section: "NPCCoreChanges",
                    expected: "usable validated pre-turn game_state/npcs/npc_core.json snapshot",
                    actual: DescribeValidatedPendingTurnSnapshotStatus(lookup.Status),
                    repairHint: "Restore the validated pending-turn snapshot authority before retrying NPCCoreChanges."));
            }

            return issues;
        }

        var preTurnJson = await ReadValidatedPendingTurnSnapshotFileAsync(
            lookup.Manifest,
            NpcCoreChangesContract.NpcCorePath);
        JsonObject? preTurnRoot = null;
        var preTurnActual = "missing or malformed";
        try
        {
            if (!string.IsNullOrWhiteSpace(preTurnJson))
            {
                using var preTurnDocument = JsonDocument.Parse(preTurnJson);
                if (preTurnDocument.RootElement.ValueKind != JsonValueKind.Object)
                {
                    preTurnActual = "non-object root";
                }
                else if (TryFindDuplicateJsonProperty(preTurnDocument.RootElement, out var duplicatePath))
                {
                    preTurnActual = $"duplicate property {duplicatePath}";
                }
                else
                {
                    preTurnRoot = JsonNode.Parse(preTurnDocument.RootElement.GetRawText())!.AsObject();
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            preTurnActual = "malformed";
        }

        if (preTurnRoot == null)
        {
            if (NpcCoreChangesContract.HasCommandLikeProperty(currentRoot))
            {
                issues.Add(new ValidationIssue(
                    $"{NpcCoreChangesContract.NpcCorePath}.{NpcCoreChangesContract.PropertyName}",
                    IssueSeverity.Error,
                    "NPCCoreChanges validated pre-turn NPC authority is missing or malformed.",
                    code: "npc_core_changes_pre_turn_authority_unavailable",
                    section: "NPCCoreChanges",
                    expected: "readable validated pre-turn npc_core object",
                    actual: preTurnActual,
                    repairHint: "Restore the exact validated pre-turn npc_core snapshot before retrying the command."));
            }

            return issues;
        }

        var authority = await NpcCoreChangesContract.ReadAuthorityAsync(_fs);
        var acceptedTurnAuthority = MortalActorAcceptedTurnAuthority.Create(
            currentRoot,
            await ReadValidatedPendingTurnSnapshotFileAsync(
                lookup.Manifest,
                NpcTradeRequestState.PendingRequestPath),
            await ReadValidatedPendingTurnSnapshotFileAsync(
                lookup.Manifest,
                TrainingRequestState.PendingRequestPath));
        var evaluation = NpcCoreChangesContract.Evaluate(
            currentRoot,
            preTurnRoot,
            authority,
            ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract,
            detectDirectMutations: true,
            acceptedTurnAuthority);
        issues.AddRange(evaluation.Issues);
        return issues;
    }

    private static ValidationIssue CreateInvalidNpcCoreChangesAuthorityIssue(string actual) =>
        new(
            NpcCoreChangesContract.NpcCorePath,
            IssueSeverity.Error,
            "Current npc_core authority must be one readable JSON object before NPCCoreChanges validation.",
            code: "npc_core_changes_invalid_json",
            section: "NPCCoreChanges",
            expected: "one readable JSON object with unique property names",
            actual: actual,
            repairHint: "Restore valid npc_core.json authority before retrying the turn; do not bypass or discard pending NPC changes.");

    internal static IReadOnlyList<ValidationIssue> ValidateNpcCoreFateCardsAgainstProductionContract(
        JsonArray cards,
        string context)
    {
        using var document = JsonDocument.Parse(cards.ToJsonString());
        var issues = new List<ValidationIssue>();
        ValidateNpcFateCardArray(document.RootElement, context, issues);
        return issues;
    }

    internal static bool IsProductionValidMortalActiveSkill(JsonElement skill)
    {
        var issues = new List<ValidationIssue>();
        ValidateActiveSkillObject(skill, "activeSkill", issues);
        return issues.All(issue => issue.Severity != IssueSeverity.Error);
    }

    internal static bool IsProductionValidMortalPassiveSkill(JsonElement skill)
    {
        var issues = new List<ValidationIssue>();
        ValidatePassiveSkillObject(skill, "passiveSkill", issues);
        return issues.All(issue => issue.Severity != IssueSeverity.Error);
    }

    private async Task<bool> NpcCoreChangesCommandIsPresentAsync()
    {
        try
        {
            var json = await _fs.ReadFileAsync(NpcCoreChangesContract.NpcCorePath);
            return !string.IsNullOrWhiteSpace(json) &&
                   JsonNode.Parse(json) is JsonObject root &&
                   NpcCoreChangesContract.HasCommandLikeProperty(root);
        }
        catch
        {
            return false;
        }
    }
}
