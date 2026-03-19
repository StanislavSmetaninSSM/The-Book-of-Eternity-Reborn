using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed class NpcTradeService
{
    private readonly FileSystemManager _fs;
    private readonly ILogger<NpcTradeService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private const string NpcCorePath = "game_state/npcs/npc_core.json";
    private const string ItemsPath = "game_state/inventory/items.json";
    private const string PlayerStatusPath = "game_state/core/player_status.json";
    private const string WorldTimePath = "game_state/world/world_time.json";
    private const int RefreshWindowMinutes = 30 * 24 * 60;

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
        int CurrentWorldTimeMinutes,
        int GeneratedAtWorldTimeMinutes,
        int RefreshAfterWorldTimeMinutes,
        IReadOnlyList<NpcTradeOffer> Offers);

    public sealed record NpcSellOffer(
        string ItemId,
        string Name,
        string Rarity,
        int Price,
        string Description,
        JsonObject ItemData);

    public sealed record NpcTradeOperationResult(bool Success, bool StateChanged, string Message);

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

    public NpcTradeService(FileSystemManager fs, ILogger<NpcTradeService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task<NpcTradeView?> EnsureTradeInventoryAsync(string npcId)
    {
        var npcRoot = await ReadNpcRootAsync();
        var itemsRoot = await ReadInventoryRootAsync();
        var statusRoot = await ReadPlayerStatusRootAsync();
        if (npcRoot == null || itemsRoot == null || statusRoot == null)
            return null;

        var npc = FindNpcEntry(npcRoot, npcId);
        if (npc == null)
            return null;

        var currentWorldMinutes = await ResolveCurrentWorldMinutesAsync();
        var changed = EnsureNpcTradeInventoryState(npcRoot, npc, statusRoot, currentWorldMinutes, out var view);
        if (changed)
            await _fs.WriteFileAtomicAsync(NpcCorePath, npcRoot.ToJsonString(JsonOpts));

        return view;
    }

    public async Task<IReadOnlyList<NpcSellOffer>> GetSellableItemsAsync(string npcId)
    {
        var npcRoot = await ReadNpcRootAsync();
        var itemsRoot = await ReadInventoryRootAsync();
        var statusRoot = await ReadPlayerStatusRootAsync();
        if (npcRoot == null || itemsRoot == null || statusRoot == null)
            return Array.Empty<NpcSellOffer>();

        var npc = FindNpcEntry(npcRoot, npcId);
        if (npc == null)
            return Array.Empty<NpcSellOffer>();

        if (!NpcTradeAllowedHere(npc, out _))
            return Array.Empty<NpcSellOffer>();

        var npcTrade = ReadNpcTradeValue(npc);
        var playerTrade = await ReadPlayerTradeAsync();
        var relation = ReadNpcRelationshipLevel(npc);
        var pricingTier = GetPricingTier(relation);

        NormalizeInventoryShape(itemsRoot);
        var items = itemsRoot["items"]?.AsArray();
        if (items == null)
            return Array.Empty<NpcSellOffer>();

        var equippedRefs = CollectEquippedItemReferences(itemsRoot);
        return items.OfType<JsonObject>()
            .Where(item => !IsQuestBoundItem(item))
            .Where(item => !IsSoulRelicLikeItem(item))
            .Where(item =>
            {
                var itemId = GetNodeString(item["itemId"]) ?? GetNodeString(item["id"]) ?? GetNodeString(item["existedId"]);
                return string.IsNullOrWhiteSpace(itemId) || !equippedRefs.Contains(itemId);
            })
            .Select(item =>
            {
                var rarity = GetItemRarity(item);
                var baseSellPrice = GetBaseSellPrice(item, rarity);
                return new NpcSellOffer(
                    GetNodeString(item["itemId"]) ?? GetNodeString(item["id"]) ?? GetNodeString(item["existedId"]) ?? "",
                    GetNodeString(item["name"]) ?? "Неизвестный товар",
                    rarity,
                    ComputeSellPrice(baseSellPrice, playerTrade, npcTrade, pricingTier),
                    GetNodeString(item["description"]) ?? "",
                    CloneObject(item));
            })
            .Where(offer => !string.IsNullOrWhiteSpace(offer.ItemId))
            .OrderByDescending(offer => GetRarityRank(offer.Rarity))
            .ThenBy(offer => offer.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<NpcTradeOperationResult> BuyAsync(string npcId, string slotId)
    {
        var npcRoot = await ReadNpcRootAsync();
        var itemsRoot = await ReadInventoryRootAsync();
        var statusRoot = await ReadPlayerStatusRootAsync();
        if (npcRoot == null || itemsRoot == null || statusRoot == null)
            return new NpcTradeOperationResult(false, false, "Не удалось прочитать состояние торговли, инвентаря или денег.");

        var npc = FindNpcEntry(npcRoot, npcId);
        if (npc == null)
            return new NpcTradeOperationResult(false, false, "Торговец не найден.");

        var currentWorldMinutes = await ResolveCurrentWorldMinutesAsync();
        var changed = EnsureNpcTradeInventoryState(npcRoot, npc, statusRoot, currentWorldMinutes, out var view);
        if (view == null)
            return new NpcTradeOperationResult(false, false, "Не удалось подготовить витрину торговца.");
        if (view.TradeBlocked)
            return new NpcTradeOperationResult(false, false, view.BlockReason ?? "Торговля недоступна.");

        if (npc["tradeInventory"] is not JsonObject tradeInventory || tradeInventory["items"] is not JsonArray items)
            return new NpcTradeOperationResult(false, false, "Витрина торговца недоступна.");

        var slot = items.OfType<JsonObject>().FirstOrDefault(item =>
            string.Equals(GetNodeString(item["slotId"]), slotId, StringComparison.OrdinalIgnoreCase));
        if (slot == null)
            return new NpcTradeOperationResult(false, false, "Выбранный товар не найден.");

        if (GetNodeBool(slot["soldOut"]))
            return new NpcTradeOperationResult(false, false, "Этот товар уже выкуплен в текущем ассортименте.");

        var price = GetNodeInt(slot["price"], 0);
        if (price <= 0)
            return new NpcTradeOperationResult(false, false, "Цена товара повреждена.");

        var money = GetNodeInt(statusRoot["money"], 0);
        if (money < price)
            return new NpcTradeOperationResult(false, false, "Недостаточно денег.");

        if (slot["itemData"] is not JsonObject itemData)
            return new NpcTradeOperationResult(false, false, "Данные товара повреждены.");

        NormalizeInventoryShape(itemsRoot);
        var inventoryItems = itemsRoot["items"]!.AsArray();
        UpsertInventoryItem(inventoryItems, CloneObject(itemData));
        statusRoot["money"] = money - price;
        slot["soldOut"] = true;
        SyncNpcEntries(npcRoot, GetNpcIdentity(npc), npc);

        await _fs.WriteFileAtomicAsync(ItemsPath, itemsRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(PlayerStatusPath, statusRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(NpcCorePath, npcRoot.ToJsonString(JsonOpts));

        var itemName = GetNodeString(itemData["name"]) ?? "Товар";
        return new NpcTradeOperationResult(true, true, $"Куплен товар «{itemName}» за {price}.");
    }

    public async Task<NpcTradeOperationResult> SellAsync(string npcId, string itemId)
    {
        var npcRoot = await ReadNpcRootAsync();
        var itemsRoot = await ReadInventoryRootAsync();
        var statusRoot = await ReadPlayerStatusRootAsync();
        if (npcRoot == null || itemsRoot == null || statusRoot == null)
            return new NpcTradeOperationResult(false, false, "Не удалось прочитать состояние торговли, инвентаря или денег.");

        var npc = FindNpcEntry(npcRoot, npcId);
        if (npc == null)
            return new NpcTradeOperationResult(false, false, "Торговец не найден.");

        if (!NpcTradeAllowedHere(npc, out var blockedReason))
            return new NpcTradeOperationResult(false, false, blockedReason ?? "Торговля недоступна.");

        NormalizeInventoryShape(itemsRoot);
        var items = itemsRoot["items"]?.AsArray();
        if (items == null)
            return new NpcTradeOperationResult(false, false, "Инвентарь недоступен.");

        var equippedRefs = CollectEquippedItemReferences(itemsRoot);
        if (equippedRefs.Contains(itemId))
            return new NpcTradeOperationResult(false, false, "Экипированный предмет нельзя продать из этой панели.");

        var itemIndex = FindInventoryItemIndex(items, itemId);
        if (itemIndex < 0)
            return new NpcTradeOperationResult(false, false, "Товар не найден в инвентаре.");

        if (items[itemIndex] is not JsonObject item)
            return new NpcTradeOperationResult(false, false, "Данные товара повреждены.");
        if (IsQuestBoundItem(item))
            return new NpcTradeOperationResult(false, false, "Этот предмет нельзя продать через локальную торговлю.");
        if (IsSoulRelicLikeItem(item))
            return new NpcTradeOperationResult(false, false, "Реликвии души нельзя продать через локальную торговлю НПС.");

        var npcTrade = ReadNpcTradeValue(npc);
        var playerTrade = await ReadPlayerTradeAsync();
        var relation = ReadNpcRelationshipLevel(npc);
        var pricingTier = GetPricingTier(relation);
        var rarity = GetItemRarity(item);
        var baseSellPrice = GetBaseSellPrice(item, rarity);
        var price = ComputeSellPrice(baseSellPrice, playerTrade, npcTrade, pricingTier);
        if (price <= 0)
            return new NpcTradeOperationResult(false, false, "Цена продажи повреждена.");

        items.RemoveAt(itemIndex);
        statusRoot["money"] = GetNodeInt(statusRoot["money"], 0) + price;

        await _fs.WriteFileAtomicAsync(ItemsPath, itemsRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(PlayerStatusPath, statusRoot.ToJsonString(JsonOpts));

        var itemName = GetNodeString(item["name"]) ?? "Товар";
        return new NpcTradeOperationResult(true, true, $"Продан товар «{itemName}» за {price}.");
    }

    internal static bool IsValidGenerationTierCode(string? tierCode) =>
        tierCode is nameof(GenerationTradeTier.Poor)
            or nameof(GenerationTradeTier.Standard)
            or nameof(GenerationTradeTier.Good)
            or nameof(GenerationTradeTier.Premium)
            or nameof(GenerationTradeTier.Elite);

    internal static bool IsValidPricingTierCode(string? tierCode) =>
        tierCode is nameof(PricingTradeTier.Hostile)
            or nameof(PricingTradeTier.Wary)
            or nameof(PricingTradeTier.Neutral)
            or nameof(PricingTradeTier.Warm)
            or nameof(PricingTradeTier.Trusted);

    internal static bool IsRarityAllowedForGenerationTier(string rarity, string tierCode)
    {
        var rarityRank = GetRarityRank(rarity);
        var maxRank = tierCode switch
        {
            nameof(GenerationTradeTier.Poor) => GetRarityRank("Common"),
            nameof(GenerationTradeTier.Standard) => GetRarityRank("Uncommon"),
            nameof(GenerationTradeTier.Good) => GetRarityRank("Rare"),
            nameof(GenerationTradeTier.Premium) => GetRarityRank("Epic"),
            nameof(GenerationTradeTier.Elite) => GetRarityRank("Epic"),
            _ => 0
        };
        return rarityRank <= maxRank;
    }

    internal static bool IsValidMerchantProfileCode(string? profileCode) =>
        TryNormalizeMerchantProfileCode(profileCode, out _);

    internal static bool IsValidTradeItemClassCode(string? tradeItemClass) =>
        tradeItemClass is "Functional" or "Material" or "FlavorOrUtility";

    internal static string GetMerchantProfileDisplayName(string? profileCode)
    {
        if (TryNormalizeMerchantProfileCode(profileCode, out var normalizedProfile) &&
            MerchantProfiles.TryGetValue(normalizedProfile, out var profile))
            return profile.DisplayName;

        return "Товары смертной жизни";
    }

    internal static string GetTradeItemClassDisplayName(string? tradeItemClass) => tradeItemClass switch
    {
        "Functional" => "Функциональный",
        "Material" => "Материальный",
        "FlavorOrUtility" => "Бытовой или утилитарный",
        _ => "Неизвестный"
    };

    internal static string? ResolveMerchantProfileCode(string? explicitProfile, params string?[] sourceParts)
    {
        if (TryNormalizeMerchantProfileCode(explicitProfile, out var normalizedProfile))
            return normalizedProfile;

        var source = string.Join(" ", sourceParts.Where(s => !string.IsNullOrWhiteSpace(s))).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(source))
            return null;

        if (ContainsAny(source, "контраб", "тенев", "подполь", "smugg", "black market", "fence", "fixer"))
            return "IllicitGoods";
        if (ContainsAny(source, "инжен", "техн", "mechanic", "engineer", "technician", "cyber", "электр", "diagnostic", "repair depot"))
            return "TechnicalGoods";
        if (ContainsAny(source, "антиквар", "artifact", "curio", "collect", "relic", "диковин", "коллекц"))
            return "ArtifactsAndCurios";
        if (ContainsAny(source, "декор", "роскош", "luxury", "jewel", "atelier", "tailor", "furniture", "gallery", "salon", "ювел"))
            return "LuxuryAndDecor";
        if (ContainsAny(source, "книг", "архив", "scribe", "scholar", "library", "bookseller", "media", "editor", "printer", "document", "учен", "редак"))
            return "KnowledgeAndMedia";
        if (ContainsAny(source, "аптек", "зель", "alchem", "grocer", "provision", "baker", "cook", "innkeep", "food", "bar", "cafe", "consum"))
            return "Consumables";
        if (ContainsAny(source, "оруж", "брон", "gear", "equipment", "armorer", "smith", "кузнец", "outfit", "quartermaster", "патрон"))
            return "Equipment";
        if (ContainsAny(source, "ремес", "materials", "supplier", "hardware", "workshop", "реагент", "мастерск", "склад", "fabric", "textile"))
            return "CraftingSupplies";
        if (ContainsAny(source, "торгов", "merchant", "trader", "vendor", "shopkeep", "market", "лавк"))
            return "GeneralGoods";

        return null;
    }

    internal static NpcTradeAvailability EvaluateTradeAvailability(JsonElement npc, string currentLocationId, string currentLocationName)
    {
        var merchantProfile = ResolveMerchantProfileCode(
            npc.TryGetProperty("tradeState", out var tradeState) && tradeState.ValueKind == JsonValueKind.Object
                ? GetFirstNonEmptyString(tradeState, "merchantProfile")
                : null,
            GetFirstNonEmptyString(npc, "role"),
            GetFirstNonEmptyString(npc, "occupation"),
            GetFirstNonEmptyString(npc, "class"),
            GetFirstNonEmptyString(npc, "name"));

        return BuildTradeAvailability(
            merchantProfile,
            GetFirstNonEmptyString(npc, "currentLocationId") ?? "",
            GetFirstNonEmptyString(npc, "currentLocation") ?? "",
            currentLocationId,
            currentLocationName,
            npc.TryGetProperty("tradeState", out var tradeStateNode) && tradeStateNode.ValueKind == JsonValueKind.Object
                ? tradeStateNode
                : (JsonElement?)null);
    }

    internal static NpcTradeAvailability EvaluateTradeAvailability(JsonObject npc, string currentLocationId, string currentLocationName)
    {
        var tradeState = npc["tradeState"] as JsonObject;
        var merchantProfile = ResolveMerchantProfileCode(
            GetNodeString(tradeState?["merchantProfile"]),
            GetNodeString(npc["role"]),
            GetNodeString(npc["occupation"]),
            GetNodeString(npc["class"]),
            GetNodeString(npc["name"]));

        return BuildTradeAvailability(
            merchantProfile,
            GetNodeString(npc["currentLocationId"]) ?? "",
            GetNodeString(npc["currentLocation"]) ?? "",
            currentLocationId,
            currentLocationName,
            tradeState);
    }

    internal static int ComputeBuyPriceForValidation(int basePrice, int playerTrade, int npcTrade, string pricingTierCode) =>
        ComputeBuyPrice(basePrice, playerTrade, npcTrade, ParsePricingTierCode(pricingTierCode));

    internal static int ComputeSellPriceForValidation(int baseSellPrice, int playerTrade, int npcTrade, string pricingTierCode) =>
        ComputeSellPrice(baseSellPrice, playerTrade, npcTrade, ParsePricingTierCode(pricingTierCode));

    internal static int ResolveWorldMinutes(JsonElement root)
    {
        if (TryReadIntLike(root, "currentTimeInMinutes", out var absolute))
            return absolute;

        var year = TryReadIntLike(root, "year", out var parsedYear) ? parsedYear : 0;
        var day = TryReadIntLike(root, "day", out var parsedDay)
            ? parsedDay
            : (TryReadIntLike(root, "dayOfMonth", out parsedDay) ? parsedDay : 1);
        var minutes = MapTimeOfDayToMinutes(GetFirstNonEmptyString(root, "timeOfDay") ?? "Morning");
        return Math.Max(0, ((year * 400) + Math.Max(0, day - 1)) * 1440 + minutes);
    }

    private bool EnsureNpcTradeInventoryState(JsonObject root, JsonObject npc, JsonObject statusRoot,
        int currentWorldMinutes, out NpcTradeView? view)
    {
        view = null;
        var npcId = GetNpcIdentity(npc);
        var blocked = !NpcTradeAllowedHere(npc, out var blockedReason);
        var npcTrade = ReadNpcTradeValue(npc);
        var playerTrade = ReadPlayerTradeSync();
        var relation = ReadNpcRelationshipLevel(npc);
        var generationTier = GetGenerationTier(ReadNpcLevel(npc), npcTrade, relation);
        var pricingTier = GetPricingTier(relation);
        var changed = false;

        if (!blocked)
        {
            var inventory = npc["tradeInventory"] as JsonObject;
            if (!TradeInventoryMatchesCurrentContract(inventory, currentWorldMinutes, npc, playerTrade, npcTrade))
            {
                npc["tradeInventory"] = GenerateTradeInventory(npcId, npc, generationTier, pricingTier, playerTrade, npcTrade, currentWorldMinutes);
                changed = true;
            }
            else if (inventory != null)
            {
                changed = RepriceTradeInventory(inventory, playerTrade, npcTrade, pricingTier);
            }

            if (changed)
                SyncNpcEntries(root, npcId, npc);
        }

        view = BuildTradeView(npc, statusRoot, currentWorldMinutes, blocked, blockedReason);
        return changed;
    }

    private NpcTradeView BuildTradeView(JsonObject npc, JsonObject statusRoot, int currentWorldMinutes, bool blocked, string? blockedReason)
    {
        var npcId = GetNpcIdentity(npc);
        var npcName = GetNodeString(npc["name"]) ?? npcId;
        var npcTrade = ReadNpcTradeValue(npc);
        var playerTrade = ReadPlayerTradeSync();
        var relation = ReadNpcRelationshipLevel(npc);
        var profile = ResolveMerchantProfile(npc);
        var offers = new List<NpcTradeOffer>();

        if (!blocked &&
            npc["tradeInventory"] is JsonObject tradeInventory &&
            tradeInventory["items"] is JsonArray items)
        {
            foreach (var item in items.OfType<JsonObject>())
            {
                if (item["itemData"] is not JsonObject itemData)
                    continue;

                offers.Add(new NpcTradeOffer(
                    GetNodeString(item["slotId"]) ?? "",
                    GetNodeString(itemData["name"]) ?? "Товар",
                    GetItemRarity(itemData),
                    GetNodeInt(item["price"], 0),
                    GetNodeString(itemData["description"]) ?? "",
                    GetNodeString(item["merchantProfile"]) ?? profile?.Key ?? DefaultMerchantProfileKey,
                    GetNodeBool(item["soldOut"]),
                    CloneObject(itemData)));
            }
        }

        return new NpcTradeView(
            npcId,
            npcName,
            profile?.Key ?? DefaultMerchantProfileKey,
            profile?.DisplayName ?? GetMerchantProfileDisplayName(DefaultMerchantProfileKey),
            npcTrade,
            playerTrade,
            relation,
            GetNodeInt(statusRoot["money"], 0),
            blocked,
            blocked ? blockedReason : null,
            currentWorldMinutes,
            GetGeneratedAtWorldMinutes(npc["tradeInventory"] as JsonObject, currentWorldMinutes),
            GetRefreshAfterWorldMinutes(npc["tradeInventory"] as JsonObject, currentWorldMinutes),
            offers);
    }

    private static JsonObject GenerateTradeInventory(string npcId, JsonObject npc, GenerationTradeTier generationTier,
        PricingTradeTier pricingTier, int playerTrade, int npcTrade, int currentWorldMinutes)
    {
        var profile = ResolveMerchantProfile(npc)!;
        var slotCount = ComputeSlotCount(ReadNpcLevel(npc), npcTrade);
        var rarities = GenerateRarityPattern(generationTier, slotCount, npcId, currentWorldMinutes);
        var random = new Random(ComputeStableSeed($"{npcId}|{currentWorldMinutes}|npc_trade"));
        var items = new JsonArray();

        for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            var template = SelectTemplate(profile, slotIndex, random);
            var rarity = rarities[slotIndex];
            var itemData = GenerateItemData(profile, template, npcId, rarity, slotIndex, currentWorldMinutes, random);
            var basePrice = GetBaseBuyPrice(itemData, rarity);
            items.Add(new JsonObject
            {
                ["slotId"] = $"npc_trade_{SanitizeId(npcId)}_{currentWorldMinutes}_{slotIndex + 1}",
                ["itemId"] = GetNodeString(itemData["itemId"]) ?? "",
                ["price"] = ComputeBuyPrice(basePrice, playerTrade, npcTrade, pricingTier),
                ["merchantProfile"] = profile.Key,
                ["soldOut"] = false,
                ["itemData"] = itemData
            });
        }

        return new JsonObject
        {
            ["generatedAtWorldDate"] = currentWorldMinutes,
            ["refreshAfterWorldDate"] = currentWorldMinutes + RefreshWindowMinutes,
            ["generationTradeTier"] = generationTier.ToString(),
            ["pricingTradeTier"] = pricingTier.ToString(),
            ["items"] = items
        };
    }

    private static JsonObject GenerateItemData(MerchantProfile profile, TradeItemTemplate template, string npcId, string rarity,
        int slotIndex, int currentWorldMinutes, Random random)
    {
        var itemName = template.ItemNames[random.Next(template.ItemNames.Length)];
        var itemId = $"npc_item_{SanitizeId(npcId)}_{SanitizeId(template.TemplateId)}_{currentWorldMinutes}_{slotIndex + 1}";
        var description = BuildItemDescription(profile, template, itemName, slotIndex, random);
        var baseBuyPrice = ScaleBaseBuyPriceByRarity(template.BasePrice, rarity);

        var item = new JsonObject
        {
            ["itemId"] = itemId,
            ["name"] = itemName,
            ["description"] = description,
            ["type"] = template.Type,
            ["tradeItemClass"] = template.TradeItemClass,
            ["quality"] = rarity,
            ["price"] = baseBuyPrice,
            ["baseSellPrice"] = Math.Max(1, (int)Math.Floor(baseBuyPrice * 0.4)),
            ["weight"] = ((template.WeightGrams * Math.Max(1, template.InitialCount)) / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
            ["group"] = template.Group
        };

        if (!string.IsNullOrWhiteSpace(template.EquipmentSlot))
            item["equipmentSlot"] = template.EquipmentSlot;
        if (template.Stackable)
            item["count"] = Math.Max(1, template.InitialCount);
        if (template.IsConsumable)
            item["isConsumption"] = true;
        if (template.IsContainer)
        {
            item["isContainer"] = true;
            if (template.Capacity.HasValue)
                item["capacity"] = template.Capacity.Value;
        }
        if (template.Type is "Book" or "Document" or "Media")
        {
            item["textContent"] = new JsonArray
            {
                BuildTradeTextSnippet(template, profile, slotIndex)
            };
        }

        if (ShouldGenerateBonuses(template, rarity, random))
        {
            var primaryBonus = GetPrimaryBonus(rarity);
            var secondaryBonus = GetSecondaryBonus(rarity);
            var actionBonusValue = GetActionBonusValue(rarity);
            var primaryStat = profile.BonusStats[slotIndex % profile.BonusStats.Length];
            var secondaryStat = profile.BonusStats[(slotIndex + 1) % profile.BonusStats.Length];
            var actionBonusKey = profile.ActionBonuses[slotIndex % profile.ActionBonuses.Length];
            var bonuses = new JsonArray
            {
                $"+{primaryBonus} к {DisplayStat(primaryStat)}"
            };
            var effects = new JsonArray
            {
                DescribeActionBonus(actionBonusKey, actionBonusValue)
            };
            var passiveEffects = new JsonArray
            {
                profile.ProfileFlavors[slotIndex % profile.ProfileFlavors.Length]
            };
            var structuredBonuses = new JsonArray
            {
                new JsonObject
                {
                    ["bonusType"] = "Characteristic",
                    ["target"] = DisplayStat(primaryStat),
                    ["valueType"] = "Flat",
                    ["value"] = primaryBonus,
                    ["application"] = "Permanent",
                    ["description"] = $"+{primaryBonus} к {DisplayStat(primaryStat)}"
                },
                new JsonObject
                {
                    ["bonusType"] = "ActionCheck",
                    ["target"] = DescribeActionTarget(actionBonusKey),
                    ["valueType"] = "Percentage",
                    ["value"] = actionBonusValue * 5,
                    ["application"] = "Permanent",
                    ["description"] = DescribeActionBonus(actionBonusKey, actionBonusValue)
                }
            };

            if (secondaryBonus > 0 && GetRarityRank(rarity) >= GetRarityRank("Rare"))
            {
                bonuses.Add($"+{secondaryBonus} к {DisplayStat(secondaryStat)}");
                structuredBonuses.Add(new JsonObject
                {
                    ["bonusType"] = "Characteristic",
                    ["target"] = DisplayStat(secondaryStat),
                    ["valueType"] = "Flat",
                    ["value"] = secondaryBonus,
                    ["application"] = "Permanent",
                    ["description"] = $"+{secondaryBonus} к {DisplayStat(secondaryStat)}"
                });
            }

            item["bonuses"] = bonuses;
            item["effects"] = effects;
            item["passiveEffects"] = passiveEffects;
            item["structuredBonuses"] = structuredBonuses;
        }

        return item;
    }

    private static bool TradeInventoryMatchesCurrentContract(JsonObject? tradeInventory, int currentWorldMinutes, JsonObject npc,
        int playerTrade, int npcTrade)
    {
        if (tradeInventory == null)
            return false;

        var generatedAt = GetNodeInt(tradeInventory["generatedAtWorldDate"], -1);
        var refreshAfter = GetNodeInt(tradeInventory["refreshAfterWorldDate"], -1);
        var generationTierCode = GetNodeString(tradeInventory["generationTradeTier"]);
        var pricingTierCode = GetNodeString(tradeInventory["pricingTradeTier"]);
        if (generatedAt < 0 || refreshAfter <= generatedAt)
            return false;
        if (currentWorldMinutes >= refreshAfter)
            return false;
        if (!IsValidGenerationTierCode(generationTierCode) || !IsValidPricingTierCode(pricingTierCode))
            return false;

        var expectedGenerationTier = GetGenerationTier(ReadNpcLevel(npc), npcTrade, ReadNpcRelationshipLevel(npc)).ToString();
        if (!string.Equals(generationTierCode, expectedGenerationTier, StringComparison.OrdinalIgnoreCase))
            return false;

        if (tradeInventory["items"] is not JsonArray items)
            return false;
        if (items.Count < 6 || items.Count > 20)
            return false;

        var profile = ResolveMerchantProfile(npc);
        if (profile == null)
            return false;

        foreach (var item in items.OfType<JsonObject>())
        {
            if (string.IsNullOrWhiteSpace(GetNodeString(item["slotId"])))
                return false;
            if (!TryNormalizeMerchantProfileCode(GetNodeString(item["merchantProfile"]), out var itemProfile) ||
                !string.Equals(itemProfile, profile.Key, StringComparison.OrdinalIgnoreCase))
                return false;
            if (item["soldOut"] is not JsonValue soldNode || (!soldNode.TryGetValue<bool>(out _) && !bool.TryParse(soldNode.ToString(), out _)))
                return false;
            if (item["itemData"] is not JsonObject itemData)
                return false;

            var rarity = GetItemRarity(itemData);
            if (!IsRarityAllowedForGenerationTier(rarity, generationTierCode!))
                return false;
            if (!IsValidTradeItemClassCode(GetNodeString(itemData["tradeItemClass"])))
                return false;
            var expectedPrice = ComputeBuyPrice(GetBaseBuyPrice(itemData, rarity), playerTrade, npcTrade, ParsePricingTierCode(pricingTierCode!));
            if (GetNodeInt(item["price"], -1) != expectedPrice)
                return false;
            if (string.IsNullOrWhiteSpace(GetNodeString(itemData["itemId"])) ||
                string.IsNullOrWhiteSpace(GetNodeString(itemData["name"])) ||
                GetNodeInt(itemData["price"], 0) <= 0)
                return false;
        }

        return true;
    }

    private static bool RepriceTradeInventory(JsonObject tradeInventory, int playerTrade, int npcTrade, PricingTradeTier pricingTier)
    {
        if (tradeInventory["items"] is not JsonArray items)
            return false;

        var changed = false;
        foreach (var item in items.OfType<JsonObject>())
        {
            if (item["itemData"] is not JsonObject itemData)
                continue;
            var expected = ComputeBuyPrice(GetBaseBuyPrice(itemData, GetItemRarity(itemData)), playerTrade, npcTrade, pricingTier);
            if (GetNodeInt(item["price"], -1) != expected)
            {
                item["price"] = expected;
                changed = true;
            }
        }

        var tierCode = pricingTier.ToString();
        if (!string.Equals(GetNodeString(tradeInventory["pricingTradeTier"]), tierCode, StringComparison.OrdinalIgnoreCase))
        {
            tradeInventory["pricingTradeTier"] = tierCode;
            changed = true;
        }

        return changed;
    }

    private async Task<JsonObject?> ReadNpcRootAsync()
    {
        try
        {
            var json = await _fs.ReadFileAsync(NpcCorePath);
            if (string.IsNullOrWhiteSpace(json))
                return new JsonObject();
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать npc_core.json для локальной торговли NPC");
            return null;
        }
    }

    private async Task<JsonObject?> ReadInventoryRootAsync()
    {
        try
        {
            var json = await _fs.ReadFileAsync(ItemsPath);
            if (string.IsNullOrWhiteSpace(json))
                return new JsonObject();
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать items.json для локальной торговли NPC");
            return null;
        }
    }

    private async Task<JsonObject?> ReadPlayerStatusRootAsync()
    {
        try
        {
            var json = await _fs.ReadFileAsync(PlayerStatusPath);
            if (string.IsNullOrWhiteSpace(json))
                return new JsonObject();
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать player_status.json для локальной торговли NPC");
            return null;
        }
    }

    private async Task<int> ResolveCurrentWorldMinutesAsync()
    {
        try
        {
            var json = await _fs.ReadFileAsync(WorldTimePath);
            if (string.IsNullOrWhiteSpace(json))
                return 0;
            using var doc = JsonDocument.Parse(json);
            return ResolveWorldMinutes(doc.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось определить мировое время для локальной торговли NPC");
            return 0;
        }
    }

    private static JsonObject? FindNpcEntry(JsonObject root, string npcId)
    {
        foreach (var arr in EnumerateNpcArrays(root))
        {
            foreach (var item in arr.OfType<JsonObject>())
            {
                if (string.Equals(GetNpcIdentity(item), npcId, StringComparison.OrdinalIgnoreCase))
                    return item;
            }
        }
        return null;
    }

    private static IEnumerable<JsonArray> EnumerateNpcArrays(JsonObject root)
    {
        foreach (var key in new[] { "NPCsInScene", "UpdateNPCs" })
            if (root[key] is JsonArray arr)
                yield return arr;
    }

    private static void SyncNpcEntries(JsonObject root, string npcId, JsonObject npc)
    {
        foreach (var arr in EnumerateNpcArrays(root))
        {
            for (var i = 0; i < arr.Count; i++)
            {
                if (arr[i] is not JsonObject item)
                    continue;
                if (string.Equals(GetNpcIdentity(item), npcId, StringComparison.OrdinalIgnoreCase))
                    arr[i] = CloneObject(npc);
            }
        }
    }

    private bool NpcTradeAllowedHere(JsonObject npc, out string? blockedReason)
    {
        var (currentLocationId, currentLocationName) = ReadCurrentLocationIdentitySync();
        var availability = EvaluateTradeAvailability(npc, currentLocationId, currentLocationName);
        blockedReason = availability.BlockReason;
        return availability.TradeAvailable;
    }

    private (string locationId, string locationName) ReadCurrentLocationIdentitySync()
    {
        try
        {
            var json = _fs.ReadFileAsync("game_state/world/current_location.json").GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(json))
                return ("", "");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("currentLocationData", out var wrapped) && wrapped.ValueKind == JsonValueKind.Object)
                root = wrapped;

            return (
                GetFirstNonEmptyString(root, "locationId", "currentLocationId") ?? "",
                GetFirstNonEmptyString(root, "name", "currentLocation") ?? "");
        }
        catch
        {
            return ("", "");
        }
    }

    private static MerchantProfile? ResolveMerchantProfile(JsonObject npc)
    {
        var tradeState = npc["tradeState"] as JsonObject;
        var profileCode = ResolveMerchantProfileCode(
            GetNodeString(tradeState?["merchantProfile"]),
            GetNodeString(npc["role"]),
            GetNodeString(npc["occupation"]),
            GetNodeString(npc["class"]),
            GetNodeString(npc["name"]));
        return !string.IsNullOrWhiteSpace(profileCode) && MerchantProfiles.TryGetValue(profileCode, out var profile)
            ? profile
            : null;
    }

    private static NpcTradeAvailability BuildTradeAvailability(
        string? merchantProfile,
        string npcLocationId,
        string npcLocationName,
        string currentLocationId,
        string currentLocationName,
        JsonElement? tradeState)
    {
        if (string.IsNullOrWhiteSpace(merchantProfile))
            return new NpcTradeAvailability(null, GetMerchantProfileDisplayName(null), false, "Этот НПС не является торговцем.");

        if (!IsSameTradeLocation(npcLocationId, npcLocationName, currentLocationId, currentLocationName))
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                "Доступна только в текущей локации торговца.");
        }

        if (tradeState == null || tradeState.Value.ValueKind != JsonValueKind.Object)
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                "Локальная торговля включается только через tradeState.canTrade = true.");
        }

        if (!tradeState.Value.TryGetProperty("canTrade", out var canTradeNode) ||
            (canTradeNode.ValueKind != JsonValueKind.True && canTradeNode.ValueKind != JsonValueKind.False))
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                "Локальная торговля включается только через tradeState.canTrade = true.");
        }

        if (canTradeNode.ValueKind == JsonValueKind.False)
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                GetFirstNonEmptyString(tradeState.Value, "tradeBlockedReason") ?? "Торговля сейчас недоступна.");
        }

        return new NpcTradeAvailability(
            merchantProfile,
            GetMerchantProfileDisplayName(merchantProfile),
            true,
            null);
    }

    private static NpcTradeAvailability BuildTradeAvailability(
        string? merchantProfile,
        string npcLocationId,
        string npcLocationName,
        string currentLocationId,
        string currentLocationName,
        JsonObject? tradeState)
    {
        if (string.IsNullOrWhiteSpace(merchantProfile))
            return new NpcTradeAvailability(null, GetMerchantProfileDisplayName(null), false, "Этот НПС не является торговцем.");

        if (!IsSameTradeLocation(npcLocationId, npcLocationName, currentLocationId, currentLocationName))
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                "Доступна только в текущей локации торговца.");
        }

        if (tradeState == null)
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                "Локальная торговля включается только через tradeState.canTrade = true.");
        }

        if (tradeState["canTrade"] is not JsonValue canTradeValue ||
            !canTradeValue.TryGetValue<bool>(out var canTrade))
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                "Локальная торговля включается только через tradeState.canTrade = true.");
        }

        if (!canTrade)
        {
            return new NpcTradeAvailability(
                merchantProfile,
                GetMerchantProfileDisplayName(merchantProfile),
                false,
                GetNodeString(tradeState["tradeBlockedReason"]) ?? "Торговля сейчас недоступна.");
        }

        return new NpcTradeAvailability(
            merchantProfile,
            GetMerchantProfileDisplayName(merchantProfile),
            true,
            null);
    }

    private static bool IsSameTradeLocation(string npcLocationId, string npcLocationName, string currentLocationId, string currentLocationName)
    {
        return
            (!string.IsNullOrWhiteSpace(currentLocationId) && string.Equals(currentLocationId, npcLocationId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(currentLocationName) && string.Equals(currentLocationName, npcLocationName, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(currentLocationId) && string.Equals(currentLocationId, npcLocationName, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(currentLocationName) && string.Equals(currentLocationName, npcLocationId, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildItemDescription(MerchantProfile profile, TradeItemTemplate template, string itemName, int slotIndex, Random random)
    {
        var templateSnippet = template.DescriptionSnippets[(slotIndex + random.Next(template.DescriptionSnippets.Length)) % template.DescriptionSnippets.Length];
        var profileSnippet = profile.ProfileFlavors[(slotIndex + random.Next(profile.ProfileFlavors.Length)) % profile.ProfileFlavors.Length];
        return $"{itemName} — товар класса «{GetTradeItemClassDisplayName(template.TradeItemClass)}» из ассортимента «{profile.DisplayName}». {templateSnippet} {profileSnippet}";
    }

    private static string BuildTradeTextSnippet(TradeItemTemplate template, MerchantProfile profile, int slotIndex)
    {
        var templateSnippet = template.DescriptionSnippets[slotIndex % template.DescriptionSnippets.Length];
        return $"{templateSnippet} Ассортимент: {profile.DisplayName}.";
    }

    private static string DescribeActionBonus(string actionBonusKey, int bonusValue)
    {
        var target = DescribeActionTarget(actionBonusKey);
        return $"+{bonusValue * 5}% к {target}";
    }

    private static string DescribeActionTarget(string actionBonusKey) => actionBonusKey switch
    {
        "combatBonus" => "боевым действиям",
        "durabilityBonus" => "сохранению прочности",
        "mobilityBonus" => "мобильности и перемещению",
        "precisionBonus" => "точным действиям",
        "repairBonus" => "ремонту и обслуживанию",
        "craftingBonus" => "сборке и ремесленным проверкам",
        "healingBonus" => "исцелению",
        "enduranceBonus" => "выносливости и продолжительной нагрузке",
        "recoveryBonus" => "восстановлению и подготовке",
        "resourceBonus" => "восстановлению ресурсов",
        "knowledgeBonus" => "проверкам знаний",
        "researchBonus" => "исследованию",
        "focusBonus" => "концентрации и работе с данными",
        "loreBonus" => "работе с лором и текстами",
        "stealthBonus" => "скрытности",
        "travelBonus" => "дорожным действиям",
        "fortuneBonus" => "удачным исходам",
        "socialBonus" => "социальным действиям",
        "comfortBonus" => "комфорту и бытовым действиям",
        "prestigeBonus" => "репутационным и статусным ситуациям",
        "tradeBonus" => "торговым сделкам",
        "escapeBonus" => "выходу из опасных ситуаций",
        "utilityBonus" => "полезным действиям",
        "analysisBonus" => "анализу и диагностике",
        _ => "профильным действиям"
    };

    private static string DisplayStat(string stat) => stat switch
    {
        "strength" => "Сила",
        "dexterity" => "Ловкость",
        "constitution" => "Выносливость",
        "intelligence" => "Интеллект",
        "wisdom" => "Мудрость",
        "faith" => "Вера",
        "attractiveness" => "Привлекательность",
        "trade" => "Торговля",
        "persuasion" => "Убеждение",
        "perception" => "Восприятие",
        "luck" => "Удача",
        "speed" => "Скорость",
        _ => stat
    };

    private static GenerationTradeTier GetGenerationTier(int level, int npcTrade, int relationshipLevel)
    {
        var relationBonus = relationshipLevel switch
        {
            >= 251 => 18,
            >= 101 => 12,
            >= 0 => 6,
            >= -50 => 0,
            _ => -8
        };
        var score = level + npcTrade + relationBonus;
        return score switch
        {
            < 20 => GenerationTradeTier.Poor,
            < 35 => GenerationTradeTier.Standard,
            < 52 => GenerationTradeTier.Good,
            < 70 => GenerationTradeTier.Premium,
            _ => GenerationTradeTier.Elite
        };
    }

    private static PricingTradeTier GetPricingTier(int relationshipLevel) => relationshipLevel switch
    {
        < -100 => PricingTradeTier.Hostile,
        < 0 => PricingTradeTier.Wary,
        < 101 => PricingTradeTier.Neutral,
        < 251 => PricingTradeTier.Warm,
        _ => PricingTradeTier.Trusted
    };

    private static PricingTradeTier ParsePricingTierCode(string tierCode) => tierCode switch
    {
        nameof(PricingTradeTier.Hostile) => PricingTradeTier.Hostile,
        nameof(PricingTradeTier.Wary) => PricingTradeTier.Wary,
        nameof(PricingTradeTier.Neutral) => PricingTradeTier.Neutral,
        nameof(PricingTradeTier.Warm) => PricingTradeTier.Warm,
        nameof(PricingTradeTier.Trusted) => PricingTradeTier.Trusted,
        _ => PricingTradeTier.Neutral
    };

    private static int ComputeSlotCount(int level, int trade) => Math.Clamp(6 + (int)Math.Floor(level / 8.0) + (int)Math.Floor(trade / 15.0), 6, 20);

    private static string[] GenerateRarityPattern(GenerationTradeTier tier, int slotCount, string npcId, int worldTime)
    {
        var allowed = tier switch
        {
            GenerationTradeTier.Poor => new[] { "Common", "Common", "Uncommon" },
            GenerationTradeTier.Standard => new[] { "Common", "Uncommon", "Uncommon" },
            GenerationTradeTier.Good => new[] { "Uncommon", "Rare", "Rare" },
            GenerationTradeTier.Premium => new[] { "Rare", "Rare", "Epic" },
            GenerationTradeTier.Elite => new[] { "Rare", "Epic", "Epic" },
            _ => new[] { "Common" }
        };

        var random = new Random(ComputeStableSeed($"{npcId}|{worldTime}|rarity"));
        var result = new string[slotCount];
        for (var i = 0; i < slotCount; i++)
            result[i] = allowed[random.Next(allowed.Length)];
        return result;
    }

    private static TradeItemTemplate SelectTemplate(MerchantProfile profile, int slotIndex, Random random)
    {
        var candidates = Templates
            .Where(t => t.CategoryTags.Any(tag => profile.CategoryTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
            .ToList();
        if (candidates.Count == 0)
            candidates = Templates.ToList();
        return candidates[(slotIndex + random.Next(candidates.Count)) % candidates.Count];
    }

    private static bool ShouldGenerateBonuses(TradeItemTemplate template, string rarity, Random random)
    {
        if (!template.AllowsBonuses)
            return false;

        var chance = rarity switch
        {
            "Common" => 0.08,
            "Uncommon" => 0.22,
            "Rare" => 0.42,
            "Epic" => 0.65,
            _ => 0.08
        };

        return random.NextDouble() < chance;
    }

    private static int ScaleBaseBuyPriceByRarity(int templateBasePrice, string rarity)
    {
        var multiplier = rarity switch
        {
            "Common" => 1.00m,
            "Uncommon" => 1.35m,
            "Rare" => 1.85m,
            "Epic" => 2.75m,
            _ => 1.00m
        };

        return Math.Max(1, (int)Math.Ceiling(templateBasePrice * multiplier));
    }

    private static bool TryNormalizeMerchantProfileCode(string? profileCode, out string normalizedProfile)
    {
        normalizedProfile = "";
        if (string.IsNullOrWhiteSpace(profileCode))
            return false;

        return MerchantProfileAliases.TryGetValue(profileCode.Trim(), out normalizedProfile!);
    }

    private static bool ContainsAny(string source, params string[] fragments) =>
        fragments.Any(fragment => source.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static int GetPrimaryBonus(string rarity) => rarity switch
    {
        "Common" => 1,
        "Uncommon" => 2,
        "Rare" => 4,
        "Epic" => 7,
        _ => 1
    };

    private static int GetSecondaryBonus(string rarity) => rarity switch
    {
        "Common" => 0,
        "Uncommon" => 1,
        "Rare" => 2,
        "Epic" => 4,
        _ => 0
    };

    private static int GetActionBonusValue(string rarity) => rarity switch
    {
        "Common" => 1,
        "Uncommon" => 2,
        "Rare" => 3,
        "Epic" => 5,
        _ => 1
    };

    private static int GetBaseBuyPrice(string rarity) => rarity switch
    {
        "Common" => 20,
        "Uncommon" => 50,
        "Rare" => 120,
        "Epic" => 280,
        _ => 20
    };

    private static int GetBaseBuyPrice(JsonObject item, string rarity) => GetNodeInt(item["price"], GetBaseBuyPrice(rarity));

    private static int GetBaseSellPrice(string rarity) => rarity switch
    {
        "Common" => 8,
        "Uncommon" => 20,
        "Rare" => 48,
        "Epic" => 112,
        _ => 8
    };

    private static int GetBaseSellPrice(JsonObject item, string rarity) => GetNodeInt(item["baseSellPrice"], GetBaseSellPrice(rarity));

    private static int ComputeBuyPrice(int basePrice, int playerTrade, int npcTrade, PricingTradeTier pricingTier)
    {
        var tradeDelta = playerTrade - npcTrade;
        var tradeModifier = 1.20 - Math.Clamp(tradeDelta * 0.01, -0.20, 0.20);
        var reputationModifier = pricingTier switch
        {
            PricingTradeTier.Hostile => 1.20,
            PricingTradeTier.Wary => 1.10,
            PricingTradeTier.Neutral => 1.00,
            PricingTradeTier.Warm => 0.92,
            PricingTradeTier.Trusted => 0.85,
            _ => 1.00
        };
        return (int)Math.Ceiling(basePrice * tradeModifier * reputationModifier);
    }

    private static int ComputeSellPrice(int basePrice, int playerTrade, int npcTrade, PricingTradeTier pricingTier)
    {
        var tradeDelta = playerTrade - npcTrade;
        var tradeModifier = 0.80 + Math.Clamp(tradeDelta * 0.01, -0.20, 0.20);
        var reputationModifier = pricingTier switch
        {
            PricingTradeTier.Hostile => 0.80,
            PricingTradeTier.Wary => 0.90,
            PricingTradeTier.Neutral => 1.00,
            PricingTradeTier.Warm => 1.08,
            PricingTradeTier.Trusted => 1.15,
            _ => 1.00
        };
        return (int)Math.Floor(basePrice * tradeModifier * reputationModifier);
    }

    private static int ReadNpcLevel(JsonObject npc) => GetNodeInt(npc["level"], 1);

    private static int ReadNpcTradeValue(JsonObject npc)
    {
        if (npc["characteristics"] is JsonObject chars)
        {
            var modified = GetNodeInt(chars["modifiedTrade"], int.MinValue);
            if (modified != int.MinValue)
                return modified;
            var standard = GetNodeInt(chars["standardTrade"], int.MinValue);
            if (standard != int.MinValue)
                return standard;
            var flat = GetNodeInt(chars["trade"], int.MinValue);
            if (flat != int.MinValue)
                return flat;
        }
        return 10;
    }

    private int ReadPlayerTradeSync()
    {
        foreach (var path in new[] { "game_state/misc/characteristics.json", "game_state/player/player_status.json", PlayerStatusPath })
        {
            try
            {
                var json = _fs.ReadFileAsync(path).GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(json))
                    continue;
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (TryReadIntLike(root, "modifiedTrade", out var modified))
                    return modified;
                if (TryReadIntLike(root, "trade", out var flat))
                    return flat;
            }
            catch
            {
                // ignore and try next source
            }
        }

        return 10;
    }

    private async Task<int> ReadPlayerTradeAsync() => await Task.FromResult(ReadPlayerTradeSync());

    private static int ReadNpcRelationshipLevel(JsonObject npc) => GetNodeInt(npc["relationshipLevel"], 0);

    private static void NormalizeInventoryShape(JsonObject root)
    {
        if (root["items"] is not JsonArray)
            root["items"] = new JsonArray();
        if (root["equipment"] is not JsonObject)
        {
            root["equipment"] = new JsonObject
            {
                ["head"] = null, ["body"] = null, ["hands"] = null, ["feet"] = null,
                ["mainHand"] = null, ["offHand"] = null, ["neck"] = null, ["ring1"] = null, ["ring2"] = null
            };
        }
    }

    private static HashSet<string> CollectEquippedItemReferences(JsonObject root)
    {
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root["equipment"] is not JsonObject eq)
            return refs;

        foreach (var prop in eq)
        {
            if (prop.Value is JsonValue value && value.TryGetValue<string>(out var str) && !string.IsNullOrWhiteSpace(str))
                refs.Add(str);
        }

        return refs;
    }

    private static bool IsQuestBoundItem(JsonObject item)
    {
        if (item["isQuestItem"] is JsonValue questValue && questValue.TryGetValue<bool>(out var isQuestItem) && isQuestItem)
            return true;
        return string.Equals(GetNodeString(item["group"]), "Quest", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSoulRelicLikeItem(JsonObject item)
    {
        if (!string.IsNullOrWhiteSpace(GetNodeString(item["relicId"])) ||
            !string.IsNullOrWhiteSpace(GetNodeString(item["soulRelicId"])))
            return true;

        var type = GetNodeString(item["type"]);
        if (!string.IsNullOrWhiteSpace(type) &&
            (type.Contains("soul relic", StringComparison.OrdinalIgnoreCase) ||
             type.Contains("реликвия души", StringComparison.OrdinalIgnoreCase)))
            return true;

        var group = GetNodeString(item["group"]);
        if (!string.IsNullOrWhiteSpace(group) &&
            (group.Contains("soul relic", StringComparison.OrdinalIgnoreCase) ||
             group.Contains("реликвия души", StringComparison.OrdinalIgnoreCase)))
            return true;

        var itemId = GetNodeString(item["itemId"]) ?? GetNodeString(item["id"]) ?? GetNodeString(item["existedId"]);
        return !string.IsNullOrWhiteSpace(itemId) &&
               (itemId.StartsWith("sr_", StringComparison.OrdinalIgnoreCase) ||
                itemId.Contains("soulrelic", StringComparison.OrdinalIgnoreCase));
    }

    private static void UpsertInventoryItem(JsonArray items, JsonObject item)
    {
        var itemId = GetNodeString(item["itemId"]) ?? GetNodeString(item["id"]) ?? GetNodeString(item["existedId"]);
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] is not JsonObject existing)
                    continue;
                var existingId = GetNodeString(existing["itemId"]) ?? GetNodeString(existing["id"]) ?? GetNodeString(existing["existedId"]);
                if (!string.IsNullOrWhiteSpace(existingId) && string.Equals(existingId, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    items[i] = item;
                    return;
                }
            }
        }

        items.Add(item);
    }

    private static int FindInventoryItemIndex(JsonArray items, string itemId)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is not JsonObject item)
                continue;
            var existingId = GetNodeString(item["itemId"]) ?? GetNodeString(item["id"]) ?? GetNodeString(item["existedId"]);
            if (!string.IsNullOrWhiteSpace(existingId) && string.Equals(existingId, itemId, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static int GetRefreshAfterWorldMinutes(JsonObject? tradeInventory, int fallback) =>
        tradeInventory == null ? fallback + RefreshWindowMinutes : GetNodeInt(tradeInventory["refreshAfterWorldDate"], fallback + RefreshWindowMinutes);

    private static int GetGeneratedAtWorldMinutes(JsonObject? tradeInventory, int fallback) =>
        tradeInventory == null ? fallback : GetNodeInt(tradeInventory["generatedAtWorldDate"], fallback);

    private static string GetItemRarity(JsonObject item) => GetNodeString(item["quality"]) ?? GetNodeString(item["rarity"]) ?? "Common";

    private static int GetRarityRank(string rarity) => rarity switch
    {
        "Common" => 1,
        "Uncommon" => 2,
        "Rare" => 3,
        "Epic" => 4,
        "Legendary" => 5,
        _ => 1
    };

    private static string GetNpcIdentity(JsonObject npc) =>
        GetNodeString(npc["NPCId"]) ?? GetNodeString(npc["npcId"]) ?? GetNodeString(npc["id"]) ?? "";

    private static string SanitizeId(string value)
    {
        var chars = value.Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? "item" : new string(chars).ToLowerInvariant();
    }

    private static int ComputeStableSeed(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in value)
                hash = hash * 31 + c;
            return hash;
        }
    }

    private static JsonObject CloneObject(JsonObject source) => JsonNode.Parse(source.ToJsonString())!.AsObject();

    private static string? GetNodeString(JsonNode? node)
    {
        if (node == null) return null;
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var str))
                return str;
            return value.ToJsonString().Trim('"');
        }

        return node.ToJsonString();
    }

    private static int GetNodeInt(JsonNode? node, int fallback = 0)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var parsed))
                return parsed;
            if (value.TryGetValue<string>(out var str) && int.TryParse(str, out parsed))
                return parsed;
        }
        return fallback;
    }

    private static bool GetNodeBool(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var parsed))
                return parsed;
            if (value.TryGetValue<string>(out var str) && bool.TryParse(str, out parsed))
                return parsed;
        }
        return false;
    }

    private static string? GetFirstNonEmptyString(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!root.TryGetProperty(propertyName, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.String)
            {
                var stringValue = value.GetString();
                if (!string.IsNullOrWhiteSpace(stringValue))
                    return stringValue;
            }
        }
        return null;
    }

    private static bool TryReadIntLike(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var prop))
            return false;

        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.TryGetInt32(out value),
            JsonValueKind.String => int.TryParse(prop.GetString(), out value),
            _ => false
        };
    }

    private static int MapTimeOfDayToMinutes(string timeOfDay) => timeOfDay.ToLowerInvariant() switch
    {
        "dawn" or "рассвет" => 300,
        "morning" or "утро" => 480,
        "noon" or "day" or "день" => 720,
        "afternoon" => 900,
        "evening" or "вечер" => 1080,
        "night" or "ночь" => 1320,
        _ => 720
    };
}
