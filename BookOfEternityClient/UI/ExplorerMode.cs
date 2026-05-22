using BookOfEternityClient.CommandProtocol;
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
        IExplorerConsole? console = null,
        LocalUiSessionLockService? localUiSessionLockService = null,
        LocalUiSessionLockOwner? localUiSessionLockOwner = null)
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
        _localUiSessionLockService = localUiSessionLockService ?? new LocalUiSessionLockService(fs);
        _localUiSessionLockOwner = localUiSessionLockOwner ?? BuildDefaultConsoleLockOwner();
        _clipboardService = clipboardService;
        _fs = fs;
        _loc = loc;

        _universalCommands = BuildCommandMap(
            ("help", ShowHelpDto),
            ("math", ShowMathAssistantAsync),
            ("soul", ShowSoulInfo),
            ("soul_relics", ShowSoulRelics),
            ("afterlife_archive", ShowAfterlifeArchive),
            ("archive_candidates", ShowAfterlifeArchiveCandidates),
            ("soul_quests", ShowSoulQuests),
            ("gm", ShowGmThoughts),
            ("debug", ShowDebugInfo),
            ("codex", ShowLoreCodex),
            ("achievements", ShowAchievements),
            ("chronicle", ShowChronicle),
            ("story", ShowStory),
            ("behavior", ShowBehaviorAssessment),
            ("validate", ShowValidation),
            ("lives", ShowLivesHistory),
            ("feathers", ShowInkFeathersMenu),
            ("mods", ShowSystemMods),
            ("system_guardians", ShowSystemGuardianLibrary),
            ("world_setup", ShowWorldSetup),
            ("world_rules", ShowWorldRules),
            ("gallery", ShowGallery),
            ("status", ShowDetailedStatus),
            ("saref_story", ShowSarefStoryAsync));

        _chaosSeaOnlyCommands = BuildCommandMap(
            ("chaos_sea", ShowGuardians),
            ("guardians", ShowGuardians),
            ("map", ShowMap),
            ("abode_power", ShowAbodePower),
            ("abode_offering", ShowAbodeOffering),
            ("guardian_projects", ShowGuardianProjects),
            ("guardian_politics", ShowGuardianPoliticsAsync),
            ("abodes", ShowAbodesNavigation),
            ("shining_abode", ShowShiningAbodeOverview),
            ("shining_politics", ShowShiningPoliticsOverview),
            ("shining_treasury", ShowShiningTreasuryAsync),
            ("source_of_light", ShowSourceOfLightAsync),
            ("afterlife_profiles", ShowAfterlifeEntityProfilesAsync),
            ("afterlife_threats", ShowAfterlifeThreatsAsync),
            ("saref_memory_scene", ShowSarefMemorySceneAsync),
            ("afterlife_inbox", ShowAfterlifeInbox),
            ("spiritual_conflict", ShowSpiritualConflictAsync),
            ("spiritual_combat_log", ShowSpiritualCombatLogAsync),
            ("spiritual_combat_help", ShowSpiritualCombatHelpAsync),
            ("spiritual_arts", ShowSpiritualArtsAsync),
            ("spiritual_action", ShowSpiritualActionAsync),
            ("gacha", ShowGachaInfo),
            ("found_guardian_mantle", ShowPlayerGuardianFoundationAsync));

        _mortalOnlyCommands = BuildCommandMap(
            ("inventory", ShowInventory),
            ("npcs", ShowNPCs),
            ("quests", ShowQuests),
            ("map", ShowMap),
            ("where_am_i", ShowCurrentLocation),
            ("factions", ShowFactions),
            ("skills", ShowSkills),
            ("stats", ShowPlayerStats),
            ("distribute", ShowStatDistributionCommand),
            ("companion_directive", SetCompanionDirective),
            ("faction_directive", SetFactionDirective),
            ("world_news", ShowWorldNews),
            ("rival_threads", ShowRivalSoulThreads),
            ("guardian_corrections", ShowGuardianCorrections),
            ("craft", ShowCraftMenu),
            ("locations", ShowLocations),
            ("transport", ShowTransport),
            ("effects", ShowEffects),
            ("combat", ShowCombat),
            ("weather", ShowWeatherTime),
            ("books", ShowItemTexts),
            ("storage_access", ShowStorageAccess),
            ("interactions", ShowPlayerInteractions));

        _allCommandNames = ExplorerCommandCatalog.AllAliases.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, Func<Task>> BuildCommandMap(params (string Id, Func<Task> Handler)[] registrations)
    {
        var map = new Dictionary<string, Func<Task>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, handler) in registrations)
        {
            foreach (var alias in ExplorerCommandCatalog.Require(id).Aliases)
                map[alias] = handler;
        }

        return map;
    }

    private static LocalUiSessionLockOwner BuildDefaultConsoleLockOwner() =>
        new(
            $"console:{Environment.MachineName}:{Environment.ProcessId}",
            "console",
            $"Консоль PID {Environment.ProcessId}",
            TimeSpan.FromMinutes(2));

    /// <summary>
    /// Try to process as a local command. Returns:
    /// - null: not a recognized command
    /// - "": command handled locally (no GM action needed)
    /// - non-empty string: action to send to the GM.
    /// </summary>
    public async Task<string?> TryProcessCommand(string input)
    {
        var trimmedInput = input.Trim();
        if (!IsCommand(trimmedInput))
            return null;

        var parsedCommand = ExplorerCommandParser.Parse(trimmedInput);
        if (!parsedCommand.Success)
        {
            if (string.Equals(parsedCommand.ErrorTitle, "Некорректные аргументы", StringComparison.OrdinalIgnoreCase))
            {
                MarkupLine($"[yellow]⚠️ {parsedCommand.ErrorTitle}: {parsedCommand.ErrorMessage}[/]");
                WaitForKey();
                return "";
            }

            return null;
        }

        var commandParts = parsedCommand.BuilderCommand.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = commandParts.Length > 0 ? commandParts[0].ToLowerInvariant() : string.Empty;
        _currentCommandRemainder = commandParts.Length > 1 ? commandParts[1].Trim() : string.Empty;
        var currentRealm = _stateManager.CurrentState.CurrentRealm;
        var hasResolvedRealm = RealmSemantics.HasResolvedRealm(currentRealm);
        var isAfterlife = RealmSemantics.IsAfterlifeRealm(currentRealm);
        var isMortal = RealmSemantics.IsMortalRealm(currentRealm);
        _pendingGmAction = null;

        if (_universalCommands.TryGetValue(cmd, out var handler))
        {
            if (RequiresResolvedRealmForUniversalCommand(cmd) && !hasResolvedRealm)
            {
                MarkupLine("[yellow]⚠️ Эта команда требует определённого realm, но soul_state.currentRealm не определён.[/]");
                MarkupLine("[dim]Восстановите game_state/meta/soul_state.json.currentRealm перед командами, которые могут менять состояние души.[/]");
                WaitForKey();
                return "";
            }

            if (!await TryAcquireLocalUiSessionMutationLockAsync(cmd))
                return "";

            await SafeExecute(handler, cmd);
            if (string.IsNullOrEmpty(_pendingGmAction))
                await DiscardPendingLocalTurnRollbackSnapshotAsync();
            return _pendingGmAction ?? "";
        }

        if (_chaosSeaOnlyCommands.TryGetValue(cmd, out var chaosHandler))
        {
            if (!hasResolvedRealm)
            {
                MarkupLine("[yellow]⚠️ Нельзя выполнить realm-scoped команду: soul_state.currentRealm не определён.[/]");
                MarkupLine("[dim]Сначала восстановите game_state/meta/soul_state.json.currentRealm; клиент не будет угадывать смертный или загробный режим.[/]");
                WaitForKey();
                return "";
            }

            if (IsExactChaosSeaCommand(cmd) && isAfterlife && !_stateManager.CurrentState.IsInChaosSea)
            {
                MarkupLine("[yellow]⚠️ Эта команда доступна только в Море Хаоса.[/]");
                MarkupLine("[dim]В Сияющей Обители и во время bootstrap прямые действия Моря Хаоса недоступны.[/]");
                WaitForKey();
                return "";
            }

            if (isAfterlife)
            {
                if (!await TryAcquireLocalUiSessionMutationLockAsync(cmd))
                    return "";

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
            if (isMortal)
            {
                if (!await TryAcquireLocalUiSessionMutationLockAsync(cmd))
                    return "";

                await SafeExecute(mortalHandler, cmd);
                if (string.IsNullOrEmpty(_pendingGmAction))
                    await DiscardPendingLocalTurnRollbackSnapshotAsync();
                return _pendingGmAction ?? "";
            }

            if (!hasResolvedRealm)
            {
                MarkupLine("[yellow]⚠️ Эта команда требует явного смертного мира, но soul_state.currentRealm не определён.[/]");
                MarkupLine("[dim]Восстановите game_state/meta/soul_state.json.currentRealm перед командами смертной жизни.[/]");
                WaitForKey();
                return "";
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

    private static bool RequiresResolvedRealmForUniversalCommand(string command) =>
        string.Equals(command, "/feathers", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(command, "/перья", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(command, "/soul_relics", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(command, "/реликвии", StringComparison.OrdinalIgnoreCase);
}
