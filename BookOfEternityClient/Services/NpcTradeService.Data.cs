using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.Services;

public sealed partial class NpcTradeService
{
    private enum GenerationTradeTier
    {
        Poor,
        Standard,
        Good,
        Premium,
        Elite
    }

    private enum PricingTradeTier
    {
        Hostile,
        Wary,
        Neutral,
        Warm,
        Trusted
    }

    private sealed record MerchantProfile(
        string Key,
        string DisplayName,
        string[] CategoryTags,
        string[] BonusStats,
        string[] ActionBonuses,
        string[] ProfileFlavors);

    private sealed record TradeItemTemplate(
        string TemplateId,
        string TradeItemClass,
        string[] CategoryTags,
        string Type,
        string Group,
        string[] ItemNames,
        bool Stackable,
        int InitialCount,
        bool IsConsumable,
        bool IsContainer,
        int? Capacity,
        string? EquipmentSlot,
        int BasePrice,
        bool AllowsBonuses,
        string[] DescriptionSnippets,
        int WeightGrams);

    public sealed record NpcTradeOffer(
        string SlotId,
        string Name,
        string Rarity,
        int Price,
        string Description,
        string MerchantProfile,
        bool SoldOut,
        JsonObject ItemData);

    public sealed record NpcTradeView(
        string NpcId,
        string NpcName,
        string MerchantProfile,
        string MerchantProfileDisplay,
        int NpcTrade,
        int PlayerTrade,
        int RelationshipLevel,
        int CurrentMoney,
        bool TradeBlocked,
        string? BlockReason,
        string TradeCycleId,
        bool InventoryReady,
        bool InventoryRequestPending,
        bool InventoryRequestCreatedThisCall,
        string? InventoryStatusMessage,
        string? PendingGmAction,
        int CurrentWorldTimeMinutes,
        int GeneratedAtWorldTimeMinutes,
        int RefreshAfterWorldTimeMinutes,
        IReadOnlyList<NpcTradeOffer> Offers,
        IReadOnlyList<NpcBuybackOffer> BuybackOffers);

    public sealed record NpcSellOffer(
        string ItemId,
        string Name,
        string Rarity,
        int Price,
        string Description,
        JsonObject ItemData);

    public sealed record NpcBuybackOffer(
        string BuybackEntryId,
        string ItemId,
        string Name,
        string Rarity,
        int Price,
        int SoldForPrice,
        int SoldAtTurn,
        string Description,
        JsonObject ItemData);

    public sealed record NpcTradeOperationResult(bool Success, bool StateChanged, string Message);

    public sealed record NpcTradeTarget(
        string NpcId,
        string NpcName,
        string MerchantProfileDisplay,
        string LocationName,
        bool TradeAvailable,
        string? BlockReason);

    internal readonly record struct NpcTradeAvailability(
        string? MerchantProfile,
        string MerchantProfileDisplay,
        bool TradeAvailable,
        string? BlockReason)
    {
        public bool IsMerchant => !string.IsNullOrWhiteSpace(MerchantProfile);
    }

    private const string DefaultMerchantProfileKey = "GeneralGoods";

