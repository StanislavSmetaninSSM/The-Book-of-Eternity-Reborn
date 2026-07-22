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

        JsonObject? currentRoot;
        try
        {
            currentRoot = JsonNode.Parse(currentJson) as JsonObject;
        }
        catch
        {
            currentRoot = null;
        }

        if (currentRoot == null)
            return issues;

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
        JsonObject? preTurnRoot;
        try
        {
            preTurnRoot = string.IsNullOrWhiteSpace(preTurnJson)
                ? null
                : JsonNode.Parse(preTurnJson) as JsonObject;
        }
        catch
        {
            preTurnRoot = null;
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
                    actual: "missing or malformed",
                    repairHint: "Restore the exact validated pre-turn npc_core snapshot before retrying the command."));
            }

            return issues;
        }

        var authority = await NpcCoreChangesContract.ReadAuthorityAsync(_fs);
        var evaluation = NpcCoreChangesContract.Evaluate(
            currentRoot,
            preTurnRoot,
            authority,
            detectDirectMutations: true);
        issues.AddRange(evaluation.Issues);
        return issues;
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
