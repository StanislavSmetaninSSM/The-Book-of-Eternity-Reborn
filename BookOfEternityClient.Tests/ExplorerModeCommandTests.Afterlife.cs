using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using System.Linq;
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
    public void NormalizeLegacyFlatSoulRelics_SplitsRelicsIntoCanonicalCollections()
    {
        var root = JsonNode.Parse("""
        {
          "soulRelics": [
            {
              "relicId": "relic_equipped",
              "name": "Клинок Памяти",
              "gameplayStatus": {
                "equipped": true,
                "currentSlot": "weapon"
              }
            },
            {
              "relicId": "relic_stored",
              "name": "Кольцо Тишины",
              "gameplayStatus": {
                "equipped": false,
                "currentSlot": ""
              }
            }
          ]
        }
        """)!.AsObject();

        var changed = ExplorerMode.NormalizeLegacyFlatSoulRelics(root);

        Assert.True(changed);
        var relics = Assert.IsType<JsonObject>(root["soulRelics"]);
        var equipped = Assert.IsType<JsonArray>(relics["equipped"]);
        var stored = Assert.IsType<JsonArray>(relics["stored"]);
        Assert.Single(equipped);
        Assert.Single(stored);
        Assert.Equal("relic_equipped", equipped[0]?["relicId"]?.GetValue<string>());
        Assert.Equal("relic_stored", stored[0]?["relicId"]?.GetValue<string>());
    }

    [Fact]
    public async Task TryProcessCommand_GuardianProjects_RendersTrackerAndJournal()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_projects_001",
                    canonicalName = "Азалия",
                    domain = "Social",
                    abodePower = new
                    {
                        currentPower = 62,
                        tier = "Могущественная",
                        lastUpdatedAt = "2026-03-23T00:00:00Z",
                        history = Array.Empty<object>()
                    },
                    nameVariants = new
                    {
                        @default = "Азалия",
                        feminine = "Азалия",
                        masculine = (string?)null,
                        neutral = (string?)null
                    },
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Текущая тестовая форма."
                    },
                    manifestationHistory = Array.Empty<object>(),
                    relationshipData = new { currentReputation = 120, reputationHistory = Array.Empty<object>(), lastInteraction = (string?)null },
                    personalityProfile = new { archetype = "Diplomat", speechPattern = "Measured", coreValues = new[] { "связь", "страсть", "влияние" } },
                    questManagement = new { availableQuests = Array.Empty<object>(), activeQuests = Array.Empty<object>(), completedQuests = Array.Empty<object>() },
                    gachaSystem = new { chargesPerReturn = 3, chargesUsedThisReturn = 0, gachaHistory = Array.Empty<object>() },
                    mood = new { current = "focused", intensity = 60, reason = "Собирает силы", since = 1 },
                    loreFragments = Array.Empty<object>()
                }
            }
        });
        await WriteJsonAsync(GuardianProjectState.TrackerPath, new
        {
            activeProjects = new[]
            {
                new
                {
                    guardianId = "guardian_projects_001",
                    project = new
                    {
                        projectId = "guardian_project_001",
                        projectType = "abode_expansion",
                        projectTier = "major",
                        projectMode = "internal",
                        projectName = "Расширение Обители",
                        activeState = "Binding",
                        totalWork = 18,
                        workDone = 8,
                        totalStages = 3,
                        currentStage = 1,
                        pressure = 18,
                        stability = 67,
                        startedTurn = 80,
                        estimatedCompletionTurn = 95,
                        playerCanAssist = true,
                        assistDescription = "Укрепить новый контур силы."
                    }
                }
            },
            completedProjects = Array.Empty<object>(),
            temporaryProjectModifiers = Array.Empty<object>()
        });
        await WriteJsonAsync(GuardianProjectState.JournalPath, new
        {
            entries = new[]
            {
                new
                {
                    entryId = "gpj_test_001",
                    turn = 84,
                    guardianId = "guardian_projects_001",
                    projectId = "guardian_project_001",
                    eventType = "pressured",
                    visibility = "player_known",
                    title = "Проект испытывает давление",
                    summary = "Новый контур удержан, но pressure усилилось.",
                    details = new[] { "Работа: 6 -> 8", "Pressure: 8 -> 18", "Stability: 76 -> 67" }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/проекты_хранителей"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_projects");
        Assert.True(_console.Rendered.Count > 0, BuildConsoleDiagnostics("guardian_projects"));
        Assert.Contains(_console.SelectionChoicesHistory, history =>
            history.Choices.Any(choice => choice.Contains("Расширение Обители", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task TryProcessCommand_GuardianProjects_DetailUsesPlayerFacingWording()
    {
        await SeedSessionForCommandAsync("/проекты_хранителей");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/проекты_хранителей"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_projects_wording");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Внешнее давление", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Устойчивость замысла", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Pressure:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stability:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Power loss", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Pressure relief", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stability relief", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Safe pressure", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Defense rating", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Проекты Хранителей", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Any(choice => choice.Contains("Осмотр и замер контуров", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task TryProcessCommand_GuardianProjects_CompletedProjectDetailUsesPlayerFacingStateLabel()
    {
        await SeedSessionForCommandAsync("/проекты_хранителей");
        await WriteJsonAsync(GuardianProjectState.TrackerPath, new
        {
            activeProjects = Array.Empty<object>(),
            completedProjects = new[]
            {
                new
                {
                    guardianId = "guard_test_azalia",
                    project = new
                    {
                        projectId = "guardian_project_completed_002",
                        projectType = "abode_expansion",
                        projectTier = "major",
                        projectMode = "internal",
                        projectName = "Круг завершённого расширения",
                        finalState = "Completed",
                        completionTurn = 9,
                        outcome = "Обитель обрела новый устойчивый ярус.",
                        abodePowerDelta = 6
                    }
                }
            },
            temporaryProjectModifiers = Array.Empty<object>()
        });
        await WriteJsonAsync(GuardianProjectState.JournalPath, new
        {
            entries = new[]
            {
                new
                {
                    entryId = "gpj_completed_001",
                    turn = 9,
                    guardianId = "guard_test_azalia",
                    projectId = "guardian_project_completed_002",
                    eventType = "completed",
                    visibility = "player_known",
                    title = "Проект завершён",
                    summary = "Финальный контур устойчиво замкнулся.",
                    details = new[] { "Работа: 18 -> 18", "Pressure: 4 -> 0", "Stability: 78 -> 85" }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/проекты_хранителей"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_projects_completed_project_detail");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Конечное состояние", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Завершён", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Completed", renderedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_GuardianProjects_DetailShowsLifecycleAssistFieldsAndFullJournal()
    {
        _ = await Task.FromResult(0);
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "UI",
            "ExplorerMode",
            "ExplorerMode.Afterlife.GuardiansProjectsTrade.cs"));

        Assert.Contains("Ход начала:", source, StringComparison.Ordinal);
        Assert.Contains("Ожидаемый ход завершения:", source, StringComparison.Ordinal);
        Assert.Contains("Помощь души:", source, StringComparison.Ordinal);
        Assert.Contains("Описание помощи:", source, StringComparison.Ordinal);
        Assert.DoesNotContain("journalEntries.Take(8)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("details.EnumerateArray().Take(4)", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_AbodePower_DetailUsesPlayerFacingWording()
    {
        await SeedSessionForCommandAsync("/сила_обители");
        await WriteJsonAsync(GuardianPowerEventState.JournalPath, new
        {
            entries = new object[]
            {
                new
                {
                    entryId = "ape_smoke_001",
                    eventId = "ape_event_001",
                    turn = 2,
                    guardianId = "guard_test_azalia",
                    guardianName = "Азалия",
                    delta = 8,
                    reasonType = "project_completion",
                    sourceSurface = "completeGuardianProjects",
                    sourceId = "guardian_project_smoke_001",
                    title = "Проект усилил Обитель",
                    summary = "Тестовый проект поднял силу Обители Азалии.",
                    visibility = "player_known",
                    relatedGuardianId = (string?)null,
                    appliedAt = "2026-03-23T00:00:00Z",
                    audit = new
                    {
                        projectId = "guardian_project_smoke_001",
                        projectType = "abode_expansion"
                    }
                },
                new
                {
                    entryId = "ape_smoke_002",
                    eventId = "ape_event_002",
                    turn = 3,
                    guardianId = "guard_test_azalia",
                    guardianName = "Азалия",
                    delta = -4,
                    reasonType = "rival_strike",
                    sourceSurface = "updateGuardianProjects",
                    sourceId = "guardian_project_smoke_002",
                    title = "Соперник ударил по Обители",
                    summary = "Тестовый враждебный удар уменьшил силу Обители.",
                    visibility = "player_known",
                    relatedGuardianId = "guard_test_rival",
                    appliedAt = "2026-03-24T00:00:00Z",
                    audit = new
                    {
                        projectId = "guardian_project_smoke_002",
                        projectType = "offensive_intrigue"
                    }
                }
            }
        });
        await WriteJsonAsync(GuardianProjectState.TrackerPath, new
        {
            activeProjects = Array.Empty<object>(),
            completedProjects = Array.Empty<object>(),
            temporaryProjectModifiers = new[]
            {
                new
                {
                    modifierId = "temp_pressure_smoke_001",
                    guardianId = "guard_test_azalia",
                    modifierType = "next_internal_project_starting_pressure",
                    value = 3,
                    remainingApplications = 2
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/сила_обители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("abode_power_wording");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Что даёт текущая сила", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Предел враждебного давления", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Derived-эффекты", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Hostile cap", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Clues ", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Clarity ", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Последний power event", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("canonical history", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("modifierId", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Terminal state", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" value ", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" applications ", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rival-Хранителя", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("next_internal_project_starting_pressure", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Удар Хранителя-соперника", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Временных усилений 1", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_AbodePower_FullHistoryAndJournalAreNotTruncated()
    {
        await SeedSessionForCommandAsync("/сила_обители");
        var guardiansPath = _fs.ResolvePath("game_state/meta/guardians.json");
        var guardiansRoot = JsonNode.Parse(await File.ReadAllTextAsync(guardiansPath))?.AsObject()
            ?? throw new InvalidOperationException("Expected guardians state for abode power test.");
        var guardian = guardiansRoot["guardians"]?.AsArray()[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected first guardian.");
        guardian["abodePower"]!["history"] = new JsonArray(Enumerable.Range(1, 7).Select(index => (JsonNode?)new JsonObject
        {
            ["change"] = index % 2 == 0 ? -index : index,
            ["reason"] = $"История силы {index}",
            ["timestamp"] = $"2026-03-{10 + index:00}T00:00:00Z"
        }).ToArray());
        await File.WriteAllTextAsync(
            guardiansPath,
            guardiansRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        await WriteJsonAsync(GuardianPowerEventState.JournalPath, new
        {
            entries = Enumerable.Range(1, 9).Select(index => new
            {
                entryId = $"ape_full_{index:000}",
                eventId = $"ape_evt_{index:000}",
                turn = 10 + index,
                guardianId = "guard_test_azalia",
                guardianName = "Азалия",
                delta = index % 2 == 0 ? -index : index,
                reasonType = index % 2 == 0 ? "rival_strike" : "project_completion",
                sourceSurface = "completeGuardianProjects",
                sourceId = $"source_{index:000}",
                title = $"Полный журнал силы {index}",
                summary = $"Сводка журнала силы {index}.",
                visibility = "player_known",
                relatedGuardianId = index == 1 ? "guard_test_rival" : (string?)null,
                appliedAt = $"2026-03-{10 + index:00}T00:00:00Z",
                audit = new
                {
                    projectId = $"project_{index:000}",
                    projectType = "abode_expansion"
                }
            }).ToArray()
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/сила_обители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("abode_power_full_history");
        var renderedText = ExtractRenderedText();
        Assert.Contains("История силы 7", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("История силы 1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Полный журнал силы 9", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Полный журнал силы 1", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]

    public async Task TryProcessCommand_SoulInfo_RenameSoul_PersistsPreviousSoulNames()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            pendingGuardianCreation = new
            {
                description = "Тестовый хранитель",
                soulName = "Тестовая Душа"
            }
        });

        _console.QueueAnySelection("✏️ Сменить имя души", "← Назад");
        _console.QueueAskResponse("Новое имя души", "Пепельная Искра");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/душа"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("soul_rename");

        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.NotNull(soulRaw);
        Assert.Contains("\"soulName\": \"Пепельная Искра\"", soulRaw, StringComparison.Ordinal);
        Assert.Contains("\"previousSoulNames\": [", soulRaw, StringComparison.Ordinal);
        Assert.Contains("\"Тестовая Душа\"", soulRaw, StringComparison.Ordinal);

        var guardiansRaw = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        Assert.NotNull(guardiansRaw);
        Assert.Contains("\"soulName\": \"Пепельная Искра\"", guardiansRaw, StringComparison.Ordinal);
        Assert.Equal("Пепельная Искра", _stateManager.CurrentState.SoulName);
    }

    [Fact]

    public async Task TryProcessCommand_SoulInfo_RenameSoul_CancelPreviewDoesNotWrite()
    {
        await SeedAfterlifeStateAsync();

        _console.QueueAnySelection("✏️ Сменить имя души", "← Назад");
        _console.QueueAskResponse("Новое имя души", "Пепельная Искра");
        _console.QueueAnyConfirmResponse(false);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/душа"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("soul_rename_cancel");
        Assert.True(_console.ConfirmPrompts.Any(prompt => prompt.Contains("переименование души", StringComparison.OrdinalIgnoreCase)),
            BuildConsoleDiagnostics("soul_rename_cancel"));

        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.NotNull(soulRaw);
        Assert.Contains("\"soulName\": \"Тестовая Душа\"", soulRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("Пепельная Искра", soulRaw, StringComparison.Ordinal);
    }

    [Fact]

    public async Task TryProcessCommand_Guardians_SystemAttraction_WritesRequestAndReturnsGmAction()
    {
        await SeedAfterlifeStateAsync();
        await SeedSystemGuardianPresetAsync("azalia", "Азалия", "Social", "Обитель Неутолимого Пламени");
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_existing_001",
                    canonicalName = "Старый Хранитель",
                    domain = "Knowledge",
                    nameVariants = new
                    {
                        @default = "Старый Хранитель",
                        feminine = (string?)null,
                        masculine = "Старый Хранитель",
                        neutral = (string?)null
                    },
                    manifestation = new
                    {
                        currentDisplayName = "Старый Хранитель",
                        formFlexibility = "fixed",
                        currentPresentationStyle = "masculine",
                        currentPronouns = "он/его",
                        appearanceDescription = "Тестовая устойчивая форма хранителя знаний."
                    },
                    manifestationHistory = Array.Empty<object>(),
                    personalityProfile = new
                    {
                        archetype = "Archivist",
                        speechPattern = "Measured",
                        coreValues = new[] { "память", "ясность", "порядок" }
                    },
                    relationshipData = new
                    {
                        currentReputation = 10,
                        reputationHistory = Array.Empty<object>(),
                        lastInteraction = (string?)null
                    },
                    questManagement = new
                    {
                        availableQuests = Array.Empty<object>(),
                        completedQuests = Array.Empty<object>(),
                        activeQuests = Array.Empty<object>()
                    },
                    gachaSystem = new
                    {
                        chargesPerReturn = 1,
                        chargesUsedThisReturn = 0,
                        gachaHistory = Array.Empty<object>()
                    },
                    tradeInventory = new
                    {
                        cycleId = "cycle_1",
                        items = Array.Empty<object>()
                    },
                    currentProject = new
                    {
                        projectId = "proj_1",
                        name = "Наблюдение",
                        description = "Тестовый проект",
                        progressPercent = 0,
                        estimatedTurnsLeft = 3,
                        playerCanAssist = false
                    },
                    mood = new
                    {
                        current = "curious",
                        intensity = 30,
                        reason = "Наблюдает",
                        since = 1
                    },
                    loreFragments = Enumerable.Range(1, 7).Select(i => new
                    {
                        fragmentId = $"fragment_{i}",
                        title = $"Фрагмент {i}",
                        category = "domain_truth",
                        requiredReputation = 0,
                        content = (string?)null,
                        unlocked = false
                    }).ToArray(),
                    musings = Array.Empty<object>(),
                    abode = new
                    {
                        abodeId = "abode_existing_001",
                        name = "Старая Обитель",
                        isDiscovered = true
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_existing_001",
                canonicalName = "Старый Хранитель",
                nameVariants = new
                {
                    @default = "Старый Хранитель",
                    feminine = (string?)null,
                    masculine = "Старый Хранитель",
                    neutral = (string?)null
                },
                manifestation = new
                {
                    currentDisplayName = "Старый Хранитель",
                    formFlexibility = "fixed",
                    currentPresentationStyle = "masculine",
                    currentPronouns = "он/его",
                    appearanceDescription = "Тестовая устойчивая форма хранителя знаний."
                },
                manifestationHistory = Array.Empty<object>()
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_existing_001",
                discoveredAbodes = new[] { "abode_existing_001" }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        _console.QueueAnySelection(
            "🔍 Искать новую обитель (силой мысли)",
            "🧲 Притяжение к извечному хранителю",
            "Азалия (Social)",
            "✅ Выбрать");

        var result = await _explorer.TryProcessCommand("/хранители");

        Assert.NotNull(result);
        Assert.Contains("[CHAOS_SEA_SYSTEM_GUARDIAN_ATTRACTION: azalia]", result, StringComparison.Ordinal);

        var requestJson = await _fs.ReadFileAsync(SystemGuardianLibraryService.AttractionRequestPath);
        Assert.NotNull(requestJson);
        Assert.Contains("\"targetPresetId\": \"azalia\"", requestJson, StringComparison.Ordinal);
        Assert.Contains("\"targetPresetDisplayName\": \"Азалия\"", requestJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_InShiningAbode_HidesChaosSeaOnlySearchActions()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await _stateManager.RefreshGameStateAsync();

        _console.QueueAnySelection("← Назад");
        var result = await _explorer.TryProcessCommand("/хранители");

        Assert.Equal(string.Empty, result);
        AssertNoHiddenExplorerErrors("guardians_shining_hides_chaos_search");
        var allChoices = _console.SelectionChoicesHistory.SelectMany(entry => entry.Choices).ToArray();
        Assert.DoesNotContain(allChoices, choice => choice.Contains("Искать новую обитель", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(allChoices, choice => choice.Contains("Учредить собственного Хранителя", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Chaos Sea-only действия скрыты", ExtractRenderedText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]

    public async Task TryProcessCommand_Guardians_UsesSharedGuardianReputationLabelsInChoices()
    {
        await SeedSessionForCommandAsync("/хранители");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardians_reputation_choices");
        Assert.Contains(_console.SelectionChoicesHistory.SelectMany(entry => entry.Choices),
            choice => choice.Contains("Нейтральный", StringComparison.Ordinal));
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
        var sellOffers = await _guardianTradeService.GetSellableRelicsAsync("guardian_trade_001");
        var sellOffer = Assert.Single(sellOffers);
        Assert.Contains("full_sale_payload_marker", sellOffer.RelicData.ToJsonString(), StringComparison.OrdinalIgnoreCase);

        _console.QueueSelection("Выберите раздел", "💰 Продать реликвии");
        _console.QueueSelection("Действие", "🛒 Торговать");
        _console.QueueAnyConfirmResponse(true);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_trade_sell");
        var guardiansRaw = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.DoesNotContain("Реликвия для продажи", soulRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"buybackRelics\"", guardiansRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"available\"", guardiansRaw ?? string.Empty, StringComparison.Ordinal);
        var renderedText = ExtractRenderedText();
        Assert.Contains("Полный JSON продаваемой Реликвии Души", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("обратном выкупе", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_GuardianTradeBuyback_ReacquiresPreviouslySoldRelic()
    {
        await SeedGuardianTradeStateAsync(includeBuybackRelics: true);
        _console.QueueSelection("Выберите раздел", "🔁 Выкупить обратно");
        _console.QueueSelection("Действие", "🛒 Торговать", "🔁 Выкупить");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_trade_buyback");
        var guardiansRaw = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.Contains("Отзвук Зеркального Двора", soulRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"rebought\"", guardiansRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Выберите раздел", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("🔁 Выкупить обратно", StringComparer.Ordinal));
    }

    [Fact]

    public async Task TryProcessCommand_GuardianTradeWithoutInventory_CreatesRequestAndShowsWaitingStatus()
    {
        await SeedGuardianTradeStateAsync(includeTradeInventory: false);
        _console.QueueSelection("Действие", "🛒 Торговать", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        string? gmAction = null;
        var ex = await Record.ExceptionAsync(async () => gmAction = await _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_trade_inventory_request");
        Assert.Contains("GUARDIAN_TRADE_REQUEST", gmAction ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(_console.MarkupLines,
            line => line.Contains("Витрина Хранителя подготавливается", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Выберите раздел", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("🔄 Проверить витрину", StringComparer.Ordinal) &&
                     !entry.Choices.Contains("🛍 Купить реликвии", StringComparer.Ordinal));
        var renderedText = ExtractRenderedText();
        Assert.Contains("Полный предпросмотр торговли Хранителя", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending_guardian_trade_request.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UpdateGuardianTradeInventoryReceipts", renderedText, StringComparison.OrdinalIgnoreCase);
        var pendingRequestRaw = await _fs.ReadFileAsync("game_state/control/pending_guardian_trade_request.json");
        Assert.Contains("\"guardianId\": \"guardian_trade_001\"", pendingRequestRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"derivedTradeSlotCount\":", pendingRequestRaw ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"projectBonusSignature\":", pendingRequestRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_AbodesTravel_ShowsFullContractPreviewBeforeGmAction()
    {
        await WriteJsonAsync("input/turn_request.json", new { turnNumber = 14 });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 25 }
        });
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_target_001",
                    canonicalName = "Мириэль",
                    domain = "Magic",
                    abode = new
                    {
                        abodeId = "abode_target_001",
                        name = "Сад Переходов",
                        isDiscovered = true
                    },
                    manifestation = new
                    {
                        currentDisplayName = "Мириэль"
                    },
                    relationshipData = new { currentReputation = 80 }
                },
                new
                {
                    guardianId = "guardian_current_001",
                    canonicalName = "Азалия",
                    domain = "Social",
                    abode = new
                    {
                        abodeId = "abode_current_001",
                        name = "Шелковая Обитель",
                        isDiscovered = true
                    },
                    manifestation = new
                    {
                        currentDisplayName = "Азалия"
                    },
                    relationshipData = new { currentReputation = 120 }
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_current_001",
                canonicalName = "Азалия",
                manifestation = new
                {
                    currentDisplayName = "Азалия"
                }
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_current_001",
                discoveredAbodes = new[] { "abode_current_001", "abode_target_001" }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        string? gmAction = null;
        var ex = await Record.ExceptionAsync(async () => gmAction = await _explorer.TryProcessCommand("/обители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("chaos_sea_travel_preview");
        Assert.Contains("CHAOS_SEA_TRAVEL", gmAction ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("targetAbodeId=abode_target_001", gmAction ?? string.Empty, StringComparison.Ordinal);
        var renderedText = ExtractRenderedText();
        Assert.Contains("Полный предпросмотр перехода Моря Хаоса", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("guardians.json.activeGuardian = targetGuardianId", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("abode_target_001", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_AbodesTravel_IgnoresGuardianOnlyDiscoveryWithoutNavigationMembership()
    {
        await WriteJsonAsync("input/turn_request.json", new { turnNumber = 14 });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 25 }
        });
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_target_001",
                    canonicalName = "Мириэль",
                    domain = "Magic",
                    abode = new
                    {
                        abodeId = "abode_target_001",
                        name = "Сад Переходов",
                        isDiscovered = true
                    },
                    manifestation = new
                    {
                        currentDisplayName = "Мириэль"
                    },
                    relationshipData = new { currentReputation = 80 }
                },
                new
                {
                    guardianId = "guardian_current_001",
                    canonicalName = "Азалия",
                    domain = "Social",
                    abode = new
                    {
                        abodeId = "abode_current_001",
                        name = "Шелковая Обитель",
                        isDiscovered = true
                    },
                    manifestation = new
                    {
                        currentDisplayName = "Азалия"
                    },
                    relationshipData = new { currentReputation = 120 }
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_current_001",
                canonicalName = "Азалия",
                manifestation = new
                {
                    currentDisplayName = "Азалия"
                }
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "ABODE_CURRENT_001",
                discoveredAbodes = new[] { "abode_current_001" }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var gmAction = await _explorer.TryProcessCommand("/обители");

        AssertNoHiddenExplorerErrors("chaos_sea_abodes_navigation_membership");
        Assert.DoesNotContain("CHAOS_SEA_TRAVEL", gmAction ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_console.SelectionChoicesHistory.SelectMany(entry => entry.Choices),
            choice => choice.Contains("Сад Переходов", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_Abodes_ShiningAbodeDoesNotEmitChaosSeaTravel()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await _stateManager.RefreshGameStateAsync();

        var gmAction = await _explorer.TryProcessCommand("/обители");

        Assert.Equal("", gmAction);
        AssertNoHiddenExplorerErrors("shining_abode_abodes_blocked");
        Assert.DoesNotContain("CHAOS_SEA_TRAVEL", gmAction ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_AfterlifeInbox_MarksNotificationReadOnlyAfterExplicitAction()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/control/afterlife_notifications.json", new
        {
            notifications = new[]
            {
                new
                {
                    notificationId = "guardian_trade_inventory_ready:guardian_trade_req_1",
                    notificationType = "guardian_trade_inventory_ready",
                    requestId = "guardian_trade_req_1",
                    status = "unread",
                    guardianId = "guardian_trade_001",
                    guardianName = "Азалия",
                    archiveId = "",
                    archiveTitle = "",
                    targetProjectId = "",
                    targetProjectName = "",
                    summary = "Витрина Хранителя Азалия готова. Можно открывать торговлю.",
                    createdAtTurn = 8,
                    createdAtUtc = "2026-03-26T00:10:00Z"
                }
            }
        });
        _console.QueueSelection("Действие", "✅ Отметить как прочитанное");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/уведомления_загробья"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_inbox_mark_read");
        var notificationsRaw = await _fs.ReadFileAsync("game_state/control/afterlife_notifications.json");
        Assert.Contains("\"status\": \"read\"", notificationsRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]

    public async Task TryProcessCommand_AfterlifeInbox_TradeQuickActionOpensTradeWithoutAutoRead()
    {
        await SeedGuardianTradeStateAsync();
        await WriteJsonAsync("game_state/control/afterlife_notifications.json", new
        {
            notifications = new[]
            {
                new
                {
                    notificationId = "guardian_trade_inventory_ready:guardian_trade_req_1",
                    notificationType = "guardian_trade_inventory_ready",
                    requestId = "guardian_trade_req_1",
                    status = "unread",
                    guardianId = "guardian_trade_001",
                    guardianName = "Азалия",
                    archiveId = "",
                    archiveTitle = "",
                    targetProjectId = "",
                    targetProjectName = "",
                    summary = "Витрина Хранителя Азалия готова: 4 позиции. Можно открывать торговлю.",
                    createdAtTurn = 8,
                    createdAtUtc = "2026-03-26T00:10:00Z"
                }
            }
        });
        _console.QueueSelection("Выберите раздел", "← Назад");
        _console.QueueSelection("Действие", "🛒 Открыть торговлю");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/уведомления_загробья"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_inbox_trade_quick_action");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Действие", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("🛒 Открыть торговлю", StringComparer.Ordinal));
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Выберите раздел", StringComparison.OrdinalIgnoreCase));
        var notificationsRaw = await _fs.ReadFileAsync("game_state/control/afterlife_notifications.json");
        Assert.Contains("\"status\": \"unread\"", notificationsRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]

    public async Task TryProcessCommand_AfterlifeInbox_GuardianQuestQuickActionOpensGuardiansWithoutAutoRead()
    {
        await SeedGuardianTradeStateAsync(includeTradeInventory: false);
        await WriteJsonAsync("game_state/control/afterlife_notifications.json", new
        {
            notifications = new[]
            {
                new
                {
                    notificationId = "guardian_quest_available:guardian_trade_001:quest_archive_1",
                    notificationType = "guardian_quest_available",
                    requestId = "guardian_trade_001:quest_archive_1",
                    status = "unread",
                    guardianId = "guardian_trade_001",
                    guardianName = "Азалия",
                    archiveId = "",
                    archiveTitle = "",
                    targetProjectId = "",
                    targetProjectName = "",
                    summary = "У Хранителя Азалия появился архивный квест «След летописи».",
                    createdAtTurn = 8,
                    createdAtUtc = "2026-03-26T00:10:00Z"
                }
            }
        });
        _console.QueueSelection("Обители Моря Хаоса", "← Назад");
        _console.QueueSelection("Действие", "🛡️ Открыть Хранителей");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/уведомления_загробья"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_inbox_guardian_quest_quick_action");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Действие", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("🛡️ Открыть Хранителей", StringComparer.Ordinal));
        Assert.Contains(_console.SelectionTitles,
            title => title.Contains("Обители Моря Хаоса", StringComparison.OrdinalIgnoreCase));
        var notificationsRaw = await _fs.ReadFileAsync("game_state/control/afterlife_notifications.json");
        Assert.Contains("\"status\": \"unread\"", notificationsRaw ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_AfterlifeInbox_ProjectFuelNotificationShowsExactArchiveAndProjectDetails()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 2,
            inkFeathers = new { current = 25 },
            afterlifeArchive = new
            {
                stored = new object[]
                {
                    new
                    {
                        archiveId = "archive_memory_1",
                        title = "Память о пламени",
                        entryType = "lore_fragment",
                        rarity = "Rare",
                        summary = "Поддерживает работу над проектом."
                    }
                }
            }
        });
        await WriteJsonAsync(GuardianProjectState.TrackerPath, new
        {
            activeProjects = new object[]
            {
                new
                {
                    guardianId = "guardian_trade_001",
                    guardianName = "Азалия",
                    project = new
                    {
                        projectId = "project_archive_1",
                        projectName = "Свод Искр",
                        activeState = "In Progress",
                        progressPercent = 65
                    }
                }
            },
            completedProjects = Array.Empty<object>()
        });
        await WriteJsonAsync("game_state/control/afterlife_notifications.json", new
        {
            notifications = new[]
            {
                new
                {
                    notificationId = "archive_project_fuel_accepted:archive_fuel_req_1",
                    notificationType = "archive_project_fuel_accepted",
                    requestId = "archive_fuel_req_1",
                    status = "unread",
                    guardianId = "guardian_trade_001",
                    guardianName = "Азалия",
                    archiveId = "archive_memory_1",
                    archiveTitle = "Память о пламени",
                    targetProjectId = "project_archive_1",
                    targetProjectName = "Свод Искр",
                    summary = "Хранитель Азалия усилил проект «Свод Искр»: работа +2.",
                    createdAtTurn = 12,
                    createdAtUtc = "2026-04-19T12:00:00Z"
                }
            }
        });
        _console.QueueSelection("Действие", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/уведомления_загробья"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_inbox_exact_archive_project_detail");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Связанная запись Архива", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Память о пламени", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Связанный проект", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Свод Искр", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Прогресс: 65%", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_AfterlifeInbox_ProjectFuelNotificationUsesStoredSnapshotWhenLiveStateIsGone()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 2,
            inkFeathers = new { current = 25 },
            afterlifeArchive = new
            {
                stored = Array.Empty<object>(),
                actionReceipts = Array.Empty<object>()
            }
        });
        await WriteJsonAsync(GuardianProjectState.TrackerPath, new
        {
            activeProjects = Array.Empty<object>(),
            completedProjects = Array.Empty<object>()
        });
        await WriteJsonAsync("game_state/control/afterlife_notifications.json", new
        {
            notifications = new[]
            {
                new
                {
                    notificationId = "archive_project_fuel_accepted:archive_fuel_req_snapshot",
                    notificationType = "archive_project_fuel_accepted",
                    requestId = "archive_fuel_req_snapshot",
                    status = "unread",
                    guardianId = "guardian_trade_001",
                    guardianName = "Азалия",
                    archiveId = "archive_memory_1",
                    archiveTitle = "Память о пламени",
                    archiveEntryType = "secret_record",
                    archiveRarity = "Rare",
                    archiveSummary = "Историческая архивная запись о пламени.",
                    targetProjectId = "project_archive_1",
                    targetProjectName = "Свод Искр",
                    targetProjectStateLabel = "Completed",
                    targetProjectProgressPercent = 65,
                    summary = "Хранитель Азалия усилил проект «Свод Искр»: работа +2.",
                    createdAtTurn = 12,
                    createdAtUtc = "2026-04-19T12:00:00Z"
                }
            }
        });
        _console.QueueSelection("Действие", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/уведомления_загробья"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_inbox_exact_archive_project_snapshot_detail");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Связанная запись Архива", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Память о пламени", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Запись Тайны", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Редкая", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Историческая архивная запись о пламени.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Связанный проект", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Свод Искр", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Прогресс: 65%", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]

    public async Task TryProcessCommand_Guardians_ShowsUnreadTradeBannerInPromptTitle()
    {
        await SeedGuardianTradeStateAsync();
        await WriteJsonAsync("game_state/control/afterlife_notifications.json", new
        {
            notifications = new[]
            {
                new
                {
                    notificationId = "guardian_trade_inventory_ready:guardian_trade_req_2",
                    notificationType = "guardian_trade_inventory_ready",
                    requestId = "guardian_trade_req_2",
                    status = "unread",
                    guardianId = "guardian_trade_001",
                    guardianName = "Азалия",
                    archiveId = "",
                    archiveTitle = "",
                    targetProjectId = "",
                    targetProjectName = "",
                    summary = "Витрина Хранителя Азалия готова: 4 позиции. Можно открывать торговлю.",
                    createdAtTurn = 9,
                    createdAtUtc = "2026-03-26T00:11:00Z"
                }
            }
        });
        _console.QueueSelection("Обители Моря Хаоса", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_trade_banner");
        Assert.Contains(_console.SelectionTitles,
            title => title.Contains("Непрочитанные ответы по торговле", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GuardianDetailPanel_ShowsActiveAndAvailableQuestsAsSeparateBlocks()
    {
        await SeedGuardianTradeStateAsync();
        var guardiansPath = _fs.ResolvePath("game_state/meta/guardians.json");
        var guardiansRoot = JsonNode.Parse(await File.ReadAllTextAsync(guardiansPath))?.AsObject()
            ?? throw new InvalidOperationException("Expected guardians state.");
        var guardian = guardiansRoot["guardians"]?.AsArray()?[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected guardian.");
        guardian["questManagement"] = new JsonObject
        {
            ["activeQuests"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "Текущий дозор",
                    ["description"] = "Хранитель уже ведёт эту линию.",
                    ["difficulty"] = "Сложно",
                    ["status"] = "In Progress",
                    ["rewards"] = new JsonObject
                    {
                        ["experience"] = 15,
                        ["items"] = new JsonArray("Архивный знак")
                    }
                }
            },
            ["availableQuests"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "Новый зов архива",
                    ["description"] = "Можно начать после совета.",
                    ["difficulty"] = "Средне"
                }
            },
            ["completedQuests"] = new JsonArray()
        };
        await File.WriteAllTextAsync(
            guardiansPath,
            guardiansRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Действие", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        using var guardiansDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/guardians.json");
        var detailMethod = typeof(ExplorerMode).GetMethod("ShowGuardianDetailPanel", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(detailMethod);

        var task = detailMethod!.Invoke(_explorer, new object?[]
        {
            guardiansDoc!.RootElement.GetProperty("guardians")[0],
            null,
            string.Empty,
            string.Empty,
            null
        }) as Task;

        await (task ?? throw new InvalidOperationException("Expected guardian detail task."));

        AssertNoHiddenExplorerErrors("guardian_detail_quest_blocks");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Активные задания", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Текущий дозор", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Опыт: 15", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Предметы: Архивный знак", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("experience: 15", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Доступные задания", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Новый зов архива", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]

    public async Task TryProcessCommand_Guardians_ShowsUnreadGuardianQuestBannerInPromptTitle()
    {
        await SeedGuardianTradeStateAsync();
        await WriteJsonAsync("game_state/control/afterlife_notifications.json", new
        {
            notifications = new[]
            {
                new
                {
                    notificationId = "guardian_quest_available:guardian_trade_001:quest_archive_2",
                    notificationType = "guardian_quest_available",
                    requestId = "guardian_trade_001:quest_archive_2",
                    status = "unread",
                    guardianId = "guardian_trade_001",
                    guardianName = "Азалия",
                    archiveId = "",
                    archiveTitle = "",
                    targetProjectId = "",
                    targetProjectName = "",
                    summary = "У Хранителя Азалия появилась новая квестовая зацепка «След архива».",
                    createdAtTurn = 9,
                    createdAtUtc = "2026-03-26T00:11:00Z"
                }
            }
        });
        _console.QueueSelection("Обители Моря Хаоса", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_quest_banner");
        Assert.Contains(_console.SelectionTitles,
            title => title.Contains("Новые квесты Хранителей", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]

    public async Task TryProcessCommand_SoulInfo_ShowsUnreadAfterlifeSummaryInRenderedPanel()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/control/afterlife_notifications.json", new
        {
            notifications = new[]
            {
                new
                {
                    notificationId = "guardian_trade_inventory_ready:guardian_trade_req_3",
                    notificationType = "guardian_trade_inventory_ready",
                    requestId = "guardian_trade_req_3",
                    status = "unread",
                    guardianId = "guardian_trade_001",
                    guardianName = "Азалия",
                    archiveId = "",
                    archiveTitle = "",
                    targetProjectId = "",
                    targetProjectName = "",
                    summary = "Витрина Хранителя Азалия готова: 4 позиции. Можно открывать торговлю.",
                    createdAtTurn = 10,
                    createdAtUtc = "2026-03-26T00:12:00Z"
                },
                new
                {
                    notificationId = "archive_consultation_accepted:archive_req_3",
                    notificationType = "archive_consultation_accepted",
                    requestId = "archive_req_3",
                    status = "unread",
                    guardianId = "guardian_trade_001",
                    guardianName = "Азалия",
                    archiveId = "archive_3",
                    archiveTitle = "Хроника северной тени",
                    targetProjectId = "",
                    targetProjectName = "",
                    summary = "Хранитель Азалия принял архивную консультацию по записи «Хроника северной тени»: гарантирован 1 квест Хранителя.",
                    createdAtTurn = 10,
                    createdAtUtc = "2026-03-26T00:13:00Z"
                }
            }
        });
        _console.QueueSelection("Действие души", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/душа"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("soul_info_unread_summary");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Непрочитанные ответы Хранителей: 2", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Витрина Хранителя Азалия готова: 4 позиции", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SoulInfo_NamedMortalRealm_ShowsManifestationHint()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Неон-Сити",
            currentIncarnation = 2,
            inkFeathers = new { current = 25 }
        });
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "manifest_req_named_world",
                    relicId = "relic_companion_echo_001",
                    relicName = "Отзвук Спутника",
                    manifestationSource = "imprint_relic",
                    targetIncarnation = 2,
                    companionNameHint = "Старый Друг"
                }
            }
        });
        _console.QueueSelection("Действие души", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/душа"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("soul_info_named_world_manifestation_hint");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Эхо спутников ищет путь в эту жизнь: 1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Старый Друг", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SoulInfo_ShowsAllManifestationRequestsWithoutSilentCap()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Неон-Сити",
            currentIncarnation = 2,
            inkFeathers = new { current = 25 }
        });
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = Enumerable.Range(1, 5).Select(index => new
            {
                requestId = $"manifest_req_{index}",
                relicId = $"relic_companion_echo_{index:000}",
                relicName = $"Отзвук {index}",
                manifestationSource = "imprint_relic",
                targetIncarnation = 2,
                companionNameHint = $"Спутник {index}"
            }).ToArray()
        });
        _console.QueueSelection("Действие души", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/душа"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("soul_info_manifestation_full_list");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Эхо спутников ищет путь в эту жизнь: 5", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Спутник 1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Спутник 5", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SoulInfo_ShowsExpandedCanonicalSoulState()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Новая Искра",
            currentRealm = "Chaos Sea",
            currentIncarnation = 4,
            inkFeathers = new { current = 17, total = 42 },
            enlightenment = new
            {
                currentTier = "Вознесённый",
                level = 6,
                experience = 180,
                progressPercent = 72
            },
            previousSoulNames = new[] { "Тестовая Душа", "Пепельная Искра" },
            pendingMemoryLegacy = new
            {
                legacyId = "legacy_focus_001",
                legacyType = "startingCharacteristicBonus",
                sourceLifeHint = "Жизнь Пепельной Искры",
                grantSource = "memoryLegacyGrant",
                applicationState = "applied-awaiting-turn-accept",
                grantedAtUtc = "2026-04-19T10:15:00Z",
                characteristic = "Mind",
                bonus = 3,
                grantSnapshot = new
                {
                    legacyId = "legacy_focus_001",
                    legacyType = "startingCharacteristicBonus",
                    characteristic = "Mind",
                    bonus = 3,
                    summary = "Ум станет острее в следующей жизни."
                }
            }
        });
        _console.QueueSelection("Действие души", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/душа"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("soul_info_expanded_state");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Чернильные Перья сейчас: 17", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Всего получено Чернильных Перьев: 42", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Текущий тир: Вознесённый", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Уровень: 6", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Опыт просветления: 180", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Прогресс до следующего тира: 72%", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Прежние имена: Тестовая Душа, Пепельная Искра", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Состояние применения: applied-awaiting-turn-accept", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Снимок дара:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Размер бонуса: 3", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_InkFeathers_ShiningAbode_UsesAfterlifeActions()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 1,
            inkFeathers = new { current = 120 }
        });
        _console.QueueSelection("Выберите действие", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/перья"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("ink_feathers_shining_abode_afterlife");
        var actionPrompt = Assert.Single(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Выберите действие", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(actionPrompt.Choices, choice => choice.Contains("Пожертвовать Хранителю", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(actionPrompt.Choices, choice => choice.Contains("Открыть Судьбу", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_InkFeathers_AfterlifeDonateShowsExactFormulaBeforeSpend()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 120 }
        });
        _console.QueueSelection("Выберите действие", "🎁 Пожертвовать Хранителю (−18 🪶)");
        _console.QueueAnySelection("❌ Отмена");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/перья"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("ink_feathers_afterlife_donate_formula");
        var renderedText = ExtractRenderedText();
        Assert.Contains("reputationChange = min(25, max(15, cost / 3))", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expected reputationChange = 15", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stateEvidence.guardianId", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_InkFeathers_AfterlifeMemoryGatesShowsReplacementContract()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 120 }
        });
        _console.QueueSelection("Выберите действие", "🧠 Открыть Врата Памяти (−24 🪶)");
        _console.QueueAnySelection("❌ Отмена");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/перья"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("ink_feathers_afterlife_memory_gates_contract");
        var renderedText = ExtractRenderedText();
        Assert.Contains("metaStateUpdates.memoryLegacyGrant", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pendingMemoryLegacy", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("заменяет старое наследие", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Canonical after payload schema", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("grantSnapshot", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("applicationState: pending", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("startingCharacteristicBonus", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("startingPassiveKnowledgeSkill", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("memoryLegacyGrant", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sourceLifeHint", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("group=Knowledge", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("playerStatBonus", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("optional source ids/context", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stat_bonus", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("knowledge_skill", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("grantSource: memory_gates", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("source ids/context: carry sourceLifeHint", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_DirectGachaShowsBaseMechanicsAndCostPhraseBeforeSpend()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 50 }
        });
        _console.QueueSelection("Выберите действие", "🎰 Вытянуть реликвию из Моря Хаоса");
        _console.QueueAnySelection("❌ Отмена");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/gacha"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("direct_gacha_base_mechanics");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Пороги: 4-48 Common, 49-67 Uncommon, 68-75 Rare, 76-79 Epic, 80 Legendary", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 Чернильных Перьев", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("валидатор извлекает", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("diceUsed", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("baseRarity", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("guardian modifiers", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("finalRarity должен точно совпасть с baseRarity", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ровно одну новую Soul Relic", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("не списывает Чернильные Перья второй раз", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending_turn_snapshot", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TriggerLifeEnd", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("не ниже baseRarity", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SoulRelics_ShiningAbode_AllowsManagement()
    {
        await SeedSessionForCommandAsync("/душа");
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 1,
            inkFeathers = new { current = 10 },
            soulRelics = new
            {
                stored = new[]
                {
                    new
                    {
                        relicId = "relic_test_001",
                        name = "Искра Памяти",
                        description = "Реликвия для проверки режима управления.",
                        rarity = "Rare"
                    }
                },
                equipped = Array.Empty<object>()
            }
        });
        _console.QueueSelection("Действие души", "💎 Реликвии души");
        _console.QueueSelection("✨", "← Назад");
        _console.QueueAnySelection("← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/душа"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("soul_relics_shining_abode_management");
        Assert.Contains(_console.SelectionTitles,
            title => title.Contains("выберите для просмотра / управления", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(_console.SelectionTitles,
            title => title.Contains("только просмотр", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_OverviewShowsHallAndRadiantActorSummaries()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        _console.QueueSelection("Сияющая Обитель", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_structure_summary");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Залы Обители", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Зал Рассветного Хора", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Поющие своды собирают отзвуки верности.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Светозарные акторы", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Лиора Светоносная", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Хранит спор о преемнике.", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_OverviewUsesRussianPreparedPackageLabels()
    {
        await SeedShiningInspectionStateAsync();
        _console.QueueSelection("Сияющая Обитель", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_structure_prepared_package_wording");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Зафиксированные карты: Тропа возвращения", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Полный зафиксированный набор карт", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("уровень сияния", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("уровень торговли", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("currentReturnCycleId=return_7", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Выбранные идентификаторы карт", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Выбранные card id", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("frozen payload", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("(tier ", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_GatesInspectionShowsFrozenPackagePayload()
    {
        await SeedShiningInspectionStateAsync();
        _console.QueueSelection("Сияющая Обитель", "🔎 Осмотреть набор и пакет");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_gates_full_inspection");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Сияющая Обитель", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("🔎 Осмотреть набор и пакет", StringComparer.Ordinal));
        var renderedText = ExtractRenderedText();
        Assert.Contains("Подготовленный пакет новой жизни", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Лимит выбора: 3", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Размер исходного набора: 8", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Зафиксированные карты: Тропа возвращения", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Зафиксирован на ходу: 155", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Эффект:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Осталось использований: 1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Семя маршрута", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Техническая раскладка эффекта для диагностики", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"routeSeedId\": \"route_dawn\"", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Показанные идентификаторы карт", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Выбранные идентификаторы карт", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_AllFactionAuditShowsResidentsAndProjects()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        _console.QueueSelection("Сияющая Обитель", "🧭 Сводный аудит резидентов и проектов");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_resident_project_audit");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Сводный аудит резидентов и проектов", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Хор Рассвета", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Residents:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Projects:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Песнь согласия", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("loyalty=", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_GatesInspectionShowsRerolledCandidateDetails()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var gates = shiningRoot["gates"]?.AsObject()
            ?? throw new InvalidOperationException("Expected gates.");
        var availableCards = gates["availableBlessingCards"]?.AsArray()
            ?? throw new InvalidOperationException("Expected available cards.");
        for (var i = availableCards.Count - 1; i >= 0; i--)
        {
            if (availableCards[i] is JsonObject card &&
                string.Equals(card["cardId"]?.GetValue<string>(), "card_route_dawn", StringComparison.OrdinalIgnoreCase))
            {
                availableCards.RemoveAt(i);
            }
        }

        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Сияющая Обитель", "🔎 Осмотреть набор и пакет");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_gates_rerolled_candidate_details");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Карты, показанные Вратами", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тропа возвращения", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Осталось использований: 1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("уже не входит в текущий набор выбора", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_TradeLifecycleInspectionLabelsSameCycleFallbackAsHistorical()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var faction = shiningRoot["factions"]?.AsArray()[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected faction.");
        faction["tradeInventoryReceipts"] = new JsonArray
        {
            new JsonObject
            {
                ["requestId"] = "historical_same_cycle_receipt",
                ["factionId"] = "faction_dawn",
                ["factionName"] = "Хор Рассвета",
                ["tradeCycleId"] = "shining_return_7",
                ["status"] = "ready",
                ["itemCount"] = 2,
                ["resolvedAtTurn"] = 154,
                ["resolvedAtUtc"] = "2026-04-19T10:02:00Z"
            }
        };
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Сияющая Обитель", "🧾 Осмотреть торговые циклы", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_trade_same_cycle_fallback");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Последняя запись этого цикла", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Подтверждение исхода:\r\n    [dim]Строгое подтверждение текущего контракта не найдено", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_OverviewMarksWrongCycleInventoryAsStale()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var faction = shiningRoot["factions"]?.AsArray()[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected faction.");
        faction["tradeInventory"]!.AsObject()["tradeCycleId"] = "shining_return_stale";
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueAnySelection("⚒ Торговля и кузня", "← Назад", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_trade_overview_stale_inventory");
        var renderedText = ExtractRenderedText();
        Assert.Contains("витрина: устарела или не совпадает с текущим контрактом", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("витрина: готова", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningPolitics_FactionInspectionShowsResidentPoliticalState()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var dawnFaction = shiningRoot["factions"]?.AsArray()[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected first Shining faction.");
        dawnFaction["leadership"] = new JsonObject
        {
            ["headActorType"] = "player_soul",
            ["headActorId"] = "player_soul",
            ["leadershipState"] = "secure"
        };
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Политика Сияющей Обители", "👥 Осмотреть политическое состояние фракции");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_politics"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_political_resident_inspection");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Политическое состояние фракции", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Базовая сила", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Любимый архетип проектов", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Покровительствующая семья эффекта", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Инвестиций за это Вознесение", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Архетипы проектов, уже учтённые за это Вознесение", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Устав", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Мираэль", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сэль", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Роль во фракции", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Лояльность к фракции", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тир лояльности", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Внутреннее брожение", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Состояние перестройки", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Глава: душа игрока", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Проекты фракции", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Песнь согласия", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тональность: radiant", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идентификатор проекта: project_social", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Project id", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryProcessCommand_ShiningPolitics_FoundingHallServicePromptUsesReadableLabels()
    {
        _console.QueueSelection("Дополнительная служба зала", "знание");
        var method = typeof(ExplorerMode).GetMethod("PromptFoundingHallServiceTags", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var result = method!.Invoke(_explorer, new object[] { ShiningAbodeState.EffectFamilySocial }) as List<string>;

        Assert.NotNull(result);
        Assert.Contains(ShiningAbodeState.HallServiceTagSocial, result!);
        Assert.Contains(ShiningAbodeState.HallServiceTagLore, result!);
        Assert.Contains(_console.SelectionTitles,
            title => title.Contains("социальная поддержка", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(_console.SelectionTitles,
            title => title.Contains("requiredPrimary", StringComparison.OrdinalIgnoreCase) ||
                     title.Contains("social", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Choices.Contains("знание", StringComparer.Ordinal));
        Assert.DoesNotContain(_console.SelectionChoicesHistory,
            entry => entry.Choices.Contains(ShiningAbodeState.HallServiceTagLore, StringComparer.Ordinal));
    }

    [Fact]
    public async Task TryProcessCommand_ShiningPolitics_PendingInspectionShowsFullPoliticalContracts()
    {
        await SeedShiningInspectionStateAsync();
        await WriteJsonAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "founding_pending_1",
                    proposedFactionId = "faction_dawn_pending",
                    proposedHallId = "hall_dawn_pending",
                    proposedHallName = "Зал Грядущего Рассвета",
                    proposedHallDescription = "Своды готовят новый хор.",
                    proposedHallServiceTags = new[] { "social", "lore" },
                    charter = new
                    {
                        factionName = "Грядущий Хор",
                        favoredArchetype = "accord",
                        patronEffectFamily = "social",
                        summary = "Собирает утренние клятвы."
                    },
                    supportingResidentIds = new[] { "resident_mirael", "resident_sel" },
                    quotedCostFeathers = ShiningFactionRequestState.FactionFoundingCostFeathers,
                    quotedCostLightSparks = ShiningFactionRequestState.FactionFoundingCostLightSparks,
                    createdAtTurn = 161,
                    createdAtUtc = "2026-04-19T11:15:00Z"
                }
            }
        });
        await WriteJsonAsync(ShiningFactionRequestState.PendingRealignmentsRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "realignment_pending_1",
                    residentId = "resident_orin",
                    residentName = "Орин",
                    sourceFactionId = "faction_memory",
                    sourceFactionName = "Хранители Отзвука",
                    targetFactionId = "faction_dawn",
                    targetFactionName = "Хор Рассвета",
                    realignmentMode = "accepted_transfer",
                    factionLoyaltyLevel = 18,
                    factionLoyaltyTier = "uncertain",
                    factionRestlessness = 67,
                    factionRealignmentState = "considering_realignment",
                    createdAtTurn = 162,
                    createdAtUtc = "2026-04-19T11:20:00Z"
                }
            }
        });
        await WriteJsonAsync(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "leadership_pending_1",
                    factionId = "faction_dawn",
                    factionName = "Хор Рассвета",
                    transitionMode = "peaceful_succession",
                    incumbentHeadActorType = "guardian",
                    incumbentHeadActorId = "guard_test_founder",
                    candidateHeadActorType = "resident",
                    candidateHeadActorId = "resident_mirael",
                    supportingResidentIds = new[] { "resident_sel", "resident_orin" },
                    createdAtTurn = 163,
                    createdAtUtc = "2026-04-19T11:25:00Z"
                }
            }
        });
        _console.QueueSelection("Политика Сияющей Обители", "📝 Осмотреть ожидающие политические запросы", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_politics"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_pending_political_inspection");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Политика Сияющей Обители", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("📝 Осмотреть ожидающие политические запросы", StringComparer.Ordinal));
        var renderedText = ExtractRenderedText();
        Assert.Contains("Зал Грядущего Рассвета", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идентификатор запроса: founding_pending_1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Службы зала", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Стоимость: 25 Перьев / 15 Искр Света", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Орин", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Режим перехода: согласованный переход", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("задумывается о переходе", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Кандидат:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("резидент Мираэль", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resident:resident_mirael", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Создан в UTC: 2026-04-19T11:25:00Z", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refused|withdrawn", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_CoreReceiptInspectionShowsCanonicalOutcomePayload()
    {
        await SeedShiningInspectionStateAsync();
        _console.QueueSelection("Сияющая Обитель", "📜 Осмотреть исходы Обители", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_core_receipt_inspection");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Сияющая Обитель", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("📜 Осмотреть исходы Обители", StringComparer.Ordinal));
        var renderedText = ExtractRenderedText();
        Assert.Contains("Причина решения: основание принято", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("founding_accepted", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Новые резиденты", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Мираэль", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Стартовые проекты", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Песнь согласия", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Зафиксированные карты", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тропа возвращения", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Added properties", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Resolved core-action receipts", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Resolved political receipts", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_CoreReceiptInspectionShowsGachaAndForgeIds()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var receipts = shiningRoot["coreActionReceipts"]?.AsArray()
            ?? throw new InvalidOperationException("Expected core action receipts.");
        receipts.Add(new JsonObject
        {
            ["requestId"] = "core_gacha_dawn_2",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypePullRelicGacha,
            ["factionId"] = "faction_dawn",
            ["factionName"] = "Хор Рассвета",
            ["relicId"] = "relic_gacha_dawn_2",
            ["relicName"] = "Перо Хора",
            ["baseRarity"] = "rare",
            ["finalRarity"] = "epic",
            ["returnCycleId"] = "return_7",
            ["resolvedAtTurn"] = 162,
            ["resolvedAtUtc"] = "2026-04-19T11:10:00Z",
            ["status"] = "accepted",
            ["reason"] = "relic_gacha_ready"
        });
        receipts.Add(new JsonObject
        {
            ["requestId"] = "core_forge_dawn_2",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
            ["factionId"] = "faction_dawn",
            ["factionName"] = "Хор Рассвета",
            ["relicId"] = "relic_routeglass",
            ["relicName"] = "Стекло Пути",
            ["targetFormTag"] = "solar_crown",
            ["resolvedAtTurn"] = 161,
            ["resolvedAtUtc"] = "2026-04-19T11:05:00Z",
            ["status"] = "accepted",
            ["reason"] = "relic_reshaped"
        });
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Сияющая Обитель", "📜 Осмотреть исходы Обители", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_core_receipt_ids");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Баннер:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("faction_dawn", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("relic_gacha_dawn_2", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Фракция кузни: faction_dawn", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("relic_routeglass", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_CoreReceiptInspectionUsesStableDiscoverySnapshotAfterStateMutation()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var discoveryReceipt = shiningRoot["coreActionReceipts"]?.AsArray()?[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected discovery receipt.");
        discoveryReceipt["hallName"] = "Исторический Зал Рассветного Хора";
        discoveryReceipt["factionName"] = "Исторический Хор Рассвета";
        discoveryReceipt["charterSummary"] = "Исторический устав открытия.";
        discoveryReceipt["favoredArchetype"] = "accord";
        discoveryReceipt["patronEffectFamily"] = "social";
        discoveryReceipt["newResidentNames"] = new JsonArray("Мираэль Историческая", "Сэль Историческая");
        discoveryReceipt["seededProjectNames"] = new JsonArray("Историческая Песнь", "Историческая Тропа");
        shiningRoot["halls"]!.AsArray()[0]!.AsObject()["hallName"] = "Текущее имя зала";
        shiningRoot["factions"]!.AsArray()[0]!.AsObject()["charter"]!["factionName"] = "Текущее имя фракции";
        shiningRoot["factions"]!.AsArray()[0]!.AsObject()["projects"]!.AsArray()[0]!.AsObject()["displayName"] = "Текущий проект";
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        await _stateManager.RefreshGameStateAsync();

        var loadContextMethod = typeof(ExplorerMode).GetMethod("LoadShiningContextAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(loadContextMethod);
        var loadContextTask = loadContextMethod!.Invoke(_explorer, Array.Empty<object>()) as Task;
        await (loadContextTask ?? throw new InvalidOperationException("Expected Shining context task."));
        var context = loadContextTask.GetType().GetProperty("Result")!.GetValue(loadContextTask);
        Assert.NotNull(context);
        var showPanelMethod = typeof(ExplorerMode).GetMethod("ShowShiningCoreReceiptInspectionPanel", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(showPanelMethod);

        var ex = Record.Exception(() => showPanelMethod!.Invoke(_explorer, new[] { context }));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_core_receipt_snapshot_history");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Исторический Зал Рассветного Хора", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Исторический Хор Рассвета", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Исторический устав открытия.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Мираэль Историческая", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Историческая Песнь", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Текущее имя зала", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Текущее имя фракции", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Текущий проект", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningPolitics_FoundingInspectionUsesStableReceiptSnapshotAfterStateMutation()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var foundingReceipt = shiningRoot["factionFoundingReceipts"]?.AsArray()[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected founding receipt.");
        foundingReceipt["hallDescription"] = "Историческое описание зала.";
        foundingReceipt["hallServiceTags"] = new JsonArray("social", "lore");
        foundingReceipt["factionName"] = "Исторический Хор Рассвета";
        foundingReceipt["charterSummary"] = "Исторический устав фракции.";
        foundingReceipt["favoredArchetype"] = "accord";
        foundingReceipt["patronEffectFamily"] = "social";
        shiningRoot["halls"]!.AsArray()[0]!.AsObject()["description"] = "Новое текущее описание зала.";
        shiningRoot["halls"]!.AsArray()[0]!.AsObject()["serviceTags"] = new JsonArray("memory");
        shiningRoot["factions"]!.AsArray()[0]!.AsObject()["charter"] = new JsonObject
        {
            ["factionName"] = "Текущий Хор Рассвета",
            ["favoredArchetype"] = "remembrance",
            ["patronEffectFamily"] = "memory",
            ["summary"] = "Текущий устав фракции."
        };
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Политика Сияющей Обители", "📜 Осмотреть решения фракций", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_politics"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_political_snapshot_history");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Историческое описание зала.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Исторический Хор Рассвета", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Исторический устав фракции.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Новое текущее описание зала.", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_TradeReceiptSummaryUsesStableSoldOutSnapshot()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var dawnFaction = shiningRoot["factions"]?.AsArray()[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected dawn faction.");
        var tradeReceipt = dawnFaction["tradeInventoryReceipts"]?.AsArray()[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected trade receipt.");
        tradeReceipt["soldOutCount"] = 1;
        dawnFaction["tradeInventory"]!["items"]!.AsArray()[0]!.AsObject()["soldOut"] = false;
        dawnFaction["tradeInventory"]!["items"]!.AsArray()[1]!.AsObject()["soldOut"] = false;
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Сияющая Обитель", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_trade_receipt_snapshot");
        var renderedText = ExtractRenderedText();
        Assert.Contains("распродано 1/2", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_CoreReceiptInspectionKeepsStableProjectIdentityAfterRename()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var dawnFaction = shiningRoot["factions"]?.AsArray()[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected dawn faction.");
        dawnFaction["charter"]!["factionName"] = "Текущее имя фракции";
        dawnFaction["projects"]!.AsArray()[0]!.AsObject()["displayName"] = "Текущее имя проекта";
        shiningRoot["coreActionReceipts"]!.AsArray().Add(new JsonObject
        {
            ["requestId"] = "core_project_complete_1",
            ["actionType"] = "complete_project",
            ["factionId"] = "faction_dawn",
            ["factionName"] = "Хор Рассвета",
            ["projectId"] = "project_social",
            ["projectName"] = "Песнь согласия",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = 0,
            ["resolvedAtTurn"] = 158,
            ["resolvedAtUtc"] = "2026-04-19T10:30:00Z",
            ["status"] = "accepted",
            ["reason"] = "project_completed"
        });
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Сияющая Обитель", "📜 Осмотреть исходы Обители", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_core_receipt_identity_snapshot");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Песнь согласия", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project_social", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("faction_dawn", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_CoreReceiptInspectionUsesStableSelectedCardSnapshot()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var packageReceipt = shiningRoot["coreActionReceipts"]?.AsArray()
            ?.OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(item["actionType"]?.GetValue<string>(), "prepare_incarnation_package", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Expected prepare package receipt.");
        packageReceipt["selectedCards"] = new JsonArray(new JsonObject
        {
            ["cardId"] = "card_route_dawn",
            ["dedupeKey"] = "route_dawn",
            ["sourceType"] = "project",
            ["sourceFactionId"] = "faction_dawn",
            ["sourceActorId"] = "project_passage",
            ["effectFamily"] = "route",
            ["rarity"] = "Epic",
            ["displayName"] = "Тропа возвращения",
            ["displaySummary"] = "Открывает путь через память.",
            ["effectPayload"] = new JsonObject
            {
                ["routeSeedId"] = "route_dawn",
                ["remainingUses"] = 1
            }
        });
        shiningRoot["preparedIncarnationPackage"] = new JsonObject
        {
            ["generatedFromDraftVersion"] = 4,
            ["preparedAtTurn"] = 160,
            ["preparedAtUtc"] = "2026-04-19T11:00:00Z",
            ["selectedCardIds"] = new JsonArray("card_social_dawn"),
            ["selectedCards"] = new JsonArray(new JsonObject
            {
                ["cardId"] = "card_social_dawn",
                ["dedupeKey"] = "social_dawn",
                ["sourceType"] = "head",
                ["sourceFactionId"] = "faction_dawn",
                ["sourceActorId"] = "guard_test_founder",
                ["effectFamily"] = "social",
                ["rarity"] = "Rare",
                ["displayName"] = "Песнь Рассвета",
                ["displaySummary"] = "Укрепляет союз.",
                ["effectPayload"] = new JsonObject
                {
                    ["relationshipBoost"] = 12,
                    ["meetingTag"] = "dawn_choir"
                }
            })
        };
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Сияющая Обитель", "📜 Осмотреть исходы Обители", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_prepare_receipt_snapshot");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Зафиксированные карты", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тропа возвращения", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("card_route_dawn", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("путь", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("effectPayload", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Семя маршрута", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"routeSeedId\"", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_GatesInspectionUsesFrozenBlessingSourceSnapshots()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var gates = shiningRoot["gates"]?.AsObject()
            ?? throw new InvalidOperationException("Expected gates.");
        foreach (var card in gates["availableBlessingCards"]?.AsArray()?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
        {
            var cardId = card["cardId"]?.GetValue<string>() ?? string.Empty;
            if (string.Equals(cardId, "card_social_dawn", StringComparison.OrdinalIgnoreCase))
            {
                card["sourceFactionName"] = "Хор Рассвета";
                card["sourceActorName"] = "Северин";
            }
            else if (string.Equals(cardId, "card_route_dawn", StringComparison.OrdinalIgnoreCase))
            {
                card["sourceFactionName"] = "Хор Рассвета";
                card["sourceActorName"] = "Тропа возвращения";
            }
        }
        foreach (var card in gates["allCandidateBlessingCards"]?.AsArray()?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
        {
            var cardId = card["cardId"]?.GetValue<string>() ?? string.Empty;
            if (string.Equals(cardId, "card_social_dawn", StringComparison.OrdinalIgnoreCase))
            {
                card["sourceFactionName"] = "Хор Рассвета";
                card["sourceActorName"] = "Северин";
            }
            else if (string.Equals(cardId, "card_route_dawn", StringComparison.OrdinalIgnoreCase))
            {
                card["sourceFactionName"] = "Хор Рассвета";
                card["sourceActorName"] = "Тропа возвращения";
            }
        }
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var guardiansPath = _fs.ResolvePath("game_state/meta/guardians.json");
        var guardiansRoot = JsonNode.Parse(await File.ReadAllTextAsync(guardiansPath))?.AsObject()
            ?? throw new InvalidOperationException("Expected guardians state.");
        var founderGuardian = guardiansRoot["guardians"]?.AsArray()?.OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(item["guardianId"]?.GetValue<string>(), "guard_test_founder", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Expected founder guardian.");
        founderGuardian["canonicalName"] = "Северин Новый";
        founderGuardian["manifestation"]!["currentDisplayName"] = "Северин Новый";
        await File.WriteAllTextAsync(
            guardiansPath,
            guardiansRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        _console.QueueSelection("Сияющая Обитель", "🔎 Осмотреть набор и пакет", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_gates_source_snapshots");
        var renderedText = ExtractRenderedText();
        Assert.Contains("глава фракции «Хор Рассвета» — Северин", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("проект «Тропа возвращения»", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Северин Новый", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Техническая раскладка эффекта для диагностики", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"routeSeedId\"", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_CoreReceiptInspectionHumanizesForgeTargetForm()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var receiptArray = shiningRoot["coreActionReceipts"]?.AsArray()
            ?? throw new InvalidOperationException("Expected seeded core action receipts.");
        receiptArray.Add(new JsonObject
        {
            ["requestId"] = "core_forge_receipt_1",
            ["actionType"] = "forge_relic.reshape",
            ["factionId"] = "faction_dawn",
            ["relicId"] = "relic_routeglass",
            ["relicName"] = "Стекло Пути",
            ["targetFormTag"] = "solar_crown",
            ["resolvedAtTurn"] = 159,
            ["resolvedAtUtc"] = "2026-04-19T10:35:00Z",
            ["status"] = "accepted",
            ["reason"] = "forge_reshape_accepted"
        });
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Сияющая Обитель", "📜 Осмотреть исходы Обители", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_forge_receipt_humanized");
        var renderedText = ExtractRenderedText();
        Assert.Contains("солнечный венец", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("solar_crown", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_CoreReceiptInspectionHumanizesForgeMutationPayload()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var receiptArray = shiningRoot["coreActionReceipts"]?.AsArray()
            ?? throw new InvalidOperationException("Expected seeded core action receipts.");
        receiptArray.Add(new JsonObject
        {
            ["requestId"] = "core_forge_receipt_payload_1",
            ["actionType"] = "forge_relic.retune_property",
            ["factionId"] = "faction_dawn",
            ["factionName"] = "Хор Рассвета",
            ["relicId"] = "relic_routeglass",
            ["relicName"] = "Стекло Пути",
            ["propertyIndex"] = 0,
            ["replacementProperty"] = new JsonObject
            {
                ["propertyId"] = "resonance",
                ["band"] = "rare",
                ["description"] = "Заменяет путь на хор."
            },
            ["addedProperties"] = new JsonArray
            {
                new JsonObject
                {
                    ["propertyId"] = "echo_seed",
                    ["band"] = "epic",
                    ["description"] = "Добавляет устойчивое эхо."
                }
            },
            ["resolvedAtTurn"] = 159,
            ["resolvedAtUtc"] = "2026-04-19T10:35:00Z",
            ["status"] = "accepted",
            ["reason"] = "forge_retune_accepted"
        });
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Сияющая Обитель", "📜 Осмотреть исходы Обители", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_forge_receipt_payload_humanized");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Новое свойство", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resonance", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Диапазон: редкая", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Описание: Заменяет путь на хор.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"propertyId\": \"resonance\"", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_CoreReceiptInspectionDoesNotTruncateOlderResolvedOutcomes()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var receiptArray = shiningRoot["coreActionReceipts"]?.AsArray()
            ?? throw new InvalidOperationException("Expected seeded core action receipts.");

        for (var offset = 0; offset < 8; offset++)
        {
            receiptArray.Add(new JsonObject
            {
                ["requestId"] = $"core_archive_receipt_{offset + 1}",
                ["actionType"] = "support_project",
                ["factionId"] = "faction_dawn",
                ["projectId"] = "project_social",
                ["selectedCardIds"] = new JsonArray(),
                ["newResidentIds"] = new JsonArray(),
                ["seededProjectIds"] = new JsonArray(),
                ["resolvedAtTurn"] = 147 - offset,
                ["resolvedAtUtc"] = $"2026-04-19T09:{40 - offset:00}:00Z",
                ["status"] = "accepted",
                ["reason"] = $"support_project_archived_{offset + 1}"
            });
        }

        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Сияющая Обитель", "📜 Осмотреть исходы Обители", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_core_receipt_full_history");
        var renderedText = ExtractRenderedText();
        Assert.Contains("архивный исход поддержки проекта №8", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("архивный исход поддержки проекта №1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("основание принято", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("набор новой жизни зафиксирован", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("support_project_archived_", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("founding_accepted", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("package_frozen_for_next_life", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_PendingCoreInspectionShowsFullPendingContract()
    {
        await SeedShiningInspectionStateAsync();
        await WriteJsonAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "core_pending_complete_1",
                    actionType = "complete_project",
                    factionId = "faction_dawn",
                    factionName = "Хор Рассвета",
                    projectId = "",
                    projectDisplayName = "",
                    projectDraft = new
                    {
                        displayName = "Свод Рассвета",
                        summary = "Усиливает клятвенный хор.",
                        toneTags = new[] { "radiant", "choral" },
                        targetFactionIds = Array.Empty<string>(),
                        projectArchetype = "accord",
                        outputEffectFamily = "social",
                        tier = 2
                    },
                    radianceTierAtRequest = 2,
                    quotedCostFeathers = 5,
                    quotedCostLightSparks = 12,
                    sourceDraftVersion = 4,
                    selectedCardIds = new[] { "card_route_dawn" },
                    createdAtTurn = 160,
                    createdAtUtc = "2026-04-19T11:05:00Z"
                }
            }
        });
        _console.QueueSelection("Сияющая Обитель", "📝 Осмотреть ожидающие действия Обители", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_pending_core_inspection");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Сияющая Обитель", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("📝 Осмотреть ожидающие действия Обители", StringComparer.Ordinal));
        var renderedText = ExtractRenderedText();
        Assert.Contains("Ожидающие действия Обители", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идентификатор запроса: core_pending_complete_1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Стоимость: 5 Перьев / 12 Искр Света", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Черновик проекта:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Свод Рассвета", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тоновые метки", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("card_route_dawn", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refused|withdrawn", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_PendingForgeInspectionShowsFullMutationPayload()
    {
        await SeedShiningInspectionStateAsync();
        await WriteJsonAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "core_pending_forge_1",
                    actionType = "forge_relic.retune_property",
                    factionId = "faction_dawn",
                    factionName = "Хор Рассвета",
                    relicId = "relic_routeglass",
                    relicName = "Стекло Пути",
                    targetFormTag = "glass_path",
                    propertyIndex = 0,
                    replacementProperty = new
                    {
                        propertyId = "resonance",
                        band = "rare",
                        description = "Заменяет путь на хор."
                    },
                    addedProperties = new object[]
                    {
                        new
                        {
                            propertyId = "echo_seed",
                            band = "epic",
                            description = "Добавляет устойчивое эхо."
                        }
                    },
                    quotedCostFeathers = 7,
                    quotedCostLightSparks = 11,
                    createdAtTurn = 160,
                    createdAtUtc = "2026-04-19T11:07:00Z"
                }
            }
        });
        _console.QueueSelection("Сияющая Обитель", "📝 Осмотреть ожидающие действия Обители", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_pending_forge_payload");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Замещающее свойство", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resonance", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Диапазон: редкая", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Описание: Заменяет путь на хор.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Добавляемые свойства", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("echo_seed", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"propertyId\": \"resonance\"", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_PendingPreparePackageInspectionUsesStoredCardSnapshot()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await WriteJsonAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "core_pending_package_snapshot_1",
                    actionType = "prepare_incarnation_package",
                    sourceDraftVersion = 4,
                    selectedCardIds = new[] { "card_route_dawn" },
                    selectedCards = new object[]
                    {
                        new
                        {
                            cardId = "card_route_dawn",
                            displayName = "Тропа возвращения",
                            sourceType = "project",
                            sourceFactionId = "faction_dawn",
                            sourceFactionName = "Хор Рассвета",
                            sourceActorId = "project_passage",
                            sourceActorName = "Тропа возвращения",
                            effectFamily = "route",
                            rarity = "rare"
                        }
                    },
                    createdAtTurn = 160,
                    createdAtUtc = "2026-04-19T11:05:00Z"
                }
            }
        });
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var gates = shiningRoot["gates"]?.AsObject()
            ?? throw new InvalidOperationException("Expected gates state.");
        gates["availableBlessingCards"] = new JsonArray();
        gates["allCandidateBlessingCards"] = new JsonArray();
        await File.WriteAllTextAsync(shiningStatePath, shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        _console.QueueSelection("Сияющая Обитель", "📝 Осмотреть ожидающие действия Обители", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_pending_package_snapshot_inspection");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Тропа возвращения", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("card_route_dawn)", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSelectedBlessingCardSnapshot_UsesCanonicalGatesCardsWithoutLegacyBlessingDraft()
    {
        static JsonObject Card(string cardId, string displayName) => new()
        {
            ["cardId"] = cardId,
            ["dedupeKey"] = cardId,
            ["sourceType"] = "project",
            ["sourceFactionId"] = "faction_dawn",
            ["sourceActorId"] = "project_passage",
            ["effectFamily"] = "route",
            ["rarity"] = "rare",
            ["displayName"] = displayName,
            ["displaySummary"] = displayName,
            ["effectPayload"] = new JsonObject
            {
                ["routeSeedId"] = cardId
            }
        };

        var shiningRoot = new JsonObject
        {
            ["gates"] = new JsonObject
            {
                ["availableBlessingCards"] = new JsonArray(
                    Card("card_visible_a", "Видимая А"),
                    Card("card_visible_b", "Видимая Б")),
                ["allCandidateBlessingCards"] = new JsonArray(
                    Card("card_visible_a", "Видимая А"),
                    Card("card_visible_b", "Видимая Б"))
            }
        };
        var method = typeof(ExplorerMode).GetMethod(
            "BuildSelectedBlessingCardSnapshot",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var snapshot = Assert.IsType<JsonArray>(method!.Invoke(null, new object[]
        {
            shiningRoot,
            new List<string> { "card_visible_b", "card_visible_a" }
        }));

        Assert.Equal(2, snapshot.Count);
        Assert.Equal("card_visible_b", snapshot[0]?["cardId"]?.GetValue<string>());
        Assert.Equal("card_visible_a", snapshot[1]?["cardId"]?.GetValue<string>());
        Assert.Null(shiningRoot["gates"]?["blessingDraft"]);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_TradeLifecycleInspectionShowsContractAndReceiptProof()
    {
        await SeedShiningInspectionStateAsync();
        _console.QueueSelection("Сияющая Обитель", "🧾 Осмотреть торговые циклы", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_trade_lifecycle_inspection");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Сияющая Обитель", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("🧾 Осмотреть торговые циклы", StringComparer.Ordinal));
        var renderedText = ExtractRenderedText();
        Assert.Contains("Расчётный контракт цикла", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ожидающий запрос", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Создан на ходу: 158", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Подготовленная витрина", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Учёт витрины: слотов 2, распродано 1, доступно 1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("slotId slot_dawn_2", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("relicId relic_routeglass", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Подтверждение исхода", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Подтверждено на ходу: 156", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Полная история подтверждений", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_TradeLifecycleInspectionShowsHistoricalReceipts()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var dawnReceipts = shiningRoot["factions"]?.AsArray()[0]?["tradeInventoryReceipts"]?.AsArray()
            ?? throw new InvalidOperationException("Expected trade receipt history.");
        dawnReceipts.Add(new JsonObject
        {
            ["requestId"] = "shining_trade_dawn_6",
            ["factionId"] = "faction_dawn",
            ["factionName"] = "Хор Рассвета",
            ["tradeCycleId"] = "shining_return_6",
            ["status"] = "ready",
            ["itemCount"] = 3,
            ["resolvedAtTurn"] = 148,
            ["resolvedAtUtc"] = "2026-04-18T09:40:00Z"
        });
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Сияющая Обитель", "🧾 Осмотреть торговые циклы", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_trade_receipt_history");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Полная история подтверждений", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shining_return_7", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shining_return_6", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("идентификатор запроса shining_trade_dawn_6", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_TradeLifecycleInspectionShowsMissingSoldOutCountHonestly()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var dawnReceipt = shiningRoot["factions"]?.AsArray()[0]?["tradeInventoryReceipts"]?.AsArray()?[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected trade receipt.");
        dawnReceipt.Remove("soldOutCount");
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        await _stateManager.RefreshGameStateAsync();

        var loadContextMethod = typeof(ExplorerMode).GetMethod("LoadShiningContextAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(loadContextMethod);
        var loadContextTask = loadContextMethod!.Invoke(_explorer, Array.Empty<object>()) as Task;
        Assert.NotNull(loadContextTask);
        await loadContextTask!;
        var context = loadContextTask!.GetType().GetProperty("Result")?.GetValue(loadContextTask)
            ?? throw new InvalidOperationException("Expected shining context result.");

        var inspectionMethod = typeof(ExplorerMode).GetMethod("ShowShiningTradeLifecycleInspectionAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(inspectionMethod);

        var ex = await Record.ExceptionAsync(async () =>
        {
            var inspectionTask = inspectionMethod!.Invoke(_explorer, new[] { context }) as Task;
            Assert.NotNull(inspectionTask);
            await inspectionTask!;
        });

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_trade_missing_sold_out_count");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Подтверждение исхода", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Распродано: 0", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_TradeLifecycleInspectionDoesNotTruncateGachaHistory()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var gachaHistory = shiningRoot["gachaSystem"]?["gachaHistory"]?.AsArray()
            ?? throw new InvalidOperationException("Expected seeded gacha history array.");

        for (var index = 1; index <= 7; index++)
        {
            gachaHistory.Add(new JsonObject
            {
                ["factionId"] = "faction_dawn",
                ["factionName"] = "Хор Рассвета",
                ["relicId"] = $"relic_history_{index}",
                ["relicName"] = $"Реликвия истории {index}",
                ["baseRarity"] = "rare",
                ["finalRarity"] = "epic",
                ["requestId"] = $"shining_gacha_req_{index}",
                ["returnCycleId"] = $"return_{index}",
                ["costInFeathers"] = 60 + index,
                ["turnNumber"] = 140 + index,
                ["timestamp"] = $"2026-04-19T10:{index:00}:00Z"
            });
        }

        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Сияющая Обитель", "🧾 Осмотреть торговые циклы", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_trade_gacha_full_history");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Полная история сияющих призывов", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Реликвия истории 7", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("relic_history_7", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shining_gacha_req_7", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("return_7", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Стоимость в Перьях: 67", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Реликвия истории 1", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_TradeLifecycleInspectionUsesStrictCurrentContractWhenPendingRequestMatches()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var dawnReceipt = shiningRoot["factions"]?.AsArray()[0]?["tradeInventoryReceipts"]?.AsArray()?[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected dawn trade receipt.");
        dawnReceipt["soldOutCount"] = 1;
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        await WriteJsonAsync(ShiningTradeRequestState.PendingRequestsPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "shining_trade_dawn_7",
                    factionId = "faction_dawn",
                    factionName = "Хор Рассвета",
                    tradeCycleId = "shining_return_7",
                    derivedTradeTier = 2,
                    derivedTradeSlotCount = 2,
                    derivedRarityCeiling = "rare",
                    derivedServiceMultiplier = 1.25,
                    merchantProfile = "shining_faction",
                    createdAtTurn = 156,
                    createdAtUtc = "2026-04-19T10:21:00Z"
                }
            }
        });

        _console.QueueSelection("Сияющая Обитель", "🧾 Осмотреть торговые циклы", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_trade_strict_current_contract");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Подтверждение исхода:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идентификатор запроса: shining_trade_dawn_7", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Строгое подтверждение текущего контракта не найдено", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_GachaHistoryShowsCanonicalRequestAndCostFields()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var gachaHistory = shiningRoot["gachaSystem"]?["gachaHistory"]?.AsArray()
            ?? throw new InvalidOperationException("Expected seeded gacha history array.");
        gachaHistory.Clear();
        gachaHistory.Add(new JsonObject
        {
            ["requestId"] = "gacha_request_1",
            ["factionId"] = "faction_dawn",
            ["factionName"] = "Хор Рассвета",
            ["returnCycleId"] = "return_7",
            ["costInFeathers"] = 30,
            ["baseRarity"] = "rare",
            ["finalRarity"] = "epic",
            ["relicId"] = "relic_history_detail",
            ["relicName"] = "Песнь Янтаря",
            ["turnNumber"] = 148,
            ["timestamp"] = "2026-04-19T10:07:00Z"
        });

        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Сияющая Обитель", "🧾 Осмотреть торговые циклы", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_trade_gacha_full_fields");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Идентификатор запроса: gacha_request_1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Цикл возвращения: return_7", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Стоимость в Перьях: 30", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Подтверждено в UTC: 2026-04-19T10:07:00Z", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningPolitics_ResolutionInspectionShowsDecisionContext()
    {
        await SeedShiningInspectionStateAsync();
        _console.QueueSelection("Политика Сияющей Обители", "📜 Осмотреть решения фракций", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_politics"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_political_resolution_inspection");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Политика Сияющей Обители", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("📜 Осмотреть решения фракций", StringComparer.Ordinal));
        var renderedText = ExtractRenderedText();
        Assert.Contains("Устав фракции: Поют утренний свет.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Описание зала: Поющие своды собирают отзвуки верности.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Стоимость: 25 Перьев / 15 Искр Света", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Орин признал, что зов Хора Рассвета звучит для него яснее прежней верности.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Открыта в UTC: 2026-04-19T10:24:00Z", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Причина решения: целевая фракция приняла переход", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accepted_by_target_faction", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Историческая сводка: Мираэль признана новым голосом Хора Рассвета.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Историческое событие: преемник признан", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningPolitics_OverviewShowsOverflowIndicatorForHiddenEntries()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var foundingReceipts = shiningRoot["factionFoundingReceipts"]?.AsArray()
            ?? throw new InvalidOperationException("Expected founding receipts.");

        for (var index = 0; index < 5; index++)
        {
            foundingReceipts.Add(new JsonObject
            {
                ["requestId"] = $"founding_overflow_{index + 1}",
                ["proposedFactionId"] = $"faction_overflow_{index + 1}",
                ["proposedHallId"] = $"hall_overflow_{index + 1}",
                ["hallName"] = $"Зал переполнения {index + 1}",
                ["factionName"] = $"Фракция переполнения {index + 1}",
                ["factionId"] = $"faction_overflow_{index + 1}",
                ["hallId"] = $"hall_overflow_{index + 1}",
                ["status"] = "accepted",
                ["supportingResidentIds"] = new JsonArray("resident_mirael"),
                ["resolvedAtTurn"] = 140 - index,
                ["resolvedAtUtc"] = $"2026-04-18T10:{index:00}:00Z"
            });
        }

        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Политика Сияющей Обители", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_politics"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_politics_overflow_indicator");
        var renderedText = ExtractRenderedText();
        Assert.Contains("без сокращения", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Фракция переполнения 5", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("…и ещё", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningPolitics_ResolutionInspectionUsesStableSnapshotsAfterRename()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var factions = shiningRoot["factions"]?.AsArray()
            ?? throw new InvalidOperationException("Expected factions.");
        var dawnFaction = factions[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected dawn faction.");
        var memoryFaction = factions[1]?.AsObject()
            ?? throw new InvalidOperationException("Expected memory faction.");

        dawnFaction["charter"]!["factionName"] = "Новое имя Хора";
        memoryFaction["charter"]!["factionName"] = "Новое имя Отзвука";

        var realignmentReceipt = shiningRoot["factionRealignmentReceipts"]?.AsArray()?[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected realignment receipt.");
        realignmentReceipt["sourceFactionName"] = "Хранители Отзвука";
        realignmentReceipt["targetFactionName"] = "Хор Рассвета";

        var leadershipReceipt = dawnFaction["leadershipReceipts"]?.AsArray()?[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected leadership receipt.");
        leadershipReceipt["factionName"] = "Хор Рассвета";
        leadershipReceipt["previousHeadLabel"] = "основанный хранитель Северин";
        leadershipReceipt["newHeadLabel"] = "резидент Мираэль";

        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var residentPath = _fs.ResolvePath(GuardianAbodeResidentState.StatePath);
        var residentRoot = JsonNode.Parse(await File.ReadAllTextAsync(residentPath))?.AsObject()
            ?? throw new InvalidOperationException("Expected resident state.");
        var residents = residentRoot["entries"]?.AsArray()
            ?? throw new InvalidOperationException("Expected resident entries.");
        residents.OfType<JsonObject>().First(item => string.Equals(item["residentId"]?.GetValue<string>(), "resident_orin", StringComparison.OrdinalIgnoreCase))["displayName"] = "Орин Новый";
        residents.OfType<JsonObject>().First(item => string.Equals(item["residentId"]?.GetValue<string>(), "resident_orin", StringComparison.OrdinalIgnoreCase))["residentName"] = "Орин Новый";
        residents.OfType<JsonObject>().First(item => string.Equals(item["residentId"]?.GetValue<string>(), "resident_mirael", StringComparison.OrdinalIgnoreCase))["displayName"] = "Мираэль Новая";
        residents.OfType<JsonObject>().First(item => string.Equals(item["residentId"]?.GetValue<string>(), "resident_mirael", StringComparison.OrdinalIgnoreCase))["residentName"] = "Мираэль Новая";
        await File.WriteAllTextAsync(
            residentPath,
            residentRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var guardiansPath = _fs.ResolvePath("game_state/meta/guardians.json");
        var guardiansRoot = JsonNode.Parse(await File.ReadAllTextAsync(guardiansPath))?.AsObject()
            ?? throw new InvalidOperationException("Expected guardians state.");
        var founderGuardian = guardiansRoot["guardians"]?.AsArray()?.OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(item["guardianId"]?.GetValue<string>(), "guard_test_founder", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Expected founder guardian.");
        founderGuardian["canonicalName"] = "Северин Новый";
        founderGuardian["manifestation"]!["currentDisplayName"] = "Северин Новый";
        await File.WriteAllTextAsync(
            guardiansPath,
            guardiansRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        _console.QueueSelection("Политика Сияющей Обители", "📜 Осмотреть решения фракций", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_politics"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_politics_snapshot_after_rename");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Хранители Отзвука -> Хор Рассвета", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("основанный хранитель Северин", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("резидент Мираэль", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_AfterlifeInbox_ShiningTradeNotificationDetailUsesStableSoldOutSnapshot()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var dawnFaction = shiningRoot["factions"]?.AsArray()?[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected dawn faction.");
        var tradeReceipt = dawnFaction["tradeInventoryReceipts"]?.AsArray()?[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected trade receipt.");
        tradeReceipt["soldOutCount"] = 1;
        foreach (var item in dawnFaction["tradeInventory"]?["items"]?.AsArray()?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
            item["soldOut"] = false;
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        var detailLines = new List<string>();
        var method = typeof(ExplorerMode).GetMethod(
            "AppendShiningTradeNotificationDetails",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        method!.Invoke(null, new object[]
        {
            shiningRoot,
            new AfterlifeNotificationState.NotificationEntry
            {
                NotificationId = "shining_trade_inventory_ready:shining_trade_dawn_7",
                NotificationType = AfterlifeNotificationState.TypeShiningTradeInventoryReady,
                RequestId = "shining_trade_dawn_7",
                Status = AfterlifeNotificationState.StatusUnread,
                Summary = "Сияющая витрина готова."
            },
            detailLines
        });

        var renderedText = string.Join("\n", detailLines);
        Assert.Contains("Распродано: [dim]1/2[/]", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_AfterlifeInbox_GuardianQuestNotificationShowsExactQuestDetail()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_archive",
                    canonicalName = "Азалия",
                    domain = "archive",
                    questManagement = new
                    {
                        activeQuests = new[]
                        {
                            new
                            {
                                questId = "quest_archive_key",
                                title = "Ключ архивной комнаты",
                                description = "Найти ключ в старой памяти.",
                                status = "in_progress",
                                targetWorld = "Мир Пепельных Архивов"
                            }
                        },
                        availableQuests = Array.Empty<object>(),
                        completedQuests = Array.Empty<object>()
                    }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();
        var detailLines = new List<string>();
        var method = typeof(ExplorerMode).GetMethod(
            "AppendExactGuardianNotificationDetailLinesAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var task = method!.Invoke(_explorer, new object[]
        {
            new AfterlifeNotificationState.NotificationEntry
            {
                NotificationId = "guardian_quest_available:guardian_archive:quest_archive_key",
                NotificationType = AfterlifeNotificationState.TypeGuardianQuestAvailable,
                RequestId = "guardian_archive:quest_archive_key",
                GuardianId = "guardian_archive",
                GuardianName = "Азалия",
                Status = AfterlifeNotificationState.StatusUnread,
                Summary = "У Хранителя Азалия появился архивный квест."
            },
            detailLines
        }) as Task;
        Assert.NotNull(task);

        await task!;

        var renderedText = string.Join("\n", detailLines);
        Assert.Contains("Точный квест Хранителя", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ключ архивной комнаты", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Мир Пепельных Архивов", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_AfterlifeInbox_ShiningFoundingNotificationUsesReadableFactionSnapshot()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        shiningRoot["factions"]!.AsArray()[0]!.AsObject()["charter"]!["factionName"] = "Новое текущее имя";
        var foundingReceipt = shiningRoot["factionFoundingReceipts"]?.AsArray()?[0]?.AsObject()
            ?? throw new InvalidOperationException("Expected founding receipt.");
        foundingReceipt["factionName"] = "Исторический Хор Рассвета";
        var detailLines = new List<string>();
        var method = typeof(ExplorerMode).GetMethod(
            "AppendShiningFoundingNotificationDetails",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        method!.Invoke(null, new object[]
        {
            shiningRoot,
            new AfterlifeNotificationState.NotificationEntry
            {
                NotificationId = "shining_faction_founding_resolved:founding_dawn_1",
                NotificationType = AfterlifeNotificationState.TypeShiningFactionFoundingResolved,
                RequestId = "founding_dawn_1",
                Status = AfterlifeNotificationState.StatusUnread,
                Summary = "Основание новой фракции."
            },
            detailLines
        });

        var renderedText = string.Join("\n", detailLines);
        Assert.Contains("Исторический Хор Рассвета", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("faction_dawn", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningPolitics_ResolutionInspectionDoesNotTruncateOlderDecisions()
    {
        await SeedShiningInspectionStateAsync();
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var foundingReceipts = shiningRoot["factionFoundingReceipts"]?.AsArray()
            ?? throw new InvalidOperationException("Expected founding receipts.");
        var realignmentReceipts = shiningRoot["factionRealignmentReceipts"]?.AsArray()
            ?? throw new InvalidOperationException("Expected realignment receipts.");
        var leadershipReceipts = shiningRoot["factions"]?.AsArray()[0]?["leadershipReceipts"]?.AsArray()
            ?? throw new InvalidOperationException("Expected leadership receipts.");

        for (var index = 1; index <= 6; index++)
        {
            foundingReceipts.Add(new JsonObject
            {
                ["requestId"] = $"founding_archive_{index}",
                ["proposedFactionId"] = $"faction_archive_{index}",
                ["proposedHallId"] = $"hall_archive_{index}",
                ["hallName"] = $"Архивный зал {index}",
                ["factionId"] = $"faction_archive_{index}",
                ["hallId"] = $"hall_archive_{index}",
                ["status"] = "accepted",
                ["supportingResidentIds"] = new JsonArray("resident_mirael", "resident_sel"),
                ["resolvedAtTurn"] = 130 + index,
                ["resolvedAtUtc"] = $"2026-04-18T09:{index:00}:00Z",
                ["reason"] = $"founding_archive_reason_{index}"
            });
            realignmentReceipts.Add(new JsonObject
            {
                ["requestId"] = $"realignment_archive_{index}",
                ["residentId"] = "resident_orin",
                ["residentName"] = "Орин",
                ["sourceFactionId"] = "faction_memory",
                ["targetFactionId"] = "faction_dawn",
                ["status"] = "accepted",
                ["realignmentMode"] = "accepted_transfer",
                ["residentHistoryEntryId"] = $"history_archive_{index}",
                ["resolvedAtTurn"] = 130 + index,
                ["resolvedAtUtc"] = $"2026-04-18T10:{index:00}:00Z",
                ["reason"] = $"realignment_archive_reason_{index}"
            });
            leadershipReceipts.Add(new JsonObject
            {
                ["requestId"] = $"leadership_archive_{index}",
                ["transitionMode"] = "peaceful_succession",
                ["previousHeadActorType"] = "guardian",
                ["previousHeadActorId"] = "guard_test_founder",
                ["newHeadActorType"] = "resident",
                ["newHeadActorId"] = "resident_mirael",
                ["status"] = "accepted",
                ["resolvedAtTurn"] = 130 + index,
                ["resolvedAtUtc"] = $"2026-04-18T11:{index:00}:00Z",
                ["reason"] = $"leadership_archive_reason_{index}"
            });
        }

        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Политика Сияющей Обители", "📜 Осмотреть решения фракций", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_politics"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_political_resolution_full_history");
        var renderedText = ExtractRenderedText();
        Assert.Contains("архивный исход основания фракции №6", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("архивный исход основания фракции №1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("архивный исход перехода резидента №6", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("архивный исход перехода резидента №1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("архивный исход смены главы №6", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("архивный исход смены главы №1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("founding_archive_reason_", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("realignment_archive_reason_", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("leadership_archive_reason_", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_InvestmentPreviewCanCancelWithoutWritingPendingRequest()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        _console.QueueSelection("[bold yellow]Сияющая Обитель[/]", "✨ Основные действия", "← Назад");
        _console.QueueSelection("Основные действия Сияющей Обители", "📈 Инвестировать во фракцию", "← Назад");
        _console.QueueSelection("Инвестиция во фракцию", "Хор Рассвета");
        _console.QueueSelection("Подтвердить действие Обители", "← Отмена");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_investment_preview_cancel");
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));

        var renderedText = ExtractRenderedText();
        Assert.Contains("Полный предпросмотр контракта Сияющей Обители", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending_shining_abode_actions.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quotedCostFeathers", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("coreActionReceipts", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Пока этот request не закрыт", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ожидаемый accepted-state delta audit", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("changedSurfaces", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("targetFaction", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningPolitics_RealignmentPreviewCanCancelWithoutWritingPendingRequest()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        _console.QueueSelection("Политика Сияющей Обители", "⇄ Создать запрос на переход между фракциями", "← Назад");
        _console.QueueSelection("Режим перестройки", "Перейти в другую фракцию");
        _console.QueueSelection("Подтвердить перестройку резидента", "← Отмена");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_politics"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_political_realign_preview_cancel");
        Assert.False(_fs.FileExists(ShiningFactionRequestState.PendingRealignmentsRequestPath));

        var renderedText = ExtractRenderedText();
        Assert.Contains("Полный предпросмотр политического контракта", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending_shining_faction_realignments.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("factionRealignmentReceipts", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("realignmentMode", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("accepted_transfer", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("residentHistoryEntryId", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ожидаемый каркас политического receipt/history", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quotedCostLightSparks", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refusedOrWithdrawn", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningPolitics_DeparturePreviewShowsModeConsistentStatuses()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        _console.QueueSelection("Политика Сияющей Обители", "⇄ Создать запрос на переход между фракциями", "← Назад");
        _console.QueueSelection("Режим перестройки", "Уйти в нейтральное состояние");
        _console.QueueSelection("Подтвердить перестройку резидента", "← Отмена");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_politics"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_political_departure_preview_cancel");
        Assert.False(_fs.FileExists(ShiningFactionRequestState.PendingRealignmentsRequestPath));

        var renderedText = ExtractRenderedText();
        Assert.Contains("departure_to_neutral", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("departed_to_neutral", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("residentHistoryEntryId", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refused|withdrawn", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_GatesSelectionPreviewCanCancelWithoutChangingSelection()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        _console.QueueSelection("[bold yellow]Сияющая Обитель[/]", "🚪 Врата и благословения", "← Назад");
        _console.QueueSelection("Врата Сияющей Обители", "🎴 Выбрать или снять благословение", "← Назад");
        _console.QueueSelection("Подтвердить выбор благословения", "← Отмена");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_gates_selection_preview_cancel");
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(_fs.ResolvePath(ShiningAbodeState.StatePath)))!.AsObject();
        var selectedIds = shiningRoot["gates"]!["selectedBlessingCardIds"]!.AsArray()
            .Select(node => node!.GetValue<string>())
            .ToArray();
        Assert.Equal(new[] { "card_route_dawn" }, selectedIds);

        var renderedText = ExtractRenderedText();
        Assert.Contains("Предпросмотр локального изменения Врат", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Projected canonical JSON gates", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("локальное действие клиента", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("effectPayload", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_GatesRerollPreviewComparesAvailableCards()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))!.AsObject();
        var gates = shiningRoot["gates"]!.AsObject();
        var allCandidates = gates["allCandidateBlessingCards"]!.AsArray();
        var availableCards = gates["availableBlessingCards"]!.AsArray();
        var shownIds = gates["shownBlessingCardIds"]!.AsArray();
        var memoryCard = JsonNode.Parse("""
        {
          "cardId": "card_memory_echo",
          "dedupeKey": "memory_echo",
          "sourceType": "project",
          "sourceFactionId": "faction_dawn",
          "sourceActorId": "project_social",
          "effectFamily": "memory",
          "rarity": "Rare",
          "displayName": "Память Эха",
          "displaySummary": "Даёт новый вариант памяти.",
          "effectPayload": { "options": 1 }
        }
        """)!.AsObject();
        var resourceCard = JsonNode.Parse("""
        {
          "cardId": "card_resource_seed",
          "dedupeKey": "resource_seed",
          "sourceType": "head",
          "sourceFactionId": "faction_dawn",
          "sourceActorId": "guard_test_founder",
          "effectFamily": "resource",
          "rarity": "Epic",
          "displayName": "Зерно запаса",
          "displaySummary": "Даёт стартовые ресурсы.",
          "effectPayload": { "common": 2 }
        }
        """)!.AsObject();
        var survivalCard = JsonNode.Parse("""
        {
          "cardId": "card_survival_shield",
          "dedupeKey": "survival_shield",
          "sourceType": "project",
          "sourceFactionId": "faction_dawn",
          "sourceActorId": "project_refinement",
          "effectFamily": "survival",
          "rarity": "Rare",
          "displayName": "Щит выживания",
          "displaySummary": "Смягчает будущий провал.",
          "effectPayload": { "downgrade": 1 }
        }
        """)!.AsObject();
        allCandidates.Add(memoryCard.DeepClone());
        allCandidates.Add(resourceCard.DeepClone());
        allCandidates.Add(survivalCard.DeepClone());
        availableCards.Add(memoryCard.DeepClone());
        shownIds.Add("card_memory_echo");
        gates["nextCandidateCursor"] = 3;
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString());

        _console.QueueSelection("[bold yellow]Сияющая Обитель[/]", "🚪 Врата и благословения", "← Назад");
        _console.QueueSelection("Врата Сияющей Обители", "🔁 Обновить набор благословений", "← Назад");
        _console.QueueSelection("Подтвердить обновление набора Врат", "← Отмена");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_gates_reroll_preview_available_cards");
        var afterRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))!.AsObject();
        var afterAvailableIds = afterRoot["gates"]!["availableBlessingCards"]!.AsArray()
            .OfType<JsonObject>()
            .Select(card => card["cardId"]!.GetValue<string>())
            .ToArray();
        Assert.Contains("card_social_dawn", afterAvailableIds);
        Assert.Contains("card_memory_echo", afterAvailableIds);
        Assert.DoesNotContain("card_resource_seed", afterAvailableIds);

        var renderedText = ExtractRenderedText();
        Assert.Contains("Уходят из доступного набора: Песнь Рассвета (card_social_dawn), Память Эха (card_memory_echo)", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Приходят в доступный набор: Зерно запаса (card_resource_seed), Щит выживания (card_survival_shield)", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Новый доступный набор", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Полные карты, уходящие из selectable-набора", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Полные карты, приходящие в selectable-набор", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Итоговый selectable-набор после подтверждения", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Новый shown set", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_MalformedLegacyDiscoveryBlocksLocalGatesSaveAndPreservesEvidence()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))!.AsObject();
        var gates = shiningRoot["gates"]!.AsObject();
        var allCandidates = gates["allCandidateBlessingCards"]!.AsArray();
        var availableCards = gates["availableBlessingCards"]!.AsArray();
        var shownIds = gates["shownBlessingCardIds"]!.AsArray();
        var memoryCard = JsonNode.Parse("""
        {
          "cardId": "card_memory_echo",
          "dedupeKey": "memory_echo",
          "sourceType": "project",
          "sourceFactionId": "faction_dawn",
          "sourceActorId": "project_social",
          "effectFamily": "memory",
          "rarity": "Rare",
          "displayName": "Память Эха",
          "displaySummary": "Даёт новый вариант памяти.",
          "effectPayload": { "options": 1 }
        }
        """)!.AsObject();
        var resourceCard = JsonNode.Parse("""
        {
          "cardId": "card_resource_seed",
          "dedupeKey": "resource_seed",
          "sourceType": "head",
          "sourceFactionId": "faction_dawn",
          "sourceActorId": "guard_test_founder",
          "effectFamily": "resource",
          "rarity": "Epic",
          "displayName": "Зерно запаса",
          "displaySummary": "Даёт стартовые ресурсы.",
          "effectPayload": { "common": 2 }
        }
        """)!.AsObject();
        var survivalCard = JsonNode.Parse("""
        {
          "cardId": "card_survival_shield",
          "dedupeKey": "survival_shield",
          "sourceType": "project",
          "sourceFactionId": "faction_dawn",
          "sourceActorId": "project_refinement",
          "effectFamily": "survival",
          "rarity": "Rare",
          "displayName": "Щит выживания",
          "displaySummary": "Смягчает будущий провал.",
          "effectPayload": { "downgrade": 1 }
        }
        """)!.AsObject();
        allCandidates.Add(memoryCard.DeepClone());
        allCandidates.Add(resourceCard.DeepClone());
        allCandidates.Add(survivalCard.DeepClone());
        availableCards.Add(memoryCard.DeepClone());
        shownIds.Add("card_memory_echo");
        gates["nextCandidateCursor"] = 3;
        shiningRoot["pendingNativeFactionDiscovery"] = "malformed_contract";
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString());

        _console.QueueSelection("[bold yellow]Сияющая Обитель[/]", "🚪 Врата и благословения", "← Назад");
        _console.QueueSelection("Врата Сияющей Обители", "🔁 Обновить набор благословений", "← Назад");
        _console.QueueSelection("Подтвердить обновление набора Врат", "✅ Применить изменение");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_gates_malformed_legacy_discovery_preserved");
        var afterRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))!.AsObject();
        Assert.Equal("malformed_contract", afterRoot["pendingNativeFactionDiscovery"]?.GetValue<string>());
        Assert.Equal(1, afterRoot["gates"]?["rerollsRemaining"]?.GetValue<int>());
        Assert.Contains(_console.MarkupLines, line => line.Contains("pendingNativeFactionDiscovery", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_console.MarkupLines, line => line.Contains("повреж", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_InventoryRequestPreviewCanCancelWithoutWritingPendingRequest()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await WriteJsonAsync("input/turn_request.json", new { turnNumber = 17 });
        _fs.DeleteFile(ShiningTradeRequestState.PendingRequestsPath);
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))!.AsObject();
        var dawnFaction = shiningRoot["factions"]!.AsArray()
            .OfType<JsonObject>()
            .First(faction => string.Equals(faction["factionId"]?.GetValue<string>(), "faction_dawn", StringComparison.OrdinalIgnoreCase));
        dawnFaction.Remove("tradeInventory");
        dawnFaction["tradeInventoryReceipts"] = new JsonArray();
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString());

        _console.QueueSelection("[bold yellow]Сияющая Обитель[/]", "⚒ Торговля и кузня", "← Назад");
        _console.QueueSelection("Торговля и кузня Сияющей Обители", "🛒 Торговля фракции", "← Назад");
        _console.QueueSelection("Выберите сияющую фракцию для торговли", "Хор Рассвета");
        _console.QueueSelection("Действие", "🧾 Запросить витрину");
        _console.QueueSelection("Подтвердить запрос сияющей витрины", "← Отмена");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_trade_inventory_preview_cancel");
        Assert.False(_fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));

        var renderedText = ExtractRenderedText();
        Assert.Contains("Предпросмотр сияющей торговой витрины", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending_shining_trade_inventory_requests.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("derivedTradeTier", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("derivedTradeSlotCount", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tradeInventoryReceipts", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_InventoryRequestRequiresCurrentTurn()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        _fs.DeleteFile("input/turn_request.json");
        _fs.DeleteFile(ShiningTradeRequestState.PendingRequestsPath);
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))!.AsObject();
        var dawnFaction = shiningRoot["factions"]!.AsArray()
            .OfType<JsonObject>()
            .First(faction => string.Equals(faction["factionId"]?.GetValue<string>(), "faction_dawn", StringComparison.OrdinalIgnoreCase));
        dawnFaction.Remove("tradeInventory");
        dawnFaction["tradeInventoryReceipts"] = new JsonArray();
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        _console.QueueSelection("[bold yellow]Сияющая Обитель[/]", "⚒ Торговля и кузня", "← Назад");
        _console.QueueSelection("Торговля и кузня Сияющей Обители", "🛒 Торговля фракции", "← Назад");
        _console.QueueSelection("Выберите сияющую фракцию для торговли", "Хор Рассвета");
        _console.QueueSelection("Действие", "🧾 Запросить витрину");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_trade_inventory_requires_current_turn");
        Assert.True(_fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
        var pendingRaw = await _fs.ReadFileAsync(ShiningTradeRequestState.PendingRequestsPath);
        Assert.Contains("\"createdAtTurn\":", pendingRaw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(_console.MarkupLines,
            line => line.Contains("pending Shining trade contract", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("requestId=", StringComparison.OrdinalIgnoreCase));

        var renderedText = ExtractRenderedText();
        Assert.Contains("Предпросмотр сияющей торговой витрины", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_PreparePackagePreviewSeparatesBootstrapHandoff()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        _console.QueueSelection("[bold yellow]Сияющая Обитель[/]", "🚪 Врата и благословения", "← Назад");
        _console.QueueSelection("Врата Сияющей Обители", "🌱 Подготовить новую жизнь");
        _console.QueueSelection("Подтвердить действие Обители", "✅ Создать pending request");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_prepare_package_preview");

        var pendingRaw = await _fs.ReadFileAsync(ShiningCoreActionRequestState.PendingActionsRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("\"actionType\": \"prepare_incarnation_package\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"sourceDraftVersion\": 4", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"selectedCards\"", pendingRaw, StringComparison.Ordinal);

        var renderedText = ExtractRenderedText();
        Assert.Contains("preparedIncarnationPackage", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TriggerIncarnation", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Runtime позже сам", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Soul state и ресурсы не меняются", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_ForgeAuthoringUsesPreviewDrivenFlow()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        _console.QueueSelection("[bold yellow]Сияющая Обитель[/]", "⚒ Торговля и кузня", "← Назад");
        _console.QueueSelection("Торговля и кузня Сияющей Обители", "⚒ Создать запрос на перековку", "← Назад");
        _console.QueueSelection("Выберите фракцию для кузни", "Хор Рассвета");
        _console.QueueSelection("Выберите действие кузни", "Перековать форму реликвии");
        _console.QueueSelection("Выберите Реликвию Души для перековки", "Стекло Пути");
        _console.QueueSelection("Новая форма реликвии", "✅ Использовать предложенную форму");
        _console.QueueSelection("Подтвердить запрос на перековку", "✅ Создать запрос");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_forge_preview");
        Assert.Contains(_console.SelectionTitles,
            title => title.Contains("Выберите действие кузни", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_console.SelectionTitles,
            title => title.Contains("Новая форма реликвии", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_console.SelectionTitles,
            title => title.Contains("Подтвердить запрос на перековку", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Выберите действие кузни", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Any(choice => choice.Contains("Перековать форму реликвии", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Новая форма реликвии", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Any(choice => choice.Contains("Использовать предложенную форму: солнечный венец", StringComparison.OrdinalIgnoreCase) &&
                                                !choice.Contains("solar_crown", StringComparison.OrdinalIgnoreCase)));

        var renderedText = ExtractRenderedText();
        Assert.Contains("Форма реликвии: стекло пути → солнечный венец", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("glass_path", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("solar_crown", renderedText, StringComparison.OrdinalIgnoreCase);

        var pendingRaw = await _fs.ReadFileAsync(ShiningCoreActionRequestState.PendingActionsRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("\"actionType\": \"forge_relic.reshape\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"targetFormTag\": \"solar_crown\"", pendingRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_RerollCancelPreservesBlessingEntitlement()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        var soulPath = _fs.ResolvePath("game_state/meta/soul_state.json");
        var soulRoot = JsonNode.Parse(await File.ReadAllTextAsync(soulPath))!.AsObject();
        soulRoot[ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
        {
            ["applicationState"] = "active",
            ["materializedAtUtc"] = "2026-04-19T10:00:00Z",
            ["currentIncarnation"] = 7,
            ["sourcePackagePreparedAtTurn"] = 155,
            ["sourceCardIds"] = new JsonArray("card_relic_reroll"),
            ["sourceCardCount"] = 1,
            ["relicRefinementEntitlements"] = new JsonObject
            {
                ["rerolls"] = 1,
                ["freeShape"] = false,
                ["freeRetune"] = false,
                ["status"] = ShiningBlessingEffectState.RelicStatusPendingEntitlement,
                ["sourceCardIds"] = new JsonArray("card_relic_reroll")
            }
        };
        await File.WriteAllTextAsync(soulPath, soulRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        _console.QueueSelection("[bold yellow]Сияющая Обитель[/]", "⚒ Торговля и кузня", "← Назад");
        _console.QueueSelection("Торговля и кузня Сияющей Обители", "⚒ Создать запрос на перековку", "← Назад");
        _console.QueueSelection("Выберите фракцию для кузни", "Хор Рассвета");
        _console.QueueSelection("Выберите действие кузни", "Перековать форму реликвии");
        _console.QueueSelection("Выберите Реликвию Души для перековки", "Стекло Пути");
        _console.QueueSelection("Новая форма реликвии", "🔄 Перебросить благословением (1)", "✅ Использовать предложенную форму");
        _console.QueueSelection("Подтвердить запрос на перековку", "← Отмена");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_forge_reroll_cancel_preserves_entitlement");
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        var afterRoot = JsonNode.Parse(await File.ReadAllTextAsync(soulPath))!.AsObject();
        var entitlements = afterRoot[ShiningBlessingEffectState.SoulStateProperty]!["relicRefinementEntitlements"]!.AsObject();
        Assert.Equal(1, entitlements["rerolls"]!.GetValue<int>());
        Assert.Equal(ShiningBlessingEffectState.RelicStatusPendingEntitlement, entitlements["status"]!.GetValue<string>());

        var renderedText = ExtractRenderedText();
        Assert.Contains("Перебросы реликвий от благословений", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("отмена сохраняет право", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_ReshapeFallbackHumanizesPromptAndNormalizesCanonicalFormTag()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 7,
            inkFeathers = new { current = 90, total = 126 },
            soulRelics = new
            {
                stored = new object[]
                {
                    new
                    {
                        relicId = "relic_routeglass",
                        name = "Стекло Пути",
                        quality = "rare",
                        formTag = "glass_path",
                        properties = new object[]
                        {
                            new { propertyId = "route_seed", band = "rare" }
                        }
                    }
                },
                equipped = Array.Empty<object>()
            }
        });
        _console.QueueSelection("[bold yellow]Сияющая Обитель[/]", "⚒ Торговля и кузня", "← Назад");
        _console.QueueSelection("Торговля и кузня Сияющей Обители", "⚒ Создать запрос на перековку", "← Назад");
        _console.QueueSelection("Выберите фракцию для кузни", "Хор Рассвета");
        _console.QueueSelection("Выберите действие кузни", "Перековать форму реликвии");
        _console.QueueSelection("Выберите Реликвию Души для перековки", "Стекло Пути");
        _console.QueueAskResponse("Новая форма реликвии", "стекло пути");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_forge_reshape_fallback");
        Assert.Contains(_console.AskPrompts,
            prompt => prompt.Contains("Новая форма реликвии", StringComparison.OrdinalIgnoreCase));

        var renderedText = ExtractRenderedText();
        Assert.DoesNotContain("glass_path", renderedText, StringComparison.OrdinalIgnoreCase);

        var pendingRaw = await _fs.ReadFileAsync(ShiningCoreActionRequestState.PendingActionsRequestPath);
        Assert.True(string.IsNullOrWhiteSpace(pendingRaw));
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_RetuneFallbackOffersTemplateBeforeManualJson()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 7,
            inkFeathers = new { current = 90, total = 126 },
            soulRelics = new
            {
                stored = new object[]
                {
                    new
                    {
                        relicId = "relic_routeglass",
                        name = "Стекло Пути",
                        quality = "rare",
                        formTag = "glass_path",
                        properties = new object[]
                        {
                            new { propertyId = "route_seed", band = "rare" }
                        }
                    }
                },
                equipped = Array.Empty<object>()
            }
        });
        _console.QueueSelection("[bold yellow]Сияющая Обитель[/]", "⚒ Торговля и кузня", "← Назад");
        _console.QueueSelection("Торговля и кузня Сияющей Обители", "⚒ Создать запрос на перековку", "← Назад");
        _console.QueueSelection("Выберите фракцию для кузни", "Хор Рассвета");
        _console.QueueSelection("Выберите действие кузни", "Перенастроить свойство реликвии");
        _console.QueueSelection("Выберите Реликвию Души для перековки", "Стекло Пути");
        _console.QueueSelection("Выберите свойство для перенастройки", "Свойство 1");
        _console.QueueSelection("Новая версия свойства", "✅ Использовать базовый шаблон");
        _console.QueueSelection("Подтвердить запрос на перековку", "✅ Создать запрос");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_forge_retune_fallback");
        Assert.DoesNotContain(_console.SelectionTitles,
            title => title.Contains("Способ заполнения поля", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_console.SelectionTitles,
            title => title.Contains("Подтвердить запрос на перековку", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Новая версия свойства", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Any(choice => choice.Contains("Использовать базовый шаблон", StringComparison.OrdinalIgnoreCase)) &&
                     entry.Choices.Any(choice => choice.Contains("Настроить свойство вручную", StringComparison.OrdinalIgnoreCase)));

        var renderedText = ExtractRenderedText();
        Assert.Contains("Выбранное свойство: Свойство 1:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Новая версия свойства:", renderedText, StringComparison.OrdinalIgnoreCase);

        var pendingRaw = await _fs.ReadFileAsync(ShiningCoreActionRequestState.PendingActionsRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("\"actionType\": \"forge_relic.retune_property\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"propertyId\": \"new_property\"", pendingRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_UpliftFallbackOffersPreparedSetBeforeManualJson()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 7,
            inkFeathers = new { current = 21, total = 57 },
            soulRelics = new
            {
                stored = new object[]
                {
                    new
                    {
                        relicId = "relic_routeglass",
                        name = "Стекло Пути",
                        quality = "rare",
                        formTag = "glass_path",
                        properties = new object[]
                        {
                            new { propertyId = "route_seed", band = "rare" }
                        }
                    }
                },
                equipped = Array.Empty<object>()
            }
        });
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var radianceRoot = shiningRoot["radiance"]?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded radiance state.");
        radianceRoot["tier"] = 4;
        await File.WriteAllTextAsync(shiningStatePath, shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("[bold yellow]Сияющая Обитель[/]", "⚒ Торговля и кузня", "← Назад");
        _console.QueueSelection("Торговля и кузня Сияющей Обители", "⚒ Создать запрос на перековку", "← Назад");
        _console.QueueSelection("Выберите фракцию для кузни", "Хор Рассвета");
        _console.QueueSelection("Выберите действие кузни", "Возвысить редкость реликвии");
        _console.QueueSelection("Выберите Реликвию Души для перековки", "Стекло Пути");
        _console.QueueSelection("Дополнительные свойства для новой редкости", "✅ Использовать подготовленный набор");
        _console.QueueSelection("Подтвердить запрос на перековку", "✅ Создать запрос");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_forge_uplift_fallback");
        Assert.DoesNotContain(_console.SelectionTitles,
            title => title.Contains("Способ заполнения поля", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Дополнительные свойства для новой редкости", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Any(choice => choice.Contains("Использовать подготовленный набор", StringComparison.OrdinalIgnoreCase)) &&
                     entry.Choices.Any(choice => choice.Contains("Настроить набор вручную", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_NoSoulRelicsUsesPlayerFacingRussianWording()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 7,
            inkFeathers = new { current = 90, total = 126 },
            soulRelics = new
            {
                stored = Array.Empty<object>(),
                equipped = Array.Empty<object>()
            }
        });
        _console.QueueSelection("[bold yellow]Сияющая Обитель[/]", "⚒ Торговля и кузня", "← Назад");
        _console.QueueSelection("Торговля и кузня Сияющей Обители", "⚒ Создать запрос на перековку", "← Назад");
        _console.QueueSelection("Выберите фракцию для кузни", "Хор Рассвета");
        _console.QueueSelection("Выберите действие кузни", "Перековать форму реликвии");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_forge_no_soul_relics_wording");
        Assert.Contains(_console.MarkupLines,
            line => line.Contains("У души сейчас нет доступных реликвий души.", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(_console.MarkupLines,
            line => line.Contains("Soul Relics", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTradeAndForge_MissingRelicPropertiesUsesPlayerFacingRussianWording()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 7,
            inkFeathers = new { current = 90, total = 126 },
            soulRelics = new
            {
                stored = new object[]
                {
                    new
                    {
                        relicId = "relic_routeglass",
                        name = "Стекло Пути",
                        quality = "rare",
                        formTag = "glass_path",
                        properties = Array.Empty<object>()
                    }
                },
                equipped = Array.Empty<object>()
            }
        });
        _console.QueueSelection("[bold yellow]Сияющая Обитель[/]", "⚒ Торговля и кузня", "← Назад");
        _console.QueueSelection("Торговля и кузня Сияющей Обители", "⚒ Создать запрос на перековку", "← Назад");
        _console.QueueSelection("Выберите фракцию для кузни", "Хор Рассвета");
        _console.QueueSelection("Выберите действие кузни", "Перенастроить свойство реликвии");
        _console.QueueSelection("Выберите Реликвию Души для перековки", "Стекло Пути");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_forge_missing_properties_wording");
        Assert.Contains(_console.MarkupLines,
            line => line.Contains("У выбранной реликвии нет списка свойств для перековки.", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(_console.MarkupLines,
            line => line.Contains("canonical properties array", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_SoulRelics_CompanionDetailShowsFullCanonicalPayload()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new object[]
            {
                new
                {
                    guardianId = "guardian_azure_001",
                    canonicalName = "Азалия Лазурная"
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_azure_001",
                    displayName = "Лазурный Друг"
                }
            }
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 10, total = 10 },
            soulRelics = new
            {
                stored = new[]
                {
                    new
                    {
                        relicId = "relic_companion_001",
                        name = "Отзвук Лазурного Друга",
                        description = "Реликвия со слепком спутника.",
                        rarity = "Rare",
                        companionManifestationStatus = "pending",
                        companionSeed = new
                        {
                            sourceResidentId = "resident_azure_001",
                            sourceGuardianId = "guardian_azure_001",
                            companionNameHint = "Лазурный Друг",
                            originWorldSummary = "Мир из стеклянных садов и дождя.",
                            futureCompanionPrompt = "Спутник должен вернуться как тихий проводник через руины.",
                            bondReason = "Он однажды удержал душу на краю распада.",
                            coreTraits = new[] { "верный", "вдумчивый" },
                            archetypeHints = new[] { "проводник", "свидетель" },
                            appearanceMotifs = new[] { "лазурное свечение", "осколки стекла" },
                            personalityProfile = new
                            {
                                archetype = "Witness",
                                worldview = "Hopeful",
                                culturalLayer = "Glass Gardens",
                                coreValues = new[] { "верность", "память", "милосердие" },
                                personalityTraits = new[] { "тихий", "наблюдательный", "терпеливый" }
                            }
                        }
                    }
                },
                equipped = Array.Empty<object>()
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/реликвии"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("soul_relics_companion_payload");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Мир происхождения: Мир из стеклянных садов и дождя.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Образ будущего спутника: Спутник должен вернуться как тихий проводник через руины.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Причина связи: Он однажды удержал душу на краю распада.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Резидент-источник: Лазурный Друг (resident_azure_001)", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Хранитель-источник: Азалия Лазурная (guardian_azure_001)", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ключевые черты: верный, вдумчивый", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Архетипические намёки: проводник, свидетель", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Образы и мотивы: лазурное свечение, осколки стекла", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("культурный слой: Glass Gardens", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ценности: верность, память, милосердие", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("черты: тихий, наблюдательный, терпеливый", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("quality", "Rare")]
    [InlineData("relicRarity", "Epic")]
    public async Task TryProcessCommand_AbodeOfferingSoulRelic_UsesRarityAliases(string rarityField, string rarity)
    {
        await SeedAbodeOfferingRelicAliasStateAsync(rarityField, rarity);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/подношение_обители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors($"abode_offering_{rarityField}_alias");
        var pendingJson = await _fs.ReadFileAsync(GuardianAbodeOfferingState.PendingRequestPath);
        Assert.False(string.IsNullOrWhiteSpace(pendingJson));
        using var pendingDoc = JsonDocument.Parse(pendingJson!);
        Assert.Equal(GuardianAbodeOfferingState.OfferingTypeSoulRelic, pendingDoc.RootElement.GetProperty("offeringType").GetString());
        Assert.Equal("relic_alias_001", pendingDoc.RootElement.GetProperty("relicId").GetString());
        Assert.Equal(rarity, pendingDoc.RootElement.GetProperty("relicRarity").GetString());
        Assert.Contains(_console.SelectionChoicesHistory.SelectMany(entry => entry.Choices),
            choice => choice.Contains($"via {rarityField}", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_AbodeOfferingSoulRelic_InvalidAliasRarityDoesNotCreatePendingRequest()
    {
        await SeedAbodeOfferingRelicAliasStateAsync("quality", "Mythic");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/подношение_обители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("abode_offering_invalid_relic_alias");
        Assert.False(_fs.FileExists(GuardianAbodeOfferingState.PendingRequestPath));
        Assert.Contains(_console.MarkupLines,
            line => line.Contains("Нельзя поднести реликвию", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("unsupported quality='Mythic'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_SoulRelics_CompanionDetailShowsObjectFormPersonalityTraits()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 10, total = 10 },
            soulRelics = new
            {
                stored = new[]
                {
                    new
                    {
                        relicId = "relic_companion_object_traits",
                        name = "Отзвук Мираэли",
                        description = "Реликвия с подробным слепком личности.",
                        rarity = "Rare",
                        companionManifestationStatus = "pending",
                        companionSeed = new
                        {
                            sourceResidentId = "resident_mirael",
                            sourceGuardianId = "guard_test_founder",
                            companionNameHint = "Мираэль",
                            originWorldSummary = "Мир у поющих стен.",
                            futureCompanionPrompt = "Возвращается как певчая тень.",
                            bondReason = "Слышит клятвы даже после смерти.",
                            coreTraits = new[] { "верность" },
                            archetypeHints = new[] { "голос" },
                            appearanceMotifs = new[] { "золотая нить" },
                            personalityProfile = new
                            {
                                archetype = "Voice",
                                worldview = "Harmony survives strain.",
                                culturalLayer = "Choir halls",
                                coreValues = new[] { "верность" },
                                personalityTraits = new object[]
                                {
                                    new
                                    {
                                        traitName = "quiet_resolve",
                                        value = 8,
                                        valueDescription = "тихая решимость"
                                    }
                                }
                            }
                        }
                    }
                },
                equipped = Array.Empty<object>()
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/реликвии"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("soul_relics_companion_object_traits");
        var renderedText = ExtractRenderedText();
        Assert.Contains("черты: quiet_resolve", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Черты личности:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quiet_resolve 8/10", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("тихая решимость", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SoulRelics_DetailUsesLocalizedSlotLabelsAndTechnicalEffectBlock()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 10, total = 10 },
            soulRelics = new
            {
                stored = new[]
                {
                    new
                    {
                        relicId = "relic_slot_001",
                        name = "Клинок Эха",
                        description = "Реликвия с эффектами проверки.",
                        rarity = "Rare",
                        slot = "mainHand",
                        formTag = "blade",
                        properties = new object[]
                        {
                            new
                            {
                                propertyId = "echoSignature",
                                name = "Отзвук клинка",
                                stat = "memory",
                                band = "rare"
                            },
                            new
                            {
                                propertyId = "social_focus",
                                stat = "social",
                                band = "common"
                            }
                        },
                        effects = new
                        {
                            characteristicBonuses = new
                            {
                                strength = 2
                            },
                            actionCheckBonuses = new
                            {
                                social = 3
                            },
                            echoSignature = "luminous_trace"
                        }
                    }
                },
                equipped = Array.Empty<object>()
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/реликвии"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("soul_relics_localized_slot_and_effects");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Основная рука", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Форма ковки", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("клинок", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Свойства ковки", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Отзвук клинка", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("социальное влияние", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Бонус к социальной проверке", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Дополнительные свойства эффекта", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сигнатура эха", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("echoSignature", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("actionCheckBonuses", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_LivesHistory_ShowsCanonicalRecordLifeCompletionPayload()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 3,
            livesHistory = new object[]
            {
                new
                {
                    incarnation = 2,
                    turnsLived = 27,
                    recordLifeCompletion = new
                    {
                        characterFinalState = new
                        {
                            characterName = "Энна",
                            worldName = "Стеклянные Пределы",
                            finalLevel = 19,
                            causeOfDeath = "Падение моста над бурей",
                            finalAlignment = "Сострадательная"
                        },
                        majorAchievements = new[] { "Вернула клятву дому дорог", "Спасла переправу" },
                        relationshipsFormed = new object[]
                        {
                            new { name = "Кай", relationshipType = "союзник", summary = "стал спутником в бурю" }
                        },
                        moralChoices = new object[]
                        {
                            new { choice = "Пощадила дозорного", consequence = "дорога открылась миру" }
                        },
                        skillsLearned = new[] { "ритуал мостов", "песнь ветра" },
                        enlightenmentGained = 14
                    }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/жизни"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("lives_history_record_completion");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Энна", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Стеклянные Пределы", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Главные свершения", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Вернула клятву дому дорог", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Канонические связи", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Кай — союзник — стал спутником в бурю", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Канонические выборы", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Пощадила дозорного — дорога открылась миру", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Освоенные навыки", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Просветление за жизнь: 14", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SeedShiningInspectionStateAsync(bool includePreparedPackage = true)
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 7,
            inkFeathers = new { current = 21, total = 57 },
            soulRelics = new
            {
                stored = new object[]
                {
                    new
                    {
                        relicId = "relic_routeglass",
                        name = "Стекло Пути",
                        quality = "rare",
                        formTag = "glass_path",
                        properties = new object[]
                        {
                            new { propertyId = "route_seed", band = "rare" },
                            new { propertyId = "memory_glow", band = "uncommon" },
                            new { propertyId = "oath_trace", band = "rare" }
                        }
                    },
                    new
                    {
                        relicId = "relic_sunband",
                        name = "Солнечный Венец",
                        quality = "rare",
                        formTag = "solar_crown",
                        properties = new object[]
                        {
                            new { propertyId = "choral_resonance", band = "rare" },
                            new { propertyId = "dawn_pulse", band = "uncommon" },
                            new { propertyId = "radiant_thread", band = "rare" }
                        }
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
                    guardianId = "guard_test_founder",
                    canonicalName = "Северин",
                    originType = PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul,
                    manifestation = new
                    {
                        currentDisplayName = "Северин"
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guard_test_founder",
                canonicalName = "Северин",
                originType = PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul,
                manifestation = new
                {
                    currentDisplayName = "Северин"
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_mirael",
                    residentName = "Мираэль",
                    displayName = "Мираэль",
                    ascensionState = "ascended",
                    shiningFactionId = "faction_dawn",
                    residentRole = "social_support",
                    factionLoyaltyLevel = 76,
                    factionLoyaltyTier = "devoted",
                    factionRestlessness = 18,
                    factionRealignmentState = "settled",
                    abodeDevotionLevel = 78,
                    restlessness = 18
                },
                new
                {
                    residentId = "resident_sel",
                    residentName = "Сэль",
                    displayName = "Сэль",
                    ascensionState = "ascended",
                    shiningFactionId = "faction_dawn",
                    residentRole = "descent_support",
                    factionLoyaltyLevel = 41,
                    factionLoyaltyTier = "uncertain",
                    factionRestlessness = 61,
                    factionRealignmentState = "considering_realignment",
                    abodeDevotionLevel = 44,
                    restlessness = 61,
                    grantedRelicId = "relic_echo"
                },
                new
                {
                    residentId = "resident_orin",
                    residentName = "Орин",
                    displayName = "Орин",
                    ascensionState = "ascended",
                    shiningFactionId = "faction_memory",
                    residentRole = "archive_support",
                    factionLoyaltyLevel = 14,
                    factionLoyaltyTier = "alienated",
                    factionRestlessness = 76,
                    factionRealignmentState = "ready_to_realign",
                    abodeDevotionLevel = 52,
                    restlessness = 28,
                    historyLog = new object[]
                    {
                        new
                        {
                            entryId = "history_orin_realignment_1",
                            summary = "Орин признал, что зов Хора Рассвета звучит для него яснее прежней верности.",
                            timestamp = "2026-04-19T10:24:00Z"
                        }
                    }
                }
            }
        });
        await WriteJsonAsync(ShiningAbodeState.StatePath, new
        {
            availability = "active",
            radiance = new
            {
                experience = 540,
                tier = 3
            },
            lightSparks = 94,
            gachaSystem = new
            {
                chargesPerReturn = 3,
                chargesUsedThisReturn = 1,
                currentReturnCycleId = "return_7",
                gachaHistory = Array.Empty<object>()
            },
            halls = new[]
            {
                new
                {
                    hallId = "hall_dawn",
                    hallName = "Зал Рассветного Хора",
                    description = "Поющие своды собирают отзвуки верности.",
                    serviceTags = new[] { "social", "memory" }
                },
                new
                {
                    hallId = "hall_memory",
                    hallName = "Зал Хранящих Отзвуки",
                    description = "Здесь удерживают следы забытых клятв.",
                    serviceTags = new[] { "lore", "memory" }
                }
            },
            factions = new object[]
            {
                new
                {
                    factionId = "faction_dawn",
                    originType = "player_founded",
                    hallId = "hall_dawn",
                    charter = new
                    {
                        factionName = "Хор Рассвета",
                        favoredArchetype = "accord",
                        patronEffectFamily = "social",
                        summary = "Поют утренний свет."
                    },
                    leadership = new
                    {
                        headActorType = "guardian",
                        headActorId = "guard_test_founder",
                        leadershipState = "secure"
                    },
                    baseStrength = 35,
                    factionStrength = 72,
                    investCountThisAscension = 0,
                    projects = new object[]
                    {
                        new
                        {
                            projectId = "project_social",
                            displayName = "Песнь согласия",
                            summary = "Укрепляет связи.",
                            toneTags = new[] { "radiant" },
                            targetFactionIds = Array.Empty<string>(),
                            projectArchetype = "accord",
                            outputEffectFamily = "social",
                            tier = 2,
                            status = "completed",
                            isSupported = false,
                            strengthReward = 12
                        },
                        new
                        {
                            projectId = "project_passage",
                            displayName = "Тропа возвращения",
                            summary = "Зовёт спутников.",
                            toneTags = new[] { "passage" },
                            targetFactionIds = Array.Empty<string>(),
                            projectArchetype = "passage",
                            outputEffectFamily = "route",
                            tier = 1,
                            status = "completed",
                            isSupported = true,
                            strengthReward = 8
                        },
                        new
                        {
                            projectId = "project_refinement",
                            displayName = "Чистый резонанс",
                            summary = "Раскрывает кузню фракции.",
                            toneTags = new[] { "refinement" },
                            targetFactionIds = Array.Empty<string>(),
                            projectArchetype = "refinement",
                            outputEffectFamily = "relic",
                            tier = 2,
                            status = "completed",
                            isSupported = true,
                            strengthReward = 9
                        }
                    },
                    tradeInventory = new
                    {
                        tradeCycleId = "shining_return_7",
                        generatedAtUtc = "2026-04-19T10:20:00Z",
                        generationTradeTier = 3,
                        generationRarityCeiling = "epic",
                        serviceMultiplierSnapshot = 1.60,
                        merchantProfile = "shining_faction",
                        items = new object[]
                        {
                            new
                            {
                                slotId = "slot_dawn_1",
                                priceInFeathers = 70,
                                soldOut = true,
                                relicData = new
                                {
                                    relicId = "relic_sunband",
                                    name = "Солнечный Венец",
                                    quality = "Rare",
                                    description = "Оставляет после себя тонкую нить согласия."
                                }
                            },
                            new
                            {
                                slotId = "slot_dawn_2",
                                priceInFeathers = 110,
                                soldOut = false,
                                relicData = new
                                {
                                    relicId = "relic_routeglass",
                                    name = "Стекло Пути",
                                    quality = "Epic",
                                    description = "Сохраняет дорогу через забытую клятву."
                                }
                            }
                        }
                    },
                    tradeInventoryReceipts = new object[]
                    {
                        new
                        {
                            requestId = "shining_trade_dawn_7",
                            factionId = "faction_dawn",
                            factionName = "Хор Рассвета",
                            tradeCycleId = "shining_return_7",
                            status = "ready",
                            itemCount = 2,
                            resolvedAtTurn = 156,
                            resolvedAtUtc = "2026-04-19T10:21:00Z"
                        }
                    },
                    leadershipReceipts = new object[]
                    {
                        new
                        {
                            requestId = "leadership_dawn_1",
                            factionName = "Хор Рассвета",
                            transitionMode = "peaceful_succession",
                            previousHeadActorType = "guardian",
                            previousHeadActorId = "guard_test_founder",
                            previousHeadLabel = "основанный хранитель guard_test_founder",
                            newHeadActorType = "resident",
                            newHeadActorId = "resident_mirael",
                            newHeadLabel = "резидент resident_mirael",
                            status = "accepted",
                            resolvedAtTurn = 154,
                            resolvedAtUtc = "2026-04-19T09:58:00Z",
                            reason = "recognized_succession"
                        }
                    },
                    leadershipHistory = new object[]
                    {
                        new
                        {
                            eventId = "leadership_evt_dawn_154",
                            requestId = "leadership_dawn_1",
                            eventType = "succeeded",
                            summary = "Мираэль признана новым голосом Хора Рассвета.",
                            turnNumber = 154,
                            occurredAtUtc = "2026-04-19T09:58:00Z"
                        }
                    }
                },
                new
                {
                    factionId = "faction_memory",
                    originType = "ascended_guardian",
                    hallId = "hall_memory",
                    charter = new
                    {
                        factionName = "Хранители Отзвука",
                        favoredArchetype = "remembrance",
                        patronEffectFamily = "lore",
                        summary = "Собирают клятвы, которые мир забыл."
                    },
                    leadership = new
                    {
                        headActorType = "radiant_actor",
                        headActorId = "actor_liora",
                        leadershipState = "contested"
                    },
                    baseStrength = 30,
                    factionStrength = 58,
                    investCountThisAscension = 0,
                    projects = Array.Empty<object>(),
                    tradeInventoryReceipts = Array.Empty<object>(),
                    leadershipReceipts = Array.Empty<object>(),
                    leadershipHistory = Array.Empty<object>()
                }
            },
            coreActionReceipts = new object[]
            {
                new
                {
                    requestId = "core_discovery_dawn_1",
                    actionType = "discover_native_faction",
                    hallId = "hall_dawn",
                    hallName = "Зал Рассветного Хора",
                    resolvedFactionId = "faction_dawn",
                    factionName = "Хор Рассвета",
                    charterSummary = "Поют утренний свет.",
                    favoredArchetype = "accord",
                    patronEffectFamily = "social",
                    selectedCardIds = Array.Empty<string>(),
                    newResidentIds = new[] { "resident_mirael", "resident_sel" },
                    newResidentNames = new[] { "Мираэль", "Сэль" },
                    seededProjectIds = new[] { "project_social", "project_passage" },
                    seededProjectNames = new[] { "Песнь согласия", "Тропа возвращения" },
                    generatedDraftVersion = 0,
                    resolvedAtTurn = 153,
                    resolvedAtUtc = "2026-04-19T09:55:00Z",
                    status = "accepted",
                    reason = "founding_accepted"
                },
                new
                {
                    requestId = "core_package_dawn_1",
                    actionType = "prepare_incarnation_package",
                    selectedCardIds = new[] { "card_route_dawn" },
                    newResidentIds = Array.Empty<string>(),
                    seededProjectIds = Array.Empty<string>(),
                    generatedDraftVersion = 4,
                    resolvedAtTurn = 155,
                    resolvedAtUtc = "2026-04-19T10:00:00Z",
                    status = "accepted",
                    reason = "package_frozen_for_next_life"
                }
            },
            factionFoundingReceipts = new object[]
            {
                new
                {
                    requestId = "founding_dawn_1",
                    proposedFactionId = "faction_dawn",
                    proposedHallId = "hall_dawn",
                    hallName = "Зал Рассветного Хора",
                    hallDescription = "Поющие своды собирают отзвуки верности.",
                    hallServiceTags = new[] { "social", "memory" },
                    factionId = "faction_dawn",
                    hallId = "hall_dawn",
                    factionName = "Хор Рассвета",
                    charterSummary = "Поют утренний свет.",
                    favoredArchetype = "accord",
                    patronEffectFamily = "social",
                    status = "accepted",
                    supportingResidentIds = new[] { "resident_mirael", "resident_sel" },
                    quotedCostFeathers = ShiningFactionRequestState.FactionFoundingCostFeathers,
                    quotedCostLightSparks = ShiningFactionRequestState.FactionFoundingCostLightSparks,
                    resolvedAtTurn = 153,
                    resolvedAtUtc = "2026-04-19T09:55:00Z",
                    reason = "founding_accepted"
                }
            },
            factionRealignmentReceipts = new object[]
            {
                new
                {
                    requestId = "realignment_orin_1",
                    residentId = "resident_orin",
                    residentName = "Орин",
                    sourceFactionId = "faction_memory",
                    sourceFactionName = "Хранители Отзвука",
                    targetFactionId = "faction_dawn",
                    targetFactionName = "Хор Рассвета",
                    status = "accepted",
                    realignmentMode = "accepted_transfer",
                    residentHistoryEntryId = "history_orin_realignment_1",
                    resolvedAtTurn = 157,
                    resolvedAtUtc = "2026-04-19T10:24:00Z",
                    reason = "accepted_by_target_faction"
                }
            },
            shiningPoliticalActors = new[]
            {
                new
                {
                    actorId = "actor_liora",
                    actorType = "radiant_actor",
                    displayName = "Лиора Светоносная",
                    summary = "Хранит спор о преемнике.",
                    originFactionId = "faction_memory",
                    currentFactionId = "faction_memory",
                    politicalStatus = "elder"
                }
            },
            gates = new
            {
                draftVersion = 4,
                hasOpenDraft = true,
                isStale = false,
                allCandidateBlessingCards = new object[]
                {
                    new
                    {
                        cardId = "card_social_dawn",
                        dedupeKey = "social_dawn",
                        sourceType = "head",
                        sourceFactionId = "faction_dawn",
                        sourceActorId = "guard_test_founder",
                        effectFamily = "social",
                        rarity = "Rare",
                        displayName = "Песнь Рассвета",
                        displaySummary = "Укрепляет союз.",
                        effectPayload = new
                        {
                            relationshipBoost = 12,
                            meetingTag = "dawn_choir"
                        }
                    },
                    new
                    {
                        cardId = "card_route_dawn",
                        dedupeKey = "route_dawn",
                        sourceType = "project",
                        sourceFactionId = "faction_dawn",
                        sourceActorId = "project_passage",
                        effectFamily = "route",
                        rarity = "Epic",
                        displayName = "Тропа возвращения",
                        displaySummary = "Открывает путь через память.",
                        effectPayload = new
                        {
                            routeSeedId = "route_dawn",
                            remainingUses = 1
                        }
                    }
                },
                availableBlessingCards = new object[]
                {
                    new
                    {
                        cardId = "card_social_dawn",
                        dedupeKey = "social_dawn",
                        sourceType = "head",
                        sourceFactionId = "faction_dawn",
                        sourceActorId = "guard_test_founder",
                        effectFamily = "social",
                        rarity = "Rare",
                        displayName = "Песнь Рассвета",
                        displaySummary = "Укрепляет союз.",
                        effectPayload = new
                        {
                            relationshipBoost = 12,
                            meetingTag = "dawn_choir"
                        }
                    },
                    new
                    {
                        cardId = "card_route_dawn",
                        dedupeKey = "route_dawn",
                        sourceType = "project",
                        sourceFactionId = "faction_dawn",
                        sourceActorId = "project_passage",
                        effectFamily = "route",
                        rarity = "Epic",
                        displayName = "Тропа возвращения",
                        displaySummary = "Открывает путь через память.",
                        effectPayload = new
                        {
                            routeSeedId = "route_dawn",
                            remainingUses = 1
                        }
                    }
                },
                shownBlessingCardIds = new[] { "card_social_dawn", "card_route_dawn" },
                selectedBlessingCardIds = new[] { "card_route_dawn" },
                nextCandidateCursor = 2,
                rerollsRemaining = 1
            },
            preparedIncarnationPackage = includePreparedPackage
                ? new
                {
                    generatedFromDraftVersion = 4,
                    preparedAtTurn = 155,
                    preparedAtUtc = "2026-04-19T10:00:00Z",
                    selectedCardIds = new[] { "card_route_dawn" },
                    selectedCards = new object[]
                    {
                        new
                        {
                            cardId = "card_route_dawn",
                            dedupeKey = "route_dawn",
                            sourceType = "project",
                            sourceFactionId = "faction_dawn",
                            sourceActorId = "project_passage",
                            effectFamily = "route",
                            rarity = "Epic",
                            displayName = "Тропа возвращения",
                            displaySummary = "Открывает путь через память.",
                            effectPayload = new
                            {
                                routeSeedId = "route_dawn",
                                remainingUses = 1
                            }
                        }
                    }
                }
                : null
        });
        await WriteJsonAsync(ShiningTradeRequestState.PendingRequestsPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "shining_trade_memory_7",
                    factionId = "faction_memory",
                    factionName = "Хранители Отзвука",
                    tradeCycleId = "shining_return_7",
                    derivedTradeTier = 2,
                    derivedTradeSlotCount = 5,
                    derivedRarityCeiling = "rare",
                    derivedServiceMultiplier = 1.45,
                    merchantProfile = "shining_faction",
                    createdAtTurn = 158,
                    createdAtUtc = "2026-04-19T10:27:00Z"
                }
            }
        });
    }

    private async Task SetShiningRadianceAsync(int experience, int tier)
    {
        var raw = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var root = JsonNode.Parse(raw ?? "{}")!.AsObject();
        root["radiance"] = new JsonObject
        {
            ["experience"] = experience,
            ["tier"] = tier
        };

        await _fs.WriteFileAtomicAsync(
            ShiningAbodeState.StatePath,
            root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    [Fact]

    public async Task TryProcessCommand_AfterlifeArchive_ShowsUnreadArchiveBannerInRenderedPanel()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 3 },
            afterlifeArchive = new
            {
                stored = new[]
                {
                    new
                    {
                        archiveId = "archive_1",
                        entryType = "lore_fragment",
                        title = "Пепельная хроника",
                        summary = "Тестовая архивная запись.",
                        rarity = "Rare",
                        sourceLife = 1,
                        sourceKind = "codex",
                        acquiredAtUtc = "2026-03-26T00:00:00Z"
                    }
                }
            }
        });
        await WriteJsonAsync("game_state/control/afterlife_notifications.json", new
        {
            notifications = new[]
            {
                new
                {
                    notificationId = "archive_project_fuel_rejected:archive_fuel_req_9",
                    notificationType = "archive_project_fuel_rejected",
                    requestId = "archive_fuel_req_9",
                    status = "unread",
                    guardianId = "guardian_trade_001",
                    guardianName = "Азалия",
                    archiveId = "archive_1",
                    archiveTitle = "Пепельная хроника",
                    targetProjectId = "project_alpha",
                    targetProjectName = "Башня Наблюдений",
                    summary = "Хранитель Азалия отклонил архивную подпитку проекта «Башня Наблюдений». Запись возвращена в Архив души.",
                    createdAtTurn = 10,
                    createdAtUtc = "2026-03-26T00:14:00Z"
                }
            }
        });
        _console.QueueSelection("📚 Архив души", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/архив_души"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_archive_banner");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Непрочитанные ответы Хранителей по Архиву", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Архив души", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Any(choice => choice.Contains("Пепельная хроника", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task TryProcessCommand_AfterlifeArchive_Consultation_ShowsFullContractPreviewBeforeWritingRequest()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("input/turn_request.json", new { turnNumber = 16 });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 2,
            inkFeathers = new { current = 25 },
            afterlifeArchive = new
            {
                stored = new[]
                {
                    new
                    {
                        archiveId = "archive_consult_preview_1",
                        entryType = "lore_fragment",
                        title = "Пепельная хроника",
                        summary = "Тестовая архивная запись для консультации.",
                        rarity = "Rare",
                        sourceLife = 2,
                        sourceKind = "codex",
                        acquiredAtUtc = "2026-03-26T00:00:00Z"
                    }
                },
                actionReceipts = Array.Empty<object>()
            }
        });
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_archive_1",
                    canonicalName = "Азалия",
                    domain = "Knowledge",
                    manifestation = new
                    {
                        currentDisplayName = "Азалия"
                    },
                    relationshipData = new
                    {
                        currentReputation = 88
                    },
                    abode = new
                    {
                        abodeId = "abode_archive_1",
                        name = "Тихая Обитель",
                        isDiscovered = true
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_archive_1",
                canonicalName = "Азалия",
                manifestation = new
                {
                    currentDisplayName = "Азалия"
                }
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_archive_1",
                discoveredAbodes = new[] { "abode_archive_1" }
            }
        });
        await WriteJsonAsync(GuardianProjectState.TrackerPath, new
        {
            activeProjects = Array.Empty<object>(),
            completedProjects = Array.Empty<object>(),
            temporaryProjectModifiers = Array.Empty<object>()
        });
        _console.QueueSelection("Действие", "🔮 Консультация с дружественным Хранителем");
        await _stateManager.RefreshGameStateAsync();

        string? gmAction = null;
        var ex = await Record.ExceptionAsync(async () => gmAction = await _explorer.TryProcessCommand("/архив_души"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("archive_consultation_preview");
        Assert.Contains("ARCHIVE_CONSULTATION_REQUEST", gmAction ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("archive_consult_preview_1", gmAction ?? string.Empty, StringComparison.Ordinal);
        var pendingRaw = await _fs.ReadFileAsync(AfterlifeArchiveActionState.ConsultationRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("\"archiveId\": \"archive_consult_preview_1\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"requestedMode\": \"consultation\"", pendingRaw, StringComparison.Ordinal);
        var renderedText = ExtractRenderedText();
        Assert.Contains("Полный предпросмотр архивной консультации", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending_archive_consultation_request.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("archiveActionResolutions", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requestedMode", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_AfterlifeArchiveCandidates_ShowsFullCandidateContent()
    {
        await SeedAfterlifeStateAsync();
        var longContent = new string('Ж', 240);
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            livesHistory = new[] { new { incarnation = 1 } },
            afterlifeArchive = new { stored = Array.Empty<object>() }
        });
        await WriteRawJsonAsync("lore/codex_entries.json", $$"""
        {
          "entries": [
            {
              "entryId": "codex_long_1",
              "title": "Длинная запись",
              "category": "history",
              "content": "{{longContent}}",
              "discoveredAt": "2026-03-24T00:00:00Z",
              "incarnation": 1
            }
          ],
          "totalEntries": 1,
          "categories": { "history": 1 }
        }
        """);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/архив_кандидаты"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_archive_candidates_full_content");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Кандидаты в Архив", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Any(choice => choice.Contains("Длинная запись", StringComparison.OrdinalIgnoreCase)));
        var manifest = await _afterlifeArchiveCandidateService.ReadAsync();
        Assert.NotNull(manifest);
        Assert.Equal(longContent, Assert.Single(manifest!.Candidates).Content);
    }

    [Fact]
    public async Task TryProcessCommand_AfterlifeArchive_ShowsStoredFullEntryContent()
    {
        await SeedAfterlifeStateAsync();
        var fullContent = "Полный архивный текст, который должен отображаться вместо одной лишь краткой сводки.";
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            afterlifeArchive = new
            {
                stored = new[]
                {
                    new
                    {
                        archiveId = "archive_full_1",
                        entryType = "lore_fragment",
                        title = "Полная запись",
                        summary = "Короткая сводка",
                        content = fullContent,
                        rarity = "Rare",
                        sourceLife = 1,
                        sourceKind = "codex",
                        acquiredAtUtc = "2026-03-26T00:00:00Z"
                    }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var method = typeof(ExplorerMode).GetMethod(
            "ReadStoredAfterlifeArchiveEntriesAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(_explorer, Array.Empty<object>()) as Task;
        Assert.NotNull(task);
        await task!;
        var resultProperty = task.GetType().GetProperty("Result", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(resultProperty);
        var result = resultProperty!.GetValue(task) as System.Collections.IEnumerable;
        Assert.NotNull(result);
        var entries = result!.Cast<object>().ToList();
        Assert.Single(entries);
        var contentProperty = entries[0].GetType().GetProperty("Content", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(contentProperty);
        Assert.Equal(fullContent, contentProperty!.GetValue(entries[0])?.ToString());
    }

    [Fact]
    public async Task TryProcessCommand_AfterlifeArchive_DetailResolvesReadableGuardianAndProjectLabels()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_archive_1",
                    canonicalName = "Азалия"
                }
            }
        });
        await WriteJsonAsync(GuardianProjectState.TrackerPath, new
        {
            activeProjects = new[]
            {
                new
                {
                    guardianId = "guardian_archive_1",
                    project = new
                    {
                        projectId = "project_archive_1",
                        projectName = "Башня Наблюдений"
                    }
                }
            },
            completedProjects = Array.Empty<object>(),
            temporaryProjectModifiers = Array.Empty<object>()
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            afterlifeArchive = new
            {
                stored = new[]
                {
                    new
                    {
                        archiveId = "archive_link_1",
                        entryType = "lore_fragment",
                        title = "Архивная связка",
                        summary = "Проверка читаемых связей.",
                        rarity = "Rare",
                        sourceLife = 1,
                        sourceKind = "codex",
                        acquiredAtUtc = "2026-03-26T00:00:00Z",
                        sourceGuardianId = "guardian_archive_1",
                        reservation = new
                        {
                            reservationKind = "project_fuel",
                            requestId = "archive_fuel_1",
                            guardianId = "guardian_archive_1",
                            guardianName = "",
                            targetProjectId = "project_archive_1",
                            targetProjectName = "",
                            createdAtTurn = 10,
                            createdAtUtc = "2026-03-26T00:05:00Z"
                        }
                    }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/архив_души"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_archive_readable_links");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Связанный хранитель: Азалия", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Целевой проект: Башня Наблюдений", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идентификатор хранителя: guardian_archive_1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идентификатор проекта: project_archive_1", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_AfterlifeArchive_DetailOffersExactGuardianAndProjectDrillDown()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_archive_1",
                    canonicalName = "Азалия",
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Тестовая форма."
                    },
                    manifestationHistory = Array.Empty<object>(),
                    abode = new
                    {
                        abodeId = "abode_archive_1",
                        name = "Тихая Обитель"
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_archive_1"
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_archive_1"
            }
        });
        await WriteJsonAsync(GuardianProjectState.TrackerPath, new
        {
            activeProjects = new[]
            {
                new
                {
                    guardianId = "guardian_archive_1",
                    project = new
                    {
                        projectId = "project_archive_1",
                        projectName = "Башня Наблюдений"
                    }
                }
            },
            completedProjects = Array.Empty<object>(),
            temporaryProjectModifiers = Array.Empty<object>()
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            afterlifeArchive = new
            {
                stored = new[]
                {
                    new
                    {
                        archiveId = "archive_link_1",
                        entryType = "lore_fragment",
                        title = "Архивная связка",
                        summary = "Проверка навигации.",
                        rarity = "Rare",
                        sourceLife = 1,
                        sourceKind = "codex",
                        acquiredAtUtc = "2026-03-26T00:00:00Z",
                        sourceGuardianId = "guardian_archive_1",
                        reservation = new
                        {
                            reservationKind = "project_fuel",
                            requestId = "archive_fuel_1",
                            guardianId = "guardian_archive_1",
                            targetProjectId = "project_archive_1",
                            createdAtTurn = 10,
                            createdAtUtc = "2026-03-26T00:05:00Z"
                        }
                    }
                }
            }
        });
        _console.QueueSelection("Действие", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/архив_души"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_archive_drill_down_actions");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Действие", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("🛡 Открыть связанного Хранителя", StringComparer.Ordinal) &&
                     entry.Choices.Contains("🔬 Открыть целевой проект", StringComparer.Ordinal));
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_ShowsActiveGuardianMarker()
    {
        await SeedSessionForCommandAsync("/хранители");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardians_active_marker");
        Assert.Contains(_console.SelectionChoicesHistory.SelectMany(entry => entry.Choices),
            choice => choice.Contains("АКТИВНЫЙ", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_console.SelectionTitles,
            title => title.Contains("Активный Хранитель: Азалия", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_DetailHeaderMarksActiveGuardian()
    {
        await SeedSessionForCommandAsync("/хранители");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardians_active_detail_header");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Азалия · активный", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Текущий активный Хранитель", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_TradeDetailRendersDerivedSlotCount()
    {
        await SeedSessionForCommandAsync("/хранители");
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guard_test_azalia",
                    canonicalName = "Азалия",
                    domain = "Social",
                    nameVariants = new
                    {
                        @default = "Азалия",
                        feminine = "Азалия",
                        masculine = (string?)null,
                        neutral = (string?)null
                    },
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Тестовая текущая форма Азалии."
                    },
                    manifestationHistory = Array.Empty<object>(),
                    description = "Тестовая хранительница для smoke test explorer mode.",
                    abodePower = new
                    {
                        currentPower = 35,
                        tier = "Хрупкая",
                        lastUpdatedAt = "2026-03-23T00:00:00Z",
                        history = Array.Empty<object>()
                    },
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
                    },
                    abode = new
                    {
                        abodeId = "abode_social_001",
                        name = "Шелковая Обитель"
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guard_test_azalia",
                canonicalName = "Азалия",
                nameVariants = new
                {
                    @default = "Азалия",
                    feminine = "Азалия",
                    masculine = (string?)null,
                    neutral = (string?)null
                },
                manifestation = new
                {
                    currentDisplayName = "Азалия",
                    formFlexibility = "selective",
                    currentPresentationStyle = "feminine",
                    currentPronouns = "она/её",
                    appearanceDescription = "Тестовая текущая форма Азалии."
                },
                manifestationHistory = Array.Empty<object>()
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_social_001"
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_trade_detail_derived_slot_count");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Доступна: 5 локальных слотов", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Доступна: 4 локальных слота", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_CompletedProjectSummaryUsesPlayerFacingStateLabel()
    {
        await SeedSessionForCommandAsync("/хранители");
        await WriteJsonAsync(GuardianProjectState.TrackerPath, new
        {
            activeProjects = Array.Empty<object>(),
            completedProjects = new[]
            {
                new
                {
                    guardianId = "guard_test_azalia",
                    project = new
                    {
                        projectId = "guardian_project_completed_summary_001",
                        projectType = "abode_expansion",
                        projectTier = "major",
                        projectMode = "internal",
                        projectName = "Круг завершённого расширения",
                        finalState = "Completed",
                        completionTurn = 9,
                        outcome = "Обитель обрела новый устойчивый ярус."
                    }
                }
            },
            temporaryProjectModifiers = Array.Empty<object>()
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_completed_project_summary");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Завершённые проекты", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Круг завершённого расширения", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Завершён", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Completed", renderedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_CompletedProjectDetailShowsEntriesBeyondFirstFive()
    {
        await SeedSessionForCommandAsync("/хранители");
        await WriteJsonAsync(GuardianProjectState.TrackerPath, new
        {
            activeProjects = Array.Empty<object>(),
            completedProjects = Enumerable.Range(1, 6).Select(i => new
            {
                guardianId = "guard_test_azalia",
                project = new
                {
                    projectId = $"guardian_project_completed_{i}",
                    projectType = "abode_expansion",
                    projectTier = "major",
                    projectMode = "internal",
                    projectName = $"Завершённый проект {i}",
                    finalState = "Completed",
                    completionTurn = i,
                    outcome = $"Итог {i}"
                }
            }).ToArray(),
            temporaryProjectModifiers = Array.Empty<object>()
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_completed_project_full_list");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Завершённый проект 6", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_DetailOffersProjectDrillDown()
    {
        await SeedSessionForCommandAsync("/хранители");
        await WriteJsonAsync(GuardianProjectState.TrackerPath, new
        {
            activeProjects = new[]
            {
                new
                {
                    guardianId = "guard_test_azalia",
                    project = new
                    {
                        projectId = "guardian_project_active_001",
                        projectType = "lore_research",
                        projectTier = "major",
                        projectMode = "internal",
                        projectName = "Осмотр и замер контуров",
                        activeState = "in_progress"
                    }
                }
            },
            completedProjects = Array.Empty<object>(),
            temporaryProjectModifiers = Array.Empty<object>()
        });
        _console.QueueSelection("Действие", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_project_drill_down_action");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Действие", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("🔬 Открыть проекты Хранителя", StringComparer.Ordinal));
    }

    [Fact]
    public async Task TryProcessCommand_GuardianProjects_TemporaryModifierDetailShowsEntriesBeyondFirstFour()
    {
        await SeedSessionForCommandAsync("/проекты_хранителей");
        await WriteJsonAsync(GuardianProjectState.TrackerPath, new
        {
            activeProjects = new[]
            {
                new
                {
                    guardianId = "guard_test_azalia",
                    project = new
                    {
                        projectId = "guardian_project_active_001",
                        projectType = "lore_research",
                        projectTier = "major",
                        projectMode = "internal",
                        projectName = "Осмотр и замер контуров",
                        activeState = "in_progress",
                        totalResourceCost = 12,
                        resourcesSpent = 8,
                        totalTimeCostMinutes = 120,
                        timeSpentMinutes = 60,
                        totalSteps = 3,
                        currentStep = 2
                    }
                }
            },
            completedProjects = Array.Empty<object>(),
            temporaryProjectModifiers = Enumerable.Range(1, 5).Select(i => new
            {
                guardianId = "guard_test_azalia",
                modifierId = $"modifier_{i}",
                modifierType = "next_internal_project_starting_pressure",
                value = i,
                remainingApplications = 6 - i
            }).ToArray()
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/проекты_хранителей"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_project_temporary_modifier_full_list");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Идентификатор модификатора: modifier_5", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_ShowFullGuardianJournalActionRendersAllEntries()
    {
        await SeedSessionForCommandAsync("/хранители");
        await WriteJsonAsync(GuardianThoughtJournalState.StatePath, new
        {
            entries = Enumerable.Range(1, 4).Select(i => new
            {
                entryId = $"gthought_{i}",
                guardianId = "guard_test_azalia",
                turn = i,
                timestamp = $"2026-03-2{i}T00:00:00Z",
                title = $"Мысль {i}",
                summary = $"Подробность мысли {i}",
                consequence = $"echo_{i}",
                attitude = $"attitude_{i}",
                intent = $"intent_{i}",
                tags = new[] { $"thought_tag_{i}" }
            }).ToArray()
        });
        await WriteJsonAsync(GuardianSocialJournalState.StatePath, new
        {
            entries = Enumerable.Range(1, 6).Select(i => new
            {
                entryId = $"gsocial_{i}",
                guardianId = "guard_test_azalia",
                turn = i,
                timestamp = $"2026-03-2{i}T01:00:00Z",
                title = $"Разговор {i}",
                summary = $"Краткая память общения {i}",
                requestId = $"guardian_social_req_{i}",
                interactionType = i % 2 == 0 ? "lore" : "talk",
                status = "accepted",
                responseMode = i % 2 == 0 ? "lore_revealed" : "conversation",
                tags = new[] { $"social_tag_{i}" }
            }).ToArray()
        });
        _console.QueueSelection("Действие", "📚 Показать весь журнал Хранителя", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_full_journal");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Действие", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("📚 Показать весь журнал Хранителя", StringComparer.Ordinal));
        Assert.True(_console.SelectionTitles.Count(title => title.Contains("Действие", StringComparison.OrdinalIgnoreCase)) >= 2,
            BuildConsoleDiagnostics("guardian_full_journal"));
        var renderedText = ExtractRenderedText();
        Assert.Contains("показано 3 из 4", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("показано 5 из 6", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Полный журнал Хранителя", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Все актуальные мысли", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Вся память общения", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("t4: Мысль 4 — Подробность мысли 4", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("t6: Разговор 6 — Краткая память общения 6", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идентификатор записи: gthought_4", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идентификатор записи: gsocial_6", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идентификатор Хранителя: guard_test_azalia", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Последствие: echo_4", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Намерение: intent_4", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идентификатор запроса: guardian_social_req_6", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Request id", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Режим ответа: знание раскрыто", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Метки: social_tag_6", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_PlayerGuardianFoundation_CompletedStateShowsDurableIds()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 7,
            inkFeathers = new { current = 33 },
            playerFoundedGuardianId = "guard_founded_1",
            playerGuardianFoundationStatus = PlayerGuardianFoundationState.SoulStateFoundationStatusFounded
        });
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new object[]
            {
                new
                {
                    guardianId = "guard_founded_1",
                    canonicalName = "Северин",
                    originType = PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul,
                    guardianRoleToPlayer = "active_patron",
                    manifestation = new { currentDisplayName = "Северин" },
                    abode = new
                    {
                        abodeId = "abode_founded_1",
                        name = "Обитель Северина"
                    },
                    founderBonuses = new
                    {
                        extraGachaChargesPerReturn = PlayerGuardianFoundationState.DefaultFounderExtraGachaChargesPerReturn
                    },
                    founderAbodeFeatures = new
                    {
                        featureTitle = "Зов основателя",
                        featureSummary = "Новая Обитель притягивает собственную линию резидентов.",
                        residentAttractionMode = PlayerGuardianFoundationState.FounderAbodeResidentAttractionModeFounderCall
                    }
                },
                new
                {
                    guardianId = "guard_patron_1",
                    canonicalName = "Азалия",
                    guardianRoleToPlayer = PlayerGuardianFoundationState.GuardianRoleFormerPatron,
                    manifestation = new { currentDisplayName = "Азалия" }
                }
            },
            activeGuardian = new
            {
                guardianId = "guard_founded_1",
                canonicalName = "Северин",
                originType = PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul,
                manifestation = new { currentDisplayName = "Северин" },
                abode = new
                {
                    abodeId = "abode_founded_1",
                    name = "Обитель Северина"
                }
            },
            playerGuardianFoundationHistory = new[]
            {
                new
                {
                    requestId = "foundation_req_1",
                    guardianId = "guard_founded_1",
                    guardianDisplayName = "Северин",
                    founderSoulName = "Тестовая Душа",
                    formerPatronGuardianId = "guard_patron_1",
                    formerPatronGuardianName = "Азалия",
                    foundationSource = PlayerGuardianFoundationState.FoundationSourceShiningReturn,
                    resolvedAtTurn = 174,
                    resolvedAtUtc = "2026-04-22T10:00:00Z"
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/found_guardian_mantle"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("player_guardian_foundation_completed_ids");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Идентификатор основанного Хранителя: guard_founded_1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идентификатор активного Хранителя: guard_founded_1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идентификатор текущей Обители: abode_founded_1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идентификатор запроса основания: foundation_req_1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идентификатор прежнего покровителя: guard_patron_1", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_ResidentDetailShowsFullCanonicalData()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new object[]
            {
                new
                {
                    guardianId = "guardian_resident_001",
                    canonicalName = "Азалия",
                    abode = new
                    {
                        abodeId = "abode_social_azalia_001",
                        name = "Лазурная Обитель"
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_resident_001"
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_social_azalia_001"
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_ember_001",
                    guardianId = "guardian_resident_001",
                    abodeId = "abode_social_azalia_001",
                    displayName = "Лиора",
                    residentKind = "ascended_soul",
                    roleLabel = "Певчая",
                    summary = "Берегла клятвы на мостах памяти.",
                    bondLevel = 86,
                    bondTier = "trusted",
                    abodeDevotionLevel = 74,
                    abodeDevotionTier = "devoted",
                    restlessness = 18,
                    migrationState = "settled",
                    historyRevealed = true,
                    bondRewardState = "eligible",
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Вернётся как тихий проводник через лазурные мосты.",
                        bondReason = "Она сохранила песнь клятвы.",
                        coreTraits = new[] { "верная", "смелая" },
                        archetypeHints = new[] { "проводник", "свидетель" },
                        appearanceMotifs = new[] { "лазурная нить", "мостовой свет" }
                    },
                    personalityProfile = new
                    {
                        archetype = "Witness",
                        worldview = "Hopeful",
                        culturalLayer = "Glass Gardens",
                        coreValues = new[] { "верность", "память", "милосердие" },
                        personalityTraits = new object[]
                        {
                            new { traitName = "тихая решимость", value = 8, valueDescription = "не отступает" },
                            new { traitName = "бережность", value = 7, valueDescription = "бережет узы" },
                            new { traitName = "внимательность", value = 9, valueDescription = "замечает каждую трещину" },
                            new { traitName = "терпение", value = 8, valueDescription = "не рвёт связь раньше времени" },
                            new { traitName = "чуткость", value = 6, valueDescription = "слышит перемены в Обители" }
                        }
                    },
                    abodeDisposition = new
                    {
                        powerSensitivity = "high",
                        migrationDisposition = "selective",
                        communalOrientation = "medium",
                        stabilityNeed = "high"
                    },
                    linkedSoulQuestId = "soul_quest_liora",
                    grantedRelicId = "relic_echo_liora",
                    availableInteractions = new[] { "talk", "history" }
                }
            },
            thoughtJournal = new object[]
            {
                new
                {
                    entryId = "resident_thought_1",
                    residentId = "resident_ember_001",
                    title = "Сдвиг в сердце Обители",
                    summary = "Лиора ощущает новый ритм сводов.",
                    eventType = "abode_devotion_shift",
                    consequence = "migration_pressure",
                    attitude = "cautious",
                    intent = "remain",
                    tags = new[] { "pressure", "abode" },
                    turn = 18,
                    timestamp = "2026-04-19T18:00:00Z"
                }
            },
            interactionLog = new object[]
            {
                new
                {
                    entryId = "resident_interaction_1",
                    residentId = "resident_ember_001",
                    title = "Разговор о клятве",
                    summary = "Лиора вспомнила песнь переправы.",
                    eventType = "talk",
                    consequence = "bond_warmed",
                    attitude = "warm",
                    intent = "share",
                    tags = new[] { "oath", "memory" },
                    turn = 19,
                    timestamp = "2026-04-19T19:00:00Z"
                }
            },
            historyLog = new object[]
            {
                new
                {
                    entryId = "resident_history_1",
                    residentId = "resident_ember_001",
                    title = "Письмо через огонь",
                    summary = "Когда-то она несла письмо через горящий мост.",
                    tags = new[] { "bridge", "oath" },
                    revealedAtTurn = 20,
                    revealedAtUtc = "2026-04-19T20:00:00Z"
                }
            },
            transferReceipts = new object[]
            {
                new
                {
                    requestId = "resident_transfer_1",
                    residentId = "resident_ember_001",
                    residentName = "Лиора",
                    sourceGuardianId = "guardian_resident_001",
                    sourceGuardianName = "Азалия",
                    sourceAbodeId = "abode_social_azalia_001",
                    sourceAbodeName = "Лазурная Обитель",
                    targetGuardianId = "guardian_beta",
                    targetGuardianName = "Мириэль",
                    targetAbodeId = "abode_beta",
                    targetAbodeName = "Сад Перекрёстков",
                    status = "accepted",
                    transferMode = "accepted_transfer",
                    departureHistoryEntryId = "transfer_depart_1",
                    arrivalHistoryEntryId = "transfer_arrive_1",
                    reason = "followed_resonance",
                    resolvedAtTurn = 21,
                    resolvedAtUtc = "2026-04-19T21:00:00Z"
                }
            },
            interactionReceipts = Array.Empty<object>(),
            rosterReceipts = Array.Empty<object>()
        });
        _console.QueueSelection("Действие", "🏛 Обитатели Обители");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_resident_detail_full");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Обитатель Обители", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("тихая решимость 8/10", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("внимательность 9/10", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Актуальные мысли", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Последствие: migration_pressure", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Краткая память общения", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Намерение: share", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Раскрытые фрагменты прошлого", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Метки: bridge, oath", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Полная история переходов", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Идентификатор записи прибытия: transfer_arrive_1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("неспокойствие 18/100", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Образ будущего спутника: Вернётся как тихий проводник через лазурные мосты.", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Образы и мотивы: лазурная нить, мостовой свет", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026-04-19T18:00:00Z", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026-04-19T19:00:00Z", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026-04-19T20:00:00Z", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026-04-19T21:00:00Z", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abode_devotion_shift", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("restlessness", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Arrival history id", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_AbodeResidentDetailResolvesLinkedQuestAndRelicLabels()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_resident_001",
                    canonicalName = "Азалия",
                    abode = new
                    {
                        abodeId = "abode_social_azalia_001",
                        name = "Лазурная Обитель"
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_resident_001",
                canonicalName = "Азалия",
                abode = new
                {
                    abodeId = "abode_social_azalia_001",
                    name = "Лазурная Обитель"
                }
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_social_azalia_001"
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new[]
            {
                new
                {
                    residentId = "resident_ember_001",
                    displayName = "Лиора",
                    guardianId = "guardian_resident_001",
                    abodeId = "abode_social_azalia_001",
                    residentKind = "wayfaring_soul",
                    roleLabel = "Певчая",
                    bondLevel = 72,
                    bondTier = "trusted",
                    abodeDevotionLevel = 66,
                    abodeDevotionTier = "settled",
                    migrationState = "settled",
                    restlessness = 12,
                    historyRevealed = true,
                    bondRewardState = "granted",
                    linkedSoulQuestId = "quest_liora",
                    grantedRelicId = "relic_echo_liora",
                    personalityProfile = new
                    {
                        archetype = "Singer",
                        worldview = "Songs preserve bridges.",
                        culturalLayer = "Choir",
                        coreValues = new[] { "верность" },
                        personalityTraits = new object[]
                        {
                            new { traitName = "тихая решимость", value = 8, valueDescription = "тихая решимость" }
                        }
                    },
                    abodeDisposition = new
                    {
                        powerSensitivity = "high",
                        migrationDisposition = "selective",
                        communalOrientation = "medium",
                        stabilityNeed = "high"
                    }
                }
            },
            thoughtJournal = Array.Empty<object>(),
            interactionLog = Array.Empty<object>(),
            historyLog = Array.Empty<object>(),
            transferReceipts = Array.Empty<object>(),
            interactionReceipts = Array.Empty<object>(),
            rosterReceipts = Array.Empty<object>()
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 10, total = 10 },
            soulRelics = new
            {
                stored = new[]
                {
                    new
                    {
                        relicId = "relic_echo_liora",
                        name = "Отзвук Лиоры",
                        rarity = "Rare"
                    }
                },
                equipped = Array.Empty<object>()
            }
        });
        await WriteJsonAsync("game_state/quests/soul_quests.json", new
        {
            quests = new[]
            {
                new
                {
                    questId = "quest_liora",
                    title = "Просьба Лиоры",
                    relatedAfterlifeResidentId = "resident_ember_001"
                }
            }
        });
        _console.QueueSelection("Действие", "🏛 Обитатели Обители");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_resident_detail_linked_labels");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Связанный квест души: Просьба Лиоры (quest_liora)", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Дарованная реликвия: Отзвук Лиоры (relic_echo_liora)", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_ResidentDetailOpensLinkedSoulQuest()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_resident_001",
                    canonicalName = "Азалия",
                    abode = new
                    {
                        abodeId = "abode_social_azalia_001",
                        name = "Лазурная Обитель"
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_resident_001",
                canonicalName = "Азалия",
                abode = new
                {
                    abodeId = "abode_social_azalia_001",
                    name = "Лазурная Обитель"
                }
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_social_azalia_001"
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new[]
            {
                new
                {
                    residentId = "resident_ember_001",
                    displayName = "Лиора",
                    guardianId = "guardian_resident_001",
                    abodeId = "abode_social_azalia_001",
                    residentKind = "wayfaring_soul",
                    roleLabel = "Певчая",
                    bondLevel = 72,
                    bondTier = "trusted",
                    abodeDevotionLevel = 66,
                    abodeDevotionTier = "settled",
                    migrationState = "settled",
                    restlessness = 12,
                    historyRevealed = true,
                    bondRewardState = "granted",
                    linkedSoulQuestId = "quest_liora",
                    personalityProfile = new
                    {
                        archetype = "Singer",
                        worldview = "Songs preserve bridges.",
                        culturalLayer = "Choir",
                        coreValues = new[] { "верность" },
                        personalityTraits = Array.Empty<object>()
                    },
                    abodeDisposition = new
                    {
                        powerSensitivity = "high",
                        migrationDisposition = "selective",
                        communalOrientation = "medium",
                        stabilityNeed = "high"
                    }
                }
            },
            thoughtJournal = Array.Empty<object>(),
            interactionLog = Array.Empty<object>(),
            historyLog = Array.Empty<object>(),
            transferReceipts = Array.Empty<object>(),
            interactionReceipts = Array.Empty<object>(),
            rosterReceipts = Array.Empty<object>()
        });
        await WriteJsonAsync("game_state/quests/soul_quests.json", new
        {
            quests = new[]
            {
                new
                {
                    questId = "quest_liora",
                    title = "Просьба Лиоры",
                    description = "Услышь тихую песнь над лазурным мостом.",
                    relatedAfterlifeResidentId = "resident_ember_001"
                }
            }
        });

        var detailMethod = typeof(ExplorerMode).GetMethod("ShowGuardianAbodeResidentDetailByIdAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(detailMethod);
        _console.QueueSelection("Действие", "📜 Открыть связанный квест души");
        await _stateManager.RefreshGameStateAsync();

        var task = detailMethod!.Invoke(_explorer, new object?[] { "resident_ember_001" }) as Task<bool>;
        Assert.NotNull(task);
        Assert.True(await task!);

        AssertNoHiddenExplorerErrors("guardian_resident_linked_quest_drilldown");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Просьба Лиоры", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Квест души", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_ResidentDetailOpensGrantedRelic()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_resident_001",
                    canonicalName = "Азалия",
                    abode = new
                    {
                        abodeId = "abode_social_azalia_001",
                        name = "Лазурная Обитель"
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_resident_001",
                canonicalName = "Азалия",
                abode = new
                {
                    abodeId = "abode_social_azalia_001",
                    name = "Лазурная Обитель"
                }
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_social_azalia_001"
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new[]
            {
                new
                {
                    residentId = "resident_ember_001",
                    displayName = "Лиора",
                    guardianId = "guardian_resident_001",
                    abodeId = "abode_social_azalia_001",
                    residentKind = "wayfaring_soul",
                    roleLabel = "Певчая",
                    bondLevel = 72,
                    bondTier = "trusted",
                    abodeDevotionLevel = 66,
                    abodeDevotionTier = "settled",
                    migrationState = "settled",
                    restlessness = 12,
                    historyRevealed = true,
                    bondRewardState = "granted",
                    grantedRelicId = "relic_echo_liora",
                    personalityProfile = new
                    {
                        archetype = "Singer",
                        worldview = "Songs preserve bridges.",
                        culturalLayer = "Choir",
                        coreValues = new[] { "верность" },
                        personalityTraits = Array.Empty<object>()
                    },
                    abodeDisposition = new
                    {
                        powerSensitivity = "high",
                        migrationDisposition = "selective",
                        communalOrientation = "medium",
                        stabilityNeed = "high"
                    }
                }
            },
            thoughtJournal = Array.Empty<object>(),
            interactionLog = Array.Empty<object>(),
            historyLog = Array.Empty<object>(),
            transferReceipts = Array.Empty<object>(),
            interactionReceipts = Array.Empty<object>(),
            rosterReceipts = Array.Empty<object>()
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 10, total = 10 },
            soulRelics = new
            {
                stored = new[]
                {
                    new
                    {
                        relicId = "relic_echo_liora",
                        name = "Отзвук Лиоры",
                        rarity = "Rare",
                        description = "Хранит тихий перелив её голоса."
                    }
                },
                equipped = Array.Empty<object>()
            }
        });

        var detailMethod = typeof(ExplorerMode).GetMethod("ShowGuardianAbodeResidentDetailByIdAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(detailMethod);
        _console.QueueSelection("Действие", "💎 Открыть дарованную реликвию");
        await _stateManager.RefreshGameStateAsync();

        var task = detailMethod!.Invoke(_explorer, new object?[] { "resident_ember_001" }) as Task<bool>;
        Assert.NotNull(task);
        Assert.True(await task!);

        AssertNoHiddenExplorerErrors("guardian_resident_granted_relic_drilldown");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Отзвук Лиоры", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Реликвия", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_DepartedResidentStillHasReachableFullDetail()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_resident_001",
                    canonicalName = "Азалия",
                    abode = new
                    {
                        abodeId = "abode_social_azalia_001",
                        name = "Лазурная Обитель"
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_resident_001",
                canonicalName = "Азалия",
                abode = new
                {
                    abodeId = "abode_social_azalia_001",
                    name = "Лазурная Обитель"
                }
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_social_azalia_001"
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_departed_001",
                    guardianId = "guardian_resident_001",
                    abodeId = "abode_social_azalia_001",
                    displayName = "Лиора",
                    residentKind = "ascended_soul",
                    roleLabel = "Певчая",
                    bondLevel = 81,
                    bondTier = "trusted",
                    abodeDevotionLevel = 54,
                    abodeDevotionTier = "attached",
                    restlessness = 66,
                    migrationState = "ready_to_transfer",
                    historyRevealed = true,
                    bondRewardState = "granted",
                    isPresent = false,
                    personalityProfile = new
                    {
                        archetype = "Witness"
                    }
                }
            },
            thoughtJournal = Array.Empty<object>(),
            interactionLog = Array.Empty<object>(),
            historyLog = new object[]
            {
                new
                {
                    entryId = "resident_history_departed_1",
                    residentId = "resident_departed_001",
                    title = "Последний взгляд на своды",
                    summary = "Лиора запомнила путь назад.",
                    tags = new[] { "departure" },
                    revealedAtTurn = 22,
                    revealedAtUtc = "2026-04-19T22:00:00Z"
                }
            },
            transferReceipts = new object[]
            {
                new
                {
                    requestId = "resident_departure_1",
                    residentId = "resident_departed_001",
                    residentName = "Лиора",
                    sourceGuardianId = "guardian_resident_001",
                    sourceGuardianName = "Азалия",
                    sourceAbodeId = "abode_social_azalia_001",
                    sourceAbodeName = "Лазурная Обитель",
                    targetGuardianId = "guardian_beta",
                    targetGuardianName = "Мириэль",
                    targetAbodeId = "abode_beta",
                    targetAbodeName = "Сад Перекрёстков",
                    status = "accepted",
                    transferMode = "accepted_transfer",
                    departureHistoryEntryId = "depart_hist_1",
                    arrivalHistoryEntryId = "arrive_hist_1",
                    resolvedAtTurn = 23,
                    resolvedAtUtc = "2026-04-19T23:00:00Z"
                }
            },
            interactionReceipts = Array.Empty<object>(),
            rosterReceipts = Array.Empty<object>()
        });
        _console.QueueSelection("Действие", "🏛 Обитатели Обители");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_departed_resident_detail");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Лиора", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("уже покинул Обитель", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Полная история переходов", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Последний взгляд на своды", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_AfterlifeInbox_ResidentNotificationOpensExactResidentDetail()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_resident_001",
                    canonicalName = "Азалия",
                    abode = new
                    {
                        abodeId = "abode_social_azalia_001",
                        name = "Лазурная Обитель"
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_resident_001",
                canonicalName = "Азалия",
                abode = new
                {
                    abodeId = "abode_social_azalia_001",
                    name = "Лазурная Обитель"
                }
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_social_azalia_001"
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_ember_001",
                    displayName = "Лиора",
                    guardianId = "guardian_resident_001",
                    abodeId = "abode_social_azalia_001",
                    residentKind = "wayfaring_soul",
                    roleLabel = "Певчая",
                    bondLevel = 72,
                    bondTier = "trusted",
                    abodeDevotionLevel = 66,
                    abodeDevotionTier = "settled",
                    migrationState = "settled",
                    restlessness = 12,
                    historyRevealed = true,
                    bondRewardState = "granted"
                }
            },
            thoughtJournal = Array.Empty<object>(),
            interactionLog = Array.Empty<object>(),
            historyLog = Array.Empty<object>(),
            transferReceipts = Array.Empty<object>(),
            interactionReceipts = Array.Empty<object>(),
            rosterReceipts = Array.Empty<object>()
        });
        await WriteJsonAsync("game_state/control/afterlife_notifications.json", new
        {
            notifications = new[]
            {
                new
                {
                    notificationId = "abode_resident_history_revealed:history_req_1",
                    notificationType = "abode_resident_history_revealed",
                    requestId = "history_req_1",
                    status = "unread",
                    guardianId = "guardian_resident_001",
                    guardianName = "Азалия",
                    residentId = "resident_ember_001",
                    residentName = "Лиора",
                    archiveId = "",
                    archiveTitle = "",
                    targetProjectId = "",
                    targetProjectName = "",
                    summary = "Резидент «Лиора» раскрыл часть своей прошлой истории.",
                    createdAtTurn = 24,
                    createdAtUtc = "2026-04-20T00:24:00Z"
                }
            }
        });
        _console.QueueSelection("Действие", "👤 Открыть резидента");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/уведомления_загробья"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_inbox_exact_resident_detail");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Действие", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Contains("👤 Открыть резидента", StringComparer.Ordinal));
        var renderedText = ExtractRenderedText();
        Assert.Contains("Связанный резидент", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Обитатель Обители", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Лиора", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_AfterlifeInbox_ResidentQuestNotificationOpensExactQuestDetail()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/quests/soul_quests.json", new
        {
            quests = new[]
            {
                new
                {
                    questId = "quest_liora",
                    title = "Просьба Лиоры",
                    description = "Услышь тихую песнь над лазурным мостом.",
                    relatedAfterlifeResidentId = "resident_ember_001"
                }
            }
        });
        await WriteJsonAsync("game_state/control/afterlife_notifications.json", new
        {
            notifications = new[]
            {
                new
                {
                    notificationId = "abode_resident_quest_available:resident_ember_001:quest_liora",
                    notificationType = "abode_resident_quest_available",
                    requestId = "resident_ember_001:quest_liora",
                    status = "unread",
                    guardianId = "guardian_resident_001",
                    guardianName = "Азалия",
                    residentId = "resident_ember_001",
                    residentName = "Лиора",
                    archiveId = "",
                    archiveTitle = "",
                    targetProjectId = "",
                    targetProjectName = "",
                    summary = "Лиора зовёт душу к новому пути.",
                    createdAtTurn = 24,
                    createdAtUtc = "2026-04-20T00:24:00Z"
                }
            }
        });
        _console.QueueSelection("Действие", "🧵 Открыть квест души");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/уведомления_загробья"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_inbox_exact_resident_quest");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Просьба Лиоры", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Квест души", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_AfterlifeInbox_ArchiveProjectNotificationOpensExactProjectDetail()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync(GuardianProjectState.TrackerPath, new
        {
            activeProjects = new[]
            {
                new
                {
                    guardianId = "guardian_trade_001",
                    project = new
                    {
                        projectId = "project_alpha",
                        projectName = "Башня Наблюдений",
                        displayName = "Башня Наблюдений",
                        stage = "active",
                        summary = "Проект держит путь к Архиву."
                    }
                }
            },
            completedProjects = Array.Empty<object>()
        });
        await WriteJsonAsync("game_state/control/afterlife_notifications.json", new
        {
            notifications = new[]
            {
                new
                {
                    notificationId = "archive_project_fuel_rejected:archive_fuel_req_9",
                    notificationType = "archive_project_fuel_rejected",
                    requestId = "archive_fuel_req_9",
                    status = "unread",
                    guardianId = "guardian_trade_001",
                    guardianName = "Азалия",
                    archiveId = "archive_1",
                    archiveTitle = "Пепельная хроника",
                    targetProjectId = "project_alpha",
                    targetProjectName = "Башня Наблюдений",
                    summary = "Архивная подпитка проекта была отклонена.",
                    createdAtTurn = 10,
                    createdAtUtc = "2026-03-26T00:14:00Z"
                }
            }
        });
        _console.QueueSelection("Действие", "🔬 Открыть проекты Хранителей");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/уведомления_загробья"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_inbox_exact_project_detail");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Проект Хранителя", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Башня Наблюдений", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SoulInfo_ManifestationRequestsOpenExactInspection()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Неон-Сити",
            currentIncarnation = 2,
            inkFeathers = new { current = 25 },
            soulRelics = new
            {
                stored = new[]
                {
                    new
                    {
                        relicId = "relic_companion_echo_001",
                        name = "Отзвук 1",
                        rarity = "Rare"
                    }
                },
                equipped = Array.Empty<object>()
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "manifest_req_1",
                    relicId = "relic_companion_echo_001",
                    relicName = "Отзвук 1",
                    manifestationSource = "resident_relic",
                    sourceResidentId = "resident_ember_001",
                    sourceGuardianId = "guardian_resident_001",
                    sourceGuardianName = "Азалия",
                    targetIncarnation = 2,
                    companionNameHint = "Спутник 1",
                    futureCompanionPrompt = "Появится на раннем этапе новой жизни."
                }
            }
        });
        _console.QueueSelection("Действие души", "👤 Осмотреть пути воплощения спутников", "← Назад");
        _console.QueueSelection("Пути воплощения спутников", "👤 Спутник 1", "← Назад");
        _console.QueueSelection("Действие", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/душа"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("soul_info_manifestation_exact_inspection");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Пути воплощения спутников", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Any(choice => choice.Contains("Спутник 1", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_ActionsOpenExactPendingNativeFactionDiscoveryInspection()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        shiningRoot["pendingNativeFactionDiscovery"] = new JsonObject
        {
            ["requestId"] = "discover_native_1",
            ["createdAtTurn"] = 160,
            ["createdAtUtc"] = "2026-04-19T11:00:00Z",
            ["radianceTierAtRequest"] = 2,
            ["costFeathers"] = 25,
            ["costLightSparks"] = 20
        };
        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        _console.QueueSelection("Сияющая Обитель", "✨ Основные действия", "← Назад");
        _console.QueueSelection("Основные действия Сияющей Обители", "🔎 Осмотреть ожидающее открытие нативной фракции", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_pending_native_discovery_inspection");
        var renderedText = ExtractRenderedText();
        Assert.Contains("discover_native_1", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("25 Перьев", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("20 Искр Света", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_ActionsBlockNativeDiscoveryBelowRadianceTierOne()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        var soulRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/meta/soul_state.json") ?? "{}")!.AsObject();
        soulRoot["inkFeathers"] = new JsonObject { ["current"] = 50 };
        await WriteJsonAsync("game_state/meta/soul_state.json", soulRoot);
        var shiningRoot = JsonNode.Parse(await _fs.ReadFileAsync(ShiningAbodeState.StatePath) ?? "{}")!.AsObject();
        shiningRoot["radiance"] = new JsonObject
        {
            ["experience"] = 0,
            ["tier"] = 0
        };
        shiningRoot["lightSparks"] = 94;
        await WriteJsonAsync(ShiningAbodeState.StatePath, shiningRoot);
        _console.QueueSelection("Сияющая Обитель", "✨ Основные действия");
        _console.QueueSelection("Основные действия Сияющей Обители", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_actions_native_discovery_radiance_gate");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Основные действия Сияющей Обители", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Any(choice =>
                         choice.Contains("Запросить открытие нативной фракции", StringComparison.OrdinalIgnoreCase) &&
                         choice.Contains("нужен Radiance tier 1+", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task TryProcessCommand_ShiningAbode_ActionsBlockProjectSupportWhenGlobalCapFull()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        var shiningRoot = JsonNode.Parse(await _fs.ReadFileAsync(ShiningAbodeState.StatePath) ?? "{}")!.AsObject();
        var firstFaction = (shiningRoot["factions"] as JsonArray)?.OfType<JsonObject>().First()
            ?? throw new InvalidOperationException("Expected seeded Shining faction.");
        var projects = firstFaction["projects"] as JsonArray
            ?? throw new InvalidOperationException("Expected seeded projects.");
        projects.Add(new JsonObject
        {
            ["projectId"] = "project_unsup_cap_test",
            ["displayName"] = "Неподдержанный проект",
            ["summary"] = "Проверяет global support cap.",
            ["toneTags"] = new JsonArray("test"),
            ["targetFactionIds"] = new JsonArray(),
            ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeRevelation,
            ["outputEffectFamily"] = ShiningAbodeState.EffectFamilyLore,
            ["tier"] = 1,
            ["status"] = ShiningAbodeState.ProjectStatusCompleted,
            ["isSupported"] = false,
            ["strengthReward"] = 4
        });
        await WriteJsonAsync(ShiningAbodeState.StatePath, shiningRoot);
        _console.QueueSelection("Сияющая Обитель", "✨ Основные действия");
        _console.QueueSelection("Основные действия Сияющей Обители", "← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/shining_abode"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("shining_actions_support_cap_gate");
        Assert.Contains(_console.SelectionChoicesHistory,
            entry => entry.Title.Contains("Основные действия Сияющей Обители", StringComparison.OrdinalIgnoreCase) &&
                     entry.Choices.Any(choice =>
                         choice.Contains("Поддержать проект", StringComparison.OrdinalIgnoreCase) &&
                         choice.Contains("лимит поддерживаемых проектов исчерпан", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task TryProcessCommand_Help_ChaosSeaUsesPlayerFacingRussianWording()
    {
        await SeedSessionForCommandAsync("/душа");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/помощь"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("help_chaos_sea_wording");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Поздний ритуал основания собственного Хранителя после возвращения из Сияющей Обители", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/status", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/afterlife_inbox", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/guardian_projects", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Late-game", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("New Game+ reset", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Help_ShiningAbodeUsesPlayerFacingRussianWording()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/помощь"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("help_shining_wording");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Новый цикл: вернуться в Море Хаоса, сбросить Просветление, сохранить Перья и прогресс Обители", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/shining_treasury", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/source_of_light", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/источник_света", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/status", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("локальные Врата", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expected state delta", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Карта аудита Shining", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("full/canonical JSON", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("trade lifecycle", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("New Game+ reset", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SourceOfLight_BelowFullRadianceDoesNotCreatePendingRequest()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await _stateManager.RefreshGameStateAsync();

        var result = await _explorer.TryProcessCommand("/source_of_light");

        Assert.Equal("", result);
        AssertNoHiddenExplorerErrors("source_of_light_locked_below_radiance");
        Assert.False(_fs.FileExists(SourceOfLightCapstoneState.PendingRequestPath));
        var renderedText = ExtractRenderedText();
        Assert.Contains("Источник Света", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Capstone ещё закрыт", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("radiance.tier=4", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("radiance.experience>=580", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SourceOfLight_FullRadianceCreatesPendingRequestAndGmAction()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await SetShiningRadianceAsync(experience: 580, tier: 4);
        _fs.DeleteFile(ShiningTradeRequestState.PendingRequestsPath);
        _console.QueueAnyConfirmResponse(true);
        await _stateManager.RefreshGameStateAsync();

        var result = await _explorer.TryProcessCommand("/источник_света");

        Assert.NotNull(result);
        Assert.Contains("[SOURCE_OF_LIGHT_CAPSTONE:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(SourceOfLightCapstoneState.PendingRequestPath, result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(SourceOfLightCapstoneState.PassiveId, result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(SourceOfLightCapstoneState.RelicId, result, StringComparison.OrdinalIgnoreCase);
        AssertNoHiddenExplorerErrors("source_of_light_creates_pending");

        var pendingJson = await _fs.ReadFileAsync(SourceOfLightCapstoneState.PendingRequestPath);
        Assert.False(string.IsNullOrWhiteSpace(pendingJson));
        var pendingRoot = JsonNode.Parse(pendingJson!)!.AsObject();
        Assert.StartsWith("source_of_light_capstone:", pendingRoot["requestId"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(580, pendingRoot["radianceExperienceAtRequest"]!.GetValue<int>());
        Assert.Equal(4, pendingRoot["radianceTierAtRequest"]!.GetValue<int>());
        Assert.Equal(SourceOfLightCapstoneState.PassiveId, pendingRoot["rewardPassiveId"]!.GetValue<string>());
        Assert.Equal(SourceOfLightCapstoneState.RelicId, pendingRoot["rewardRelicId"]!.GetValue<string>());

        var renderedText = ExtractRenderedText();
        Assert.Contains("JSON pending_source_of_light_capstone.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Воплощение Света", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Воплощенный Свет", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SourceOfLight_BlocksWhileShiningCorePendingRequestExists()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await SetShiningRadianceAsync(experience: 580, tier: 4);
        await WriteJsonAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "core_blocks_source_light_001",
                    actionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                    quotedCostFeathers = 0,
                    quotedCostLightSparks = 0,
                    createdAtTurn = 159,
                    createdAtUtc = "2026-05-12T08:00:00Z"
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var result = await _explorer.TryProcessCommand("/source_of_light");

        Assert.Equal("", result);
        AssertNoHiddenExplorerErrors("source_of_light_blocks_core_pending");
        Assert.False(_fs.FileExists(SourceOfLightCapstoneState.PendingRequestPath));
        var renderedText = ExtractRenderedText();
        Assert.Contains("Источник Света заблокирован", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ShiningCoreActionRequestState.PendingActionsRequestPath, renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SourceOfLight_AllowsValidManifestationPendingRequest()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await SetShiningRadianceAsync(experience: 580, tier: 4);
        _fs.DeleteFile("input/turn_request.json");
        _fs.DeleteFile("game_state/control/pending_turn_snapshot.json");
        var snapshotDir = _fs.ResolvePath("game_state/control/pending_turn_snapshot");
        if (Directory.Exists(snapshotDir))
            Directory.Delete(snapshotDir, recursive: true);
        _fs.DeleteFile(ShiningCoreActionRequestState.PendingActionsRequestPath);
        _fs.DeleteFile(ShiningTradeRequestState.PendingRequestsPath);
        _fs.DeleteFile(ShiningFactionRequestState.PendingFoundingsRequestPath);
        _fs.DeleteFile(ShiningFactionRequestState.PendingRealignmentsRequestPath);
        _fs.DeleteFile(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath);
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "manifest_blocks_source_light_001",
                    relicId = "relic_companion_echo_001",
                    relicName = "Отзвук спутника",
                    manifestationSource = "imprint_relic",
                    targetIncarnation = 2,
                    companionNameHint = "Спутник"
                }
            }
        });
        _console.QueueAnyConfirmResponse(true);
        await _stateManager.RefreshGameStateAsync();

        var result = await _explorer.TryProcessCommand("/source_of_light");

        Assert.NotNull(result);
        Assert.Contains("[SOURCE_OF_LIGHT_CAPSTONE:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(SourceOfLightCapstoneState.PendingRequestPath, result, StringComparison.OrdinalIgnoreCase);
        AssertNoHiddenExplorerErrors("source_of_light_allows_valid_manifestation_pending");
        Assert.True(_fs.FileExists(SourceOfLightCapstoneState.PendingRequestPath));
        var renderedText = ExtractRenderedText();
        Assert.DoesNotContain("Источник Света заблокирован", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SourceOfLight_BlocksMalformedManifestationPendingRequest()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await SetShiningRadianceAsync(experience: 580, tier: 4);
        _fs.DeleteFile("input/turn_request.json");
        _fs.DeleteFile("game_state/control/pending_turn_snapshot.json");
        var snapshotDir = _fs.ResolvePath("game_state/control/pending_turn_snapshot");
        if (Directory.Exists(snapshotDir))
            Directory.Delete(snapshotDir, recursive: true);
        _fs.DeleteFile(ShiningCoreActionRequestState.PendingActionsRequestPath);
        _fs.DeleteFile(ShiningTradeRequestState.PendingRequestsPath);
        _fs.DeleteFile(ShiningFactionRequestState.PendingFoundingsRequestPath);
        _fs.DeleteFile(ShiningFactionRequestState.PendingRealignmentsRequestPath);
        _fs.DeleteFile(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath);
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, "{ malformed");
        await _stateManager.RefreshGameStateAsync();

        var result = await _explorer.TryProcessCommand("/source_of_light");

        Assert.Equal("", result);
        AssertNoHiddenExplorerErrors("source_of_light_blocks_malformed_manifestation_pending");
        Assert.False(_fs.FileExists(SourceOfLightCapstoneState.PendingRequestPath));
        var renderedText = ExtractRenderedText();
        Assert.Contains("Источник Света заблокирован", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTreasury_BlocksCostBearingCorePendingRequests()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await WriteJsonAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "core_cost_pending_001",
                    actionType = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
                    factionId = "faction_dawn",
                    quotedCostFeathers = 12,
                    quotedCostLightSparks = 0,
                    createdAtTurn = 159,
                    createdAtUtc = "2026-04-19T10:33:00Z"
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();
        var beforeSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var beforeShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);

        var result = await _explorer.TryProcessCommand("/shining_treasury");

        Assert.Equal("", result);
        AssertNoHiddenExplorerErrors("shining_treasury_blocks_core_cost_pending");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Казначейство заблокировано", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("core_cost_pending_001", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoulJson, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Equal(beforeShiningJson, await _fs.ReadFileAsync(ShiningAbodeState.StatePath));
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTreasury_BlocksNoncanonicalFoundingPendingCosts()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await WriteJsonAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "founding_noncanonical_cost_001",
                    proposedFactionId = "faction_noncanonical_cost",
                    proposedHallId = "hall_noncanonical_cost",
                    quotedCostFeathers = 0,
                    quotedCostLightSparks = 0,
                    createdAtTurn = 159,
                    createdAtUtc = "2026-04-19T10:34:00Z"
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();
        var beforeSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var beforeShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);

        var result = await _explorer.TryProcessCommand("/shining_treasury");

        Assert.Equal("", result);
        AssertNoHiddenExplorerErrors("shining_treasury_blocks_noncanonical_founding_costs");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Казначейство заблокировано", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("founding_noncanonical_cost_001", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("noncanonical", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ShiningFactionRequestState.FactionFoundingCostFeathers.ToString(), renderedText, StringComparison.Ordinal);
        Assert.Contains(ShiningFactionRequestState.FactionFoundingCostLightSparks.ToString(), renderedText, StringComparison.Ordinal);
        Assert.Equal(beforeSoulJson, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Equal(beforeShiningJson, await _fs.ReadFileAsync(ShiningAbodeState.StatePath));
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTreasury_BlocksActiveGmTurnLifecycle()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await WriteJsonAsync("input/turn_request.json", new
        {
            turnNumber = 159,
            playerAction = "Ordinary Shining turn still awaiting GM response"
        });
        await WriteJsonAsync("game_state/control/pending_turn_snapshot.json", new
        {
            sessionId = "session_shining_treasury_pending_001",
            turnNumber = 159
        });
        Directory.CreateDirectory(_fs.ResolvePath("game_state/control/pending_turn_snapshot"));
        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", "{}");
        await _stateManager.RefreshGameStateAsync();
        var beforeSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var beforeShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);

        var result = await _explorer.TryProcessCommand("/shining_treasury");

        Assert.Equal("", result);
        AssertNoHiddenExplorerErrors("shining_treasury_blocks_active_gm_turn");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Казначейство заблокировано", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("активный GM-turn", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("input/turn_request.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending_turn_snapshot", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoulJson, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Equal(beforeShiningJson, await _fs.ReadFileAsync(ShiningAbodeState.StatePath));
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTreasury_IgnoresEmptyStalePendingSnapshotDirectory()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        Directory.CreateDirectory(_fs.ResolvePath("game_state/control/pending_turn_snapshot"));
        await _stateManager.RefreshGameStateAsync();
        var beforeSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var beforeShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);

        var result = await _explorer.TryProcessCommand("/shining_treasury");

        Assert.Equal("", result);
        AssertNoHiddenExplorerErrors("shining_treasury_ignores_empty_snapshot_directory");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Казначейство Сияющей Обители", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("активный GM-turn", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoulJson, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Equal(beforeShiningJson, await _fs.ReadFileAsync(ShiningAbodeState.StatePath));
    }

    [Fact]
    public async Task SaveShiningTreasuryRoots_BlocksActiveGmTurnCreatedAfterPreview()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await _stateManager.RefreshGameStateAsync();
        var beforeSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var beforeShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var projectedShiningRoot = JsonNode.Parse(beforeShiningJson ?? "{}")!.AsObject();
        var projectedSoulRoot = JsonNode.Parse(beforeSoulJson ?? "{}")!.AsObject();
        ShiningAbodeState.EnsureTreasuryObject(projectedShiningRoot)["depositedInkFeathers"] = 999;
        projectedSoulRoot["inkFeathers"] = new JsonObject
        {
            ["current"] = 0,
            ["total"] = 0
        };
        await WriteJsonAsync("game_state/control/pending_turn_snapshot.json", new
        {
            sessionId = "session_shining_treasury_race_001",
            turnNumber = 160
        });

        var method = typeof(ExplorerMode).GetMethod(
            "SaveShiningTreasuryRootsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var task = Assert.IsAssignableFrom<Task<bool>>(method!.Invoke(_explorer, new object?[]
        {
            projectedShiningRoot,
            projectedSoulRoot,
            null,
            null
        }));
        var saved = await task;

        Assert.False(saved);
        Assert.Contains("активный GM-turn", ExtractRenderedText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoulJson, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Equal(beforeShiningJson, await _fs.ReadFileAsync(ShiningAbodeState.StatePath));
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTreasury_BlocksLegacyNativeDiscoveryPending()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        var shiningRoot = JsonNode.Parse(await _fs.ReadFileAsync(ShiningAbodeState.StatePath) ?? "{}")!.AsObject();
        shiningRoot["pendingNativeFactionDiscovery"] = new JsonObject
        {
            ["requestId"] = "legacy_discovery_pending_001",
            ["costFeathers"] = 25,
            ["costLightSparks"] = 20
        };
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _stateManager.RefreshGameStateAsync();
        var beforeSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var beforeShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);

        var result = await _explorer.TryProcessCommand("/shining_treasury");

        Assert.Equal("", result);
        AssertNoHiddenExplorerErrors("shining_treasury_blocks_legacy_native_discovery");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Казначейство заблокировано", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pendingNativeFactionDiscovery", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("legacy_discovery_pending_001", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoulJson, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Equal(beforeShiningJson, await _fs.ReadFileAsync(ShiningAbodeState.StatePath));
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTreasury_BlocksMalformedFoundingPendingFile()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await _fs.WriteFileAtomicAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, "{");
        await _stateManager.RefreshGameStateAsync();
        var beforeSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var beforeShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);

        var result = await _explorer.TryProcessCommand("/shining_treasury");

        Assert.Equal("", result);
        AssertNoHiddenExplorerErrors("shining_treasury_blocks_malformed_founding_pending");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Казначейство заблокировано", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending_shining_faction_foundings.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoulJson, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Equal(beforeShiningJson, await _fs.ReadFileAsync(ShiningAbodeState.StatePath));
    }

    [Fact]
    public async Task TryProcessCommand_ShiningTreasury_BlocksMalformedTreasuryWithoutNormalizingIt()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        var shiningRoot = JsonNode.Parse(await _fs.ReadFileAsync(ShiningAbodeState.StatePath) ?? "{}")!.AsObject();
        shiningRoot[ShiningAbodeState.TreasuryProperty] = "malformed_treasury";
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _stateManager.RefreshGameStateAsync();
        var beforeShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);

        var result = await _explorer.TryProcessCommand("/shining_treasury");

        Assert.Equal("", result);
        AssertNoHiddenExplorerErrors("shining_treasury_blocks_malformed_treasury");
        Assert.Contains(_console.MarkupLines, line => line.Contains("treasury", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(_console.MarkupLines, line => line.Contains("повреж", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(beforeShiningJson, await _fs.ReadFileAsync(ShiningAbodeState.StatePath));
    }

    [Fact]
    public async Task TryProcessCommand_Help_PendingBootstrapDoesNotAdvertiseMortalOnlyStatus()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: true);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/помощь"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("help_pending_bootstrap_read_only_audits");
        var renderedText = ExtractRenderedText();
        Assert.Contains("SHINING ABODE HANDOFF", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/status", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/статус", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/shining_abode", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/shining_politics", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SpiritualArts_UsesCanonicalShiningRadianceFields()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/spiritual_arts"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_spiritual_arts_shining_radiance");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Духовные искусства посмертия", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ранг Просветления", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Shining radiance", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сияние Сияющей Обители", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("540 XP", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tier 3", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Shining radiance value: 0", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SpiritualArts_UpgradesArtAndSpendsInkFeathers()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 500, total = 500 },
            enlightenment = new { currentTier = "Illuminated", experience = 100, level = 5 },
            soulProgression = new { totalExperience = 100, tier = 5, progressPercent = 100 }
        });
        await _stateManager.RefreshGameStateAsync();
        _console.QueueAnySelection("⬆ Прокачать духовное искусство");
        _console.QueueAnyConfirmResponse(true);

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/spiritual_arts"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("spiritual_arts_upgrade_ink_feathers");
        var soulRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/meta/soul_state.json") ?? "{}")!.AsObject();
        var profile = Assert.IsType<JsonObject>(soulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty]);
        var artTiers = Assert.IsType<JsonObject>(profile["artTiers"]);
        Assert.Equal(1, artTiers["pressure"]?.GetValue<int>());
        Assert.Equal(5, profile["enlightenmentRank"]?.GetValue<int>());
        var inkFeathers = Assert.IsType<JsonObject>(soulRoot["inkFeathers"]);
        Assert.Equal(375, inkFeathers["current"]?.GetValue<int>());
        Assert.Equal(500, inkFeathers["total"]?.GetValue<int>());
    }

    [Fact]
    public async Task TryProcessCommand_SpiritualArts_AllowsFirstUpgradeForHigherUnlockArt()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 500, total = 500 },
            enlightenment = new { currentTier = "Tempered", experience = 45, level = 3 },
            soulProgression = new { totalExperience = 45, tier = 3, progressPercent = 45 }
        });
        await _stateManager.RefreshGameStateAsync();
        _console.QueueAnySelection("⬆ Прокачать духовное искусство");
        _console.QueueSelection("Выберите духовное искусство", "Разрыв оков [break_binding; Break Binding] — уровень 0->1, 150 🪶");
        _console.QueueAnyConfirmResponse(true);

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/spiritual_arts"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("spiritual_arts_upgrade_higher_unlock_art");
        var soulRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/meta/soul_state.json") ?? "{}")!.AsObject();
        var profile = Assert.IsType<JsonObject>(soulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty]);
        var artTiers = Assert.IsType<JsonObject>(profile["artTiers"]);
        Assert.Equal(1, artTiers["break_binding"]?.GetValue<int>());
        var inkFeathers = Assert.IsType<JsonObject>(soulRoot["inkFeathers"]);
        Assert.Equal(350, inkFeathers["current"]?.GetValue<int>());
    }

    [Fact]
    public async Task TryProcessCommand_SpiritualArts_BlocksUpgradeAboveRankGate()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 500, total = 500 },
            enlightenment = new { currentTier = "Новичок", experience = 0, level = 0 },
            soulProgression = new { totalExperience = 0, tier = 0, progressPercent = 0 }
        });
        var beforeSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        await _stateManager.RefreshGameStateAsync();
        _console.QueueAnySelection("⬆ Прокачать духовное искусство");

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/spiritual_arts"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("spiritual_arts_blocks_rank_gate");
        Assert.Equal(beforeSoulJson, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        var renderedText = ExtractRenderedText();
        Assert.Contains("нужен ранг", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SpiritualArts_BlocksUpgradeDuringActiveConflict()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 500, total = 500 },
            enlightenment = new { currentTier = "Illuminated", experience = 100, level = 5 },
            soulProgression = new { totalExperience = 100, tier = 5, progressPercent = 100 }
        });
        await WriteJsonAsync(AfterlifeSpiritualConflictState.StatePath, new
        {
            schemaVersion = 1,
            activeConflict = new
            {
                conflictId = "afterlife_conflict_active_upgrade_block",
                realm = "Chaos Sea",
                sideModel = "direct_duel",
                playerSide = new { leadContestant = new { actorType = "player", actorId = "player", displayName = "Игрок" }, supporters = Array.Empty<object>() },
                oppositionSide = new { leadContestant = new { actorType = "guardian", actorId = "guardian_liora", displayName = "Лиора", actorArtTierSnapshot = new { pressure = 1 }, artAuthoritySource = "guardian_state" }, supporters = Array.Empty<object>() },
                playerSideStrain = "clear",
                oppositionSideStrain = "clear",
                conflictPosition = "contested",
                resolutionState = "active",
                exchangeLog = Array.Empty<object>()
            },
            recentConflicts = Array.Empty<object>()
        });
        var beforeSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        await _stateManager.RefreshGameStateAsync();
        _console.QueueAnySelection("⬆ Прокачать духовное искусство");

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/spiritual_arts"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("spiritual_arts_blocks_active_conflict");
        Assert.Equal(beforeSoulJson, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        var renderedText = ExtractRenderedText();
        Assert.Contains("активен духовный конфликт посмертия", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SpiritualArts_BlocksUpgradeWhenInkFeathersAreInsufficient()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 10, total = 10 },
            enlightenment = new { currentTier = "Illuminated", experience = 100, level = 5 },
            soulProgression = new { totalExperience = 100, tier = 5, progressPercent = 100 }
        });
        var beforeSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        await _stateManager.RefreshGameStateAsync();
        _console.QueueAnySelection("⬆ Прокачать духовное искусство");

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/spiritual_arts"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("spiritual_arts_blocks_insufficient_ink_feathers");
        Assert.Equal(beforeSoulJson, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        var renderedText = ExtractRenderedText();
        Assert.Contains("Недостаточно Чернильных Перьев", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_console.ConfirmPrompts);
    }

    [Fact]
    public async Task TryProcessCommand_SpiritualArts_BlocksUpgradeDuringActiveGmTurnLifecycle()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 500, total = 500 },
            enlightenment = new { currentTier = "Illuminated", experience = 100, level = 5 },
            soulProgression = new { totalExperience = 100, tier = 5, progressPercent = 100 }
        });
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "session_spiritual_arts_pending",
            requestId = "request_spiritual_arts_pending",
            turnNumber = 7,
            playerAction = "pending turn"
        });
        var beforeSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        await _stateManager.RefreshGameStateAsync();
        _console.QueueAnySelection("⬆ Прокачать духовное искусство");

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/spiritual_arts"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("spiritual_arts_blocks_active_gm_turn");
        Assert.Equal(beforeSoulJson, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        var renderedText = ExtractRenderedText();
        Assert.Contains("активный GM-turn lifecycle", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("input/turn_request.json", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SpiritualConflict_ShowsOrdinaryProseRoutingRule()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/spiritual_conflict"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("spiritual_conflict_prose_routing_rule");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Духовный конфликт посмертия", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("afterlife spiritual conflict", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Обычная художественная заявка", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("действие конфликта", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_SpiritualCombatHelp_ExplainsTacticsPositionAndFairCriticals()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/spiritual_combat_help"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("spiritual_combat_help_tactics_position_critical_fairness");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Справка по духовному бою", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Давление", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Контрприём", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Позиция конфликта", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("binding/force_binding", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("симметрично", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("не создаёт decisive", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/spiritual_arts", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Status_ChaosSeaShowsNumericMemoryLegacyBonus()
    {
        await SeedGuardianTradeStateAsync(includeTradeInventory: false);
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 500 },
            soulRelics = new
            {
                equipped = Array.Empty<object>(),
                stored = Array.Empty<object>()
            },
            pendingMemoryLegacy = new
            {
                legacyId = "legacy_status_bonus_001",
                legacyType = "startingCharacteristicBonus",
                sourceLifeHint = "Эхо жизни мудреца.",
                grantSource = "memoryLegacyGrant",
                applicationState = "pending",
                characteristic = "intelligence",
                bonus = 2,
                grantSnapshot = new
                {
                    legacyId = "legacy_status_bonus_001",
                    legacyType = "startingCharacteristicBonus",
                    sourceLifeHint = "Эхо жизни мудреца.",
                    characteristic = "intelligence",
                    bonus = 2
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/status"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_status_numeric_memory_bonus");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Next-life payloads", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("legacy_status_bonus_001", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("startingCharacteristicBonus", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("characteristic: intelligence", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bonus: 2", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Status_UnwrapsCanonicalProgressionReport()
    {
        await SeedGuardianTradeStateAsync(includeTradeInventory: false);
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 500 }
        });
        await WriteJsonAsync(ProgressionScheduleService.ReportPath, new
        {
            progressionProcessingReport = new
            {
                sessionId = "session_status_progression_001",
                requestId = "request_status_progression_001",
                turnNumber = 22,
                chaosSeaCyclesProcessed = 2,
                guardianProjectCyclesProcessed = 1,
                residentAgencyCyclesProcessed = 1,
                shiningAbodeCyclesProcessed = 0,
                shiningFactionCyclesProcessed = 0,
                shiningTradeCyclesProcessed = 0,
                newLastChaosSeaSimulationOrdinal = 42,
                newLastGuardianProjectCycleOrdinal = 17,
                newLastResidentAgencyCycleOrdinal = 9,
                afterlifeCatchupProcessed = true,
                afterlifeCatchupSummaryEventsProcessed = 3
            },
            _lastUpdated = "2026-04-21T00:00:00Z"
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/status"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_status_progression_report_unwrap");
        var renderedText = ExtractRenderedText();
        Assert.Contains("progression_report.progressionProcessingReport", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chaosSeaCyclesProcessed=2", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("newLastChaosSeaSimulationOrdinal=42", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sessionId=session_status_progression_001", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("progression_report: no compact fields", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("_lastUpdated", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Status_ShowsRepairOnlyShiningStateWithMalformedLegacyDiscovery()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))!.AsObject();
        shiningRoot["pendingNativeFactionDiscovery"] = "malformed_contract";
        await File.WriteAllTextAsync(shiningStatePath, shiningRoot.ToJsonString());
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/status"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_status_shining_malformed_legacy_discovery");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Сияющая Обитель", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Repair-only blocker", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pendingNativeFactionDiscovery", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("повреж", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shining_abode_state.json пока отсутствует или повреждён", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Status_ShowsRawMalformedSpiritualConflictState()
    {
        const string rawConflict = "{ malformed spiritual conflict payload";
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 500 }
        });
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, rawConflict);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/status"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_status_malformed_spiritual_conflict_raw");
        var renderedText = ExtractRenderedText();
        Assert.Contains(AfterlifeSpiritualConflictState.StatePath, renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Raw malformed", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(rawConflict, ExtractRenderedLiteralText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Status_SkipsEmptyNpcPendingQueues()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 500 }
        });
        await WriteJsonAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath, new { requests = Array.Empty<object>() });
        await WriteJsonAsync(NpcTradeRequestState.PendingRequestPath, new { requests = Array.Empty<object>() });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/status"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_status_empty_npc_queues");
        var renderedText = ExtractRenderedText();
        Assert.DoesNotContain("pending_npc_social_interactions.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pending_npc_trade_inventory_requests.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NPC social request из смертного мира", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NPC trade request из смертного мира", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Status_ShowsNonEmptyNpcPendingQueues()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 500 }
        });
        await WriteJsonAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "npc_social_status_001",
                    npcId = "npc_merchant_001",
                    npcName = "Марек",
                    interactionType = "talk"
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/status"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_status_nonempty_npc_queue");
        var renderedText = ExtractRenderedText();
        Assert.Contains("pending_npc_social_interactions.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("npc_social_status_001", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wrong-realm repair-only", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Status_ChaosSeaShowsFullPendingContractAudit()
    {
        await SeedGuardianTradeStateAsync(includeTradeInventory: false);
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 500 },
            soulRelics = new
            {
                equipped = Array.Empty<object>(),
                stored = Array.Empty<object>()
            },
            pendingMemoryLegacy = new
            {
                legacyId = "legacy_status_001",
                legacyType = "startingPassiveKnowledgeSkill",
                grantSource = "memoryLegacyGrant",
                applicationState = "pending",
                skillName = "Echo Cartography",
                skillDescription = "Reads routes through afterlife echoes.",
                group = "Knowledge",
                playerStatBonus = "+1 to afterlife route knowledge checks",
                grantSnapshot = new
                {
                    requestId = "memory_gates_status_001",
                    costInFeathers = 24,
                    legacyType = "startingPassiveKnowledgeSkill",
                    skillName = "Echo Cartography",
                    skillDescription = "Reads routes through afterlife echoes.",
                    group = "Knowledge",
                    playerStatBonus = "+1 to afterlife route knowledge checks",
                    structuredBonuses = new[]
                    {
                        new
                        {
                            bonusType = "knowledge_check",
                            target = "afterlife_routes",
                            value = 1
                        }
                    }
                },
                structuredBonuses = new[]
                {
                    new
                    {
                        bonusType = "knowledge_check",
                        target = "afterlife_routes",
                        value = 1
                    }
                }
            },
            soulImprint = new
            {
                imprintId = "imprint_status_001",
                sourceCompanionId = "companion_status_001",
                companionName = "Лиора",
                summary = "Будущий спутник помнит берег Моря Хаоса.",
                futureCompanionPrompt = "Лиора должна вернуться как проводник через туман.",
                coreTraits = new[] { "верность", "память" },
                relationshipMarkers = new[] { "saved_at_sea" },
                sourceProvenance = new
                {
                    sourceRealm = "Chaos Sea",
                    sourceTurn = 12
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeOfferingState.PendingRequestPath, new
        {
            requestId = "offering_status_001",
            guardianId = "guardian_trade_001",
            guardianName = "Азалия",
            abodeId = "abode_trade_001",
            abodeName = "Торговая Обитель",
            offeringType = "ink_feathers",
            inkFeathersOffered = 100,
            powerGain = 20,
            createdAtTurn = 12,
            createdAtUtc = "2026-04-21T00:00:00Z"
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/status"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_status_chaos_pending");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Полный статус загробного цикла", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending_abode_offering.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("offering_status_001", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("full payload", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/afterlife_inbox", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Next-life payloads", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("legacy_status_001", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Echo Cartography", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("playerStatBonus", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("+1 to afterlife route knowledge checks", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("imprint_status_001", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("futureCompanionPrompt", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sourceProvenance", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Status_ShiningAbodeShowsRadianceGatesAndTradeSignals()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/статус"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_status_shining");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Сияющая Обитель", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Radiance", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Light Sparks", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("draftVersion", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Selected blessing cards", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тропа возвращения", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("effect:", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending_shining_trade_inventory_requests.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shining_trade_memory_7", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Status_ShiningSelectedCardsUsePackageFallback()
    {
        await SeedShiningInspectionStateAsync(includePreparedPackage: true);
        var shiningStatePath = _fs.ResolvePath(ShiningAbodeState.StatePath);
        var shiningRoot = JsonNode.Parse(await File.ReadAllTextAsync(shiningStatePath))?.AsObject()
            ?? throw new InvalidOperationException("Expected seeded shining abode state.");
        var availableCards = shiningRoot["gates"]?["availableBlessingCards"]?.AsArray()
            ?? throw new InvalidOperationException("Expected available blessing cards.");
        for (var index = availableCards.Count - 1; index >= 0; index--)
        {
            if (string.Equals(availableCards[index]?["cardId"]?.GetValue<string>(), "card_route_dawn", StringComparison.OrdinalIgnoreCase))
                availableCards.RemoveAt(index);
        }

        await File.WriteAllTextAsync(
            shiningStatePath,
            shiningRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/статус"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("afterlife_status_shining_package_card_fallback");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Prepared package selected cards", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Тропа возвращения", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Открывает путь через память", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("frozen card snapshot unavailable", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_ChaosSeaOverviewEnumeratesFullPendingContracts()
    {
        await SeedGuardianTradeStateAsync(includeTradeInventory: false);
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "resident_talk_pending_001",
                    guardianId = "guardian_trade_001",
                    guardianName = "Азалия",
                    abodeId = "abode_trade_001",
                    residentId = "resident_liora_001",
                    residentName = "Лиора",
                    interactionType = "talk",
                    createdAtTurn = 12,
                    createdAtUtc = "2026-04-21T00:02:00Z"
                }
            }
        });
        _console.QueueAnySelection("← Назад");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/chaos_sea"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("chaos_sea_pending_contract_overview");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Chaos Sea Audit", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending_guardian_abode_resident_interactions.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resident_talk_pending_001", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("residentInteractionLogUpdates", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("full payload", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_AbodeResidentsMissing_CreatesRosterRequestAndReturnsGmAction()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_resident_001",
                    canonicalName = "Азалия",
                    domain = "Social",
                    abode = new
                    {
                        abodeId = "abode_social_azalia_001",
                        name = "Обитель Неутолимого Пламени",
                        description = "Тёплый тестовый свет.",
                        atmosphere = "Welcoming"
                    },
                    nameVariants = new
                    {
                        @default = "Азалия",
                        feminine = "Азалия",
                        masculine = (string?)null,
                        neutral = (string?)null
                    },
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Тестовая форма Хранителя."
                    },
                    manifestationHistory = Array.Empty<object>(),
                    personalityProfile = new
                    {
                        archetype = "Diplomat",
                        speechPattern = "Measured",
                        coreValues = new[] { "страсть", "связь" }
                    },
                    relationshipData = new
                    {
                        currentReputation = 130,
                        reputationHistory = Array.Empty<object>(),
                        lastInteraction = "2026-03-27T00:00:00Z"
                    },
                    questManagement = new
                    {
                        availableQuests = Array.Empty<object>(),
                        activeQuests = Array.Empty<object>(),
                        completedQuests = Array.Empty<object>()
                    },
                    gachaSystem = new
                    {
                        chargesPerReturn = 2,
                        chargesUsedThisReturn = 0,
                        gachaHistory = Array.Empty<object>()
                    },
                    abodePower = new
                    {
                        currentPower = 58,
                        tier = "Могущественная",
                        history = Array.Empty<object>()
                    },
                    loreFragments = Array.Empty<object>()
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_resident_001"
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_social_azalia_001"
            }
        });
        _console.QueueSelection("Действие", "🏛 Обитатели Обители");
        await _stateManager.RefreshGameStateAsync();

        await _explorer.TryProcessCommand("/хранители");

        AssertNoHiddenExplorerErrors("guardian_abode_residents_request");

        var pendingRaw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("\"requests\": [", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("guardian_resident_001", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("abode_social_azalia_001", pendingRaw, StringComparison.Ordinal);
        var renderedText = ExtractRenderedText();
        Assert.Contains("Полный предпросмотр запроса резидентов Обители", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending_guardian_abode_residents_request.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requestMode", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("guardian_abode_residents.json roster", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Realm rule matrix Моря Хаоса", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mortal inventory/money/XP/skills", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("progression_report.json", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_ResidentTransfer_ShowsFullContractPreviewBeforeWritingRequest()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("input/turn_request.json", new { turnNumber = 22 });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 25 }
        });
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_source_001",
                    canonicalName = "Азалия",
                    domain = "Social",
                    manifestation = new
                    {
                        currentDisplayName = "Азалия"
                    },
                    relationshipData = new { currentReputation = 120 },
                    abodePower = new
                    {
                        currentPower = 12,
                        tier = "Хрупкая",
                        history = Array.Empty<object>()
                    },
                    abode = new
                    {
                        abodeId = "abode_source_001",
                        name = "Лазурная Обитель",
                        isDiscovered = true
                    }
                },
                new
                {
                    guardianId = "guardian_target_001",
                    canonicalName = "Мириэль",
                    domain = "Magic",
                    manifestation = new
                    {
                        currentDisplayName = "Мириэль"
                    },
                    relationshipData = new { currentReputation = 90 },
                    abodePower = new
                    {
                        currentPower = 82,
                        tier = "Сияющая",
                        history = Array.Empty<object>()
                    },
                    abode = new
                    {
                        abodeId = "abode_target_001",
                        name = "Сад Перекрёстков",
                        isDiscovered = true
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_source_001",
                canonicalName = "Азалия",
                manifestation = new
                {
                    currentDisplayName = "Азалия"
                },
                abode = new
                {
                    abodeId = "abode_source_001",
                    name = "Лазурная Обитель"
                }
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_source_001",
                discoveredAbodes = new[] { "abode_source_001", "abode_target_001" }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_ember_001",
                    guardianId = "guardian_source_001",
                    abodeId = "abode_source_001",
                    displayName = "Лиора",
                    residentKind = "ascended_soul",
                    originType = "mortal_echo",
                    roleLabel = "Певчая",
                    summary = "Берегла клятвы на мостах памяти.",
                    bondLevel = 72,
                    bondTier = "trusted",
                    abodeDevotionLevel = 18,
                    abodeDevotionTier = "uncertain",
                    restlessness = 94,
                    migrationState = "ready_to_transfer",
                    historyRevealed = true,
                    bondRewardState = "none",
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Вернётся как тихий проводник через лазурные мосты.",
                        bondReason = "Она сохранила песнь клятвы.",
                        coreTraits = new[] { "верная" },
                        archetypeHints = new[] { "проводник" },
                        appearanceMotifs = new[] { "лазурная нить" }
                    },
                    personalityProfile = new
                    {
                        archetype = "Witness",
                        worldview = "Hopeful",
                        culturalLayer = "Glass Gardens",
                        coreValues = new[] { "память" },
                        personalityTraits = Array.Empty<object>()
                    },
                    abodeDisposition = new
                    {
                        powerSensitivity = "high",
                        migrationDisposition = "seeks_stronger_abode",
                        communalOrientation = "medium",
                        stabilityNeed = "high"
                    }
                }
            },
            thoughtJournal = Array.Empty<object>(),
            interactionLog = Array.Empty<object>(),
            historyLog = Array.Empty<object>(),
            transferReceipts = Array.Empty<object>(),
            interactionReceipts = Array.Empty<object>(),
            rosterReceipts = Array.Empty<object>()
        });
        _console.QueueSelection("Действие", "🏛 Обитатели Обители", "🚪 Разрешить переход в другую Обитель");
        await _stateManager.RefreshGameStateAsync();

        string? gmAction = null;
        var ex = await Record.ExceptionAsync(async () => gmAction = await _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_resident_transfer_preview");
        Assert.Contains("ABODE_RESIDENT_TRANSFER_REQUEST", gmAction ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("resident_ember_001", gmAction ?? string.Empty, StringComparison.Ordinal);
        var pendingRaw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("\"residentId\": \"resident_ember_001\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"targetGuardianId\": \"guardian_target_001\"", pendingRaw, StringComparison.Ordinal);
        var renderedText = ExtractRenderedText();
        Assert.Contains("Полный предпросмотр перехода резидента", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending_guardian_abode_resident_transfers.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UpdateGuardianAbodeResidentTransferReceipts", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resident_ember_001", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_AbodeResidentsExistingPendingShowsFullRosterContract()
    {
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_roster_pending_001",
                    canonicalName = "Азалия",
                    domain = "Social",
                    manifestation = new { currentDisplayName = "Азалия" },
                    relationshipData = new { currentReputation = 130 },
                    abodePower = new { currentPower = 58, tier = "Могущественная", history = Array.Empty<object>() },
                    abode = new
                    {
                        abodeId = "abode_roster_pending_001",
                        name = "Обитель Неутолимого Пламени",
                        isDiscovered = true
                    }
                }
            },
            activeGuardian = new
            {
                guardianId = "guardian_roster_pending_001"
            },
            chaosSeaNavigation = new
            {
                currentAbodeId = "abode_roster_pending_001"
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "roster_pending_existing_001",
                    guardianId = "guardian_roster_pending_001",
                    guardianName = "Азалия",
                    abodeId = "abode_roster_pending_001",
                    abodeName = "Обитель Неутолимого Пламени",
                    currentReputation = 130,
                    requestMode = "standard_roster",
                    createdAtTurn = 41,
                    createdAtUtc = "2026-04-21T00:03:00Z"
                }
            }
        });
        _console.QueueSelection("Действие", "🏛 Обитатели Обители");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_abode_residents_existing_pending");
        var renderedText = ExtractRenderedText();
        Assert.Contains("Живой pending contract", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pending_guardian_abode_residents_request.json", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("roster_pending_existing_001", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("full payload", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UpdateGuardianAbodeResidentRosterReceipts", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_Guardians_TalkAction_CreatesPendingGuardianSocialRequest()
    {
        await SeedGuardianTradeStateAsync(includeTradeInventory: false);
        _console.QueueSelection("Действие", "💬 Поговорить");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/хранители"));

        Assert.Null(ex);
        AssertNoHiddenExplorerErrors("guardian_social_talk_request");
        var pendingRaw = await _fs.ReadFileAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("\"guardianId\": \"guardian_trade_001\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"interactionType\": \"talk\"", pendingRaw, StringComparison.Ordinal);
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

    [Fact]
    public async Task TryProcessCommand_InkFeathers_UnresolvedRealm_DoesNotMutateSoulOrDice()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "",
            currentIncarnation = 1,
            inkFeathers = new { current = 50 }
        });
        var before = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        _console.QueueAnySelection("🔮 Открыть Судьбу (−5 🪶)");
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/перья"));

        Assert.Null(ex);
        Assert.Equal(before, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.False(File.Exists(_fs.ResolvePath(PendingTurnStateService.PendingDiceStatePath)));
        Assert.Empty(_console.SelectionChoicesHistory);
        Assert.Contains(_console.MarkupLines, line => line.Contains("currentRealm не определён", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_SoulRelics_UnresolvedRealm_DoesNotNormalizeLegacyFlatRelics()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new JsonObject
        {
            ["soulName"] = "Тестовая Душа",
            ["currentRealm"] = "",
            ["currentIncarnation"] = 1,
            ["inkFeathers"] = new JsonObject { ["current"] = 3 },
            ["soulRelics"] = new JsonArray
            {
                new JsonObject
                {
                    ["relicId"] = "relic_flat_001",
                    ["name"] = "Плоская Реликвия",
                    ["gameplayStatus"] = new JsonObject { ["equipped"] = false }
                }
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/реликвии"));

        Assert.Null(ex);
        var soulRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/meta/soul_state.json")!)!.AsObject();
        Assert.IsType<JsonArray>(soulRoot["soulRelics"]);
        Assert.Contains(_console.MarkupLines, line => line.Contains("currentRealm не определён", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryProcessCommand_InkFeathers_InvalidShiningPackage_BlocksOrdinaryAfterlifeActions()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 3,
            inkFeathers = new { current = 50 }
        });
        await WriteJsonAsync(ShiningAbodeState.StatePath, new JsonObject
        {
            ["availability"] = ShiningAbodeState.AvailabilityActive,
            ["preparedIncarnationPackage"] = new JsonObject
            {
                ["selectedCardIds"] = new JsonArray()
            }
        });
        await _stateManager.RefreshGameStateAsync();

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/перья"));

        Assert.Null(ex);
        Assert.Empty(_console.SelectionChoicesHistory);
        var renderedText = ExtractRenderedText();
        Assert.Contains("package fault", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SeedAbodeOfferingRelicAliasStateAsync(string rarityField, string rarity)
    {
        var relic = new JsonObject
        {
            ["relicId"] = "relic_alias_001",
            ["name"] = "Реликвия с алиасом редкости",
            ["description"] = "Реликвия для проверки destructive offering rarity alias.",
            ["formTag"] = "alias_test",
            ["properties"] = new JsonArray()
        };
        relic[rarityField] = rarity;

        await WriteJsonAsync("game_state/meta/soul_state.json", new JsonObject
        {
            ["soulName"] = "Тестовая Душа",
            ["currentRealm"] = "Chaos Sea",
            ["currentIncarnation"] = 4,
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = 3,
                ["total"] = 3
            },
            ["soulRelics"] = new JsonObject
            {
                ["stored"] = new JsonArray(relic),
                ["equipped"] = new JsonArray()
            }
        });

        var guardian = new JsonObject
        {
            ["guardianId"] = "guard_abode_alias_001",
            ["canonicalName"] = "Азалия",
            ["domain"] = "Social",
            ["manifestation"] = new JsonObject
            {
                ["currentDisplayName"] = "Азалия",
                ["formFlexibility"] = "selective",
                ["currentPresentationStyle"] = "feminine",
                ["currentPronouns"] = "она/её",
                ["appearanceDescription"] = "Тестовая форма хранительницы."
            },
            ["abodePower"] = new JsonObject
            {
                ["currentPower"] = 35,
                ["tier"] = "Хрупкая",
                ["lastUpdatedAt"] = "2026-03-23T00:00:00Z",
                ["history"] = new JsonArray()
            },
            ["relationshipData"] = new JsonObject
            {
                ["currentReputation"] = 25,
                ["reputationHistory"] = new JsonArray()
            }
        };

        await WriteJsonAsync("game_state/meta/guardians.json", new JsonObject
        {
            ["guardians"] = new JsonArray(guardian),
            ["chaosSeaNavigation"] = new JsonObject()
        });
        await WriteJsonAsync(GuardianPowerEventState.JournalPath, new JsonObject
        {
            ["entries"] = new JsonArray()
        });
    }


}
