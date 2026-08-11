using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class QuestRewardAuthorityValidationTests : IDisposable
{
    private const string MissingItemAuthorityCode = "quest_reward_item_missing_detail_authority";
    private const string MissingSkillAuthorityCode = "quest_reward_skill_missing_detail_authority";
    private const string MissingRelationshipAuthorityCode = "quest_reward_relationship_missing_detail_authority";
    private const string MissingHistoryReasonCode = "quest_reward_history_reason_missing";

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public QuestRewardAuthorityValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-quest-reward-authority-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_QuestRewardItemResolvedByInventory_DoesNotReportItemAuthorityIssue()
    {
        await WriteQuestRewardAsync("""
          "rewardId": "reward_merchant_seal",
          "itemsReceived": [
            {
              "itemId": "item_merchant_seal",
              "displayName": "Печать караванного мастера"
            }
          ]
        """);
        var item = MortalItemTestFixture.CreateRawRoot(
            route: "quest_reward",
            authorityKind: "quest_reward",
            authorityId: "reward_merchant_seal",
            creationRef: "new_item_merchant_seal",
            materializationId: "mat_item_merchant_seal");
        var receipt = MortalItemIdentityState.CreateRootReceipt(
            item,
            "item_merchant_seal",
            acceptedTurn: 42);
        item["itemId"] = "item_merchant_seal";
        item["existedId"] = "item_merchant_seal";
        item.Remove("creationRef");
        item["materializationReceipt"] = receipt;
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            MortalItemTestFixture.CreateCarrier(
                item,
                "player_inventory",
                "player").ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(item).ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.QuestReward);

        Assert.DoesNotContain(issues, issue => IsIssue(issue, MissingItemAuthorityCode));
        Assert.DoesNotContain(issues, issue => IsIssue(
            issue,
            QuestRewardAuthority.MortalItemTransitionAuthorityMismatchCode));
    }

    [Fact]
    public async Task ValidateGameStateAsync_QuestRewardItemMissingInventoryAuthority_ReportsItemAuthorityIssue()
    {
        await WriteQuestRewardAsync("""
          "itemsReceived": ["item_gold_ring"]
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.QuestReward);

        Assert.Contains(issues, issue =>
            IsIssue(issue, MissingItemAuthorityCode) &&
            issue.FilePath.Contains("itemsReceived[0]", StringComparison.OrdinalIgnoreCase) &&
                issue.Actual?.Contains("item_gold_ring", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentItemRewardWithoutRewardId_ReportsTransitionAuthorityIssue()
    {
        await WriteQuestRewardAsync("""
          "itemsReceived": [
            {
              "itemId": "item_unsealed_reward",
              "displayName": "Незапечатанная награда"
            }
          ]
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.QuestReward);

        Assert.Contains(issues, issue =>
            IsIssue(
                issue,
                QuestRewardAuthority.MortalItemTransitionAuthorityMismatchCode));
    }

    [Fact]
    public async Task ValidateGameStateAsync_QuestRewardSkillResolvedByActiveSkillState_DoesNotReportSkillAuthorityIssue()
    {
        await WriteQuestRewardAsync("""
          "skillsUnlocked": ["Продвинутая торговля"]
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/skills_active.json", """
        {
          "activeSkillChanges": [
            {
              "skillName": "Продвинутая торговля",
              "skillDescription": "Позволяет выгоднее вести сложные сделки.",
              "rarity": "Uncommon",
              "actionCost": 1,
              "combatEffect": {
                "actionName": "Торговая оценка",
                "isActivatedEffect": true,
                "effects": []
              }
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.QuestReward);

        Assert.DoesNotContain(issues, issue => IsIssue(issue, MissingSkillAuthorityCode));
    }

    [Fact]
    public async Task ValidateGameStateAsync_QuestRewardSkillMissingSkillAuthority_ReportsSkillAuthorityIssue()
    {
        await WriteQuestRewardAsync("""
          "skillsUnlocked": ["skill_trading_advanced"]
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.QuestReward);

        Assert.Contains(issues, issue =>
            IsIssue(issue, MissingSkillAuthorityCode) &&
            issue.FilePath.Contains("skillsUnlocked[0]", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("skill_trading_advanced", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_QuestRewardRelationshipResolvedByNpcRelationshipState_DoesNotReportRelationshipAuthorityIssue()
    {
        await WriteQuestRewardAsync("""
          "relationshipChanges": ["npc_guild_master_+20"]
        """);
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_relationships.json", """
        {
          "NPCRelationshipChanges": [
            {
              "npcId": "npc_guild_master",
              "npcName": "Мастер гильдии",
              "relationshipLevel": 80,
              "attitude": "Доверие и Расположение"
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.QuestReward);

        Assert.DoesNotContain(issues, issue => IsIssue(issue, MissingRelationshipAuthorityCode));
    }

    [Fact]
    public async Task ValidateGameStateAsync_QuestRewardRelationshipMissingAuthority_ReportsRelationshipAuthorityIssue()
    {
        await WriteQuestRewardAsync("""
          "relationshipChanges": ["npc_guild_master_+20"]
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.QuestReward);

        Assert.Contains(issues, issue =>
            IsIssue(issue, MissingRelationshipAuthorityCode) &&
            issue.FilePath.Contains("relationshipChanges[0]", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("npc_guild_master_+20", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_HistoricalQuestRewardRecordsWithPlayerFacingReasons_DoNotReportAuthorityIssues()
    {
        await WriteQuestRewardAsync("""
          "itemsReceived": [
            {
              "itemId": "item_first_life_ring",
              "displayName": "Перстень первой жизни",
              "authorityStatus": "HistoricalOnly",
              "reason": "Перстень остался в прошлой инкарнации и больше не существует в текущем инвентаре."
            }
          ],
          "skillsUnlocked": [
            {
              "skillName": "Забытый язык купцов",
              "displayName": "Забытый язык купцов",
              "authorityStatus": "Forgotten",
              "reason": "Навык принадлежал прошлой личности и не перенесён в эту жизнь."
            }
          ],
          "relationshipChanges": [
            {
              "npcId": "npc_old_patron",
              "displayName": "Старый покровитель",
              "change": 20,
              "authorityStatus": "PriorIncarnation",
              "reason": "Этот союзник умер до текущей инкарнации; связь хранится только как история."
            }
          ]
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.QuestReward);

        Assert.DoesNotContain(issues, issue => IsIssue(issue, MissingItemAuthorityCode));
        Assert.DoesNotContain(issues, issue => IsIssue(issue, MissingSkillAuthorityCode));
        Assert.DoesNotContain(issues, issue => IsIssue(issue, MissingRelationshipAuthorityCode));
        Assert.DoesNotContain(issues, issue => IsIssue(issue, MissingHistoryReasonCode));
        Assert.DoesNotContain(issues, issue => IsIssue(
            issue,
            QuestRewardAuthority.MortalItemTransitionAuthorityMismatchCode));
    }

    [Fact]
    public async Task ValidateGameStateAsync_HistoricalQuestRewardRecordWithoutReason_ReportsMissingReasonIssue()
    {
        await WriteQuestRewardAsync("""
          "itemsReceived": [
            {
              "itemId": "item_first_life_ring",
              "displayName": "Перстень первой жизни",
              "authorityStatus": "HistoricalOnly"
            }
          ]
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.QuestReward);

        Assert.Contains(issues, issue =>
            IsIssue(issue, MissingHistoryReasonCode) &&
            issue.FilePath.Contains("itemsReceived[0]", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Перстень первой жизни", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_HistoricalQuestRewardRecordWithNumericReason_ReportsMissingReasonIssue()
    {
        await WriteQuestRewardAsync("""
          "itemsReceived": [
            {
              "itemId": "item_first_life_ring",
              "displayName": "Перстень первой жизни",
              "authorityStatus": "HistoricalOnly",
              "reason": 123
            }
          ]
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.QuestReward);

        Assert.Contains(issues, issue =>
            IsIssue(issue, MissingHistoryReasonCode) &&
            issue.FilePath.Contains("itemsReceived[0]", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Перстень первой жизни", StringComparison.OrdinalIgnoreCase) == true);
    }

    private async Task WriteQuestRewardAsync(string rewardFields)
    {
        await _fs.WriteFileAtomicAsync("game_state/quests/quest_history.json", $$"""
        {
          "questHistory": [
            {
              "questId": "quest_merchants_caravan",
              "questName": "Караван купцов",
              "outcome": "completed",
              "completionDate": "2026-06-05T12:00:00Z",
              "experience": 25,
              "incarnationNumber": 1
            }
          ],
          "questRewards": [
            {
              "questId": "quest_merchants_caravan",
        {{rewardFields}}
            }
          ],
          "questChains": []
        }
        """);
    }

    private static bool IsIssue(ValidationIssue issue, string code) =>
        string.Equals(issue.Code, code, StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
