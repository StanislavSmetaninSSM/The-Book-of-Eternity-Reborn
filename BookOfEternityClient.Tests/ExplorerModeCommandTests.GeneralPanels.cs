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
    [Theory]
    [InlineData("/душа")]
    [InlineData("/хранители")]
    [InlineData("/сила_обители")]
    [InlineData("/проекты_хранителей")]
    [InlineData("/реликвии")]
    [InlineData("/архив_души")]
    [InlineData("/архив_кандидаты")]
    [InlineData("/инв")]
    [InlineData("/карта")]
    [InlineData("/квесты")]
    [InlineData("/чужие_нити")]
    [InlineData("/коррективы_хранителя")]
    public async Task TryProcessCommand_RendersWithoutException_ForKeyExplorerCommands(string command)
    {
        await SeedSessionForCommandAsync(command);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand(command));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors(command);
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0, $"Command {command} did not render anything.");
    }

    [Fact]

    public async Task TryProcessCommand_CompanionDirective_UpdatesNpcCoreWithoutException()
    {
        await SeedMortalStateAsync();
        await WriteRawJsonAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_companion_001",
              "name": "Лира",
              "progressionType": "Companion",
              "playerCompanionDirective": ""
            }
          ]
        }
        """);

        _console.QueueAnyAskResponse("Прикрывай меня и держись рядом.");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/директива_компаньону"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("companion_directive");
        Assert.True(_console.AskPrompts.Count > 0, BuildConsoleDiagnostics("companion_directive"));
        var raw = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");
        Assert.NotNull(raw);
        Assert.Contains("Прикрывай меня и держись рядом.", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_InMortalRealm_UsesAfterlifeCycleCopy()
    {
        await SeedMortalStateAsync();
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardians_denial_afterlife_cycle_copy");
        Assert.Contains(_console.MarkupLines,
            line => line.Contains("загробном цикле", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(_console.MarkupLines,
            line => line.Contains("только в Море Хаоса", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]

    public async Task TryProcessCommand_FactionDirective_UpdatesFactionCoreWithoutException()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/factions/faction_core.json", new[]
        {
            new
            {
                factionId = "faction_test_001",
                name = "Дом Пепла",
                isPlayerFaction = true,
                playerStrategyDirective = ""
            }
        });

        _console.QueueAnyAskResponse("Сохранять контроль над рынком и не рисковать людьми.");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/директива_фракции"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("faction_directive");
        Assert.True(_console.AskPrompts.Count > 0, BuildConsoleDiagnostics("faction_directive"));
        var raw = await _fs.ReadFileAsync("game_state/factions/faction_core.json");
        Assert.NotNull(raw);
        Assert.Contains("Сохранять контроль над рынком и не рисковать людьми.", raw, StringComparison.Ordinal);
    }

    [Fact]

    public async Task TryProcessCommand_WorldSetup_ClearFlow_UsesAdapterAndRemovesPendingSetup()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync(WorldDirectiveService.PendingSetupPath, new
        {
            mode = "manual",
            worldDirectives = new
            {
                worldTitle = "Тестовый Мир",
                settingSummary = "Подготовка для smoke test."
            }
        });

        _console.QueueAnySelection("🧹 Очистить подготовку мира", "← Назад");
        _console.QueueAnyConfirmResponse(true);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/world_setup"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("world_setup");
        Assert.True(_console.ConfirmPrompts.Any(prompt => prompt.Contains("подготовку следующего мира", StringComparison.OrdinalIgnoreCase)),
            BuildConsoleDiagnostics("world_setup"));
        Assert.True(_console.ClearCalls > 0);
    }

    [Fact]

    public async Task TryProcessCommand_WorldSetup_ClearFlow_CancelPreviewPreservesPendingSetup()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync(WorldDirectiveService.PendingSetupPath, new
        {
            mode = "manual",
            worldDirectives = new
            {
                worldTitle = "Тестовый Мир",
                settingSummary = "Подготовка для smoke test."
            }
        });

        _console.QueueAnySelection("🧹 Очистить подготовку мира", "← Назад");
        _console.QueueAnyConfirmResponse(false);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/world_setup"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("world_setup_clear_cancel");
        Assert.True(_console.ConfirmPrompts.Any(prompt => prompt.Contains("подготовку следующего мира", StringComparison.OrdinalIgnoreCase)),
            BuildConsoleDiagnostics("world_setup_clear_cancel"));

        var raw = await _fs.ReadFileAsync(WorldDirectiveService.PendingSetupPath);
        Assert.NotNull(raw);
        Assert.Contains("\"worldTitle\": \"Тестовый Мир\"", raw, StringComparison.Ordinal);
    }

    [Fact]

    public async Task TryProcessCommand_WorldSetup_EditFlow_PersistsLargeDetailedDescriptionBlock()
    {
        await SeedAfterlifeStateAsync();
        _console.QueueAnySelection("✏️ Создать / редактировать подготовку мира", "↩️ Оставить текущее значение", "↩️ Оставить текущее значение", "✏️ Изменить текст", "← Назад");
        _console.QueueAskResponse("Название мира", "Этернум");
        _console.QueueAskResponse("Жанр", "Dark fantasy");
        _console.QueueAskResponse("Эпоха", "Поздняя бронза");
        _console.QueueAskResponse("Тон", "Мрачный, медитативный");
        _console.QueueAskResponse("Краткая сводка", "Мир башен, глубин и древних договоров.");
        _console.QueueReadLineResponses("\\p");
        _clipboard.Text = "Первый абзац подробного описания мира.\n\nВторой абзац с дополнительными свободными правилами и ограничениями.";
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/world_setup"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("world_setup_edit");
        var raw = await _fs.ReadFileAsync(WorldDirectiveService.PendingSetupPath);
        Assert.NotNull(raw);
        Assert.Contains("\"worldTitle\": \"Этернум\"", raw, StringComparison.Ordinal);
        Assert.Contains("\"settingSummary\": \"Мир башен, глубин и древних договоров.\"", raw, StringComparison.Ordinal);
        Assert.Contains("Первый абзац подробного описания мира.", raw, StringComparison.Ordinal);
        Assert.Contains("Второй абзац с дополнительными свободными правилами и ограничениями.", raw, StringComparison.Ordinal);
        Assert.Contains("detailedWorldDescription", raw, StringComparison.Ordinal);
    }

    [Fact]

    public async Task TryProcessCommand_WorldSetup_EditFlow_CancelPreviewDoesNotCreatePendingSetup()
    {
        await SeedAfterlifeStateAsync();
        _console.QueueAnySelection("✏️ Создать / редактировать подготовку мира", "↩️ Оставить текущее значение", "↩️ Оставить текущее значение", "✏️ Изменить текст", "← Назад");
        _console.QueueAskResponse("Название мира", "Этернум");
        _console.QueueAskResponse("Жанр", "Dark fantasy");
        _console.QueueAskResponse("Эпоха", "Поздняя бронза");
        _console.QueueAskResponse("Тон", "Мрачный, медитативный");
        _console.QueueAskResponse("Краткая сводка", "Мир башен, глубин и древних договоров.");
        _console.QueueReadLineResponses("\\p");
        _console.QueueAnyConfirmResponse(false);
        _clipboard.Text = "Первый абзац подробного описания мира.";
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/world_setup"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("world_setup_edit_cancel");
        Assert.True(_console.ConfirmPrompts.Any(prompt => prompt.Contains("подготовку следующего мира", StringComparison.OrdinalIgnoreCase)),
            BuildConsoleDiagnostics("world_setup_edit_cancel"));

        var raw = await _fs.ReadFileAsync(WorldDirectiveService.PendingSetupPath);
        Assert.Null(raw);
    }

    [Fact]

    public async Task TryProcessCommand_GuardianCorrections_RendersCurrentLifeCorrectionJournal()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync(GuardianCorrectionService.StatePath, new
        {
            lifeIncarnation = 1,
            appliedAt = "2026-03-23T00:00:00Z",
            guardianId = "guard_test_azalia",
            guardianName = "Азалия",
            intent = "friendly",
            reputationAtApplication = 85,
            powerBefore = 62,
            powerAfter = 50,
            baseBudgetPoints = 3,
            remainingBudgetPoints = 1,
            totalAbodePowerSpent = 12,
            summary = "Азалия усилила старт союзной нитью и добрым предзнаменованием.",
            scenarioCoreSnapshot = new
            {
                scenarioCoreAssertions = new[]
                {
                    new { assertionId = "core_1", category = "role_status", value = "Игрок начинает королём", @explicit = true, source = "structured_field" },
                    new { assertionId = "core_2", category = "world_condition", value = "Королевство процветает", @explicit = true, source = "structured_field" }
                },
                openCorrectionSlots = new[]
                {
                    new { slotId = "slot_1", slotType = "ally_thread", maxSeverity = "medium", allowsFriendly = true, allowsHostile = true, sourceAssertionId = "core_1" }
                }
            },
            corrections = new[]
            {
                new
                {
                    correctionId = "corr_1",
                    sourceGuardianId = "guard_test_azalia",
                    sourceGuardianName = "Азалия",
                    intent = "friendly",
                    slotId = "slot_1",
                    slotType = "ally_thread",
                    severity = "medium",
                    budgetCostPoints = 2,
                    abodePowerCost = 12,
                    claimStrength = 5,
                    title = "Союзная нить судьбы (средняя корректива)",
                    summary = "Азалия вносит заметную коррективу: у игрока с самого начала появляется потенциальный союзник или покровитель.",
                    reason = "Азалия благожелательно тратит силу Обители, добавляя совместимую социальную опору в пределах сценарного ядра.",
                    affectsStartAs = "ally_thread"
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/коррективы_хранителя"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_corrections");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0, BuildConsoleDiagnostics("guardian_corrections"));
    }

    [Fact]

    public async Task TryProcessCommand_SystemGuardians_RendersLibraryWithoutException()
    {
        await SeedAfterlifeStateAsync();
        await SeedSystemGuardianPresetAsync("azalia", "Азалия", "Social", "Обитель Неутолимого Пламени");
        await _stateManager.RefreshGameStateAsync();
        _console.QueueAnySelection("← Назад");

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/извечные_хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("system_guardians");
        Assert.True(_console.SelectionTitles.Any(title => title.Contains("Извечные хранители", StringComparison.OrdinalIgnoreCase)),
            BuildConsoleDiagnostics("system_guardians"));
    }

    [Fact]

    public async Task TryProcessCommand_WorldRules_ClearFlow_UsesAdapterAndRemovesActiveDirectives()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync(WorldDirectiveService.ActiveDirectivesPath, new
        {
            worldTitle = "Текущий Мир",
            settingSummary = "Активное досье для smoke test."
        });

        _console.QueueAnySelection("🧹 Очистить досье мира", "← Назад");
        _console.QueueAnyConfirmResponse(true);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/world_rules"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("world_rules");
        Assert.True(_console.ConfirmPrompts.Any(prompt => prompt.Contains("активное досье текущего мира", StringComparison.OrdinalIgnoreCase)),
            BuildConsoleDiagnostics("world_rules"));
        Assert.True(_console.ClearCalls > 0);
    }

    [Fact]

    public async Task TryProcessCommand_Story_RendersReaderWithoutException()
    {
        await SeedMortalStateAsync();
        await WriteStoryAsync(StoryService.GetStoryPath("Mortal Realm", 1), new
        {
            turn = 1,
            timestamp = "2026-03-19T10:00:00Z",
            realm = "Mortal Realm",
            player = "Осмотреть площадь",
            narrative = "Герой вступает на площадь и замечает движение у фонтана.",
            location = "Тестовая площадь"
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/история"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("story");
        Assert.True(_console.ClearCalls > 0);
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_CodexSearch_RendersSearchResultsWithoutException()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("lore/codex_entries.json", new
        {
            entries = new[]
            {
                new
                {
                    entryId = "codex_test_001",
                    title = "Азалия",
                    category = "Guardians",
                    content = "Азалия хранит память о социальных узорах и союзах."
                }
            }
        });

        _console.QueueAnySelection("🔍 Поиск по кодексу", "← Назад");
        _console.QueueAnyAskResponse("Азалия");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/кодекс"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("codex_search");
        Assert.True(_console.AskPrompts.Any(prompt => prompt.Contains("🔍 Поиск", StringComparison.Ordinal)),
            BuildConsoleDiagnostics("codex_search"));
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_Weather_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/world/world_time.json", new
        {
            day = 12,
            monthName = "Листопад",
            year = 137,
            timeOfDay = "Вечер"
        });
        await WriteJsonAsync("game_state/world/weather.json", new
        {
            state = "Rain",
            description = "Мелкий, но упрямый дождь.",
            season = "Осень",
            temperature = "8C",
            wind = "Порывистый",
            visibility = "Средняя"
        });
        await WriteJsonAsync("game_state/world/current_location.json", new
        {
            name = "Тестовая площадь",
            biome = "Город"
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/погода"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("weather");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_Locations_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/world/world_map.json", new
        {
            worldMapUpdates = new
            {
                newLocations = new[]
                {
                    new
                    {
                        locationId = "loc_test_square",
                        name = "Тестовая площадь",
                        locationType = "City",
                        shortDescription = "Шумная площадь с фонтаном.",
                        factionControl = new[]
                        {
                            new
                            {
                                factionName = "Дом Пепла"
                            }
                        }
                    }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/локации"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("locations");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_ItemTexts_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/inventory/item_text_updates.json", new[]
        {
            new
            {
                itemName = "Письмо от Лиры",
                textToAppend = "Встретимся у фонтана до рассвета."
            }
        });
        await WriteJsonAsync("game_state/inventory/items.json", new
        {
            items = new[]
            {
                new
                {
                    itemId = "item_book_001",
                    name = "Дневник путника",
                    textContent = new[]
                    {
                        "Первая запись о долгой дороге.",
                        "Вторая запись о странных снах."
                    }
                }
            }
        });
        await WriteJsonAsync("game_state/npcs/item_journals.json", new[]
        {
            new
            {
                itemName = "Шкатулка",
                journalEntries = new object[]
                {
                    new
                    {
                        timestamp = "2026-03-19T12:00:00Z",
                        @event = "Открытие",
                        description = "Внутри оказался сложенный лист бумаги."
                    }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/книги"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("item_texts");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_SystemMods_RendersDetailLoopWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        _settings.EnabledSystemMods = new List<string> { "test_mod.md" };
        await _fs.WriteFileAtomicAsync("mods/test_mod.md", """
        # Test Mod

        A small system mod used by explorer smoke tests.
        """);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/моды"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("system_mods");
        Assert.True(_console.ClearCalls > 0);
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_Craft_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/inventory/recipes.json", new
        {
            recipes = new[]
            {
                new
                {
                    recipeName = "Лечебная припарка",
                    description = "Простое ремесленное средство.",
                    craftedItemName = "Припарка",
                    recipeRank = "Novice",
                    outputQuantity = 1,
                    timeCost = 15,
                    requiredMaterials = new[]
                    {
                        new { materialName = "Травы", quantity = 2 }
                    }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/ремесло"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("craft");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_Achievements_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/meta/achievements.json", new
        {
            unlockedAchievements = new[]
            {
                new
                {
                    achievementId = "ach_test_001",
                    name = "Первый шаг",
                    description = "Вы сделали первый шаг в этом мире.",
                    category = "story",
                    rarity = "common",
                    icon = "🏆",
                    unlockedAt = "2026-03-19T10:00:00Z"
                }
            },
            trackedProgress = new[]
            {
                new
                {
                    achievementId = "ach_track_001",
                    name = "Долгий путь",
                    description = "Сделайте десять шагов.",
                    category = "exploration",
                    rarity = "uncommon",
                    icon = "📊",
                    progress = new { current = 3, target = 10 }
                }
            },
            stats = new
            {
                totalUnlocked = 1,
                byCategory = new { story = 1 },
                byRarity = new { common = 1 }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/достижения"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("achievements");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_CurrentLocation_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/world/current_location.json", new
        {
            name = "Тестовая площадь",
            locationType = "City",
            biome = "Город",
            description = "Площадь с фонтаном и торговыми рядами.",
            features = new[] { "Фонтан", "Рынок" },
            activeThreats = new[]
            {
                new
                {
                    threatName = "Карманники",
                    description = "Держатся в толпе."
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/где_я"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("current_location");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_Status_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/core/player_status.json", new
        {
            characterName = "Элиан",
            characterRace = "Человек",
            characterClass = "Следопыт",
            healthPercentage = "85%",
            energyPercentage = "70%",
            poisePercentage = "60%",
            currentCondition = "Собран",
            money = 120
        });
        await WriteJsonAsync("game_state/player/experience.json", new
        {
            level = 3,
            totalExperience = 120,
            experienceForNextLevel = 200
        });
        await WriteJsonAsync("game_state/player/computed_characteristics.json", new
        {
            characteristics = new { strength = 6, dexterity = 7, constitution = 6, intelligence = 5, wisdom = 5, faith = 4, attractiveness = 5, trade = 4, persuasion = 4, perception = 7, luck = 5, speed = 6 },
            modifiedCharacteristics = new { strength = 6, dexterity = 7, constitution = 6, intelligence = 5, wisdom = 5, faith = 4, attractiveness = 5, trade = 4, persuasion = 4, perception = 7, luck = 5, speed = 6 },
            permanentlyModifiedCharacteristics = new { strength = 6, dexterity = 7, constitution = 6, intelligence = 5, wisdom = 5, faith = 4, attractiveness = 5, trade = 4, persuasion = 4, perception = 7, luck = 5, speed = 6 },
            unspentStatPoints = 1
        });
        await WriteJsonAsync("game_state/player/effects.json", new[]
        {
            new { effectType = "buff", value = "+5%", duration = 1, effectDescription = "Боевой тонус" }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/статус"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("status");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_Skills_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/player/skills_active.json", new
        {
            activeSkillChanges = new[]
            {
                new
                {
                    skillName = "Рывок",
                    rarity = "Rare",
                    category = "Combat",
                    level = 2,
                    description = "Быстрый рывок вперёд."
                }
            }
        });
        await WriteJsonAsync("game_state/player/skills_passive.json", new
        {
            passiveSkillChanges = new[]
            {
                new
                {
                    skillName = "Острый глаз",
                    rarity = "Uncommon",
                    type = "KnowledgeBased",
                    description = "Вы замечаете детали быстрее других."
                }
            }
        });
        await WriteJsonAsync("game_state/player/skill_mastery.json", new
        {
            skillMasteryChanges = new[]
            {
                new
                {
                    skillName = "Рывок",
                    currentMasteryLevel = 2,
                    experienceTowardsNextLevel = 10,
                    experienceNeededForNextLevel = 20
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/навыки"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("skills");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_Factions_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/factions/faction_core.json", new[]
        {
            new
            {
                factionId = "faction_test_001",
                name = "Дом Пепла",
                reputation = 180,
                level = "II",
                isPlayerMember = true,
                description = "Торговый дом с жёсткой дисциплиной."
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/фракции"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("factions");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
        Assert.Contains(_console.SelectionChoicesHistory.SelectMany(entry => entry.Choices),
            choice => choice.Contains("180 (Сочувствующий)", StringComparison.Ordinal));
    }

    [Fact]

    public async Task TryProcessCommand_StorageAccess_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/misc/storage_access.json", new
        {
            grantStorageAccess = new[]
            {
                new { storageId = "vault_1", playerId = "player_ally" }
            },
            revokeStorageAccess = new[]
            {
                new { storageId = "vault_2", playerId = "player_rival" }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/доступ_к_хранилищам"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("storage_access");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_PlayerInteractions_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/misc/player_interactions.json", new
        {
            otherPlayersInteractions = new
            {
                player_two = new
                {
                    sharedQuestHooks = new[]
                    {
                        new { questName = "Рынок в огне", status = "active" }
                    }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/взаимодействия"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("player_interactions");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_Effects_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/player/effects.json", new[]
        {
            new
            {
                effectType = "buff",
                value = "+10%",
                duration = 2,
                effectDescription = "Боевой подъем"
            }
        });
        await WriteJsonAsync("game_state/player/wounds.json", new[]
        {
            new
            {
                woundName = "Порез",
                severity = "light",
                descriptionOfEffects = "Мешает точным движениям."
            }
        });
        await WriteJsonAsync("game_state/player/custom_states.json", new[]
        {
            new
            {
                stateName = "Голод",
                currentLevel = 2,
                description = "Нужно перекусить."
            }
        });
        await WriteJsonAsync("game_state/player/stealth.json", new
        {
            isActive = true,
            detectionLevel = 20,
            description = "Вы двигаетесь почти бесшумно."
        });
        await WriteJsonAsync("game_state/player/experience.json", new
        {
            experienceGained = 25,
            totalExperience = 125
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/эффекты"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("effects");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_Combat_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/core/player_status.json", new
        {
            healthPercentage = 80,
            energyPercentage = 60,
            currentCondition = "Собран"
        });
        await WriteJsonAsync("game_state/combat/enemies.json", new[]
        {
            new
            {
                name = "Головорез",
                type = "strong",
                currentHealth = 40,
                maxHealth = 50,
                description = "Опасный противник."
            }
        });
        await WriteJsonAsync("game_state/combat/allies.json", new[]
        {
            new
            {
                name = "Лира",
                currentHealth = 25,
                maxHealth = 30
            }
        });
        await WriteJsonAsync("game_state/combat/combat_log.json", new
        {
            combat_log_markdown = "Игрок наносит удар.\nПротивник отступает."
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/бой"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("combat");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_Chronicle_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/meta/character_chronicle.json", new[]
        {
            new
            {
                title = "Первое пробуждение",
                content = "Вы очнулись под шум дождя.",
                timestamp = "2026-03-19T10:00:00Z"
            }
        });
        await WriteJsonAsync("game_state/quests/plot_outline.json", new
        {
            mainArc = new
            {
                summary = "Выяснить, кто управляет рынком.",
                nextImmediateStep = "Найти связного в трактире."
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хроника"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("chronicle");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_BehaviorAssessment_RendersWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/meta/player_behavior.json", new
        {
            historyManipulationCoefficient = 0.25,
            playerBehaviorAssessment = new
            {
                historyManipulationCoefficient = 0.25,
                notes = "Незначительные признаки мета-влияния."
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/поведение"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("behavior_assessment");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

    [Fact]

    public async Task TryProcessCommand_Validation_WithUnavailableService_RendersGracefully()
    {
        await SeedMortalStateAsync();
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/валидация"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("validation_unavailable");
        Assert.Contains(_console.MarkupLines, line => line.Contains("Сервис валидации недоступен", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]

    public async Task TryProcessCommand_LivesHistory_RendersWithoutHiddenErrors()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 2,
            inkFeathers = new { current = 25 },
            livesHistory = new[]
            {
                new
                {
                    incarnation = 1,
                    summary = "Короткая, но яркая жизнь.",
                    endedAt = "2026-03-18T10:00:00Z",
                    turnsLived = 12,
                    characterName = "Элиан",
                    world = "Тестовый мир"
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/жизни"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("lives_history");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
    }

}
