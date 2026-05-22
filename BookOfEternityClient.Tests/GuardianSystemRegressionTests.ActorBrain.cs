using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GuardianSystemRegressionTests
{
    [Fact]
    public async Task AcceptedTurnReasoning_ShiningPoliticalActorDiffRequiresActorScope()
    {
        const string preTurnShining = """
        {
          "availability": "active",
          "shiningPoliticalActors": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "currentAgenda": "Сдерживает торговые гильдии."
            }
          ]
        }
        """;

        await WriteRawAsync(ShiningAbodeState.StatePath, """
        {
          "availability": "active",
          "shiningPoliticalActors": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "currentAgenda": "Саботирует сделку с фракцией игрока."
            }
          ]
        }
        """);
        await WritePreTurnTrackedFileAsync(
            ShiningAbodeState.StatePath,
            "test_backups/preturn_shining_actor_brain_scope.json",
            preTurnShining);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Наблюдатель\n- Почему они релевантны: reasoning ошибочно не включает сияющего политического актора.\n- Акторы вне охвата: Канцлер Лучей\n- Почему они вне охвата: ГМ ошибочно считает его фоновым.\n\n## Reasoning\n### Наблюдатель\n- Ситуация: Он видит политический шум.\n- Мысли: Он не связывает его с конкретным канцлером.\n- Действия: Он ничего не меняет.\n",
          "timestamp": "2026-05-22T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_shining_actor_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Канцлер Лучей", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_ShiningPoliticalActorDiffPassesWithActorBrainBlock()
    {
        const string preTurnShining = """
        {
          "availability": "active",
          "shiningPoliticalActors": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "currentAgenda": "Сдерживает торговые гильдии."
            }
          ]
        }
        """;

        await WriteRawAsync(ShiningAbodeState.StatePath, """
        {
          "availability": "active",
          "shiningPoliticalActors": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "currentAgenda": "Саботирует сделку с фракцией игрока."
            }
          ]
        }
        """);
        await WritePreTurnTrackedFileAsync(
            ShiningAbodeState.StatePath,
            "test_backups/preturn_shining_actor_brain_scope_valid.json",
            preTurnShining);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Канцлер Лучей\n- Почему они релевантны: Изменяется structured сияющий политический актор и его текущая стратегия.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не меняются.\n\n## Actor Brain 2.0\n### Канцлер Лучей\n- Ситуация: Политический актор видит угрозу своему залу и должен выбрать линию поведения.\n- Мысли: Он сверяет выгоду, риск, репутацию, долг перед залом и отношение к Душе.\n- Действия: Он выбирает саботаж сделки, потому что открытый конфликт пока опаснее.\n- Рассмотренные стратегии: союз, давление, саботаж, отказ.\n- Почему альтернативы отвергнуты: союз ослабит статус, давление раскроет план, отказ оставит игроку свободу.\n- State changes: меняется только shiningPoliticalActors.currentAgenda.\n",
          "timestamp": "2026-05-22T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "structured_shining_actor_update_out_of_scope", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "missing_actor_reasoning_section", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_ShiningFactionDiffRequiresActorScope()
    {
        const string preTurnShining = """
        {
          "availability": "active",
          "factions": [
            {
              "factionId": "dawn_order",
              "charter": { "factionName": "Орден Рассвета" },
              "factionStrength": 4
            }
          ]
        }
        """;

        await WriteRawAsync(ShiningAbodeState.StatePath, """
        {
          "availability": "active",
          "factions": [
            {
              "factionId": "dawn_order",
              "charter": { "factionName": "Орден Рассвета" },
              "factionStrength": 7
            }
          ]
        }
        """);
        await WritePreTurnTrackedFileAsync(
            ShiningAbodeState.StatePath,
            "test_backups/preturn_shining_faction_actor_brain_scope.json",
            preTurnShining);

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Наблюдатель\n- Почему они релевантны: reasoning ошибочно не включает сияющую фракцию.\n- Акторы вне охвата: Орден Рассвета\n- Почему они вне охвата: ГМ ошибочно считает фракцию фоном.\n\n## Reasoning\n### Наблюдатель\n- Ситуация: Он замечает шум в Обители.\n- Мысли: Он не связывает его с фракцией.\n- Действия: Он ничего не меняет.\n",
          "timestamp": "2026-05-22T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_shining_faction_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Орден Рассвета", StringComparison.OrdinalIgnoreCase));
    }
}
