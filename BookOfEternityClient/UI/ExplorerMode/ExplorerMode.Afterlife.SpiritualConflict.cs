using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private const int SpiritualArtMaxTier = 5;

    private enum SpiritualArtCurrency
    {
        InkFeathers,
        LightSparks
    }

    private sealed record SpiritualArtUpgradeQuote(
        AfterlifeSpiritualConflictState.SpiritualArtDefinition Art,
        int CurrentTier,
        int NextTier,
        int MaxUnlockedTier,
        int InkFeatherCost,
        int LightSparkCost,
        string RequiredRankLabel,
        string? BlockReason,
        string? SpecialArtId = null,
        string? SpecialArtDisplayName = null,
        string? SpecialArtEffectSummary = null,
        string? SpecialArtCombatEffect = null)
    {
        public bool IsSpecialArt => !string.IsNullOrWhiteSpace(SpecialArtId);
    }

    private sealed record SpiritFocusUpgradeQuote(
        int CurrentTier,
        int NextTier,
        int CurrentMaxActionPoints,
        int NextMaxActionPoints,
        int MaxUnlockedTier,
        int InkFeatherCost,
        int LightSparkCost,
        string RequiredRankLabel,
        string? BlockReason);

    private sealed record SpiritualProfileUpgradeChoice(
        SpiritualArtUpgradeQuote? ArtQuote,
        SpiritFocusUpgradeQuote? SpiritFocusQuote);

    private async Task ShowSpiritualConflictAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Духовный конфликт"))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Духовный конфликт", "Духовный конфликт посмертия доступен только в Море Хаоса и Сияющей Обители.");
            return;
        }

        await _stateManager.RefreshGameStateAsync();
        var root = await ReadJsonObjectForAfterlifeStatusAsync(AfterlifeSpiritualConflictState.StatePath);
        var active = root?["activeConflict"] as JsonObject;

        var lines = new List<string>
        {
            "[bold cyan]Духовный конфликт посмертия[/]",
            "",
            "Это отдельная загробная система конфликтов. Она не использует здоровье, энергию и смертные боевые навыки.",
            "Конфликт начинает ГМ по роли: по заявке игрока или когда актор посмертия сам инициирует давление.",
            "Победа в проверяемом конфликте может дать награду: в Море Хаоса — Чернильные Перья, в обычной активной Сияющей Обители — Искры Света.",
            "Награда появляется только за полноценный проверяемый конфликт; отмена, отсутствие эффекта, добровольное отступление и переговоры без состязания валюту не дают.",
            ""
        };

        if (active == null)
        {
            lines.Add("[dim]Активного духовного конфликта нет.[/]");
            lines.Add("");
            lines.Add("Когда в сцене появится проверяемое духовное противостояние, здесь будут видны стороны, позиция, напряжение и доступные действия.");
        }
        else
        {
            lines.Add("[bold]Активный конфликт:[/] [white]идёт[/]");
            lines.Add($"  • Область: [white]{Markup.Escape(FormatAfterlifeRealmLabel(AfterlifeSpiritualConflictState.GetNodeString(active["realm"])))}[/]");
            lines.Add($"  • Модель сторон: [white]{Markup.Escape(FormatSideModelLabel(AfterlifeSpiritualConflictState.GetNodeString(active["sideModel"])))}[/]");
            lines.Add($"  • Позиция конфликта: [white]{Markup.Escape(FormatConflictPositionLabel(AfterlifeSpiritualConflictState.GetNodeString(active["conflictPosition"])))}[/]");
            lines.Add($"  • Напряжение стороны игрока: [white]{Markup.Escape(FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(active["playerSideStrain"])))}[/]");
            lines.Add($"  • Напряжение противостоящей стороны: [white]{Markup.Escape(FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(active["oppositionSideStrain"])))}[/]");
            lines.Add($"  • Контроль/оковы: [white]{Markup.Escape(DescribeControlState(active["controlState"] as JsonObject))}[/]");
            lines.Add($"  • ОД: [white]{Markup.Escape(DescribeActionEconomy(active["actionEconomy"] as JsonObject))}[/]");
            lines.Add($"  • Состояние завершения: [white]{Markup.Escape(FormatResolutionStateLabel(AfterlifeSpiritualConflictState.GetNodeString(active["resolutionState"])))}[/]");
            lines.Add("");
            AppendConflictSideSummary(lines, "Сторона игрока", active["playerSide"] as JsonObject);
            AppendConflictSideSummary(lines, "Противостоящая сторона", active["oppositionSide"] as JsonObject);
            AppendVisibleCombatConditions(lines, active["combatConditions"] as JsonArray);
            lines.Add("");
            lines.Add($"  • Записано обменов действиями: [white]{(active["exchangeLog"] as JsonArray)?.Count ?? 0}[/]");
        }

        lines.Add("");
        lines.Add("[bold]Команды:[/]");
        lines.Add("  • /spiritual_combat_log — журнал духовного боя: обмены действиями, завершённые конфликты, кубики, позиция, напряжение и награды.");
        lines.Add("  • /spiritual_combat_help — подробная справка: тактика, позиция, кубики, криты, награды и прокачка.");
        lines.Add("  • /spiritual_action — отправить действие в активном духовном конфликте с явным тегом для ГМ.");
        lines.Add("  • Обычная художественная заявка во время активного конфликта тоже должна резолвиться ГМ как действие конфликта.");
        lines.Add("  • /spiritual_arts — посмотреть ранги, уровни духовных искусств и применимые действия.");

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ⚔ Духовный конфликт посмертия ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        WaitForKey();
    }

    private async Task ShowSpiritualCombatLogAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Журнал духовного боя"))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Журнал духовного боя", "Журнал духовного боя посмертия доступен только в Море Хаоса и Сияющей Обители.");
            return;
        }

        await _stateManager.RefreshGameStateAsync();
        var root = await ReadJsonObjectForAfterlifeStatusAsync(AfterlifeSpiritualConflictState.StatePath);
        var playerFacingRoot = BuildPlayerFacingCombatConditionAudit(root) as JsonObject;
        var active = playerFacingRoot?["activeConflict"] as JsonObject;
        var recentConflicts = playerFacingRoot?["recentConflicts"] as JsonArray;
        var lines = new List<string>
        {
            "[bold cyan]Журнал духовного боя[/]",
            "",
            "Это не журнал смертного боя: здесь показаны обмены действиями, завершённые конфликты, броски, позиция, напряжение и награды.",
            ""
        };

        var wroteEntry = false;
        if (active != null)
        {
            lines.Add("[bold]Активный конфликт:[/] [white]идёт[/]");
            lines.Add($"  • Область: [white]{Markup.Escape(FormatAfterlifeRealmLabel(AfterlifeSpiritualConflictState.GetNodeString(active["realm"])))}[/]");
            lines.Add($"  • Модель сторон: [white]{Markup.Escape(FormatSideModelLabel(AfterlifeSpiritualConflictState.GetNodeString(active["sideModel"])))}[/]");
            lines.Add($"  • Текущая позиция: [white]{Markup.Escape(FormatConflictPositionLabel(AfterlifeSpiritualConflictState.GetNodeString(active["conflictPosition"])))}[/]");
            lines.Add($"  • Текущее напряжение: игрок=[white]{Markup.Escape(FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(active["playerSideStrain"])))}[/], противник=[white]{Markup.Escape(FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(active["oppositionSideStrain"])))}[/]");
            lines.Add($"  • Текущий контроль/оковы: [white]{Markup.Escape(DescribeControlState(active["controlState"] as JsonObject))}[/]");
            lines.Add("");
            AppendVisibleCombatConditions(lines, active["combatConditions"] as JsonArray);

            if (active["exchangeLog"] is JsonArray activeExchangeLog && activeExchangeLog.Count > 0)
            {
                AppendSpiritualExchangeLog(lines, activeExchangeLog);
                wroteEntry = true;
            }
            else
            {
                lines.Add("  • Обменов в журнале действий пока нет.");
            }
        }

        if (recentConflicts is { Count: > 0 })
        {
            if (active != null)
                lines.Add("");

            AppendSpiritualRecentConflictLog(lines, recentConflicts);
            wroteEntry = true;
        }

        if (!wroteEntry)
        {
            lines.Add("[dim]Журнал духовного боя пуст: нет обменов действий и недавних завершённых конфликтов.[/]");
            lines.Add("Когда ГМ проведёт спорный обмен или завершение конфликта, запись появится здесь вместе с бросками и наградой.");
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ⚔ Журнал духовного боя ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        WaitForKey();
    }

    private Task ShowSpiritualCombatHelpAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Справка по духовному бою"))
            return Task.CompletedTask;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Справка по духовному бою", "Духовный бой посмертия доступен только в Море Хаоса и Сияющей Обители.");
            return Task.CompletedTask;
        }

        var lines = new List<string>
        {
            "[bold cyan]Духовный бой посмертия[/]",
            "",
            "[bold]Что это такое[/]",
            "  • Это ролевая система конфликтов загробной жизни, а не смертный бой: нет здоровья, энергии и смертных боевых навыков.",
            "  • Конфликт начинает ГМ по ситуации: по заявке игрока или когда Хранитель, резидент, светозарный актор или другой актор посмертия сам давит на душу.",
            "  • После старта игрок может писать обычную прозу. Команда /spiritual_action только добавляет явный маршрутный тег; она не обязательна, если действие очевидно относится к активному конфликту.",
            "",
            "[bold]Команды игрока[/]",
            "  • /spiritual_conflict / /духовный_конфликт — показать активный конфликт, стороны, позицию и напряжение.",
            "  • /spiritual_combat_log / /журнал_духовного_боя — показать журнал обменов и недавних конфликтов: действия, кубики, позицию, напряжение и награды.",
            "  • /spiritual_action / /духовное_действие — отправить ГМ явное действие в активном конфликте.",
            "  • /spiritual_arts / /духовные_искусства — посмотреть и локально прокачать духовные искусства.",
            "  • /spiritual_combat_help / /духовный_бой — эта подробная справка.",
            "",
            "[bold]Как выбирается исход[/]",
            "  • В спорных обменах ГМ использует видимые d20 из заранее сгенерированных кубиков и записывает результат так, чтобы журнал мог показать выбранный бросок и отброшенные значения.",
            "  • Формула: итог игрока = d20 игрока + модификаторы; итог противника = d20 противника + модификаторы; разница = итог игрока минус итог противника.",
            "  • Разница 8 и выше — решительный успех игрока; 3..7 — успех игрока; -2..2 — смешанный или нулевой эффект; -7..-3 — успех противника; -8 и ниже — решительный успех противника.",
            "  • Модификаторы идут от рангов Просветления/Сияния, уровней духовных искусств, силы ведущего бойца, поддержки, Воплощения Света и контекста сцены.",
            "  • Преимущество и Помеха бывают двух уровней: Преимущество — 2d20 и лучший результат, Великое Преимущество — 3d20 и лучший результат; Помеха — 2d20 и худший результат, Тяжкая Помеха — 3d20 и худший результат.",
            "  • Встречные источники гасятся ступенчато: Великое Преимущество против обычной Помехи становится обычным Преимуществом, Великое Преимущество против Тяжкой Помехи становится обычным броском. Два обычных источника одного направления не превращаются в великий/тяжкий уровень.",
            "  • В журнале выбранный куб помечается как выбранный, отброшенные — как отброшенные; крит считается только по выбранному кубу.",
            "  • Успешная защита против прямого давления дает одноразовое темповое окно: Преимущество на следующее подходящее духовное действие. Оно не применяется к восстановлению ОД, отступлению, сдаче или переговорам.",
            "",
            "[bold]Честные криты[/]",
            "  • Благоприятный крит для игрока: натуральная 20 игрока или натуральная 1 противника. Если разница дала результат хуже обычного успеха, итог поднимается только до успеха игрока.",
            "  • Неблагоприятный крит для игрока: натуральная 1 игрока или натуральная 20 противника. Если разница дала результат лучше обычной неудачи, итог опускается только до успеха противника.",
            "  • Это симметрично: крит сам по себе не создаёт решительный успех игрока или решительный успех противника. Решительный исход появляется только если разница уже достаточно велика.",
            "  • Если обе стороны получают натуральный крит, они отменяют друг друга, и используется обычная категория разницы.",
            "  • Любой крит, изменивший исход, должен сохранять правдоподобный масштаб результата для силы сторон. Натуральная 20 комара не превращает его в убийцу дракона.",
            "",
            "[bold]Позиция конфликта[/]",
            "  • Позиция — это шкала рычага/инициативы: доминирование противника, преимущество противника, спорная позиция, преимущество игрока, доминирование игрока.",
            "  • Она механически важна: успешный манёвр обязан менять позицию и не должен напрямую менять напряжение.",
            "  • Она влияет на спорный бросок: преимущество/доминирование игрока дают игроку +2/+4, преимущество/доминирование противника дают противнику +2/+4.",
            "  • Она открывает контроль: обычные и силовые оковы требуют преимущества игрока, доминирования игрока, подготовки или решительного успеха игрока.",
            "  • Она влияет на награду: победа из плохой стартовой позиции имеет больший риск и уровень вызова; победа из доминирования игрока платит меньше.",
            "  • Она ограничивает масштаб нарратива: крит или успех в плохой позиции обычно даёт правдоподобный прорыв/срыв угрозы, а не мгновенную абсолютную победу.",
            "",
            "[bold]ОД и стоимость действий[/]",
            "  • ОД — очки духовного действия. Это ресурс духовного боя посмертия, не здоровье, не энергия и не выносливость смертного мира.",
            "  • Активный конфликт хранит ОД обеих сторон: текущее значение, максимум и источник расчёта.",
            "  • Каждый новый обмен, который тратит или восстанавливает ОД игрока, должен позволять журналу показать тип действия, базовую стоимость, уровень искусства, итоговую стоимость, ОД до и после.",
            "  • Если обмен разрешает активное затратное действие противника, журнал боя показывает оба расхода ОД.",
            "  • Формула стоимости: итоговая стоимость = максимум из минимальной стоимости и разницы между базовой стоимостью и уровнем искусства.",
            "  • Уровни духовных искусств уменьшают стоимость действий; Средоточие Души увеличивает максимум ОД: уровни 0/1/2/3/4/5 дают 6/7/8/10/12/15 ОД. Всё это прокачивается локально через /spiritual_arts.",
            "  • Базовые стоимости: давление 3, защита 2, контрприём 4, манёвр 3, оковы 4, силовые оковы 5, разрыв оков 3, сопротивление воплощению 3, координация чемпиона 2.",
            "  • Собрать Средоточие (recover_spiritual_power) не тратит ОД и восстанавливает ОД: обычно +3 при успехе, +2 при частичном успехе, но не выше максимума.",
            "  • Собрать Средоточие выгодно против защиты, контрприёма, ожидания или пассивности; оно опасно против давления, манёвра, оков, силовых оков и принудительного воплощения — тогда восстановление ограничено 0..1 ОД, а действие противника проходит по своей линии.",
            "  • Отступление, сдача и переговоры остаются допустимыми даже при 0 ОД, если сама сцена позволяет такой выбор.",
            "",
            "[bold]Контроль и оковы[/]",
            "  • Контроль — отдельная ось боя, не урон и не позиция. Он ограничивает свободу действий стороны и создаёт рычаг для следующих ходов.",
            "  • Уровни контроля: нет контроля, стеснён, скован, запечатан. Активный контроль всегда указывает сторону-контролёра, источник, ограниченные действия и краткое описание.",
            "  • Наложение оков при успехе должно создать или усилить контроль игрока: нет контроля -> стеснён -> скован -> запечатан. Силовые оковы требуют более сильного рычага и дают более широкий контроль: минимум две ограниченные операции.",
            "  • Разрыв оков при успехе должен ослабить, снять или развернуть контроль против игрока. Если контроль не меняется, это не разрыв оков.",
            "  • Манёвр не проходит бесплатно сквозь активный контроль противника: сначала нужно ослабить контроль через разрыв оков, валидный контрприём, сопротивление воплощению, переговоры или сдачу.",
            "  • Защита может не дать новому входящему контролю усилиться, но не снимает уже наложенные оковы. Для снятия нужны разрыв оков или контрприём против конкретного входящего контроля.",
            "",
            "[bold]Духовные искусства: что выбирать[/]",
            "  • Давление — проактивная атака на устойчивость противника. Главный эффект: ухудшить напряжение противостоящей стороны. Выбирай, когда хочешь продавить волю/клятву/обет противника.",
            "  • Контрприём — реакция на конкретное входящее действие противника. Его преимущество над давлением: можно не просто ударить в ответ, а заблокировать, развернуть или наказать уже заявленное действие врага. Успешный контрприём обязан дать выигрыш: улучшить позицию, ухудшить напряжение противника либо ослабить или развернуть уже существующие вражеские оковы/контроль.",
            "  • Защита — снижает или предотвращает напряжение стороны игрока или последствие. Лучше контрприёма, когда нечего разворачивать или нужно пережить удар без риска: даже при провале против прямого давления ухудшение напряжения ограничено одним уровнем.",
            "  • Манёвр (maneuver) — меняет позицию. Выбирай, когда прямое давление опасно, но можно занять лучший духовный угол, разорвать дистанцию, вывести спор из чужой зоны силы. Под активным контролем противника манёвр сначала требует анти-контрольный ответ.",
            "  • Наложение оков и силовые оковы — контроль после преимущества. Не стартовая кнопка победы: сначала получи рычаг через позицию, подготовку или решительный успех.",
            "  • Разрыв оков — ответ на оковы, принудительную передачу/выброс или контекст принуждения. Это не универсальная атака.",
            "  • Сопротивление воплощению — только против принудительного воплощения Хранителем.",
            "  • Координация чемпиона — когда ведущий боец не игрок, а союзник/чемпион; игрок усиливает сторону, а не превращает сцену в массовый бой.",
            "  • Собрать Средоточие — восстановить ОД в момент, когда противник защищается, ждёт или не давит напрямую. Это не атака и не бесплатный пропуск опасного действия врага.",
            "",
            "[bold]Матрица приём-контрприём[/]",
            "  • Давление бьёт манёвр: если враг пытается занять позицию, прямой нажим может сорвать перестроение и ухудшить напряжение противника.",
            "  • Защита бьёт давление: это самый безопасный ответ на прямой нажим. Она не ранит противника, зато стабильно предотвращает или снижает напряжение стороны игрока, а при провале не даёт давлению сразу пробить несколько уровней напряжения.",
            "  • Контрприём бьёт прямое входящее действие: давление, оковы или принуждение можно развернуть обратно, но только если названо входящее действие. При провале контрприём обязан иметь риск для игрока.",
            "  • Манёвр бьёт пассивную защиту: он не наносит напряжение, но улучшает позицию и даёт будущий бонус к броскам. Его останавливают давление, встречный манёвр или контроль.",
            "  • Практический выбор: защита безопаснее контрприёма, контрприём сильнее только против конкретного входящего действия, манёвр даёт будущий бонус, но не защищает от прямого нажима.",
            "  • Оковы бьют противника после рычага: сначала нужно преимущество, подготовка или решительный успех; затем оковы ограничивают будущие действия. Силовые оковы дороже по требованиям, зато обязаны ограничивать шире обычных оков.",
            "  • Разрыв оков бьёт контроль: применяй его против оков, принудительной передачи/выброса и другого принуждения, а не вместо обычной защиты.",
            "  • Сопротивление воплощению бьёт только принудительное воплощение; добровольный переход в смертную жизнь не является боем.",
            "  • Собрать Средоточие бьёт пустой темп: защиту, контрприём без входящего действия, ожидание и пассивность. Его бьют давление, манёвр, оковы, силовые оковы и принудительное воплощение.",
            "  • Каждый новый спорный обмен с кубиками должен показывать, что сделал игрок, что сделал противник, какая линия решала исход, профиль риска и краткое объяснение.",
            "",
            "[bold]Прокачка[/]",
            "  • /spiritual_arts показывает ранги, текущие уровни искусств и стоимость улучшений.",
            "  • Ранги Просветления и Сияния открывают максимальный уровень искусства; сохранённый ранг Сияния продолжает помогать после возвращения в Море Хаоса.",
            "  • Прокачка принадлежит клиенту: клиент локально тратит Чернильные Перья или, в активной Сияющей Обители, Искры Света. ГМ не пишет квитанцию/отчёт прокачки.",
            "  • Прокачка заблокирована во время активного конфликта, активного жизненного цикла хода ГМ и открытых ожидающих контрактов со стоимостью.",
            "",
            "[bold]Награды[/]",
            "  • Победа в проверяемом спорном конфликте может дать валюту: Чернильные Перья в Море Хаоса или Искры Света в Сияющей Обители.",
            "  • Награда маленькая и формульная: зависит от силы противника, модели сторон, стартовой позиции и категории исхода.",
            "  • Нет награды за ремонтную отмену, отсутствие эффекта, добровольную сдачу/отступление, переговоры без состязания или повторную награду за тот же конфликт."
        };

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ⚔ Справка по духовному бою ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        WaitForKey();
        return Task.CompletedTask;
    }

    private async Task ShowSpiritualArtsAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Духовные искусства"))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Духовные искусства", "Духовные искусства посмертия доступны только в Море Хаоса и Сияющей Обители.");
            return;
        }

        while (true)
        {
            await _stateManager.RefreshGameStateAsync();
            var soulRoot = await ReadJsonObjectForAfterlifeStatusAsync("game_state/meta/soul_state.json");
            if (soulRoot == null)
            {
                ShowEmptyPanel("Духовные искусства", "Состояние души недоступно; прокачка духовных искусств заблокирована.");
                WaitForKey();
                return;
            }

            var shiningRoot = await ReadJsonObjectForAfterlifeStatusAsync(ShiningAbodeState.StatePath);
            var entityProfilesRoot = await ReadJsonObjectForAfterlifeStatusAsync(AfterlifeEntityProfileState.StatePath);
            var profile = BuildSyncedAfterlifeCombatProfile(soulRoot, shiningRoot);
            var quotes = BuildSpiritualArtUpgradeQuotes(
                profile,
                ReadPlayerLearnedSpecialArts(entityProfilesRoot),
                _stateManager.CurrentState.IsInShiningAbode);
            var spiritFocusQuote = BuildSpiritFocusUpgradeQuote(profile);

            Clear();
            Write(BuildSpiritualArtsPanel(soulRoot, shiningRoot, profile, quotes, spiritFocusQuote));

            var choice = Prompt(new SelectionPrompt<string>()
                .Title("[bold cyan]Действие духовных искусств[/]")
                .HighlightStyle(new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1))
                .AddChoices(
                    "⬆ Прокачать духовное искусство",
                    "← Назад"));

            if (!choice.Contains("Прокачать", StringComparison.OrdinalIgnoreCase))
                return;

            await HandleSpiritualArtUpgradeAsync(soulRoot, shiningRoot, quotes, spiritFocusQuote);
        }
    }

    private Panel BuildSpiritualArtsPanel(
        JsonObject soulRoot,
        JsonObject? shiningRoot,
        JsonObject profile,
        IReadOnlyList<SpiritualArtUpgradeQuote> quotes,
        SpiritFocusUpgradeQuote spiritFocusQuote)
    {
        var enlightenment = soulRoot["enlightenment"] as JsonObject;
        var radiance = shiningRoot?["radiance"] as JsonObject;
        var artTiers = profile["artTiers"] as JsonObject;
        var maxUnlockedTier = quotes.Count == 0 ? 0 : quotes.Max(quote => quote.MaxUnlockedTier);

        var lines = new List<string>
        {
            "[bold cyan]Духовные искусства посмертия[/]",
            "",
            "[bold]Текущий боевой профиль:[/]",
            $"  • Ранг Просветления: [white]{AfterlifeSpiritualConflictState.GetNodeInt(profile["enlightenmentRank"])}[/]",
            $"  • Ранг Сияния: [white]{AfterlifeSpiritualConflictState.GetNodeInt(profile["radianceRank"])}[/]",
            $"  • Сохранённый ранг Сияния: [white]{AfterlifeSpiritualConflictState.GetNodeInt(profile["retainedRadianceRank"])}[/]",
            $"  • Уровень Просветления души: [white]{AfterlifeSpiritualConflictState.GetNodeInt(enlightenment?["level"])}[/] [dim]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(enlightenment?["currentTier"]) ?? "")}[/]",
            $"  • Сияние Сияющей Обители: [white]{AfterlifeSpiritualConflictState.GetNodeInt(radiance?["experience"])}[/] опыта / уровень [white]{AfterlifeSpiritualConflictState.GetNodeInt(radiance?["tier"])}[/]",
            $"  • Средоточие Души: уровень [white]{spiritFocusQuote.CurrentTier}[/], макс ОД [white]{spiritFocusQuote.CurrentMaxActionPoints}[/], следующий макс ОД [white]{spiritFocusQuote.NextMaxActionPoints}[/]",
            $"  • Максимальный открытый уровень искусства: [white]{maxUnlockedTier}[/]",
            $"  • Доступные Чернильные Перья: [white]{ShiningAbodeState.GetSoulSpendableInkFeathers(soulRoot)}[/]",
            $"  • Искры Света: [gold1]{AfterlifeSpiritualConflictState.GetNodeInt(shiningRoot?["lightSparks"])}[/] [dim](тратятся только в обычной активной Сияющей Обители)[/]",
            "",
            "[bold]Средоточие Души:[/]",
            $"  • Уровень [white]{spiritFocusQuote.CurrentTier}[/] -> [white]{spiritFocusQuote.NextTier}[/], макс ОД [white]{spiritFocusQuote.CurrentMaxActionPoints}[/] -> [white]{spiritFocusQuote.NextMaxActionPoints}[/], {Markup.Escape(spiritFocusQuote.BlockReason == null ? $"цена {spiritFocusQuote.InkFeatherCost} 🪶" : $"заблокировано: {spiritFocusQuote.BlockReason}")}{Markup.Escape(_stateManager.CurrentState.IsInShiningAbode ? $" / {spiritFocusQuote.LightSparkCost} ✨" : "")}",
            $"    Смысл: {Markup.Escape(DescribeSpiritFocusTier(spiritFocusQuote.CurrentTier))}. Это увеличивает максимум ОД; уровни духовных искусств отдельно уменьшают стоимость действий.",
            "",
            "[bold]Искусства:[/]"
        };

        foreach (var quote in quotes)
        {
            var tier = quote.IsSpecialArt
                ? quote.CurrentTier
                : AfterlifeSpiritualConflictState.GetNodeInt(artTiers?[quote.Art.ArtId]);
            var blocked = quote.BlockReason == null
                ? $"следующий уровень {quote.NextTier}, цена {FormatSpiritualArtUpgradeCost(quote, _stateManager.CurrentState.IsInShiningAbode)}"
                : $"заблокировано: {quote.BlockReason}";
            lines.Add($"  • [white]{Markup.Escape(FormatSpiritualArtQuoteLabel(quote))}[/]: уровень [white]{tier}[/], порог ранга [white]{quote.RequiredRankLabel}[/], {Markup.Escape(blocked)} — {Markup.Escape(FormatSpiritualArtQuoteUse(quote))}");
            lines.Add($"    Правило: {Markup.Escape(FormatSpiritualArtQuoteRule(quote))}");
            lines.Add($"    Сильнее против: {Markup.Escape(FormatSpiritualArtStrongAgainst(quote.Art))}");
            lines.Add($"    Контрится: {Markup.Escape(FormatSpiritualArtCounteredBy(quote.Art))}");
            lines.Add($"    Пример: {Markup.Escape(FormatSpiritualArtExample(quote.Art))}");
        }

        lines.Add("");
        lines.Add("[bold]Лестница рангов Просветления:[/]");
        foreach (var rank in AfterlifeSpiritualConflictState.EnlightenmentRanks)
            lines.Add($"  • {rank.Rank}: {Markup.Escape(FormatRankIdLabel(rank.RankId))}, требует {rank.RequiredProgress}, открывает уровень искусства {rank.UnlocksArtTier}. {Markup.Escape(FormatRankMechanicalEffect(rank.MechanicalEffect))}");

        lines.Add("");
        lines.Add("[bold]Лестница рангов Сияния:[/]");
        foreach (var rank in AfterlifeSpiritualConflictState.RadianceRanks)
            lines.Add($"  • {rank.Rank}: {Markup.Escape(FormatRankIdLabel(rank.RankId))}, требует {rank.RequiredProgress}, открывает уровень искусства {rank.UnlocksArtTier}. {Markup.Escape(FormatRankMechanicalEffect(rank.MechanicalEffect))}");

        lines.Add("");
        lines.Add("[dim]Правило прокачки: ранги ограничивают максимальный уровень искусства; клиент локально обновляет боевой профиль души и тратит выбранную валюту. ГМ не пишет квитанцию/отчёт прокачки.[/]");

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" ✨ Духовные искусства ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private async Task HandleSpiritualArtUpgradeAsync(
        JsonObject soulRoot,
        JsonObject? shiningRoot,
        IReadOnlyList<SpiritualArtUpgradeQuote> quotes,
        SpiritFocusUpgradeQuote spiritFocusQuote)
    {
        var blocker = await TryDescribeSpiritualArtUpgradeBlockerAsync();
        if (blocker != null)
        {
            ShowEmptyPanel("Прокачка духовных искусств", blocker);
            WaitForKey();
            return;
        }

        var choicesByLabel = new Dictionary<string, SpiritualProfileUpgradeChoice?>(StringComparer.Ordinal);
        foreach (var quote in quotes)
            choicesByLabel[BuildSpiritualArtUpgradeChoiceLabel(quote, _stateManager.CurrentState.IsInShiningAbode)] = new SpiritualProfileUpgradeChoice(quote, null);
        choicesByLabel[BuildSpiritFocusUpgradeChoiceLabel(spiritFocusQuote)] = new SpiritualProfileUpgradeChoice(null, spiritFocusQuote);
        choicesByLabel["← Назад"] = null;

        var selected = Prompt(new SelectionPrompt<string>()
            .Title("[bold cyan]Выберите духовное искусство для прокачки[/]")
            .HighlightStyle(new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1))
            .PageSize(12)
            .AddChoices(choicesByLabel.Keys));

        if (!choicesByLabel.TryGetValue(selected, out var choice) || choice == null)
            return;

        if (choice.ArtQuote is { } artQuote)
        {
            await HandleSpiritualArtUpgradeChoiceAsync(soulRoot, shiningRoot, artQuote);
            return;
        }

        if (choice.SpiritFocusQuote is { } selectedSpiritFocusQuote)
            await HandleSpiritFocusUpgradeChoiceAsync(soulRoot, shiningRoot, selectedSpiritFocusQuote);
    }

    private async Task HandleSpiritualArtUpgradeChoiceAsync(
        JsonObject soulRoot,
        JsonObject? shiningRoot,
        SpiritualArtUpgradeQuote quote)
    {
        if (quote.BlockReason != null)
        {
            ShowEmptyPanel("Прокачка духовных искусств", quote.BlockReason);
            WaitForKey();
            return;
        }

        var currency = PromptSpiritualArtCurrency(quote, soulRoot, shiningRoot);
        if (currency == null)
            return;

        JsonObject? entityProfilesRoot = null;
        if (quote.IsSpecialArt)
        {
            var entityProfilesRead = await ReadJsonObjectForAfterlifeStatusResultAsync(AfterlifeEntityProfileState.StatePath);
            if (entityProfilesRead.Error != null || entityProfilesRead.Root == null)
            {
                ShowEmptyPanel(
                    "Прокачка духовных искусств",
                    $"Прокачка особого духовного искусства заблокирована: {AfterlifeEntityProfileState.StatePath} повреждён или отсутствует. Сначала исправьте профиль сущностей посмертия.");
                WaitForKey();
                return;
            }

            entityProfilesRoot = entityProfilesRead.Root;
        }

        var beforeSoulRoot = soulRoot.DeepClone().AsObject();
        var beforeShiningRoot = shiningRoot?.DeepClone()?.AsObject();
        var beforeEntityProfilesRoot = entityProfilesRoot?.DeepClone()?.AsObject();
        var projectedSoulRoot = soulRoot.DeepClone().AsObject();
        var projectedShiningRoot = shiningRoot?.DeepClone()?.AsObject();
        var projectedEntityProfilesRoot = entityProfilesRoot?.DeepClone()?.AsObject();
        var result = quote.IsSpecialArt && projectedEntityProfilesRoot != null
            ? ApplySpecialSpiritualArtUpgrade(projectedSoulRoot, projectedShiningRoot, projectedEntityProfilesRoot, quote, currency.Value)
            : ApplySpiritualArtUpgrade(projectedSoulRoot, projectedShiningRoot, quote, currency.Value);
        if (!result.Success)
        {
            ShowEmptyPanel("Прокачка духовных искусств", result.Message);
            WaitForKey();
            return;
        }

        Write(BuildSpiritualArtUpgradePreviewPanel(beforeSoulRoot, beforeShiningRoot, projectedSoulRoot, projectedShiningRoot, quote, currency.Value));
        WriteJsonAuditPanel(
            "JSON локальной прокачки духовного искусства",
            BuildSpiritualArtUpgradeAuditNode(beforeSoulRoot, beforeShiningRoot, beforeEntityProfilesRoot, projectedSoulRoot, projectedShiningRoot, projectedEntityProfilesRoot, quote, currency.Value),
            Color.Cyan1);

        if (!Confirm("[yellow]Подтвердить локальную прокачку духовного искусства?[/]", false))
        {
            MarkupLine("[dim]Прокачка отменена; состояние не изменено.[/]");
            WaitForKey();
            return;
        }

        if (!await SaveSpiritualArtUpgradeRootsAsync(projectedSoulRoot, projectedShiningRoot, currency.Value, projectedEntityProfilesRoot))
        {
            WaitForKey();
            return;
        }

        MarkupLine($"[green]Прокачано: {Markup.Escape(FormatSpiritualArtQuoteLabel(quote))}, уровень {quote.CurrentTier} -> {quote.NextTier}.[/]");
        WaitForKey();
    }

    private async Task HandleSpiritFocusUpgradeChoiceAsync(
        JsonObject soulRoot,
        JsonObject? shiningRoot,
        SpiritFocusUpgradeQuote quote)
    {
        if (quote.BlockReason != null)
        {
            ShowEmptyPanel("Прокачка духовных искусств", quote.BlockReason);
            WaitForKey();
            return;
        }

        var currency = PromptSpiritualArtCurrency(quote, shiningRoot);
        if (currency == null)
            return;

        var beforeSoulRoot = soulRoot.DeepClone().AsObject();
        var beforeShiningRoot = shiningRoot?.DeepClone()?.AsObject();
        var projectedSoulRoot = soulRoot.DeepClone().AsObject();
        var projectedShiningRoot = shiningRoot?.DeepClone()?.AsObject();
        var result = ApplySpiritFocusUpgrade(projectedSoulRoot, projectedShiningRoot, quote, currency.Value);
        if (!result.Success)
        {
            ShowEmptyPanel("Прокачка духовных искусств", result.Message);
            WaitForKey();
            return;
        }

        Write(BuildSpiritFocusUpgradePreviewPanel(beforeSoulRoot, beforeShiningRoot, projectedSoulRoot, projectedShiningRoot, quote, currency.Value));
        WriteJsonAuditPanel("JSON локальной прокачки Средоточия Души", BuildSpiritFocusUpgradeAuditNode(beforeSoulRoot, beforeShiningRoot, projectedSoulRoot, projectedShiningRoot, quote, currency.Value), Color.Cyan1);

        if (!Confirm("[yellow]Подтвердить локальную прокачку Средоточия Души?[/]", false))
        {
            MarkupLine("[dim]Прокачка отменена; состояние не изменено.[/]");
            WaitForKey();
            return;
        }

        if (!await SaveSpiritualArtUpgradeRootsAsync(projectedSoulRoot, projectedShiningRoot, currency.Value))
        {
            WaitForKey();
            return;
        }

        MarkupLine($"[green]Прокачано: Средоточие Души, уровень {quote.CurrentTier} -> {quote.NextTier}, макс ОД {quote.CurrentMaxActionPoints} -> {quote.NextMaxActionPoints}.[/]");
        WaitForKey();
    }

    private async Task<string?> TryDescribeSpiritualArtUpgradeBlockerAsync()
    {
        var activeTurnArtifacts = new List<string>();
        if (_fs.FileExists("input/turn_request.json"))
            activeTurnArtifacts.Add("input/turn_request.json");
        if (_fs.FileExists("game_state/control/pending_turn_snapshot.json"))
            activeTurnArtifacts.Add("game_state/control/pending_turn_snapshot.json");
        if (HasAnyShiningTreasuryPendingTurnSnapshotFile())
            activeTurnArtifacts.Add("game_state/control/pending_turn_snapshot");
        if (activeTurnArtifacts.Count > 0)
        {
            return "Прокачка духовных искусств заблокирована: найден активный жизненный цикл хода ГМ. " +
                   "Локальная прокачка меняет боевой профиль души и валюту, поэтому дождитесь завершения, отмены или ремонта текущего хода. " +
                   $"Найдено: {string.Join(", ", activeTurnArtifacts)}.";
        }

        var conflictRead = await ReadJsonObjectForAfterlifeStatusResultAsync(AfterlifeSpiritualConflictState.StatePath);
        if (conflictRead.Error != null)
        {
            return $"Прокачка духовных искусств заблокирована: состояние духовного конфликта повреждено ({conflictRead.Error}). Сначала выполните ремонт состояния.";
        }

        if (conflictRead.Root?["activeConflict"] is JsonObject)
        {
            return "Прокачка духовных искусств заблокирована: сейчас активен духовный конфликт посмертия. Завершите обмен действиями, завершение конфликта или ремонтную отмену перед изменением боевого профиля.";
        }

        if (conflictRead.Root != null &&
            conflictRead.Root.TryGetPropertyValue("activeConflict", out var activeConflict) &&
            activeConflict != null)
        {
            return "Прокачка духовных искусств заблокирована: состояние активного духовного конфликта повреждено. Сначала выполните ремонт состояния.";
        }

        var soulRead = await ReadJsonObjectForAfterlifeStatusResultAsync(SoulStatePath);
        if (soulRead.Error != null)
            return $"Прокачка духовных искусств заблокирована: {SoulStatePath} повреждён ({soulRead.Error}). Сначала исправьте состояние души.";
        if (soulRead.Root != null &&
            TryDescribeAfterlifeCombatProfileDamage(soulRead.Root, out var profileDamage))
        {
            return $"Прокачка духовных искусств заблокирована: {profileDamage}";
        }

        if (_fs.FileExists(GuardianAbodeOfferingState.PendingRequestPath))
        {
            return $"Прокачка духовных искусств заблокирована: найден незакрытый контракт с зарезервированной ценой {GuardianAbodeOfferingState.PendingRequestPath}. Дождитесь закрытия со статусом accepted или refused, либо ремонта.";
        }

        foreach (var archivePath in new[] { AfterlifeArchiveActionState.ConsultationRequestPath, AfterlifeArchiveActionState.ProjectFuelRequestPath })
        {
            if (_fs.FileExists(archivePath))
                return $"Прокачка духовных искусств заблокирована: найден незакрытый контракт Архива с зарезервированной ценой {archivePath}. Дождитесь закрытия со статусом (status) accepted, rejected или cancelled, либо ремонта (repair).";
        }

        if (_stateManager.CurrentState.IsInShiningAbode)
        {
            var shiningBlocker = await TryDescribeShiningTreasuryPendingCostBlockerAsync();
            if (shiningBlocker != null)
                return "Прокачка духовных искусств заблокирована из-за незакрытого ожидающего контракта Сияющей Обители с зарезервированной ценой. " + shiningBlocker;
        }

        return null;
    }

    private SpiritualArtCurrency? PromptSpiritualArtCurrency(
        SpiritualArtUpgradeQuote quote,
        JsonObject soulRoot,
        JsonObject? shiningRoot)
    {
        var choices = new List<string>();

        if (quote.InkFeatherCost > 0)
            choices.Add($"Чернильные Перья — {quote.InkFeatherCost} 🪶");

        if (_stateManager.CurrentState.IsInShiningAbode && shiningRoot != null && quote.LightSparkCost > 0)
            choices.Add($"Искры Света — {quote.LightSparkCost} ✨");

        if (choices.Count == 0)
            return null;

        choices.Add("← Назад");

        var selected = Prompt(new SelectionPrompt<string>()
            .Title("[bold cyan]Выберите валюту прокачки[/]")
            .HighlightStyle(new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1))
            .AddChoices(choices));

        if (selected.Contains("Назад", StringComparison.OrdinalIgnoreCase))
            return null;

        return selected.Contains("Искры", StringComparison.OrdinalIgnoreCase)
            ? SpiritualArtCurrency.LightSparks
            : SpiritualArtCurrency.InkFeathers;
    }

    private SpiritualArtCurrency? PromptSpiritualArtCurrency(
        SpiritFocusUpgradeQuote quote,
        JsonObject? shiningRoot)
    {
        var choices = new List<string>
        {
            $"Чернильные Перья — {quote.InkFeatherCost} 🪶",
        };

        if (_stateManager.CurrentState.IsInShiningAbode && shiningRoot != null)
            choices.Add($"Искры Света — {quote.LightSparkCost} ✨");

        choices.Add("← Назад");

        var selected = Prompt(new SelectionPrompt<string>()
            .Title("[bold cyan]Выберите валюту прокачки[/]")
            .HighlightStyle(new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1))
            .AddChoices(choices));

        if (selected.Contains("Назад", StringComparison.OrdinalIgnoreCase))
            return null;

        return selected.Contains("Искры", StringComparison.OrdinalIgnoreCase)
            ? SpiritualArtCurrency.LightSparks
            : SpiritualArtCurrency.InkFeathers;
    }

    private static (bool Success, string Message) ApplySpiritualArtUpgrade(
        JsonObject soulRoot,
        JsonObject? shiningRoot,
        SpiritualArtUpgradeQuote quote,
        SpiritualArtCurrency currency)
    {
        if (quote.BlockReason != null)
            return (false, quote.BlockReason);

        var profile = BuildSyncedAfterlifeCombatProfile(soulRoot, shiningRoot);
        var artTiers = profile["artTiers"] as JsonObject ?? new JsonObject();
        artTiers[quote.Art.ArtId] = quote.NextTier;
        profile["artTiers"] = artTiers;
        soulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = profile;

        if (currency == SpiritualArtCurrency.InkFeathers)
        {
            if (!TrySpendSoulInkFeathers(soulRoot, quote.InkFeatherCost, out var reason))
                return (false, reason);
        }
        else
        {
            if (shiningRoot == null)
                return (false, "Искры Света доступны для прокачки только в Сияющей Обители.");

            var current = AfterlifeSpiritualConflictState.GetNodeInt(shiningRoot["lightSparks"]);
            if (current < quote.LightSparkCost)
                return (false, $"Недостаточно Искр Света: нужно {quote.LightSparkCost}, доступно {current}.");

            shiningRoot["lightSparks"] = current - quote.LightSparkCost;
        }

        return (true, "ok");
    }

    private static (bool Success, string Message) ApplySpecialSpiritualArtUpgrade(
        JsonObject soulRoot,
        JsonObject? shiningRoot,
        JsonObject entityProfilesRoot,
        SpiritualArtUpgradeQuote quote,
        SpiritualArtCurrency currency)
    {
        if (quote.BlockReason != null)
            return (false, quote.BlockReason);

        var artId = quote.SpecialArtId;
        if (string.IsNullOrWhiteSpace(artId))
            return (false, "Особое духовное искусство не содержит идентификатор.");

        var playerProfile = FindPlayerSoulEntityProfile(entityProfilesRoot);
        if (playerProfile == null)
            return (false, $"В {AfterlifeEntityProfileState.StatePath} нет профиля души игрока (player_soul).");

        var specialArt = FindSpecialArtById(playerProfile, artId);
        if (specialArt == null)
            return (false, $"Профиль души игрока не содержит особое духовное искусство {artId}.");

        var currentTier = Math.Clamp(AfterlifeEntityProfileState.GetNodeInt(specialArt["tier"]), 0, SpiritualArtMaxTier);
        if (currentTier != quote.CurrentTier)
            return (false, $"Уровень особого духовного искусства изменился: ожидался {quote.CurrentTier}, сейчас {currentTier}. Обновите меню и повторите.");

        specialArt["tier"] = quote.NextTier;
        AppendSpecialArtUpgradeLedger(playerProfile, quote, currency);

        if (currency == SpiritualArtCurrency.InkFeathers)
        {
            if (!TrySpendSoulInkFeathers(soulRoot, quote.InkFeatherCost, out var reason))
                return (false, reason);

            SyncPlayerSoulProfileCurrencies(playerProfile, soulRoot, shiningRoot);
        }
        else
        {
            if (shiningRoot == null)
                return (false, "Искры Света доступны для прокачки только в Сияющей Обители.");

            var current = AfterlifeSpiritualConflictState.GetNodeInt(shiningRoot["lightSparks"]);
            if (current < quote.LightSparkCost)
                return (false, $"Недостаточно Искр Света: нужно {quote.LightSparkCost}, доступно {current}.");

            shiningRoot["lightSparks"] = current - quote.LightSparkCost;
            SyncPlayerSoulProfileCurrencies(playerProfile, soulRoot, shiningRoot);
        }

        return (true, "ok");
    }

    private static (bool Success, string Message) ApplySpiritFocusUpgrade(
        JsonObject soulRoot,
        JsonObject? shiningRoot,
        SpiritFocusUpgradeQuote quote,
        SpiritualArtCurrency currency)
    {
        if (quote.BlockReason != null)
            return (false, quote.BlockReason);

        var profile = BuildSyncedAfterlifeCombatProfile(soulRoot, shiningRoot);
        profile[AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = quote.NextTier;
        soulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = profile;

        if (currency == SpiritualArtCurrency.InkFeathers)
        {
            if (!TrySpendSoulInkFeathers(soulRoot, quote.InkFeatherCost, out var reason))
                return (false, reason);
        }
        else
        {
            if (shiningRoot == null)
                return (false, "Искры Света доступны для прокачки только в Сияющей Обители.");

            var current = AfterlifeSpiritualConflictState.GetNodeInt(shiningRoot["lightSparks"]);
            if (current < quote.LightSparkCost)
                return (false, $"Недостаточно Искр Света: нужно {quote.LightSparkCost}, доступно {current}.");

            shiningRoot["lightSparks"] = current - quote.LightSparkCost;
        }

        return (true, "ok");
    }

    private async Task<bool> SaveSpiritualArtUpgradeRootsAsync(
        JsonObject soulRoot,
        JsonObject? shiningRoot,
        SpiritualArtCurrency currency,
        JsonObject? entityProfilesRoot = null)
    {
        var blocker = await TryDescribeSpiritualArtUpgradeBlockerAsync();
        if (blocker != null)
        {
            ShowEmptyPanel("Прокачка духовных искусств", blocker);
            return false;
        }

        var previousSoulJson = await _fs.ReadFileAsync(SoulStatePath);
        var previousShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var previousEntityProfilesJson = entityProfilesRoot == null
            ? null
            : await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath);
        JsonObject? previousSoulRoot = null;

        if (!string.IsNullOrWhiteSpace(previousSoulJson))
        {
            try
            {
                previousSoulRoot = JsonNode.Parse(previousSoulJson) as JsonObject;
            }
            catch
            {
                previousSoulRoot = null;
            }

            if (previousSoulRoot == null)
            {
                MarkupLine("[red]Прокачка духовных искусств не может сохранить операцию: текущее состояние души нечитаемо. Сначала исправь состояние души.[/]");
                return false;
            }
        }

        try
        {
            await WriteCanonicalSoulStateJsonAsync(soulRoot);
            if (currency == SpiritualArtCurrency.LightSparks && shiningRoot != null)
                await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            if (entityProfilesRoot != null)
                await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, entityProfilesRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

            await _stateManager.RefreshGameStateAsync();
            return true;
        }
        catch (Exception ex)
        {
            if (previousSoulJson == null)
                _fs.DeleteFile(SoulStatePath);
            else if (previousSoulRoot != null)
                await WriteCanonicalSoulStateJsonAsync(previousSoulRoot);
            else
                _fs.DeleteFile(SoulStatePath);

            if (currency == SpiritualArtCurrency.LightSparks)
            {
                if (previousShiningJson != null)
                    await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, previousShiningJson);
                else
                    _fs.DeleteFile(ShiningAbodeState.StatePath);
            }

            if (entityProfilesRoot != null)
            {
                if (previousEntityProfilesJson != null)
                    await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, previousEntityProfilesJson);
                else
                    _fs.DeleteFile(AfterlifeEntityProfileState.StatePath);
            }

            MarkupLine($"[red]Не удалось сохранить прокачку духовного искусства; состояние восстановлено: {Markup.Escape(ex.Message)}[/]");
            return false;
        }
    }

    private static JsonObject BuildSyncedAfterlifeCombatProfile(JsonObject soulRoot, JsonObject? shiningRoot)
    {
        var profile = soulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty]?.DeepClone() as JsonObject
                      ?? AfterlifeSpiritualConflictState.CreateDefaultCombatProfile();

        if (profile["schemaVersion"] is not JsonValue)
            profile["schemaVersion"] = 1;
        if (profile["artTiers"] is not JsonObject)
            profile["artTiers"] = new JsonObject();
        if (!profile.ContainsKey(AfterlifeSpiritualConflictState.SpiritFocusTierProperty))
            profile[AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = 0;

        var enlightenmentRank = ResolveEnlightenmentRank(soulRoot);
        var radianceRank = ResolveRadianceRank(shiningRoot);
        var previousRetained = AfterlifeSpiritualConflictState.GetNodeInt(profile["retainedRadianceRank"]);

        profile["enlightenmentRank"] = enlightenmentRank;
        profile["radianceRank"] = radianceRank;
        profile["retainedRadianceRank"] = shiningRoot != null
            ? Math.Max(previousRetained, radianceRank)
            : previousRetained;
        if (!profile.ContainsKey("lastRecoveryTurn"))
            profile["lastRecoveryTurn"] = 0;

        return profile;
    }

    private static IReadOnlyList<JsonObject> ReadPlayerLearnedSpecialArts(JsonObject? entityProfilesRoot)
    {
        var playerProfile = FindPlayerSoulEntityProfile(entityProfilesRoot);
        if (playerProfile?["specialArts"] is not JsonArray specialArts)
            return Array.Empty<JsonObject>();

        return specialArts
            .OfType<JsonObject>()
            .Select(art => art.DeepClone() as JsonObject ?? new JsonObject())
            .Where(art => !string.IsNullOrWhiteSpace(AfterlifeEntityProfileState.GetNodeString(art["artId"])))
            .ToArray();
    }

    private static JsonObject? FindPlayerSoulEntityProfile(JsonObject? entityProfilesRoot)
    {
        if (entityProfilesRoot?[AfterlifeEntityProfileState.ProfilesProperty] is not JsonArray profiles)
            return null;

        return profiles
            .OfType<JsonObject>()
            .FirstOrDefault(profile =>
                string.Equals(AfterlifeEntityProfileState.GetNodeString(profile["actorType"]), "player_soul", StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject? FindSpecialArtById(JsonObject profile, string artId)
    {
        if (profile["specialArts"] is not JsonArray specialArts)
            return null;

        return specialArts
            .OfType<JsonObject>()
            .FirstOrDefault(art => string.Equals(AfterlifeEntityProfileState.GetNodeString(art["artId"]), artId, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonArray EnsureSpiritualArtJsonArray(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonArray array)
            return array;

        array = new JsonArray();
        root[propertyName] = array;
        return array;
    }

    private static JsonObject EnsureSpiritualArtJsonObject(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonObject obj)
            return obj;

        obj = new JsonObject();
        root[propertyName] = obj;
        return obj;
    }

    private static void SyncPlayerSoulProfileCurrencies(
        JsonObject playerProfile,
        JsonObject soulRoot,
        JsonObject? shiningRoot)
    {
        var currencies = EnsureSpiritualArtJsonObject(playerProfile, "currencies");
        currencies["inkFeathers"] = ReadSoulInkFeathers(soulRoot).Current;
        currencies["lightSparks"] = shiningRoot == null
            ? 0
            : Math.Max(0, AfterlifeSpiritualConflictState.GetNodeInt(shiningRoot["lightSparks"]));
    }

    private static void AppendSpecialArtUpgradeLedger(
        JsonObject playerProfile,
        SpiritualArtUpgradeQuote quote,
        SpiritualArtCurrency currency)
    {
        var ledger = EnsureSpiritualArtJsonArray(playerProfile, "ledger");
        ledger.Add(new JsonObject
        {
            ["entryId"] = $"special_art_local_upgrade_{quote.SpecialArtId}_{quote.NextTier}",
            ["reason"] = "special_art_local_upgrade",
            ["summary"] = "Игрок локально прокачал особое духовное искусство.",
            ["sourceSurface"] = "spiritual_arts_local_upgrade",
            ["artId"] = quote.SpecialArtId,
            ["displayName"] = FormatSpiritualArtQuoteLabel(quote),
            ["tierBefore"] = quote.CurrentTier,
            ["tierAfter"] = quote.NextTier,
            ["currency"] = DescribeSpiritualArtCurrencyToken(currency),
            ["cost"] = currency == SpiritualArtCurrency.LightSparks ? quote.LightSparkCost : quote.InkFeatherCost
        });
    }

    private static IReadOnlyList<SpiritualArtUpgradeQuote> BuildSpiritualArtUpgradeQuotes(
        JsonObject profile,
        IReadOnlyList<JsonObject> playerSpecialArts,
        bool isInShiningAbode)
    {
        var maxUnlockedTier = ResolveMaxUnlockedSpiritualArtTier(profile);
        var artTiers = profile["artTiers"] as JsonObject;
        var result = new List<SpiritualArtUpgradeQuote>();
        foreach (var art in AfterlifeSpiritualConflictState.SpiritualArts)
        {
            var currentTier = Math.Clamp(AfterlifeSpiritualConflictState.GetNodeInt(artTiers?[art.ArtId]), 0, SpiritualArtMaxTier);
            var nextTier = Math.Min(SpiritualArtMaxTier, currentTier + 1);
            var requiredRankLabel = DescribeRequiredRankForArtTier(Math.Max(art.MinUnlockTier, nextTier));
            string? blockReason = null;
            if (currentTier >= SpiritualArtMaxTier)
                blockReason = "уже достигнут максимальный уровень искусства 5";
            else if (maxUnlockedTier < art.MinUnlockTier)
                blockReason = $"нужен ранг, открывающий уровень искусства {art.MinUnlockTier}: {DescribeRequiredRankForArtTier(art.MinUnlockTier)}";
            else if (nextTier > maxUnlockedTier)
                blockReason = $"нужен ранг, открывающий уровень искусства {nextTier}: {requiredRankLabel}";

            result.Add(new SpiritualArtUpgradeQuote(
                art,
                currentTier,
                nextTier,
                maxUnlockedTier,
                ComputeSpiritualArtInkFeatherCost(art, nextTier),
                ComputeSpiritualArtLightSparkCost(art, nextTier),
                requiredRankLabel,
                blockReason));
        }

        foreach (var specialArt in playerSpecialArts)
        {
            var artId = AfterlifeEntityProfileState.GetNodeString(specialArt["artId"]);
            var displayName = AfterlifeEntityProfileState.GetNodeString(specialArt["displayName"]) ?? artId;
            var baseOperation = AfterlifeEntityProfileState.GetNodeString(specialArt["baseOperation"]);
            if (string.IsNullOrWhiteSpace(artId) || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(baseOperation))
                continue;

            var baseArt = AfterlifeSpiritualConflictState.SpiritualArts
                .FirstOrDefault(art => string.Equals(art.ArtId, baseOperation, StringComparison.OrdinalIgnoreCase));
            if (baseArt == null)
                continue;

            var currentTier = Math.Clamp(AfterlifeEntityProfileState.GetNodeInt(specialArt["tier"]), 0, SpiritualArtMaxTier);
            var nextTier = Math.Min(SpiritualArtMaxTier, currentTier + 1);
            var requiredRankLabel = DescribeRequiredRankForArtTier(Math.Max(baseArt.MinUnlockTier, nextTier));
            var upgradeCost = specialArt["upgradeCost"] as JsonObject;
            var inkCost = Math.Max(0, AfterlifeEntityProfileState.GetNodeInt(upgradeCost?["inkFeathers"]));
            var sparkCost = Math.Max(0, AfterlifeEntityProfileState.GetNodeInt(upgradeCost?["lightSparks"]));
            string? blockReason = null;
            if (currentTier >= SpiritualArtMaxTier)
                blockReason = "уже достигнут максимальный уровень особого искусства 5";
            else if (maxUnlockedTier < baseArt.MinUnlockTier)
                blockReason = $"нужен ранг, открывающий базовое действие уровня {baseArt.MinUnlockTier}: {DescribeRequiredRankForArtTier(baseArt.MinUnlockTier)}";
            else if (nextTier > maxUnlockedTier)
                blockReason = $"нужен ранг, открывающий уровень искусства {nextTier}: {requiredRankLabel}";
            else if (inkCost <= 0 && sparkCost <= 0)
                blockReason = "у особого искусства должна быть положительная цена прокачки в Чернильных Перьях или Искрах Света";
            else if (inkCost <= 0 && sparkCost > 0 && !isInShiningAbode)
                blockReason = "цена указана только в Искрах Света; такая прокачка доступна только в обычной активной Сияющей Обители";

            result.Add(new SpiritualArtUpgradeQuote(
                baseArt,
                currentTier,
                nextTier,
                maxUnlockedTier,
                inkCost,
                sparkCost,
                requiredRankLabel,
                blockReason,
                artId,
                displayName,
                AfterlifeEntityProfileState.GetNodeString(specialArt["effectSummary"]),
                FormatAfterlifeSpecialArtCombatEffect(specialArt)));
        }

        return result;
    }

    private static SpiritFocusUpgradeQuote BuildSpiritFocusUpgradeQuote(JsonObject profile)
    {
        var maxUnlockedTier = ResolveMaxUnlockedSpiritualArtTier(profile);
        var currentTier = Math.Clamp(
            AfterlifeSpiritualConflictState.GetNodeInt(profile[AfterlifeSpiritualConflictState.SpiritFocusTierProperty]),
            0,
            AfterlifeSpiritualConflictState.SpiritFocusMaxTier);
        var nextTier = Math.Min(AfterlifeSpiritualConflictState.SpiritFocusMaxTier, currentTier + 1);
        var requiredRankLabel = DescribeRequiredRankForArtTier(nextTier);
        string? blockReason = null;
        if (currentTier >= AfterlifeSpiritualConflictState.SpiritFocusMaxTier)
            blockReason = "уже достигнут максимальный уровень Средоточия Души 5";
        else if (nextTier > maxUnlockedTier)
            blockReason = $"нужен ранг, открывающий уровень {nextTier}: {requiredRankLabel}";

        return new SpiritFocusUpgradeQuote(
            currentTier,
            nextTier,
            AfterlifeSpiritualConflictState.GetSpiritFocusMaxActionPoints(currentTier),
            AfterlifeSpiritualConflictState.GetSpiritFocusMaxActionPoints(nextTier),
            maxUnlockedTier,
            ComputeSpiritFocusInkFeatherCost(nextTier),
            ComputeSpiritFocusLightSparkCost(nextTier),
            requiredRankLabel,
            blockReason);
    }

    private static int ResolveMaxUnlockedSpiritualArtTier(JsonObject profile)
    {
        var enlightenmentRank = AfterlifeSpiritualConflictState.GetNodeInt(profile["enlightenmentRank"]);
        var radianceRank = AfterlifeSpiritualConflictState.GetNodeInt(profile["radianceRank"]);
        var retainedRadianceRank = AfterlifeSpiritualConflictState.GetNodeInt(profile["retainedRadianceRank"]);
        return Math.Clamp(
            Math.Max(
                ResolveUnlockedTierFromRanks(AfterlifeSpiritualConflictState.EnlightenmentRanks, enlightenmentRank),
                Math.Max(
                    ResolveUnlockedTierFromRanks(AfterlifeSpiritualConflictState.RadianceRanks, radianceRank),
                    ResolveUnlockedTierFromRanks(AfterlifeSpiritualConflictState.RadianceRanks, retainedRadianceRank))),
            0,
            SpiritualArtMaxTier);
    }

    private static int ResolveUnlockedTierFromRanks(
        IReadOnlyList<AfterlifeSpiritualConflictState.RankDefinition> ranks,
        int rank)
    {
        return ranks
            .Where(definition => definition.Rank <= rank)
            .Select(definition => definition.UnlocksArtTier)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static int ResolveEnlightenmentRank(JsonObject soulRoot)
    {
        var directProgress = AfterlifeSpiritualConflictState.GetNodeInt(soulRoot["enlightenment"]);
        var enlightenment = soulRoot["enlightenment"] as JsonObject;
        var soulProgression = soulRoot["soulProgression"] as JsonObject;
        var progress = Math.Max(
            Math.Max(directProgress, AfterlifeSpiritualConflictState.GetNodeInt(enlightenment?["experience"])),
            Math.Max(
                AfterlifeSpiritualConflictState.GetNodeInt(soulProgression?["totalExperience"]),
                AfterlifeSpiritualConflictState.GetNodeInt(soulProgression?["progressPercent"])));
        var tier = Math.Max(
            AfterlifeSpiritualConflictState.GetNodeInt(enlightenment?["level"]),
            AfterlifeSpiritualConflictState.GetNodeInt(soulProgression?["tier"]));
        return Math.Clamp(
            Math.Max(tier, ResolveRankFromProgress(AfterlifeSpiritualConflictState.EnlightenmentRanks, progress)),
            0,
            AfterlifeSpiritualConflictState.EnlightenmentRanks.Max(rank => rank.Rank));
    }

    private static int ResolveRadianceRank(JsonObject? shiningRoot)
    {
        var radiance = shiningRoot?["radiance"] as JsonObject;
        var progress = AfterlifeSpiritualConflictState.GetNodeInt(radiance?["experience"]);
        var tier = AfterlifeSpiritualConflictState.GetNodeInt(radiance?["tier"]);
        return Math.Clamp(
            Math.Max(tier, ResolveRankFromProgress(AfterlifeSpiritualConflictState.RadianceRanks, progress)),
            0,
            AfterlifeSpiritualConflictState.RadianceRanks.Max(rank => rank.Rank));
    }

    private static int ResolveRankFromProgress(
        IReadOnlyList<AfterlifeSpiritualConflictState.RankDefinition> ranks,
        int progress)
    {
        return ranks
            .Where(rank => progress >= rank.RequiredProgress)
            .Select(rank => rank.Rank)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static string DescribeRequiredRankForArtTier(int tier)
    {
        var enlightenmentRank = AfterlifeSpiritualConflictState.EnlightenmentRanks
            .FirstOrDefault(rank => rank.UnlocksArtTier >= tier);
        var radianceRank = AfterlifeSpiritualConflictState.RadianceRanks
            .FirstOrDefault(rank => rank.UnlocksArtTier >= tier);

        var parts = new List<string>();
        if (enlightenmentRank != null)
            parts.Add($"Просветление {enlightenmentRank.Rank}: {FormatRankIdLabel(enlightenmentRank.RankId)}");
        if (radianceRank != null)
            parts.Add($"Сияние {radianceRank.Rank}: {FormatRankIdLabel(radianceRank.RankId)}");

        return parts.Count == 0 ? "не открывается текущими шкалами" : string.Join(" или ", parts);
    }

    private static int ComputeSpiritualArtInkFeatherCost(
        AfterlifeSpiritualConflictState.SpiritualArtDefinition art,
        int nextTier) =>
        checked(50 + nextTier * 50 + art.MinUnlockTier * 25);

    private static int ComputeSpiritualArtLightSparkCost(
        AfterlifeSpiritualConflictState.SpiritualArtDefinition art,
        int nextTier) =>
        checked(4 + nextTier * 3 + art.MinUnlockTier);

    private static int ComputeSpiritFocusInkFeatherCost(int nextTier) =>
        checked(100 + nextTier * 100);

    private static int ComputeSpiritFocusLightSparkCost(int nextTier) =>
        checked(8 + nextTier * 4);

    private static string BuildSpiritualArtUpgradeChoiceLabel(SpiritualArtUpgradeQuote quote, bool includeLightSparks)
    {
        var status = quote.BlockReason == null
            ? $"уровень {quote.CurrentTier}->{quote.NextTier}, {FormatSpiritualArtUpgradeCost(quote, includeLightSparks)}"
            : $"заблокировано: {quote.BlockReason}";
        return $"{FormatSpiritualArtQuoteLabel(quote)} — {status}";
    }

    private static string FormatSpiritualArtUpgradeCost(SpiritualArtUpgradeQuote quote, bool includeLightSparks)
    {
        var parts = new List<string>();
        if (quote.InkFeatherCost > 0)
            parts.Add($"{quote.InkFeatherCost} 🪶");
        if (includeLightSparks && quote.LightSparkCost > 0)
            parts.Add($"{quote.LightSparkCost} ✨");

        return parts.Count == 0
            ? "0 🪶"
            : string.Join(" / ", parts);
    }

    private static string FormatSpiritualArtQuoteLabel(SpiritualArtUpgradeQuote quote) =>
        quote.IsSpecialArt
            ? quote.SpecialArtDisplayName ?? quote.SpecialArtId ?? FormatSpiritualArtLabel(quote.Art)
            : FormatSpiritualArtLabel(quote.Art);

    private static string FormatSpiritualArtQuoteUse(SpiritualArtUpgradeQuote quote)
    {
        if (!quote.IsSpecialArt)
            return FormatSpiritualArtUse(quote.Art);

        var effect = string.IsNullOrWhiteSpace(quote.SpecialArtEffectSummary)
            ? "особый эффект должен быть описан ГМ при применении искусства"
            : NormalizeAfterlifeCombatPlayerText(quote.SpecialArtEffectSummary) ?? quote.SpecialArtEffectSummary;
        return string.IsNullOrWhiteSpace(quote.SpecialArtCombatEffect)
            ? $"особое искусство на основе действия «{FormatSpiritualArtLabel(quote.Art)}». {effect}"
            : $"особое искусство на основе действия «{FormatSpiritualArtLabel(quote.Art)}». {effect}. {quote.SpecialArtCombatEffect}";
    }

    private static string FormatSpiritualArtQuoteRule(SpiritualArtUpgradeQuote quote)
    {
        if (!quote.IsSpecialArt)
            return FormatSpiritualArtRule(quote.Art);

        return FormatSpiritualArtRule(quote.Art) +
               " Особый эффект применяется только если ГМ записывает заметку о его влиянии в аудите обмена.";
    }

    private static string BuildSpiritFocusUpgradeChoiceLabel(SpiritFocusUpgradeQuote quote)
    {
        var status = quote.BlockReason == null
            ? $"уровень {quote.CurrentTier}->{quote.NextTier}, макс ОД {quote.CurrentMaxActionPoints}->{quote.NextMaxActionPoints}, {quote.InkFeatherCost} 🪶"
            : $"заблокировано: {quote.BlockReason}";
        return $"Средоточие Души — {status}";
    }

    private static string DescribeSpiritFocusTier(int tier) =>
        AfterlifeSpiritualConflictState.SpiritFocusTiers
            .FirstOrDefault(definition => definition.Tier == Math.Clamp(tier, 0, AfterlifeSpiritualConflictState.SpiritFocusMaxTier))
            ?.PlayerMeaning ?? "Базовый запас души";

    private static bool TryDescribeAfterlifeCombatProfileDamage(JsonObject soulRoot, out string damage)
    {
        damage = "";
        if (!soulRoot.TryGetPropertyValue(AfterlifeSpiritualConflictState.SoulStateProfileProperty, out var profileNode) ||
            profileNode == null)
        {
            return false;
        }

        if (profileNode is not JsonObject profile)
        {
            damage = $"{SoulStatePath}.{AfterlifeSpiritualConflictState.SoulStateProfileProperty} должен быть object. Сначала исправьте боевой профиль души.";
            return true;
        }

        if (profile.TryGetPropertyValue(AfterlifeSpiritualConflictState.SpiritFocusTierProperty, out var spiritFocusNode) &&
            spiritFocusNode != null &&
            (!TryGetJsonNodeInt(spiritFocusNode, out var spiritFocusTier) ||
             spiritFocusTier < 0 ||
             spiritFocusTier > AfterlifeSpiritualConflictState.SpiritFocusMaxTier))
        {
            damage = $"{SoulStatePath}.{AfterlifeSpiritualConflictState.SoulStateProfileProperty}.{AfterlifeSpiritualConflictState.SpiritFocusTierProperty} должен быть integer 0..5.";
            return true;
        }

        if (profile.TryGetPropertyValue("artTiers", out var artTiersNode) &&
            artTiersNode != null &&
            artTiersNode is not JsonObject)
        {
            damage = $"{SoulStatePath}.{AfterlifeSpiritualConflictState.SoulStateProfileProperty}.artTiers должен быть object.";
            return true;
        }

        return false;
    }

    private static bool TryGetJsonNodeInt(JsonNode? node, out int value)
    {
        value = 0;
        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<int>(out value))
                return true;
            if (jsonValue.TryGetValue<string>(out var text) && int.TryParse(text, out value))
                return true;
        }

        return false;
    }

    private static (int Current, int Total) ReadSoulInkFeathers(JsonObject soulRoot)
    {
        var node = soulRoot["inkFeathers"];
        if (node is JsonObject obj)
        {
            var current = Math.Max(0, AfterlifeSpiritualConflictState.GetNodeInt(obj["current"]));
            var total = Math.Max(current, AfterlifeSpiritualConflictState.GetNodeInt(obj["total"], current));
            return (current, total);
        }

        var value = Math.Max(0, AfterlifeSpiritualConflictState.GetNodeInt(node));
        return (value, value);
    }

    private static bool TrySpendSoulInkFeathers(JsonObject soulRoot, int cost, out string reason)
    {
        reason = "";
        if (cost <= 0)
        {
            reason = "Стоимость должна быть положительной.";
            return false;
        }

        var (current, total) = ReadSoulInkFeathers(soulRoot);
        if (current < cost)
        {
            reason = $"Недостаточно Чернильных Перьев: нужно {cost}, доступно {current}.";
            return false;
        }

        soulRoot["inkFeathers"] = new JsonObject
        {
            ["current"] = current - cost,
            ["total"] = Math.Max(total, current)
        };
        return true;
    }

    private static Panel BuildSpiritualArtUpgradePreviewPanel(
        JsonObject beforeSoulRoot,
        JsonObject? beforeShiningRoot,
        JsonObject afterSoulRoot,
        JsonObject? afterShiningRoot,
        SpiritualArtUpgradeQuote quote,
        SpiritualArtCurrency currency)
    {
        var lines = new List<string>
        {
            "[bold cyan]Предпросмотр локальной прокачки духовного искусства[/]",
            "",
            $"  • Искусство: [white]{Markup.Escape(FormatSpiritualArtQuoteLabel(quote))}[/]",
            $"  • Уровень: [white]{quote.CurrentTier}[/] -> [white]{quote.NextTier}[/]",
            $"  • Валюта: [white]{DescribeSpiritualArtCurrency(currency)}[/]",
            $"  • Чернильные Перья: [white]{ReadSoulInkFeathers(beforeSoulRoot).Current}[/] -> [white]{ReadSoulInkFeathers(afterSoulRoot).Current}[/]",
            $"  • Искры Света: [white]{AfterlifeSpiritualConflictState.GetNodeInt(beforeShiningRoot?["lightSparks"])}[/] -> [white]{AfterlifeSpiritualConflictState.GetNodeInt(afterShiningRoot?["lightSparks"])}[/]",
            "",
            "[dim]Это операция клиента: ход ГМ, квитанция и отчёт не создаются.[/]"
        };

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Прокачка духовного искусства ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(currency == SpiritualArtCurrency.LightSparks ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private static JsonObject BuildSpiritualArtUpgradeAuditNode(
        JsonObject beforeSoulRoot,
        JsonObject? beforeShiningRoot,
        JsonObject? beforeEntityProfilesRoot,
        JsonObject afterSoulRoot,
        JsonObject? afterShiningRoot,
        JsonObject? afterEntityProfilesRoot,
        SpiritualArtUpgradeQuote quote,
        SpiritualArtCurrency currency) =>
        new()
        {
            ["sourceSurface"] = "spiritual_arts_local_upgrade",
            ["gmTurnSent"] = false,
            ["receiptWritten"] = false,
            ["upgradeType"] = quote.IsSpecialArt ? "special_art" : "standard_art",
            ["artId"] = quote.SpecialArtId ?? quote.Art.ArtId,
            ["displayName"] = FormatSpiritualArtQuoteLabel(quote),
            ["tierBefore"] = quote.CurrentTier,
            ["tierAfter"] = quote.NextTier,
            ["currency"] = DescribeSpiritualArtCurrencyToken(currency),
            ["cost"] = currency == SpiritualArtCurrency.LightSparks ? quote.LightSparkCost : quote.InkFeatherCost,
            ["before"] = new JsonObject
            {
                ["soulInkFeathersCurrent"] = ReadSoulInkFeathers(beforeSoulRoot).Current,
                ["lightSparks"] = AfterlifeSpiritualConflictState.GetNodeInt(beforeShiningRoot?["lightSparks"]),
                ["afterlifeCombatProfile"] = beforeSoulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty]?.DeepClone(),
                ["afterlifeEntityProfiles"] = beforeEntityProfilesRoot?[AfterlifeEntityProfileState.ProfilesProperty]?.DeepClone()
            },
            ["after"] = new JsonObject
            {
                ["soulInkFeathersCurrent"] = ReadSoulInkFeathers(afterSoulRoot).Current,
                ["lightSparks"] = AfterlifeSpiritualConflictState.GetNodeInt(afterShiningRoot?["lightSparks"]),
                ["afterlifeCombatProfile"] = afterSoulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty]?.DeepClone(),
                ["afterlifeEntityProfiles"] = afterEntityProfilesRoot?[AfterlifeEntityProfileState.ProfilesProperty]?.DeepClone()
            },
            ["affectedFiles"] = BuildSpiritualArtUpgradeAffectedFiles(currency, quote.IsSpecialArt)
        };

    private static Panel BuildSpiritFocusUpgradePreviewPanel(
        JsonObject beforeSoulRoot,
        JsonObject? beforeShiningRoot,
        JsonObject afterSoulRoot,
        JsonObject? afterShiningRoot,
        SpiritFocusUpgradeQuote quote,
        SpiritualArtCurrency currency)
    {
        var lines = new List<string>
        {
            "[bold cyan]Предпросмотр локальной прокачки Средоточия Души[/]",
            "",
            "  • Параметр: [white]Средоточие Души[/]",
            $"  • Уровень: [white]{quote.CurrentTier}[/] -> [white]{quote.NextTier}[/]",
            $"  • Максимум ОД: [white]{quote.CurrentMaxActionPoints}[/] -> [white]{quote.NextMaxActionPoints}[/]",
            $"  • Валюта: [white]{DescribeSpiritualArtCurrency(currency)}[/]",
            $"  • Чернильные Перья: [white]{ReadSoulInkFeathers(beforeSoulRoot).Current}[/] -> [white]{ReadSoulInkFeathers(afterSoulRoot).Current}[/]",
            $"  • Искры Света: [white]{AfterlifeSpiritualConflictState.GetNodeInt(beforeShiningRoot?["lightSparks"])}[/] -> [white]{AfterlifeSpiritualConflictState.GetNodeInt(afterShiningRoot?["lightSparks"])}[/]",
            "",
            "[dim]Это операция клиента: ГМ не пишет и не подтверждает Средоточие Души. В новом духовном конфликте максимум ОД берётся из этого уровня.[/]"
        };

        return new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Прокачка Средоточия Души ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(currency == SpiritualArtCurrency.LightSparks ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        };
    }

    private static JsonObject BuildSpiritFocusUpgradeAuditNode(
        JsonObject beforeSoulRoot,
        JsonObject? beforeShiningRoot,
        JsonObject afterSoulRoot,
        JsonObject? afterShiningRoot,
        SpiritFocusUpgradeQuote quote,
        SpiritualArtCurrency currency) =>
        new()
        {
            ["sourceSurface"] = "spiritual_arts_local_upgrade",
            ["upgradeType"] = "spirit_focus",
            ["gmTurnSent"] = false,
            ["receiptWritten"] = false,
            ["displayName"] = "Средоточие Души",
            ["tierBefore"] = quote.CurrentTier,
            ["tierAfter"] = quote.NextTier,
            ["maxActionPointsBefore"] = quote.CurrentMaxActionPoints,
            ["maxActionPointsAfter"] = quote.NextMaxActionPoints,
            ["currency"] = DescribeSpiritualArtCurrencyToken(currency),
            ["cost"] = currency == SpiritualArtCurrency.LightSparks ? quote.LightSparkCost : quote.InkFeatherCost,
            ["before"] = new JsonObject
            {
                ["soulInkFeathersCurrent"] = ReadSoulInkFeathers(beforeSoulRoot).Current,
                ["lightSparks"] = AfterlifeSpiritualConflictState.GetNodeInt(beforeShiningRoot?["lightSparks"]),
                ["afterlifeCombatProfile"] = beforeSoulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty]?.DeepClone()
            },
            ["after"] = new JsonObject
            {
                ["soulInkFeathersCurrent"] = ReadSoulInkFeathers(afterSoulRoot).Current,
                ["lightSparks"] = AfterlifeSpiritualConflictState.GetNodeInt(afterShiningRoot?["lightSparks"]),
                ["afterlifeCombatProfile"] = afterSoulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty]?.DeepClone()
            },
            ["affectedFiles"] = currency == SpiritualArtCurrency.LightSparks
                ? new JsonArray(SoulStatePath, ShiningAbodeState.StatePath)
                : new JsonArray(SoulStatePath)
        };

    private static string DescribeSpiritualArtCurrency(SpiritualArtCurrency currency) =>
        currency == SpiritualArtCurrency.LightSparks ? "Искры Света" : "Чернильные Перья";

    private static string DescribeSpiritualArtCurrencyToken(SpiritualArtCurrency currency) =>
        currency == SpiritualArtCurrency.LightSparks ? "light_sparks" : "ink_feathers";

    private static JsonArray BuildSpiritualArtUpgradeAffectedFiles(
        SpiritualArtCurrency currency,
        bool isSpecialArt)
    {
        var files = new JsonArray(SoulStatePath);
        if (currency == SpiritualArtCurrency.LightSparks)
            files.Add(ShiningAbodeState.StatePath);
        if (isSpecialArt)
            files.Add(AfterlifeEntityProfileState.StatePath);

        return files;
    }

    private async Task ShowSpiritualActionAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Действие в духовном конфликте"))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Духовное действие", "Духовное действие посмертия доступно только в Море Хаоса и Сияющей Обители.");
            return;
        }

        var root = await ReadJsonObjectForAfterlifeStatusAsync(AfterlifeSpiritualConflictState.StatePath);
        var active = root?["activeConflict"] as JsonObject;
        if (active == null)
        {
            ShowEmptyPanel("Духовное действие", "Нет активного духовного конфликта посмертия. Конфликт должен начать ГМ через отыгрыш и обновление принятого хода.");
            return;
        }

        var conflictId = AfterlifeSpiritualConflictState.GetNodeString(active["conflictId"]) ?? "unknown";
        Clear();
        MarkupLine($"[cyan]Активный конфликт:[/] [white]{Markup.Escape(conflictId)}[/]");
        MarkupLine("[dim]Опишите одно намерение: давление, защита, манёвр, контрприём, разрыв или наложение духовных оков, сдача, отступление или переговоры. Команда только добавляет явный тег; обычная ролевая заявка во время активного конфликта тоже валидна.[/]");
        MarkupLine("[dim]Выберите действие по механике: давление бьёт по напряжению противника; защита удерживает свою сторону; манёвр двигает позицию конфликта, но не проходит бесплатно сквозь активный контроль; контрприём требует конкретное входящее действие; оковы требуют преимущества или подготовки и меняют контроль; разрыв оков и сопротивление воплощению работают только против контроля или принуждения; координация чемпиона — только в поединке чемпиона.[/]");
        MarkupLine("[dim]Сопоставление действий: защита безопаснее контрприёма против давления; контрприём сильнее только против конкретного входящего действия и рискован при провале; манёвр даёт будущий бонус позиции, но его останавливают давление, встречный манёвр или контроль.[/]");
        var action = Ask("[cyan]Действие:[/]");
        if (string.IsNullOrWhiteSpace(action))
            return;

        var gmAction =
            $"[AFTERLIFE_SPIRITUAL_ACTION: {conflictId}] {action.Trim()}\n\n" +
            "Разреши это как обмен действиями активного духовного конфликта посмертия. " +
            $"Если конфликт меняется, запиши `{AfterlifeSpiritualConflictState.ResponseField}` с `mode=exchange` или `mode=resolve`. " +
            "Не используй файлы смертного боя, здоровье, энергию, списки врагов/союзников (enemiesData/alliesData), смертные поверхности NPC/world/faction или прямые награды валютой.";

        var preview = new JsonObject
        {
            ["playerActionTag"] = "AFTERLIFE_SPIRITUAL_ACTION",
            ["conflictId"] = conflictId,
            ["playerAction"] = action.Trim(),
            ["expectedResponseSurface"] = AfterlifeSpiritualConflictState.ResponseField,
            ["stateFile"] = AfterlifeSpiritualConflictState.StatePath
        };

        if (!ConfirmChaosSeaContractPreview(
                "Предпросмотр духовного действия посмертия",
                new List<string>
                {
                    "[bold]Контракт ГМ:[/]",
                    $"  • Активный конфликт (active conflict): {Markup.Escape(conflictId)}",
                    $"  • Поверхность ответа (response surface): `{AfterlifeSpiritualConflictState.ResponseField}`",
                    $"  • Файл состояния (state file): `{AfterlifeSpiritualConflictState.StatePath}`",
                    "  • Конфликт остаётся сторона-против-стороны: используй сторону игрока (playerSide), противостоящую сторону (oppositionSide) и поля напряжения сторон.",
            "  • Духовное искусство должно менять только разрешённые поля: давление (pressure)=напряжение противника, защита (guard)=защита своей стороны, манёвр (maneuver)=позиция, контрприём (counter)=входящее действие (incomingAction), оковы (binding)=controlState после преимущества, разрыв/сопротивление (break_binding/incarnation_resistance)=анти-принуждение, координация чемпиона (champion_coordination)=поединок чемпиона (champion_duel).",
                    "  • Для спорного обмена с кубиками ГМ должен указать аудит сопоставления действий (matchupAudit): действие игрока, действие противника, линию разрешения, профиль риска и объяснение выбора.",
                    "  • Принудительное воплощение Хранителем требует доказательство проигрыша/сдачи/уступки в завершении (resolve).",
                    "  • Файлы смертного боя и смертного состояния запрещены."
                },
                preview,
                "Аудит духовного действия",
                confirmChoice: "✅ Отправить действие ГМ"))
        {
            return;
        }

        _pendingGmAction = gmAction;
    }

    private static string FormatSideModelLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "direct_duel" => "прямой поединок",
            "assisted_duel" => "поединок с поддержкой",
            "champion_duel" => "поединок чемпиона/союзника",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatConflictPositionLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "opposition_dominant" => "противник доминирует",
            "opposition_advantaged" => "преимущество противника",
            "contested" => "спорная позиция",
            "player_advantaged" => "преимущество игрока",
            "player_dominant" => "игрок доминирует",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatSideStrainLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "clear" => "устойчиво",
            "strained" => "напряжено",
            "fractured" => "надломлено",
            "overwhelmed" => "подавлено",
            "broken" => "сломлено",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatResolutionStateLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "active" => "активен",
            "concession_pending" => "уступка ожидает закрытия",
            "surrender_pending" => "сдача ожидает закрытия",
            "retreat_pending" => "отступление ожидает закрытия",
            "ready_to_resolve" => "готов к завершению",
            "resolved" => "завершён",
            "repair_cancelled" => "отменён ремонтным путём",
            "" => "?",
            _ => value ?? "?"
        };

    private static void AppendVisibleCombatConditions(List<string> lines, JsonArray? combatConditions)
    {
        if (combatConditions == null)
            return;

        var visible = combatConditions
            .OfType<JsonObject>()
            .Where(AfterlifeCombatConditionPlayerAuditSanitizer.IsVisibleToPlayer)
            .Where(static condition => string.Equals(AfterlifeSpiritualConflictState.GetNodeString(condition["status"]) ?? "active", "active", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (visible.Length == 0)
            return;

        lines.Add("[bold]Боевые условия[/] [dim](combatConditions)[/]");
        foreach (var condition in visible)
        {
            var name = AfterlifeSpiritualConflictState.GetNodeString(condition["displayName"]) ??
                       AfterlifeSpiritualConflictState.GetNodeString(condition["name"]) ??
                       AfterlifeSpiritualConflictState.GetNodeString(condition["conditionId"]) ??
                       "Без названия";
            var kind = AfterlifeSpiritualConflictState.GetNodeString(condition["kind"]) ?? "?";
            var target = DescribeCombatConditionTarget(condition);
            var source = DescribeCombatConditionSource(condition["source"] as JsonObject);
            var operations = DescribeCombatConditionStringArray(condition["affectedOperations"] as JsonArray);
            var duration = DescribeCombatConditionDuration(condition["duration"] as JsonObject);
            var counterplay = DescribeCombatConditionStringArray(condition["counterplay"] as JsonArray);
            var summary = AfterlifeSpiritualConflictState.GetNodeString(condition["summary"]) ?? "";
            lines.Add($"  • [white]{Markup.Escape(name)}[/] ({Markup.Escape(kind)}): цель={Markup.Escape(target)}, источник={Markup.Escape(source)}, действия={Markup.Escape(operations)}, срок={Markup.Escape(duration)}");
            lines.Add($"    Ответ: {Markup.Escape(counterplay)}");
            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add($"    Итог: {Markup.Escape(summary)}");
        }

        lines.Add("");
    }

    private static string DescribeCombatConditionTarget(JsonObject condition)
    {
        if (condition["target"] is JsonObject target)
        {
            var targetSideValue = AfterlifeSpiritualConflictState.GetNodeString(target["side"]) ??
                                  AfterlifeSpiritualConflictState.GetNodeString(target["targetSide"]) ??
                                  "?";
            var targetActorValue = AfterlifeSpiritualConflictState.GetNodeString(target["displayName"]) ??
                                   AfterlifeSpiritualConflictState.GetNodeString(target["actorId"]) ??
                                   AfterlifeSpiritualConflictState.GetNodeString(target["actorRef"]);
            return string.IsNullOrWhiteSpace(targetActorValue)
                ? targetSideValue
                : $"{targetSideValue}:{targetActorValue}";
        }

        var targetSide = AfterlifeSpiritualConflictState.GetNodeString(condition["targetSide"]) ?? "?";
        var targetActor = AfterlifeSpiritualConflictState.GetNodeString(condition["targetActorRef"]) ??
                          AfterlifeSpiritualConflictState.GetNodeString(condition["targetActorId"]);
        return string.IsNullOrWhiteSpace(targetActor)
            ? targetSide
            : $"{targetSide}:{targetActor}";
    }

    private static string DescribeCombatConditionSource(JsonObject? source)
    {
        if (source == null)
            return "не указан";

        var parts = new[]
        {
            AfterlifeSpiritualConflictState.GetNodeString(source["type"]) ??
            AfterlifeSpiritualConflictState.GetNodeString(source["sourceType"]),
            AfterlifeSpiritualConflictState.GetNodeString(source["actorId"]) ??
            AfterlifeSpiritualConflictState.GetNodeString(source["sourceId"]),
            AfterlifeSpiritualConflictState.GetNodeString(source["displayName"])
        }
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return parts.Length == 0 ? "не указан" : string.Join(":", parts);
    }

    private static string DescribeCombatConditionDuration(JsonObject? duration)
    {
        if (duration == null)
            return "не указано";

        var parts = new List<string>();
        var type = AfterlifeSpiritualConflictState.GetNodeString(duration["type"]);
        if (!string.IsNullOrWhiteSpace(type))
            parts.Add(type);
        if (duration.ContainsKey("remainingUses"))
            parts.Add($"remainingUses={AfterlifeSpiritualConflictState.GetNodeInt(duration["remainingUses"])}");
        if (duration.ContainsKey("expiresAtTurn"))
            parts.Add($"expiresAtTurn={AfterlifeSpiritualConflictState.GetNodeInt(duration["expiresAtTurn"])}");
        var until = AfterlifeSpiritualConflictState.GetNodeString(duration["until"]);
        if (!string.IsNullOrWhiteSpace(until))
            parts.Add($"until={until}");
        return parts.Count == 0 ? "не указано" : string.Join("; ", parts);
    }

    private static string DescribeCombatConditionStringArray(JsonArray? array)
    {
        if (array == null)
            return "нет";

        var values = array
            .Select(AfterlifeSpiritualConflictState.GetNodeString)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return values.Length == 0 ? "нет" : string.Join("; ", values);
    }

    private static JsonNode? BuildPlayerFacingCombatConditionAudit(JsonNode? root)
    {
        if (root == null)
            return null;

        return AfterlifeCombatConditionPlayerAuditSanitizer.Sanitize(root);
    }

    private static void AppendSpiritualExchangeLog(List<string> lines, JsonArray exchangeLog)
    {
        lines.Add("[bold]Обмены действиями[/]");
        var index = 1;
        foreach (var exchange in exchangeLog.OfType<JsonObject>())
        {
            var operationType = FormatOperationTypeLabel(AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]));
            var outcome = FormatOutcomeLabel(AfterlifeSpiritualConflictState.GetNodeString(exchange["outcome"]));
            lines.Add($"  • #{index}: [white]{Markup.Escape(operationType)}[/] -> {Markup.Escape(outcome)}");
            lines.Add($"    Состояние: {Markup.Escape(DescribeExchangeStateDelta(exchange))}");

            if (exchange["incomingAction"] is JsonObject incomingAction)
                lines.Add($"    Входящее действие: {Markup.Escape(DescribeIncomingAction(incomingAction))}");

            if (exchange["counterPayoff"] is JsonObject counterPayoff)
                lines.Add($"    Выигрыш контрприёма: {Markup.Escape(DescribeCounterPayoff(counterPayoff))}");

            if (exchange["diceAudit"] is JsonObject diceAudit)
                lines.Add($"    Кубики: {Markup.Escape(DescribeDiceAudit(diceAudit))}");

            if (exchange["actionCostAudit"] is JsonObject actionCostAudit)
                lines.Add($"    ОД: {Markup.Escape(DescribeActionCostAudit(actionCostAudit))}");

            if (exchange["rewardAudit"] is JsonObject rewardAudit)
                lines.Add($"    Награда: {Markup.Escape(DescribeRewardAudit(rewardAudit))}");

            var summary = AfterlifeSpiritualConflictState.GetNodeString(exchange["summary"]);
            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add($"    Кратко: {Markup.Escape(summary)}");

            index++;
        }
    }

    private static void AppendSpiritualRecentConflictLog(List<string> lines, JsonArray recentConflicts)
    {
        lines.Add("[bold]Недавние завершённые конфликты[/]");
        var index = 1;
        foreach (var conflict in recentConflicts.OfType<JsonObject>())
        {
            var resolutionState = FormatResolutionStateLabel(AfterlifeSpiritualConflictState.GetNodeString(conflict["resolutionState"]));
            var operationType = FormatOperationTypeLabel(AfterlifeSpiritualConflictState.GetNodeString(conflict["operationType"]));
            var playerOutcome = AfterlifeSpiritualConflictState.GetNodeString(conflict["playerOutcome"]) ??
                                AfterlifeSpiritualConflictState.GetNodeString(conflict["resolutionKind"]) ??
                                "?";
            lines.Add($"  • #{index}: [white]{Markup.Escape(resolutionState)}[/], приём: {Markup.Escape(operationType)}, исход игрока: {Markup.Escape(FormatPlayerOutcomeLabel(playerOutcome))}");
            lines.Add($"    Ход: {Markup.Escape(FormatIntOrUnknown(conflict["resolvedAtTurn"] ?? conflict["turnNumber"]))}");

            if (conflict["diceAudit"] is JsonObject diceAudit)
                lines.Add($"    Кубики: {Markup.Escape(DescribeDiceAudit(diceAudit))}");

            if (conflict["rewardAudit"] is JsonObject rewardAudit)
                lines.Add($"    Награда: {Markup.Escape(DescribeRewardAudit(rewardAudit))}");

            var summary = AfterlifeSpiritualConflictState.GetNodeString(conflict["summary"]);
            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add($"    Кратко: {Markup.Escape(summary)}");

            if (conflict["exchangeLog"] is JsonArray exchangeLog && exchangeLog.Count > 0)
            {
                lines.Add("    История обменов:");
                var exchangeIndex = 1;
                foreach (var exchange in exchangeLog.OfType<JsonObject>())
                {
                    lines.Add($"      - Обмен #{exchangeIndex}: {Markup.Escape(DescribeExchangeStateDelta(exchange))}");
                    exchangeIndex++;
                }
            }

            index++;
        }
    }

    private static string DescribeExchangeStateDelta(JsonObject exchange)
    {
        var before = exchange["before"] as JsonObject;
        var after = exchange["after"] as JsonObject;
        return
            $"позиция {FormatConflictPositionLabel(AfterlifeSpiritualConflictState.GetNodeString(before?["conflictPosition"]))} -> {FormatConflictPositionLabel(AfterlifeSpiritualConflictState.GetNodeString(after?["conflictPosition"]))}; " +
            $"напряжение игрока {FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(before?["playerSideStrain"]))} -> {FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(after?["playerSideStrain"]))}; " +
            $"напряжение противника {FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(before?["oppositionSideStrain"]))} -> {FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(after?["oppositionSideStrain"]))}; " +
            $"контроль/оковы {DescribeControlState(before?["controlState"] as JsonObject)} -> {DescribeControlState(after?["controlState"] as JsonObject)}; " +
            $"ОД {DescribeActionEconomy(before?["actionEconomy"] as JsonObject)} -> {DescribeActionEconomy(after?["actionEconomy"] as JsonObject)}";
    }

    private static string DescribeIncomingAction(JsonObject incomingAction)
    {
        var operationType = FormatOperationTypeLabel(AfterlifeSpiritualConflictState.GetNodeString(incomingAction["operationType"]));
        var actorId = AfterlifeSpiritualConflictState.GetNodeString(incomingAction["actorId"]);
        var summary = AfterlifeSpiritualConflictState.GetNodeString(incomingAction["summary"]);
        var parts = new List<string> { operationType };
        if (!string.IsNullOrWhiteSpace(actorId))
            parts.Add($"актор={actorId}");
        if (!string.IsNullOrWhiteSpace(summary))
            parts.Add(summary);
        return string.Join("; ", parts);
    }

    private static string DescribeCounterPayoff(JsonObject counterPayoff)
    {
        var summary = AfterlifeSpiritualConflictState.GetNodeString(counterPayoff["summary"]) ??
                      AfterlifeSpiritualConflictState.GetNodeString(counterPayoff["effect"]) ??
                      AfterlifeSpiritualConflictState.GetNodeString(counterPayoff["effectSummary"]);
        if (!string.IsNullOrWhiteSpace(summary))
            return summary;

        var parts = counterPayoff
            .Select(property => AfterlifeSpiritualConflictState.GetNodeString(property.Value))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return parts.Length == 0 ? "зафиксирован выигрыш контрприёма" : string.Join("; ", parts);
    }

    private static string DescribeDiceAudit(JsonObject diceAudit)
    {
        var playerDie = FormatDiceValue(diceAudit, "player");
        var oppositionDie = FormatDiceValue(diceAudit, "opposition");
        var playerTotal = FormatIntOrUnknown(diceAudit["playerTotal"]);
        var oppositionTotal = FormatIntOrUnknown(diceAudit["oppositionTotal"]);
        var margin = FormatIntOrUnknown(diceAudit["margin"]);
        var outcomeBand = AfterlifeSpiritualConflictState.GetNodeString(diceAudit["outcomeBand"]) ?? "?";
        var difficulty = FormatDifficultyAudit(diceAudit["difficultyAudit"] as JsonObject);
        var parts = new List<string>
        {
            $"d20 игрока={playerDie}",
            $"d20 противника={oppositionDie}",
            $"итоги бросков {playerTotal}/{oppositionTotal}",
            $"разница={margin}",
            $"категория исхода={FormatOutcomeBandLabel(outcomeBand)}"
        };
        if (!string.IsNullOrWhiteSpace(difficulty))
            parts.Add(difficulty);
        return string.Join(", ", parts);
    }

    private static string DescribeRewardAudit(JsonObject rewardAudit)
    {
        var currency = FormatRewardCurrencyLabel(AfterlifeSpiritualConflictState.GetNodeString(rewardAudit["currency"]));
        var finalAmount = FormatIntOrUnknown(rewardAudit["finalAmount"]);
        var baseAmount = FormatIntOrUnknown(rewardAudit["baseAmount"]);
        var challengeTier = FormatIntOrUnknown(rewardAudit["challengeTier"]);
        var riskMultiplier = FormatIntOrUnknown(rewardAudit["riskMultiplierPercent"]);
        var outcomeMultiplier = FormatIntOrUnknown(rewardAudit["outcomeMultiplierPercent"]);
        var difficulty = FormatDifficultyAudit(rewardAudit["difficultyAudit"] as JsonObject);
        var parts = new List<string>
        {
            $"{currency}: итог={finalAmount}",
            $"база={baseAmount}",
            $"уровень вызова={challengeTier}",
            $"риск={riskMultiplier}%",
            $"исход={outcomeMultiplier}%"
        };
        if (!string.IsNullOrWhiteSpace(difficulty))
            parts.Add(difficulty);
        return string.Join(", ", parts);
    }

    private static string FormatDifficultyAudit(JsonObject? difficultyAudit)
    {
        if (difficultyAudit == null)
            return string.Empty;

        var difficulty = AfterlifeSpiritualConflictState.GetNodeString(difficultyAudit["difficulty"]);
        var label = AfterlifeSpiritualConflictState.GetNodeString(difficultyAudit["russianLabel"]);
        if (string.IsNullOrWhiteSpace(label))
            label = FormatDifficultyLabel(difficulty);

        var oppositionModifier = FormatSignedIntOrUnknown(difficultyAudit["oppositionModifier"]);
        var rewardMultiplier = FormatIntOrUnknown(difficultyAudit["rewardMultiplierPercent"]);
        return $"сложность: {label}, модификатор противника {oppositionModifier}, множитель награды {rewardMultiplier}%";
    }

    private static string DescribeActionEconomy(JsonObject? actionEconomy)
    {
        if (actionEconomy == null)
            return "нет данных";

        return $"игрок {DescribeActionPool(actionEconomy["player"] as JsonObject)}, противник {DescribeActionPool(actionEconomy["opposition"] as JsonObject)}";
    }

    private static string DescribeActionPool(JsonObject? pool)
    {
        if (pool == null)
            return "?";

        var current = FormatIntOrUnknown(pool["current"]);
        var max = FormatIntOrUnknown(pool["max"]);
        return $"{current}/{max}";
    }

    private static string DescribeActionCostAudit(JsonObject actionCostAudit)
    {
        var parts = new List<string>();
        if (actionCostAudit["player"] is JsonObject playerAudit)
            parts.Add(DescribeActionCostAuditSide("игрок", playerAudit));
        if (actionCostAudit["opposition"] is JsonObject oppositionAudit)
            parts.Add(DescribeActionCostAuditSide("противник", oppositionAudit));

        return parts.Count == 0
            ? actionCostAudit.ToJsonString()
            : string.Join("; ", parts);
    }

    private static string DescribeActionCostAuditSide(string label, JsonObject audit)
    {
        var operationType = FormatOperationTypeLabel(AfterlifeSpiritualConflictState.GetNodeString(audit["operationType"]));
        var before = FormatIntOrUnknown(audit["before"]);
        var after = FormatIntOrUnknown(audit["after"]);
        var baseCost = FormatIntOrUnknown(audit["baseCost"]);
        var minCost = FormatIntOrUnknown(audit["minCost"]);
        var artTier = FormatIntOrUnknown(audit["artTier"]);
        var effectiveCost = FormatIntOrUnknown(audit["effectiveCost"]);
        return $"{label}: {operationType}, ОД {before}->{after}, база={baseCost}, минимум={minCost}, уровень искусства={artTier}, итоговая стоимость={effectiveCost}";
    }

    private static string FormatDiceValue(JsonObject diceAudit, string side)
    {
        if (diceAudit["diceUsed"] is not JsonArray diceUsed)
            return "?";

        var normalizedSide = NormalizeDiceSide(side);
        var sideDice = diceUsed
            .OfType<JsonObject>()
            .Where(die => string.Equals(
                NormalizeDiceSide(AfterlifeSpiritualConflictState.GetNodeString(die["side"])),
                normalizedSide,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (sideDice.Count == 0)
            return "?";

        var selected = sideDice.FirstOrDefault(die =>
            string.Equals(AfterlifeSpiritualConflictState.GetNodeString(die["selection"]), "selected", StringComparison.OrdinalIgnoreCase)) ??
            sideDice[0];
        var selectedValue = FormatIntOrUnknown(selected["value"]);
        var details = new List<string>();
        var mode = DescribeDiceRollMode(diceAudit, normalizedSide);
        if (!string.IsNullOrWhiteSpace(mode))
            details.Add(mode);

        var discardedValues = sideDice
            .Where(die => string.Equals(AfterlifeSpiritualConflictState.GetNodeString(die["selection"]), "discarded", StringComparison.OrdinalIgnoreCase))
            .Select(die => FormatIntOrUnknown(die["value"]))
            .Where(value => value != "?")
            .ToList();
        if (discardedValues.Count > 0)
            details.Add($"отброшено: {string.Join(", ", discardedValues)}");

        return details.Count == 0
            ? selectedValue
            : $"{selectedValue} ({string.Join("; ", details)})";
    }

    private static string DescribeDiceRollMode(JsonObject diceAudit, string normalizedSide)
    {
        if (diceAudit["rollMode"] is not JsonObject rollModeRoot ||
            rollModeRoot[normalizedSide] is not JsonObject sideMode)
        {
            return string.Empty;
        }

        var advantageSources = ReadDiceRollModeSources(sideMode, "advantageSources");
        var disadvantageSources = ReadDiceRollModeSources(sideMode, "disadvantageSources");
        var effectiveMode = NormalizeKey(AfterlifeSpiritualConflictState.GetNodeString(sideMode["effectiveMode"]));
        var label = effectiveMode switch
        {
            "advantage" => "Преимущество",
            "great_advantage" => "Великое Преимущество",
            "disadvantage" => "Помеха",
            "dire_disadvantage" => "Тяжкая Помеха",
            "normal" when advantageSources.Count > 0 && disadvantageSources.Count > 0 => "Преимущество и Помеха погашены",
            _ => string.Empty
        };

        var allSources = advantageSources.Concat(disadvantageSources).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (allSources.Count == 0)
            return label;

        var sourceText = $"источник: {string.Join(", ", allSources)}";
        return string.IsNullOrWhiteSpace(label)
            ? sourceText
            : $"{label}; {sourceText}";
    }

    private static List<string> ReadDiceRollModeSources(JsonObject sideMode, string propertyName)
    {
        if (sideMode[propertyName] is not JsonArray sources)
            return new List<string>();

        var result = new List<string>();
        foreach (var source in sources)
        {
            if (source is JsonObject sourceObject)
            {
                var summary =
                    AfterlifeSpiritualConflictState.GetNodeString(sourceObject["summary"]) ??
                    AfterlifeSpiritualConflictState.GetNodeString(sourceObject["source"]) ??
                    AfterlifeSpiritualConflictState.GetNodeString(sourceObject["sourceId"]) ??
                    AfterlifeSpiritualConflictState.GetNodeString(sourceObject["id"]);
                if (string.IsNullOrWhiteSpace(summary))
                    continue;

                var level = NormalizeKey(AfterlifeSpiritualConflictState.GetNodeString(sourceObject["level"]));
                var suffix = level switch
                {
                    "great_advantage" => " — Великое Преимущество",
                    "dire_disadvantage" => " — Тяжкая Помеха",
                    "advantage" => " — Преимущество",
                    "disadvantage" => " — Помеха",
                    _ => string.Empty
                };
                result.Add(summary + suffix);
                continue;
            }

            var sourceText = AfterlifeSpiritualConflictState.GetNodeString(source);
            if (!string.IsNullOrWhiteSpace(sourceText))
                result.Add(sourceText!);
        }

        return result;
    }

    private static string NormalizeDiceSide(string? side) =>
        NormalizeKey(side) switch
        {
            "playerside" or "player_side" or "soul" => "player",
            "oppositionside" or "opposition_side" or "guardian" => "opposition",
            var value => value
        };

    private static string FormatIntOrUnknown(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var number))
            return number.ToString();

        var text = AfterlifeSpiritualConflictState.GetNodeString(node);
        return string.IsNullOrWhiteSpace(text) ? "?" : text;
    }

    private static string FormatSignedIntOrUnknown(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var number))
            return number >= 0 ? $"+{number}" : number.ToString();

        var text = AfterlifeSpiritualConflictState.GetNodeString(node);
        return string.IsNullOrWhiteSpace(text) ? "?" : text;
    }

    private static string FormatDifficultyLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "normal" => "Нормальная",
            "hard" => "Тяжёлая",
            "impossible" => "Невозможная",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatRewardCurrencyLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "ink_feathers" => "Чернильные Перья",
            "light_sparks" => "Искры Света",
            "" => "?",
            _ => value ?? "?"
        };

    private static string DescribeControlState(JsonObject? controlState)
    {
        if (controlState == null)
            return "нет контроля";

        var level = NormalizeKey(AfterlifeSpiritualConflictState.GetNodeString(controlState["level"]));
        if (string.IsNullOrWhiteSpace(level) || level == "none")
            return "нет контроля";

        var controllerSide = FormatControlSideLabel(AfterlifeSpiritualConflictState.GetNodeString(controlState["controllerSide"]));
        var controlId = AfterlifeSpiritualConflictState.GetNodeString(controlState["controlId"]);
        var sourceOperation = FormatOperationTypeLabel(AfterlifeSpiritualConflictState.GetNodeString(controlState["sourceOperation"]));
        var restrictions = controlState["restrictedOperations"] is JsonArray restrictedOperations
            ? string.Join(", ", restrictedOperations.Select(item => AfterlifeSpiritualConflictState.GetNodeString(item)).Where(item => !string.IsNullOrWhiteSpace(item)))
            : "?";
        var summary = AfterlifeSpiritualConflictState.GetNodeString(controlState["summary"]);
        var parts = new List<string>
        {
            FormatControlLevelLabel(level),
            $"контролирует: {controllerSide}",
            $"ограничено: {restrictions}"
        };
        if (!string.IsNullOrWhiteSpace(controlId))
            parts.Add($"id={controlId}");
        if (!string.IsNullOrWhiteSpace(sourceOperation) && sourceOperation != "?")
            parts.Add($"источник: {sourceOperation}");
        if (!string.IsNullOrWhiteSpace(summary))
            parts.Add(summary);
        return string.Join("; ", parts);
    }

    private static string FormatControlLevelLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "hindered" => "стеснён",
            "bound" => "скован",
            "locked" => "запечатан",
            "none" or "" => "нет контроля",
            _ => value ?? "?"
        };

    private static string FormatControlSideLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "player" => "игрок",
            "opposition" => "противник",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatOperationTypeLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "pressure" => "Давление",
            "counter" => "Контрприём",
            "guard" => "Защита",
            "maneuver" => "Манёвр",
            "binding" => "Наложение оков",
            "force_binding" => "Силовое наложение оков",
            "break_binding" => "Разрыв оков",
            "force_incarnation" => "Принудительное воплощение",
            "incarnation_resistance" => "Сопротивление воплощению",
            "champion_coordination" => "Координация чемпиона",
            "recover_spiritual_power" => "Собрать Средоточие",
            "withdraw" => "Отступление",
            "surrender" => "Сдача",
            "negotiate" => "Переговоры",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatOutcomeLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "success" => "успех",
            "partial_success" => "частичный успех",
            "blocked" => "заблокировано",
            "countered" => "контрировано",
            "setback" => "неудача/откат",
            "no_effect" => "без эффекта",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatActorTypeLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "player" => "игрок",
            "guardian" => "Хранитель",
            "resident" => "резидент Обители",
            "radiant_actor" => "светозарный актор",
            "custom_afterlife_actor" => "особый актор посмертия",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatAfterlifeRealmLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "chaos sea" or "chaos_sea" or "chaossea" or "море хаоса" => "Море Хаоса",
            "shining abode" or "shining_abode" or "shiningabode" or "сияющая обитель" => "Сияющая Обитель",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatPlayerOutcomeLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "won" or "success" or "player_success" => "победа",
            "partial_success" => "частичный успех",
            "lost" or "failure" or "player_failure" => "поражение",
            "surrendered" or "surrender" => "сдача",
            "conceded" or "concession" => "уступка",
            "withdraw" or "withdrew" => "отступление",
            "no_effect" => "без эффекта",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatOutcomeBandLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "player_success" => "успех игрока",
            "player_decisive_success" or "decisive_player_success" => "решительный успех игрока",
            "player_partial_success" or "partial_success" => "частичный успех игрока",
            "opposition_success" => "успех противника",
            "opposition_decisive_success" or "decisive_opposition_success" => "решительный успех противника",
            "stalemate" or "draw" => "ничья",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatSpiritualArtLabel(AfterlifeSpiritualConflictState.SpiritualArtDefinition art) =>
        NormalizeKey(art.ArtId) switch
        {
            "pressure" => "Давление",
            "counter" => "Контрприём",
            "guard" => "Защита",
            "maneuver" => "Манёвр",
            "break_binding" => "Разрыв оков",
            "binding" => "Наложение оков",
            "incarnation_resistance" => "Сопротивление воплощению",
            "champion_coordination" => "Координация чемпиона",
            _ => art.DisplayName
        };

    private static string FormatSpiritualArtUse(AfterlifeSpiritualConflictState.SpiritualArtDefinition art) =>
        NormalizeKey(art.ArtId) switch
        {
            "pressure" => "усиливает прямое духовное давление на ведущего противника",
            "counter" => "усиливает отражение и разворот заявленного действия противника",
            "guard" => "снижает входящее напряжение или последствия для своей стороны",
            "maneuver" => "улучшает позиционный сдвиг без грубого подавления",
            "break_binding" => "помогает сопротивляться оковам, принудительной передаче/выбросу и воплощениям",
            "binding" => "помогает наложить ограничивающие духовные оковы после получения преимущества",
            "incarnation_resistance" => "усиливает сопротивление принудительному воплощению от Хранителя",
            "champion_coordination" => "усиливает поддержку, когда ведущим бойцом выступает союзник/чемпион",
            _ => art.MechanicalUse
        };

    private static string FormatSpiritualArtRule(AfterlifeSpiritualConflictState.SpiritualArtDefinition art) =>
        NormalizeKey(art.ArtId) switch
        {
            "pressure" => "Может ухудшать напряжение противника; не является позиционным манёвром и не должен сам по себе закрывать конфликт без явного завершения.",
            "counter" => "Только реакция на конкретное входящее действие; успех требует подтверждённый выигрыш: лучшую позицию, худшее напряжение противника или разворот/ослабление контроля.",
            "guard" => "Защищает сторону игрока от напряжения или последствия; не наносит прямое напряжение противнику и не заменяет контрприём.",
            "maneuver" => "Меняет позицию конфликта; успешный манёвр должен двигать позицию, не должен напрямую менять напряжение сторон и не проходит бесплатно через активный контроль противника.",
            "break_binding" => "Работает только против оков, принудительной передачи/выброса или контекста принуждения; при успехе должен ослабить, снять или развернуть контроль; не является универсальной атакой.",
            "binding" => "Требует преимущества, доминирования, подготовки или решительного успеха игрока; при успехе создаёт или усиливает контроль, а не наносит обычное напряжение.",
            "incarnation_resistance" => "Только против принудительного воплощения; против обычного давления используй защиту, контрприём или манёвр.",
            "champion_coordination" => "Только в поединке чемпиона, когда союзник или чемпион ведёт сторону; игрок усиливает сторону, а не становится ведущим бойцом.",
            _ => art.MechanicalUse
        };

    private static string FormatSpiritualArtStrongAgainst(AfterlifeSpiritualConflictState.SpiritualArtDefinition art) =>
        NormalizeKey(art.ArtId) switch
        {
            "pressure" => "манёвр, пассивное перестроение, попытку удержать позицию без встречного нажима",
            "counter" => "конкретное входящее давление, оковы, принуждение или другой прямой приём противника",
            "guard" => "прямое давление и опасные последствия, когда важнее не проиграть состояние, чем наказать врага",
            "maneuver" => "пассивную защиту, осторожное ожидание и ситуации, где будущий позиционный бонус важнее немедленного напряжения",
            "break_binding" => "наложенные оковы, принудительную передачу/выброс и подготовку принудительного воплощения",
            "binding" => "противника, уже поставленного в худшую позицию или раскрытого подготовкой",
            "incarnation_resistance" => "только принудительное воплощение и связанные с ним силовые попытки затащить душу в жизнь",
            "champion_coordination" => "поединок чемпиона, где союзник ведёт бой, а игрок усиливает сторону",
            _ => art.MechanicalUse
        };

    private static string FormatSpiritualArtCounteredBy(AfterlifeSpiritualConflictState.SpiritualArtDefinition art) =>
        NormalizeKey(art.ArtId) switch
        {
            "pressure" => "защита снижает ущерб, контрприём может развернуть давление, встречное давление решает спор силой",
            "counter" => "манёвр, переговоры, сдача или отсутствие конкретного входящего действия; контрприём нельзя применять в пустоту",
            "guard" => "манёвр улучшает позицию против защищающейся стороны, оковы могут пройти только при уже полученном рычаге",
            "maneuver" => "давление, встречный манёвр или контроль; манёвр нельзя провести бесплатно сквозь активный нажим",
            "break_binding" => "усиление контроля, решительный успех стороны оков или позиционное доминирование противника",
            "binding" => "разрыв оков, контрприём против контроля или отсутствие нужного преимущества перед попыткой оков",
            "incarnation_resistance" => "успешное принудительное воплощение после проигранного спора; против обычных атак это искусство не подходит",
            "champion_coordination" => "давление по стороне чемпиона, срыв поддержки или перевод сцены из поединка чемпиона в прямой конфликт",
            _ => art.MechanicalUse
        };

    private static string FormatSpiritualArtExample(AfterlifeSpiritualConflictState.SpiritualArtDefinition art) =>
        NormalizeKey(art.ArtId) switch
        {
            "pressure" => "«Я давлю на клятву Хранителя» - при успехе противник теряет устойчивость и начинает трещать под духовным нажимом.",
            "counter" => "«Когда он тянет меня в сон, я разворачиваю поток памяти» - ответ срабатывает против конкретной попытки давления, оков или иного входящего действия.",
            "guard" => "«Я закрываю трещину в душе сияющим щитом» - защита удерживает сторону души от ухудшения или помогает снизить напряжение.",
            "maneuver" => "«Я смещаюсь к спокойному течению Моря» - манёвр переводит спор в более выгодную позицию без прямого удара по напряжению.",
            "break_binding" => "«Я разрываю печать на имени» - приём снимает или ослабляет чужие оковы, принуждение или подготовленную передачу.",
            "binding" => "«Удерживаю противника печатью рассвета» - оковы уместны после преимущества, подготовки или явного рычага в сцене.",
            "incarnation_resistance" => "«Я сопротивляюсь навязанной жизни» - искусство применяется против попытки силой втянуть душу в воплощение.",
            "champion_coordination" => "«Я направляю союзного Хранителя через слабое место врага» - приём помогает, когда бой ведёт чемпион или союзная сторона.",
            _ => art.MechanicalUse
        };

    private static string FormatRankIdLabel(string? rankId) =>
        NormalizeKey(rankId) switch
        {
            "dormant" => "дремлющий",
            "stirring" => "пробуждающийся",
            "focused" => "собранный",
            "tempered" => "закалённый",
            "lucid" => "ясный",
            "illuminated" => "просветлённый",
            "unlit" => "не зажжён",
            "spark" => "искра",
            "gleam" => "проблеск",
            "ray" => "луч",
            "halo" => "ореол",
            "suncrest" => "солнечный гребень",
            "aurora" => "аврора",
            "dawn_throne" => "трон рассвета",
            "stellar_mantle" => "звёздная мантия",
            "radiant_sovereign" => "сияющий владыка",
            "" => "?",
            _ => rankId ?? "?"
        };

    private static string FormatRankMechanicalEffect(string? effect) =>
        effect switch
        {
            "Baseline afterlife conflict participation." => "Базовое участие в духовных конфликтах посмертия.",
            "Unlocks tier-1 spiritual art upgrades." => "Открывает прокачку духовных искусств до уровня 1.",
            "Improves strain recovery after ordinary Chaos Sea conflicts." => "Улучшает восстановление напряжения после обычных конфликтов Моря Хаоса.",
            "Unlocks tier-2 spiritual art upgrades." => "Открывает прокачку духовных искусств до уровня 2.",
            "Improves resistance against ordinary Guardian pressure." => "Улучшает сопротивление обычному давлению Хранителя.",
            "Unlocks tier-3 spiritual art upgrades and ascension-ready conflict scale." => "Открывает прокачку духовных искусств до уровня 3 и масштаб конфликтов перед восхождением.",
            "No persistent Radiant combat advantage." => "Нет постоянного боевого преимущества Сияния.",
            "Radiance begins to count as retained combat authority after Shining return." => "Сияние начинает учитываться как сохранённый боевой авторитет после возвращения из Обители.",
            "Unlocks tier-1 Radiant art upgrades." => "Открывает Сияющие духовные искусства до уровня 1.",
            "Unlocks tier-2 Radiant art upgrades." => "Открывает Сияющие духовные искусства до уровня 2.",
            "Improves side support when a Shining ally is the lead contestant." => "Улучшает поддержку стороны, когда ведущим бойцом является союзник из Сияющей Обители.",
            "Unlocks tier-3 Radiant art upgrades." => "Открывает Сияющие духовные искусства до уровня 3.",
            "Retained Radiance strongly influences Chaos Sea conflicts after return." => "Сохранённое Сияние заметно влияет на конфликты Моря Хаоса после возвращения.",
            "Unlocks tier-4 Radiant art upgrades." => "Открывает Сияющие духовные искусства до уровня 4.",
            "High-rank Abode actors recognize the soul as a major spiritual combatant." => "Высокоранговые акторы Обители распознают душу как значимого духовного бойца.",
            "Unlocks tier-5 Radiant art upgrades and top-end afterlife conflict authority." => "Открывает Сияющие духовные искусства до уровня 5 и верхний предел авторитета в конфликтах посмертия.",
            _ => effect ?? ""
        };

    private static string NormalizeKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();

    private static void AppendConflictSideSummary(List<string> lines, string label, JsonObject? side)
    {
        if (side == null)
        {
            lines.Add($"  • {label}: [red]отсутствует (missing)[/]");
            return;
        }

        var lead = side["leadContestant"] as JsonObject;
        var displayName = AfterlifeSpiritualConflictState.GetNodeString(lead?["displayName"]) ??
                          AfterlifeSpiritualConflictState.GetNodeString(lead?["actorId"]) ??
                          "неизвестно (unknown)";
        var actorType = AfterlifeSpiritualConflictState.GetNodeString(lead?["actorType"]) ?? "?";
        var supporters = (side["supporters"] as JsonArray)?.Count ?? 0;
        lines.Add($"  • {label}: [white]{Markup.Escape(displayName)}[/] [dim]({Markup.Escape(FormatActorTypeLabel(actorType))}; ведущий, поддержка={supporters})[/]");
    }
}
