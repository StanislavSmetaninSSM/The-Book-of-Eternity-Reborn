namespace BookOfEternityClient.CommandProtocol;

public static class ExplorerCommandCatalog
{
    public static IReadOnlyList<ExplorerCommandDescriptor> Descriptors { get; } =
    [
        D("help", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.Help, ["/help", "/помощь"]),
        D("math", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.Math, ["/math", "/математик"], acceptsArguments: true),
        D("soul", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/soul", "/душа"]),
        D("soul_relics", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/soul_relics", "/реликвии"]),
        D("afterlife_archive", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/afterlife_archive", "/архив_души"]),
        D("archive_candidates", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/archive_candidates", "/архив_кандидаты"]),
        D("soul_quests", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/soul_quests", "/квесты_души"]),
        D("codex", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/codex", "/кодекс"]),
        D("achievements", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/achievements", "/достижения"]),
        D("chronicle", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/chronicle", "/хроника"]),
        D("story", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/story", "/рассказ", "/история"]),
        D("behavior", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/behavior", "/поведение"]),
        D("lives", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/lives", "/жизни"]),
        D("feathers", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/feathers", "/перья"]),
        D("world_rules", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/world_rules", "/правила_мира"]),
        D("gallery", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/gallery", "/галерея"]),
        D("status", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/status", "/статус"]),
        D("gm", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/gm", "/гм"]),
        D("debug", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/debug", "/отладка"]),
        D("mods", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/mods", "/моды"]),
        D("system_guardians", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/system_guardians", "/системные_хранители", "/извечные_хранители"]),
        D("saref_story", ExplorerCommandGroup.SarefStory, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/saref", "/сареф", "/saref_story", "/история_сарефа", "/wings_of_angels", "/крылья_над_бездной"], subcommands:
        [
            new("find_wings", ["find_wings", "find wings", "найти_крылья", "найти крылья"], "/сареф найти_крылья", BrowserStatus: ExplorerCommandMigrationStatus.MutatingParity),
            new("use_advantage", ["use_advantage", "use advantage", "преимущество", "использовать_преимущество", "использовать преимущество"], "/сареф преимущество", BrowserStatus: ExplorerCommandMigrationStatus.MutatingParity),
            new("confrontation", ["confrontation", "confront", "final", "финал", "конфронтация", "сразиться"], "/сареф конфронтация", BrowserStatus: ExplorerCommandMigrationStatus.MutatingParity),
            new("break_oath", ["break_oath", "break oath", "разорвать_клятву", "разорвать клятву", "разрыв_клятвы", "разрыв клятвы"], "/сареф разорвать_клятву", BrowserStatus: ExplorerCommandMigrationStatus.MutatingParity),
            new("agenda", ["agenda", "assignment", "поручение", "повестка", "задание"], "/сареф поручение", BrowserStatus: ExplorerCommandMigrationStatus.MutatingParity)
        ]),
        D("saref_memory_scene", ExplorerCommandGroup.SarefStory, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/воспоминание", "/воспоминание_статус", "/воспоминание_начать", "/воспоминание_способности"], subcommands:
        [
            new("status", ["status", "статус"], "/воспоминание_статус"),
            new("start", ["start", "начать"], "/воспоминание_начать"),
            new("abilities", ["abilities", "способности"], "/воспоминание_способности")
        ]),

        D("validate", ExplorerCommandGroup.Lifecycle, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/validate", "/валидация"]),
        D("world_setup", ExplorerCommandGroup.Lifecycle, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/world_setup", "/настройка_мира"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),

        D("inventory", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/inv", "/inventory", "/инв", "/инвентарь"]),
        D("npcs", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/npc", "/npcs", "/characters", "/нпс", "/персонажи"]),
        D("quests", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/quests", "/квесты"]),
        D("map", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/map", "/карта"]),
        D("where_am_i", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/where_am_i", "/где_я"]),
        D("factions", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/factions", "/фракции"]),
        D("skills", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/skills", "/навыки"]),
        D("stats", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/stats", "/статы", "/характеристики"]),
        D("world_news", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/world_news", "/новости_мира"]),
        D("rival_threads", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/rival_threads", "/чужие_нити"]),
        D("guardian_corrections", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/guardian_corrections", "/коррективы_хранителя"]),
        D("locations", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/locations", "/локации"]),
        D("transport", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/transport", "/транспорт"]),
        D("effects", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/effects", "/эффекты"]),
        D("combat", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/combat", "/бой"]),
        D("weather", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/weather", "/погода"]),
        D("books", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/books", "/книги", "/читать"]),
        D("storage_access", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/storage_access", "/доступ_к_хранилищам"]),
        D("interactions", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/interactions", "/взаимодействия"]),
        D("distribute", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/distribute", "/распределить"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("companion_directive", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/companion_directive", "/директива_компаньону"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("faction_directive", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/faction_directive", "/директива_фракции"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("craft", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/craft", "/ремесло"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),

        D("chaos_sea", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ChaosSea, ["/chaos_sea", "/море_хаоса"]),
        D("guardians", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ChaosSea, ["/guardians", "/хранители"]),
        D("abode_power", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ChaosSea, ["/abode_power", "/сила_обители"]),
        D("guardian_projects", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ChaosSea, ["/guardian_projects", "/проекты_хранителей"]),
        D("abodes", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ChaosSea, ["/abodes", "/обители"]),
        D("gacha", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ChaosSea, ["/gacha", "/гача"]),
        D("abode_offering", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/abode_offering", "/подношение_обители"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("found_guardian_mantle", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/found_guardian_mantle", "/учредить_хранителя"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),

        D("shining_abode", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_abode", "/сияющая_обитель"]),
        D("shining_politics", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_politics", "/сияющая_политика"]),
        D("shining_treasury", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_treasury", "/казначейство"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("source_of_light", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/source_of_light", "/источник_света"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),

        D("afterlife_profiles", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.AfterlifeCombat, ["/afterlife_profiles", "/профили_загробья"]),
        D("afterlife_threats", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.AfterlifeCombat, ["/afterlife_threats", "/угрозы_загробья"]),
        D("afterlife_inbox", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.AfterlifeCombat, ["/afterlife_inbox", "/уведомления_загробья"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("spiritual_conflict", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.AfterlifeCombat, ["/spiritual_conflict", "/духовный_конфликт"]),
        D("spiritual_combat_log", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.AfterlifeCombat, ["/spiritual_combat_log", "/журнал_духовного_боя"]),
        D("spiritual_combat_help", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.AfterlifeCombat, ["/spiritual_combat_help", "/духовный_бой"]),
        D("spiritual_arts", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.AfterlifeCombat, ["/spiritual_arts", "/духовные_искусства"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("spiritual_action", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/spiritual_action", "/духовное_действие"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity)
    ];

    public static IReadOnlyList<string> AllAliases { get; } =
        Descriptors.SelectMany(static descriptor => descriptor.Aliases).ToArray();

    public static ExplorerCommandDescriptor? FindByAlias(string command)
    {
        var token = ExtractCommandToken(command);
        return Descriptors.FirstOrDefault(descriptor =>
            descriptor.Aliases.Contains(token, StringComparer.OrdinalIgnoreCase));
    }

    public static ExplorerCommandDescriptor Require(string id) =>
        Descriptors.First(descriptor => string.Equals(descriptor.Id, id, StringComparison.OrdinalIgnoreCase));

    public static string ExtractCommandToken(string command)
    {
        var parts = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? string.Empty : parts[0];
    }

    private static ExplorerCommandDescriptor D(
        string id,
        ExplorerCommandGroup group,
        ExplorerCommandMutationMode mutationMode,
        ExplorerCommandBrowserHandlerKind browserHandlerKind,
        IReadOnlyList<string> aliases,
        ExplorerCommandMigrationStatus browserStatus = ExplorerCommandMigrationStatus.ReadOnlyParity,
        string followUpIssue = "",
        string reason = "",
        bool acceptsArguments = false,
        IReadOnlyList<ExplorerCommandSubcommandDescriptor>? subcommands = null) =>
        new(id, aliases, group, mutationMode, browserStatus, browserHandlerKind, followUpIssue, reason, acceptsArguments, subcommands ?? []);
}

public sealed record ExplorerCommandDescriptor(
    string Id,
    IReadOnlyList<string> Aliases,
    ExplorerCommandGroup Group,
    ExplorerCommandMutationMode MutationMode,
    ExplorerCommandMigrationStatus BrowserStatus,
    ExplorerCommandBrowserHandlerKind BrowserHandlerKind,
    string FollowUpIssue = "",
    string Reason = "",
    bool AcceptsArguments = false,
    IReadOnlyList<ExplorerCommandSubcommandDescriptor>? Subcommands = null)
{
    public string PrimaryAlias => Aliases.Count == 0 ? string.Empty : Aliases[0];
    public IReadOnlyList<ExplorerCommandSubcommandDescriptor> SubcommandDescriptors => Subcommands ?? [];
}

public sealed record ExplorerCommandSubcommandDescriptor(
    string Id,
    IReadOnlyList<string> Aliases,
    string CanonicalCommand,
    ExplorerCommandMigrationStatus BrowserStatus = ExplorerCommandMigrationStatus.ReadOnlyParity,
    string FollowUpIssue = "",
    string Reason = "");

public enum ExplorerCommandMutationMode
{
    ReadOnly,
    LocalTurn,
    Diagnostics
}

public enum ExplorerCommandBrowserHandlerKind
{
    Help,
    Math,
    UniversalMeta,
    MortalWorld,
    ChaosSea,
    ShiningAbode,
    AfterlifeCombat,
    LifecycleLocalTurn
}
