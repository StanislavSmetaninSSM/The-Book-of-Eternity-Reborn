namespace BookOfEternityClient.CommandProtocol;

public static class ExplorerCommandMigrationRegistry
{
    public static IReadOnlyList<ExplorerCommandMigrationEntry> Entries => BuildEntries();

    private static readonly string[] UniversalReadOnlyCommands =
    [
        "/help",
        "/помощь",
        "/soul",
        "/душа",
        "/soul_relics",
        "/реликвии",
        "/afterlife_archive",
        "/архив_души",
        "/archive_candidates",
        "/архив_кандидаты",
        "/soul_quests",
        "/квесты_души",
        "/codex",
        "/кодекс",
        "/achievements",
        "/достижения",
        "/chronicle",
        "/хроника",
        "/story",
        "/рассказ",
        "/история",
        "/behavior",
        "/поведение",
        "/lives",
        "/жизни",
        "/feathers",
        "/перья",
        "/world_rules",
        "/правила_мира",
        "/gallery",
        "/галерея",
        "/status",
        "/статус"
    ];

    private static readonly string[] UniversalDiagnosticsCommands =
    [
        "/gm",
        "/гм",
        "/debug",
        "/отладка",
        "/mods",
        "/моды",
        "/system_guardians",
        "/системные_хранители",
        "/извечные_хранители"
    ];

    private static readonly string[] UniversalLifecycleCommands =
    [
        "/validate",
        "/валидация",
        "/world_setup",
        "/настройка_мира"
    ];

    private static readonly string[] SarefStoryCommands =
    [
        "/saref",
        "/сареф",
        "/saref_story",
        "/история_сарефа",
        "/wings_of_angels",
        "/крылья_над_бездной"
    ];

    private static readonly string[] MortalReadOnlyCommands =
    [
        "/inv",
        "/inventory",
        "/инв",
        "/инвентарь",
        "/npc",
        "/npcs",
        "/characters",
        "/нпс",
        "/персонажи",
        "/quests",
        "/квесты",
        "/map",
        "/карта",
        "/where_am_i",
        "/где_я",
        "/factions",
        "/фракции",
        "/skills",
        "/навыки",
        "/stats",
        "/статы",
        "/характеристики",
        "/world_news",
        "/новости_мира",
        "/rival_threads",
        "/чужие_нити",
        "/guardian_corrections",
        "/коррективы_хранителя",
        "/locations",
        "/локации",
        "/transport",
        "/транспорт",
        "/effects",
        "/эффекты",
        "/combat",
        "/бой",
        "/weather",
        "/погода",
        "/books",
        "/книги",
        "/читать",
        "/storage_access",
        "/доступ_к_хранилищам",
        "/interactions",
        "/взаимодействия"
    ];

    private static readonly string[] MortalMutatingCommands =
    [
        "/distribute",
        "/распределить",
        "/companion_directive",
        "/директива_компаньону",
        "/faction_directive",
        "/директива_фракции",
        "/craft",
        "/ремесло"
    ];

    private static readonly string[] ChaosSeaReadOnlyCommands =
    [
        "/chaos_sea",
        "/море_хаоса",
        "/guardians",
        "/хранители",
        "/abode_power",
        "/сила_обители",
        "/guardian_projects",
        "/проекты_хранителей",
        "/abodes",
        "/обители",
        "/gacha",
        "/гача"
    ];

    private static readonly string[] ChaosSeaMutatingCommands =
    [
        "/abode_offering",
        "/подношение_обители",
        "/found_guardian_mantle",
        "/учредить_хранителя"
    ];

    private static readonly string[] ShiningReadOnlyCommands =
    [
        "/shining_abode",
        "/сияющая_обитель",
        "/shining_politics",
        "/сияющая_политика"
    ];

    private static readonly string[] ShiningMutatingCommands =
    [
        "/shining_treasury",
        "/казначейство",
        "/source_of_light",
        "/источник_света"
    ];

    private static readonly string[] AfterlifeCombatReadOnlyCommands =
    [
        "/afterlife_profiles",
        "/профили_загробья",
        "/afterlife_inbox",
        "/уведомления_загробья",
        "/spiritual_conflict",
        "/духовный_конфликт",
        "/spiritual_combat_log",
        "/журнал_духовного_боя",
        "/spiritual_combat_help",
        "/духовный_бой",
        "/spiritual_arts",
        "/духовные_искусства"
    ];

