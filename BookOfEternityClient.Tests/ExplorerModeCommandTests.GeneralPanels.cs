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
    public async Task TryProcessCommand_MortalOnlyCommand_BlankRealmFailsClosed()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "",
            currentIncarnation = 1
        });
        await _stateManager.RefreshGameStateAsync();

        var result = await _explorer.TryProcessCommand("/инв");

        Assert.Equal("", result);
        Assert.Contains(_console.MarkupLines,
            line => line.Contains("currentRealm", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("не определ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_MortalOnlyCommand_MissingRealmFailsClosed()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentIncarnation = 1
        });
        await _stateManager.RefreshGameStateAsync();

        var result = await _explorer.TryProcessCommand("/карта");

        Assert.Equal("", result);
        Assert.Contains(_console.MarkupLines,
            line => line.Contains("currentRealm", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("не определ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_MapRussian_InMortalRealm_OpensVisualMapViewerInsteadOfLocationList()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/world/current_location.json", new
        {
            locationId = "loc_market_square",
            name = "Рыночная площадь",
            description = "Открытая смертная локация для проверки карты.",
            lastEventsDescription = "#[9] - Дозор проверил площадь после полуночи.",
            coordinates = new { x = 1, y = 2, z = 0 },
            adjacencyMap = new[]
            {
                new
                {
                    targetLocationId = "loc_east_gate",
                    name = "Восточные ворота",
                    direction = "восток",
                    distance = "10 минут",
                    linkState = "safe"
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var result = await _explorer.TryProcessCommand("/карта");

        Assert.Equal(string.Empty, result);
        Assert.DoesNotContain(_console.MarkupLines,
            line => line.Contains("загробном цикле", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(_console.MarkupLines,
            line => line.Contains("хранителями", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(_fs.ResolvePath("output/map_viewer.html")));
        Assert.Contains(_console.MarkupLines,
            line => line.Contains("локальная карта", StringComparison.OrdinalIgnoreCase));
        var renderedText = ExtractRenderedText();
        Assert.Contains("Рыночная площадь", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Текущая созданная локация", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Восточные ворота", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Намеченный выход", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Дозор проверил площадь после полуночи.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#[9]", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_console.MarkupLines,
            line => line.Contains("output/map_viewer.html", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(_console.SelectionTitles,
            title => title.Contains("Карта", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(_console.SelectionChoicesHistory.SelectMany(entry => entry.Choices),
            choice => choice.Contains("Рыночная площадь", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_Help_MortalLetsPlayerChooseSectionInsteadOfDumpingAllCommands()
    {
        await SeedMortalStateAsync();
        _console.QueueSelection("Раздел справки", "Общие команды");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/help"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("help_mortal_section_choice");
        Assert.Contains(_console.SelectionTitles,
            title => title.Contains("Раздел справки", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_console.SelectionChoicesHistory,
            history => history.Title.Contains("Раздел справки", StringComparison.OrdinalIgnoreCase) &&
                       history.Choices.Contains("СМЕРТНАЯ ЖИЗНЬ") &&
                       history.Choices.Contains("Общие команды") &&
                       history.Choices.Contains("Показать все разделы") &&
                       history.Choices.Contains("Закрыть"));

        var renderedText = ExtractRenderedText();
        Assert.Contains("/codex", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/refresh", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/npc_talk", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/инв", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ChaosSeaCommand_BlankRealmFailsClosed()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "",
            currentIncarnation = 1
        });
        await _stateManager.RefreshGameStateAsync();

        var result = await _explorer.TryProcessCommand("/хранители");

        Assert.Equal("", result);
        Assert.Contains(_console.MarkupLines,
            line => line.Contains("currentRealm", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("не определ", StringComparison.OrdinalIgnoreCase));
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
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/world_setup"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("world_setup_clear_cancel");
        Assert.True(_console.ConfirmPrompts.Any(prompt => prompt.Contains("Очистить локальную подготовку следующего мира", StringComparison.OrdinalIgnoreCase)),
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
        var renderedText = ExtractRenderedText();
        Assert.Contains("Намерение: дружественное", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Роль и статус", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Состояние мира", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тип слота", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сила коррективы", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("friendly", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("role_status", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("world_condition", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Slot:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Severity:", renderedText, StringComparison.OrdinalIgnoreCase);
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
        Assert.DoesNotContain(_console.SelectionTitles,
            title => title.Contains("Built-in:", StringComparison.OrdinalIgnoreCase) ||
                     title.Contains("User:", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_console.SelectionTitles,
            title => title.Contains("Встроенные:", StringComparison.OrdinalIgnoreCase) &&
                     title.Contains("Пользовательские:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]

    public async Task TryProcessCommand_SystemGuardians_RendersPlayerFacingPresetOverview()
    {
        await SeedAfterlifeStateAsync();
        await SeedSystemGuardianPresetAsync("azalia", "Азалия", "Social", "Обитель Неутолимого Пламени");
        await _stateManager.RefreshGameStateAsync();
        _console.QueueAnySelection("← Назад");

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/system_guardians"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("system_guardians_overview");

        var renderedText = ExtractRenderedText();
        Assert.Contains("Обзор извечных Хранителей", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Азалия", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ценность 1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ценность 2", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Обитель Неутолимого Пламени", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тестовый системный хранитель для regression tests.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Built-in:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User:", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]

    public async Task TryProcessCommand_SystemGuardians_DetailShowsFullDossierWithoutTechnicalEnglishFields()
    {
        await SeedAfterlifeStateAsync();
        await SeedSystemGuardianPresetAsync("azalia", "Азалия", "Social", "Обитель Неутолимого Пламени");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/system_guardians"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("system_guardians_detail");

        var renderedText = ExtractRenderedText();
        Assert.Contains("Досье:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тестовое досье для системного хранителя.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Технический id:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Домен:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Архетип:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Тон:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Domain:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Archetype:", renderedText, StringComparison.OrdinalIgnoreCase);
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
    public async Task TryProcessCommand_WorldRules_RendersPlayerFacingTextWithoutFileContractLeak()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync(WorldDirectiveService.ActiveDirectivesPath, new
        {
            worldTitle = "Текущий Мир",
            settingSummary = "Активное досье для smoke test."
        });

        _console.QueueAnySelection("← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/правила_мира"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("world_rules_player_facing");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Досье текущего мира", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Текущий Мир", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GM должен читать", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lore/current_world/world_directives.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("world_directives.json", renderedText, StringComparison.OrdinalIgnoreCase);
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
    public async Task TryProcessCommand_Locations_HidesUnknownAdjacentLinkState()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/world/current_location.json", new
        {
            locationId = "loc_valmont_corridor",
            name = "Коридор поместья Вальмонт",
            description = "Длинный коридор с потухшими бра.",
            adjacencyMap = new[]
            {
                new
                {
                    targetLocationId = "loc_east_service_stairs",
                    targetLocationName = "Восточная служебная лестница",
                    direction = "восток",
                    distance = "пара минут",
                    linkState = "Unknown"
                }
            }
        });
        _console.QueueAnySelection("← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/локации"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("locations_unknown_link_state");
        var choices = string.Join("\n", _console.SelectionChoicesHistory.SelectMany(entry => entry.Choices));
        Assert.Contains("Восточная служебная лестница", choices, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Unknown", choices, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Locations_LocalizesLocationTypeInChooser()
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
                        locationId = "loc_archive_corridor",
                        name = "Коридор закрытого архива",
                        locationType = "indoor",
                        shortDescription = "Узкий коридор с опечатанными дверями."
                    }
                }
            }
        });
        _console.QueueAnySelection("← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/локации"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("locations_type_localization");
        var choices = string.Join("\n", _console.SelectionChoicesHistory.SelectMany(entry => entry.Choices));
        Assert.Contains("Коридор закрытого архива", choices, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("помещение", choices, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("indoor", choices, StringComparison.OrdinalIgnoreCase);
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
    public async Task TryProcessCommand_Books_WithOnlySealedDocument_RendersUnreadableReason()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/inventory/items.json", new
        {
            items = new[]
            {
                new
                {
                    existedId = "doc_sealed_cli_1",
                    name = "Запечатанное письмо",
                    type = "Документ",
                    textContent = (string[]?)null,
                    unreadableReason = "Печать не позволяет прочесть письмо сейчас."
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/книги"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("books_sealed_document_reason");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Запечатанное письмо", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Печать не позволяет прочесть письмо сейчас.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Нет читаемых предметов", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Books_FirstViewShowsShelfWithoutDumpingLongBodies()
    {
        await SeedMortalStateAsync();
        await SeedBooksReadingFlowStateAsync();
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/книги"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("books_shelf_first_view");
        var firstRendered = _console.Rendered.Count > 0
            ? ExtractRenderableText(_console.Rendered[0])
            : string.Empty;
        Assert.Contains("Письмо с площади", firstRendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Записка с рынка", firstRendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Памятная книга", firstRendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Запечатанное письмо", firstRendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Можно читать", firstRendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Не прочесть", firstRendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INLINE_FULL_BODY_MARKER", firstRendered, StringComparison.Ordinal);
        Assert.DoesNotContain("SIDECAR_FULL_BODY_MARKER", firstRendered, StringComparison.Ordinal);
        Assert.DoesNotContain("JOURNAL_FULL_BODY_MARKER", firstRendered, StringComparison.Ordinal);
        Assert.Contains(_console.SelectionChoicesHistory, history =>
            history.Choices.Any(choice => choice.Contains("Письмо с площади", StringComparison.OrdinalIgnoreCase)) &&
            history.Choices.Any(choice => choice.Contains("Записка с рынка", StringComparison.OrdinalIgnoreCase)) &&
            history.Choices.Any(choice => choice.Contains("← Назад", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task TryProcessCommand_Books_SelectedDocumentShowsOnlyThatDocumentAndReturnsToShelf()
    {
        await SeedMortalStateAsync();
        await SeedBooksReadingFlowStateAsync();
        _console.QueueSelection("Книги", "📜 Записка с рынка — Можно читать — 1 запись", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/books"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("books_selected_document");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Записка с рынка", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SIDECAR_FULL_BODY_MARKER", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("INLINE_FULL_BODY_MARKER", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("JOURNAL_FULL_BODY_MARKER", renderedText, StringComparison.Ordinal);
        Assert.Contains(_console.SelectionChoicesHistory, history =>
            history.Title.Contains("Книги", StringComparison.OrdinalIgnoreCase) &&
            history.Choices.Any(choice => choice.Contains("Записка с рынка", StringComparison.OrdinalIgnoreCase)));
        Assert.True(
            _console.SelectionChoicesHistory.Count(history =>
                history.Title.Contains("Книги", StringComparison.OrdinalIgnoreCase)) >= 2,
            BuildConsoleDiagnostics("books_selected_document_back_navigation"));
    }

    private async Task SeedBooksReadingFlowStateAsync()
    {
        await WriteRawJsonAsync("game_state/inventory/items.json", """
        {
          "items": [
            {
              "existedId": "doc_inline_1",
              "itemId": "doc_inline_1",
              "name": "Письмо с площади",
              "type": "Документ",
              "group": "Документы и медиа",
              "textContent": [
                "Лира просит встретиться у фонтана до рассвета. Это длинное письмо продолжается подробностями о стороже, мокрой мостовой и тайном знаке. INLINE_FULL_BODY_MARKER"
              ]
            },
            {
              "existedId": "doc_sidecar_1",
              "itemId": "doc_sidecar_1",
              "name": "Записка с рынка",
              "type": "note",
              "group": "Документы и медиа",
              "textContent": null
            },
            {
              "existedId": "doc_journal_1",
              "itemId": "doc_journal_1",
              "name": "Памятная книга",
              "type": "Книга",
              "group": "Документы и медиа",
              "textContent": null
            },
            {
              "existedId": "doc_sealed_1",
              "itemId": "doc_sealed_1",
              "name": "Запечатанное письмо",
              "type": "Документ",
              "group": "Документы и медиа",
              "textContent": null,
              "unreadableReason": "Печать не позволяет прочесть письмо сейчас."
            }
          ]
        }
        """);
        await WriteRawJsonAsync("game_state/inventory/item_text_updates.json", """
        {
          "entries": [
            {
              "itemId": "doc_sidecar_1",
              "itemName": "Не это имя",
              "textContent": [
                "На обороте записки указан путь через северные ворота. Это длинная приписка с именами торговцев, часом встречи и предупреждением о дозорных. SIDECAR_FULL_BODY_MARKER"
              ]
            }
          ]
        }
        """);
        await WriteRawJsonAsync("game_state/npcs/item_journals.json", """
        {
          "entries": [
            {
              "itemId": "doc_journal_1",
              "itemName": "Другое имя",
              "journalEntries": [
                {
                  "event": "Пробуждение",
                  "description": "Книга шепчет о владельце. В записи слышен скрип пера, сухой шорох страниц и обещание вернуться к началу. JOURNAL_FULL_BODY_MARKER"
                }
              ]
            }
          ]
        }
        """);
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
        _console.QueueAnySelection("← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/моды"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("system_mods");
        Assert.True(_console.ClearCalls > 0);
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
        var renderedText = ExtractRenderedText();
        Assert.Contains("Глобальные системные моды", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System Mods", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_session", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Папка модов", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SystemMods_HidesTechnicalFileNamesInPlayerChoices()
    {
        await SeedMortalStateAsync();
        _settings.EnabledSystemMods = new List<string> { "test_mod.md" };
        await _fs.WriteFileAtomicAsync("mods/test_mod.md", """
        # Тонкая настройка мира

        Description

        Правило влияет на тон повествования.
        """);
        _console.QueueAnySelection("← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/моды"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("system_mods_player_facing_choices");
        var renderedText = ExtractRenderedText();
        var choices = string.Join("\n", _console.SelectionChoicesHistory.SelectMany(entry => entry.Choices));
        Assert.Contains("Тонкая настройка мира", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тонкая настройка мира", choices, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".md", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".md", choices, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Description", renderedText, StringComparison.OrdinalIgnoreCase);
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
    public async Task TryProcessCommand_CurrentLocation_LocalizesLocationType()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/world/current_location.json", new
        {
            name = "Покои виконта де Вальмонта",
            locationType = "indoor",
            indoorType = "Building",
            description = "Роскошные покои в поместье Вальмонт."
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/где_я"));

        Assert.Null(ex);
        var output = ExtractRenderedText();
        Assert.Contains("Тип: помещение", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Тип: indoor", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_CurrentLocation_HidesCanonicalTurnAnchorInLastEvents()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/world/current_location.json", new
        {
            name = "Кабинет наставника",
            description = "Комната с мокрыми реестрами и сломанной печатью.",
            locationType = "indoor",
            lastEventsDescription = "#[9]. Марена нашла след воска у замка и отметила знак разомкнутой звезды."
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/где_я"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("current_location_turn_anchor_projection");
        var output = ExtractRenderedText();
        Assert.Contains("Марена нашла след воска у замка", output, StringComparison.Ordinal);
        Assert.DoesNotContain("#[9]", output, StringComparison.Ordinal);
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
    public async Task TryProcessCommand_Status_UsesSoulFallbackAndHidesEmptyIdentityRows()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Серебряная Тень",
            soulFormDescription = "Женская душа в виде тонкого серебристого силуэта.",
            currentRealm = "Mortal World",
            currentIncarnation = 1,
            inkFeathers = new { current = 0 },
            enlightenment = new { currentTier = "Новичок" }
        });
        await WriteJsonAsync("game_state/core/player_status.json", new
        {
            healthPercentage = "100%",
            energyPercentage = "100%",
            poisePercentage = "100%",
            currentCondition = "Здоров",
            money = 0
        });
        await WriteJsonAsync("game_state/player/computed_characteristics.json", new
        {
            characteristics = new { strength = 1, dexterity = 3, constitution = 1, intelligence = 3, wisdom = 1, faith = 1, attractiveness = 1, trade = 1, persuasion = 3, perception = 2, luck = 2, speed = 1 },
            modifiedCharacteristics = new { strength = 1, dexterity = 3, constitution = 1, intelligence = 3, wisdom = 1, faith = 1, attractiveness = 1, trade = 1, persuasion = 3, perception = 2, luck = 2, speed = 1 },
            permanentlyModifiedCharacteristics = new { strength = 1, dexterity = 3, constitution = 1, intelligence = 3, wisdom = 1, faith = 1, attractiveness = 1, trade = 1, persuasion = 3, perception = 2, luck = 2, speed = 1 }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/статус"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("status_soul_fallback");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Серебряная Тень", renderedText);
        Assert.Contains("Форма души", renderedText);
        Assert.Contains("Женская душа", renderedText);
        Assert.DoesNotContain("Раса", renderedText);
        Assert.DoesNotContain("Класс", renderedText);
    }

    [Fact]
    public async Task TryProcessCommand_Status_MortalBootstrapUsesIncarnationIdentityBeforeSoulFallback()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Северная Искра",
            soulFormDescription = "Полупрозрачная душа в серо-синем свете.",
            currentRealm = "Mortal World",
            currentIncarnation = 1,
            inkFeathers = new { current = 0 },
            enlightenment = new { currentTier = "Новичок" }
        });
        await WriteJsonAsync("game_state/control/next_life_scenario_core.json", new
        {
            scenarioCoreAssertions = new[]
            {
                new
                {
                    assertionId = "core_identity",
                    category = "identity_anchor",
                    value = "Ронан Вельт, молодой городской писарь из портового архива: худой человек с чернильными пальцами.",
                    @explicit = true,
                    source = "structured_field"
                }
            }
        });
        await WriteJsonAsync("game_state/core/player_status.json", new
        {
            healthPercentage = "100%",
            energyPercentage = "100%",
            poisePercentage = "100%",
            currentCondition = "Здоров",
            money = 100
        });
        await WriteJsonAsync("game_state/player/computed_characteristics.json", new
        {
            characteristics = new { strength = 1, dexterity = 2, constitution = 1, intelligence = 4, wisdom = 2, faith = 1, attractiveness = 2, trade = 3, persuasion = 2, perception = 3, luck = 1, speed = 2 },
            modifiedCharacteristics = new { strength = 1, dexterity = 2, constitution = 1, intelligence = 4, wisdom = 2, faith = 1, attractiveness = 2, trade = 3, persuasion = 2, perception = 3, luck = 1, speed = 2 },
            permanentlyModifiedCharacteristics = new { strength = 1, dexterity = 2, constitution = 1, intelligence = 4, wisdom = 2, faith = 1, attractiveness = 2, trade = 3, persuasion = 2, perception = 3, luck = 1, speed = 2 }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/статус"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("status_mortal_bootstrap_identity");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Ронан Вельт", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("👤 Северная Искра", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Форма души", renderedText, StringComparison.OrdinalIgnoreCase);
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
    public async Task TryProcessCommand_Skills_UsesCanonicalMasteryAndLocalizesStableValues()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/misc/characteristics.json", new
        {
            strength = 2,
            dexterity = 4,
            constitution = 2,
            intelligence = 4,
            wisdom = 1,
            faith = 1,
            attractiveness = 1,
            trade = 1,
            persuasion = 2,
            perception = 4,
            luck = 2,
            speed = 1
        });
        await WriteJsonAsync("game_state/player/experience.json", new
        {
            playerLevel = 2,
            level = 2,
            currentExperience = 49,
            totalExperience = 149,
            experienceForNextLevel = 150
        });
        await WriteJsonAsync("game_state/player/stat_points.json", new
        {
            unspentStatPoints = 0,
            levelUpStatPointsAwardedThroughLevel = 2
        });
        await WriteJsonAsync("game_state/player/skills_active.json", new
        {
            activeSkillChanges = new[]
            {
                new
                {
                    skillName = "Быстрый выпад",
                    skillDescription = "Короткая атака тонким клинком по открывшейся цели.",
                    rarity = "Common",
                    actionCost = "Fast",
                    scalingCharacteristic = "dexterity",
                    scalesValue = true,
                    combatEffect = new
                    {
                        isActivatedEffect = true,
                        actionName = "Быстрый выпад",
                        actionDescription = "Колючий удар стилетом.",
                        damageType = "piercing",
                        baseDamage = 8,
                        range = "melee",
                        actionCost = "Fast",
                        actionPointCost = 1,
                        cooldown = 0
                    }
                }
            }
        });
        await WriteJsonAsync("game_state/player/skill_mastery.json", new
        {
            skillMasteryChanges = new[]
            {
                new
                {
                    skillName = "Быстрый выпад",
                    newMasteryLevel = 3,
                    newCurrentMasteryProgress = 2,
                    newMasteryProgressNeeded = 8,
                    masteryLeveledUp = false
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/навыки"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("skills_mastery");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Мастерство: 3", renderedText);
        Assert.Contains("2/8", renderedText);
        Assert.Contains("+12%", renderedText);
        Assert.Contains("×1,12", renderedText);
        Assert.DoesNotContain("Common", renderedText);
        Assert.DoesNotContain("Fast", renderedText);
        var choices = string.Join("\n", _console.SelectionChoicesHistory.SelectMany(entry => entry.Choices));
        Assert.DoesNotContain("Common", choices);
    }

    [Fact]
    public async Task TryProcessCommand_BracketBearingMortalText_RendersStatusQuestsSkillsAndBooksSafely()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/core/player_status.json", new
        {
            healthPercentage = "85%",
            energyPercentage = "70%",
            poisePercentage = "60%",
            currentCondition = "Собран [status]",
            money = 120
        });
        await WriteJsonAsync("game_state/player/transformation.json", new
        {
            playerCharacterNameChange = "Элиан [debug]",
            playerRaceChange = "Человек [broken",
            playerClassChange = "Следопыт [card_alpha, card_beta]"
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
            new { effectType = "buff [debug]", value = "+5% [broken", duration = 1, effectDescription = "Боевой тонус [card_alpha, card_beta]" }
        });
        await WriteJsonAsync("game_state/quests/regular_quests.json", new
        {
            quests = new[]
            {
                new
                {
                    questId = "quest_bracket_001",
                    questName = "Контракт [debug]",
                    status = "Active",
                    questGiver = "Наниматель [broken",
                    description = "Тестовый квест [card_alpha, card_beta].",
                    objectives = new[]
                    {
                        new
                        {
                            description = "Проверить [objective]",
                            status = "Active"
                        }
                    }
                }
            }
        });
        await WriteJsonAsync("game_state/player/skills_active.json", new
        {
            activeSkillChanges = new[]
            {
                new
                {
                    skillName = "Рывок [debug]",
                    rarity = "Rare [Кольцо]",
                    category = "Combat [broken",
                    level = "2 [level]",
                    description = "Быстрый рывок [card_alpha, card_beta]."
                }
            }
        });
        await WriteJsonAsync("game_state/player/skills_passive.json", new
        {
            passiveSkillChanges = new[]
            {
                new
                {
                    skillName = "Острый глаз [debug]",
                    rarity = "Uncommon [Кольцо]",
                    type = "KnowledgeBased [broken",
                    description = "Вы замечаете [details] быстрее других."
                }
            }
        });
        await WriteJsonAsync("game_state/player/skill_mastery.json", new
        {
            skillMasteryChanges = new[]
            {
                new
                {
                    skillName = "Рывок [debug]",
                    currentMasteryLevel = 2,
                    experienceTowardsNextLevel = 10,
                    experienceNeededForNextLevel = 20
                }
            }
        });
        await WriteJsonAsync("game_state/inventory/items.json", new
        {
            items = new[]
            {
                new
                {
                    itemId = "item_book_bracket_001",
                    name = "Дневник [debug]",
                    textContent = new[]
                    {
                        "Первая запись [broken",
                        "Вторая запись [card_alpha, card_beta]."
                    }
                }
            }
        });
        await WriteJsonAsync("game_state/npcs/item_journals.json", new[]
        {
            new
            {
                itemName = "Шкатулка [debug]",
                journalEntries = new object[]
                {
                    new
                    {
                        timestamp = "2026-03-19T12:00:00Z",
                        @event = "Открытие [broken",
                        description = "Внутри оказался лист [card_alpha, card_beta]."
                    }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        foreach (var command in new[] { "/статус", "/квесты", "/навыки", "/книги" })
        {
            var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand(command));
            Assert.Null(ex);
            AssertNoHiddenExplorerErrors(command);
        }

        AssertSelectionChoicesAreSpectreMarkupSafe("mortal_bracket_quests", "Квесты");
        AssertSelectionChoicesAreSpectreMarkupSafe("mortal_bracket_skills", "Навыки");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Элиан [debug]", renderedText, StringComparison.Ordinal);
        Assert.Contains("Контракт [debug]", renderedText, StringComparison.Ordinal);
        Assert.Contains("Дневник [debug]", renderedText, StringComparison.Ordinal);
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
    public async Task TryProcessCommand_FactionDrilldown_RichFactionShowsKnowledgeSectionMenu()
    {
        await SeedRichFactionDrilldownStateAsync();
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/фракции"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("faction_drilldown_sections");
        var factionPrompt = _console.SelectionChoicesHistory.First(
            entry => entry.Title.Contains("Фракции", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(factionPrompt.Choices,
            choice => choice.Contains("Скрытый архивариус", StringComparison.OrdinalIgnoreCase));

        var sectionPrompt = _console.SelectionChoicesHistory.First(
            entry => entry.Title.Contains("Разделы фракции", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sectionPrompt.Choices,
            choice => choice.Contains("Ресурсы и экономика", StringComparison.Ordinal) &&
                      choice.Contains("2 ресурса", StringComparison.Ordinal));
        Assert.Contains(sectionPrompt.Choices,
            choice => choice.Contains("Хроники", StringComparison.Ordinal) &&
                      choice.Contains("2 записи", StringComparison.Ordinal));
        Assert.Contains(sectionPrompt.Choices,
            choice => choice.Contains("Ранги и иерархия", StringComparison.Ordinal) &&
                      choice.Contains("1 ветвь", StringComparison.Ordinal));
        Assert.Contains(sectionPrompt.Choices,
            choice => choice.Contains("Проекты и операции", StringComparison.Ordinal) &&
                      choice.Contains("2 проекта", StringComparison.Ordinal));
        Assert.Contains(sectionPrompt.Choices,
            choice => choice.Contains("Стратегия и память", StringComparison.Ordinal) &&
                      choice.Contains("2 записи", StringComparison.Ordinal));
        Assert.Contains(sectionPrompt.Choices,
            choice => choice.Contains("Территории и влияние", StringComparison.Ordinal) &&
                      choice.Contains("2 территории", StringComparison.Ordinal));
        Assert.DoesNotContain(sectionPrompt.Choices,
            choice => choice.Contains("Показать изображение", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sectionPrompt.Choices,
            choice => choice.Contains("← Закрыть разделы фракции", StringComparison.Ordinal));
        AssertSelectionChoicesAreSpectreMarkupSafe("faction_drilldown_sections", "Разделы фракции");
    }

    [Fact]
    public async Task TryProcessCommand_FactionDrilldown_StringControlledTerritoriesRenderWithoutHiddenErrors()
    {
        await SeedMortalStateAsync();
        await WriteRawJsonAsync("game_state/factions/faction_core.json", """
        [
          {
            "factionId": "faction_scribe_house",
            "name": "Дом переписчика Восковой улицы",
            "description": "Малый дом-служба при переписчике.",
            "reputation": 0,
            "level": 1,
            "developmentArchetype": "scribe_household",
            "controlledTerritories": [
              "loc_life_001_start",
              {
                "locationName": "Лавка переписчика",
                "controlLevel": "strong",
                "influence": 18,
                "summary": "Семья держит рабочую лавку."
              }
            ]
          }
        ]
        """);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/фракции"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("faction_drilldown_string_controlled_territories");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Дом переписчика Восковой улицы", renderedText, StringComparison.Ordinal);
        Assert.Contains("дом переписчика", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("scribe_household", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Локация loc_life_001_start", renderedText, StringComparison.Ordinal);
        Assert.Contains("Лавка переписчика", renderedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_FactionDrilldown_SectionSelectionsRenderResourceProjectAndHierarchyDetails()
    {
        await SeedRichFactionDrilldownStateAsync();
        _console.QueueSelection(
            "Разделы фракции",
            "💰 Ресурсы и экономика — 2 ресурса",
            "🔨 Проекты и операции — 2 проекта",
            "👑 Ранги и иерархия — 1 ветвь",
            "← Закрыть разделы фракции");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/фракции"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("faction_drilldown_resource_project_hierarchy");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Ресурсы и экономика", renderedText, StringComparison.Ordinal);
        Assert.Contains("Казна", renderedText, StringComparison.Ordinal);
        Assert.Contains("120", renderedText, StringComparison.Ordinal);
        Assert.Contains("+12/цикл", renderedText, StringComparison.Ordinal);
        Assert.Contains("Закупка железа", renderedText, StringComparison.Ordinal);
        Assert.Contains("Проекты и операции", renderedText, StringComparison.Ordinal);
        Assert.Contains("Сеть наблюдателей", renderedText, StringComparison.Ordinal);
        Assert.Contains("Контракт караванов", renderedText, StringComparison.Ordinal);
        Assert.Contains("Ранги и иерархия", renderedText, StringComparison.Ordinal);
        Assert.Contains("Старший торговец / Старшая торговка", renderedText, StringComparison.Ordinal);
        Assert.Contains("право вести переговоры", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("resourceLedger", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("project_shadow", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_FactionDrilldown_SectionDetailsHideHiddenChroniclesAndRawStrategicState()
    {
        await SeedRichFactionDrilldownStateAsync();
        _console.QueueSelection(
            "Разделы фракции",
            "📜 Хроники — 2 записи",
            "🧭 Стратегия и память — 2 записи",
            "🗺 Территории и влияние — 2 территории",
            "← Закрыть разделы фракции");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/фракции"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("faction_drilldown_hidden_boundaries");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Хроники", renderedText, StringComparison.Ordinal);
        Assert.Contains("Гильдия открыла северные склады", renderedText, StringComparison.Ordinal);
        Assert.Contains("Договор с портовыми мастерами", renderedText, StringComparison.Ordinal);
        Assert.Contains("Стратегия и память", renderedText, StringComparison.Ordinal);
        Assert.Contains("Сохранять контроль над караванами", renderedText, StringComparison.Ordinal);
        Assert.Contains("Слухачи подтвердили безопасный маршрут", renderedText, StringComparison.Ordinal);
        Assert.Contains("Территории и влияние", renderedText, StringComparison.Ordinal);
        Assert.Contains("Купеческий квартал", renderedText, StringComparison.Ordinal);
        Assert.Contains("Южная пристань", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Тайная запись о подкупе судьи", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Тайный долговой список", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Скрытый план давления", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("strategicMemory", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("internal", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Полный JSON", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_FactionDrilldown_SparseFactionSectionsRenderUsefulEmptyStates()
    {
        await SeedMortalStateAsync();
        await WriteRawJsonAsync("game_state/factions/faction_core.json", """
        [
          {
            "factionId": "faction_sparse_001",
            "name": "Тихая артель",
            "description": "Малая артель без подробных записей.",
            "reputation": 15,
            "level": 1
          }
        ]
        """);
        _console.QueueSelection(
            "Разделы фракции",
            "💰 Ресурсы и экономика — нет данных",
            "📜 Хроники — нет данных",
            "← Закрыть разделы фракции");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/фракции"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("faction_drilldown_empty_states");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Ресурсы и экономика", renderedText, StringComparison.Ordinal);
        Assert.Contains("Открытые сведения о ресурсах этой фракции пока не внесены.", renderedText, StringComparison.Ordinal);
        Assert.Contains("Хроники", renderedText, StringComparison.Ordinal);
        Assert.Contains("Открытых хроник этой фракции пока нет.", renderedText, StringComparison.Ordinal);
    }

    private async Task SeedRichFactionDrilldownStateAsync()
    {
        await SeedMortalStateAsync();
        await WriteRawJsonAsync("game_state/factions/faction_core.json", """
        [
          {
            "factionId": "faction_trade_001",
            "name": "Серебряная гильдия",
            "description": "Купеческая сила, удерживающая караваны и склады.",
            "reputation": 180,
            "reputationDescription": "Гильдия считает героя полезным союзником.",
            "level": 3,
            "experience": 45,
            "experienceForNextLevel": 90,
            "isPlayerMember": true,
            "playerRank": "Старший торговец",
            "playerBranch": "trade",
            "developmentArchetype": "economic",
            "playerStrategyDirective": "Сохранять контроль над караванами без открытой войны.",
            "powerProfile": {
              "military": 24,
              "economic": 72,
              "social": 58,
              "covert": 31,
              "logistics": 64
            },
            "controlledTerritories": [
              {
                "locationId": "loc_market",
                "locationName": "Купеческий квартал",
                "controlLevel": "strong",
                "influence": 78,
                "summary": "Гильдия держит склады и весовые."
              },
              {
                "locationId": "loc_south_dock",
                "locationName": "Южная пристань",
                "controlLevel": "contested",
                "influence": 42,
                "summary": "Портовые мастера ждут выплат."
              }
            ],
            "strategicMemory": [
              {
                "title": "Маршрут без засад",
                "summary": "Слухачи подтвердили безопасный маршрут.",
                "turn": 14,
                "visibleToPlayer": true
              },
              {
                "title": "Скрытый план давления",
                "summary": "Не показывать игроку.",
                "visibility": "gm_only"
              }
            ],
            "scribeChronicle": [
              "Гильдия открыла северные склады.",
              {
                "title": "Тайный долговой список",
                "summary": "Не показывать игроку.",
                "visibility": "gm_only"
              }
            ]
          },
          {
            "factionId": "faction_hidden_001",
            "name": "Скрытый архивариус",
            "description": "Эта фракция не должна попасть в список.",
            "isPlayerVisible": false,
            "visibility": "hidden"
          }
        ]
        """);

        await WriteRawJsonAsync("game_state/factions/faction_resources.json", """
        [
          {
            "factionId": "faction_trade_001",
            "factionName": "Серебряная гильдия",
            "metaResources": [
              {
                "resourceName": "Казна",
                "currentStockpile": 120,
                "incomePerCycle": 12,
                "upkeepPerCycle": 4
              },
              {
                "resourceName": "Влияние",
                "currentStockpile": 64,
                "incomePerCycle": 6,
                "upkeepPerCycle": 1
              }
            ],
            "resourceLedger": [
              {
                "title": "Закупка железа",
                "resourceName": "Казна",
                "amount": -20,
                "balanceAfter": 100,
                "summary": "Оплачены балки для северных ворот.",
                "visibleToPlayer": true
              },
              {
                "title": "Скрытая взятка",
                "resourceName": "Казна",
                "amount": -30,
                "summary": "GM-only расход.",
                "visibility": "gm_only"
              }
            ]
          }
        ]
        """);

        await WriteRawJsonAsync("game_state/factions/faction_projects.json", """
        [
          {
            "factionId": "faction_trade_001",
            "projectId": "project_watchers",
            "projectName": "Сеть наблюдателей",
            "activeState": "active",
            "description": "Смотрители отмечают движение караванов.",
            "currentStep": 2,
            "totalSteps": 4,
            "timeSpentMinutes": 90,
            "totalTimeCostMinutes": 180,
            "totalResourceCost": [
              { "resourceName": "Казна", "totalAmount": 40 }
            ],
            "resourcesSpent": [
              { "resourceName": "Казна", "amountSpent": 20 }
            ],
            "visibleToPlayer": true
          },
          {
            "factionId": "faction_trade_001",
            "projectId": "project_caravans",
            "projectName": "Контракт караванов",
            "finalState": "completed",
            "completionTurn": 13,
            "description": "Караванщики приняли новые печати.",
            "visibleToPlayer": true
          },
          {
            "factionId": "faction_trade_001",
            "projectId": "project_shadow",
            "projectName": "Скрытый проект давления",
            "activeState": "active",
            "visibility": "hidden"
          }
        ]
        """);

        await WriteRawJsonAsync("game_state/factions/faction_structure.json", """
        [
          {
            "factionId": "faction_trade_001",
            "ranks": {
              "branches": [
                {
                  "branchId": "trade",
                  "displayName": "Торговая ветвь",
                  "isCoreBranch": true,
                  "ranks": [
                    {
                      "rankNameMale": "Младший приказчик",
                      "rankNameFemale": "Младшая приказчица",
                      "requiredReputation": 30,
                      "benefits": ["доступ к складским слухам"]
                    },
                    {
                      "rankNameMale": "Старший торговец",
                      "rankNameFemale": "Старшая торговка",
                      "requiredReputation": 150,
                      "unlockCondition": "закрыть спор о караванной пошлине",
                      "benefits": ["право вести переговоры", "доступ к закрытым складам"]
                    }
                  ]
                }
              ]
            }
          }
        ]
        """);

        await WriteRawJsonAsync("game_state/factions/faction_chronicles.json", """
        [
          {
            "factionId": "faction_trade_001",
            "title": "Портовая сделка",
            "entry": "Договор с портовыми мастерами закреплён.",
            "turn": 15,
            "visibleToPlayer": true
          },
          {
            "factionId": "faction_trade_001",
            "title": "Скрытая запись",
            "entry": "Тайная запись о подкупе судьи.",
            "visibility": "gm_only"
          }
        ]
        """);
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
        var renderedText = ExtractRenderedText();
        Assert.Contains("Доступ к хранилищам", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Storage Access", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Quests_HistoryRewardsRenderLabelsAndUnavailableReasonsWithoutRawIds()
    {
        await SeedMortalStateAsync();
        await WriteRawJsonAsync("game_state/quests/quest_history.json", """
        {
          "questHistory": [
            {
              "questId": "quest_merchants_caravan",
              "questName": "Караван купцов",
              "outcome": "completed",
              "completionDate": "2026-06-05T12:00:00Z",
              "experience": 25,
              "incarnationNumber": 1
            }
          ],
          "questRewards": [
            {
              "questId": "quest_merchants_caravan",
              "itemsReceived": [
                {
                  "itemId": "item_merchant_seal",
                  "displayName": "Печать караванного мастера"
                },
                {
                  "itemId": "item_first_life_ring",
                  "displayName": "Перстень первой жизни",
                  "authorityStatus": "HistoricalOnly",
                  "reason": "Перстень остался в прошлой инкарнации."
                }
              ],
              "skillsUnlocked": [
                {
                  "skillName": "Продвинутая торговля",
                  "displayName": "Продвинутая торговля"
                }
              ],
              "relationshipChanges": [
                {
                  "npcId": "npc_guild_master",
                  "displayName": "Мастер гильдии",
                  "change": 20
                }
              ]
            }
          ],
          "questChains": []
        }
        """);
        await WriteRawJsonAsync("game_state/inventory/items.json", """
        {
          "items": [
            {
              "itemId": "item_merchant_seal",
              "name": "Печать караванного мастера",
              "description": "Печать, полученная за охрану каравана.",
              "type": "Квестовый предмет"
            }
          ]
        }
        """);
        await WriteRawJsonAsync("game_state/player/skills_active.json", """
        {
          "activeSkillChanges": [
            {
              "skillName": "Продвинутая торговля",
              "skillDescription": "Позволяет выгоднее вести сложные сделки.",
              "rarity": "Uncommon",
              "actionCost": 1,
              "combatEffect": {
                "actionName": "Торговая оценка",
                "isActivatedEffect": true,
                "effects": []
              }
            }
          ]
        }
        """);
        await WriteRawJsonAsync("game_state/npcs/npc_relationships.json", """
        {
          "NPCRelationshipChanges": [
            {
              "npcId": "npc_guild_master",
              "npcName": "Мастер гильдии",
              "relationshipLevel": 80,
              "attitude": "Доверие и Расположение"
            }
          ]
        }
        """);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/квесты"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("quest_history_reward_authority");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Печать караванного мастера", renderedText, StringComparison.Ordinal);
        Assert.Contains("Продвинутая торговля", renderedText, StringComparison.Ordinal);
        Assert.Contains("Мастер гильдии", renderedText, StringComparison.Ordinal);
        Assert.Contains("Перстень первой жизни", renderedText, StringComparison.Ordinal);
        Assert.Contains("Перстень остался в прошлой инкарнации.", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("item_merchant_seal", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("item_first_life_ring", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("npc_guild_master", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"itemId\"", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("validation", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Quests_DetailLocalizesStableStatusValues()
    {
        await SeedMortalStateAsync();
        await WriteRawJsonAsync("game_state/quests/regular_quests.json", """
        {
          "quests": [
            {
              "questId": "quest_black_seal",
              "questName": "Чёрная печать",
              "status": "Active",
              "description": "Разобраться, почему раненый принёс в лечебницу запретный знак.",
              "objectives": [
                {
                  "description": "Спросить Вирента о печати",
                  "status": "Active"
                }
              ]
            }
          ]
        }
        """);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/квесты"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("quest_detail_localized_status");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Чёрная печать", renderedText, StringComparison.Ordinal);
        Assert.Contains("Статус", renderedText, StringComparison.Ordinal);
        Assert.Contains("Активен", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Active", renderedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_Quests_DetailHidesCanonicalTurnAnchorsInJournal()
    {
        await SeedMortalStateAsync();
        await WriteRawJsonAsync("game_state/quests/regular_quests.json", """
        {
          "quests": [
            {
              "questId": "quest_anchor_leak",
              "questName": "След наставника",
              "questGiver": "Исчезновение наставника",
              "status": "Active",
              "questBackground": "Наставник исчез после ночного визита.",
              "description": "Собрать улики и понять, кто оставил печать.",
              "objectives": [
                {
                  "description": "Сравнить воск на перчатке со сломанной печатью.",
                  "status": "Completed"
                }
              ],
              "detailsLog": [
                "#[7]. Марена сравнила воск на перчатке и сломанной печати.",
                "#[8] - В реестре нужно найти знак разомкнутой звезды."
              ]
            }
          ]
        }
        """);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/квесты"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("quest_detail_turn_anchor_projection");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Марена сравнила воск на перчатке и сломанной печати.", renderedText, StringComparison.Ordinal);
        Assert.Contains("В реестре нужно найти знак разомкнутой звезды.", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("#[7]", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("#[8]", renderedText, StringComparison.Ordinal);
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
    public async Task TryProcessCommand_PlayerInteractions_ConsoleExposesSharedPlayerAndRecordDrilldowns()
    {
        await SeedMortalStateAsync();
        await SeedRichMortalPlayerInteractionsFilesAsync();
        await _stateManager.RefreshGameStateAsync();

        var overviewResult = await _explorer.TryProcessCommand("/взаимодействия");

        Assert.Equal(string.Empty, overviewResult);
        AssertNoHiddenExplorerErrors("player_interactions_overview_drilldowns");
        var overviewText = ExtractRenderedText();
        Assert.Contains("Взаимодействия игроков", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/взаимодействия игрок player_lienna", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/взаимодействия запись meeting_cipher", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Передача шифра", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/misc", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Полная запись взаимодействий", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"otherPlayersInteractions\"", overviewText, StringComparison.OrdinalIgnoreCase);

        _console.Rendered.Clear();
        _console.MarkupLines.Clear();

        var playerResult = await _explorer.TryProcessCommand("/взаимодействия игрок player_lienna");

        Assert.Equal(string.Empty, playerResult);
        AssertNoHiddenExplorerErrors("player_interactions_player_detail");
        var playerText = ExtractRenderedText();
        Assert.Contains("Игрок: Лианна из янтарной башни", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ждёт ответа у старого фонтана", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Спор у переправы", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Страж Кай", playerText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("playerId", playerText, StringComparison.OrdinalIgnoreCase);

        _console.Rendered.Clear();
        _console.MarkupLines.Clear();

        var recordResult = await _explorer.TryProcessCommand("/взаимодействия запись meeting_cipher");

        Assert.Equal(string.Empty, recordResult);
        AssertNoHiddenExplorerErrors("player_interactions_record_detail");
        var recordText = ExtractRenderedText();
        Assert.Contains("Запись взаимодействия: Передача шифра", recordText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("шифр спрятан в перчатке", recordText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Можно спросить о знаке Вальмонтов", recordText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("interactionId", recordText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/misc", recordText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConsolePlayerInteractionsSource_UsesSharedMortalInteractionsResultBuilder()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "UI",
            "ExplorerMode",
            "ExplorerMode.MetaStoryAndStatus.cs"));
        var methodStart = source.IndexOf("private async Task ShowPlayerInteractions()", StringComparison.Ordinal);
        Assert.True(methodStart >= 0, "ShowPlayerInteractions method was not found.");
        var nextMethodStart = source.IndexOf("\n    //", methodStart, StringComparison.Ordinal);
        var methodSource = nextMethodStart > methodStart
            ? source[methodStart..nextMethodStart]
            : source[methodStart..];

        Assert.Contains("ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(commandLine", methodSource, StringComparison.Ordinal);
        Assert.Contains("ExplorerCommandResultConsoleRenderer.Render(_console, result)", methodSource, StringComparison.Ordinal);
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
    public async Task TryProcessCommand_Effects_InMortalRealm_WithStatusConditionsAndOtherSections_RendersStatusFallback()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/core/player_status.json", new
        {
            healthPercentage = "92%",
            energyPercentage = "64%",
            poisePercentage = "70%",
            currentCondition = "Лёгкое недомогание",
            currentConditionDescription = "Тело ломит, а мысли держатся будто сквозь туман.",
            activeConditions = new[]
            {
                "Головная боль после тяжёлых снов (-1 к Восприятию до полудня)",
                "Магический резонанс: слабое покалывание в пальцах"
            }
        });
        await WriteJsonAsync("game_state/player/effects.json", new
        {
            activeEffects = Array.Empty<object>(),
            wounds = Array.Empty<object>(),
            temporaryConditions = Array.Empty<object>()
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
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/эффекты"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("effects_status_fallback_with_other_sections");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Подробная запись эффекта ещё не заведена", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Лёгкое недомогание", renderedText, StringComparison.Ordinal);
        Assert.Contains("Тело ломит, а мысли держатся будто сквозь туман.", renderedText, StringComparison.Ordinal);
        Assert.Contains("Головная боль после тяжёлых снов (-1 к Восприятию до полудня)", renderedText, StringComparison.Ordinal);
        Assert.Contains("Порез", renderedText, StringComparison.Ordinal);
        Assert.Contains("Голод", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("game_state/player/effects.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TryProcessCommand_Effects_InMortalRealm_WithStatusConditionsAndMissingStructuredEffects_RendersStatusFallback(
        bool writeEmptyStructuredEffects)
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/core/player_status.json", new
        {
            healthPercentage = "92%",
            energyPercentage = "64%",
            poisePercentage = "70%",
            currentCondition = "Лёгкое недомогание",
            currentConditionDescription = "Тело ломит, а мысли держатся будто сквозь туман.",
            activeConditions = new[]
            {
                "Головная боль после тяжёлых снов (-1 к Восприятию до полудня)",
                "Магический резонанс: слабое покалывание в пальцах"
            }
        });

        if (writeEmptyStructuredEffects)
        {
            await WriteJsonAsync("game_state/player/effects.json", new
            {
                activeEffects = Array.Empty<object>(),
                wounds = Array.Empty<object>(),
                temporaryConditions = Array.Empty<object>()
            });
        }

        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/эффекты"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("effects_status_fallback");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Подробная запись эффекта ещё не заведена", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Лёгкое недомогание", renderedText, StringComparison.Ordinal);
        Assert.Contains("Тело ломит, а мысли держатся будто сквозь туман.", renderedText, StringComparison.Ordinal);
        Assert.Contains("Головная боль после тяжёлых снов (-1 к Восприятию до полудня)", renderedText, StringComparison.Ordinal);
        Assert.Contains("Магический резонанс: слабое покалывание в пальцах", renderedText, StringComparison.Ordinal);
        Assert.DoesNotContain("game_state/player/effects.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", renderedText, StringComparison.OrdinalIgnoreCase);
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
    public async Task TryProcessCommand_Combat_ConsoleExposesSharedEnemyAllyAndLogDrilldowns()
    {
        await SeedMortalStateAsync();
        await SeedRichMortalCombatFilesAsync();
        await _stateManager.RefreshGameStateAsync();

        var overviewResult = await _explorer.TryProcessCommand("/бой");

        Assert.Equal(string.Empty, overviewResult);
        AssertNoHiddenExplorerErrors("combat_overview_drilldowns");
        var overviewText = ExtractRenderedText();
        Assert.Contains("Боевая обстановка", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/бой враг shadow_messenger", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/бой союзник rina_guard", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/бой журнал log_round_2", overviewText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/combat", overviewText, StringComparison.OrdinalIgnoreCase);

        _console.Rendered.Clear();
        _console.MarkupLines.Clear();

        var detailResult = await _explorer.TryProcessCommand("/бой враг shadow_messenger");

        Assert.Equal(string.Empty, detailResult);
        AssertNoHiddenExplorerErrors("combat_enemy_detail");
        var detailText = ExtractRenderedText();
        Assert.Contains("Враг: Теневой посыльный", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("сорвать концентрацию мага", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Горит после серебряной стрелы", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("enemyId", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/combat", detailText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Combat_DefeatedEnemiesRenderAsCompletedEncounter()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/combat/enemies.json", new
        {
            enemiesData = new[]
            {
                new
                {
                    enemyId = "enemy_gray_forest_creature_001",
                    name = "Серая лесная тварь",
                    type = "beast",
                    status = "defeated",
                    currentHealth = "0%",
                    currentPoise = "0%",
                    maxPoise = "100%",
                    description = "Тварь добита и больше не представляет угрозы."
                }
            }
        });
        await WriteJsonAsync("game_state/combat/allies.json", new { alliesData = Array.Empty<object>() });
        await WriteJsonAsync("game_state/combat/combat_log.json", new
        {
            combat_log_markdown = "Бой окончен: тварь повержена."
        });
        await _stateManager.RefreshGameStateAsync();

        var result = await _explorer.TryProcessCommand("/бой");

        Assert.Equal(string.Empty, result);
        AssertNoHiddenExplorerErrors("combat_defeated_overview");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Сражение завершено", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Сражение активно", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("зверь", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("beast", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConsoleCombatSource_UsesSharedMortalCombatResultBuilder()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "UI",
            "ExplorerMode",
            "ExplorerMode.MetaStoryAndStatus.cs"));

        Assert.Contains("ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(commandLine", source, StringComparison.Ordinal);
        Assert.Contains("ExplorerCommandResultConsoleRenderer.Render(_console, result)", source, StringComparison.Ordinal);
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
    public async Task TryProcessCommand_Chronicle_RendersStructuredEntriesWithoutRawFieldLabels()
    {
        await SeedMortalStateAsync();
        await WriteJsonAsync("game_state/meta/character_chronicle.json", new[]
        {
            new
            {
                entryId = "chronicle_valmont_rebirth_001",
                title = "Возвращение в Вальмонт",
                summary = "Душа Асурана вновь открыла глаза в семейной библиотеке.",
                eventType = "rebirth",
                turnNumber = 1
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хроника"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("chronicle_structured_fields");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Возвращение в Вальмонт", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Душа Асурана вновь открыла глаза", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("entryId:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("summary:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eventType:", renderedText, StringComparison.OrdinalIgnoreCase);
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
                summary = "Игрок исследует подсказки без переписывания фактов.",
                recentSignals = new[] { "прочитал письмо", "проверяет перчатку" },
                notes = "Незначительные признаки мета-влияния."
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/поведение"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("behavior_assessment");
        Assert.True(_console.Rendered.Count > 0 || _console.MarkupLines.Count > 0);
        var renderedText = ExtractRenderedText();
        Assert.Contains("Кратко", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Недавние признаки", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("summary:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recentSignals:", renderedText, StringComparison.OrdinalIgnoreCase);
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
