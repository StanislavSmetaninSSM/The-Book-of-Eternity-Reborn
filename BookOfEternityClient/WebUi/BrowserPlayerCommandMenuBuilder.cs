using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Models.GameState;

namespace BookOfEternityClient.WebUi;

public static class BrowserPlayerCommandMenuBuilder
{
    private const int SchemaVersion = 1;

    private static readonly IReadOnlyList<SectionDefinition> SectionDefinitions =
    [
        new("soul", "Персонаж / Душа", "Состояние героя, души, характеристик и личных ресурсов.", true),
        new("world", "Мир", "Окружение, персонажи, погода, локации и доступные взаимодействия.", true),
        new("quests", "Квесты", "Активные задачи, сюжетные нити и личные поручения.", true),
        new("map", "Карта", "Навигация по текущему царству и известным переходам.", true),
        new("factions", "Фракции", "Фракции, соперники и управляемые поручения союзникам.", true),
        new("guardians", "Хранители", "Хранители, их проекты, политика и особые истории.", true),
        new("afterlife", "Посмертие", "Море Хаоса, Сияющая Обитель и посмертные системы.", true),
        new("combat", "Бой", "Боевые сведения, угрозы и духовные столкновения.", true),
        new("archive", "Архив", "Хроника, кодекс, достижения, реликвии и сохранённые истории.", true),
        new("settings", "Настройки", "Подготовка мира и безопасные игровые настройки.", true),
        new("advanced", "Расширенный режим", "Командная палитра, диагностика и системные инструменты для опытного режима.", false)
    ];