    private static readonly IReadOnlyDictionary<string, string> MerchantProfileAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GeneralGoods"] = "GeneralGoods",
            ["Equipment"] = "Equipment",
            ["CraftingSupplies"] = "CraftingSupplies",
            ["Consumables"] = "Consumables",
            ["KnowledgeAndMedia"] = "KnowledgeAndMedia",
            ["LuxuryAndDecor"] = "LuxuryAndDecor",
            ["ArtifactsAndCurios"] = "ArtifactsAndCurios",
            ["TechnicalGoods"] = "TechnicalGoods",
            ["IllicitGoods"] = "IllicitGoods",
            ["Blacksmith"] = "Equipment",
            ["Alchemist"] = "Consumables",
            ["Scholar"] = "KnowledgeAndMedia",
            ["Outfitter"] = "Equipment",
            ["Curio"] = "ArtifactsAndCurios",
            ["Smuggler"] = "IllicitGoods"
        };

    private static readonly IReadOnlyDictionary<string, MerchantProfile> MerchantProfiles =
        new Dictionary<string, MerchantProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["GeneralGoods"] = new(
                "GeneralGoods", "Товары общего назначения",
                new[] { "general", "consumable", "knowledge", "luxury" },
                new[] { Characteristics.Trade, Characteristics.Perception, Characteristics.Luck },
                new[] { "utilityBonus", "tradeBonus", "travelBonus" },
                new[] { "Пригодится в дороге, хозяйстве и повседневных делах.", "Обычно это добротные товары на каждый день." }),
            ["Equipment"] = new(
                "Equipment", "Снаряжение и экипировка",
                new[] { "equipment", "general" },
                new[] { Characteristics.Strength, Characteristics.Dexterity, Characteristics.Constitution },
                new[] { "combatBonus", "durabilityBonus", "mobilityBonus" },
                new[] { "Сюда попадает практичная экипировка для выживания и работы.", "Ассортимент делает ставку на надёжность, защиту и удобство ношения." }),
            ["CraftingSupplies"] = new(
                "CraftingSupplies", "Материалы и расходники",
                new[] { "crafting", "technical", "general" },
                new[] { Characteristics.Intelligence, Characteristics.Trade, Characteristics.Perception },
                new[] { "craftingBonus", "repairBonus", "resourceBonus" },
                new[] { "Хорошо подходит для мастерских, ремонта и сборки.", "Такие товары берут ради снабжения, а не ради боевых бонусов." }),
            ["Consumables"] = new(
                "Consumables", "Расходуемые товары",
                new[] { "consumable", "general" },
                new[] { Characteristics.Constitution, Characteristics.Wisdom, Characteristics.Speed },
                new[] { "healingBonus", "enduranceBonus", "recoveryBonus" },
                new[] { "Это запасы, которые удобно использовать быстро и по делу.", "Основная ценность здесь в непосредственной пользе и доступности." }),
            ["KnowledgeAndMedia"] = new(
                "KnowledgeAndMedia", "Документы и знания",
                new[] { "knowledge", "general" },
                new[] { Characteristics.Intelligence, Characteristics.Perception, Characteristics.Wisdom },
                new[] { "knowledgeBonus", "researchBonus", "focusBonus" },
                new[] { "Ассортимент помогает с навигацией, чтением, архивами и справками.", "Такие товары часто нужны для подготовки, расследований и обучения." }),
            ["LuxuryAndDecor"] = new(
                "LuxuryAndDecor", "Роскошь и декор",
                new[] { "luxury", "general" },
                new[] { Characteristics.Attractiveness, Characteristics.Persuasion, Characteristics.Luck },
                new[] { "socialBonus", "comfortBonus", "prestigeBonus" },
                new[] { "Здесь важны статус, атмосфера и визуальное впечатление.", "Эти товары чаще покупают ради быта, подарков и обстановки." }),
            ["ArtifactsAndCurios"] = new(
                "ArtifactsAndCurios", "Артефакты и диковины",
                new[] { "curio", "luxury", "knowledge" },
                new[] { Characteristics.Luck, Characteristics.Intelligence, Characteristics.Perception },
                new[] { "fortuneBonus", "loreBonus", "utilityBonus" },
                new[] { "Это необычные вещи с историей, редким происхождением или странной репутацией.", "Ассортимент строится вокруг редкостей, находок и памятных предметов." }),
            ["TechnicalGoods"] = new(
                "TechnicalGoods", "Технические товары",
                new[] { "technical", "crafting" },
                new[] { Characteristics.Intelligence, Characteristics.Perception, Characteristics.Dexterity },
                new[] { "analysisBonus", "repairBonus", "precisionBonus" },
                new[] { "Такие товары ценят за точность, диагностику и обслуживание сложных систем.", "Здесь встречаются детали, приборы и рабочие технические комплекты." }),
            ["IllicitGoods"] = new(
                "IllicitGoods", "Теневой рынок",
                new[] { "illicit", "technical", "curio" },
                new[] { Characteristics.Dexterity, Characteristics.Luck, Characteristics.Trade },
                new[] { "stealthBonus", "escapeBonus", "tradeBonus" },
                new[] { "Эти товары предназначены для обходных схем, серых сделок и скрытного применения.", "Ассортимент держится на неприметности, редкости и неофициальном происхождении." })
        };

    private static readonly TradeItemTemplate[] Templates =
    {
        new("supply_crate", "Functional", new[] { "general", "technical" }, "Container", "Контейнеры",
            new[] { "Контейнер снабжения", "Дорожный ящик", "Универсальный кофр" }, false, 1, false, true, 12, null, 65, false,
            new[] { "Подходит для хранения, перевозки и сортировки обычных вещей.", "Удобен для запасов, инструментов и бытового имущества." }, 3200),
        new("utility_kit", "Functional", new[] { "general", "crafting" }, "Tool", "Инструменты",
            new[] { "Хозяйственный набор", "Набор мелкого ремонта", "Комплект полевых принадлежностей" }, false, 1, false, false, null, null, 55, true,
            new[] { "Полезен для мелких работ, регулировки крепежа и повседневного обслуживания.", "Помогает решать обычные задачи без обращения в мастерскую." }, 1100),
        new("fabric_roll", "Material", new[] { "general", "crafting", "luxury" }, "Material", "Материалы",
            new[] { "Рулон складской ткани", "Плотный отрез материала", "Упаковка технического текстиля" }, true, 1, true, false, null, null, 24, false,
            new[] { "Подходит для ремонта одежды, упаковки, перевязки и пошива.", "Это типичное сырьё для мастерских, лавок и бытовых нужд." }, 900),
        new("route_map", "FlavorOrUtility", new[] { "general", "knowledge" }, "Document", "Документы и медиа",
            new[] { "Карта окрестных маршрутов", "Памятка локальных дорог", "Схема торгового района" }, false, 1, false, false, null, null, 18, false,
            new[] { "Полезна для навигации, адресов и обычной городской логистики.", "Содержит повседневные сведения, которые ценны именно в текущем регионе." }, 150),
        new("reinforced_jacket", "Functional", new[] { "equipment" }, "Armor", "Защита",
            new[] { "Усиленная куртка", "Служебный жилет", "Защитная накидка" }, false, 1, false, false, null, "body", 92, true,
            new[] { "Рассчитан на повседневную защиту и длительное ношение.", "Балансирует между комфортом, практичностью и базовой защитой." }, 2200),
        new("service_boots", "Functional", new[] { "equipment", "general" }, "Armor", "Защита",
            new[] { "Полевые ботинки", "Служебные сапоги", "Устойчивые рабочие ботинки" }, false, 1, false, false, null, "feet", 70, true,
            new[] { "Подходят для долгих переходов, смен и тяжёлой повседневной работы.", "Обычно выбираются за прочность и удобство передвижения." }, 1600),
        new("work_gloves", "Functional", new[] { "equipment", "crafting" }, "Armor", "Защита",
            new[] { "Рабочие перчатки", "Усиленные краги", "Защитные рукавицы" }, false, 1, false, false, null, "hands", 42, true,
            new[] { "Защищают руки и помогают при обслуживании оборудования или ручной работе.", "Подходят для повседневного труда, ремонта и грубой эксплуатации." }, 450),
        new("utility_blade", "Functional", new[] { "equipment", "general" }, "Weapon", "Снаряжение",
            new[] { "Универсальный клинок", "Рабочий нож", "Многоцелевой резак" }, false, 1, false, false, null, "mainHand", 88, true,
            new[] { "Полезен как инструмент и как средство самообороны.", "Часто используется там, где один предмет должен закрывать сразу несколько задач." }, 850),
        new("signal_pendant", "FlavorOrUtility", new[] { "equipment", "curio" }, "Accessory", "Аксессуары",
            new[] { "Сигнальный кулон", "Именной жетон", "Опознавательный медальон" }, false, 1, false, false, null, "neck", 58, true,
            new[] { "Служит для идентификации, связи или просто удобного ношения при себе.", "Обычно ценится за практичность, привычку и личный смысл." }, 180),
        new("fastener_case", "Material", new[] { "crafting", "technical" }, "Material", "Материалы",
            new[] { "Ящик крепежа", "Комплект метизов", "Набор универсальных фиксаторов" }, true, 1, true, false, null, null, 18, false,
            new[] { "Подходит для сборки, ремонта, укрепления и замены расходников.", "Это типичный запас для мастерской, склада или выездной работы." }, 650),
        new("sealant_pack", "Material", new[] { "crafting", "technical" }, "Material", "Материалы",
            new[] { "Пакет герметика", "Упаковка ремонтной смолы", "Набор уплотняющего состава" }, true, 1, true, false, null, null, 22, false,
            new[] { "Полезен при ремонте швов, корпусов, упаковки и технических узлов.", "Обычно его держат под рукой для обслуживания и срочных латок." }, 500),
        new("reagent_set", "Material", new[] { "crafting", "consumable" }, "Material", "Реагенты",
            new[] { "Комплект реагентов", "Полевой набор составов", "Малая упаковка лабораторных смесей" }, true, 1, true, false, null, null, 44, true,
            new[] { "Подходит для проверки веществ, подготовки составов и базовых ремесленных операций.", "Чаще всего нужен для точных работ, где важно качество расходников." }, 550),
        new("wire_bundle", "Material", new[] { "crafting", "technical" }, "Material", "Материалы",
            new[] { "Связка проводки", "Моток кабеля", "Комплект соединительных жил" }, true, 1, true, false, null, null, 28, false,
            new[] { "Используется для сборки, подключения и быстрого ремонта оборудования.", "Нужен там, где приходится что-то подпаивать, перекидывать или замыкать на месте." }, 750),
        new("parts_bundle", "Material", new[] { "crafting", "technical" }, "Component", "Технические товары",
            new[] { "Пакет запасных деталей", "Упаковка сервисных компонентов", "Набор сменных элементов" }, true, 1, true, false, null, null, 40, false,
            new[] { "Подходит для профилактики, замены узлов и поддержания техники в строю.", "Обычно покупается как рабочий запас, а не как предмет экипировки." }, 700),
        new("ration_pack", "Functional", new[] { "consumable", "general" }, "Consumable", "Расходники",
            new[] { "Походный паёк", "Питательный набор", "Набор долгого хранения" }, true, 1, true, false, null, null, 18, false,
            new[] { "Полезен в дороге, на смене и в затяжных вылазках без нормального снабжения.", "Это обычный расходник, который ценят за предсказуемую практичность." }, 450),
        new("water_flask", "Functional", new[] { "consumable", "general" }, "Consumable", "Расходники",
            new[] { "Фляга с очищенной водой", "Герметичный резерв питья", "Полевой запас воды" }, true, 1, true, false, null, null, 12, false,
            new[] { "Простой товар для базового комфорта, выездов и повседневного снабжения.", "Берут ради надёжности и того, чтобы нужный запас был под рукой." }, 900),
        new("medical_pack", "Functional", new[] { "consumable", "general" }, "Consumable", "Расходники",
            new[] { "Комплект перевязки", "Аптечный набор", "Пакет первой помощи" }, true, 1, true, false, null, null, 42, true,
            new[] { "Нужен для оперативной помощи, обработки мелких травм и экстренной стабилизации.", "Это вещь из категории очевидной пользы: без пафоса, но вовремя." }, 650),
        new("battery_pack", "Functional", new[] { "consumable", "technical" }, "Consumable", "Расходники",
            new[] { "Сменный аккумуляторный блок", "Комплект энергомодулей", "Упаковка резервного питания" }, true, 1, true, false, null, null, 32, true,
            new[] { "Полезен для техники, автономной работы и запасного питания вне базы.", "Чаще всего нужен там, где простой оборудования обходится дороже самого товара." }, 700),
        new("solvent_canister", "Material", new[] { "consumable", "technical", "crafting" }, "Consumable", "Расходники",
            new[] { "Канистра сервисного раствора", "Упаковка очищающего состава", "Флакон технического растворителя" }, true, 1, true, false, null, null, 20, false,
            new[] { "Используется для чистки, подготовки поверхностей и обслуживания механизмов.", "Это рабочий расходник, который редко выглядит эффектно, но постоянно нужен." }, 800),
        new("training_manual", "FlavorOrUtility", new[] { "knowledge", "general" }, "Book", "Документы и медиа",
            new[] { "Учебное руководство", "Практический справочник", "Рабочий мануал" }, false, 1, false, false, null, null, 36, true,
            new[] { "Содержит структурированные заметки, схемы и прикладные советы.", "Полезен для подготовки, сверки стандартов и быстрого поиска нужной информации." }, 700),
        new("permit_packet", "FlavorOrUtility", new[] { "knowledge", "general" }, "Document", "Документы и медиа",
            new[] { "Пакет пропусков", "Комплект служебных бумаг", "Набор регистрационных форм" }, false, 1, false, false, null, null, 26, false,
            new[] { "Нужен там, где бюрократия, контроль доступа и формальные процедуры важнее силы.", "Содержит обычные документы, но их наличие часто экономит массу времени." }, 180),
        new("archive_drive", "FlavorOrUtility", new[] { "knowledge", "technical" }, "Media", "Документы и медиа",
            new[] { "Архивный носитель", "Каталогизированный накопитель", "Портативный медиа-модуль" }, false, 1, false, false, null, null, 60, true,
            new[] { "Подходит для хранения справочных материалов, записей и рабочих архивов.", "Такие вещи покупают ради доступа к данным, а не ради внешнего вида." }, 120),
        new("survey_map", "FlavorOrUtility", new[] { "knowledge", "general" }, "Document", "Документы и медиа",
            new[] { "Съёмочная карта района", "Инженерная схема квартала", "Печатная карта коммуникаций" }, false, 1, false, false, null, null, 22, false,
            new[] { "Полезна для ориентирования, планирования маршрутов и оценки местности.", "Содержит прикладные сведения, которые особенно ценны на незнакомой территории." }, 130),
        new("tea_set", "FlavorOrUtility", new[] { "luxury", "general" }, "Household", "Роскошь и декор",
            new[] { "Керамический чайный набор", "Домашний сервиз", "Набор для приёма гостей" }, false, 1, false, false, null, null, 55, false,
            new[] { "Подходит для гостевого стола, быта и создания аккуратной атмосферы.", "Его ценность не в боевых свойствах, а в статусе, удобстве и впечатлении." }, 2100),
        new("framed_print", "FlavorOrUtility", new[] { "luxury", "general" }, "Decor", "Роскошь и декор",
            new[] { "Оформленный оттиск", "Настенная репродукция", "Коллекционная иллюстрация" }, false, 1, false, false, null, null, 40, false,
            new[] { "Используется для оформления жилья, кабинета или витрины.", "Это товар ради атмосферы, вкуса и привычки окружать себя определёнными вещами." }, 900),
        new("scented_lamp", "FlavorOrUtility", new[] { "luxury", "general" }, "Household", "Роскошь и декор",
            new[] { "Ароматическая лампа", "Салонный светильник", "Домашний источник мягкого света" }, false, 1, false, false, null, null, 48, false,
            new[] { "Подходит для освещения, уюта и спокойной обстановки в помещении.", "Чаще всего берётся ради быта, а не ради прямой механической пользы." }, 1200),
        new("music_box", "FlavorOrUtility", new[] { "luxury", "technical", "curio" }, "Decor", "Роскошь и декор",
            new[] { "Механическая шкатулка", "Музыкальный сувенир", "Настольная шкатулка памяти" }, false, 1, false, false, null, null, 92, true,
            new[] { "Ценится как предмет настроения, интерьера и личной памяти.", "Это вещь для атмосферы, жеста или подарка, а не для прямого выживания." }, 900),
        new("table_clock", "FlavorOrUtility", new[] { "luxury", "technical" }, "Device", "Роскошь и декор",
            new[] { "Настольные часы", "Домашний хронометр", "Кабинетный таймер" }, false, 1, false, false, null, null, 76, true,
            new[] { "Полезен для режима, рабочего ритма и упорядоченного быта.", "Это вещь на стыке функции и обстановки: нужна не всем, но многим оказывается удобной." }, 600),
        new("vintage_compass", "Functional", new[] { "curio", "general", "knowledge" }, "Artifact", "Артефакты и диковины",
            new[] { "Коллекционный компас", "Старинный ориентир", "Компас с необычной шкалой" }, false, 1, false, false, null, null, 90, true,
            new[] { "Полезен в дороге, а ценится ещё и за редкость происхождения.", "Это предмет с историей: одновременно практичный и заметно отличающийся от типового товара." }, 300),
        new("curiosity_lens", "Functional", new[] { "curio", "knowledge", "technical" }, "Artifact", "Артефакты и диковины",
            new[] { "Линза наблюдателя", "Коллекционный объектив", "Редкий увеличительный окуляр" }, false, 1, false, false, null, null, 80, true,
            new[] { "Подходит для осмотра деталей, символов и мелких элементов конструкции.", "Обычно её покупают те, кому нужно соединить любопытство с практической пользой." }, 250),
        new("sealed_curio_box", "FlavorOrUtility", new[] { "curio", "luxury" }, "Container", "Артефакты и диковины",
            new[] { "Запечатанная шкатулка", "Кейс для редкостей", "Футляр необычного образца" }, false, 1, false, true, 4, null, 95, false,
            new[] { "Подходит для хранения памятных, деликатных или просто необычных мелочей.", "Такой товар ценят за саму форму обладания и презентации содержимого." }, 850),
        new("resonant_token", "FlavorOrUtility", new[] { "curio", "luxury" }, "Artifact", "Артефакты и диковины",
            new[] { "Резонансный жетон", "Памятный знак", "Странный карманный токен" }, false, 1, false, false, null, null, 72, true,
            new[] { "Может быть личным символом, сувениром или предметом странной репутации.", "Его покупают не потому, что он обязателен, а потому что у него есть история." }, 140),
        new("diagnostic_scanner", "Functional", new[] { "technical" }, "Device", "Технические товары",
            new[] { "Диагностический сканер", "Портативный анализатор", "Сервисный считыватель" }, false, 1, false, false, null, null, 120, true,
            new[] { "Полезен для поиска неисправностей, проверки узлов и оценки состояния систем.", "Это рабочий инструмент, который ценится за точность и скорость диагностики." }, 650),
        new("precision_toolkit", "Functional", new[] { "technical", "crafting" }, "Tool", "Технические товары",
            new[] { "Точный инструментальный набор", "Сервисный комплект регулировки", "Кейс тонкой настройки" }, false, 1, false, false, null, null, 96, true,
            new[] { "Подходит для точной подгонки, ремонта и обслуживания чувствительных механизмов.", "Его берут ради аккуратной работы там, где грубый инструмент уже не подходит." }, 1300),
        new("sensor_node", "Functional", new[] { "technical" }, "Component", "Технические товары",
            new[] { "Сенсорный узел", "Пакет датчиков", "Модуль наблюдения" }, false, 1, false, false, null, null, 84, true,
            new[] { "Подходит для наблюдения, настройки систем контроля и технического мониторинга.", "Чаще всего покупается под конкретную задачу, а не для общего антуража." }, 480),
        new("servo_unit", "Material", new[] { "technical", "crafting" }, "Component", "Технические товары",
            new[] { "Сервоприводной блок", "Сменный привод", "Исполнительный модуль" }, false, 1, true, false, null, null, 78, false,
            new[] { "Нужен для ремонта, замены узлов и восстановления рабочих механизмов.", "Это типичная техническая покупка: не красивая, зато очень прикладная." }, 1100),
        new("relay_box", "FlavorOrUtility", new[] { "technical", "general" }, "Device", "Технические товары",
            new[] { "Релейный модуль", "Компактный узел связи", "Переходной коммутационный блок" }, false, 1, false, false, null, null, 74, false,
            new[] { "Используется для стыковки линий, связи и технической логистики.", "Такие вещи редко производят впечатление, но часто решают неприятные практические проблемы." }, 420),
        new("forged_pass", "FlavorOrUtility", new[] { "illicit", "knowledge" }, "Document", "Теневые товары",
            new[] { "Поддельный пропуск", "Комплект фальшивых бумаг", "Набор обходной документации" }, false, 1, false, false, null, null, 86, false,
            new[] { "Предназначен для обхода формальных барьеров и лишних вопросов.", "Это товар не для витрины статуса, а для тех, кто предпочитает обходные пути." }, 120),
        new("lockpick_roll", "Functional", new[] { "illicit", "technical" }, "Tool", "Теневые товары",
            new[] { "Скрытый набор отмычек", "Тихий инструмент вскрытия", "Свернутый комплект тонких ключей" }, false, 1, false, false, null, null, 72, true,
            new[] { "Полезен для деликатной работы с замками, защёлками и скрытыми механизмами.", "Обычно выбирается за неприметность, а не за громкое имя или внешний вид." }, 220),
        new("unmarked_ampoule", "Functional", new[] { "illicit", "consumable" }, "Consumable", "Теневые товары",
            new[] { "Немаркированная ампула", "Флакон серого происхождения", "Запечатанный обходной состав" }, true, 1, true, false, null, null, 64, true,
            new[] { "Используется быстро и без лишней огласки, когда официальный путь недоступен.", "Его ценность в скрытности происхождения и простоте применения." }, 120),
        new("signal_jammer", "Functional", new[] { "illicit", "technical" }, "Device", "Теневые товары",
            new[] { "Глушитель сигналов", "Карманный подавитель", "Скрытый модуль помех" }, false, 1, false, false, null, null, 112, true,
            new[] { "Полезен там, где нужно сорвать отслеживание, пометку или передачу данных.", "Это вещь для рискованных задач, в которых заметность сама по себе уже опасна." }, 450),
        new("cache_box", "FlavorOrUtility", new[] { "illicit", "curio" }, "Container", "Теневые товары",
            new[] { "Тайниковый кейс", "Скрытый короб", "Неприметная кассета для хранения" }, false, 1, false, true, 6, null, 88, false,
            new[] { "Подходит для хранения чувствительных мелочей, бумаг и скрываемых предметов.", "Главная ценность здесь в неприметности и удобстве скрытого хранения." }, 1400),
        new("black_chip_set", "Material", new[] { "illicit", "technical" }, "Component", "Теневые товары",
            new[] { "Немаркированный чип-набор", "Комплект серых модулей", "Пакет обходных компонентов" }, true, 1, true, false, null, null, 88, false,
            new[] { "Используется для обходных схем, нестандартного ремонта и неофициальной настройки.", "Такие детали редко бывают красивыми, но часто оказываются незаменимыми." }, 200)
    };
}
