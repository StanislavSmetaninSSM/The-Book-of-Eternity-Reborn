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
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

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
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_relevant_actor_missing_persistence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_UnchangedCanonicalUpdateNpcsFromPreTurnSnapshotDoNotRequireCurrentScope()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        const string npcCoreJson = """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_miron_hunter",
              "name": "Мирон-охотник",
              "role": "Следопыт",
              "currentLocationId": "old_watch_house",
              "thoughtJournal": [
                {
                  "entry": "Мирон держит сторожку под наблюдением и не вмешивается в текущий ход."
                }
              ]
            }
          ]
        }
        """;

        await WritePreTurnTrackedFileAsync(
            "game_state/npcs/npc_core.json",
            "test_backups/preturn_npc_core_unchanged_scope.json",
            npcCoreJson);
        await WriteRawAsync("game_state/npcs/npc_core.json", npcCoreJson);
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: player character\n- Why relevant: The player character examines the porch and chooses the next move.\n- Actors outside scope: Мирон-охотник\n- Why outside scope: Мирон remains unchanged canonical context from the previous turn.\n\n## Reasoning\n### player character\n- Situation: The protagonist examines the porch and fresh tracks.\n- Thoughts: She is trying to decide whether to enter or circle the house.\n- Actions: She studies the mud without interacting with Мирон.\n",
          "timestamp": "2026-07-06T12:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "structured_npc_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Мирон-охотник", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MortalPlayerCharacterFromScenarioCoreDoesNotRequireNpcPersistence()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        await WriteRawAsync("game_state/control/next_life_scenario_core.json", """
        {
          "characterDescription": "Молодая дворянка-маг Лира Вальмонт: осторожная, образованная, с талантом к руническим следам.",
          "worldDescription": "Тёмное фэнтези позднего средневековья.",
          "circumstances": "Лира просыпается в семейных покоях."
        }
        """);
        _fs.DeleteFile("game_state/npcs/npc_core.json");
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: Лира Вальмонт\n- Почему они релевантны: Это текущий герой игрока, чьи решения определяют сцену.\n- Акторы вне охвата: слуги дома Вальмонт\n- Почему они вне охвата: Они пока не появляются в сцене.\n\n## Размышления акторов\n### Лира Вальмонт\n- Ситуация: Лира просыпается и видит перчатку и письмо.\n- Мысли: Она осторожна и пытается скрыть страх.\n- Действия: Она ещё не выбрала, что исследовать первым.\n",
          "timestamp": "2026-06-29T12:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_relevant_actor_missing_persistence", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Лира Вальмонт", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MortalPlayerCharacterFromScenarioCoreAssertionsDoesNotRequireNpcPersistence()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        await WriteRawAsync("game_state/control/next_life_scenario_core.json", """
        {
          "scenarioCoreAssertions": [
            {
              "category": "identity_anchor",
              "value": "Молодая дворянка-маг Лира Вальмонт: осторожная, образованная, с талантом к руническим следам."
            },
            {
              "category": "world_anchor",
              "value": "Тёмное фэнтези позднего средневековья."
            }
          ]
        }
        """);
        _fs.DeleteFile("game_state/npcs/npc_core.json");
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: Лира Вальмонт\n- Почему они релевантны: Это текущий герой игрока, чьи решения определяют сцену.\n- Акторы вне охвата: слуги дома Вальмонт\n- Почему они вне охвата: Они пока не появляются в сцене.\n\n## Размышления акторов\n### Лира Вальмонт\n- Ситуация: Лира просыпается и видит перчатку и письмо.\n- Мысли: Она осторожна и пытается скрыть страх.\n- Действия: Она ещё не выбрала, что исследовать первым.\n",
          "timestamp": "2026-06-29T12:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_relevant_actor_missing_persistence", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Лира Вальмонт", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MortalPlayerCharacterIntroducedBySingleNamePhraseDoesNotRequireNpcPersistence()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        await WriteRawAsync("game_state/control/next_life_scenario_core.json", """
        {
          "scenarioCoreAssertions": [
            {
              "category": "identity_anchor",
              "value": "Молодая архивистка по имени Марена, выпускница портовой семинарии, привыкшая работать с запретными печатями."
            },
            {
              "category": "world_anchor",
              "value": "Позднесредневековый город-государство у холодного моря."
            }
          ]
        }
        """);
        _fs.DeleteFile("game_state/npcs/npc_core.json");
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: Марена\n- Почему они релевантны: Марена является текущей героиней игрока, через чьи действия открывается сцена.\n- Акторы вне охвата: голоса за дверью\n- Почему они вне охвата: Они пока не присутствуют в комнате и не получают структурированных обновлений.\n\n## Размышления акторов\n### Марена\n- Ситуация: Марена приходит в себя за архивным столом и видит тёмную печать.\n- Мысли: Она пытается понять, кто оставил записку.\n- Действия: Она ещё не выбрала, что исследовать первым.\n",
          "timestamp": "2026-06-29T12:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_relevant_actor_missing_persistence", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Марена", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MortalPlayerCharacterParentheticalAnnotationMatchesPlayerAliasAndBlock()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        await WriteRawAsync("game_state/control/next_life_scenario_core.json", """
        {
          "scenarioCoreAssertions": [
            {
              "category": "identity_anchor",
              "value": "Молодая дворянка-маг Лира Вальмонт: осторожная, образованная, с талантом к руническим следам."
            }
          ]
        }
        """);
        _fs.DeleteFile("game_state/npcs/npc_core.json");
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: Лира Вальмонт (player character), Дом Вальмонт\n- Почему они релевантны: Лира является текущим героем игрока, а Дом Вальмонт задаёт социальный риск сцены.\n- Акторы вне охвата: слуги дома Вальмонт\n- Почему они вне охвата: Они пока не появляются в сцене.\n\n## Размышления акторов\n### Лира Вальмонт\n- Ситуация: Лира просыпается и видит перчатку и письмо.\n- Мысли: Она осторожна и пытается скрыть страх.\n- Действия: Она ещё не выбрала, что исследовать первым.\n\n### Дом Вальмонт\n- Ситуация: Дом Вальмонт остаётся фоном сцены.\n- Мысли: Фракция не мыслит как персонаж, но её давление ощущается.\n- Действия: Дом пока не предпринимает прямого действия.\n",
          "timestamp": "2026-06-29T12:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "missing_actor_block", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Лира Вальмонт (player character)", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_relevant_actor_missing_persistence", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Лира Вальмонт (player character)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MortalPlayerCharacterRussianParentheticalDoesNotRequireNpcPersistence()
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
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: Юная дворянка-мага (персонаж игрока)\n- Почему они релевантны: Это текущая героиня игрока, через чьи решения открывается стартовая сцена.\n- Акторы вне охвата: слуги поместья\n- Почему они вне охвата: Они пока не появляются в комнате и не получают структурированных обновлений.\n\n## Размышления акторов\n### Юная дворянка-мага\n- Ситуация: Героиня просыпается в родовой спальне и видит письмо и перчатку.\n- Мысли: Она пытается сохранить самообладание.\n- Действия: Она ещё выбирает, что исследовать первым.\n",
          "timestamp": "2026-07-02T12:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_relevant_actor_missing_persistence", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Юная дворянка-мага (персонаж игрока)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MortalGenericPlayerCharacterMarkerDoesNotRequireNpcPersistence()
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
          "gm_thoughts_markdown": "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: player character, Пристав церковного суда\n- Why relevant: The player character is the current protagonist, while the bailiff speaks behind the door.\n- Actors outside scope: archive staff\n- Why outside scope: They are background only.\n\n## Reasoning\n### player character\n- Situation: The protagonist wakes at the archive table.\n- Thoughts: She tries to understand who left the seal.\n- Actions: She has not chosen a response yet.\n\n### Пристав церковного суда\n- Текущая локация: Коридор Дома Печатей.\n- Ситуация: Пристав требует документы до полудня.\n- Мысли: Он хочет получить бумаги первым.\n- Действия: Он давит на дверь и спорит с гильдейцем.\n",
          "timestamp": "2026-07-02T12:00:00Z"
        }
        """);
        await WriteRawAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "npcId": "npc_church_bailiff",
              "name": "Пристав церковного суда",
              "role": "Пристав",
              "currentLocationId": "seal_house_corridor"
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_relevant_actor_missing_persistence", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "player character", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MortalRelevantFactionWithPersistenceDoesNotRequireNpcPersistence()
    {
        await WriteRawAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        await WriteRawAsync("game_state/factions/faction_core.json", """
        {
          "factions": [
            {
              "factionId": "faction_house_valmont",
              "name": "Дом Вальмонт",
              "displayName": "Дом Вальмонт",
              "description": "Дворянский дом с архивной властью и руническими тайнами.",
              "status": "active",
              "visibility": "known",
              "developmentArchetype": "noble_house",
              "level": 1,
              "experience": 0,
              "experienceForNextLevel": 100,
              "isPlayerFaction": false,
              "isPlayerMember": true,
              "powerProfile": {},
              "resources": { "metaResources": [], "strategicGoods": [] },
              "ranks": {},
              "rankBranches": [],
              "relations": [],
              "projects": [],
              "chronicle": [],
              "customStates": []
            }
          ]
        }
        """);
        _fs.DeleteFile("game_state/npcs/npc_core.json");
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Дом Вальмонт\n- Почему они релевантны: Дом Вальмонт задаёт социальный риск сцены, доступ к архиву и последствия письма.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Других действующих сил нет.\n\n## Размышления акторов\n### Дом Вальмонт\n- Ситуация: Дом пытается удержать тайну письма внутри семьи.\n- Мысли: Фракция не мыслит как человек, но её интересы давят на сцену.\n- Действия: Давление дома проявляется через охрану архива и семейные запреты.\n",
          "timestamp": "2026-06-29T12:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_relevant_actor_missing_persistence", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Дом Вальмонт", StringComparison.OrdinalIgnoreCase));
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
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "missing_actor_block", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Стая охотников за душами.", StringComparison.OrdinalIgnoreCase));
    }
}
