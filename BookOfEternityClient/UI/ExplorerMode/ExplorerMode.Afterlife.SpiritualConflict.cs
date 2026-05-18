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
        string? SpecialArtEffectSummary = null)
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
            "Это отдельная загробная система конфликтов. Она не использует файлы смертного боя, здоровье, энергию, списки врагов/союзников (enemiesData/alliesData) или смертные боевые навыки.",
            "Конфликт начинает ГМ по роли: по заявке игрока или когда актор посмертия сам инициирует давление.",
            "Победа в проверяемом конфликте может дать награду: в Море Хаоса — Чернильные Перья, в обычной активной Сияющей Обители — Искры Света.",
            "Награда требует аудит награды (rewardAudit) с формулой сложности; ремонтная отмена (repair_cancel), отсутствие эффекта (no_effect), добровольное отступление и переговоры без состязания не дают валюту.",
            ""
        };

        if (active == null)
        {
            lines.Add("[dim]Активного духовного конфликта нет.[/]");
            lines.Add("");
            lines.Add("ГМ может начать конфликт только через ответ принятого хода:");
            lines.Add($"  • `{AfterlifeSpiritualConflictState.ResponseField}` с `mode=start`");
            lines.Add($"  • сохраняемое состояние (persisted state): `{AfterlifeSpiritualConflictState.StatePath}`");
        }
        else
        {
            var conflictId = AfterlifeSpiritualConflictState.GetNodeString(active["conflictId"]) ?? "unknown";
            lines.Add($"[bold]Активный конфликт:[/] [white]{Markup.Escape(conflictId)}[/]");
            lines.Add($"  • Область (realm): [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["realm"]) ?? "?")}[/]");
            lines.Add($"  • Модель сторон (sideModel): [white]{Markup.Escape(FormatSideModelLabel(AfterlifeSpiritualConflictState.GetNodeString(active["sideModel"])))}[/]");
            lines.Add($"  • Позиция конфликта (conflictPosition): [white]{Markup.Escape(FormatConflictPositionLabel(AfterlifeSpiritualConflictState.GetNodeString(active["conflictPosition"])))}[/]");
            lines.Add($"  • Напряжение стороны игрока (playerSideStrain): [white]{Markup.Escape(FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(active["playerSideStrain"])))}[/]");
            lines.Add($"  • Напряжение противостоящей стороны (oppositionSideStrain): [white]{Markup.Escape(FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(active["oppositionSideStrain"])))}[/]");
            lines.Add($"  • Контроль/оковы (controlState): [white]{Markup.Escape(DescribeControlState(active["controlState"] as JsonObject))}[/]");
            lines.Add($"  • ОД (actionEconomy): [white]{Markup.Escape(DescribeActionEconomy(active["actionEconomy"] as JsonObject))}[/]");
            lines.Add($"  • Состояние завершения (resolutionState): [white]{Markup.Escape(FormatResolutionStateLabel(AfterlifeSpiritualConflictState.GetNodeString(active["resolutionState"])))}[/]");
            lines.Add("");
            AppendConflictSideSummary(lines, "Сторона игрока (playerSide)", active["playerSide"] as JsonObject);
            AppendConflictSideSummary(lines, "Противостоящая сторона (oppositionSide)", active["oppositionSide"] as JsonObject);
            lines.Add("");
            lines.Add($"  • Записано обменов действиями (exchangeLog): [white]{(active["exchangeLog"] as JsonArray)?.Count ?? 0}[/]");
        }

        lines.Add("");
        lines.Add("[bold]Команды:[/]");
        lines.Add("  • /spiritual_combat_log — журнал духовного боя: обмены действиями (exchangeLog), недавние конфликты (recentConflicts), кубики, позиция, напряжение и награды.");
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

        if (root != null)
            WriteJsonAuditPanel($"Полный JSON {AfterlifeSpiritualConflictState.StatePath}", root, Color.Cyan1);

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
        var active = root?["activeConflict"] as JsonObject;
        var recentConflicts = root?["recentConflicts"] as JsonArray;
        var lines = new List<string>
        {
            "[bold cyan]Журнал духовного боя[/]",
            "",
            $"Источник: `{AfterlifeSpiritualConflictState.StatePath}`.",
            "Это не журнал смертного боя: загробный бой хранится в журнале обменов действиями (activeConflict.exchangeLog) и недавних завершённых конфликтах (recentConflicts).",
            ""
        };

        var wroteEntry = false;
        if (active != null)
        {
            var conflictId = AfterlifeSpiritualConflictState.GetNodeString(active["conflictId"]) ?? "unknown";
            lines.Add($"[bold]Активный конфликт:[/] [white]{Markup.Escape(conflictId)}[/]");
            lines.Add($"  • Область (realm): [white]{Markup.Escape(AfterlifeSpiritualConflictState.GetNodeString(active["realm"]) ?? "?")}[/]");
            lines.Add($"  • Модель сторон (sideModel): [white]{Markup.Escape(FormatSideModelLabel(AfterlifeSpiritualConflictState.GetNodeString(active["sideModel"])))}[/]");
            lines.Add($"  • Текущая позиция: [white]{Markup.Escape(FormatConflictPositionLabel(AfterlifeSpiritualConflictState.GetNodeString(active["conflictPosition"])))}[/]");
            lines.Add($"  • Текущее напряжение: игрок=[white]{Markup.Escape(FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(active["playerSideStrain"])))}[/], противник=[white]{Markup.Escape(FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(active["oppositionSideStrain"])))}[/]");
            lines.Add($"  • Текущий контроль/оковы: [white]{Markup.Escape(DescribeControlState(active["controlState"] as JsonObject))}[/]");
            lines.Add("");

            if (active["exchangeLog"] is JsonArray activeExchangeLog && activeExchangeLog.Count > 0)
            {
                AppendSpiritualExchangeLog(lines, activeExchangeLog);
                wroteEntry = true;
            }
            else
            {
                lines.Add("  • Обменов в журнале действий (activeConflict.exchangeLog) пока нет.");
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
            lines.Add("[dim]Журнал духовного боя пуст: нет обменов действий (activeConflict.exchangeLog) и недавних конфликтов (recentConflicts).[/]");
            lines.Add("Когда ГМ проведёт спорный обмен или завершение конфликта (resolve), запись появится здесь вместе с аудитом кубиков (diceAudit) и аудитом награды (rewardAudit).");
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

        if (root != null)
            WriteJsonAuditPanel($"Полный JSON {AfterlifeSpiritualConflictState.StatePath}", root, Color.Cyan1);

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
            "  • Это ролевая система конфликтов загробной жизни, а не смертный бой: нет здоровья, энергии, списков врагов/союзников (enemiesData/alliesData) и смертных боевых навыков.",
            "  • Конфликт начинает ГМ по ситуации: по заявке игрока или когда Хранитель, резидент, светозарный актор или другой актор посмертия сам давит на душу.",
            "  • После старта игрок может писать обычную прозу. Команда /spiritual_action только добавляет явный маршрутный тег; она не обязательна, если действие очевидно относится к активному конфликту.",
            "",
            "[bold]Команды игрока[/]",
            "  • /spiritual_conflict / /духовный_конфликт — показать активный конфликт, стороны, позицию, напряжение и полный JSON-аудит.",
            "  • /spiritual_combat_log / /журнал_духовного_боя — показать журнал обменов (exchangeLog) и недавних конфликтов (recentConflicts): действия, кубики, позицию, напряжение и награды.",
            "  • /spiritual_action / /духовное_действие — отправить ГМ явное действие в активном конфликте.",
            "  • /spiritual_arts / /духовные_искусства — посмотреть и локально прокачать духовные искусства.",
            "  • /spiritual_combat_help / /духовный_бой — эта подробная справка.",
            "",
            "[bold]Как выбирается исход[/]",
            "  • В спорных обменах ГМ обязан использовать видимые d20 из заранее сгенерированных кубиков (preGeneratedDices1d20) и записать аудит кубиков (diceAudit).",
            "  • Формула: итог игрока (playerTotal) = d20 игрока + модификаторы; итог противника (oppositionTotal) = d20 противника + модификаторы; разница (margin) = playerTotal - oppositionTotal.",
            "  • Разница (margin) >= 8 — решительный успех игрока (decisive_player_success); 3..7 — успех игрока (player_success); -2..2 — смешанный или нулевой эффект (mixed_or_no_effect); -7..-3 — успех противника (opposition_success); <= -8 — решительный успех противника (decisive_opposition_success).",
            "  • Модификаторы идут от рангов Просветления/Сияния, уровней духовных искусств, силы ведущего бойца, поддержки, Воплощения Света и контекста сцены.",
            "  • Преимущество и Помеха записываются в rollMode: Преимущество выбирает лучший d20, Помеха выбирает худший d20, выбранный куб помечается selection=selected, отброшенные — selection=discarded.",
            "  • Встречные Преимущество и Помеха гасятся: если у стороны есть оба источника, используется обычный один d20; крит считается только по выбранному кубу.",
            "",
            "[bold]Честные криты[/]",
            "  • Благоприятный крит для игрока: натуральная 20 игрока или натуральная 1 противника. Если разница (margin) дала результат хуже обычного успеха, итог поднимается только до успеха игрока (player_success).",
            "  • Неблагоприятный крит для игрока: натуральная 1 игрока или натуральная 20 противника. Если разница (margin) дала результат лучше обычной неудачи, итог опускается только до успеха противника (opposition_success).",
            "  • Это симметрично: крит сам по себе не создаёт решительный успех игрока (decisive_player_success) или решительный успех противника (decisive_opposition_success). Решительный исход появляется только если разница (margin) уже достаточно велика.",
            "  • Если обе стороны получают натуральный крит, они отменяют друг друга, и используется обычная категория разницы.",
            "  • Любой крит, изменивший исход, требует аудит критического исхода (criticalResult) с пределом масштаба (scaleLimit) и нарративным ограничением (narrativeConstraint): результат должен быть правдоподобен для силы сторон. Натуральная 20 комара не превращает его в убийцу дракона.",
            "",
            "[bold]Позиция конфликта[/] [dim](conflictPosition)[/]",
            "  • Позиция — это шкала рычага/инициативы: доминирование противника (opposition_dominant), преимущество противника (opposition_advantaged), спорная позиция (contested), преимущество игрока (player_advantaged), доминирование игрока (player_dominant).",
            "  • Она механически важна: успешный манёвр обязан менять позицию и не должен напрямую менять напряжение.",
            "  • Она влияет на спорный бросок: преимущество/доминирование игрока (player_advantaged/player_dominant) дают игроку +2/+4, преимущество/доминирование противника (opposition_advantaged/opposition_dominant) дают противнику +2/+4 в аудите кубиков (diceAudit).",
            "  • Она открывает контроль: наложение оков (binding/force_binding) требует преимущества игрока (player_advantaged), доминирования игрока (player_dominant), подготовки или решительного успеха игрока (decisive_player_success).",
            "  • Она влияет на награду: победа из плохой стартовой позиции имеет больший множитель риска (riskMultiplier) и уровень вызова (challengeTier); победа из доминирования игрока (player_dominant) платит меньше.",
            "  • Она ограничивает масштаб нарратива: крит или успех в плохой позиции обычно даёт правдоподобный прорыв/срыв угрозы, а не мгновенную абсолютную победу.",
            "",
            "[bold]ОД и стоимость действий[/] [dim](actionEconomy / actionCostAudit)[/]",
            "  • ОД — очки духовного действия. Это ресурс духовного боя посмертия, не здоровье, не энергия и не выносливость смертного мира.",
            "  • Активный конфликт хранит ОД обеих сторон в actionEconomy: текущее значение (current), максимум (max) и источник расчёта (source).",
            "  • Каждый новый обмен, который тратит или восстанавливает ОД игрока, обязан иметь actionCostAudit.player: тип действия, базовую стоимость, уровень искусства, итоговую стоимость, ОД до и после.",
            "  • Если обмен разрешает активное затратное действие противника, нужен actionCostAudit.opposition в той же форме; журнал боя показывает оба расхода ОД.",
            "  • Формула стоимости: итоговая стоимость = max(минимальная стоимость, базовая стоимость - уровень искусства). В JSON-аудите это effectiveCost = max(minCost, baseCost - artTier).",
            "  • Уровни духовных искусств уменьшают стоимость действий; Средоточие Души увеличивает максимум ОД: уровни 0/1/2/3/4/5 дают 6/7/8/10/12/15 ОД. Всё это прокачивается локально через /spiritual_arts.",
            "  • Базовые стоимости: давление 3, защита 2, контрприём 4, манёвр 3, оковы 4, силовые оковы 5, разрыв оков 3, сопротивление воплощению 3, координация чемпиона 2.",
            "  • Собрать Средоточие (recover_spiritual_power) не тратит ОД и восстанавливает ОД: обычно +3 при успехе, +2 при частичном успехе, но не выше максимума.",
            "  • Собрать Средоточие выгодно против защиты, контрприёма, ожидания или пассивности; оно опасно против давления, манёвра, оков, силовых оков и принудительного воплощения — тогда восстановление ограничено 0..1 ОД, а действие противника проходит по своей линии.",
            "  • Отступление, сдача и переговоры остаются допустимыми даже при 0 ОД, если сама сцена позволяет такой выбор.",
            "",
            "[bold]Контроль и оковы[/] [dim](controlState)[/]",
            "  • Контроль — отдельная ось боя, не урон и не позиция. Он ограничивает свободу действий стороны и создаёт рычаг для следующих ходов.",
            "  • Уровни контроля: нет контроля (none), стеснён (hindered), скован (bound), запечатан (locked). Активный контроль всегда указывает сторону-контролёра (controllerSide), идентификатор (controlId), источник (sourceOperation), ограниченные действия (restrictedOperations) и краткое описание (summary).",
            "  • Наложение оков (binding) при успехе должно создать или усилить контроль игрока: none -> hindered, hindered -> bound, bound -> locked. Силовые оковы (force_binding) требуют более сильного рычага и дают более широкий контроль: минимум две ограниченные операции.",
            "  • Разрыв оков (break_binding) при успехе должен ослабить, снять или развернуть контроль против игрока. Если контроль не меняется, это не разрыв оков.",
            "  • Манёвр не проходит бесплатно сквозь активный контроль противника: сначала нужно ослабить контроль через разрыв оков, валидный контрприём, сопротивление воплощению, переговоры или сдачу.",
            "  • Защита может не дать новому входящему контролю усилиться, но не снимает уже наложенные оковы. Для снятия нужны разрыв оков или контрприём против конкретного входящего контроля.",
            "",
            "[bold]Духовные искусства: что выбирать[/]",
            "  • Давление (pressure) — проактивная атака на устойчивость противника. Главный эффект: ухудшить напряжение противостоящей стороны (oppositionSideStrain). Выбирай, когда хочешь продавить волю/клятву/обет противника.",
            "  • Контрприём (counter) — реакция на конкретное входящее действие (incomingAction) противника. Его преимущество над давлением (pressure): можно не просто ударить в ответ, а заблокировать, развернуть или наказать уже заявленное действие врага. Успешный контрприём обязан дать выигрыш (counterPayoff): явно описанный выигрыш (payoff), улучшить позицию, ухудшить напряжение противника (oppositionSideStrain), либо ослабить или развернуть уже существующие вражеские оковы/контроль (controlState).",
            "  • Защита (guard) — снижает или предотвращает напряжение стороны игрока (playerSideStrain) или последствие. Лучше контрприёма (counter), когда нечего разворачивать или нужно пережить удар без риска: даже при провале против прямого давления ухудшение напряжения ограничено одним уровнем.",
            "  • Манёвр (maneuver) — меняет позицию. Выбирай, когда прямое давление опасно, но можно занять лучший духовный угол, разорвать дистанцию, вывести спор из чужой зоны силы. Под активным контролем противника манёвр сначала требует анти-контрольный ответ.",
            "  • Наложение оков (binding/force_binding) — контроль после преимущества. Не стартовая кнопка победы: сначала получи рычаг через позицию, подготовку (setup) или решительный успех.",
            "  • Разрыв оков (break_binding) — ответ на оковы, принудительную передачу/выброс или контекст принуждения. Это не универсальная атака.",
            "  • Сопротивление воплощению (incarnation_resistance) — только против принудительного воплощения Хранителем (guardian_forced / force_incarnation).",
            "  • Координация чемпиона (champion_coordination) — когда ведущий боец не игрок, а союзник/чемпион; игрок усиливает сторону, а не превращает сцену в массовый бой.",
            "  • Собрать Средоточие (recover_spiritual_power) — восстановить ОД в момент, когда противник защищается, ждёт или не давит напрямую. Это не атака и не бесплатный пропуск опасного действия врага.",
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
            "  • Каждый новый спорный обмен с кубиками должен иметь аудит сопоставления действий (matchupAudit): что сделал игрок, что сделал противник, какая линия решала исход, профиль риска и краткое объяснение.",
            "",
            "[bold]Прокачка[/]",
            "  • /spiritual_arts показывает ранги, текущие уровни искусств и стоимость улучшений.",
            "  • Ранги Просветления и Сияния открывают максимальный уровень искусства; сохранённый ранг Сияния продолжает помогать после возвращения в Море Хаоса.",
            "  • Прокачка принадлежит клиенту: клиент локально тратит Чернильные Перья или, в активной Сияющей Обители, Искры Света. ГМ не пишет квитанцию/отчёт прокачки.",
            "  • Прокачка заблокирована во время активного конфликта, активного жизненного цикла хода ГМ и открытых ожидающих контрактов со стоимостью.",
            "",
            "[bold]Награды[/]",
            "  • Победа в проверяемом спорном конфликте (contested conflict) может дать валюту: Чернильные Перья в Море Хаоса или Искры Света в Сияющей Обители.",
            "  • Награда маленькая и формульная: зависит от силы противника, модели сторон, стартовой позиции и категории исхода (outcomeBand).",
            "  • Нет награды за ремонтную отмену (repair_cancel), отсутствие эффекта (no_effect), добровольную сдачу/отступление, переговоры без состязания или повторную награду за тот же конфликт (conflictId)."
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
                ShowEmptyPanel("Духовные искусства", "game_state/meta/soul_state.json недоступен; прокачка духовных искусств заблокирована.");
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
            WriteJsonAuditPanel("Полный JSON afterlifeCombatProfile", profile, Color.Cyan1);

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
        lines.Add("[dim]Правило прокачки: ранги ограничивают максимальный уровень искусства; клиент локально пишет soul_state.afterlifeCombatProfile и тратит выбранную валюту. ГМ не пишет квитанцию/отчёт прокачки.[/]");

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
                   "Локальная прокачка меняет soul_state.afterlifeCombatProfile и валюту, поэтому дождитесь завершения, отмены или ремонта текущего хода. " +
                   $"Найдено: {string.Join(", ", activeTurnArtifacts)}.";
        }

        var conflictRead = await ReadJsonObjectForAfterlifeStatusResultAsync(AfterlifeSpiritualConflictState.StatePath);
        if (conflictRead.Error != null)
        {
            return $"Прокачка духовных искусств заблокирована: {AfterlifeSpiritualConflictState.StatePath} повреждён ({conflictRead.Error}). Сначала выполните ремонт состояния (repair).";
        }

        if (conflictRead.Root?["activeConflict"] is JsonObject)
        {
            return "Прокачка духовных искусств заблокирована: сейчас активен духовный конфликт посмертия. Завершите обмен действиями, завершение конфликта или ремонтную отмену перед изменением боевого профиля.";
        }

        if (conflictRead.Root != null &&
            conflictRead.Root.TryGetPropertyValue("activeConflict", out var activeConflict) &&
            activeConflict != null)
        {
            return $"Прокачка духовных искусств заблокирована: {AfterlifeSpiritualConflictState.StatePath}.activeConflict повреждён. Сначала выполните ремонт состояния (repair).";
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
                MarkupLine("[red]Прокачка духовных искусств не может сохранить операцию: текущий soul_state.json нечитаем. Сначала исправь состояние души.[/]");
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
                AfterlifeEntityProfileState.GetNodeString(specialArt["effectSummary"])));
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
            parts.Add($"Просветление (Enlightenment) {enlightenmentRank.Rank} `{enlightenmentRank.RankId}`");
        if (radianceRank != null)
            parts.Add($"Сияние (Radiance) {radianceRank.Rank} `{radianceRank.RankId}`");

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
            : quote.SpecialArtEffectSummary;
        return $"особое искусство на основе действия «{FormatSpiritualArtLabel(quote.Art)}»; {effect}";
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
        MarkupLine("[dim]Опишите одно намерение: давление (pressure), защита (guard), манёвр (maneuver), контрприём (counter), разрыв/наложение духовных оков (break_binding/binding), сдача, отступление или переговоры. Команда только добавляет явный тег; обычная ролевая заявка во время активного конфликта тоже валидна.[/]");
        MarkupLine("[dim]Выберите действие по механике: давление (pressure) бьёт по напряжению противника (oppositionSideStrain); защита (guard) защищает свою сторону; манёвр (maneuver) двигает позицию конфликта (conflictPosition), но не проходит бесплатно сквозь активный контроль; контрприём (counter) требует входящее действие (incomingAction); оковы (binding) требуют преимущества или подготовки и меняют controlState; разрыв оков (break_binding) и сопротивление воплощению (incarnation_resistance) работают только против контроля/принуждения; координация чемпиона (champion_coordination) — только в поединке чемпиона (champion_duel).[/]");
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
            "direct_duel" => "прямой поединок (direct_duel)",
            "assisted_duel" => "поединок с поддержкой (assisted_duel)",
            "champion_duel" => "поединок чемпиона/союзника (champion_duel)",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatConflictPositionLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "opposition_dominant" => "противник доминирует (opposition_dominant)",
            "opposition_advantaged" => "преимущество противника (opposition_advantaged)",
            "contested" => "спорная позиция (contested)",
            "player_advantaged" => "преимущество игрока (player_advantaged)",
            "player_dominant" => "игрок доминирует (player_dominant)",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatSideStrainLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "clear" => "устойчиво (clear)",
            "strained" => "напряжено (strained)",
            "fractured" => "надломлено (fractured)",
            "overwhelmed" => "подавлено (overwhelmed)",
            "broken" => "сломлено (broken)",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatResolutionStateLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "active" => "активен (active)",
            "concession_pending" => "уступка ожидает закрытия (concession_pending)",
            "surrender_pending" => "сдача ожидает закрытия (surrender_pending)",
            "retreat_pending" => "отступление ожидает закрытия (retreat_pending)",
            "ready_to_resolve" => "готов к завершению (ready_to_resolve)",
            "resolved" => "завершён (resolved)",
            "repair_cancelled" => "отменён ремонтным путём (repair_cancelled)",
            "" => "?",
            _ => value ?? "?"
        };

    private static void AppendSpiritualExchangeLog(List<string> lines, JsonArray exchangeLog)
    {
        lines.Add("[bold]Обмены действиями[/] [dim](exchangeLog)[/]");
        var index = 1;
        foreach (var exchange in exchangeLog.OfType<JsonObject>())
        {
            var exchangeId = AfterlifeSpiritualConflictState.GetNodeString(exchange["exchangeId"]) ?? $"exchange_{index}";
            var operationType = FormatOperationTypeLabel(AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]));
            var outcome = FormatOutcomeLabel(AfterlifeSpiritualConflictState.GetNodeString(exchange["outcome"]));
            lines.Add($"  • #{index} [white]{Markup.Escape(exchangeId)}[/]: {Markup.Escape(operationType)} -> {Markup.Escape(outcome)}");
            lines.Add($"    Состояние: {Markup.Escape(DescribeExchangeStateDelta(exchange))}");

            if (exchange["incomingAction"] is JsonObject incomingAction)
                lines.Add($"    Входящее действие (incomingAction): {Markup.Escape(DescribeIncomingAction(incomingAction))}");

            if (exchange["counterPayoff"] is JsonObject counterPayoff)
                lines.Add($"    Выигрыш контрприёма (counterPayoff): {Markup.Escape(counterPayoff.ToJsonString())}");

            if (exchange["diceAudit"] is JsonObject diceAudit)
                lines.Add($"    Кубики (diceAudit): {Markup.Escape(DescribeDiceAudit(diceAudit))}");

            if (exchange["actionCostAudit"] is JsonObject actionCostAudit)
                lines.Add($"    ОД (actionCostAudit): {Markup.Escape(DescribeActionCostAudit(actionCostAudit))}");

            if (exchange["rewardAudit"] is JsonObject rewardAudit)
                lines.Add($"    Награда (rewardAudit): {Markup.Escape(DescribeRewardAudit(rewardAudit))}");

            var summary = AfterlifeSpiritualConflictState.GetNodeString(exchange["summary"]);
            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add($"    Краткое описание (summary): {Markup.Escape(summary)}");

            index++;
        }
    }

    private static void AppendSpiritualRecentConflictLog(List<string> lines, JsonArray recentConflicts)
    {
        lines.Add("[bold]Недавние завершённые конфликты[/] [dim](recentConflicts)[/]");
        var index = 1;
        foreach (var conflict in recentConflicts.OfType<JsonObject>())
        {
            var conflictId = AfterlifeSpiritualConflictState.GetNodeString(conflict["conflictId"]) ?? $"recent_conflict_{index}";
            var mode = AfterlifeSpiritualConflictState.GetNodeString(conflict["mode"]) ?? "?";
            var resolutionState = FormatResolutionStateLabel(AfterlifeSpiritualConflictState.GetNodeString(conflict["resolutionState"]));
            var operationType = FormatOperationTypeLabel(AfterlifeSpiritualConflictState.GetNodeString(conflict["operationType"]));
            var playerOutcome = AfterlifeSpiritualConflictState.GetNodeString(conflict["playerOutcome"]) ??
                                AfterlifeSpiritualConflictState.GetNodeString(conflict["resolutionKind"]) ??
                                "?";
            lines.Add($"  • #{index} [white]{Markup.Escape(conflictId)}[/]: режим (mode)={Markup.Escape(mode)}, {Markup.Escape(resolutionState)}, операция={Markup.Escape(operationType)}, исход игрока (playerOutcome)={Markup.Escape(playerOutcome)}");
            lines.Add($"    Ход (turn): {Markup.Escape(FormatIntOrUnknown(conflict["resolvedAtTurn"] ?? conflict["turnNumber"]))}");

            if (conflict["diceAudit"] is JsonObject diceAudit)
                lines.Add($"    Кубики (diceAudit): {Markup.Escape(DescribeDiceAudit(diceAudit))}");

            if (conflict["rewardAudit"] is JsonObject rewardAudit)
                lines.Add($"    Награда (rewardAudit): {Markup.Escape(DescribeRewardAudit(rewardAudit))}");

            var summary = AfterlifeSpiritualConflictState.GetNodeString(conflict["summary"]);
            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add($"    Краткое описание (summary): {Markup.Escape(summary)}");

            if (conflict["exchangeLog"] is JsonArray exchangeLog && exchangeLog.Count > 0)
            {
                lines.Add("    История обменов (exchange history):");
                foreach (var exchange in exchangeLog.OfType<JsonObject>())
                {
                    var exchangeId = AfterlifeSpiritualConflictState.GetNodeString(exchange["exchangeId"]) ?? "?";
                    lines.Add($"      - {Markup.Escape(exchangeId)}: {Markup.Escape(DescribeExchangeStateDelta(exchange))}");
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
            $"позиция (conflictPosition) {FormatConflictPositionLabel(AfterlifeSpiritualConflictState.GetNodeString(before?["conflictPosition"]))} -> {FormatConflictPositionLabel(AfterlifeSpiritualConflictState.GetNodeString(after?["conflictPosition"]))}; " +
            $"напряжение игрока (playerSideStrain) {FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(before?["playerSideStrain"]))} -> {FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(after?["playerSideStrain"]))}; " +
            $"напряжение противника (oppositionSideStrain) {FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(before?["oppositionSideStrain"]))} -> {FormatSideStrainLabel(AfterlifeSpiritualConflictState.GetNodeString(after?["oppositionSideStrain"]))}; " +
            $"контроль/оковы (controlState) {DescribeControlState(before?["controlState"] as JsonObject)} -> {DescribeControlState(after?["controlState"] as JsonObject)}; " +
            $"ОД (actionEconomy) {DescribeActionEconomy(before?["actionEconomy"] as JsonObject)} -> {DescribeActionEconomy(after?["actionEconomy"] as JsonObject)}";
    }

    private static string DescribeIncomingAction(JsonObject incomingAction)
    {
        var operationType = FormatOperationTypeLabel(AfterlifeSpiritualConflictState.GetNodeString(incomingAction["operationType"]));
        var actorId = AfterlifeSpiritualConflictState.GetNodeString(incomingAction["actorId"]);
        var summary = AfterlifeSpiritualConflictState.GetNodeString(incomingAction["summary"]);
        var parts = new List<string> { operationType };
        if (!string.IsNullOrWhiteSpace(actorId))
            parts.Add($"актор (actor)={actorId}");
        if (!string.IsNullOrWhiteSpace(summary))
            parts.Add(summary);
        return string.Join("; ", parts);
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
            $"итоги (totals) {playerTotal}/{oppositionTotal}",
            $"разница (margin)={margin}",
            $"категория исхода (outcomeBand)={outcomeBand}"
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
            $"{currency}: итог (finalAmount)={finalAmount}",
            $"база (baseAmount)={baseAmount}",
            $"уровень вызова (challengeTier)={challengeTier}",
            $"риск (riskMultiplierPercent)={riskMultiplier}%",
            $"исход (outcomeMultiplierPercent)={outcomeMultiplier}%"
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
        var source = AfterlifeSpiritualConflictState.GetNodeString(pool["source"]);
        return string.IsNullOrWhiteSpace(source)
            ? $"{current}/{max}"
            : $"{current}/{max} ({source})";
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
            "disadvantage" => "Помеха",
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

        return sources
            .Select(AfterlifeSpiritualConflictState.GetNodeString)
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Select(source => source!)
            .ToList();
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
            "ink_feathers" => "Чернильные Перья (ink_feathers)",
            "light_sparks" => "Искры Света (light_sparks)",
            "" => "?",
            _ => value ?? "?"
        };

    private static string DescribeControlState(JsonObject? controlState)
    {
        if (controlState == null)
            return "нет контроля (none)";

        var level = NormalizeKey(AfterlifeSpiritualConflictState.GetNodeString(controlState["level"]));
        if (string.IsNullOrWhiteSpace(level) || level == "none")
            return "нет контроля (none)";

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
            "hindered" => "стеснён (hindered)",
            "bound" => "скован (bound)",
            "locked" => "запечатан (locked)",
            "none" or "" => "нет контроля (none)",
            _ => value ?? "?"
        };

    private static string FormatControlSideLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "player" => "игрок (player)",
            "opposition" => "противник (opposition)",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatOperationTypeLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "pressure" => "Давление (pressure)",
            "counter" => "Контрприём (counter)",
            "guard" => "Защита (guard)",
            "maneuver" => "Манёвр (maneuver)",
            "binding" => "Наложение оков (binding)",
            "force_binding" => "Силовое наложение оков (force_binding)",
            "break_binding" => "Разрыв оков (break_binding)",
            "force_incarnation" => "Принудительное воплощение (force_incarnation)",
            "incarnation_resistance" => "Сопротивление воплощению (incarnation_resistance)",
            "champion_coordination" => "Координация чемпиона (champion_coordination)",
            "recover_spiritual_power" => "Собрать Средоточие (recover_spiritual_power)",
            "withdraw" => "Отступление (withdraw)",
            "surrender" => "Сдача (surrender)",
            "negotiate" => "Переговоры (negotiate)",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatOutcomeLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "success" => "успех (success)",
            "partial_success" => "частичный успех (partial_success)",
            "blocked" => "заблокировано (blocked)",
            "countered" => "контрировано (countered)",
            "setback" => "неудача/откат (setback)",
            "no_effect" => "без эффекта (no_effect)",
            "" => "?",
            _ => value ?? "?"
        };

    private static string FormatActorTypeLabel(string? value) =>
        NormalizeKey(value) switch
        {
            "player" => "игрок (player)",
            "guardian" => "Хранитель (guardian)",
            "resident" => "резидент Обители (resident)",
            "radiant_actor" => "светозарный актор (radiant_actor)",
            "custom_afterlife_actor" => "особый актор посмертия (custom_afterlife_actor)",
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
            "pressure" => "Может ухудшать напряжение противника (oppositionSideStrain); не является позиционным манёвром и не должен сам по себе закрывать конфликт без завершения (resolve).",
            "counter" => "Только реакция на конкретное входящее действие (incomingAction); успех/контрирование (success/countered) требует выигрыш (payoff): counterPayoff, лучшую позицию (conflictPosition), худшее напряжение противника (oppositionSideStrain) или разворот/ослабление контроля (controlState).",
            "guard" => "Защищает сторону игрока (playerSide) от напряжения/последствия (strain/consequence); не наносит прямое напряжение (strain) противнику и не заменяет контрприём (counter).",
            "maneuver" => "Меняет позицию конфликта (conflictPosition); успешный манёвр (maneuver) должен двигать позицию, не должен напрямую менять напряжение сторон (side strain) и не проходит бесплатно через активный контроль противника (controlState).",
            "break_binding" => "Работает только против оков, принудительной передачи/выброса или контекста принуждения; при успехе должен ослабить, снять или развернуть controlState; не является универсальной атакой.",
            "binding" => "Требует преимущество/доминирование игрока (player_advantaged/player_dominant), подготовку (setup=true) или решительный успех игрока (decisive_player_success); при успехе создаёт или усиливает controlState, а не наносит обычное напряжение (strain).",
            "incarnation_resistance" => "Только против принудительного воплощения (force_incarnation/guardian_forced); против обычного давления (pressure) используй защиту, контрприём или манёвр (guard/counter/maneuver).",
            "champion_coordination" => "Только в поединке чемпиона (champion_duel), когда союзник/чемпион ведёт сторону; игрок усиливает сторону, а не становится ведущим бойцом.",
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
            "pressure" => "«Я давлю на клятву Хранителя» -> при успехе напряжение противника (oppositionSideStrain) меняется с устойчиво (clear) на напряжено (strained).",
            "counter" => "«Когда он тянет меня в сон, я разворачиваю поток памяти» -> входящее действие (incomingAction) описывает силовые оковы или давление (force_binding/pressure).",
            "guard" => "«Я закрываю трещину в душе сияющим щитом» -> напряжение стороны игрока (playerSideStrain) не ухудшается или снижается.",
            "maneuver" => "«Я смещаюсь к спокойному течению Моря» -> позиция конфликта (conflictPosition) меняется со спорной (contested) на преимущество игрока (player_advantaged) без урона напряжением (strain).",
            "break_binding" => "«Я разрываю печать на имени» -> снимает/ослабляет состояние оков (bindingState) или принудительную передачу/выброс (forcedHandoff).",
            "binding" => "«Удерживаю противника печатью рассвета» -> возможно только после преимущества или подготовки (setup).",
            "incarnation_resistance" => "«Я сопротивляюсь навязанной жизни» -> применяется против принудительного воплощения (force_incarnation).",
            "champion_coordination" => "«Я направляю союзного Хранителя через слабое место врага» -> работает в поединке чемпиона (champion_duel).",
            _ => art.MechanicalUse
        };

    private static string FormatRankIdLabel(string? rankId) =>
        NormalizeKey(rankId) switch
        {
            "dormant" => "дремлющий (dormant)",
            "stirring" => "пробуждающийся (stirring)",
            "focused" => "собранный (focused)",
            "tempered" => "закалённый (tempered)",
            "lucid" => "ясный (lucid)",
            "illuminated" => "просветлённый (illuminated)",
            "unlit" => "не зажжён (unlit)",
            "spark" => "искра (spark)",
            "gleam" => "проблеск (gleam)",
            "ray" => "луч (ray)",
            "halo" => "ореол (halo)",
            "suncrest" => "солнечный гребень (suncrest)",
            "aurora" => "аврора (aurora)",
            "dawn_throne" => "трон рассвета (dawn_throne)",
            "stellar_mantle" => "звёздная мантия (stellar_mantle)",
            "radiant_sovereign" => "сияющий владыка (radiant_sovereign)",
            "" => "?",
            _ => rankId ?? "?"
        };

    private static string FormatRankMechanicalEffect(string? effect) =>
        effect switch
        {
            "Baseline afterlife conflict participation." => "Базовое участие в духовных конфликтах посмертия.",
            "Unlocks tier-1 spiritual art upgrades." => "Открывает прокачку духовных искусств до уровня 1.",
            "Improves strain recovery after ordinary Chaos Sea conflicts." => "Улучшает восстановление напряжения (strain) после обычных конфликтов Моря Хаоса.",
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
