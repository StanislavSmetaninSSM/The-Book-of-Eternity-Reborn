using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GuardianSystemRegressionTests
{
    [Fact]
    public async Task AcceptedTurnReasoning_MortalRelevantNpcWithoutPersistenceReportsError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        _fs.DeleteFile("game_state/npcs/npc_core.json");
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: Иветта\n- Почему они релевантны: Иветта прямо появляется в сцене, сообщает улику про серебряную нить и направляет игрока к дворецкому Ролану.\n- Акторы вне охвата: Ролан\n- Почему они вне охвата: Ролан пока не присутствует в сцене, а только упомянут как следующий контакт.\n\n## Размышления акторов\n### Иветта\n- Текущая локация: Коридор поместья Вальмонт.\n- Ситуация: Горничная встречает Асурана в коридоре после ночного письма.\n- Мысли: Она боится, но понимает, что без её подсказки хозяин пойдёт вслепую.\n- Действия: Она сообщает про серебряную нить на манжете и про дворецкого Ролана.\n",
          "timestamp": "2026-06-20T12:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "mortal_relevant_actor_missing_persistence", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Иветта", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MortalRelevantNpcWithPersistenceDoesNotReportError()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        await WriteRawAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "npcId": "npc_ivetta",
              "name": "Иветта",
              "role": "Горничная дома Вальмонт",
              "currentLocationId": "valmont_corridor"
            }
          ]
        }
        """);
        await WriteRawAsync("game_state/npcs/npc_journals.json", """
        {
          "NPCJournals": [
            {
              "npcId": "npc_ivetta",
              "entry": "Иветта рассказала Асурана про серебряную нить на манжете и указала на Ролана."
            }
          ]
        }
        """);
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: Иветта\n- Почему они релевантны: Иветта присутствует в сцене и сообщает улику, поэтому её нужно сохранить для /нпс и журналов.\n- Акторы вне охвата: Ролан\n- Почему они вне охвата: Ролан пока только упомянут как следующий контакт.\n\n## Размышления акторов\n### Иветта\n- Текущая локация: Коридор поместья Вальмонт.\n- Ситуация: Горничная встречает Асурана в коридоре после ночного письма.\n- Мысли: Она боится, но понимает, что без её подсказки хозяин пойдёт вслепую.\n- Действия: Она сообщает про серебряную нить на манжете и про дворецкого Ролана.\n",
          "timestamp": "2026-06-20T12:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_relevant_actor_missing_persistence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_ActorBlockMatchesHarmlessTrailingPunctuation()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 0
        }
        """);
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Стая охотников за душами.\n- Почему они релевантны: Стая прямо давит на душу во время духовного конфликта.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Остальные силы в сцене не принимают решений.\n\n## Reasoning\n### Стая охотников за душами\n- Ситуация: Стая окружает душу у трещины в сером приливе.\n- Мысли: Она ищет слабое место, но боится ответного света.\n- Действия: Она давит на границу защиты и готовится отступить при яркой вспышке.\n",
          "timestamp": "2026-06-27T12:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "missing_actor_block", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Стая охотников за душами.", StringComparison.OrdinalIgnoreCase));
    }
}
