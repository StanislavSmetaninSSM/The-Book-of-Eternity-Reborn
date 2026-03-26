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
        var pendingRequestRaw = await _fs.ReadFileAsync("game_state/control/pending_guardian_trade_request.json");
        Assert.Contains("\"guardianId\": \"guardian_trade_001\"", pendingRequestRaw ?? string.Empty, StringComparison.Ordinal);
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
        await SeedGuardianTradeStateAsync();
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


}
