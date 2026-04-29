using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

/// <summary>
/// Explorer mode public API and command orchestration.
/// </summary>
public partial class ExplorerMode
{
    public ExplorerMode(StateManager stateManager, FileSystemManager fs, LocalizationManager loc,
        ValidationService? validator = null, CharacteristicsService? charService = null,
        StoryService? storyService = null, ImageService? imageService = null,
        PendingTurnStateService? pendingTurnState = null,
        GuardianTradeService? guardianTradeService = null,
        NpcTradeService? npcTradeService = null,
        SystemModService? systemModService = null,
        SystemGuardianLibraryService? systemGuardianLibraryService = null,
        WorldDirectiveService? worldDirectiveService = null,
        ScenarioCoreService? scenarioCoreService = null,
        AfterlifeArchiveCandidateService? afterlifeArchiveCandidateService = null,
        AfterlifeArchiveConsultationService? afterlifeArchiveConsultationService = null,
        AfterlifeArchiveProjectFuelService? afterlifeArchiveProjectFuelService = null,
        GuardianCorrectionService? guardianCorrectionService = null,
        SoulIdentityService? soulIdentityService = null,
        IClipboardService? clipboardService = null,
        IExplorerConsole? console = null)
    {
        _console = console ?? new SpectreExplorerConsole(clipboardService);
        _stateManager = stateManager;
        _validator = validator;
        _charService = charService;
        _storyService = storyService;
        _imageService = imageService;
        _pendingTurnState = pendingTurnState;
        _guardianTradeService = guardianTradeService;
        _npcTradeService = npcTradeService;
        _systemModService = systemModService;
        _systemGuardianLibraryService = systemGuardianLibraryService;
        _worldDirectiveService = worldDirectiveService;
        _scenarioCoreService = scenarioCoreService;
        _afterlifeArchiveCandidateService = afterlifeArchiveCandidateService;
        _afterlifeArchiveConsultationService = afterlifeArchiveConsultationService;
        _afterlifeArchiveProjectFuelService = afterlifeArchiveProjectFuelService;
        _guardianCorrectionService = guardianCorrectionService;
        _soulIdentityService = soulIdentityService;
        _clipboardService = clipboardService;
        _fs = fs;
        _loc = loc;

        _universalCommands = new Dictionary<string, Func<Task>>(StringComparer.OrdinalIgnoreCase)
        {
            ["/help"] = ShowHelp,
            ["/помощь"] = ShowHelp,
            ["/soul"] = ShowSoulInfo,
            ["/душа"] = ShowSoulInfo,
            ["/soul_relics"] = ShowSoulRelics,
            ["/реликвии"] = ShowSoulRelics,
            ["/afterlife_archive"] = ShowAfterlifeArchive,
            ["/архив_души"] = ShowAfterlifeArchive,
            ["/archive_candidates"] = ShowAfterlifeArchiveCandidates,
            ["/архив_кандидаты"] = ShowAfterlifeArchiveCandidates,
            ["/soul_quests"] = ShowSoulQuests,
            ["/квесты_души"] = ShowSoulQuests,
            ["/gm"] = ShowGmThoughts,
            ["/гм"] = ShowGmThoughts,
            ["/debug"] = ShowDebugInfo,
            ["/отладка"] = ShowDebugInfo,
            ["/codex"] = ShowLoreCodex,
            ["/кодекс"] = ShowLoreCodex,
            ["/achievements"] = ShowAchievements,
            ["/достижения"] = ShowAchievements,
            ["/chronicle"] = ShowChronicle,
            ["/хроника"] = ShowChronicle,
            ["/story"] = ShowStory,
            ["/рассказ"] = ShowStory,
            ["/история"] = ShowStory,
            ["/behavior"] = ShowBehaviorAssessment,
            ["/поведение"] = ShowBehaviorAssessment,
            ["/validate"] = ShowValidation,
            ["/валидация"] = ShowValidation,
            ["/lives"] = ShowLivesHistory,
            ["/жизни"] = ShowLivesHistory,
            ["/feathers"] = ShowInkFeathersMenu,
            ["/перья"] = ShowInkFeathersMenu,
            ["/mods"] = ShowSystemMods,
            ["/моды"] = ShowSystemMods,
            ["/system_guardians"] = ShowSystemGuardianLibrary,
            ["/системные_хранители"] = ShowSystemGuardianLibrary,
            ["/извечные_хранители"] = ShowSystemGuardianLibrary,
            ["/world_setup"] = ShowWorldSetup,
            ["/настройка_мира"] = ShowWorldSetup,
            ["/world_rules"] = ShowWorldRules,
            ["/правила_мира"] = ShowWorldRules,
            ["/gallery"] = ShowGallery,
            ["/галерея"] = ShowGallery,
        };

        _chaosSeaOnlyCommands = new Dictionary<string, Func<Task>>(StringComparer.OrdinalIgnoreCase)
        {
            ["/guardians"] = ShowGuardians,
            ["/хранители"] = ShowGuardians,
            ["/abode_power"] = ShowAbodePower,
            ["/сила_обители"] = ShowAbodePower,
            ["/abode_offering"] = ShowAbodeOffering,
            ["/подношение_обители"] = ShowAbodeOffering,
            ["/guardian_projects"] = ShowGuardianProjects,
            ["/проекты_хранителей"] = ShowGuardianProjects,
            ["/abodes"] = ShowAbodesNavigation,
            ["/обители"] = ShowAbodesNavigation,
            ["/shining_abode"] = ShowShiningAbodeOverview,
            ["/сияющая_обитель"] = ShowShiningAbodeOverview,
            ["/shining_politics"] = ShowShiningPoliticsOverview,
            ["/сияющая_политика"] = ShowShiningPoliticsOverview,
            ["/afterlife_inbox"] = ShowAfterlifeInbox,
            ["/уведомления_загробья"] = ShowAfterlifeInbox,
            ["/gacha"] = ShowGachaInfo,
            ["/гача"] = ShowGachaInfo,
            ["/found_guardian_mantle"] = ShowPlayerGuardianFoundationAsync,
            ["/учредить_хранителя"] = ShowPlayerGuardianFoundationAsync,
        };

        _mortalOnlyCommands = new Dictionary<string, Func<Task>>(StringComparer.OrdinalIgnoreCase)
        {
            ["/inv"] = ShowInventory,
            ["/inventory"] = ShowInventory,
            ["/инв"] = ShowInventory,
            ["/инвентарь"] = ShowInventory,
            ["/npc"] = ShowNPCs,
            ["/npcs"] = ShowNPCs,
            ["/characters"] = ShowNPCs,
            ["/нпс"] = ShowNPCs,
            ["/персонажи"] = ShowNPCs,
            ["/quests"] = ShowQuests,
            ["/квесты"] = ShowQuests,
            ["/map"] = ShowMap,
            ["/карта"] = ShowMap,
            ["/status"] = ShowDetailedStatus,
            ["/статус"] = ShowDetailedStatus,
            ["/where_am_i"] = ShowCurrentLocation,
            ["/где_я"] = ShowCurrentLocation,
            ["/factions"] = ShowFactions,
            ["/фракции"] = ShowFactions,
            ["/skills"] = ShowSkills,
            ["/навыки"] = ShowSkills,
            ["/stats"] = ShowPlayerStats,
            ["/статы"] = ShowPlayerStats,
            ["/характеристики"] = ShowPlayerStats,
            ["/distribute"] = ShowStatDistributionCommand,
            ["/распределить"] = ShowStatDistributionCommand,
            ["/companion_directive"] = SetCompanionDirective,
            ["/директива_компаньону"] = SetCompanionDirective,
            ["/faction_directive"] = SetFactionDirective,
            ["/директива_фракции"] = SetFactionDirective,
            ["/world_news"] = ShowWorldNews,
            ["/новости_мира"] = ShowWorldNews,
            ["/rival_threads"] = ShowRivalSoulThreads,
            ["/чужие_нити"] = ShowRivalSoulThreads,
            ["/guardian_corrections"] = ShowGuardianCorrections,
            ["/коррективы_хранителя"] = ShowGuardianCorrections,
            ["/craft"] = ShowCraftMenu,
            ["/ремесло"] = ShowCraftMenu,
            ["/locations"] = ShowLocations,
            ["/локации"] = ShowLocations,
            ["/transport"] = ShowTransport,
            ["/транспорт"] = ShowTransport,
            ["/effects"] = ShowEffects,
            ["/эффекты"] = ShowEffects,
            ["/combat"] = ShowCombat,
            ["/бой"] = ShowCombat,
            ["/weather"] = ShowWeatherTime,
            ["/погода"] = ShowWeatherTime,
            ["/books"] = ShowItemTexts,
            ["/книги"] = ShowItemTexts,
            ["/читать"] = ShowItemTexts,
            ["/storage_access"] = ShowStorageAccess,
            ["/доступ_к_хранилищам"] = ShowStorageAccess,
            ["/interactions"] = ShowPlayerInteractions,
            ["/взаимодействия"] = ShowPlayerInteractions,
        };

        _allCommandNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in _universalCommands.Keys) _allCommandNames.Add(key);
        foreach (var key in _chaosSeaOnlyCommands.Keys) _allCommandNames.Add(key);
        foreach (var key in _mortalOnlyCommands.Keys) _allCommandNames.Add(key);
    }

    /// <summary>
    /// Try to process as a local command. Returns:
    /// - null: not a recognized command
    /// - "": command handled locally (no GM action needed)
    /// - non-empty string: action to send to the GM.
    /// </summary>
    public async Task<string?> TryProcessCommand(string input)
    {
        var cmd = input.Trim().Split(' ')[0].ToLower();
        var isAfterlife = _stateManager.CurrentState.IsInAfterlifeRealm;
        _pendingGmAction = null;

        if (_universalCommands.TryGetValue(cmd, out var handler))
        {
            await SafeExecute(handler, cmd);
            if (string.IsNullOrEmpty(_pendingGmAction))
                await DiscardPendingLocalTurnRollbackSnapshotAsync();
            return _pendingGmAction ?? "";
        }

        if (_chaosSeaOnlyCommands.TryGetValue(cmd, out var chaosHandler))
        {
            if (IsExactChaosSeaCommand(cmd) && !_stateManager.CurrentState.IsInChaosSea)
            {
                MarkupLine("[yellow]⚠️ Эта команда доступна только в Море Хаоса.[/]");
                MarkupLine("[dim]В Сияющей Обители и во время bootstrap прямые действия Моря Хаоса недоступны.[/]");
                WaitForKey();
                return "";
            }

            if (isAfterlife)
            {
                await SafeExecute(chaosHandler, cmd);
                if (string.IsNullOrEmpty(_pendingGmAction))
                    await DiscardPendingLocalTurnRollbackSnapshotAsync();
                return _pendingGmAction ?? "";
            }

            MarkupLine("[yellow]⚠️ Эта команда доступна только в загробном цикле.[/]");
            MarkupLine("[dim]В смертной жизни вы не можете взаимодействовать с хранителями.[/]");
            WaitForKey();
            return "";
        }

        if (_mortalOnlyCommands.TryGetValue(cmd, out var mortalHandler))
        {
            if (!isAfterlife)
            {
                await SafeExecute(mortalHandler, cmd);
                if (string.IsNullOrEmpty(_pendingGmAction))
                    await DiscardPendingLocalTurnRollbackSnapshotAsync();
                return _pendingGmAction ?? "";
            }

            MarkupLine("[yellow]⚠️ Эта команда доступна только в смертной жизни.[/]");
            MarkupLine("[dim]В загробном цикле у вас нет смертного инвентаря, карты и т.д.[/]");
            MarkupLine("[dim]Используйте /воплотиться чтобы войти в смертную жизнь.[/]");
            WaitForKey();
            return "";
        }

        return null;
    }

    public bool IsCommand(string input)
        => input.TrimStart().StartsWith('/');
}
