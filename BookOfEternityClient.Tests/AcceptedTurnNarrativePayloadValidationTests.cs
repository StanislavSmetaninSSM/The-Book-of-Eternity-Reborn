using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AcceptedTurnNarrativePayloadValidationTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public AcceptedTurnNarrativePayloadValidationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "boe-narrative-validation-" + Guid.NewGuid().ToString("N"));
        _fs = new FileSystemManager(_tempRoot, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public async Task ValidateAcceptedTurnNarrativePayloadAsync_RejectsTechnicalRepairLeakInPlayerNarrative()
    {
        await _fs.WriteFileAtomicAsync("output/narrative_response.json", """
        {
          "response": "Оплаченный урок у Мирона завершён и теперь записан в состояние навыков корректно: \"Ножевой бой\" открыт как активный навык. Записи навыка и мастерства сохранены как массивы, поэтому будущие тренировки смогут добавляться рядом, не ломая витрину развития.",
          "timestamp": "2026-07-06T15:52:41Z"
        }
        """);

        var issues = await _validator.ValidateAcceptedTurnNarrativePayloadAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "narrative_response_technical_repair_leak", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "output/narrative_response.json.response", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnNarrativePayloadAsync_AllowsOrdinaryFantasyUseOfSimilarWords()
    {
        await _fs.WriteFileAtomicAsync("output/narrative_response.json", """
        {
          "response": "Мирон провёл Лиру к массивной дубовой двери, за которой пахло мокрой щепой и старым железом. Он показал короткий охотничий выпад без лишних слов.",
          "timestamp": "2026-07-06T15:52:41Z"
        }
        """);

        var issues = await _validator.ValidateAcceptedTurnNarrativePayloadAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "narrative_response_technical_repair_leak", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnNarrativePayloadAsync_RejectsKnownSkillUseWithoutStateDeltaOrRationale()
    {
        await WriteKnownPassiveSkillAsync("skill_life_001_seal_reading", "Чтение печатей");
        await _fs.WriteFileAtomicAsync("game_state/player/skill_mastery.json", """
        {
          "skillMasteryChanges": []
        }
        """);
        await WriteTurnCompleteAsync("output/narrative_response.json", "output/debug_logs.json");
        await _fs.WriteFileAtomicAsync("output/narrative_response.json", """
        {
          "response": "Твоя подготовка в чтении печатей помогает заметить одну неправильную деталь: свидетельская зарубка смещена на волосок.",
          "timestamp": "2026-07-09T08:00:00Z"
        }
        """);

        var issues = await _validator.ValidateAcceptedTurnNarrativePayloadAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "accepted_turn_skill_claim_missing_state_delta", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/player/skill_mastery.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnNarrativePayloadAsync_RejectsQuestClueWithoutQuestStateDeltaOrRationale()
    {
        await WriteActiveQuestAsync("quest_life_001_opening_hook", "Печать Серебряной Луны", 4);
        await WriteTurnCompleteAsync("output/narrative_response.json", "output/debug_logs.json");
        await _fs.WriteFileAtomicAsync("output/narrative_response.json", """
        {
          "response": "По делу «Печать Серебряной Луны» ты находишь новую зацепку: нижний реестр указан без полки и имени хранителя.",
          "timestamp": "2026-07-09T08:05:00Z"
        }
        """);

        var issues = await _validator.ValidateAcceptedTurnNarrativePayloadAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "accepted_turn_quest_clue_missing_state_delta", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/quests/regular_quests.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnNarrativePayloadAsync_AllowsOrdinarySkillAndQuestListing()
    {
        await WriteKnownPassiveSkillAsync("skill_life_001_seal_reading", "Чтение печатей");
        await WriteActiveQuestAsync("quest_life_001_opening_hook", "Печать Серебряной Луны", 4);
        await WriteTurnCompleteAsync("output/narrative_response.json", "output/debug_logs.json");
        await _fs.WriteFileAtomicAsync("output/narrative_response.json", """
        {
          "response": "В памяти остаются доступные ориентиры: навык «Чтение печатей» и дело «Печать Серебряной Луны». Новых выводов пока нет.",
          "timestamp": "2026-07-09T08:10:00Z"
        }
        """);

        var issues = await _validator.ValidateAcceptedTurnNarrativePayloadAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "accepted_turn_skill_claim_missing_state_delta", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "accepted_turn_quest_clue_missing_state_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnNarrativePayloadAsync_AllowsExplicitNoProgressRationale()
    {
        await WriteKnownPassiveSkillAsync("skill_life_001_seal_reading", "Чтение печатей");
        await WriteActiveQuestAsync("quest_life_001_opening_hook", "Печать Серебряной Луны", 4);
        await _fs.WriteFileAtomicAsync("game_state/player/skill_mastery.json", """
        {
          "skillMasteryChanges": []
        }
        """);
        await WriteTurnCompleteAsync("output/narrative_response.json", "output/debug_logs.json");
        await _fs.WriteFileAtomicAsync("output/narrative_response.json", """
        {
          "response": "Чтение печатей помогает удержать верный контекст, а дело «Печать Серебряной Луны» получает новую зацепку в твоих заметках.",
          "timestamp": "2026-07-09T08:15:00Z"
        }
        """);
        await _fs.WriteFileAtomicAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Prose State Delta Rationale\n- skill: Чтение печатей - no-progress rationale: сцена повторяет уже известный способ чтения, нового риска или тренировки нет.\n- quest: Печать Серебряной Луны - no-progress rationale: текст пересказывает уже сохраненную зацепку, новой стадии квеста нет.",
          "timestamp": "2026-07-09T08:15:01Z"
        }
        """);

        var issues = await _validator.ValidateAcceptedTurnNarrativePayloadAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "accepted_turn_skill_claim_missing_state_delta", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "accepted_turn_quest_clue_missing_state_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnNarrativePayloadAsync_RejectsListedButUnchangedSkillDelta()
    {
        await WriteKnownPassiveSkillAsync("skill_life_001_seal_reading", "Чтение печатей");
        const string unchangedSkillMasteryJson = """
        {
          "skillMasteryChanges": []
        }
        """;
        await _fs.WriteFileAtomicAsync("game_state/player/skill_mastery.json", unchangedSkillMasteryJson);
        await WriteValidatedSnapshotManifestAsync(("game_state/player/skill_mastery.json", unchangedSkillMasteryJson));
        await WriteTurnCompleteAsync(
            "output/narrative_response.json",
            "output/debug_logs.json",
            "game_state/player/skill_mastery.json");
        await _fs.WriteFileAtomicAsync("output/narrative_response.json", """
        {
          "response": "Твое чтение печатей помогает заметить на воске чужой нажим.",
          "timestamp": "2026-07-09T08:20:00Z"
        }
        """);

        var issues = await _validator.ValidateAcceptedTurnNarrativePayloadAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "accepted_turn_skill_claim_missing_state_delta", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/player/skill_mastery.json", StringComparison.OrdinalIgnoreCase));
    }

    private Task WriteKnownPassiveSkillAsync(string skillId, string skillName)
    {
        return _fs.WriteFileAtomicAsync("game_state/player/skills_passive.json", $$"""
        {
          "passiveSkillChanges": [
            {
              "skillId": "{{skillId}}",
              "skillName": "{{skillName}}",
              "skillDescription": "Тестовый навык.",
              "rarity": "Common",
              "type": "KnowledgeBased",
              "group": "Обученные навыки",
              "currentMasteryLevel": 1,
              "masteryLevel": 1,
              "maxMasteryLevel": 2,
              "currentMasteryProgress": 0,
              "masteryProgressNeeded": 100
            }
          ],
          "removePassiveSkills": []
        }
        """);
    }

    private Task WriteActiveQuestAsync(string questId, string title, int lastUpdatedTurn)
    {
        return _fs.WriteFileAtomicAsync("game_state/quests/regular_quests.json", $$"""
        {
          "quests": [
            {
              "questId": "{{questId}}",
              "questName": "{{title}}",
              "title": "{{title}}",
              "status": "Active",
              "summary": "Тестовое расследование.",
              "description": "Проверка сохранения квестовой зацепки.",
              "objectives": [
                {
                  "objectiveId": "obj_{{questId}}_read",
                  "description": "Найти первую зацепку.",
                  "status": "Active"
                }
              ],
              "startedAtTurn": 4,
              "lastUpdatedTurn": {{lastUpdatedTurn}},
              "visibility": "known",
              "detailsLog": [
                "#[4]. Квест начался."
              ]
            }
          ]
        }
        """);
    }

    private Task WriteTurnCompleteAsync(params string[] filesModified)
    {
        var fileList = string.Join(",\n    ", filesModified.Select(file => $"\"{file}\""));
        return _fs.WriteFileAtomicAsync("ready/turn_complete.json", $$"""
        {
          "accepted": true,
          "filesModified": [
            {{fileList}}
          ]
        }
        """);
    }

    private async Task WriteValidatedSnapshotManifestAsync(params (string Path, string Json)[] snapshotFiles)
    {
        const string sessionId = "session_prose_delta_tests";
        const string requestId = "request_prose_delta_tests";
        const int turnNumber = 9;
        const string playerAction = "Проверить контракт с печатью.";

        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": "{{playerAction}}"
        }
        """);

        var files = new JsonObject();
        var snapshotFileHashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();

        foreach (var (path, json) in snapshotFiles)
        {
            var snapshotPath = $"game_state/control/pending_turn_snapshot/{path}";
            await _fs.WriteFileAtomicAsync(snapshotPath, json);
            files[path] = snapshotPath;
            snapshotFileHashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json);
            rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-07-09T08:20:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "prose delta validation test",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }
}
