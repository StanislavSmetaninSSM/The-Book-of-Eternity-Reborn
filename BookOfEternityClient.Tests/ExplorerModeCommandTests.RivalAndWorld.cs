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
