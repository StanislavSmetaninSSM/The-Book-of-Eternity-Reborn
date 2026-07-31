using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GuardianSystemRegressionTests
{
    [Fact]
    public async Task AcceptedTurnReasoning_DirectlyNamedCanonicalNpcCannotBeHiddenByEmptyScope()
    {
        const string emptyJournals = """{ "NPCJournals": [] }""";
        await PrepareMortalNpcActorBrainJournalFixtureAsync(emptyJournals, emptyJournals);
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12,
          "playerAction": "Попросить Иветту рассказать о ночном посыльном."
        }
        """);
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: нет\n- Почему они релевантны: ГМ утверждает, что в сцене никто не реагирует.\n- Акторы вне охвата: Иветта\n- Почему они вне охвата: ГМ ошибочно считает прямо названного NPC фоном.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        AssertContainsIssueCodes(issues, "directly_addressed_actor_missing_from_scope");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_DirectlyNamedGuardianShortNameCannotBeHiddenByEmptyScope()
    {
        await PrepareGuardianDialogueActorBrainFixtureAsync(
            currentMusingsJson: "[]",
            preTurnMusingsJson: "[]");
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: нет\n- Почему они релевантны: ГМ утверждает, что никто не реагирует.\n- Акторы вне охвата: Элиара\n- Почему они вне охвата: ГМ ошибочно считает прямо названную Хранительницу фоном.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Guardian);

        AssertContainsIssueCodes(issues, "directly_addressed_actor_missing_from_scope");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_RoutingMetadataGuardianIdDoesNotMakeGuardianDirectlyAddressed()
    {
        const string previousJournal = """
        [
          {
            "entryId": "liora_thought_11",
            "residentId": "resident_liora",
            "turn": 11,
            "timestamp": "2026-07-09T23:55:00Z",
            "title": "Ждёт честности",
            "summary": "Я надеюсь, что душа не солжёт мне."
          }
        ]
        """;
        const string currentJournal = """
        [
          {
            "entryId": "liora_thought_11",
            "residentId": "resident_liora",
            "turn": 11,
            "timestamp": "2026-07-09T23:55:00Z",
            "title": "Ждёт честности",
            "summary": "Я надеюсь, что душа не солжёт мне."
          },
          {
            "entryId": "liora_thought_12",
            "residentId": "resident_liora",
            "turn": 12,
            "timestamp": "2026-07-10T00:00:00Z",
            "title": "Дар связи",
            "summary": "Я оставлю рядом только ту часть себя, которая умеет предупреждать, а не приказывать."
          }
        ]
        """;
        const string guardianState = """
        {
          "guardians": [
            {
              "guardianId": "guard_social_azalia_001",
              "canonicalName": "Азалия",
              "musings": []
            }
          ]
        }
        """;

        await PrepareAfterlifeResidentActorBrainJournalFixtureAsync(currentJournal, previousJournal);
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12,
          "playerAction": "[ABODE_RESIDENT_RELIC_GRANT] Игрок принимает реликвию связи от afterlife resident 'Лиора' (residentId=resident_liora, guardianId=guard_social_azalia_001, abodeId=abode_threads)."
        }
        """);
        await WriteRawAsync("game_state/meta/guardians.json", guardianState);
        await WritePreTurnGuardiansTrackedFileAsync(
            "test_backups/preturn_actor_brain_routing_metadata_guardian.json",
            guardianState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Guardian);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "directly_addressed_actor_missing_from_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Азалия", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_ExplicitNpcIdTargetCannotBeHiddenByEmptyScope()
    {
        const string emptyJournals = """{ "NPCJournals": [] }""";
        await PrepareMortalNpcActorBrainJournalFixtureAsync(emptyJournals, emptyJournals);
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12,
          "playerAction": "Поговорить с выбранным персонажем (npcId=npc_ivetta)."
        }
        """);
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: нет\n- Почему они релевантны: ГМ утверждает, что никто не реагирует.\n- Акторы вне охвата: Иветта\n- Почему они вне охвата: ГМ ошибочно считает выбранного по npcId персонажа фоном.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "directly_addressed_actor_missing_from_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Иветта", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_ExplicitNpcIdWithDuplicateDisplayNamesValidatesTargetJournal()
    {
        const string soulStateJson = """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """;
        const string npcCoreJson = """
        {
          "NPCsInScene": [
            {
              "npcId": "npc_guard_a",
              "name": "Стражник",
              "role": "Страж западных ворот",
              "currentLocationId": "city_gate"
            },
            {
              "npcId": "npc_guard_b",
              "name": "Стражник",
              "role": "Страж восточных ворот",
              "currentLocationId": "city_gate"
            }
          ]
        }
        """;
        const string previousJournals = """
        {
          "NPCJournals": [
            {
              "npcId": "npc_guard_a",
              "npcName": "Стражник",
              "journalEntries": [ { "description": "Я слежу за западными воротами." } ]
            },
            {
              "npcId": "npc_guard_b",
              "npcName": "Стражник",
              "journalEntries": [ { "description": "Я слежу за восточными воротами." } ]
            }
          ]
        }
        """;
        const string currentJournals = """
        {
          "NPCJournals": [
            {
              "npcId": "npc_guard_a",
              "npcName": "Стражник",
              "journalEntries": [
                { "description": "Я слежу за западными воротами." },
                { "description": "Я решил пропустить путника через западные ворота." }
              ]
            },
            {
              "npcId": "npc_guard_b",
              "npcName": "Стражник",
              "journalEntries": [ { "description": "Я слежу за восточными воротами." } ]
            }
          ]
        }
        """;

        await PrepareActorBrainTurnAsync("interact npcId=npc_guard_b", soulStateJson);
        await WriteRawAsync("game_state/npcs/npc_core.json", npcCoreJson);
        await WriteRawAsync("game_state/npcs/npc_journals.json", currentJournals);
        ResetValidatedPreTurnSnapshot();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_duplicate_npc_soul.json",
            NormalizeSoulStateJson(soulStateJson));
        await WritePreTurnTrackedFileAsync(
            "game_state/npcs/npc_core.json",
            "test_backups/preturn_duplicate_npc_core.json",
            npcCoreJson);
        await WritePreTurnTrackedFileAsync(
            "game_state/npcs/npc_journals.json",
            "test_backups/preturn_duplicate_npc_journals.json",
            previousJournals);
        await WriteFullActorBrainDebugLogAsync(
            "Стражник",
            "Восточные ворота",
            "NPCJournals[].journalEntries[]");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.MortalNpc);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "mortal_npc_relevant_actor_missing_thought_journal_delta", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Стражник", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_DirectlyNamedShiningActorWithProfileCannotBeHiddenByDuplicateAliases()
    {
        const string soulStateJson = """
        {
          "soulName": "Серебряная Нить Рассвета",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 0
        }
        """;
        const string profileStateJson = """
        {
          "profiles": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "realm": "Shining Abode",
              "gmThoughtsSummary": "Я удерживаю торговые залы от открытого раскола.",
              "ledger": []
            }
          ]
        }
        """;
        const string shiningStateJson = """
        {
          "shiningPoliticalActors": [
            {
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "politicalStatus": "Канцлер торговых залов"
            }
          ]
        }
        """;

        await PrepareActorBrainTurnAsync("Спросить Канцлера Лучей, почему он мешает сделке.", soulStateJson);
        await WriteRawAsync(AfterlifeEntityProfileState.StatePath, profileStateJson);
        await WriteRawAsync(ShiningAbodeState.StatePath, shiningStateJson);
        ResetValidatedPreTurnSnapshot();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_actor_brain_duplicate_alias_soul.json",
            NormalizeSoulStateJson(soulStateJson));
        await WritePreTurnTrackedFileAsync(
            AfterlifeEntityProfileState.StatePath,
            "test_backups/preturn_actor_brain_duplicate_alias_profiles.json",
            profileStateJson);
        await WritePreTurnTrackedFileAsync(
            ShiningAbodeState.StatePath,
            "test_backups/preturn_actor_brain_duplicate_alias_shining.json",
            shiningStateJson);
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: нет\n- Почему они релевантны: ГМ утверждает, что никто не реагирует.\n- Акторы вне охвата: Канцлер Лучей\n- Почему они вне охвата: ГМ ошибочно считает прямо названного актора фоном.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "directly_addressed_actor_missing_from_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Канцлер Лучей", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianDialogueRejectsSingleReadyMadeResponseWithoutFullStrategyDecision()
    {
        await PrepareGuardianDialogueActorBrainFixtureAsync(
            currentMusingsJson: "[]",
            preTurnMusingsJson: "[]");

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## NPC Scope\n- Mode: Guardian-centric\n- Relevant actors: Хранительница Элиара Карт Невозвращения\n- Why relevant: Элиара непосредственно отвечает душе и выбирает способ наставления.\n- Actors outside scope: none\n- Why outside scope: Другие акторы не участвуют.\n\n## Reasoning\n### Хранительница Элиара Карт Невозвращения\n- Current location: Архив Элиары; остаётся в обители.\n- Situation: Душа спрашивает, как защитить память.\n- Thoughts: Элиара считает, что память должна опираться на добровольную ответственность.\n- Actions: Она объясняет правило имени, обещания и возвращения.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Guardian);

        AssertContainsIssueCodes(
            issues,
            "actor_brain_missing_profile_inputs",
            "actor_brain_missing_motivation",
            "actor_brain_missing_constraints",
            "actor_brain_missing_strategy_options",
            "actor_brain_missing_strategy_tradeoffs",
            "actor_brain_missing_chosen_strategy",
            "actor_brain_missing_rejected_alternatives",
            "actor_brain_missing_state_changes");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_DoesNotAttributeAnotherActorsHeadingBySubstring()
    {
        const string guardianName = "Хранительница Элиара Карт Невозвращения";
        await PrepareGuardianDialogueActorBrainFixtureAsync(
            currentMusingsJson: """
            [
              {
                "turn": 12,
                "topic": "soul_assessment",
                "mood": "focused",
                "thought": "Я дам Душе ограниченное наставление и сохраню её право на выбор."
              }
            ]
            """,
            preTurnMusingsJson: "[]");
        await WriteFullActorBrainDebugLogAsync(guardianName, "Архив Элиары");
        var debugLog = (await _fs.ReadFileAsync("output/debug_logs.json"))!;
        var debugRoot = System.Text.Json.Nodes.JsonNode.Parse(debugLog)!.AsObject();
        var thoughts = debugRoot["gm_thoughts_markdown"]!.GetValue<string>();
        debugRoot["gm_thoughts_markdown"] = thoughts.Replace(
            $"### {guardianName}",
            $"### Советник {guardianName}",
            StringComparison.Ordinal);
        await WriteRawAsync(
            "output/debug_logs.json",
            debugRoot.ToJsonString());

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Guardian);

        AssertContainsIssueCodes(issues, "missing_actor_block");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianDialogueRejectsEmptyActorBrainFields()
    {
        await PrepareGuardianDialogueActorBrainFixtureAsync(
            currentMusingsJson: """
            [
              {
                "turn": 12,
                "topic": "soul_assessment",
                "mood": "focused",
                "thought": "Я дам Душе способ защищать память, но не стану проживать её выбор вместо неё."
              }
            ]
            """,
            preTurnMusingsJson: "[]");

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## NPC Scope\n- Mode: Guardian-centric\n- Relevant actors: Хранительница Элиара Карт Невозвращения\n- Why relevant: Элиара отвечает душе.\n- Actors outside scope: none\n- Why outside scope: Другие акторы не участвуют.\n\n## Actor Brain 2.0\n### Хранительница Элиара Карт Невозвращения\n- Current location: Архив Элиары.\n- Situation: Душа спрашивает, как защитить память.\n- Profile inputs:\n- Motivation:\n- Constraints:\n- Thoughts: Элиара обдумывает просьбу.\n- Strategy options:\n  1. Дать наставление. Benefit: душа получит опору. Risk: она поймёт совет неверно.\n  2. Отказать. Benefit: тайна сохранится. Risk: доверие будет потеряно.\n- Chosen strategy:\n- Rejected alternatives:\n- Actions: Элиара отвечает.\n- State changes:\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Guardian);

        AssertContainsIssueCodes(
            issues,
            "actor_brain_missing_profile_inputs",
            "actor_brain_missing_motivation",
            "actor_brain_missing_constraints",
            "actor_brain_missing_chosen_strategy",
            "actor_brain_missing_rejected_alternatives",
            "actor_brain_missing_state_changes");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianDialogueRejectsDuplicateStrategyOptions()
    {
        await PrepareGuardianDialogueActorBrainFixtureAsync(
            currentMusingsJson: """
            [
              {
                "turn": 12,
                "topic": "soul_assessment",
                "mood": "focused",
                "thought": "Я дам Душе способ защищать память, но не стану проживать её выбор вместо неё."
              }
            ]
            """,
            preTurnMusingsJson: "[]");

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## NPC Scope\n- Mode: Guardian-centric\n- Relevant actors: Хранительница Элиара Карт Невозвращения\n- Why relevant: Элиара отвечает душе.\n- Actors outside scope: none\n- Why outside scope: Другие акторы не участвуют.\n\n## Actor Brain 2.0\n### Хранительница Элиара Карт Невозвращения\n- Current location: Архив Элиары.\n- Situation: Душа спрашивает, как защитить память.\n- Profile inputs: Домен знания и долг наставницы.\n- Motivation: Дать полезный ответ без готового решения.\n- Constraints: Элиара не раскрывает скрытые дороги Архива.\n- Thoughts: Элиара обдумывает просьбу.\n- Strategy options:\n  1. Дать ограниченное наставление. Benefit: душа получит опору. Risk: совет поймут неверно.\n  2. Дать ограниченное наставление. Benefit: доверие сохранится. Risk: совет окажется недостаточным.\n- Chosen strategy: Дать ограниченное наставление.\n- Rejected alternatives: Другого реального варианта не предложено.\n- Actions: Элиара отвечает.\n- State changes: UpdateGuardians.addMusings.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Guardian);

        AssertContainsIssueCodes(issues, "actor_brain_missing_distinct_strategy_options");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianDialogueRejectsEmptyStrategyBenefitOrRisk()
    {
        await PrepareGuardianDialogueActorBrainFixtureAsync(
            currentMusingsJson: """
            [
              {
                "turn": 12,
                "topic": "soul_assessment",
                "mood": "focused",
                "thought": "Я дам Душе ограниченное наставление и сохраню её право на выбор."
              }
            ]
            """,
            preTurnMusingsJson: "[]");

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## NPC Scope\n- Mode: Guardian-centric\n- Relevant actors: Хранительница Элиара Карт Невозвращения\n- Why relevant: Элиара отвечает душе.\n- Actors outside scope: none\n- Why outside scope: Другие акторы не участвуют.\n\n## Actor Brain 2.0\n### Хранительница Элиара Карт Невозвращения\n- Current location: Архив Элиары.\n- Situation: Душа спрашивает, как защитить память.\n- Profile inputs: Домен знания и долг наставницы.\n- Motivation: Дать полезный ответ без готового решения.\n- Constraints: Элиара не раскрывает скрытые дороги Архива.\n- Thoughts: Элиара обдумывает просьбу.\n- Strategy options:\n  1. Дать ограниченное наставление. Benefit: Risk: совет поймут неверно.\n  2. Отказать до испытания. Benefit: тайна останется защищена. Risk: отказ разрушит доверие.\n- Chosen strategy: Дать ограниченное наставление.\n- Rejected alternatives: Отказ слишком сильно повредит доверию.\n- Actions: Элиара отвечает.\n- State changes: UpdateGuardians.addMusings.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Guardian);

        AssertContainsIssueCodes(issues, "actor_brain_missing_strategy_tradeoffs");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_DoesNotCountNumberedLinesOutsideStrategySection()
    {
        await PrepareGuardianDialogueActorBrainFixtureAsync(
            currentMusingsJson: """
            [
              {
                "turn": 12,
                "topic": "soul_assessment",
                "mood": "focused",
                "thought": "Я дам Душе ограниченное наставление и сохраню её право на выбор."
              }
            ]
            """,
            preTurnMusingsJson: "[]");

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## NPC Scope\n- Mode: Guardian-centric\n- Relevant actors: Хранительница Элиара Карт Невозвращения\n- Why relevant: Элиара отвечает душе.\n- Actors outside scope: none\n- Why outside scope: Другие акторы не участвуют.\n\n## Actor Brain 2.0\n### Хранительница Элиара Карт Невозвращения\n- Current location: Архив Элиары.\n- Situation: Душа спрашивает, как защитить память.\n- Profile inputs: Домен знания и долг наставницы.\n- Motivation: Дать полезный ответ без готового решения.\n- Constraints: Элиара не раскрывает скрытые дороги Архива.\n- Thoughts: Элиара обдумывает просьбу.\n- Strategy options:\n- Справочные заметки:\n  1. Первый известный факт. Benefit: он полезен. Risk: его можно понять неверно.\n  2. Второй известный факт. Benefit: он уточняет контекст. Risk: он отвлекает.\n- Chosen strategy: Дать ограниченное наставление.\n- Rejected alternatives: Полный отказ разрушит доверие.\n- Actions: Элиара отвечает.\n- State changes: guardianThoughtJournalUpdates.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Guardian);

        AssertContainsIssueCodes(issues, "actor_brain_missing_strategy_tradeoffs");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianDialogueRejectsFullDecisionWithoutPersistentMusingDelta()
    {
        await PrepareGuardianDialogueActorBrainFixtureAsync(
            currentMusingsJson: "[]",
            preTurnMusingsJson: "[]");

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## NPC Scope\n- Mode: Guardian-centric\n- Relevant actors: Хранительница Элиара Карт Невозвращения\n- Why relevant: Элиара непосредственно отвечает душе и выбирает способ наставления.\n- Actors outside scope: none\n- Why outside scope: Другие акторы не участвуют.\n\n## Actor Brain 2.0\n### Хранительница Элиара Карт Невозвращения\n- Current location: Архив Элиары; остаётся в обители.\n- Situation: Душа спрашивает, как защитить память.\n- Profile inputs: Домен знания, долг наставницы, спокойный требовательный характер и нулевая начальная репутация.\n- Motivation: Сохранить самостоятельность души и укрепить доверие без преждевременной выдачи силы.\n- Constraints: Элиара не может прожить выбор вместо души и не раскрывает скрытые дороги Архива.\n- Thoughts: Она сопоставляет просьбу с характером души и своим долгом наставницы.\n- Strategy options:\n  1. Дать ограниченное наставление. Benefit: душа получает применимый ритуал. Risk: она может принять метафору за готовую защиту.\n  2. Отказать до испытания. Benefit: тайны Архива останутся закрыты. Risk: отказ разрушит начальное доверие.\n- Chosen strategy: Дать ограниченное наставление и вернуть ответственность душе.\n- Rejected alternatives: Отказ отвергнут, потому что вопрос безопасен и искренен.\n- Actions: Элиара объясняет правило имени, обещания и возвращения.\n- State changes: Добавить first-person запись в canonical musings Хранителя за текущий ход.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Guardian);

        AssertContainsIssueCodes(issues, "guardian_relevant_actor_missing_thought_journal_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianDialogueAcceptsFullDecisionWithPersistentMusingDelta()
    {
        await PrepareGuardianDialogueActorBrainFixtureAsync(
            currentMusingsJson: """
            [
              {
                "turn": 12,
                "topic": "soul_assessment",
                "mood": "focused",
                "thought": "Я вижу, что эта душа просит о защите памяти, но ей полезнее получить способ отвечать за собственный выбор, а не готовый щит."
              }
            ]
            """,
            preTurnMusingsJson: "[]");

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## NPC Scope\n- Mode: Guardian-centric\n- Relevant actors: Хранительница Элиара Карт Невозвращения\n- Why relevant: Элиара непосредственно отвечает душе и выбирает способ наставления.\n- Actors outside scope: none\n- Why outside scope: Другие акторы не участвуют.\n\n## Actor Brain 2.0\n### Хранительница Элиара Карт Невозвращения\n- Current location: Архив Элиары; остаётся в обители.\n- Situation: Душа спрашивает, как защитить память.\n- Profile inputs: Домен знания, долг наставницы, спокойный требовательный характер и нулевая начальная репутация.\n- Motivation: Сохранить самостоятельность души и укрепить доверие без преждевременной выдачи силы.\n- Constraints: Элиара не может прожить выбор вместо души и не раскрывает скрытые дороги Архива.\n- Thoughts: Она сопоставляет просьбу с характером души и своим долгом наставницы.\n- Strategy options:\n  1. Дать ограниченное наставление. Benefit: душа получает применимый ритуал. Risk: она может принять метафору за готовую защиту.\n  2. Отказать до испытания. Benefit: тайны Архива останутся закрыты. Risk: отказ разрушит начальное доверие.\n- Chosen strategy: Дать ограниченное наставление и вернуть ответственность душе.\n- Rejected alternatives: Отказ отвергнут, потому что вопрос безопасен и искренен.\n- Actions: Элиара объясняет правило имени, обещания и возвращения.\n- State changes: UpdateGuardians.addMusings добавляет first-person запись за текущий ход.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Guardian);

        AssertDoesNotContainIssueCodes(
            issues,
            "actor_brain_missing_profile_inputs",
            "actor_brain_missing_motivation",
            "actor_brain_missing_constraints",
            "actor_brain_missing_strategy_options",
            "actor_brain_missing_strategy_tradeoffs",
            "actor_brain_missing_chosen_strategy",
            "actor_brain_missing_rejected_alternatives",
            "actor_brain_missing_state_changes",
            "guardian_relevant_actor_missing_thought_journal_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianDialogueRejectsThirdPersonThoughtJournalDelta()
    {
        await PrepareGuardianDialogueActorBrainFixtureAsync(
            currentMusingsJson: """
            [
              {
                "turn": 12,
                "topic": "soul_assessment",
                "mood": "focused",
                "thought": "Элиара решила дать Душе ограниченное наставление и сохранить дистанцию."
              }
            ]
            """,
            preTurnMusingsJson: "[]");
        await WriteFullActorBrainDebugLogAsync(
            "Хранительница Элиара Карт Невозвращения",
            "Архив Элиары");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Guardian);

        AssertContainsIssueCodes(issues, "actor_thought_journal_not_first_person");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianDialogueRejectsStateChangesThatOmitActualJournalDelta()
    {
        await PrepareGuardianDialogueActorBrainFixtureAsync(
            currentMusingsJson: """
            [
              {
                "turn": 12,
                "topic": "soul_assessment",
                "mood": "focused",
                "thought": "Я дам Душе способ защищать память, но не стану проживать её выбор вместо неё."
              }
            ]
            """,
            preTurnMusingsJson: "[]");

        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## NPC Scope\n- Mode: Guardian-centric\n- Relevant actors: Хранительница Элиара Карт Невозвращения\n- Why relevant: Элиара отвечает душе.\n- Actors outside scope: none\n- Why outside scope: Другие акторы не участвуют.\n\n## Actor Brain 2.0\n### Хранительница Элиара Карт Невозвращения\n- Current location: Архив Элиары.\n- Situation: Душа спрашивает, как защитить память.\n- Profile inputs: Домен знания и долг наставницы.\n- Motivation: Дать полезный ответ без готового решения.\n- Constraints: Элиара не раскрывает скрытые дороги Архива.\n- Thoughts: Элиара обдумывает просьбу.\n- Strategy options:\n  1. Дать наставление. Benefit: душа получит опору. Risk: совет поймут неверно.\n  2. Отказать. Benefit: тайна сохранится. Risk: доверие будет потеряно.\n- Chosen strategy: Дать наставление.\n- Rejected alternatives: Отказ разрушит доверие без достаточной причины.\n- Actions: Элиара отвечает.\n- State changes: Нет изменений.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Guardian);

        AssertContainsIssueCodes(issues, "actor_brain_state_changes_missing_actual_journal_surface");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianDialogueAcceptsStructuredThoughtJournalDelta()
    {
        await PrepareGuardianDialogueActorBrainFixtureAsync(
            currentMusingsJson: "[]",
            preTurnMusingsJson: "[]");
        await WriteRawAsync(GuardianThoughtJournalState.StatePath, """
        {
          "entries": [
            {
              "entryId": "elara_thought_12",
              "guardianId": "guard_freeform_guardian_001",
              "turn": 12,
              "timestamp": "2026-07-10T00:00:00Z",
              "title": "Ответственность памяти",
              "summary": "Я дам Душе способ защищать память, но не стану проживать её выбор вместо неё."
            }
          ]
        }
        """);
        await WritePreTurnTrackedFileAsync(
            GuardianThoughtJournalState.StatePath,
            "test_backups/preturn_actor_brain_guardian_thought_journal.json",
            """{ "entries": [] }""");
        await WriteFullActorBrainDebugLogAsync(
            "Хранительница Элиара Карт Невозвращения",
            "Архив Элиары");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Guardian);

        AssertDoesNotContainIssueCodes(issues, "guardian_relevant_actor_missing_thought_journal_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_GuardianDialogueRejectsReplacingOldMusingInsteadOfAppending()
    {
        await PrepareGuardianDialogueActorBrainFixtureAsync(
            currentMusingsJson: """
            [
              {
                "turn": 12,
                "topic": "soul_assessment",
                "mood": "focused",
                "thought": "Я дам Душе способ защищать память, но не стану проживать её выбор вместо неё."
              }
            ]
            """,
            preTurnMusingsJson: """
            [
              {
                "turn": 11,
                "topic": "personal_reflection",
                "mood": "calm",
                "thought": "Я должна сначала понять, почему эта Душа боится забыть себя."
              }
            ]
            """);
        await WriteFullActorBrainDebugLogAsync(
            "Хранительница Элиара Карт Невозвращения",
            "Архив Элиары");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Guardian);

        AssertContainsIssueCodes(issues, "guardian_relevant_actor_missing_thought_journal_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MortalNpcDecisionRejectsUnchangedThoughtJournal()
    {
        const string soulStateJson = """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """;
        const string npcCoreJson = """
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
        """;
        const string unchangedJournalJson = """
        {
          "NPCJournals": [
            {
              "npcId": "npc_ivetta",
              "npcName": "Иветта",
              "lastJournalNote": "Я боюсь ночного посыльного.",
              "journalEntries": [
                {
                  "entryId": "ivetta_thought_11",
                  "description": "Я боюсь ночного посыльного.",
                  "timestamp": "2026-07-09T23:55:00Z"
                }
              ]
            }
          ]
        }
        """;

        await PrepareActorBrainTurnAsync("Попросить Иветту рассказать о ночном посыльном.", soulStateJson);
        await WriteRawAsync("game_state/npcs/npc_core.json", npcCoreJson);
        await WriteRawAsync("game_state/npcs/npc_journals.json", unchangedJournalJson);
        ResetValidatedPreTurnSnapshot();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_actor_brain_mortal_soul.json",
            NormalizeSoulStateJson(soulStateJson));
        await WritePreTurnTrackedFileAsync(
            "game_state/npcs/npc_core.json",
            "test_backups/preturn_actor_brain_npc_core.json",
            npcCoreJson);
        await WritePreTurnTrackedFileAsync(
            "game_state/npcs/npc_journals.json",
            "test_backups/preturn_actor_brain_npc_journals.json",
            unchangedJournalJson);
        await WriteFullActorBrainDebugLogAsync("Иветта", "Коридор поместья Вальмонт");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.MortalNpc);

        AssertContainsIssueCodes(issues, "mortal_npc_relevant_actor_missing_thought_journal_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MortalNpcDecisionRejectsMissingValidatedPreTurnSnapshot()
    {
        const string currentJournal = """
        {
          "NPCJournals": [
            {
              "npcId": "npc_ivetta",
              "npcName": "Иветта",
              "journalEntries": [
                {
                  "entryId": "ivetta_thought_12",
                  "description": "Я отвечу осторожно и запомню реакцию собеседника.",
                  "timestamp": "2026-07-10T00:00:00Z"
                }
              ]
            }
          ]
        }
        """;

        await PrepareMortalNpcActorBrainJournalFixtureAsync(currentJournal, """{ "NPCJournals": [] }""");
        ResetValidatedPreTurnSnapshot();

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.MortalNpc);

        AssertContainsIssueCodes(issues, "actor_memory_invalid_validated_snapshot_context");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MortalNpcDecisionRejectsUsableSnapshotMissingPreexistingJournalEntry()
    {
        const string previousJournal = """
        {
          "NPCJournals": [
            {
              "npcId": "npc_ivetta",
              "npcName": "Иветта",
              "journalEntries": [
                {
                  "entryId": "ivetta_thought_11",
                  "description": "Я боюсь ночного посыльного.",
                  "timestamp": "2026-07-09T23:55:00Z"
                }
              ]
            }
          ]
        }
        """;
        const string currentJournal = """
        {
          "NPCJournals": [
            {
              "npcId": "npc_ivetta",
              "npcName": "Иветта",
              "journalEntries": [
                {
                  "entryId": "ivetta_thought_12",
                  "description": "Я отвечу осторожно и запомню реакцию собеседника.",
                  "timestamp": "2026-07-10T00:00:00Z"
                }
              ]
            }
          ]
        }
        """;

        await PrepareMortalNpcActorBrainJournalFixtureAsync(currentJournal, previousJournal);
        await RemoveTrackedSnapshotEntryFromCurrentPendingTurnSnapshotAsync(
            "game_state/npcs/npc_journals.json");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.MortalNpc);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "actor_memory_invalid_validated_snapshot_context", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Иветта", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MortalNpcDecisionAcceptsAuthoritativelyAbsentJournalBaseline()
    {
        const string currentJournal = """
        {
          "NPCJournals": [
            {
              "npcId": "npc_ivetta",
              "npcName": "Иветта",
              "journalEntries": [
                {
                  "entryId": "ivetta_thought_12",
                  "description": "Я отвечу осторожно и запомню реакцию собеседника.",
                  "timestamp": "2026-07-10T00:00:00Z"
                }
              ]
            }
          ]
        }
        """;

        await PrepareMortalNpcActorBrainJournalFixtureAsync(
            currentJournal,
            """{ "NPCJournals": [] }""");
        await RemoveTrackedFileFromCurrentPendingTurnSnapshotAsync(
            "game_state/npcs/npc_journals.json");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.MortalNpc);

        AssertDoesNotContainIssueCodes(
            issues,
            "actor_memory_invalid_validated_snapshot_context",
            "mortal_npc_relevant_actor_missing_thought_journal_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_AfterlifeResidentDecisionRejectsUnchangedThoughtJournal()
    {
        const string soulStateJson = """
        {
          "soulName": "Серебряная Нить Рассвета",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 0
        }
        """;
        const string unchangedResidentStateJson = """
        {
          "entries": [
            {
              "residentId": "resident_liora",
              "guardianId": "guardian_azalia",
              "abodeId": "abode_threads",
              "displayName": "Лиора",
              "residentKind": "wayfaring_soul",
              "originType": "traveler_soul",
              "roleLabel": "Вестница",
              "summary": "Слушает нити дорог.",
              "bondLevel": 61,
              "bondTier": "trusted",
              "isPresent": true
            }
          ],
          "thoughtJournal": [
            {
              "entryId": "liora_thought_11",
              "residentId": "resident_liora",
              "turn": 11,
              "timestamp": "2026-07-09T23:55:00Z",
              "title": "Ждёт честности",
              "summary": "Я надеюсь, что душа не солжёт мне."
            }
          ]
        }
        """;

        await PrepareActorBrainTurnAsync("Спросить Лиору, почему она боится опоздать.", soulStateJson);
        await WriteRawAsync(GuardianAbodeResidentState.StatePath, unchangedResidentStateJson);
        ResetValidatedPreTurnSnapshot();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_actor_brain_resident_soul.json",
            NormalizeSoulStateJson(soulStateJson));
        await WritePreTurnTrackedFileAsync(
            GuardianAbodeResidentState.StatePath,
            "test_backups/preturn_actor_brain_residents.json",
            unchangedResidentStateJson);
        await WriteFullActorBrainDebugLogAsync("Лиора", "Сад нитей Обители");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.AfterlifeResident);

        AssertContainsIssueCodes(issues, "afterlife_resident_relevant_actor_missing_thought_journal_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_ShiningResidentDecisionRejectsUnchangedThoughtJournal()
    {
        const string unchangedJournal = """
        [
          {
            "entryId": "liora_thought_11",
            "residentId": "resident_liora",
            "turn": 11,
            "timestamp": "2026-07-09T23:55:00Z",
            "title": "Ждёт честности",
            "summary": "Я надеюсь, что душа не солжёт мне."
          }
        ]
        """;

        await PrepareAfterlifeResidentActorBrainJournalFixtureAsync(
            unchangedJournal,
            unchangedJournal,
            "Shining Abode");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.AfterlifeResident);

        AssertContainsIssueCodes(issues, "afterlife_resident_relevant_actor_missing_thought_journal_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MortalNpcDecisionAcceptsNewThoughtJournalEntry()
    {
        const string previousJournal = """
        {
          "NPCJournals": [
            {
              "npcId": "npc_ivetta",
              "npcName": "Иветта",
              "journalEntries": [
                {
                  "entryId": "ivetta_thought_11",
                  "description": "Я боюсь ночного посыльного.",
                  "timestamp": "2026-07-09T23:55:00Z"
                }
              ]
            }
          ]
        }
        """;
        const string currentJournal = """
        {
          "NPCJournals": [
            {
              "npcId": "npc_ivetta",
              "npcName": "Иветта",
              "lastJournalNote": "Он спрашивает осторожно, но я пока не доверяю ему полностью.",
              "journalEntries": [
                {
                  "entryId": "ivetta_thought_11",
                  "description": "Я боюсь ночного посыльного.",
                  "timestamp": "2026-07-09T23:55:00Z"
                },
                {
                  "entryId": "ivetta_thought_12",
                  "description": "Он спрашивает осторожно, но я пока не доверяю ему полностью.",
                  "timestamp": "2026-07-10T00:00:00Z"
                }
              ]
            }
          ]
        }
        """;

        await PrepareMortalNpcActorBrainJournalFixtureAsync(currentJournal, previousJournal);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.MortalNpc);

        AssertDoesNotContainIssueCodes(
            issues,
            "mortal_relevant_actor_missing_persistence",
            "mortal_npc_relevant_actor_missing_thought_journal_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MortalNpcDecisionRejectsReplacingOldThoughtInsteadOfAppending()
    {
        const string previousJournal = """
        {
          "NPCJournals": [
            {
              "npcId": "npc_ivetta",
              "npcName": "Иветта",
              "journalEntries": [
                {
                  "entryId": "ivetta_thought_11",
                  "description": "Я боюсь ночного посыльного.",
                  "timestamp": "2026-07-09T23:55:00Z"
                }
              ]
            }
          ]
        }
        """;
        const string replacementJournal = """
        {
          "NPCJournals": [
            {
              "npcId": "npc_ivetta",
              "npcName": "Иветта",
              "journalEntries": [
                {
                  "entryId": "ivetta_thought_12",
                  "description": "Он спрашивает осторожно, но я пока не доверяю ему полностью.",
                  "timestamp": "2026-07-10T00:00:00Z"
                }
              ]
            }
          ]
        }
        """;

        await PrepareMortalNpcActorBrainJournalFixtureAsync(replacementJournal, previousJournal);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.MortalNpc);

        AssertContainsIssueCodes(issues, "mortal_npc_relevant_actor_missing_thought_journal_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_MortalNpcDecisionRejectsEditingOldEntryEvenWhenNewEntryIsAdded()
    {
        const string previousJournal = """
        {
          "NPCJournals": [
            {
              "npcId": "npc_ivetta",
              "npcName": "Иветта",
              "journalEntries": [
                {
                  "entryId": "ivetta_thought_11",
                  "description": "Я боюсь ночного посыльного.",
                  "timestamp": "2026-07-09T23:55:00Z"
                }
              ]
            }
          ]
        }
        """;
        const string rewrittenJournal = """
        {
          "NPCJournals": [
            {
              "npcId": "npc_ivetta",
              "npcName": "Иветта",
              "journalEntries": [
                {
                  "entryId": "ivetta_thought_11",
                  "description": "Я никогда не боялась ночного посыльного.",
                  "timestamp": "2026-07-09T23:55:00Z"
                },
                {
                  "entryId": "ivetta_thought_12",
                  "description": "Теперь я отвечу осторожно.",
                  "timestamp": "2026-07-10T00:00:00Z"
                }
              ]
            }
          ]
        }
        """;

        await PrepareMortalNpcActorBrainJournalFixtureAsync(rewrittenJournal, previousJournal);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.MortalNpc);

        AssertContainsIssueCodes(issues, "mortal_npc_relevant_actor_missing_thought_journal_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_AfterlifeResidentDecisionAcceptsNewThoughtJournalEntry()
    {
        const string previousJournal = """
        [
          {
            "entryId": "liora_thought_11",
            "residentId": "resident_liora",
            "turn": 11,
            "timestamp": "2026-07-09T23:55:00Z",
            "title": "Ждёт честности",
            "summary": "Я надеюсь, что душа не солжёт мне."
          }
        ]
        """;
        const string currentJournal = """
        [
          {
            "entryId": "liora_thought_11",
            "residentId": "resident_liora",
            "turn": 11,
            "timestamp": "2026-07-09T23:55:00Z",
            "title": "Ждёт честности",
            "summary": "Я надеюсь, что душа не солжёт мне."
          },
          {
            "entryId": "liora_thought_12",
            "residentId": "resident_liora",
            "turn": 12,
            "timestamp": "2026-07-10T00:00:00Z",
            "title": "Осторожный ответ",
            "summary": "Её вопрос звучит искренне, но я расскажу только то, за что готова отвечать."
          }
        ]
        """;

        await PrepareAfterlifeResidentActorBrainJournalFixtureAsync(currentJournal, previousJournal);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.AfterlifeResident);

        AssertDoesNotContainIssueCodes(
            issues,
            "afterlife_resident_relevant_actor_missing_thought_journal_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_AfterlifeResidentDecisionRejectsReplacingOldThoughtInsteadOfAppending()
    {
        const string previousJournal = """
        [
          {
            "entryId": "liora_thought_11",
            "residentId": "resident_liora",
            "turn": 11,
            "timestamp": "2026-07-09T23:55:00Z",
            "title": "Ждёт честности",
            "summary": "Я надеюсь, что душа не солжёт мне."
          }
        ]
        """;
        const string replacementJournal = """
        [
          {
            "entryId": "liora_thought_12",
            "residentId": "resident_liora",
            "turn": 12,
            "timestamp": "2026-07-10T00:00:00Z",
            "title": "Осторожный ответ",
            "summary": "Я расскажу только то, за что готова отвечать."
          }
        ]
        """;

        await PrepareAfterlifeResidentActorBrainJournalFixtureAsync(replacementJournal, previousJournal);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.AfterlifeResident);

        AssertContainsIssueCodes(issues, "afterlife_resident_relevant_actor_missing_thought_journal_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_AfterlifeEntityDecisionRejectsUnchangedCanonicalMemoryLedger()
    {
        const string unchangedProfileState = """
        {
          "profiles": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "realm": "Shining Abode",
              "goals": {
                "goalId": "goal_censor_order",
                "shortTermGoal": "Сдержать торговые гильдии.",
                "longTermGoal": "Сохранить власть Зала.",
                "plan": "Давить через правила допуска.",
                "gmThoughtsSummary": "Он считает открытый конфликт преждевременным.",
                "updatedAtTurn": 11
              },
              "ledger": []
            }
          ]
        }
        """;

        await PrepareAfterlifeEntityActorBrainFixtureAsync(unchangedProfileState, unchangedProfileState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.AfterlifeEntity);

        AssertContainsIssueCodes(issues, "afterlife_entity_relevant_actor_missing_memory_ledger_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_FirstAfterlifeEntityMaterializationCannotBypassActorScope()
    {
        const string soulStateJson = """
        {
          "soulName": "Серебряная Нить Рассвета",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 0
        }
        """;
        const string currentProfileState = """
        {
          "profiles": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "realm": "Shining Abode",
              "gmThoughtsSummary": "Я впервые выбираю, как ограничить торговые залы.",
              "ledger": []
            }
          ]
        }
        """;

        await PrepareActorBrainTurnAsync(string.Empty, soulStateJson);
        await WriteRawAsync(AfterlifeEntityProfileState.StatePath, currentProfileState);
        ResetValidatedPreTurnSnapshot();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_actor_brain_new_entity_soul.json",
            NormalizeSoulStateJson(soulStateJson));
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: нет\n- Почему они релевантны: ГМ утверждает, что значимых акторов нет.\n- Акторы вне охвата: Канцлер Лучей\n- Почему они вне охвата: ГМ ошибочно считает первое создание профиля фоном.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.AfterlifeEntity);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_afterlife_entity_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Канцлер Лучей", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_FirstAfterlifeResidentMaterializationCannotBypassActorScope()
    {
        const string soulStateJson = """
        {
          "soulName": "Серебряная Нить Рассвета",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 0
        }
        """;
        const string currentResidentState = """
        {
          "entries": [
            {
              "residentId": "resident_new_witness",
              "guardianId": "guardian_azalia",
              "abodeId": "abode_threads",
              "displayName": "Тихая Свидетельница",
              "residentKind": "wayfaring_soul",
              "originType": "traveler_soul",
              "roleLabel": "Свидетельница",
              "summary": "Впервые решает остаться в Обители.",
              "bondLevel": 10,
              "bondTier": "acquainted",
              "isPresent": true
            }
          ],
          "thoughtJournal": []
        }
        """;

        await PrepareActorBrainTurnAsync(string.Empty, soulStateJson);
        await WriteRawAsync(GuardianAbodeResidentState.StatePath, currentResidentState);
        ResetValidatedPreTurnSnapshot();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_new_resident_soul.json",
            NormalizeSoulStateJson(soulStateJson));
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: нет\n- Почему они релевантны: ГМ утверждает, что значимых акторов нет.\n- Акторы вне охвата: Тихая Свидетельница\n- Почему они вне охвата: ГМ ошибочно считает первое создание жителя фоном.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.AfterlifeResident);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_resident_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Тихая Свидетельница", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_AfterlifeEntityProfileDiffCannotStayOutsideActorScope()
    {
        const string soulStateJson = """
        {
          "soulName": "Серебряная Нить Рассвета",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 0
        }
        """;
        const string previousProfileState = """
        {
          "profiles": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "realm": "Shining Abode",
              "goals": {
                "goalId": "goal_censor_order",
                "shortTermGoal": "Сдержать торговые гильдии.",
                "longTermGoal": "Сохранить власть Зала.",
                "plan": "Давить через правила допуска.",
                "gmThoughtsSummary": "Он считает открытый конфликт преждевременным.",
                "updatedAtTurn": 11
              },
              "ledger": []
            }
          ]
        }
        """;
        const string currentProfileState = """
        {
          "profiles": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "realm": "Shining Abode",
              "goals": {
                "goalId": "goal_censor_order",
                "shortTermGoal": "Сорвать сделку игрока.",
                "longTermGoal": "Сохранить власть Зала.",
                "plan": "Подтолкнуть союзников к саботажу.",
                "gmThoughtsSummary": "Он выбирает скрытое давление.",
                "updatedAtTurn": 12
              },
              "ledger": []
            }
          ]
        }
        """;

        await PrepareActorBrainTurnAsync(string.Empty, soulStateJson);
        await WriteRawAsync(AfterlifeEntityProfileState.StatePath, currentProfileState);
        ResetValidatedPreTurnSnapshot();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_actor_brain_entity_diff_soul.json",
            NormalizeSoulStateJson(soulStateJson));
        await WritePreTurnTrackedFileAsync(
            AfterlifeEntityProfileState.StatePath,
            "test_backups/preturn_actor_brain_entity_diff_profiles.json",
            previousProfileState);
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: нет\n- Почему они релевантны: ГМ утверждает, что значимых акторов нет.\n- Акторы вне охвата: Канцлер Лучей\n- Почему они вне охвата: ГМ ошибочно считает изменение профиля фоном.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.AfterlifeEntity);

        AssertContainsIssueCodes(
            issues,
            "structured_afterlife_entity_update_out_of_scope",
            "missing_actor_block");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_AfterlifeEntityDecisionAcceptsCanonicalMemoryLedgerDelta()
    {
        const string previousProfileState = """
        {
          "profiles": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "realm": "Shining Abode",
              "goals": {
                "goalId": "goal_censor_order",
                "shortTermGoal": "Сдержать торговые гильдии.",
                "longTermGoal": "Сохранить власть Зала.",
                "plan": "Давить через правила допуска.",
                "gmThoughtsSummary": "Он считает открытый конфликт преждевременным.",
                "updatedAtTurn": 11
              },
              "ledger": []
            }
          ]
        }
        """;
        const string currentProfileState = """
        {
          "profiles": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "realm": "Shining Abode",
              "goals": {
                "goalId": "goal_censor_order",
                "shortTermGoal": "Сорвать сделку игрока без открытого конфликта.",
                "longTermGoal": "Сохранить власть Зала.",
                "plan": "Подтолкнуть союзников к скрытому саботажу.",
                "gmThoughtsSummary": "Он выбирает скрытый саботаж, потому что открытая атака объединит противников.",
                "updatedAtTurn": 12
              },
              "ledger": [
                {
                  "entryId": "radiant_censor_decision_12",
                  "summary": "Я выберу скрытый саботаж: открытая атака объединит моих противников.",
                  "turnNumber": 12
                }
              ]
            }
          ]
        }
        """;

        await PrepareAfterlifeEntityActorBrainFixtureAsync(currentProfileState, previousProfileState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.AfterlifeEntity);

        AssertDoesNotContainIssueCodes(issues, "afterlife_entity_relevant_actor_missing_memory_ledger_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_NewAfterlifeEntityAcceptsInitialThoughtSummaryWithoutLedgerHistory()
    {
        const string currentProfileState = """
        {
          "profiles": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "realm": "Shining Abode",
              "goals": {
                "goalId": "goal_censor_order",
                "shortTermGoal": "Сдержать торговые гильдии.",
                "longTermGoal": "Сохранить власть Зала.",
                "plan": "Давить через правила допуска.",
                "gmThoughtsSummary": "Я считаю открытый конфликт преждевременным.",
                "updatedAtTurn": 12
              },
              "ledger": []
            }
          ]
        }
        """;

        await PrepareAfterlifeEntityActorBrainFixtureAsync(
            currentProfileState,
            """{ "profiles": [] }""");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.AfterlifeEntity);

        AssertDoesNotContainIssueCodes(issues, "afterlife_entity_relevant_actor_missing_memory_ledger_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_AfterlifeEntityDecisionRejectsRewritingExistingSummaryWithoutLedgerAppend()
    {
        const string previousProfileState = """
        {
          "profiles": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "realm": "Shining Abode",
              "goals": {
                "goalId": "goal_censor_order",
                "shortTermGoal": "Сдержать торговые гильдии.",
                "longTermGoal": "Сохранить власть Зала.",
                "plan": "Давить через правила допуска.",
                "gmThoughtsSummary": "Он считает открытый конфликт преждевременным.",
                "updatedAtTurn": 11
              },
              "ledger": []
            }
          ]
        }
        """;
        const string rewrittenProfileState = """
        {
          "profiles": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "realm": "Shining Abode",
              "goals": {
                "goalId": "goal_censor_order",
                "shortTermGoal": "Сорвать сделку игрока без открытого конфликта.",
                "longTermGoal": "Сохранить власть Зала.",
                "plan": "Подтолкнуть союзников к скрытому саботажу.",
                "gmThoughtsSummary": "Он выбирает скрытый саботаж, потому что открытая атака объединит противников.",
                "updatedAtTurn": 12
              },
              "ledger": []
            }
          ]
        }
        """;

        await PrepareAfterlifeEntityActorBrainFixtureAsync(rewrittenProfileState, previousProfileState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.AfterlifeEntity);

        AssertContainsIssueCodes(issues, "afterlife_entity_relevant_actor_missing_memory_ledger_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_AfterlifeEntityDecisionRejectsNewActivitySummaryWithoutLedgerAppend()
    {
        const string previousProfileState = """
        {
          "profiles": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "realm": "Shining Abode",
              "goals": {
                "goalId": "goal_censor_order",
                "shortTermGoal": "Сдержать торговые гильдии.",
                "longTermGoal": "Сохранить власть Зала.",
                "plan": "Давить через правила допуска.",
                "gmThoughtsSummary": "Он считает открытый конфликт преждевременным.",
                "updatedAtTurn": 11
              },
              "ledger": []
            }
          ]
        }
        """;
        const string currentProfileState = """
        {
          "profiles": [
            {
              "actorType": "radiant_actor",
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "realm": "Shining Abode",
              "goals": {
                "goalId": "goal_censor_order",
                "shortTermGoal": "Сдержать торговые гильдии.",
                "longTermGoal": "Сохранить власть Зала.",
                "plan": "Давить через правила допуска.",
                "gmThoughtsSummary": "Он считает открытый конфликт преждевременным.",
                "updatedAtTurn": 11
              },
              "currentActivity": {
                "activityId": "activity_censor_sabotage",
                "goalId": "goal_censor_order",
                "questId": "quest_censor_sabotage",
                "gmThoughtsSummary": "Я начну скрытую подготовку саботажа.",
                "startedAtTurn": 12
              },
              "ledger": []
            }
          ]
        }
        """;

        await PrepareAfterlifeEntityActorBrainFixtureAsync(currentProfileState, previousProfileState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.AfterlifeEntity);

        AssertContainsIssueCodes(issues, "afterlife_entity_relevant_actor_missing_memory_ledger_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_AfterlifeDecisionRejectsActorWithoutCanonicalMemoryOwner()
    {
        const string soulStateJson = """
        {
          "soulName": "Серебряная Нить Рассвета",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 0
        }
        """;

        await PrepareActorBrainTurnAsync("Попросить Безымянного Советника принять решение.", soulStateJson);
        ResetValidatedPreTurnSnapshot();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_actor_brain_unknown_afterlife_soul.json",
            NormalizeSoulStateJson(soulStateJson));
        await WriteFullActorBrainDebugLogAsync(
            "Безымянный Советник",
            "Край Моря Хаоса",
            "неизвестная canonical surface");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.AfterlifeEntity);

        AssertContainsIssueCodes(issues, "afterlife_relevant_actor_missing_canonical_memory_owner");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_ShiningFactionDecisionRejectsUnchangedStrategicMemory()
    {
        const string unchangedFactionState = """
        {
          "availability": "active",
          "factions": [
            {
              "factionId": "faction_dawn_order",
              "name": "Орден Рассвета",
              "strategicMemory": {
                "summary": "Орден избегает открытого конфликта.",
                "lastUpdatedTurn": 11,
                "recentCampaigns": []
              },
              "chronicle": []
            }
          ]
        }
        """;

        await PrepareShiningFactionActorBrainFixtureAsync(unchangedFactionState, unchangedFactionState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.ShiningFaction);

        AssertContainsIssueCodes(issues, "shining_faction_relevant_actor_missing_strategic_memory_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_ShiningFactionDecisionRejectsStrategicMemoryRewriteWithoutChronicleAppend()
    {
        const string previousFactionState = """
        {
          "availability": "active",
          "factions": [
            {
              "factionId": "faction_dawn_order",
              "name": "Орден Рассвета",
              "strategicMemory": {
                "summary": "Орден избегает открытого конфликта.",
                "lastUpdatedTurn": 11,
                "recentCampaigns": []
              },
              "chronicle": []
            }
          ]
        }
        """;
        const string currentFactionState = """
        {
          "availability": "active",
          "factions": [
            {
              "factionId": "faction_dawn_order",
              "name": "Орден Рассвета",
              "strategicMemory": {
                "summary": "Орден выбрал переговоры, чтобы не объединить соперников против себя.",
                "lastUpdatedTurn": 12,
                "recentCampaigns": [ "Переговоры у Зала Рассвета" ]
              },
              "chronicle": []
            }
          ]
        }
        """;

        await PrepareShiningFactionActorBrainFixtureAsync(currentFactionState, previousFactionState);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.ShiningFaction);

        AssertContainsIssueCodes(issues, "shining_faction_relevant_actor_missing_strategic_memory_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_ShiningFactionDecisionAcceptsAppendOnlyChronicleDelta()
    {
        const string previousFactionState = """
        {
          "availability": "active",
          "factions": [
            {
              "factionId": "faction_dawn_order",
              "name": "Орден Рассвета",
              "strategicMemory": {
                "summary": "Орден избегает открытого конфликта.",
                "lastUpdatedTurn": 11,
                "recentCampaigns": []
              },
              "chronicle": [
                {
                  "entryId": "dawn_order_11",
                  "turnNumber": 11,
                  "eventType": "council",
                  "summary": "Совет отложил открытый спор."
                }
              ]
            }
          ]
        }
        """;
        const string currentFactionState = """
        {
          "availability": "active",
          "factions": [
            {
              "factionId": "faction_dawn_order",
              "name": "Орден Рассвета",
              "strategicMemory": {
                "summary": "Орден выбрал переговоры, чтобы не объединить соперников против себя.",
                "lastUpdatedTurn": 12,
                "recentCampaigns": [ "Переговоры у Зала Рассвета" ]
              },
              "chronicle": [
                {
                  "entryId": "dawn_order_11",
                  "turnNumber": 11,
                  "eventType": "council",
                  "summary": "Совет отложил открытый спор."
                },
                {
                  "entryId": "dawn_order_12",
                  "turnNumber": 12,
                  "eventType": "negotiation",
                  "summary": "Мы начинаем переговоры, чтобы не объединить соперников против Ордена."
                }
              ]
            }
          ]
        }
        """;

        await PrepareShiningFactionActorBrainFixtureAsync(
            currentFactionState,
            previousFactionState,
            "shiningFactionChronicleUpdates");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.ShiningFaction);

        AssertDoesNotContainIssueCodes(issues, "shining_faction_relevant_actor_missing_strategic_memory_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_NewShiningFactionAcceptsInitialStrategicMemoryWithoutChronicleHistory()
    {
        const string currentFactionState = """
        {
          "availability": "active",
          "factions": [
            {
              "factionId": "faction_dawn_order",
              "name": "Орден Рассвета",
              "strategicMemory": {
                "summary": "Мы начинаем с переговоров, чтобы не объединить соперников против Ордена.",
                "lastUpdatedTurn": 12,
                "recentCampaigns": []
              },
              "chronicle": []
            }
          ]
        }
        """;

        await PrepareShiningFactionActorBrainFixtureAsync(
            currentFactionState,
            """{ "availability": "active", "factions": [] }""");

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        AssertDoesNotContainIssueCodes(issues, "shining_faction_relevant_actor_missing_strategic_memory_delta");
    }

    [Fact]
    public async Task AcceptedTurnReasoning_NamedPlayerSoulDoesNotRequireNonPlayerActorBrainDecision()
    {
        const string soulStateJson = """
        {
          "soulName": "Серебряная Нить Рассвета",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 0
        }
        """;

        await PrepareActorBrainTurnAsync("Я отказываюсь отдавать воспоминание.", soulStateJson);
        ResetValidatedPreTurnSnapshot();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_named_player_soul.json",
            NormalizeSoulStateJson(soulStateJson));
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: Серебряная Нить Рассвета\n- Почему они релевантны: Душа игрока совершает собственное действие.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Других акторов в сцене нет.\n\n## Reasoning\n### Серебряная Нить Рассвета\n- Текущая локация: Море Хаоса.\n- Ситуация: Игрок отказывается отдавать воспоминание.\n- Мысли: Внутренние мысли игрока определяет сам игрок, а не ГМ.\n- Действия: Душа сохраняет воспоминание.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.ShiningFaction);

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("actor_brain_missing_", StringComparison.OrdinalIgnoreCase) == true &&
            string.Equals(issue.Actor, "Серебряная Нить Рассвета", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_FirstShiningPoliticalActorMaterializationCannotBypassActorScope()
    {
        const string soulStateJson = """
        {
          "soulName": "Серебряная Нить Рассвета",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 0
        }
        """;
        const string currentStateJson = """
        {
          "shiningPoliticalActors": [
            {
              "actorId": "radiant_censor",
              "displayName": "Канцлер Лучей",
              "politicalStatus": "Канцлер торговых залов",
              "currentAgenda": "Ограничить доступ гильдий"
            }
          ]
        }
        """;

        await PrepareActorBrainTurnAsync(string.Empty, soulStateJson);
        await WriteRawAsync(ShiningAbodeState.StatePath, currentStateJson);
        ResetValidatedPreTurnSnapshot();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_actor_brain_new_shining_actor_soul.json",
            NormalizeSoulStateJson(soulStateJson));
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Scene-local\n- Релевантные акторы: нет\n- Почему они релевантны: ГМ утверждает, что значимых акторов нет.\n- Акторы вне охвата: Канцлер Лучей\n- Почему они вне охвата: ГМ ошибочно считает первого политического актора фоном.\n",
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_shining_actor_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Канцлер Лучей", StringComparison.OrdinalIgnoreCase));
    }

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
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_shining_actor_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Канцлер Лучей", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AcceptedTurnReasoning_ShiningPoliticalActorSystemDiffRequiresFullActorBrain()
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
            "test_backups/preturn_shining_actor_brain_system_diff.json",
            preTurnShining);
        await WriteRawAsync("output/debug_logs.json", """
        {
          "gm_thoughts_markdown": "## Охват NPC-анализа\n- Режим: Mixed\n- Релевантные акторы: Канцлер Лучей\n- Почему они релевантны: Системный политический цикл меняет его стратегию.\n- Акторы вне охвата: нет\n- Почему они вне охвата: Другие акторы не меняются.\n\n## Reasoning\n### Канцлер Лучей\n- Ситуация: Он видит угрозу своему залу.\n- Мысли: Он выбирает скрытое давление.\n- Действия: Он меняет текущую повестку.\n",
          "timestamp": "2026-05-22T00:00:00Z"
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        AssertContainsIssueCodes(
            issues,
            "actor_brain_missing_profile_inputs",
            "actor_brain_missing_strategy_options",
            "actor_brain_missing_chosen_strategy");
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
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

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
        var issues = await validator.ValidateAcceptedTurnReasoningAsync(GuardianReasoningProfiles.Core);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "structured_shining_faction_update_out_of_scope", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actor, "Орден Рассвета", StringComparison.OrdinalIgnoreCase));
    }

    private async Task PrepareGuardianDialogueActorBrainFixtureAsync(
        string currentMusingsJson,
        string preTurnMusingsJson)
    {
        const string soulStateJson = """
        {
          "soulName": "Серебряная Нить Рассвета",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 0
        }
        """;

        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12,
          "playerAction": "Спросить Элиару, как защитить память."
        }
        """);
        await WriteRawAsync("game_state/meta/soul_state.json", soulStateJson);
        await WriteGuardianRawWithoutValidatedSnapshotAsync(BuildActorBrainGuardianState(currentMusingsJson));

        ResetValidatedPreTurnSnapshot();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_actor_brain_soul_state.json",
            NormalizeSoulStateJson(soulStateJson));
        await WritePreTurnGuardiansTrackedFileAsync(
            "test_backups/preturn_actor_brain_guardians.json",
            BuildActorBrainGuardianState(preTurnMusingsJson));
    }

    private async Task PrepareActorBrainTurnAsync(string playerAction, string soulStateJson)
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "test-session",
          "requestId": "test-request",
          "turnNumber": 12,
          "playerAction": {{System.Text.Json.JsonSerializer.Serialize(playerAction)}}
        }
        """);
        await WriteRawAsync("game_state/meta/soul_state.json", soulStateJson);
    }

    private async Task PrepareMortalNpcActorBrainJournalFixtureAsync(
        string currentJournalJson,
        string preTurnJournalJson)
    {
        const string soulStateJson = """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """;
        const string npcCoreJson = """
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
        """;

        await PrepareActorBrainTurnAsync("Попросить Иветту рассказать о ночном посыльном.", soulStateJson);
        await WriteRawAsync("game_state/npcs/npc_core.json", npcCoreJson);
        await WriteRawAsync("game_state/npcs/npc_journals.json", currentJournalJson);
        ResetValidatedPreTurnSnapshot();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_actor_brain_mortal_positive_soul.json",
            NormalizeSoulStateJson(soulStateJson));
        await WritePreTurnTrackedFileAsync(
            "game_state/npcs/npc_core.json",
            "test_backups/preturn_actor_brain_mortal_positive_core.json",
            npcCoreJson);
        await WritePreTurnTrackedFileAsync(
            "game_state/npcs/npc_journals.json",
            "test_backups/preturn_actor_brain_mortal_positive_journal.json",
            preTurnJournalJson);
        await WriteFullActorBrainDebugLogAsync("Иветта", "Коридор поместья Вальмонт");
    }

    private async Task PrepareAfterlifeResidentActorBrainJournalFixtureAsync(
        string currentJournalJson,
        string preTurnJournalJson,
        string currentRealm = "Chaos Sea")
    {
        var soulStateJson = $$"""
        {
          "soulName": "Серебряная Нить Рассвета",
          "currentRealm": "{{currentRealm}}",
          "currentIncarnation": 0
        }
        """;
        const string residentEntryJson = """
        {
          "residentId": "resident_liora",
          "guardianId": "guardian_azalia",
          "abodeId": "abode_threads",
          "displayName": "Лиора",
          "residentKind": "wayfaring_soul",
          "originType": "traveler_soul",
          "roleLabel": "Вестница",
          "summary": "Слушает нити дорог.",
          "bondLevel": 61,
          "bondTier": "trusted",
          "isPresent": true
        }
        """;
        var currentStateJson = $$"""
        {
          "entries": [{{residentEntryJson}}],
          "thoughtJournal": {{currentJournalJson}}
        }
        """;
        var preTurnStateJson = $$"""
        {
          "entries": [{{residentEntryJson}}],
          "thoughtJournal": {{preTurnJournalJson}}
        }
        """;

        await PrepareActorBrainTurnAsync("Спросить Лиору, почему она боится опоздать.", soulStateJson);
        await WriteRawAsync(GuardianAbodeResidentState.StatePath, currentStateJson);
        ResetValidatedPreTurnSnapshot();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_actor_brain_resident_positive_soul.json",
            NormalizeSoulStateJson(soulStateJson));
        await WritePreTurnTrackedFileAsync(
            GuardianAbodeResidentState.StatePath,
            "test_backups/preturn_actor_brain_resident_positive_state.json",
            preTurnStateJson);
        await WriteFullActorBrainDebugLogAsync("Лиора", "Сад нитей Обители");
    }

    private async Task PrepareAfterlifeEntityActorBrainFixtureAsync(
        string currentProfileState,
        string previousProfileState)
    {
        const string soulStateJson = """
        {
          "soulName": "Серебряная Нить Рассвета",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 0
        }
        """;

        await PrepareActorBrainTurnAsync("Спросить Канцлера Лучей, почему он мешает сделке.", soulStateJson);
        await WriteRawAsync(AfterlifeEntityProfileState.StatePath, currentProfileState);
        ResetValidatedPreTurnSnapshot();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_actor_brain_entity_soul.json",
            NormalizeSoulStateJson(soulStateJson));
        await WritePreTurnTrackedFileAsync(
            AfterlifeEntityProfileState.StatePath,
            "test_backups/preturn_actor_brain_entity_profiles.json",
            previousProfileState);
        await WriteFullActorBrainDebugLogAsync(
            "Канцлер Лучей",
            "Зал Лучей Сияющей Обители",
            "afterlifeActorGoalUpdates");
    }

    private async Task PrepareShiningFactionActorBrainFixtureAsync(
        string currentFactionState,
        string previousFactionState,
        string stateSurface = "shiningFactionStrategicMemoryUpdates")
    {
        const string soulStateJson = """
        {
          "soulName": "Серебряная Нить Рассвета",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 0
        }
        """;

        await PrepareActorBrainTurnAsync("Спросить Орден Рассвета, как он ответит на угрозу.", soulStateJson);
        await WriteRawAsync(ShiningAbodeState.StatePath, currentFactionState);
        ResetValidatedPreTurnSnapshot();
        await WritePreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            "test_backups/preturn_actor_brain_shining_faction_soul.json",
            NormalizeSoulStateJson(soulStateJson));
        await WritePreTurnTrackedFileAsync(
            ShiningAbodeState.StatePath,
            "test_backups/preturn_actor_brain_shining_faction_state.json",
            previousFactionState);
        await WriteFullActorBrainDebugLogAsync(
            "Орден Рассвета",
            "Зал Рассвета Сияющей Обители",
            stateSurface);
    }

    private async Task WriteFullActorBrainDebugLogAsync(
        string actorName,
        string location,
        string? explicitStateSurface = null)
    {
        var journalSurface = explicitStateSurface ?? actorName switch
        {
            "Иветта" => "NPCJournals[].journalEntries[]",
            "Лиора" => "residentThoughtJournalUpdates",
            _ => "guardianThoughtJournalUpdates"
        };
        var thoughts = $$"""
        ## Охват NPC-анализа
        - Режим: Scene-local
        - Релевантные акторы: {{actorName}}
        - Почему они релевантны: Актор непосредственно отвечает игроку и выбирает линию поведения.
        - Акторы вне охвата: нет
        - Почему они вне охвата: Другие акторы не участвуют в сцене.

        ## Actor Brain 2.0
        ### {{actorName}}
        - Текущая локация: {{location}}; остаётся на месте.
        - Ситуация: Игрок задаёт вопрос, на который нельзя ответить без личного решения.
        - Данные профиля: Характер, роль, отношения, предыдущая память и текущее эмоциональное состояние актора.
        - Мотивация: Сохранить собственные интересы и дать правдивый, но безопасный ответ.
        - Ограничения: Актор не знает скрытых намерений игрока и не станет раскрывать чужие тайны без причины.
        - Мысли: Актор сопоставляет просьбу с прошлым опытом и текущим доверием.
        - Варианты стратегий:
          1. Ответить частично. Выгода: разговор продолжится без лишнего риска. Риск: игрок заметит недосказанность.
          2. Отказаться отвечать. Выгода: тайна останется защищена. Риск: отказ разрушит доверие.
        - Выбранная стратегия: Ответить частично и обозначить границу откровенности.
        - Почему альтернативы отвергнуты: Полный отказ слишком сильно повредит отношениям в этой ситуации.
        - Действия: Актор даёт осторожный ответ и наблюдает за реакцией игрока.
        - Изменения состояния: {{journalSurface}} добавляет first-person запись в собственный канонический журнал мыслей актора.
        """;

        await WriteRawAsync("output/debug_logs.json", $$"""
        {
          "gm_thoughts_markdown": {{System.Text.Json.JsonSerializer.Serialize(thoughts)}},
          "timestamp": "2026-07-10T00:00:00Z"
        }
        """);
    }

    private static string BuildActorBrainGuardianState(string musingsJson)
    {
        var guardian = $$"""
        {
          "guardianId": "guard_freeform_guardian_001",
          "canonicalName": "Хранительница Элиара Карт Невозвращения",
          "displayName": "Хранительница Элиара Карт Невозвращения",
          "domain": "Knowledge",
          "originType": "freeform",
          "nameVariants": {
            "default": "Хранительница Элиара Карт Невозвращения",
            "feminine": "Хранительница Элиара Карт Невозвращения",
            "masculine": "Хранительница Элиара Карт Невозвращения",
            "neutral": "Хранительница Элиара Карт Невозвращения"
          },
          "manifestation": {
            "currentDisplayName": "Хранительница Элиара Карт Невозвращения",
            "formFlexibility": "selective",
            "currentPresentationStyle": "freeform",
            "currentPronouns": "она/её",
            "appearanceDescription": "Высокая женщина в дорожной мантии цвета мокрого серебра."
          },
          "manifestationHistory": [],
          "relationshipData": {
            "currentReputation": 0,
            "reputationHistory": [],
            "lastInteraction": "2026-07-10T08:48:55+10:00"
          },
          "abodePower": {
            "currentPower": 35,
            "tier": "Хрупкая",
            "lastUpdatedAt": "2026-07-10T08:48:55+10:00",
            "history": []
          },
          "guardianRelationships": [],
          "gachaSystem": {
            "chargesPerReturn": 1,
            "chargesUsedThisReturn": 0,
            "gachaHistory": []
          },
          "musings": {{musingsJson}}
        }
        """;

        return $$"""
        {
          "guardians": [{{guardian}}],
          "activeGuardian": {{guardian}}
        }
        """;
    }
}
