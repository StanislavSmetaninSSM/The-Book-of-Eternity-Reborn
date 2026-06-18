using System.Text.Json;
using System.Reflection;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerModeCommandTests : IDisposable
{
    [Fact]
    public async Task TryProcessCommand_WorldNews_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/world/world_events.json", new
        {
            events = new[]
            {
                new
                {
                    title = "Беспорядки на площади",
                    description = "Толпа спорит у центрального фонтана.",
                    severity = "Medium"
                }
            }
        });
        await WriteJsonAsync("game_state/world/current_location.json", new
        {
            name = "Тестовая площадь",
            activeThreats = new[]
            {
                new
                {
                    threatName = "Карманники",
                    dangerLevel = "Low",
                    description = "Несколько карманников работают в толпе."
                }
            }
        });
        await WriteJsonAsync("game_state/npcs/npc_activities.json", new[]
        {
            new
            {
                npcId = "npc_test_001",
                npcName = "Лира",
                currentActivity = "Наблюдает за толпой",
                location = "Тестовая площадь"
            }
        });
        await WriteJsonAsync("game_state/factions/faction_projects.json", new[]
        {
            new
            {
                factionId = "faction_test_001",
                factionName = "Дом Пепла",
                projectName = "Укрепление влияния",
                status = "Active"
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/новости_мира"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("world_news");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]
    public async Task TryProcessCommand_WorldNews_ConsoleExposesSharedEventFlagAndProgressionDrilldowns()
    {
        await SeedMortalStateAsync();
        await SeedRichMortalWorldNewsFilesAsync();
        await _stateManager.RefreshGameStateAsync();

        var overviewResult = await _explorer.TryProcessCommand("/новости_мира");

        Assert.Equal(string.Empty, overviewResult);
        AssertNoHiddenExplorerErrors("world_news_overview_drilldowns");
        var overviewText = ExtractRenderedText();
        Assert.Contains("Новости мира", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Полная запись", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("worldEventsLog", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("worldStateFlags", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("updateWorldProgressionTracker", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Карманники у ворот", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Мира Ключница", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ночные патрули", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Беспорядки у Северных ворот", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Праздник стих после тревоги", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Дорога к Серебряному броду", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/world", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(_console.SelectionChoicesHistory, history =>
            history.Title.Contains("Новости мира", StringComparison.OrdinalIgnoreCase) &&
            history.Choices.Any(choice => choice.Contains("Открыть событие", StringComparison.OrdinalIgnoreCase) &&
                                          choice.Contains("Беспорядки у Северных ворот", StringComparison.OrdinalIgnoreCase)) &&
            history.Choices.Any(choice => choice.Contains("Осмотреть флаг", StringComparison.OrdinalIgnoreCase) &&
                                          choice.Contains("Праздник стих после тревоги", StringComparison.OrdinalIgnoreCase)) &&
            history.Choices.Any(choice => choice.Contains("Открыть прогресс", StringComparison.OrdinalIgnoreCase) &&
                                          choice.Contains("Дорога к Серебряному броду", StringComparison.OrdinalIgnoreCase)));

        _console.Rendered.Clear();
        _console.MarkupLines.Clear();

        var detailResult = await _explorer.TryProcessCommand("/новости_мира событие riots_at_gate");

        Assert.Equal(string.Empty, detailResult);
        AssertNoHiddenExplorerErrors("world_news_event_detail");
        var detailText = ExtractRenderedText();
        Assert.Contains("Событие: Беспорядки у Северных ворот", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("торговая площадь закрыта до следующего утра", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eventId", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/world", detailText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_WorldNews_ConsoleSelectionRendersSelectedDetail()
    {
        await SeedMortalStateAsync();
        await SeedRichMortalWorldNewsFilesAsync();
        await _stateManager.RefreshGameStateAsync();
        _console.QueueSelection("Новости мира", "Открыть событие «Беспорядки у Северных ворот»");

        var result = await _explorer.TryProcessCommand("/новости_мира");

        Assert.Equal(string.Empty, result);
        AssertNoHiddenExplorerErrors("world_news_selection_detail");
        var text = ExtractRenderedText();
        Assert.Contains("Новости мира", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Событие: Беспорядки у Северных ворот", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("торговая площадь закрыта до следующего утра", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("worldEventsLog", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/world", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConsoleWorldNewsSource_UsesSharedMortalWorldNewsResultBuilder()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "UI",
            "ExplorerMode",
            "ExplorerMode.FactionsAndWorldNews.cs"));

        Assert.Contains("ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(commandLine", source, StringComparison.Ordinal);
        Assert.Contains("ExplorerCommandResultConsoleRenderer.Render(_console", source, StringComparison.Ordinal);
        Assert.Contains("PromptWorldNewsDetailAsync(result)", source, StringComparison.Ordinal);
    }

    [Fact]

    public async Task TryProcessCommand_SoulQuests_ShowsRivalArcMarkerForLinkedSoulQuest()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/quests/soul_quests.json", new
        {
            quests = new[]
            {
                new
                {
                    questId = "soul_quest_rival_001",
                    guardianId = "guard_social_azalia_001",
                    questName = "Остановить Алого Палача",
                    title = "Остановить Алого Палача",
                    description = "След чужой души стал слишком опасен.",
                    status = "active",
                    relatedRivalArcId = "arc_hunt_001",
                    counterToRivalArc = true,
                    progress = new { completed = 0, total = 3 },
                    rewards = new { experience = 100 },
                    objectives = new[]
                    {
                        new { description = "Найти след охотника", status = "Active" }
                    }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/квесты_души"));

        Assert.Null(ex);
        Assert.Contains(_console.SelectionChoicesHistory.SelectMany(entry => entry.Choices),
            choice => choice.Contains("🧵 🌟", StringComparison.Ordinal) &&
                      choice.Contains("Остановить Алого Палача", StringComparison.Ordinal));
    }

    [Fact]

    public async Task TryProcessCommand_RivalThreads_ShowsVisibleArcChoicesOnly()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/world/rival_soul_arcs.json", new
        {
            arcs = new object[]
            {
                new
                {
                    arcId = "arc_visible_001",
                    scope = "major",
                    arcType = "hostile_hunt",
                    status = "rising",
                    sponsorGuardianRef = new { mode = "guardianId", guardianId = "guard_social_azalia_001", displayName = "Азалия" },
                    rivalSoul = new { rivalSoulId = "soul_visible_001", displayNameOrMoniker = "Алый Палач", roleSummary = "Охотник", isKnownToPlayer = true },
                    objective = "Найти игрока",
                    playerIntersection = new { targetsPlayerDirectly = true, stakes = "Смертельная угроза", canBecomeSoulQuest = true, recommendedCounterQuestTone = "urgent" },
                        milestones = new[]
                        {
                            new { stage = 0, title = "Слухи", summary = "О нем говорят.", visibleToPlayer = true, turn = 3 }
                        },
                        currentStage = 0,
                        publicSignals = new[]
                        {
                            new { signalId = "signal_visible_001", stage = 0, description = "Следы охоты.", source = "rumor", visibleToPlayer = true, turn = 4, timestamp = "2026-03-21T18:15:00Z" }
                        },
                        resolution = new { outcome = "ongoing", notes = "Охота продолжается." }
                    },
                new
                {
                    arcId = "arc_hidden_001",
                    scope = "minor",
                    arcType = "political_claim",
                    status = "latent",
                    sponsorGuardianRef = new { mode = "guardianId", guardianId = "guard_social_azalia_001", displayName = "Азалия" },
                    rivalSoul = new { rivalSoulId = "soul_hidden_001", displayNameOrMoniker = "Тайный Претендент", roleSummary = "Претендент", isKnownToPlayer = false },
                    objective = "Захватить город",
                    playerIntersection = new { targetsPlayerDirectly = false, stakes = "Городская власть", canBecomeSoulQuest = false, recommendedCounterQuestTone = "political" },
                    milestones = new[]
                    {
                        new { stage = 0, title = "Тайный шепот", summary = "Игрок пока ничего не знает.", visibleToPlayer = false }
                    },
                    currentStage = 0,
                    publicSignals = Array.Empty<object>(),
                    resolution = new { outcome = "ongoing", notes = "Пока скрыт." }
                }
            }
        });
        await WriteJsonAsync("game_state/quests/soul_quests.json", new
        {
            quests = new[]
            {
                new
                {
                    questId = "soul_quest_rival_001",
                    guardianId = "guard_social_azalia_001",
                    questName = "Остановить Алого Палача",
                    title = "Остановить Алого Палача",
                    description = "След чужой души стал слишком опасен.",
                    status = "active",
                    relatedRivalArcId = "arc_visible_001",
                    counterToRivalArc = true,
                    progress = new { completed = 0, total = 3 },
                    rewards = new { experience = 100 },
                    objectives = new[]
                    {
                        new { description = "Найти след охотника", status = "Active" }
                    }
                }
            }
        });
        await WriteJsonAsync("game_state/world/world_events.json", new
        {
            worldEventsLog = new[]
            {
                new
                {
                    eventId = "event_rival_001",
                    title = "Кровавый знак на воротах",
                    description = "Горожане шепчутся о метке охотника.",
                    visibility = "Public",
                    relatedRivalArcId = "arc_visible_001",
                    timestamp = "2026-03-21T18:30:00Z",
                    consequences = new[] { "Жители начали бояться ночных улиц." },
                    followUp = "Стража усилила ночные патрули."
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/чужие_нити"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("rival_threads");
        Assert.Contains(_console.SelectionChoicesHistory.SelectMany(entry => entry.Choices),
            choice => choice.Contains("🆕", StringComparison.Ordinal) &&
                      choice.Contains("Алый Палач", StringComparison.Ordinal) &&
                      choice.Contains("Следы охоты", StringComparison.Ordinal));
        Assert.DoesNotContain(_console.SelectionChoicesHistory.SelectMany(entry => entry.Choices),
            choice => choice.Contains("Тайный Претендент", StringComparison.Ordinal));
    }

    [Fact]

    public async Task TryProcessCommand_RivalThreads_ShowsArcRevealedOnlyThroughLinkedWorldEvent()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/world/rival_soul_arcs.json", new
        {
            arcs = new object[]
            {
                new
                {
                    arcId = "arc_world_event_only_001",
                    scope = "minor",
                    arcType = "political_claim",
                    status = "rising",
                    sponsorGuardianRef = new { mode = "guardianId", guardianId = "guard_social_azalia_001", displayName = "Азалия" },
                    rivalSoul = new { rivalSoulId = "soul_world_event_only_001", displayNameOrMoniker = "Забытый Претендент", roleSummary = "Политический игрок", isKnownToPlayer = false },
                    objective = "Подготовить переворот в городе",
                    playerIntersection = new { targetsPlayerDirectly = false, stakes = "Смена власти", canBecomeSoulQuest = true, recommendedCounterQuestTone = "political" },
                    milestones = new[] { new { stage = 0, title = "Тень у двора", summary = "Игрок ещё не знает всей линии.", visibleToPlayer = false } },
                    currentStage = 1,
                    publicSignals = Array.Empty<object>(),
                    resolution = new { outcome = "ongoing", notes = "Нить раскрывается через новости мира." }
                }
            }
        });
        await WriteJsonAsync("game_state/world/world_events.json", new
        {
            worldEventsLog = new[]
            {
                new
                {
                    eventId = "event_world_event_only_001",
                    title = "Ночной совет за закрытыми дверями",
                    description = "По городу пошли слухи о тайном совете претендента.",
                    relatedRivalArcId = "arc_world_event_only_001",
                    visibility = "player_known",
                    timestamp = "2026-03-21T22:30:00Z",
                    consequences = new[] { "Гвардия усилила караулы у дворца." }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/чужие_нити"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("rival_threads_world_event_only");
        Assert.Contains(_console.SelectionChoicesHistory.SelectMany(entry => entry.Choices),
            choice => choice.Contains("Забытый Претендент", StringComparison.Ordinal) &&
                      choice.Contains("тайном совете претендента", StringComparison.OrdinalIgnoreCase));

        var renderedText = ExtractRenderedText();
        Assert.Contains("Новости мира", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Новость мира: Ночной совет за закрытыми дверями", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("player_known", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]

    public async Task TryProcessCommand_RivalThreads_DoesNotRevealArcOnlyThroughSecretWorldEvent()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/world/rival_soul_arcs.json", new
        {
            arcs = new object[]
            {
                new
                {
                    arcId = "arc_secret_world_event_only_001",
                    scope = "minor",
                    arcType = "political_claim",
                    status = "rising",
                    sponsorGuardianRef = new { mode = "guardianId", guardianId = "guard_social_azalia_001", displayName = "Азалия" },
                    rivalSoul = new { rivalSoulId = "soul_secret_world_event_only_001", displayNameOrMoniker = "Скрытый Претендент", roleSummary = "Политический игрок", isKnownToPlayer = false },
                    objective = "Подготовить переворот в городе",
                    playerIntersection = new { targetsPlayerDirectly = false, stakes = "Смена власти", canBecomeSoulQuest = true, recommendedCounterQuestTone = "political" },
                    milestones = new[] { new { stage = 0, title = "Тень у двора", summary = "Игрок ещё не знает всей линии.", visibleToPlayer = false } },
                    currentStage = 1,
                    publicSignals = Array.Empty<object>(),
                    resolution = new { outcome = "ongoing", notes = "Нить пока остаётся скрытой." }
                }
            }
        });
        await WriteJsonAsync("game_state/world/world_events.json", new
        {
            worldEventsLog = new[]
            {
                new
                {
                    eventId = "event_secret_world_event_only_001",
                    title = "Закрытый совет в подземельях",
                    description = "Секретное событие, о котором игрок ещё не знает.",
                    relatedRivalArcId = "arc_secret_world_event_only_001",
                    visibility = "Secret",
                    timestamp = "2026-03-21T23:15:00Z"
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/чужие_нити"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("rival_threads_secret_world_event_only");
        Assert.DoesNotContain(_console.SelectionChoicesHistory.SelectMany(entry => entry.Choices),
            choice => choice.Contains("Скрытый Претендент", StringComparison.Ordinal));
    }

    private async Task SeedRichMortalWorldNewsFilesAsync()
    {
        await WriteJsonAsync("game_state/world/world_events.json", new
        {
            worldEventsLog = new[]
            {
                new
                {
                    eventId = "riots_at_gate",
                    title = "Беспорядки у Северных ворот",
                    timestamp = "день 42, утро",
                    location = "Северные ворота",
                    visibility = "public",
                    status = "active",
                    category = "городские слухи",
                    description = "Толпа спорит у ворот [red]без разметки[/].",
                    summary = "Стража закрыла торговую площадь.",
                    involvedNPCs = new[] { "Мира Ключница" },
                    affectedFactions = new[] { "Городская стража" },
                    affectedLocations = new[] { "Северные ворота" },
                    consequences = new[] { "торговая площадь закрыта до следующего утра" },
                    followUp = "Капитан ждёт свидетелей."
                }
            }
        });

        await WriteJsonAsync("game_state/world/world_flags.json", new
        {
            worldStateFlags = new[]
            {
                new
                {
                    flagId = "festival_quiet",
                    displayName = "Праздник стих после тревоги",
                    scope = "Северный квартал",
                    status = "active",
                    value = "наблюдают стражники",
                    description = "Музыканты играют тише после ночного письма.",
                    consequence = "Площадь открыта только для жителей."
                }
            }
        });

        await WriteJsonAsync("game_state/world/progression.json", new
        {
            entries = new[]
            {
                new
                {
                    progressionId = "road_silverford",
                    trackerName = "Дорога к Серебряному броду",
                    stageName = "Караваны возвращаются",
                    status = "active",
                    description = "На тракте снова появились торговцы.",
                    changeReason = "Стража разогнала засаду.",
                    consequence = "Цены на соль упали.",
                    timestamp = "день 42"
                }
            }
        });
    }

    [Fact]

    public async Task TryProcessCommand_RivalThreads_SortsMoreDangerousThreadFirst()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/world/rival_soul_arcs.json", new
        {
            arcs = new object[]
            {
                new
                {
                    arcId = "arc_low_001",
                    scope = "major",
                    arcType = "political_claim",
                    status = "latent",
                    sponsorGuardianRef = new { mode = "guardianId", guardianId = "guard_social_azalia_001", displayName = "Азалия" },
                    rivalSoul = new { rivalSoulId = "soul_low_001", displayNameOrMoniker = "Тихий Претендент", roleSummary = "Политический игрок", isKnownToPlayer = true },
                    objective = "Медленно взять город под контроль",
                    playerIntersection = new { targetsPlayerDirectly = false, stakes = "Влияние на город", canBecomeSoulQuest = false, recommendedCounterQuestTone = "political" },
                    milestones = new[] { new { stage = 0, title = "Шёпот двора", summary = "О нем тихо говорят.", visibleToPlayer = true } },
                    currentStage = 0,
                    publicSignals = Array.Empty<object>(),
                    resolution = new { outcome = "ongoing", notes = "Пока скрыт." }
                },
                new
                {
                    arcId = "arc_high_001",
                    scope = "minor",
                    arcType = "hostile_hunt",
                    status = "intersecting",
                    sponsorGuardianRef = new { mode = "guardianId", guardianId = "guard_social_azalia_001", displayName = "Азалия" },
                    rivalSoul = new { rivalSoulId = "soul_high_001", displayNameOrMoniker = "Черный Пёс", roleSummary = "Охотник", isKnownToPlayer = true },
                    objective = "Найти и убить игрока",
                    playerIntersection = new { targetsPlayerDirectly = true, stakes = "Жизнь игрока", canBecomeSoulQuest = true, recommendedCounterQuestTone = "urgent" },
                    milestones = new[] { new { stage = 1, title = "Выход на след", summary = "Охотник рядом.", visibleToPlayer = true } },
                    currentStage = 1,
                    publicSignals = new[] { new { signalId = "signal_high_001", stage = 1, description = "Кто-то уже идет по следу игрока.", source = "rumor", visibleToPlayer = true } },
                    resolution = new { outcome = "ongoing", notes = "Уже пересекается." }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/чужие_нити"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("rival_threads_sort");
        var firstChoice = _console.SelectionChoicesHistory.SelectMany(entry => entry.Choices).FirstOrDefault(choice => !choice.Contains("Назад", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(firstChoice);
        Assert.Contains("Черный Пёс", firstChoice, StringComparison.Ordinal);
    }

}