    private static readonly string[] AfterlifeCombatMutatingCommands =
    [
        "/spiritual_action",
        "/духовное_действие"
    ];

    private static IReadOnlyList<ExplorerCommandMigrationEntry> BuildEntries() =>
    [
        ..Planned(UniversalReadOnlyCommands, ExplorerCommandGroup.UniversalMeta, "#569"),
        ..TemporaryConsoleOnly(UniversalDiagnosticsCommands, ExplorerCommandGroup.UniversalMeta, "#569",
            "Диагностические и служебные команды должны получить явный browser-safe режим отображения."),
        ..Blocked(UniversalLifecycleCommands, ExplorerCommandGroup.Lifecycle, "#574",
            "Команды настройки/валидации связаны с локальными протоколами, repair flow или управлением файлами."),
        ..Planned(SarefStoryCommands, ExplorerCommandGroup.SarefStory, "#569"),
        ..Planned(MortalReadOnlyCommands, ExplorerCommandGroup.MortalWorld, "#570"),
        ..Blocked(MortalMutatingCommands, ExplorerCommandGroup.MortalWorld, "#568",
            "Мутирующие mortal-команды нельзя открывать браузеру до local session lock."),
        ..Planned(ChaosSeaReadOnlyCommands, ExplorerCommandGroup.ChaosSea, "#571"),
        ..Blocked(ChaosSeaMutatingCommands, ExplorerCommandGroup.ChaosSea, "#568",
            "Chaos Sea pending-contract и economy-команды требуют local session lock."),
        ..Planned(ShiningReadOnlyCommands, ExplorerCommandGroup.ShiningAbode, "#572"),
        ..Blocked(ShiningMutatingCommands, ExplorerCommandGroup.ShiningAbode, "#568",
            "Shining Abode economy, capstone, gates, and pending-contract commands require local session lock."),
        ..Planned(AfterlifeCombatReadOnlyCommands, ExplorerCommandGroup.AfterlifeCombatAndEntities, "#573"),
        ..Blocked(AfterlifeCombatMutatingCommands, ExplorerCommandGroup.AfterlifeCombatAndEntities, "#568",
            "Духовное действие меняет pending/local turn state and must wait for local session lock.")
    ];

    private static IEnumerable<ExplorerCommandMigrationEntry> Planned(
        IEnumerable<string> commands,
        ExplorerCommandGroup group,
        string followUpIssue) =>
        commands.Select(command => Entry(command, group, ExplorerCommandMigrationStatus.Planned, followUpIssue));

    private static IEnumerable<ExplorerCommandMigrationEntry> Blocked(
        IEnumerable<string> commands,
        ExplorerCommandGroup group,
        string followUpIssue,
        string reason) =>
        commands.Select(command => Entry(command, group, ExplorerCommandMigrationStatus.Blocked, followUpIssue, reason));

    private static IEnumerable<ExplorerCommandMigrationEntry> TemporaryConsoleOnly(
        IEnumerable<string> commands,
        ExplorerCommandGroup group,
        string followUpIssue,
        string reason) =>
        commands.Select(command => Entry(command, group, ExplorerCommandMigrationStatus.ConsoleOnlyTemporarily, followUpIssue, reason));

    private static ExplorerCommandMigrationEntry Entry(
        string command,
        ExplorerCommandGroup group,
        ExplorerCommandMigrationStatus status,
        string followUpIssue,
        string reason = "") =>
        new(command, group, status, followUpIssue, reason);
}

public sealed record ExplorerCommandMigrationEntry(
    string Command,
    ExplorerCommandGroup Group,
    ExplorerCommandMigrationStatus Status,
    string FollowUpIssue,
    string Reason = "");

public enum ExplorerCommandMigrationStatus
{
    Migrated,
    Planned,
    Blocked,
    ConsoleOnlyTemporarily
}

public enum ExplorerCommandGroup
{
    UniversalMeta,
    MortalWorld,
    ChaosSea,
    ShiningAbode,
    AfterlifeCombatAndEntities,
    SarefStory,
    Lifecycle
}
