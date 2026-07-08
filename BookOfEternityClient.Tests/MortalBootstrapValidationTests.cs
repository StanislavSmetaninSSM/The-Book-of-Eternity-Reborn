using System.Text.Json.Nodes;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalBootstrapValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public MortalBootstrapValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-mortal-bootstrap-validation-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(TestRepoPaths.BaseSessionRoot, Path.Combine(_rootPath, "game_session"));

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public void MortalBootstrapStateBuilder_BuildsCanonicalBaselineWithoutKnownRepairLoopShapes()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Мирон, молодой архивариус-изгнанник.",
            worldDescription: "Город-государство у болот и старых руин.",
            startingCircumstances: "Мирон приходит в себя ночью в архивной башне после кражи запретной описи.",
            createdAtUtc: DateTimeOffset.Parse("2026-06-29T07:00:00Z"));

        Assert.Contains("game_state/world/current_location.json", files.Keys);
        Assert.Contains("game_state/world/world_map.json", files.Keys);
        Assert.Contains("game_state/factions/faction_core.json", files.Keys);
        Assert.Contains("game_state/quests/regular_quests.json", files.Keys);
        Assert.Contains("game_state/inventory/items.json", files.Keys);
        Assert.Contains("game_state/player/experience.json", files.Keys);
        Assert.Contains("game_state/player/skills_active.json", files.Keys);
        Assert.Contains("game_state/player/skills_passive.json", files.Keys);
        Assert.Contains("game_state/player/skill_mastery.json", files.Keys);
        Assert.Contains("lore/codex_entries.json", files.Keys);

        var currentLocation = files["game_state/world/current_location.json"];
        Assert.Equal("loc_life_001_start", currentLocation["locationId"]!.GetValue<string>());
        var lastEventsDescription = currentLocation["lastEventsDescription"]!.GetValue<string>();
        Assert.StartsWith("#[3]. Начало смертной жизни:", lastEventsDescription, StringComparison.Ordinal);
        Assert.DoesNotContain("#3 -", lastEventsDescription, StringComparison.Ordinal);

        var faction = files["game_state/factions/faction_core.json"]!["factions"]!.AsArray()[0]!.AsObject();
        Assert.Equal("faction_life_001_initial_context", faction["factionId"]!.GetValue<string>());
        Assert.False(faction.ContainsKey("initialId"));
        Assert.False(faction.ContainsKey("isNewFaction"));

        var factionResources = files["game_state/factions/faction_resources.json"];
        var resourceEntry = Assert.Single(factionResources["entries"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("faction_life_001_initial_context", resourceEntry["factionId"]!.GetValue<string>());
        Assert.Equal("Силы стартовой сцены", resourceEntry["name"]!.GetValue<string>());
        Assert.NotNull(resourceEntry["metaResources"]);
        Assert.NotNull(resourceEntry["strategicGoods"]);

        var quest = files["game_state/quests/regular_quests.json"]!["quests"]!.AsArray()[0]!.AsObject();
        var detailsLog = quest["detailsLog"]!.AsArray();
        Assert.Equal("#[3]. Первая цель новой жизни связала выбранные обстоятельства стартовой сцены.", detailsLog[0]!.GetValue<string>());
        AssertPlayerFacingBootstrapTextIsClean(quest, "fresh mortal bootstrap quest");

        var inventory = files["game_state/inventory/items.json"];
        var item = inventory["items"]!.AsArray().Single()!.AsObject();
        Assert.Equal("item_life_001_opening_anchor", item["itemId"]!.GetValue<string>());
        Assert.Equal("item_life_001_opening_anchor", item["existedId"]!.GetValue<string>());
        Assert.Equal("Common", item["quality"]!.GetValue<string>());
        Assert.Equal("100%", item["durability"]!.GetValue<string>());
        Assert.False(item["isContainer"]!.GetValue<bool>());
        Assert.False(item["isConsumption"]!.GetValue<bool>());
        Assert.False(item["requiresTwoHands"]!.GetValue<bool>());
        Assert.True(item.ContainsKey("equipmentSlot"));
        Assert.True(item.ContainsKey("accessoryForSlot"));
        Assert.True(item.ContainsKey("contentsPath"));
        Assert.Null(item["equipmentSlot"]);
        Assert.Null(item["accessoryForSlot"]);
        Assert.Null(item["contentsPath"]);
        Assert.NotEmpty(item["textContent"]!.AsArray());

        var experience = files["game_state/player/experience.json"];
        Assert.Equal(1, experience["playerLevel"]!.GetValue<int>());
        Assert.Equal(1, experience["level"]!.GetValue<int>());
        Assert.Equal(0, experience["currentExperience"]!.GetValue<int>());
        Assert.Equal(0, experience["experience"]!.GetValue<int>());
        Assert.Equal(0, experience["totalExperience"]!.GetValue<int>());
        Assert.Equal(100, experience["experienceForNextLevel"]!.GetValue<int>());
        Assert.Equal(0, experience["experienceGained"]!.GetValue<int>());

        var activeSkills = files["game_state/player/skills_active.json"];
        Assert.Empty(activeSkills["activeSkillChanges"]!.AsArray());
        Assert.Empty(activeSkills["removeActiveSkills"]!.AsArray());

        var passiveSkills = files["game_state/player/skills_passive.json"];
        Assert.Empty(passiveSkills["passiveSkillChanges"]!.AsArray());
        Assert.Empty(passiveSkills["removePassiveSkills"]!.AsArray());

        var skillMastery = files["game_state/player/skill_mastery.json"];
        Assert.Empty(skillMastery["skillMasteryChanges"]!.AsArray());

        var codexEntries = files["lore/codex_entries.json"]!["entries"]!.AsArray();
        var currentWorldEntry = codexEntries
            .OfType<JsonObject>()
            .Single(entry => string.Equals(
                entry["entryId"]?.GetValue<string>(),
                "codex_life_001_world",
                StringComparison.Ordinal));
        Assert.StartsWith(
            "current_world/",
            currentWorldEntry["sourceFile"]!.GetValue<string>(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void MortalBootstrapStateBuilder_AddsStarterExperienceBufferForPaidTrainingOrTradeStarts()
    {
        var paidStartFiles = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 4,
            characterDescription: "Асурэн де Вальмонт, молодой аристократ-маг.",
            worldDescription: "Столица Этернии с платными уроками навыков и купеческой торговлей.",
            startingCircumstances: "За дверью ждёт наставница, которая продаёт первые уроки через витрину обучения, а рядом купец предлагает купить кинжал и бинты.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-06T02:00:00Z"));

        var paidExperience = paidStartFiles["game_state/player/experience.json"];
        Assert.Equal(25, paidExperience["currentExperience"]!.GetValue<int>());
        Assert.Equal(25, paidExperience["experience"]!.GetValue<int>());
        Assert.Equal(25, paidExperience["totalExperience"]!.GetValue<int>());
        Assert.Equal(100, paidExperience["experienceForNextLevel"]!.GetValue<int>());
        Assert.Equal(0, paidExperience["experienceGained"]!.GetValue<int>());

        var plainStartFiles = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 4,
            characterDescription: "Мирон, молодой архивариус-изгнанник.",
            worldDescription: "Город-государство у болот и старых руин.",
            startingCircumstances: "Мирон приходит в себя ночью в архивной башне после кражи запретной описи.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-06T02:00:00Z"));

        var plainExperience = plainStartFiles["game_state/player/experience.json"];
        Assert.Equal(0, plainExperience["currentExperience"]!.GetValue<int>());
        Assert.Equal(0, plainExperience["experience"]!.GetValue<int>());
        Assert.Equal(0, plainExperience["totalExperience"]!.GetValue<int>());
    }

    [Fact]
    public async Task MortalBootstrapStateBuilder_MaterializesRequestedTeacherIntoBaseline()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Асурэн де Вальмонт, молодой аристократ-маг.",
            worldDescription: "Столица Этернии с городскими наставниками и витринами обучения.",
            startingCircumstances: "За дверью ждёт наставница семейного архива, которая может обучить чтению печатей за плату.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-06T01:00:00Z"));

        var npcCore = Assert.IsType<JsonObject>(files["game_state/npcs/npc_core.json"]);
        var sceneNpcs = Assert.IsType<JsonArray>(npcCore["NPCsInScene"]);
        var teacher = Assert.Single(sceneNpcs.OfType<JsonObject>());
        Assert.Equal("npc_life_001_start_teacher", teacher["npcId"]!.GetValue<string>());
        Assert.Equal("Наставница семейного архива", teacher["name"]!.GetValue<string>());
        Assert.Equal("loc_life_001_start", teacher["currentLocationId"]!.GetValue<string>());
        Assert.Contains("витрин", teacher["summary"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(25, teacher["relationshipLevel"]!.GetValue<int>());
        Assert.Equal("Нейтралитет", teacher["attitude"]!.GetValue<string>());

        var teacherProfile = Assert.IsType<JsonObject>(teacher["teacherProfile"]);
        Assert.True(teacherProfile["canTeach"]!.GetValue<bool>());
        Assert.Equal(25, teacherProfile["relationshipLevel"]!.GetValue<int>());
        Assert.Contains("чтению печатей", teacherProfile["summary"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        var taughtSkill = Assert.Single(teacherProfile["skills"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("skill_life_001_seal_reading", taughtSkill["skillId"]!.GetValue<string>());
        Assert.Equal("Чтение печатей", taughtSkill["skillName"]!.GetValue<string>());
        Assert.Equal("Чтение печатей", taughtSkill["displayName"]!.GetValue<string>());
        Assert.Equal("passive_skill_mastery", taughtSkill["skillKind"]!.GetValue<string>());
        Assert.Equal(2, taughtSkill["masteryLevel"]!.GetValue<int>());

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/mortal_bootstrap_scaffold.json", """
        {
          "schemaVersion": 1,
          "purpose": "fresh_mortal_world_bootstrap",
          "baselineMaterializedBeforeDispatch": true,
          "playerAuthoredStart": {
            "characterDescription": "Асурэн де Вальмонт, молодой аристократ-маг.",
            "worldDescription": "Столица Этернии с городскими наставниками и витринами обучения.",
            "startingCircumstances": "За дверью ждёт наставница семейного архива, которая может обучить чтению печатей за плату."
          },
          "trainingAnchorRequirements": {
            "requiredNpcShape": "The relevant NPC in NPCsInScene/UpdateNPCs must include teacherProfile with canTeach=true."
          }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_requested_teacher_missing", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "npc_attitude_relationship_tier_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_LocalUiSessionLock_IsClientOwnedAndDoesNotFailStateContract()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Асурэн де Вальмонт, молодой аристократ-маг.",
            worldDescription: "Столица Этернии с городскими наставниками и витринами обучения.",
            startingCircumstances: "За дверью ждёт наставница семейного архива, которая может обучить чтению печатей за плату.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-06T01:00:00Z"));

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await new LocalUiSessionLockService(_fs).AcquireOrRefreshAsync(
            new LocalUiSessionLockOwner("console:test", "console", "Консольный тест", TimeSpan.FromMinutes(2)),
            "Команда /обучение");
        await TrainingRequestState.WriteRequestAsync(
            _fs,
            requestKind: "mortal_teacher_showcase",
            sourceActorId: "npc_life_001_start_teacher",
            sourceActorName: "Наставница семейного архива",
            sourceActorKind: "npc_teacher",
            realm: "mortal",
            createdAtTurn: 1,
            sourceActorSnapshotHash: "test-snapshot-hash",
            reason: "missing_showcase");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.FilePath, LocalUiSessionLockService.LockPath, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(issue.Code, "missing_allowed_top_level_key", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(issue.Code, "strict_state_missing_allowed_top_level_key", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.FilePath, TrainingRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(issue.Code, "missing_allowed_top_level_key", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(issue.Code, "strict_state_missing_allowed_top_level_key", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void MortalBootstrapStateBuilder_MaterializesExplicitTrackerCompetencyAsPassiveSkill()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 5,
            characterDescription: "Молодая женщина-следопыт из обедневшего дворянского рода: зовут Асурэн де Вальмонт, носит дорожный плащ, умеет читать следы и скрывать страх за вежливостью.",
            worldDescription: "Тёмное фэнтези позднего средневековья.",
            startingCircumstances: "Асурэн приходит в себя в дорожной харчевне у северных ворот.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-05T01:00:00Z"));

        var passiveSkills = files["game_state/player/skills_passive.json"];
        var skills = passiveSkills["passiveSkillChanges"]!.AsArray();
        var tracking = Assert.Single(skills.OfType<JsonObject>(), skill =>
            string.Equals(skill["skillName"]?.GetValue<string>(), "Чтение следов", StringComparison.Ordinal));

        Assert.Equal("KnowledgeBased", tracking["type"]!.GetValue<string>());
        Assert.Equal("Полевые навыки", tracking["group"]!.GetValue<string>());
        Assert.Equal(1, tracking["masteryLevel"]!.GetValue<int>());
        Assert.Equal(5, tracking["maxMasteryLevel"]!.GetValue<int>());
        Assert.Contains("след", tracking["skillDescription"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Восприятие", tracking["playerStatBonus"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        var structuredBonuses = tracking["structuredBonuses"]!.AsArray();
        var bonus = Assert.Single(structuredBonuses.OfType<JsonObject>());
        Assert.Equal("Characteristic", bonus["bonusType"]!.GetValue<string>());
        Assert.Equal("perception", bonus["target"]!.GetValue<string>());
        Assert.Equal("Восприятие", bonus["targetDisplayName"]!.GetValue<string>());
        Assert.Equal("Flat", bonus["valueType"]!.GetValue<string>());
        Assert.Equal("Permanent", bonus["application"]!.GetValue<string>());
        Assert.Equal(1, bonus["value"]!.GetValue<int>());
        Assert.Empty(files["game_state/player/skills_active.json"]["activeSkillChanges"]!.AsArray());
        Assert.Empty(files["game_state/player/skill_mastery.json"]["skillMasteryChanges"]!.AsArray());
    }

    [Fact]
    public async Task ValidateGameStateAsync_FreshMortalBootstrapStarterSkillsAreCanonical()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 5,
            characterDescription: "Молодая женщина-следопыт из обедневшего дворянского рода: умеет читать следы и скрывать страх за вежливостью.",
            worldDescription: "Тёмное фэнтези позднего средневековья.",
            startingCircumstances: "Асурэн приходит в себя в дорожной харчевне у северных ворот.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-05T01:00:00Z"));

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.FilePath is not null &&
            issue.FilePath.Contains("skills_", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "passive_skill_missing_structured_bonuses", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "passive_skill_missing_player_stat_bonus_mirror", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_OutdoorLocationMissingBiome_ReportsSpecificRepairableIssue()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 5,
            characterDescription: "Лира Сурожская, молодая следопытка.",
            worldDescription: "Пограничная деревня у сырого леса и заброшенной сторожки.",
            startingCircumstances: "Лира выходит из охотничьего двора к лесной дороге.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-06T04:00:00Z"));

        var currentLocation = files["game_state/world/current_location.json"]!.AsObject();
        currentLocation["type"] = "outdoor";
        currentLocation["locationType"] = "outdoor";
        currentLocation.Remove("biome");

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Северная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        var biomeIssue = Assert.Single(issues, issue =>
            string.Equals(issue.FilePath, "game_state/world/current_location.json.biome", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("location_outdoor_biome_missing", biomeIssue.Code);
        Assert.Equal("Location", biomeIssue.Section);
        Assert.Contains("TemperateForest", biomeIssue.Expected, StringComparison.Ordinal);
        Assert.Contains("canonical biome", biomeIssue.RepairHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateGameStateAsync_CanonicalFactionWithOnlyTemporaryIdentity_ReportsBlockingIssue()
    {
        await _fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", new JsonObject
        {
            ["factions"] = new JsonArray
            {
                new JsonObject
                {
                    ["factionId"] = null,
                    ["initialId"] = "temp-faction-merchant-guild",
                    ["isNewFaction"] = true,
                    ["name"] = "Купеческая гильдия порта",
                    ["description"] = "Гильдия долговых книг и архивных печатей.",
                    ["image_prompt"] = "merchant guild archive seal, gothic port fantasy",
                    ["level"] = 1,
                    ["experience"] = 0,
                    ["experienceForNextLevel"] = 100,
                    ["developmentArchetype"] = "mercantile_archive_guild",
                    ["isPlayerFaction"] = false,
                    ["isPlayerMember"] = false,
                    ["reputation"] = 0,
                    ["powerProfile"] = new JsonObject
                    {
                        ["military"] = 1,
                        ["economic"] = 10,
                        ["social"] = 4,
                        ["covert"] = 2,
                        ["logistics"] = 5,
                        ["stability"] = 7,
                        ["arcane_tech"] = 1,
                        ["exploration"] = 1
                    },
                    ["resources"] = new JsonObject
                    {
                        ["wealth"] = 5,
                        ["metaResources"] = new JsonArray(),
                        ["strategicGoods"] = new JsonArray()
                    },
                    ["ranks"] = new JsonObject
                    {
                        ["branches"] = new JsonArray()
                    }
                }
            }
        }.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            string.Equals(issue.Code, "canonical_faction_core_requires_permanent_faction_id", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertPlayerFacingBootstrapTextIsClean(JsonNode? node, string context)
    {
        var forbiddenTerms = new[] { "bootstrap", "baseline", "client", "клиент", "scaffold" };
        foreach (var text in EnumerateJsonStrings(node))
        {
            foreach (var term in forbiddenTerms)
            {
                Assert.DoesNotContain(term, text, StringComparison.OrdinalIgnoreCase);
            }
        }

        static IEnumerable<string> EnumerateJsonStrings(JsonNode? current)
        {
            switch (current)
            {
                case JsonValue value when value.TryGetValue<string>(out var text):
                    yield return text;
                    break;
                case JsonArray array:
                    foreach (var item in array)
                    foreach (var text in EnumerateJsonStrings(item))
                        yield return text;
                    break;
                case JsonObject obj:
                    foreach (var property in obj)
                    foreach (var text in EnumerateJsonStrings(property.Value))
                        yield return text;
                    break;
            }
        }
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentCodexStoredEntriesCanReferenceOtherCurrentEntries()
    {
        var preTurnCodex = """
        {
          "entries": [
            {
              "entryId": "codex_life_001_world",
              "title": "Текущий смертный мир",
              "category": "geography",
              "content": "Базовая запись текущего мира.",
              "summary": "Базовая запись текущего мира.",
              "sourceFile": "current_world/world_setting.json",
              "relatedEntries": []
            }
          ]
        }
        """;

        var currentCodex = """
        {
          "entries": [
            {
              "entryId": "codex_life_001_world",
              "title": "Текущий смертный мир",
              "category": "geography",
              "content": "Базовая запись текущего мира.",
              "summary": "Базовая запись текущего мира.",
              "sourceFile": "current_world/world_setting.json",
              "relatedEntries": []
            },
            {
              "entryId": "codex_life_001_ethernia",
              "title": "Этерния",
              "category": "geography",
              "content": "Город-государство дворянских домов и магических гильдий.",
              "summary": "Город-государство дворянских домов и магических гильдий.",
              "sourceFile": "current_world/geography.json",
              "relatedEntries": [ "codex_life_001_world" ]
            },
            {
              "entryId": "codex_life_001_house_valmont",
              "title": "Дом Вальмонт",
              "category": "factions",
              "content": "Дворянский дом, связанный с архивами и руническими тайнами.",
              "summary": "Дворянский дом, связанный с архивами и руническими тайнами.",
              "sourceFile": "current_world/cultures.json",
              "relatedEntries": [ "codex_life_001_ethernia" ]
            }
          ]
        }
        """;

        await WriteValidatedSnapshotManifestAsync(("lore/codex_entries.json", preTurnCodex));
        await _fs.WriteFileAtomicAsync("lore/codex_entries.json", currentCodex);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "codex_related_entry_unknown_target", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actual, "codex_life_001_ethernia", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ClientOwnedMortalBootstrapBaselineLoreDoesNotRequireGmRewrite()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Лира Вальмонт, молодая дворянка-маг.",
            worldDescription: "Поместье Вальмонт в столице Этернии.",
            startingCircumstances: "Лира просыпается в своих покоях после тревожных снов.",
            createdAtUtc: DateTimeOffset.Parse("2026-06-29T07:00:00Z"));

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        var bootstrapLoreFiles = new[]
        {
            "lore/current_world/world_setting.json",
            "lore/current_world/geography.json",
            "lore/current_world/history.json",
            "lore/current_world/cultures.json",
            "lore/current_world/threats.json",
            "lore/codex_entries.json"
        };

        await WriteValidatedSnapshotManifestAsync(
            sourceLabel: "GM-инициированного воплощения",
            includeSnapshotFilesAsRollbackBaseline: false,
            bootstrapLoreFiles.Select(path => (Path: path, Json: files[path].ToJsonString())).ToArray());
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "sessionId": "session_mortal_bootstrap_validation_tests",
          "requestId": "request_mortal_bootstrap_validation_tests",
          "turnNumber": 3
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_reused_previous_world_lore", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AcceptedMortalBootstrapWithPlayerVisiblePlaceholderNames_ReportsBlockingIssues()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Эйра, молодая городская писарка при архиве купеческой гильдии.",
            worldDescription: "Портовый город-государство с купеческими гильдиями, архивами и тайными культами.",
            startingCircumstances: "Эйра просыпается до рассвета в комнате при архиве; на столе лежит чужая опечатанная расписка.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-09T01:00:00Z"));

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/mortal_bootstrap_scaffold.json", """
        {
          "schemaVersion": 1,
          "purpose": "fresh_mortal_world_bootstrap",
          "playerAuthoredStart": {
            "characterDescription": "Эйра, молодая городская писарка при архиве купеческой гильдии.",
            "worldDescription": "Портовый город-государство с купеческими гильдиями, архивами и тайными культами.",
            "startingCircumstances": "Эйра просыпается до рассвета в комнате при архиве; на столе лежит чужая опечатанная расписка."
          }
        }
        """);

        var bootstrapLoreFiles = new[]
        {
            "lore/current_world/world_setting.json",
            "lore/current_world/geography.json",
            "lore/current_world/history.json",
            "lore/current_world/cultures.json",
            "lore/current_world/threats.json",
            "lore/codex_entries.json"
        };

        await WriteValidatedSnapshotManifestAsync(
            sourceLabel: "GM-инициированного воплощения",
            includeSnapshotFilesAsRollbackBaseline: false,
            bootstrapLoreFiles.Select(path => (Path: path, Json: files[path].ToJsonString())).ToArray());
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "sessionId": "session_mortal_bootstrap_validation_tests",
          "requestId": "request_mortal_bootstrap_validation_tests",
          "turnNumber": 3
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            string.Equals(issue.Code, "mortal_bootstrap_placeholder_player_visible_name", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/world/current_location.json.name", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actual, "Стартовая сцена новой жизни", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            string.Equals(issue.Code, "mortal_bootstrap_placeholder_player_visible_name", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("game_state/factions/faction_core.json", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actual, "Силы стартовой сцены", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            string.Equals(issue.Code, "mortal_bootstrap_placeholder_player_visible_name", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("world_map.json", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.Actual, "Путь из стартовой сцены", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ClientOwnedMortalBootstrapBaselineWithPlaceholderNames_DoesNotReportAcceptedBootstrapPlaceholderIssue()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Эйра, молодая городская писарка при архиве купеческой гильдии.",
            worldDescription: "Портовый город-государство с купеческими гильдиями, архивами и тайными культами.",
            startingCircumstances: "Эйра просыпается до рассвета в комнате при архиве; на столе лежит чужая опечатанная расписка.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-09T01:00:00Z"));

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/mortal_bootstrap_scaffold.json", """
        {
          "schemaVersion": 1,
          "purpose": "fresh_mortal_world_bootstrap",
          "playerAuthoredStart": {
            "characterDescription": "Эйра, молодая городская писарка при архиве купеческой гильдии.",
            "worldDescription": "Портовый город-государство с купеческими гильдиями, архивами и тайными культами.",
            "startingCircumstances": "Эйра просыпается до рассвета в комнате при архиве; на столе лежит чужая опечатанная расписка."
          }
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "mortal_bootstrap_placeholder_player_visible_name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MortalBootstrapRequestedTeacherWithoutTeacherProfile_ReportsTrainingSurfaceIssue()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 3,
            characterDescription: "Асурэн де Вальмонт, молодой аристократ-маг.",
            worldDescription: "Этерния: темное фэнтези с учителями навыков и витринами обучения.",
            startingCircumstances: "За дверью ждёт наставница Селина Орвейн, которая может обучать магической диагностике, быстрым выпадам и этикету через витрину обучения.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-06T01:00:00Z"));

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        var npcCorePath = _fs.ResolvePath("game_state/npcs/npc_core.json");
        if (File.Exists(npcCorePath))
            File.Delete(npcCorePath);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/mortal_bootstrap_scaffold.json", """
        {
          "schemaVersion": 1,
          "purpose": "fresh_mortal_world_bootstrap",
          "playerAuthoredStart": {
            "characterDescription": "Асурэн де Вальмонт, молодой аристократ-маг.",
            "worldDescription": "Этерния: темное фэнтези с учителями навыков и витринами обучения.",
            "startingCircumstances": "За дверью ждёт наставница Селина Орвейн, которая может обучать магической диагностике, быстрым выпадам и этикету через витрину обучения."
          },
          "trainingAnchorRequirements": {
            "requiredNpcShape": "The relevant NPC in NPCsInScene/UpdateNPCs must include teacherProfile with canTeach=true."
          }
        }
        """);

        var bootstrapLoreFiles = new[]
        {
            "lore/current_world/world_setting.json",
            "lore/current_world/geography.json",
            "lore/current_world/history.json",
            "lore/current_world/cultures.json",
            "lore/current_world/threats.json",
            "lore/codex_entries.json"
        };

        await WriteValidatedSnapshotManifestAsync(
            sourceLabel: "GM-инициированного воплощения",
            includeSnapshotFilesAsRollbackBaseline: false,
            bootstrapLoreFiles.Select(path => (Path: path, Json: files[path].ToJsonString())).ToArray());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            string.Equals(issue.Code, "mortal_bootstrap_requested_teacher_missing", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(issue.FilePath, "game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NpcSkillStringEntries_ReportShapeIssuesInsteadOfThrowing()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Северная Искра",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "NPCId": "npc_life_001_old_miron",
              "name": "Старый Мирон",
              "image_prompt": "old hunter mentor",
              "rarity": "Common",
              "worldview": "Осторожность важнее бравады.",
              "personalityArchetype": "stern_practical_mentor",
              "culturalStance": "Pragmatist",
              "race": "Человек",
              "class": "Охотник",
              "appearanceDescription": "Седой охотник с дорожным ножом.",
              "history": "Много лет водит артель по границе леса.",
              "progressionType": "static_teacher_npc",
              "currentLocationId": "loc_life_001_start",
              "initialLocationId": null,
              "age": 57,
              "level": 2,
              "experience": 0,
              "experienceForNextLevel": 150,
              "relationshipLevel": 0,
              "attitude": "Нейтралитет",
              "playerCompanionDirective": "not_companion",
              "culturalLayer": "приграничная охотничья артель",
              "personalityTraits": [],
              "maxWeight": 35,
              "totalWeight": 0,
              "isOverloaded": false,
              "progressionTrackers": {},
              "plans": "Обучить Лиру осторожности.",
              "personalQuests": [],
              "relationshipLock": {
                "isLocked": false,
                "breakthroughQuestId": null
              },
              "characteristics": {
                "strength": 12,
                "dexterity": 13
              },
              "activeSkills": [
                "Короткий выпад копьём"
              ],
              "passiveSkills": [
                "Чтение следов"
              ],
              "equippedItems": {},
              "fateCards": [],
              "inventory": [],
              "goals": {
                "shortTerm": "Проверить готовность Лиры.",
                "longTerm": "Сделать из Лиры осторожную следопытку."
              }
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "expected_object", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("npc_core", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("activeSkills[0]", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "expected_object", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("npc_core", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("passiveSkills[0]", StringComparison.OrdinalIgnoreCase) &&
            issue.RepairHint?.Contains("JSON object", StringComparison.OrdinalIgnoreCase) == true);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private Task WriteValidatedSnapshotManifestAsync(params (string Path, string Json)[] snapshotFiles) =>
        WriteValidatedSnapshotManifestAsync(
            sourceLabel: "mortal bootstrap validation tests",
            includeSnapshotFilesAsRollbackBaseline: true,
            snapshotFiles);

    private async Task WriteValidatedSnapshotManifestAsync(
        string sourceLabel,
        bool includeSnapshotFilesAsRollbackBaseline,
        params (string Path, string Json)[] snapshotFiles)
    {
        const string sessionId = "session_mortal_bootstrap_validation_tests";
        const string requestId = "request_mortal_bootstrap_validation_tests";
        const int turnNumber = 3;
        const string playerAction = "Mortal bootstrap validation test.";

        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": {{JsonSerializer.Serialize(playerAction)}}
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
            if (includeSnapshotFilesAsRollbackBaseline)
                rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-06-29T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = sourceLabel,
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }
}
