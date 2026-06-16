namespace BookOfEternityClient.CommandProtocol;

public static class ExplorerCommandCatalog
{
    public static IReadOnlyList<ExplorerCommandDescriptor> Descriptors { get; } =
    [
        D("help", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.Help, ["/help", "/помощь"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("math", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.Math, ["/math", "/математик"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("soul", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/soul", "/душа"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("soul_relics", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/soul_relics", "/реликвии"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("afterlife_archive", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/afterlife_archive", "/архив_души"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("archive_candidates", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/archive_candidates", "/архив_кандидаты"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("soul_quests", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/soul_quests", "/квесты_души"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("codex", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/codex", "/кодекс"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("achievements", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/achievements", "/достижения"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("chronicle", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/chronicle", "/хроника"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("story", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/story", "/рассказ", "/история"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("behavior", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/behavior", "/поведение"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("lives", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/lives", "/жизни"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("feathers", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/feathers", "/перья"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("world_rules", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/world_rules", "/правила_мира"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("gallery", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/gallery", "/галерея"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("status", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/status", "/статус"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("gm", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/gm", "/гм"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("debug", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/debug", "/отладка"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("mods", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/mods", "/моды"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("system_guardians", ExplorerCommandGroup.UniversalMeta, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/system_guardians", "/системные_хранители", "/извечные_хранители"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("saref_story", ExplorerCommandGroup.SarefStory, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/saref", "/сареф", "/saref_story", "/история_сарефа", "/wings_of_angels", "/крылья_над_бездной"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, subcommands:
        [
            new("find_wings", ["find_wings", "find wings", "найти_крылья", "найти крылья"], "/сареф найти_крылья", BrowserStatus: ExplorerCommandMigrationStatus.MutatingParity),
            new("use_advantage", ["use_advantage", "use advantage", "преимущество", "использовать_преимущество", "использовать преимущество"], "/сареф преимущество", BrowserStatus: ExplorerCommandMigrationStatus.MutatingParity),
            new("confrontation", ["confrontation", "confront", "final", "финал", "конфронтация", "сразиться"], "/сареф конфронтация", BrowserStatus: ExplorerCommandMigrationStatus.MutatingParity),
            new("break_oath", ["break_oath", "break oath", "разорвать_клятву", "разорвать клятву", "разрыв_клятвы", "разрыв клятвы"], "/сареф разорвать_клятву", BrowserStatus: ExplorerCommandMigrationStatus.MutatingParity),
            new("agenda", ["agenda", "assignment", "поручение", "повестка", "задание"], "/сареф поручение", BrowserStatus: ExplorerCommandMigrationStatus.MutatingParity)
        ]),
        D("saref_memory_scene", ExplorerCommandGroup.SarefStory, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.UniversalMeta, ["/воспоминание", "/воспоминание_статус", "/воспоминание_начать", "/воспоминание_способности"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, subcommands:
        [
            new("status", ["status", "статус"], "/воспоминание_статус", BrowserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
            new("start", ["start", "начать"], "/воспоминание_начать", BrowserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
            new("abilities", ["abilities", "способности"], "/воспоминание_способности", BrowserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity)
        ]),

        D("validate", ExplorerCommandGroup.Lifecycle, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/validate", "/валидация"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("world_setup", ExplorerCommandGroup.Lifecycle, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/world_setup", "/настройка_мира"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),

        D("inventory", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/inv", "/inventory", "/инв", "/инвентарь"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("npcs", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/npc", "/npcs", "/characters", "/нпс", "/персонажи"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("npc_talk", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/npc_talk", "/talk_npc", "/поговорить_с_нпс", "/разговор_с_нпс"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("quests", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/quests", "/квесты"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("map", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/map", "/карта"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("where_am_i", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/where_am_i", "/где_я"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("factions", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/factions", "/фракции"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("skills", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/skills", "/навыки"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("stats", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/stats", "/статы", "/характеристики"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("world_news", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/world_news", "/новости_мира"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("rival_threads", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/rival_threads", "/чужие_нити"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("guardian_corrections", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/guardian_corrections", "/коррективы_хранителя"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("locations", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/locations", "/локации"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("transport", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/transport", "/транспорт"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("effects", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/effects", "/эффекты"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("combat", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/combat", "/бой"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("weather", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/weather", "/погода"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("books", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/books", "/книги", "/читать"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("storage_access", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/storage_access", "/доступ_к_хранилищам"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("interactions", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.MortalWorld, ["/interactions", "/взаимодействия"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("ink_feather_reveal_fate", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/reveal_fate", "/открыть_судьбу"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("ink_feather_rewrite_fate", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/rewrite_fate", "/переписать_судьбу"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("distribute", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/distribute", "/распределить"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("companion_directive", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/companion_directive", "/директива_компаньону"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("faction_directive", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/faction_directive", "/директива_фракции"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("inventory_equip", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/экипировать", "/equip"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("inventory_unequip", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/снять", "/unequip"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("inventory_drop", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/выбросить_предмет", "/inventory_drop"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("inventory_split", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/разделить_стопку", "/inventory_split"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("inventory_merge", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/объединить_стопки", "/inventory_merge"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("storage_item_move", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/storage_move", "/хранилище_предметы"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("vehicle_item_move", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/vehicle_move", "/транспорт_предметы"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("npc_trade", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/npc_trade", "/торговля_нпс"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("craft", ExplorerCommandGroup.MortalWorld, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/craft", "/ремесло"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),

        D("chaos_sea", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ChaosSea, ["/chaos_sea", "/море_хаоса"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("guardians", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ChaosSea, ["/guardians", "/хранители"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("abode_power", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ChaosSea, ["/abode_power", "/сила_обители"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("guardian_projects", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ChaosSea, ["/guardian_projects", "/проекты_хранителей"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("guardian_politics", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ChaosSea, ["/guardian_politics", "/политика_хранителей"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("abodes", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ChaosSea, ["/abodes", "/обители"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity, acceptsArguments: true),
        D("gacha", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/gacha", "/гача"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("abode_offering", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/abode_offering", "/подношение_обители"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("archive_consultation", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/archive_consultation", "/архивная_консультация"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("archive_project_fuel", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/archive_project_fuel", "/архивная_подпитка_проекта"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("found_guardian_mantle", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/found_guardian_mantle", "/учредить_хранителя"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("guardian_trade", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/guardian_trade", "/торговля_хранителя"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("guardian_social", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/guardian_social", "/talk_guardian", "/поговорить_с_хранителем", "/общение_хранителя"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("abode_residents", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/abode_residents", "/обитатели_обители"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("resident_interaction", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/resident_interaction", "/общение_резидента", "/поговорить_с_резидентом", "/история_резидента"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("resident_transfer", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/resident_transfer", "/переход_резидента"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("soul_relic_equip", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/soul_relic_equip", "/экипировать_реликвию"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("soul_relic_unequip", ExplorerCommandGroup.ChaosSea, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/soul_relic_unequip", "/снять_реликвию"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),

        D("shining_abode", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_abode", "/сияющая_обитель"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("shining_politics", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_politics", "/сияющая_политика"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("shining_faction_founding", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_faction_founding", "/основание_сияющей_фракции"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("shining_faction_realignment", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_faction_realignment", "/перестройка_сияющей_фракции"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("shining_faction_leadership", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_faction_leadership", "/смена_главы_сияющей_фракции"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("shining_native_faction_discovery", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_native_faction_discovery", "/открытие_нативной_фракции"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("shining_faction_investment", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_faction_investment", "/инвестиция_в_сияющую_фракцию"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("shining_project_support", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_project_support", "/поддержать_сияющий_проект"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("shining_project_unsupport", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_project_unsupport", "/снять_поддержку_сияющего_проекта"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("shining_project_retirement", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_project_retirement", "/отправить_сияющий_проект_в_историю"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("shining_gates_open", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_gates_open", "/открыть_врата_инкарнации"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("shining_gates_select", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_gates_select", "/выбрать_благословение"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("shining_gates_deselect", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_gates_deselect", "/снять_благословение"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("shining_gates_reroll", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_gates_reroll", "/обновить_врата"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("shining_incarnation_prepare", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_incarnation_prepare", "/подготовить_новую_жизнь"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("shining_relic_forge", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_relic_forge", "/сияющая_ковка"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("shining_trade", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.LifecycleLocalTurn, ["/shining_trade", "/сияющая_торговля"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity, acceptsArguments: true),
        D("shining_treasury", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/shining_treasury", "/казначейство"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("source_of_light", ExplorerCommandGroup.ShiningAbode, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.ShiningAbode, ["/source_of_light", "/источник_света"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),

        D("afterlife_profiles", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.AfterlifeCombat, ["/afterlife_profiles", "/профили_загробья"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("afterlife_threats", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.AfterlifeCombat, ["/afterlife_threats", "/угрозы_загробья"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("afterlife_chronicles", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.AfterlifeCombat, ["/afterlife_chronicles", "/хроники_посмертия"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("afterlife_inbox", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.LocalTurn, ExplorerCommandBrowserHandlerKind.AfterlifeCombat, ["/afterlife_inbox", "/уведомления_загробья"], browserStatus: ExplorerCommandMigrationStatus.MutatingParity),
        D("spiritual_conflict", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.AfterlifeCombat, ["/spiritual_conflict", "/духовный_конфликт"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("spiritual_combat_log", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.AfterlifeCombat, ["/spiritual_combat_log", "/журнал_духовного_боя"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
        D("spiritual_combat_help", ExplorerCommandGroup.AfterlifeCombatAndEntities, ExplorerCommandMutationMode.ReadOnly, ExplorerCommandBrowserHandlerKind.AfterlifeCombat, ["/spiritual_combat_help", "/духовный_бой"], browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity),
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
        ExplorerCommandMigrationStatus browserStatus,
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
    ExplorerCommandMigrationStatus BrowserStatus,
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
