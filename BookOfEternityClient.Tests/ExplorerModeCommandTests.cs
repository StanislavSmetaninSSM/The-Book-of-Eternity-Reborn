using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerModeCommandTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly GameSettings _settings;
    private readonly StateManager _stateManager;
    private readonly LocalizationManager _loc;
    private readonly TestExplorerConsole _console;
    private readonly StoryService _storyService;
    private readonly WorldDirectiveService _worldDirectiveService;
    private readonly SystemModService _systemModService;
    private readonly NpcTradeService _npcTradeService;
    private readonly GuardianTradeService _guardianTradeService;
    private readonly PendingTurnStateService _pendingTurnStateService;
    private readonly ExplorerMode _explorer;

    public ExplorerModeCommandTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-explorer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _settings = new GameSettings();
        _stateManager = new StateManager(_fs, _settings, NullLogger<StateManager>.Instance);
        _loc = new LocalizationManager { CurrentLanguage = "ru" };
        _console = new TestExplorerConsole();
        _storyService = new StoryService(_fs, NullLogger<StoryService>.Instance);
        _worldDirectiveService = new WorldDirectiveService(_fs, NullLogger<WorldDirectiveService>.Instance);
        _systemModService = new SystemModService(_fs, _settings, NullLogger<SystemModService>.Instance);
        _npcTradeService = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        _guardianTradeService = new GuardianTradeService(_fs, NullLogger<GuardianTradeService>.Instance);
        _pendingTurnStateService = new PendingTurnStateService(_fs, NullLogger<PendingTurnStateService>.Instance);
        _explorer = new ExplorerMode(_stateManager, _fs, _loc,
            npcTradeService: _npcTradeService,
            guardianTradeService: _guardianTradeService,
            storyService: _storyService,
            pendingTurnState: _pendingTurnStateService,
            systemModService: _systemModService,
            worldDirectiveService: _worldDirectiveService,
            console: _console);
    }

    [Theory]
    [InlineData("/душа")]
    [InlineData("/хранители")]
    [InlineData("/реликвии")]
    [InlineData("/инв")]
    [InlineData("/карта")]
    [InlineData("/квесты")]
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

        _console.QueueAnySelection("🧹 Очистить pending setup", "← Назад");
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
    public async Task TryProcessCommand_WorldRules_ClearFlow_UsesAdapterAndRemovesActiveDirectives()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync(WorldDirectiveService.ActiveDirectivesPath, new
        {
            worldTitle = "Текущий Мир",
            settingSummary = "Активное досье для smoke test."
        });

        _console.QueueAnySelection("🧹 Очистить world directives", "← Назад");
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
                reputation = 55,
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
    public async Task TryProcessCommand_InventoryStorageMove_MovesItemIntoStorage()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/inventory/items.json", new
        {
            money = 10,
            items = new[]
            {
                new
                {
                    itemId = "item_apple_001",
                    name = "Яблоко"
                }
            }
        });
        await WriteJsonAsync("game_state/world/current_location.json", new
        {
            name = "Тестовая площадь",
            locationStorages = new[]
            {
                new
                {
                    storageId = "storage_chest_001",
                    name = "Сундук",
                    hasFullAccess = true,
                    contents = Array.Empty<object>()
                }
            }
        });

        _console.QueueSelection("🎒", "📦 Сундук (0 пр.) → управление");
        _console.QueueSelection("Сундук", "📥 Положить предмет в хранилище (1 в инвентаре)", "← Назад к инвентарю");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/инв"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("inventory_storage_move");
        var inventoryRaw = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var locationRaw = await _fs.ReadFileAsync("game_state/world/current_location.json");
        Assert.DoesNotContain("Яблоко", inventoryRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("Яблоко", locationRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_TransportInventoryMove_MovesItemIntoVehicle()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/inventory/items.json", new
        {
            items = new[]
            {
                new
                {
                    itemId = "item_rope_001",
                    name = "Веревка"
                }
            },
            equipment = new { }
        });
        await WriteJsonAsync("game_state/misc/vehicles.json", new
        {
            vehicles = new[]
            {
                new
                {
                    vehicleId = "vehicle_cart_001",
                    name = "Телега",
                    type = "vehicle",
                    isActive = true,
                    inventory = Array.Empty<object>()
                }
            }
        });

        _console.QueueSelection("Действие с транспортом", "🎒 Управлять инвентарём транспорта");
        _console.QueueSelection("Телега", "📥 Положить предмет в транспорт (1 в инвентаре)", "← Назад к транспорту");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/транспорт"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("transport_inventory_move");
        var inventoryRaw = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var vehiclesRaw = await _fs.ReadFileAsync("game_state/misc/vehicles.json");
        Assert.DoesNotContain("Веревка", inventoryRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("Веревка", vehiclesRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_NpcTradeBuy_SucceedsAndMarksOfferSoldOut()
    {
        await SeedNpcTradeStateAsync();
        _console.QueueSelection("Действие", "🛒 Торговать", "🛍 Купить");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/нпс"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("npc_trade_buy");
        var npcRaw = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");
        var inventoryRaw = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var statusRaw = await _fs.ReadFileAsync("game_state/core/player_status.json");
        Assert.Contains("\"soldOut\": true", npcRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"items\"", inventoryRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("\"money\": 500", statusRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_NpcTradeSell_SucceedsAndRemovesSoldItem()
    {
        await SeedNpcTradeStateAsync(includeSellableInventoryItem: true);
        _console.QueueSelection("Выберите раздел", "💰 Продать товары");
        _console.QueueSelection("Действие", "🛒 Торговать", "💰 Продать");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/нпс"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("npc_trade_sell");
        var inventoryRaw = await _fs.ReadFileAsync("game_state/inventory/items.json");
        var statusRaw = await _fs.ReadFileAsync("game_state/core/player_status.json");
        Assert.DoesNotContain("Походный фонарь", inventoryRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("\"money\": 500", statusRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_GuardianTradeBuy_SucceedsAndAddsRelic()
    {
        await SeedGuardianTradeStateAsync();
        _console.QueueSelection("Действие", "🛒 Торговать", "🛍 Купить");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_trade_buy");
        var guardiansRaw = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.Contains("\"soldOut\": true", guardiansRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"stored\"", soulRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_GuardianTradeSell_SucceedsAndRemovesRelic()
    {
        await SeedGuardianTradeStateAsync(includeStoredRelicForSale: true);
        _console.QueueSelection("Выберите раздел", "💰 Продать реликвии");
        _console.QueueSelection("Действие", "🛒 Торговать");
        _console.QueueAnyConfirmResponse(true);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_trade_sell");
        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.DoesNotContain("Реликвия для продажи", soulRaw ?? string.Empty, StringComparison.Ordinal);
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

    [Fact]
    public async Task TryProcessCommand_InkFeathers_RevealFate_CreatesPendingDiceStateAndDeductsFeathers()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal Realm",
            currentIncarnation = 1,
            inkFeathers = new { current = 50 }
        });
        _console.QueueAnySelection("🔮 Открыть Судьбу (−5 🪶)", "✅ Да, потратить", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/перья"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("ink_feathers_reveal_fate");
        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.NotNull(soulRaw);
        Assert.DoesNotContain("\"current\": 50", soulRaw, StringComparison.Ordinal);
        Assert.True(File.Exists(_fs.ResolvePath(PendingTurnStateService.PendingDiceStatePath)));
    }

    private async Task SeedSessionForCommandAsync(string command)
    {
        var isAfterlife = command is "/душа" or "/хранители" or "/реликвии";
        var realm = isAfterlife ? "Chaos Sea" : "Mortal Realm";

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = realm,
            currentIncarnation = 1,
            inkFeathers = new { current = 3 },
            enlightenment = new { currentTier = "Новичок" },
            soulRelics = new
            {
                stored = new[]
                {
                    new
                    {
                        relicId = "relic_test_001",
                        name = "Искра Памяти",
                        description = "Реликвия для smoke test explorer mode.",
                        rarity = "Rare"
                    }
                },
                equipped = Array.Empty<object>()
            }
        });

        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guard_test_azalia",
                    name = "Азалия",
                    domain = "Social",
                    description = "Тестовая хранительница для smoke test explorer mode.",
                    relationshipData = new
                    {
                        currentReputation = 25,
                        lastInteraction = "2026-03-19T00:00:00Z",
                        reputationHistory = Array.Empty<object>()
                    },
                    gachaSystem = new
                    {
                        chargesPerReturn = 1,
                        chargesUsedThisReturn = 0,
                        gachaHistory = Array.Empty<object>()
                    },
                    questManagement = new
                    {
                        activeQuests = Array.Empty<object>(),
                        completedQuests = Array.Empty<object>()
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guard_test_azalia",
                name = "Азалия"
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = (string?)null
            }
        });

        if (command == "/инв")
        {
            await WriteJsonAsync("game_state/inventory/items.json", new
            {
                items = new[]
                {
                    new
                    {
                        itemId = "item_test_sword",
                        name = "Тестовый меч",
                        description = "Обычный клинок для explorer smoke test.",
                        type = "weapon",
                        quality = "Common"
                    }
                }
            });
        }

        if (command == "/карта")
        {
            await WriteJsonAsync("game_state/world/current_location.json", new
            {
                name = "Тестовая площадь",
                description = "Текущая локация для smoke test.",
                coordinates = new { x = 1, y = 2, z = 0 },
                adjacencyMap = Array.Empty<object>()
            });
        }

        if (command == "/квесты")
        {
            await WriteJsonAsync("game_state/quests/regular_quests.json", new
            {
                quests = new[]
                {
                    new
                    {
                        questId = "quest_test_001",
                        questName = "Тестовый контракт",
                        status = "Active",
                        description = "Тестовый квест для smoke test explorer mode.",
                        objectives = new[]
                        {
                            new
                            {
                                description = "Проверить экран квестов",
                                status = "Active"
                            }
                        }
                    }
                }
            });
        }
    }

    private Task SeedMortalStateAsync() => WriteJsonAsync("game_state/meta/soul_state.json", new
    {
        soulName = "Тестовая Душа",
        currentRealm = "Mortal Realm",
        currentIncarnation = 1,
        inkFeathers = new { current = 3 }
    });

    private Task SeedAfterlifeStateAsync() => WriteJsonAsync("game_state/meta/soul_state.json", new
    {
        soulName = "Тестовая Душа",
        currentRealm = "Chaos Sea",
        currentIncarnation = 1,
        inkFeathers = new { current = 3 }
    });

    private async Task SeedNpcTradeStateAsync(bool includeSellableInventoryItem = false)
    {
        await SeedMortalStateAsync();
        await WriteRawJsonAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_merchant_001",
              "name": "Марек",
              "currentLocationId": "loc_market_square",
              "currentLocation": "Рыночная площадь",
              "level": 10,
              "relationshipLevel": 80,
              "characteristics": { "modifiedTrade": 14 },
              "tradeState": {
                "canTrade": true,
                "merchantProfile": "GeneralGoods"
              }
            }
          ]
        }
        """);
        await WriteJsonAsync("game_state/world/current_location.json", new
        {
            locationId = "loc_market_square",
            name = "Рыночная площадь"
        });
        await WriteJsonAsync("game_state/core/player_status.json", new
        {
            money = 500,
            trade = 12
        });
        await WriteJsonAsync("game_state/world/world_time.json", new
        {
            currentTimeInMinutes = 100
        });
        await WriteJsonAsync("game_state/inventory/items.json", includeSellableInventoryItem
            ? new
            {
                items = new[]
                {
                    new
                    {
                        itemId = "item_sell_lantern_001",
                        name = "Походный фонарь",
                        quality = "Common",
                        type = "tool"
                    }
                },
                equipment = new { }
            }
            : new
            {
                items = Array.Empty<object>(),
                equipment = new { }
            });
    }

    private async Task SeedGuardianTradeStateAsync(bool includeStoredRelicForSale = false)
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 500 },
            soulRelics = new
            {
                equipped = Array.Empty<object>(),
                stored = includeStoredRelicForSale
                    ? new object[]
                    {
                        new
                        {
                            relicId = "relic_sell_001",
                            name = "Реликвия для продажи",
                            rarity = "Rare",
                            description = "Подходит для теста продажи."
                        }
                    }
                    : Array.Empty<object>()
            }
        });
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_trade_001",
                    name = "Азалия",
                    domain = "Social",
                    relationshipData = new
                    {
                        currentReputation = 120
                    },
                    abode = new
                    {
                        abodeId = "abode_social_001",
                        name = "Шелковая Обитель"
                    },
                    gachaSystem = new
                    {
                        chargesPerReturn = 1,
                        chargesUsedThisReturn = 0,
                        gachaHistory = Array.Empty<object>()
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_trade_001",
                name = "Азалия"
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_social_001"
            }
        });
    }

    private async Task WriteJsonAsync(string relativePath, object payload)
    {
        await _fs.WriteFileAtomicAsync(relativePath, JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }

    private async Task WriteStoryAsync(string relativePath, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        await _fs.WriteFileAtomicAsync(relativePath, json + Environment.NewLine);
    }

    private Task WriteRawJsonAsync(string relativePath, string json)
    {
        return _fs.WriteFileAtomicAsync(relativePath, json.Replace("\n", Environment.NewLine));
    }

    private string BuildConsoleDiagnostics(string scenario)
    {
        return $"{scenario} | titles: {string.Join(" || ", _console.SelectionTitles)} | ask: {string.Join(" || ", _console.AskPrompts)} | confirm: {string.Join(" || ", _console.ConfirmPrompts)} | markup: {string.Join(" || ", _console.MarkupLines)}";
    }

    private void AssertNoHiddenExplorerErrors(string scenario)
    {
        Assert.False(
            _console.MarkupLines.Any(line => line.Contains("Ошибка при выполнении команды", StringComparison.OrdinalIgnoreCase)),
            BuildConsoleDiagnostics(scenario));
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
            // ignored
        }
    }
}