    private static readonly ISet<string> AdvancedOnlyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "debug",
        "gm",
        "validate",
        "mods",
        "system_guardians",
        "math",
        "help"
    };

    private static readonly IReadOnlyDictionary<string, ActionMetadata> Metadata =
        new Dictionary<string, ActionMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["help"] = M("advanced", "Справка", "Показывает расширенную справку по перенесённым командам браузера.", "Откройте справку, если нужно сверить доступные команды в опытном режиме."),
            ["math"] = M("advanced", "Математик", "Выполняет вычисления из расширенной командной палитры.", "Введите выражение в расширенном режиме, когда нужен расчёт."),
            ["soul"] = M("soul", "Душа", "Показывает имя души, инкарнацию, царство и ключевые духовные ресурсы.", "Откройте сводку души без изменения состояния."),
            ["soul_relics"] = M("archive", "Реликвии души", "Показывает реликвии и связанные с душой особые предметы.", "Просмотрите реликвии души и их текущие описания."),
            ["afterlife_archive"] = M("archive", "Архив души", "Открывает записи посмертного архива и судьбоносных решений.", "Просмотрите архив души без ручного ввода команды."),
            ["archive_candidates"] = M("archive", "Кандидаты архива", "Показывает события и сущности, ожидающие внесения в архив.", "Проверьте кандидатов, которые игра может перенести в архив."),
            ["soul_quests"] = M("quests", "Квесты души", "Показывает личные духовные задания и поручения хранителей.", "Просмотрите квесты души и связанные подсказки."),
            ["codex"] = M("archive", "Кодекс", "Открывает справочные записи мира и лора.", "Просмотрите кодекс текущей истории."),
            ["achievements"] = M("archive", "Достижения", "Показывает открытые достижения и памятные вехи.", "Откройте достижения без изменения файлов состояния."),
            ["chronicle"] = M("archive", "Хроника", "Показывает хронику персонажа, мира и сюжетных событий.", "Просмотрите хронику текущей игры."),
            ["story"] = M("archive", "Рассказ", "Открывает сохранённый художественный рассказ и записи сцен.", "Просмотрите сюжетные записи текущей сессии."),
            ["behavior"] = M("settings", "Поведение игрока", "Показывает заметки о стиле действий игрока для безопасной настройки опыта.", "Просмотрите профиль поведения игрока."),
            ["lives"] = M("archive", "История жизней", "Показывает прошлые инкарнации души и их следы.", "Просмотрите список прошлых жизней."),
            ["feathers"] = M("soul", "Чернильные перья", "Показывает запас Чернильных Перьев и связанные записи души.", "Проверьте духовный ресурс души."),
            ["world_rules"] = M("world", "Правила мира", "Показывает активные директивы и ограничения текущего мира.", "Откройте досье правил мира."),
            ["gallery"] = M("archive", "Галерея", "Показывает доступные изображения и визуальные материалы истории.", "Просмотрите галерею без изменения состояния."),
            ["status"] = M("soul", "Статус", "Показывает краткую сводку героя, царства, здоровья и ресурсов.", "Откройте общий статус героя."),
            ["gm"] = M("advanced", "Мысли ГМа", "Показывает диагностические заметки ГМа только в расширенном режиме.", "Откройте GM-заметки только если нужен опытный анализ."),
            ["debug"] = M("advanced", "Отладка", "Показывает техническую сводку состояния для диагностики.", "Откройте отладочную панель в расширенном режиме."),
            ["mods"] = M("advanced", "Моды", "Показывает установленные модификации и служебные файлы.", "Проверьте моды в расширенном режиме."),
            ["system_guardians"] = M("advanced", "Системные хранители", "Показывает системные инструкции хранителей для диагностики.", "Откройте системных хранителей только в опытном режиме."),
            ["saref_story"] = M("guardians", "История Сарефа", "Показывает сюжет Сарефа и доступные этапы его линии.", "Просмотрите линию Сарефа и её текущие цели."),
            ["saref_memory_scene"] = M("guardians", "Воспоминание Сарефа", "Показывает состояние сцены воспоминания и доступные сведения.", "Просмотрите сцену воспоминания Сарефа."),
            ["validate"] = M("advanced", "Валидация", "Запускает проверку состояния из расширенного режима.", "Откройте форму проверки состояния и дождитесь результата."),
            ["world_setup"] = M("settings", "Подготовка мира", "Готовит следующую смертную жизнь из посмертного режима.", "Опишите желаемый жанр, запреты и стартовые условия следующего мира."),
            ["inventory"] = M("soul", "Инвентарь", "Показывает предметы, ресурсы и связи инвентаря персонажа.", "Просмотрите инвентарь персонажа."),
            ["inventory_equip"] = M("soul", "Экипировать предмет", "Открывает форму экипировки обычного предмета из рюкзака.", "Выберите предмет, слот и подтвердите экипировку."),
            ["inventory_unequip"] = M("soul", "Снять предмет", "Открывает форму снятия обычного экипированного предмета.", "Выберите экипированный слот и подтвердите снятие."),
            ["inventory_drop"] = M("soul", "Выбросить предмет", "Открывает форму безопасного выброса предмета из рюкзака или экипировки.", "Выберите предмет и подтвердите выброс."),
            ["inventory_split"] = M("soul", "Разделить стопку", "Открывает форму разделения стопки предметов на две части.", "Выберите стопку, укажите количество и подтвердите разделение."),
            ["inventory_merge"] = M("soul", "Объединить стопки", "Открывает форму объединения совместимых стопок предметов.", "Выберите совместимые стопки и подтвердите объединение."),
            ["storage_item_move"] = M("world", "Хранилище: положить или забрать", "Открывает форму перемещения предметов между рюкзаком и доступным хранилищем.", "Выберите направление, хранилище, предмет и подтвердите перемещение."),
            ["vehicle_item_move"] = M("world", "Транспорт: положить или забрать", "Открывает форму перемещения предметов между рюкзаком и транспортом.", "Выберите направление, транспорт, предмет и подтвердите перемещение."),
            ["npcs"] = M("world", "Персонажи", "Показывает известных НПС, отношения и активности.", "Просмотрите известных персонажей и их состояние."),
            ["npc_talk"] = M("world", "Поговорить с НПС", "Открывает форму разговора с известным персонажем смертного мира.", "Выберите собеседника и кратко опишите тему разговора."),
            ["quests"] = M("quests", "Квесты", "Показывает активные и завершённые задания смертного мира.", "Откройте список квестов текущей жизни."),
            ["map"] = M("map", "Карта", "Показывает карту текущего царства и известные связи.", "Откройте карту текущей области."),
            ["where_am_i"] = M("world", "Где я", "Показывает текущую локацию, регион и описание места.", "Уточните, где сейчас находится персонаж."),
            ["factions"] = M("factions", "Фракции", "Показывает фракции, проекты и хроники влияния.", "Просмотрите фракции текущего мира."),
            ["skills"] = M("soul", "Навыки", "Показывает активные и пассивные навыки персонажа.", "Откройте навыки персонажа."),
            ["training"] = M("soul", "Обучение", "Показывает доступных учителей, наставников и предложения обучения навыкам или духовным искусствам.", "Выберите источник обучения, проверьте стоимость, предел наставника и подтвердите покупку."),
            ["trade"] = M("world", "Торговля", "Открывает торговлю, доступную в текущей реальности.", "Выберите торговца или фракцию, затем осмотрите витрину и подтвердите действие."),
            ["stats"] = M("soul", "Характеристики", "Показывает здоровье, энергию, равновесие и числовые характеристики.", "Просмотрите характеристики героя."),
            ["world_news"] = M("world", "Новости мира", "Показывает события мира, флаги и записи прогресса.", "Откройте свежую сводку мира."),
            ["rival_threads"] = M("factions", "Чужие нити", "Показывает арки соперников и параллельные сюжетные линии.", "Просмотрите чужие нити и соперников."),
            ["guardian_corrections"] = M("guardians", "Коррективы хранителя", "Показывает корректировки хранителя и их состояние.", "Проверьте активные коррективы хранителя."),
            ["locations"] = M("world", "Локации", "Показывает открытые локации и обновления карты.", "Просмотрите известные локации."),
            ["transport"] = M("world", "Транспорт", "Показывает маршруты и доступный транспорт.", "Проверьте доступные перемещения."),
            ["effects"] = M("soul", "Эффекты", "Показывает активные эффекты, раны и временные состояния.", "Просмотрите действующие эффекты персонажа."),
            ["combat"] = M("combat", "Боевые сведения", "Показывает врагов, союзников и журнал боя смертного мира.", "Откройте боевую сводку текущей сцены."),
            ["weather"] = M("world", "Время и погода", "Показывает текущее время мира и погодное состояние.", "Проверьте время и погоду вокруг героя."),
            ["books"] = M("archive", "Книги и тексты", "Показывает книги, тексты и журнальные записи.", "Просмотрите найденные тексты и книги."),
            ["storage_access"] = M("world", "Доступ к хранилищам", "Показывает известные хранилища и доступ к ним.", "Проверьте, какие хранилища сейчас доступны."),
            ["interactions"] = M("world", "Взаимодействия", "Показывает важные взаимодействия игроков и мира.", "Просмотрите текущие взаимодействия."),
            ["ink_feather_reveal_fate"] = M("soul", "Открыть Судьбу", "Открывает предстоящие кубики и базу судьбы за Чернильные Перья.", "Проверьте стоимость и подтвердите раскрытие судьбы."),
            ["ink_feather_rewrite_fate"] = M("soul", "Переписать Судьбу", "Перебрасывает уже открытую судьбу за увеличенную цену в Чернильных Перьях.", "Проверьте стоимость и подтвердите переписывание судьбы."),
            ["distribute"] = M("soul", "Распределить характеристики", "Открывает игровую форму распределения свободных очков.", "Укажите, какие характеристики нужно повысить и почему."),
            ["companion_directive"] = M("world", "Директива компаньону", "Готовит поручение компаньону через безопасную форму.", "Опишите поручение, адресата и желаемый результат."),
            ["faction_directive"] = M("factions", "Директива фракции", "Готовит поручение союзной фракции через безопасную форму.", "Опишите фракцию, цель поручения и ограничения."),
            ["npc_trade"] = M("world", "Торговля с НПС", "Открывает торговлю с выбранным смертным торговцем через безопасную форму.", "Выберите покупку, продажу или обратный выкуп и подтвердите сделку."),
            ["craft"] = M("world", "Ремесло", "Готовит ремесленное действие или запрос создания предмета.", "Опишите предмет, материалы и желаемое качество работы."),
            ["chaos_sea"] = M("afterlife", "Море Хаоса", "Показывает сводку посмертного моря и доступные возможности.", "Откройте сводку Моря Хаоса."),
            ["guardians"] = M("guardians", "Хранители", "Показывает хранителей, их статусы и связи с душой.", "Просмотрите хранителей и их текущее влияние."),
            ["abode_power"] = M("afterlife", "Сила обители", "Показывает силу обители и связанные посмертные ресурсы.", "Проверьте силу обители."),
            ["guardian_projects"] = M("guardians", "Проекты хранителей", "Показывает проекты хранителей в Море Хаоса.", "Просмотрите проекты хранителей."),
            ["guardian_politics"] = M("guardians", "Политика хранителей", "Показывает политические связи и напряжения хранителей.", "Откройте политическую сводку хранителей."),
            ["abodes"] = M("afterlife", "Обители", "Показывает известные обители и их состояние.", "Просмотрите доступные обители."),
            ["gacha"] = M("afterlife", "Призыв судьбы", "Показывает посмертные возможности случайного призыва и наград.", "Просмотрите доступные посмертные призывы."),
            ["abode_offering"] = M("afterlife", "Подношение обители", "Готовит подношение обители через безопасную форму.", "Опишите подношение, адресата и ожидаемый духовный смысл."),
            ["archive_consultation"] = M("archive", "Архивная консультация", "Готовит консультацию по свободной записи Архива у дружественного Хранителя.", "Выберите запись Архива, Хранителя и подтвердите запрос консультации."),
            ["archive_project_fuel"] = M("archive", "Подпитка проекта Архивом", "Вкладывает свободную запись Архива в активный проект дружественного Хранителя.", "Выберите запись Архива, Хранителя с активным проектом и подтвердите подпитку."),
            ["found_guardian_mantle"] = M("guardians", "Учредить хранителя", "Готовит основание новой мантии хранителя.", "Опишите имя, домен и обязанности новой мантии хранителя."),
            ["guardian_trade"] = M("guardians", "Торговля хранителя", "Открывает торговлю с текущим Хранителем через безопасную форму.", "Выберите запрос витрины, покупку, продажу или обратный выкуп и подтвердите сделку."),
            ["guardian_social"] = M("guardians", "Общение с Хранителем", "Открывает форму разговора с Хранителем или просьбы о знаниях.", "Выберите Хранителя и тип обращения."),
            ["abode_residents"] = M("afterlife", "Обитатели Обители", "Запрашивает у ГМ состав обитателей выбранной Обители.", "Выберите Обитель, для которой нужен состав обитателей."),
            ["resident_interaction"] = M("afterlife", "Общение с обитателем", "Открывает форму разговора с обитателем Обители или просьбы раскрыть его историю.", "Выберите обитателя и тип обращения."),
            ["resident_transfer"] = M("afterlife", "Переход обитателя", "Готовит просьбу о переходе готового обитателя в другую Обитель или уходе.", "Выберите обитателя и направление перехода."),
            ["soul_relic_equip"] = M("afterlife", "Экипировать реликвию", "Готовит экипировку реликвии души из хранилища.", "Выберите реликвию, слот и подтвердите экипировку."),
            ["soul_relic_unequip"] = M("afterlife", "Снять реликвию", "Готовит снятие экипированной реликвии обратно в хранилище.", "Выберите слот и подтвердите снятие."),
            ["shining_abode"] = M("afterlife", "Сияющая Обитель", "Показывает состояние Сияющей Обители и доступные залы.", "Откройте сводку Сияющей Обители."),
            ["shining_politics"] = M("factions", "Политика Сияния", "Показывает фракции и политику Сияющей Обители.", "Просмотрите политические силы Сияющей Обители."),
            ["shining_faction_founding"] = M("factions", "Основать сияющую фракцию", "Открывает форму основания новой фракции Сияющей Обители.", "Заполните хартию, зал, службы и сторонников новой фракции."),
            ["shining_faction_realignment"] = M("factions", "Перестроить фракцию", "Готовит просьбу о переходе готового обитателя между сияющими фракциями или в нейтралитет.", "Выберите обитателя, режим перехода и целевую фракцию."),
            ["shining_faction_leadership"] = M("factions", "Сменить главу фракции", "Готовит просьбу о передаче власти, наследовании или мятеже внутри сияющей фракции.", "Выберите фракцию, режим смены власти, кандидата и сторонников."),
            ["shining_native_faction_discovery"] = M("factions", "Открыть нативную фракцию", "Готовит открытие новой нативной фракции Сияющей Обители.", "Проверьте стоимость в Перьях и Искрах, затем подтвердите просьбу."),
            ["shining_faction_investment"] = M("factions", "Вложиться во фракцию", "Готовит инвестицию в видимую сияющую фракцию.", "Выберите фракцию, проверьте стоимость и подтвердите вложение."),
            ["shining_project_support"] = M("factions", "Поддержать проект", "Готовит поддержку завершённого проекта сияющей фракции.", "Выберите проект, который станет поддерживаемым."),
            ["shining_project_unsupport"] = M("factions", "Снять поддержку проекта", "Готовит снятие поддержки с сияющего проекта.", "Выберите поддерживаемый проект, с которого нужно снять поддержку."),
            ["shining_project_retirement"] = M("factions", "Отправить проект в историю", "Готовит завершённый сияющий проект к уходу в историю.", "Выберите проект, который больше не должен быть активным вкладом фракции."),
            ["shining_gates_open"] = M("afterlife", "Открыть Врата", "Готовит открытие Врат инкарнации для набора благословений.", "Подтвердите просьбу к ГМ показать благословения Врат."),
            ["shining_gates_select"] = M("afterlife", "Выбрать благословение", "Открывает выбор благословения из текущего набора Врат.", "Выберите карту благословения и подтвердите выбор."),
            ["shining_gates_deselect"] = M("afterlife", "Снять благословение", "Открывает снятие уже выбранного благословения Врат.", "Выберите благословение, которое нужно убрать из будущей жизни."),
            ["shining_gates_reroll"] = M("afterlife", "Обновить Врата", "Готовит обновление доступных благословений текущего набора Врат.", "Проверьте последствия обновления и подтвердите действие."),
            ["shining_incarnation_prepare"] = M("afterlife", "Подготовить новую жизнь", "Готовит передачу выбранных благословений в новую жизнь.", "Проверьте выбранные благословения и подтвердите подготовку новой жизни."),
            ["shining_relic_forge"] = M("afterlife", "Ковка реликвий", "Готовит просьбу сияющей фракции перековать реликвию души.", "Выберите фракцию, действие ковки, реликвию и нужные параметры."),
            ["shining_trade"] = M("factions", "Сияющая торговля", "Открывает торговлю с выбранной сияющей фракцией через безопасную форму.", "Выберите запрос витрины или покупку из готовой витрины и подтвердите действие."),
            ["shining_treasury"] = M("afterlife", "Казначейство", "Готовит действие с казной Сияющей Обители.", "Опишите ресурс, операцию и причину изменения казны."),
            ["source_of_light"] = M("afterlife", "Источник Света", "Готовит действие у Источника Света Сияющей Обители.", "Опишите просьбу к Источнику Света и желаемый эффект."),
            ["afterlife_profiles"] = M("afterlife", "Профили посмертия", "Показывает профили сущностей посмертия и их прогрессию.", "Просмотрите известных посмертных сущностей."),
            ["afterlife_threats"] = M("combat", "Угрозы посмертия", "Показывает видимые угрозы посмертия и давление на душу.", "Проверьте активные угрозы посмертия."),
            ["afterlife_chronicles"] = M("afterlife", "Хроники посмертия", "Показывает события, участников и последствия посмертных сцен.", "Просмотрите хроники посмертия без изменения состояния."),
            ["afterlife_inbox"] = M("afterlife", "Уведомления посмертия", "Открывает уведомления и ответы посмертных систем.", "Просмотрите уведомления посмертия и отметьте, что требует внимания."),
            ["spiritual_conflict"] = M("combat", "Духовный конфликт", "Показывает состояние текущего духовного конфликта.", "Просмотрите духовный конфликт и стороны столкновения."),
            ["spiritual_combat_log"] = M("combat", "Журнал духовного боя", "Показывает журнал духовного боя и недавние события.", "Откройте журнал духовного боя."),
            ["spiritual_combat_help"] = M("combat", "Помощь духовного боя", "Показывает правила и подсказки духовного боя.", "Откройте справку по духовному бою."),
            ["spiritual_arts"] = M("combat", "Духовные искусства", "Готовит развитие или применение духовных искусств.", "Опишите духовное искусство, цель тренировки или желаемое применение."),
            ["spiritual_action"] = M("combat", "Духовное действие", "Готовит действие внутри активного духовного конфликта.", "Опишите духовное действие, цель, ставку и ожидаемый эффект.")
        };

    public static BrowserPlayerCommandMenuDto Build(
        AggregatedGameState state,
        BrowserLifecycleDashboardDto lifecycle,
        QteWebStateDto qte)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(qte);

        var actionsBySection = SectionDefinitions.ToDictionary(
            static section => section.Id,
            static _ => new List<BrowserPlayerCommandActionDto>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in ExplorerCommandCatalog.Descriptors
                     .Where(static descriptor => ExplorerCommandMigrationRegistry.IsBrowserExecutable(descriptor.BrowserStatus)))
        {
            if (!Metadata.TryGetValue(descriptor.Id, out var metadata))
                throw new InvalidOperationException($"Browser player action metadata is missing for command '{descriptor.Id}'.");

            var sectionId = AdvancedOnlyIds.Contains(descriptor.Id) ? "advanced" : metadata.SectionId;
            if (!actionsBySection.ContainsKey(sectionId))
                throw new InvalidOperationException($"Browser player action metadata for command '{descriptor.Id}' references unknown section '{sectionId}'.");

            var playerDefault = !AdvancedOnlyIds.Contains(descriptor.Id);
            var availability = ResolveRealmAvailability(descriptor, state);
            var enabled = availability.Enabled;
            var disabledReason = availability.DisabledReason;

            if (descriptor.MutationMode == ExplorerCommandMutationMode.LocalTurn && enabled)
            {
                var writeGate = ResolveLocalWriteGate(lifecycle, qte);
                enabled = writeGate.Enabled;
                disabledReason = writeGate.DisabledReason;
            }

            if (string.Equals(descriptor.Id, "spiritual_action", StringComparison.OrdinalIgnoreCase))
            {
                enabled = false;
                disabledReason = "Нужен активный духовный конфликт: действие станет доступно, когда конфликт появится в состоянии посмертия.";
            }

            var isMutating = descriptor.MutationMode == ExplorerCommandMutationMode.LocalTurn;
            actionsBySection[sectionId].Add(new BrowserPlayerCommandActionDto(
                Id: descriptor.Id,
                SectionId: sectionId,
                Label: metadata.Label,
                Description: metadata.Description,
                RealmAvailability: availability.RealmAvailability,
                Enabled: enabled,
                DisabledReason: enabled ? string.Empty : disabledReason,
                PlayerDefault: playerDefault,
                MutationMode: MutationModeLabel(descriptor.MutationMode),
                MutationWarning: isMutating
                    ? "Может изменить локальные файлы хода; перед записью проверяются блокировки, активный ход и быстрые сцены."
                    : "Только просмотр: состояние игры не изменяется.",
                FormMode: isMutating ? "guided-form" : "none",
                FormLabel: isMutating ? "Подготовить форму" : "Открыть раздел",
                FormPrompt: metadata.FormPrompt,
                AdvancedCommand: descriptor.PrimaryAlias));
        }

        var sections = SectionDefinitions
            .Select(section => new BrowserPlayerCommandSectionDto(
                Id: section.Id,
                Label: section.Label,
                Description: section.Description,
                PlayerDefault: section.PlayerDefault,
                Actions: actionsBySection[section.Id]))
            .ToArray();

        return new BrowserPlayerCommandMenuDto(SchemaVersion, sections);
    }

    public static BrowserPlayerCommandCoverageMetadata GetCoverageMetadata(ExplorerCommandDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!Metadata.TryGetValue(descriptor.Id, out var metadata))
            throw new InvalidOperationException($"Browser player action metadata is missing for command '{descriptor.Id}'.");

        var isAdvancedOnly = AdvancedOnlyIds.Contains(descriptor.Id);
        var isMutating = descriptor.MutationMode == ExplorerCommandMutationMode.LocalTurn;
        var sectionId = isAdvancedOnly ? "advanced" : metadata.SectionId;
        var formMode = isMutating ? "guided-form" : "none";

        return new BrowserPlayerCommandCoverageMetadata(
            SectionId: sectionId,
            Label: metadata.Label,
            PlayerDefault: !isAdvancedOnly,
            Surface: isAdvancedOnly ? "advanced-only" : "player-default",
            FormMode: formMode,
            UxDecision: ResolveUxDecision(descriptor.BrowserStatus, isAdvancedOnly, isMutating));
    }

    private static Availability ResolveRealmAvailability(ExplorerCommandDescriptor descriptor, AggregatedGameState state)
    {
        if (string.Equals(descriptor.Id, "world_setup", StringComparison.OrdinalIgnoreCase))
        {
            return state.IsInAfterlifeRealm
                ? new Availability(true, string.Empty, "Доступно в посмертии для подготовки следующей смертной жизни.")
                : new Availability(false, "Подготовка следующего мира доступна только в Море Хаоса или Сияющей Обители.", "Доступно в посмертии для подготовки следующей смертной жизни.");
        }

        if (descriptor.Id is "guardian_social" or "abode_residents" or "resident_interaction" or "resident_transfer" or "archive_consultation" or "archive_project_fuel")
        {
            return state.IsInAfterlifeRealm
                ? new Availability(true, string.Empty, "Доступно в посмертии.")
                : new Availability(false, "Это действие доступно только в посмертии.", "Доступно в посмертии.");
        }

        return descriptor.Group switch
        {
            ExplorerCommandGroup.MortalWorld => state.IsInAfterlifeRealm
                ? new Availability(false, "Это действие доступно в смертном мире; сейчас душа находится в посмертии.", "Доступно в смертном мире.")
                : new Availability(true, string.Empty, "Доступно в смертном мире."),
            ExplorerCommandGroup.ChaosSea => state.IsInChaosSea
                ? new Availability(true, string.Empty, "Доступно в Море Хаоса.")
                : new Availability(false, "Это действие доступно только в Море Хаоса.", "Доступно только в Море Хаоса."),
            ExplorerCommandGroup.ShiningAbode => state.IsInShiningAbode
                ? new Availability(true, string.Empty, "Доступно в Сияющей Обители.")
                : new Availability(false, "Это действие доступно только в Сияющей Обители.", "Доступно только в Сияющей Обители."),
            ExplorerCommandGroup.AfterlifeCombatAndEntities => state.IsInAfterlifeRealm
                ? new Availability(true, string.Empty, "Доступно в посмертии; духовное действие дополнительно требует активный духовный конфликт.")
                : new Availability(false, "Это действие доступно только в посмертии.", "Доступно только в посмертии."),
            ExplorerCommandGroup.Lifecycle => new Availability(true, string.Empty, "Доступно, когда протокол безопасности хода разрешает операцию."),
            ExplorerCommandGroup.SarefStory => new Availability(true, string.Empty, "Доступно из личной истории Сарефа, если соответствующая сюжетная линия открыта."),
            _ => new Availability(true, string.Empty, "Доступно в текущем игровом режиме.")
        };
    }

    private static Availability ResolveLocalWriteGate(BrowserLifecycleDashboardDto lifecycle, QteWebStateDto qte)
    {
        if (qte.State == "Failed")
        {
            return new Availability(
                false,
                qte.Error ??
                "Состояние QTE требует проверки перед локальной записью.",
                "Локальная форма доступна после восстановления состояния QTE.");
        }

        if (qte.State is "Offer" or "Active")
        {
            var detail = qte.Notification ?? qte.Offer?.OfferText ?? qte.ActiveScene?.Title ?? "активная быстрая сцена";
            return new Availability(false, $"Активна быстрая сцена: завершите её перед локальной формой. {detail}", "Локальная форма доступна после завершения быстрой сцены.");
        }

        if (!lifecycle.CanStartBrowserWrite)
        {
            var reason = lifecycle.Guidance.FirstOrDefault()?.Message;
            if (string.IsNullOrWhiteSpace(reason))
                reason = lifecycle.PendingTurn.Message;
            if (string.IsNullOrWhiteSpace(reason))
                reason = "Локальная запись сейчас заблокирована протоколом безопасности хода.";
            return new Availability(false, reason, "Локальная форма доступна, когда протокол безопасности хода разрешает запись из браузера.");
        }

        return new Availability(true, string.Empty, "Локальная форма доступна: протокол безопасности хода разрешает запись из браузера.");
    }

    private static string MutationModeLabel(ExplorerCommandMutationMode mode) =>
        mode switch
        {
            ExplorerCommandMutationMode.LocalTurn => "local-turn",
            ExplorerCommandMutationMode.Diagnostics => "diagnostics",
            _ => "read-only"
        };

    private static string ResolveUxDecision(
        ExplorerCommandMigrationStatus status,
        bool isAdvancedOnly,
        bool isMutating) =>
        status switch
        {
            ExplorerCommandMigrationStatus.ReadOnlyParity when isAdvancedOnly => "advanced-diagnostics",
            ExplorerCommandMigrationStatus.ReadOnlyParity => "contextual-button",
            ExplorerCommandMigrationStatus.MutatingParity when isAdvancedOnly => "advanced-diagnostics",
            ExplorerCommandMigrationStatus.MutatingParity when isMutating => "guided-form",
            ExplorerCommandMigrationStatus.MutatingParity => "contextual-button",
            ExplorerCommandMigrationStatus.InteractiveFormPending when isAdvancedOnly => "advanced-diagnostics",
            ExplorerCommandMigrationStatus.InteractiveFormPending => "guided-form-pending",
            ExplorerCommandMigrationStatus.StatusOnly when isAdvancedOnly => "advanced-diagnostics",
            ExplorerCommandMigrationStatus.StatusOnly => "status-card",
            ExplorerCommandMigrationStatus.Planned => "planned",
            ExplorerCommandMigrationStatus.Blocked => "blocked",
            ExplorerCommandMigrationStatus.ConsoleOnlyTemporarily => "console-only",
            _ => "planned"
        };

    private static ActionMetadata M(string sectionId, string label, string description, string formPrompt) =>
        new(sectionId, label, description, formPrompt);


    private sealed record SectionDefinition(string Id, string Label, string Description, bool PlayerDefault);
    private sealed record ActionMetadata(string SectionId, string Label, string Description, string FormPrompt);
    private sealed record Availability(bool Enabled, string DisabledReason, string RealmAvailability);
}

public sealed record BrowserPlayerCommandMenuDto(
    int SchemaVersion,
    IReadOnlyList<BrowserPlayerCommandSectionDto> Sections);

public sealed record BrowserPlayerCommandSectionDto(
    string Id,
    string Label,
    string Description,
    bool PlayerDefault,
    IReadOnlyList<BrowserPlayerCommandActionDto> Actions);

public sealed record BrowserPlayerCommandActionDto(
    string Id,
    string SectionId,
    string Label,
    string Description,
    string RealmAvailability,
    bool Enabled,
    string DisabledReason,
    bool PlayerDefault,
    string MutationMode,
    string MutationWarning,
    string FormMode,
    string FormLabel,
    string FormPrompt,
    string AdvancedCommand);

public sealed record BrowserPlayerCommandCoverageMetadata(
    string SectionId,
    string Label,
    bool PlayerDefault,
    string Surface,
    string FormMode,
    string UxDecision);
