namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private async Task ValidateQuestRewardAuthorityAsync(List<ValidationIssue> issues)
    {
        var questHistoryRoot = await ReadJsonNodeAsync("game_state/quests/quest_history.json");
        if (questHistoryRoot == null)
            return;

        var context = QuestRewardAuthorityContext.Create(
            await ReadJsonNodeAsync("game_state/inventory/items.json"),
            await ReadJsonNodeAsync("game_state/player/skills_active.json"),
            await ReadJsonNodeAsync("game_state/player/skills_passive.json"),
            await ReadJsonNodeAsync("game_state/npcs/npc_core.json"),
            await ReadJsonNodeAsync("game_state/npcs/npc_relationships.json"));

        foreach (var issue in QuestRewardAuthority.ValidateQuestRewards(questHistoryRoot, context))
        {
            issues.Add(new ValidationIssue(
                issue.Path,
                IssueSeverity.Error,
                issue.Message,
                code: issue.Code,
                section: "QuestHistory",
                expected: issue.Expected,
                actual: issue.Actual,
                repairHint: issue.RepairHint));
        }

        var itemIdentityIndex = MortalItemIdentityState.Parse(
            await _fs.ReadFileAsync(MortalItemIdentityState.StatePath));
        foreach (var issue in QuestRewardAuthority.ValidateMortalItemTransitionAuthorities(
                     questHistoryRoot,
                     itemIdentityIndex))
        {
            issues.Add(new ValidationIssue(
                issue.Path,
                IssueSeverity.Error,
                issue.Message,
                code: issue.Code,
                actor: issue.Actor,
                section: "MortalItemMaterialization",
                expected: issue.Expected,
                actual: issue.Actual,
                repairHint: issue.RepairHint,
                repairTargetFiles: new[]
                {
                    "game_state/quests/quest_history.json"
                }));
        }
    }
}
