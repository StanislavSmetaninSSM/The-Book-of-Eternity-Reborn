using System.Text.Json;
using System.Text.Json.Nodes;
using Spectre.Console;
using Spectre.Console.Rendering;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{private async Task ShowSystemMods()
    {
        if (_systemModService == null)
        {
            ShowEmptyPanel("Системные моды", "Сервис системных модов недоступен");
            return;
        }

        DesktopOpenResult? folderOpenResult = null;
        while (true)
        {
            var mods = await _systemModService.GetAvailableModsAsync(includeContent: false);
            var lines = new List<string>
            {
                "[bold yellow]🧩 Глобальные системные моды[/]",
                "",
                "[white]Это глобальные надстройки над всей игрой.[/]",
                "[white]Активные моды учитываются при подготовке ходов и правил мира.[/]",
                "[white]Включение и выключение выполняется через /опции.[/]",
                "[yellow]Перед включением проверьте описание: мод может менять баланс, тон и доступные правила.[/]"
            };

            if (folderOpenResult is not null) lines.Add(folderOpenResult.ToMarkup());
            else lines.Add($"[dim]Путь для ручного открытия: {Markup.Escape(_systemModService.GetModsDirectoryPath())}[/]");

            if (mods.Count == 0)
            {
                lines.Add("");
                lines.Add("[dim]Пока нет установленных модов.[/]");
            }
            else
            {
                lines.Add("");
                lines.Add($"[bold]Найдено модов:[/] {mods.Count}  [bold]Активно:[/] {mods.Count(m => m.Enabled)}");
                lines.Add("");

                foreach (var mod in mods)
                {
                    var state = mod.Enabled ? "[green]● Активен[/]" : "[dim]○ Выключен[/]";
                    lines.Add($"{state} [white]{Markup.Escape(mod.Name)}[/]");
                    var description = ExplorerPlayerFacingLabels.SystemModDescription(mod.Description);
                    if (!string.IsNullOrWhiteSpace(description))
                        lines.Add($"  [dim]{Markup.Escape(description)}[/]");
                }
            }

            Clear();
            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 🧩 Системные моды ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Gold1),
                Padding = new Padding(2, 1),
                Expand = true
            });
            WriteLine();

            var actions = new List<string>();
            var modChoices = ExplorerPlayerFacingLabels.SystemModChoiceLabels(mods);
            if (mods.Count > 0)
                actions.AddRange(modChoices.Select(static choice => choice.Label));
            actions.Add("📂 Открыть папку модов");
            actions.Add("← Назад");

            var choice = Prompt(
                new SelectionPrompt<string>()
                    .Title("[gold1]Действие:[/]")
                    .HighlightStyle(new Style(Color.Gold1))
                    .PageSize(18)
                    .AddChoices(actions));

            if (choice == "← Назад")
                return;

            if (choice == "📂 Открыть папку модов")
            {
                folderOpenResult = _desktopPathOpener.OpenFolder(_systemModService.GetModsDirectoryPath(), createIfMissing: false);
                if (folderOpenResult.Status != DesktopOpenStatus.Requested)
                {
                    MarkupLine(folderOpenResult.ToMarkup());
                    WaitForKey();
                }
                continue;
            }

            var selected = modChoices.FirstOrDefault(candidate => choice == candidate.Label).Mod;
            if (selected != null)
                await ShowSystemModDetailAsync(selected.FileName);
        }
    }

    private async Task ShowSystemModDetailAsync(string fileName)
    {
        if (_systemModService == null)
            return;

        var mods = await _systemModService.GetAvailableModsAsync(includeContent: true);
        var mod = mods.FirstOrDefault(m => string.Equals(m.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        if (mod == null)
        {
            ShowEmptyPanel("Системный мод", "Файл мода больше не найден.");
            return;
        }

        var lines = new List<string>
        {
            $"[bold yellow]{Markup.Escape(mod.Name)}[/]",
            mod.Enabled ? "[green]● Активен[/]" : "[dim]○ Выключен[/]"
        };

        var description = ExplorerPlayerFacingLabels.SystemModDescription(mod.Description);
        if (!string.IsNullOrWhiteSpace(description))
        {
            lines.Add("");
            lines.Add(Markup.Escape(description));
        }

        if (!string.IsNullOrWhiteSpace(mod.Content))
        {
            lines.Add("");
            lines.Add("[bold]Содержимое мода:[/]");
            lines.Add(Markup.Escape(mod.Content));
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 📄 Системный мод ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
    }

    // ═════════════════════════════════════════════════════════
    // LEGACY CUSTOM RULES — deprecated compatibility layer (no longer canonical)
    // ═════════════════════════════════════════════════════════
    private Task ShowHelp()
    {
        var isChaosSea = _stateManager.CurrentState.IsInChaosSea;
        var isShiningAbode = _stateManager.CurrentState.IsInShiningAbode;
        var isPendingShiningAbodeBootstrap = _stateManager.CurrentState.IsInShiningAbodePendingBootstrap;
        var isAfterlife = _stateManager.CurrentState.IsInAfterlifeRealm;

        var table = new Table()
            .Border(TableBorder.HeavyEdge)
            .BorderColor(isPendingShiningAbodeBootstrap ? Color.Khaki1 : (isShiningAbode ? Color.Gold1 : (isAfterlife ? Color.Blue : Color.Green3)))
            .AddColumn(new TableColumn("[bold]EN[/]").Width(18))
            .AddColumn(new TableColumn("[bold]RU[/]").Width(20))
            .AddColumn("[bold]Описание[/]");

        if (isPendingShiningAbodeBootstrap)
        {
            table.AddRow("[bold khaki1]── ПЕРЕДАЧА ИЗ СИЯЮЩЕЙ ОБИТЕЛИ ──[/]", "", "");
            table.AddRow("[khaki1]/help[/]", "[khaki1]/помощь[/]", "[khaki1]Показать этот экран; обычные команды Обители и Моря Хаоса пока недоступны[/]");
            table.AddRow("[khaki1]/status[/]", "[khaki1]/статус[/]", "[khaki1]Аудит только для чтения: блокеры, замороженный пакет воплощения (preparedIncarnationPackage), ожидающая настройка и данные следующей жизни[/]");
            table.AddRow("[khaki1]/shining_abode[/]", "[khaki1]/сияющая_обитель[/]", "[khaki1]Аудит только для чтения: Врата, замороженный пакет, выбранные карты и квитанции ядра; обычные действия Обители недоступны[/]");
            table.AddRow("[khaki1]/shining_politics[/]", "[khaki1]/сияющая_политика[/]", "[khaki1]Аудит только для чтения: основание фракций, переходы и лидерство, их квитанции и ожидающие контракты[/]");
        }
        else if (isShiningAbode)
        {
            table.AddRow("[bold yellow]── СИЯЮЩАЯ ОБИТЕЛЬ ──[/]", "", "");
            table.AddRow("[yellow]/guardians[/]", "[yellow]/хранители[/]", "[yellow]Информация о хранителях[/]");
            table.AddRow("[yellow]/status[/]", "[yellow]/статус[/]", "[yellow]Полный статус посмертия: ресурсы души, блокеры, ожидающие контракты, Сияние, Искры Света, Врата, торговля/кузня и политические сигналы[/]");
            table.AddRow("[yellow]/abode_power[/]", "[yellow]/сила_обители[/]", "[yellow]Журнал силы Обителей и её причин[/]");
            table.AddRow("[yellow]/abode_offering[/]", "[yellow]/подношение_обители[/]", "[yellow]Поднести Перья, Реликвию Души или запись Архива выбранной Обители[/]");
            table.AddRow("[yellow]/guardian_projects[/]", "[yellow]/проекты_хранителей[/]", "[yellow]Подробный журнал проектов хранителей[/]");
            table.AddRow("[yellow]/guardian_politics[/]", "[yellow]/политика_хранителей[/]", "[yellow]Политика, союзы, долги и влияние Хранителей Моря Хаоса[/]");
            table.AddRow("[yellow]/soul[/]", "[yellow]/душа[/]", "[yellow]Состояние души и мета-прогрессия[/]");
            table.AddRow("[yellow]/soul_relics[/]", "[yellow]/реликвии[/]", "[yellow]Реликвии души[/]");
            table.AddRow("[yellow]/afterlife_archive[/]", "[yellow]/архив_души[/]", "[yellow]Архив знаний и тайн, переживших смерть[/]");
            table.AddRow("[yellow]/archive_candidates[/]", "[yellow]/архив_кандидаты[/]", "[yellow]Выбрать, что сохранить в Архив по итогам жизни[/]");
            table.AddRow("[yellow]/afterlife_inbox[/]", "[yellow]/уведомления_загробья[/]", "[yellow]Ответы ГМ по торговле, Архиву и резидентам Обители[/]");
            table.AddRow("[yellow]/afterlife_chronicles[/]", "[yellow]/хроники_посмертия[/]", "[yellow]Хроники ключевых событий Моря Хаоса и Сияющей Обители без скрытых полей ГМ[/]");
            table.AddRow("[yellow]/afterlife_threats[/]", "[yellow]/угрозы_загробья[/]", "[yellow]Видимые persistent угрозы посмертия; скрытые угрозы не раскрываются до игровых свидетельств[/]");
            table.AddRow("[yellow]/spiritual_conflict[/]", "[yellow]/духовный_конфликт[/]", "[yellow]Активный духовный конфликт посмертия: стороны, позиция, напряжение и журнал обменов[/]");
            table.AddRow("[yellow]/spiritual_combat_log[/]", "[yellow]/журнал_духовного_боя[/]", "[yellow]Журнал духовного боя: обмены действиями (exchangeLog), недавние конфликты (recentConflicts), кубики, позиции, напряжение и награды[/]");
            table.AddRow("[yellow]/spiritual_combat_help[/]", "[yellow]/духовный_бой[/]", "[yellow]Подробная справка по духовному бою: команды, духовные искусства, позиция, кубики, криты и награды[/]");
            table.AddRow("[yellow]/spiritual_arts[/]", "[yellow]/духовные_искусства[/]", "[yellow]Ранги Просветления/Сияния и уровни духовных искусств[/]");
            table.AddRow("[yellow]/training[/]", "[yellow]/обучение[/]", "[yellow]Витрина обучения: наставники, скидки на духовные искусства и дорогая самостоятельная прокачка[/]");
            table.AddRow("[yellow]/spiritual_action[/]", "[yellow]/духовное_действие[/]", "[yellow]Отправить действие ГМ в активном духовном конфликте с явным тегом; обычная проза тоже валидна[/]");
            table.AddRow("[yellow]/shining_abode[/]", "[yellow]/сияющая_обитель[/]", "[yellow]Обзор сияния, залов, фракций, Врат и текущего состояния Сияющей Обители[/]");
            table.AddRow("[yellow]/shining_politics[/]", "[yellow]/сияющая_политика[/]", "[yellow]Фракционная политика Сияющей Обители: власть, основание и переходы между фракциями[/]");
            table.AddRow("[yellow]/shining_treasury[/]", "[yellow]/казначейство[/]", "[yellow]Локальное казначейство: вклад Чернильных Перьев, проценты и дорогой обмен Перьев на Искры Света[/]");
            table.AddRow("[gold1]/source_of_light[/]", "[gold1]/источник_света[/]", "[gold1]Вершина полного Сияния: Источник Света, Воплощение Света и уникальная реликвия Воплощенный Свет[/]");
            table.AddRow("[gold1]/saref[/]", "[gold1]/сареф[/]", "[gold1]Скрытая главная линия: до раскрытия показывает только «ты пока не знаешь, что искать»; после раскрытия показывает фрагменты, преимущества и журнал[/]");
            table.AddRow("[gold1]/воспоминание[/]", "[gold1]/воспоминание_начать[/]", "[gold1]Воспоминание: игровой слой 4-го квеста Хранителя в линии Сарефа, роль, способности, границы сцены и прогресс; это не Врата Памяти и не Наследие Памяти[/]");
            table.AddRow("[yellow]/soul_quests[/]", "[yellow]/квесты_души[/]", "[yellow]Квесты хранителей[/]");
            table.AddRow("[gold1]/feathers[/]", "[gold1]/перья[/]", "[gold1]🪶 Чернильные перья[/]");
            table.AddRow("[cyan]/world_setup[/]", "[cyan]/настройка_мира[/]", "[cyan]Подготовить следующий смертный мир[/]");
            table.AddRow("", "", "");
            table.AddRow("[blue]/return_to_chaos_sea[/]", "[blue]/вернуться_в_море_хаоса[/]", "[blue]Новый цикл: вернуться в Море Хаоса, сбросить Просветление, сохранить Перья и прогресс Обители; без запуска Нового Цикла как полного сброса[/]");
            table.AddRow("[bold gold1]/new_game_plus[/]", "[bold gold1]/новая_игра+[/]", "[bold gold1]Старое имя той же безопасной команды Нового цикла Сияющей Обители[/]");
            table.AddRow("", "", "");
            table.AddRow("[dim]💡 Это финальная зона свободного ролеплея над Морем Хаоса[/]", "", "");
            table.AddRow("[dim]💡 В /shining_abode локальные Врата/выбор карт отделены от действий ядра в ходе ГМ; предпросмотры показывают шаблон квитанции и ожидаемую дельту состояния.[/]", "", "");
            table.AddRow("[dim]💡 Карта Обители: /status показывает компактное состояние, блокеры, выбранные карты и данные следующей жизни; /shining_abode → исходы/Врата показывает Врата, пакет и итоги действий ядра; /shining_politics показывает контракты основания/переходов/лидерства; предпросмотры торговли/кузни находятся внутри /shining_abode.[/]", "", "");
            table.AddRow("[dim]💡 Для обычной игры ориентируйтесь на сводки, таблицы и пункты «Подробно». Технический аудит контрактов нужен только при ремонте данных или проверке авторинга ГМ.[/]", "", "");
        }
        else if (isChaosSea)
        {
            // Chaos Sea commands
            table.AddRow("[bold blue]── МОРЕ ХАОСА (загробная жизнь) ──[/]", "", "");
            table.AddRow("[blue]/chaos_sea[/]", "[blue]/море_хаоса[/]", "[blue]Обзор Моря Хаоса: активный Хранитель, навигация, ожидающие контракты и доступные действия[/]");
            table.AddRow("[blue]/status[/]", "[blue]/статус[/]", "[blue]Полный статус посмертия: ресурсы души, блокеры, ожидающие контракты, подсказки наград/дельт и сохранённая Сияющая Обитель[/]");
            table.AddRow("[blue]/guardians[/]", "[blue]/хранители[/]", "[blue]Информация о хранителях[/]");
            table.AddRow("[blue]/abodes[/]", "[blue]/обители[/]", "[blue]Навигация по Обителям Хранителей; переходы доступны только в Море Хаоса и не являются путешествием смертного мира[/]");
            table.AddRow("[blue]/abode_power[/]", "[blue]/сила_обители[/]", "[blue]Журнал силы Обителей и её причин[/]");
            table.AddRow("[blue]/abode_offering[/]", "[blue]/подношение_обители[/]", "[blue]Поднести Перья, Реликвию Души или запись Архива выбранной Обители[/]");
            table.AddRow("[blue]/guardian_projects[/]", "[blue]/проекты_хранителей[/]", "[blue]Подробный журнал проектов хранителей[/]");
            table.AddRow("[blue]/guardian_politics[/]", "[blue]/политика_хранителей[/]", "[blue]Политика, союзы, долги и влияние Хранителей Моря Хаоса[/]");
            table.AddRow("[blue]/soul[/]", "[blue]/душа[/]", "[blue]Состояние души (перья, просветление, история жизней)[/]");
            table.AddRow("[blue]/soul_relics[/]", "[blue]/реликвии[/]", "[blue]Реликвии души (экипировка, хранилище)[/]");
            table.AddRow("[blue]/soul_relic_equip[/]", "[blue]/экипировать_реликвию[/]", "[blue]Экипировать реликвию души из хранилища в выбранный слот[/]");
            table.AddRow("[blue]/soul_relic_unequip[/]", "[blue]/снять_реликвию[/]", "[blue]Снять экипированную реликвию обратно в хранилище[/]");
            table.AddRow("[blue]/afterlife_archive[/]", "[blue]/архив_души[/]", "[blue]Архив знаний и тайн, переживших смерть[/]");
            table.AddRow("[blue]/archive_candidates[/]", "[blue]/архив_кандидаты[/]", "[blue]Выбрать записи Кодекса, которые переживут смерть[/]");
            table.AddRow("[blue]/afterlife_inbox[/]", "[blue]/уведомления_загробья[/]", "[blue]Ответы ГМ по торговле, Архиву и резидентам Обители[/]");
            table.AddRow("[blue]/afterlife_chronicles[/]", "[blue]/хроники_посмертия[/]", "[blue]Хроники ключевых событий Моря Хаоса и Сияющей Обители без скрытых полей ГМ[/]");
            table.AddRow("[blue]/afterlife_threats[/]", "[blue]/угрозы_загробья[/]", "[blue]Видимые persistent угрозы посмертия; скрытые угрозы не раскрываются до игровых свидетельств[/]");
            table.AddRow("[blue]/spiritual_conflict[/]", "[blue]/духовный_конфликт[/]", "[blue]Активный духовный конфликт посмертия: стороны, позиция, напряжение и журнал обменов[/]");
            table.AddRow("[blue]/spiritual_combat_log[/]", "[blue]/журнал_духовного_боя[/]", "[blue]Журнал духовного боя: обмены действиями (exchangeLog), недавние конфликты (recentConflicts), кубики, позиции, напряжение и награды[/]");
            table.AddRow("[blue]/spiritual_combat_help[/]", "[blue]/духовный_бой[/]", "[blue]Подробная справка по духовному бою: команды, духовные искусства, позиция, кубики, криты и награды[/]");
            table.AddRow("[blue]/spiritual_arts[/]", "[blue]/духовные_искусства[/]", "[blue]Ранги Просветления/Сияния и уровни духовных искусств[/]");
            table.AddRow("[blue]/training[/]", "[blue]/обучение[/]", "[blue]Витрина обучения: наставники, скидки на духовные искусства и дорогая самостоятельная прокачка[/]");
            table.AddRow("[blue]/spiritual_action[/]", "[blue]/духовное_действие[/]", "[blue]Отправить действие ГМ в активном духовном конфликте с явным тегом; обычная проза тоже валидна[/]");
            table.AddRow("[blue]/shining_abode[/]", "[blue]/сияющая_обитель[/]", "[blue]Обзор сохранённого состояния Сияющей Обители: сияние, фракции и Врата[/]");
            table.AddRow("[blue]/shining_politics[/]", "[blue]/сияющая_политика[/]", "[blue]Сохранённые решения Сияющей Обители по власти, основанию и переходам между фракциями[/]");
            table.AddRow("[blue]/shining_treasury[/]", "[blue]/казначейство[/]", "[blue]Казначейство доступно только в активной Сияющей Обители; в Chaos Sea показывает сохранённое состояние через /shining_abode[/]");
            table.AddRow("[blue]/source_of_light[/]", "[blue]/источник_света[/]", "[blue]Источник Света доступен только в обычной активной Сияющей Обители после полного Сияния[/]");
            table.AddRow("[blue]/saref[/]", "[blue]/сареф[/]", "[blue]Скрытая главная линия: если ты пока не знаешь, что искать, команда не раскрывает спойлеры[/]");
            table.AddRow("[blue]/воспоминание[/]", "[blue]/воспоминание_начать[/]", "[blue]Воспоминание: игровой слой 4-го квеста Хранителя в линии Сарефа, роль, способности, границы сцены и прогресс; это не Врата Памяти и не Наследие Памяти[/]");
            table.AddRow("[blue]/soul_quests[/]", "[blue]/квесты_души[/]", "[blue]Квесты от хранителей[/]");
            table.AddRow("[blue]/found_guardian_mantle[/]", "[blue]/учредить_хранителя[/]", "[blue]Поздний ритуал основания собственного Хранителя после возвращения из Сияющей Обители[/]");
            table.AddRow("[gold1]/gacha[/]", "[gold1]/гача[/]", "[gold1]Прямое вытягивание реликвии из Моря Хаоса (без модификаторов Хранителя)[/]");
            table.AddRow("[gold1]/feathers[/]", "[gold1]/перья[/]", "[gold1]🪶 Чернильные перья (способности души)[/]");
            table.AddRow("[cyan]/world_setup[/]", "[cyan]/настройка_мира[/]", "[cyan]Подготовить следующий смертный мир[/]");
            table.AddRow("", "", "");
            table.AddRow("[yellow]/incarnate[/]", "[yellow]/воплотиться[/]", "[yellow]⚔️ Войти в смертную жизнь через Врата Души[/]");
            if (_stateManager.CurrentState.CanReenterShiningAbode)
                table.AddRow("[yellow]/reenter_shining_abode[/]", "[yellow]/вернуться_в_обитель[/]", "[yellow]✨ Вернуться в уже активную Сияющую Обитель[/]");
            table.AddRow("", "", "");
            table.AddRow("[dim]💡 Говорите с Хранителем свободным текстом:[/]", "", "");
            table.AddRow("[dim]   торговать, брать квесты, менять реликвии, сменить хранителя[/]", "", "");
            table.AddRow("[dim]💡 Для аудита перед ходом: /status, /afterlife_inbox, /feathers, /afterlife_archive, /guardian_projects, /guardian_politics, /abode_offering.[/]", "", "");
            table.AddRow("[dim]💡 Если сохранённая Сияющая Обитель важна: /shining_abode показывает Врата, пакет, квитанции ядра и предпросмотры торговли/кузни; /shining_politics показывает политические квитанции и ожидающие контракты; /status показывает компактные блокеры Обители.[/]", "", "");
        }
        else
        {
            // Mortal Life commands
            table.AddRow("[bold green]── СМЕРТНАЯ ЖИЗНЬ ──[/]", "", "");
            table.AddRow("/inv", "/инв", "Показать инвентарь");
            table.AddRow("/npc /npcs", "/нпс", "Показать персонажей");
            table.AddRow("/quests", "/квесты", "Показать квесты (смертные)");
            table.AddRow("/map", "/карта", "Показать карту");
            table.AddRow("/status", "/статус", "Детальный статус персонажа");
            table.AddRow("/skills", "/навыки", "Показать навыки");
            table.AddRow("/training", "/обучение", "Учителя, доступные навыки, цена обучения в деньгах и опыте текущего уровня");
            table.AddRow("/stats", "/статы", "Показать характеристики");
            table.AddRow("/distribute", "/распределить", "Распределить очки характеристик");
            table.AddRow("/companion_directive", "/директива_компаньону", "Задать указание компаньону");
            table.AddRow("/faction_directive", "/директива_фракции", "Задать стратегию фракции");
            table.AddRow("/effects", "/эффекты", "Эффекты, раны, состояния, опыт");
            table.AddRow("/combat", "/бой", "⚔️ Боевая обстановка (враги, союзники)");
            table.AddRow("/factions", "/фракции", "Показать фракции");
            table.AddRow("/world_news", "/новости_мира", "Мировые события");
            table.AddRow("/rival_threads", "/чужие_нити", "🧵 Проявления чужих нитей судьбы");
            table.AddRow("/guardian_corrections", "/коррективы_хранителя", "Коррективы Хранителя в старт текущей жизни");
            table.AddRow("/abode_power", "/сила_обители", "🏛 Журнал силы Обителей и её изменений");
            table.AddRow("/afterlife_archive", "/архив_души", "📚 Архив души (только просмотр)");
            table.AddRow("/archive_candidates", "/архив_кандидаты", "🗂 Кандидаты в Архив по итогам жизни");
            table.AddRow("/craft", "/ремесло", "Рецепты крафта");
            table.AddRow("/locations", "/локации", "Известные локации");
            table.AddRow("/where_am_i", "/где_я", "Текущая локация");
            table.AddRow("/weather", "/погода", "Время и погода");
            table.AddRow("/transport", "/транспорт", "Транспорт");
            table.AddRow("/books", "/книги", "Книги, письма, свитки");
            table.AddRow("/world_rules", "/правила_мира", "📜 Досье и директивы текущего мира");
            table.AddRow("/storage_access", "/доступ_к_хранилищам", "Доступ к хранилищам");
            table.AddRow("/interactions", "/взаимодействия", "Взаимодействия других игроков");
            table.AddRow("", "", "");
            table.AddRow("[blue]/soul_relics[/]", "[blue]/реликвии[/]", "[blue]Реликвии души (только просмотр!)[/]");
            table.AddRow("[blue]/soul_quests[/]", "[blue]/квесты_души[/]", "[blue]Квесты хранителей (только просмотр)[/]");
            table.AddRow("[blue]/soul[/]", "[blue]/душа[/]", "[blue]Состояние души[/]");
            table.AddRow("[blue]/saref[/]", "[blue]/сареф[/]", "[blue]Скрытая главная линия: если ты пока не знаешь, что искать, команда не раскрывает спойлеры[/]");
            table.AddRow("[gold1]/feathers[/]", "[gold1]/перья[/]", "[gold1]🪶 Чернильные перья (способности судьбы)[/]");
            table.AddRow("", "", "");
            table.AddRow("[yellow]/end_of_life[/]", "[yellow]/конец_жизни[/]", "[yellow]💀 Завершить жизнь → вернуться в Море Хаоса[/]");
            table.AddRow("", "", "");
            table.AddRow("[dim]⚠️ В смертной жизни нельзя: менять реликвии, общаться с хранителями[/]", "", "");
        }

        // Common
        table.AddRow("", "", "");
        table.AddRow("[bold grey]── Общие команды ──[/]", "", "");
        table.AddRow("[grey]/codex[/]", "[grey]/кодекс[/]", "Лор и знания");
        table.AddRow("[grey]/chronicle[/]", "[grey]/хроника[/]", "📖 Хроника и сюжет");
        table.AddRow("[grey]/story[/]", "[grey]/рассказ[/]", "📜 Полная история (все ходы по главам)");
        table.AddRow("[grey]/achievements[/]", "[grey]/достижения[/]", "🏆 Достижения");
        table.AddRow("[grey]/behavior[/]", "[grey]/поведение[/]", "🧠 Оценка поведения и манипуляция историей");
        table.AddRow("[grey]/lives[/]", "[grey]/жизни[/]", "📜 История прошлых жизней");
        table.AddRow("[grey]/validate[/]", "[grey]/валидация[/]", "🔍 Проверка файлов");
        table.AddRow("[grey]/mods[/]", "[grey]/моды[/]", "🧩 Глобальные системные моды");
        table.AddRow("[grey]/system_guardians[/]", "[grey]/извечные_хранители[/]", "🛡 Библиотека извечных хранителей");
        table.AddRow("[grey]/gallery[/]", "[grey]/галерея[/]", "🖼 Галерея изображений");
        table.AddRow("[grey]/math[/]", "[grey]/математик[/]", "🧮 Локально вычислить формулу без изменения состояния");
        table.AddRow("[grey]/options[/]", "[grey]/опции[/]", "⚙ Игровое меню");
        table.AddRow("[grey]/gm[/]", "[grey]/гм[/]", "🧠 Мысли Мастера Игры");
        table.AddRow("[grey]/debug[/]", "[grey]/отладка[/]", "🔧 Отладка");
        table.AddRow("[grey]/help[/]", "[grey]/помощь[/]", "❓ Эта справка");
        table.AddRow("[grey]/refresh[/]", "[grey]/обновить[/]", "🔄 Перечитать все данные и перерисовать экран");

        var helpColor = isChaosSea ? Color.Blue : Color.Green3;
        WrapInPanel(table, $"❓ {_loc.T("help")}", helpColor);
        WaitForKey();
        return Task.CompletedTask;
    }

    // ═══ Soul / Meta-game commands ═══

    private async Task ShowEffects()
    {
        var result = await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(
            _currentCommandInput,
            _stateManager,
            _fs);
        if (result != null)
        {
            ExplorerCommandResultConsoleRenderer.Render(_console, result);
        }
        else
        {
            Write(new Panel("Сейчас невозможно открыть принятую проекцию эффектов.")
            {
                Header = new PanelHeader(" Эффекты ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow)
            });
        }

        WaitForKey();
    }

    private async Task ShowCombat()
    {
        var commandLine = string.IsNullOrWhiteSpace(_currentCommandRemainder)
            ? "/бой"
            : _currentCommandInput;
        var result = await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(
            commandLine,
            _stateManager,
            _fs);
        if (result != null)
        {
            ExplorerCommandResultConsoleRenderer.Render(_console, result);
        }
        else
        {
            Write(new Panel("Сейчас невозможно открыть принятую проекцию боя.")
            {
                Header = new PanelHeader(" Бой ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Red)
            });
        }

        WaitForKey();
    }
    private async Task ShowWeatherTime()
    {
        var text = new List<string>();

        var timeDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/world_time.json");
        if (timeDoc != null)
        {
            AppendWorldTimeLines(text, timeDoc.RootElement, "  ");
        }
        else
        {
            text.Add("[dim]Время неизвестно[/]");
        }

        text.Add("");
        var wDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/weather.json");
        if (wDoc != null)
        {
            var wr = GetWeatherRoot(wDoc.RootElement);
            var desc = GetStr(wr, "description", "");
            var tendency = GetStr(wr, "tendency", "");
            var season = GetStr(wr, "season", "");
            var temp = GetStr(wr, "temperature", "");
            var wind = GetStr(wr, "windSpeed", GetStr(wr, "wind", ""));
            var visibility = GetStr(wr, "visibility", "");
            var mechEffects = GetStr(wr, "mechanicalEffects", "");

            text.Add("[bold cyan]🌤️ Погода:[/]");
            // Show biome context for weather interpretation (Block 27)
            var locationCatalog = await ReadMortalLocationPlayerCatalogAsync();
            if (locationCatalog.CurrentLocationId != null &&
                locationCatalog.TryGetLocation(locationCatalog.CurrentLocationId, out var currentLocation) &&
                currentLocation != null)
            {
                var biome = ReadPlayerNodeString(currentLocation.Data, "biome");
                if (!string.IsNullOrEmpty(biome))
                    text.Add($"  🌍 Биом: [white]{Markup.Escape(biome)}[/]");
            }
            var weatherState = GetStr(wr, "currentState", GetStr(wr, "state", ""));
            if (!string.IsNullOrEmpty(weatherState))
                text.Add($"  ☁ Состояние: [bold white]{Markup.Escape(weatherState)}[/]");
            if (!string.IsNullOrEmpty(desc))
                text.Add($"  {Markup.Escape(desc)}");
            if (!string.IsNullOrEmpty(season))
                text.Add($"  🍂 Сезон: [white]{Markup.Escape(season)}[/]");
            if (!string.IsNullOrEmpty(temp))
                text.Add($"  🌡️ Температура: [white]{Markup.Escape(temp)}[/]");
            if (!string.IsNullOrEmpty(wind))
                text.Add($"  💨 Ветер: [white]{Markup.Escape(wind)}[/]");
            if (!string.IsNullOrEmpty(visibility))
                text.Add($"  👁 Видимость: [white]{Markup.Escape(visibility)}[/]");
            if (!string.IsNullOrEmpty(tendency) && tendency != "NO_CHANGE")
            {
                var tendLabel = tendency switch
                {
                    "IMPROVE" => "[green]Улучшение ↑[/]",
                    "WORSEN" => "[red]Ухудшение ↓[/]",
                    _ when tendency.StartsWith("JUMP_TO_") => $"[yellow]→ {Markup.Escape(tendency.Replace("JUMP_TO_", ""))}[/]",
                    _ => $"[yellow]{Markup.Escape(tendency)}[/]"
                };
                text.Add($"  📈 Тенденция: {tendLabel}");
            }
            if (!string.IsNullOrEmpty(mechEffects))
                text.Add($"  ⚙ Эффекты: [dim]{Markup.Escape(mechEffects)}[/]");
        }
        else
        {
            text.Add("[dim]Данные о погоде недоступны[/]");
        }

        var panel = new Panel(GameInterface.SafeMarkup(string.Join("\n", text)))
        {
            Header = new PanelHeader(" 🌤️ Время и погода ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1)
        };
        Write(panel);
        WaitForKey();
    }

    private async Task ShowStory()
    {
        if (_storyService == null)
        {
            MarkupLine("[dim]Сервис рассказов недоступен.[/]");
            WaitForKey();
            return;
        }

        var stories = _storyService.GetAvailableStories();
        if (stories.Count == 0)
        {
            Write(new Panel(new Markup("[dim]Рассказ пока пуст. Сыграйте несколько ходов, и ваша история начнёт записываться.[/]"))
            {
                Header = new PanelHeader(" 📜 Рассказ ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Gold1),
                Padding = new Padding(2, 1)
            });
            WaitForKey();
            return;
        }

        var currentStoryPath = Services.StoryService.GetStoryPath(
            _stateManager.CurrentState.CurrentRealm ?? "Chaos Sea",
            _stateManager.CurrentState.Incarnation);

        while (true)
        {
            Clear();
            Write(new Rule("[gold1]📜 Рассказ — Ваша История[/]").RuleStyle("gold1"));
            WriteLine();
            MarkupLine("[dim]Каждый ваш ход записывается в вечную книгу. Здесь вы можете перечитать свою историю из Мира Смертных и Моря Хаоса.[/]");
            WriteLine();

            var choices = stories.Select(s =>
            {
                var isCurrent = string.Equals(s.RelativePath, currentStoryPath, StringComparison.OrdinalIgnoreCase);
                var currentTag = isCurrent ? " [green](текущая глава)[/]" : "";
                return $"📖 {s.DisplayName} ({s.EntryCount} записей){currentTag}";
            }).ToList();
            choices.Add("💾 Экспортировать всё в .txt");
            choices.Add("[dim]← Назад[/]");

            var selected = Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]Выберите главу:[/]")
                    .PageSize(15)
                    .AddChoices(choices));

            var selIdx = choices.IndexOf(selected);
            if (selIdx == stories.Count)
            {
                // Export all stories
                await ExportAllStoriesToTxt(stories);
                continue;
            }
            if (selIdx < 0 || selIdx >= stories.Count) break;

            var story = stories[selIdx];
            await ShowStoryReader(story);
        }
    }

    private async Task ShowStoryReader(Services.StoryFileInfo storyInfo)
    {
        if (_storyService == null) return;

        const int pageSize = 20;
        var allEntries = await _storyService.ReadStoryAsync(storyInfo.RelativePath);
        if (allEntries.Count == 0)
        {
            MarkupLine("[dim]Эта глава пока пуста.[/]");
            WaitForKey();
            return;
        }

        var totalPages = (allEntries.Count + pageSize - 1) / pageSize;
        var currentPage = totalPages - 1; // Start from the latest page

        while (true)
        {
            Clear();
            Write(new Rule($"[gold1]📖 {Markup.Escape(storyInfo.DisplayName)}[/]").RuleStyle("gold1"));
            MarkupLine($"[dim]Страница {currentPage + 1} из {totalPages} | {allEntries.Count} записей[/]\n");

            var startIdx = currentPage * pageSize;
            var endIdx = Math.Min(startIdx + pageSize, allEntries.Count);

            for (var i = startIdx; i < endIdx; i++)
            {
                var e = allEntries[i];
                var isMarker = e.Turn < 0;

                if (isMarker)
                {
                    // Transition marker
                    Write(new Rule($"[yellow]✦ {Markup.Escape(e.Player.Trim('[', ']'))} ✦[/]").RuleStyle("yellow"));
                    if (!string.IsNullOrEmpty(e.Narrative))
                        MarkupLine($"  [italic yellow]{Markup.Escape(e.Narrative)}[/]");
                    WriteLine();
                    continue;
                }

                // Regular turn
                var tsDisplay = DateTime.TryParse(e.Timestamp, out var dt) ? dt.ToLocalTime().ToString("dd.MM HH:mm") : "";
                var locStr = !string.IsNullOrEmpty(e.Location) ? $" [dim]📍 {Markup.Escape(e.Location)}[/]" : "";

                MarkupLine($"[dim]─── Ход {e.Turn} {tsDisplay}{locStr} ───[/]");
                MarkupLine($"  [cyan]▸ {Markup.Escape(e.Player)}[/]");
                if (!string.IsNullOrEmpty(e.Narrative))
                    MarkupLine($"  [white]{Markup.Escape(e.Narrative)}[/]");
                WriteLine();
            }

            // Navigation
            var navChoices = new List<string>();
            if (currentPage > 0) navChoices.Add("◀ Предыдущая страница");
            if (currentPage < totalPages - 1) navChoices.Add("▶ Следующая страница");
            navChoices.Add("⏮ В начало");
            navChoices.Add("⏭ В конец");
            navChoices.Add("💾 Экспортировать главу в .txt");
            navChoices.Add("← Назад к списку");

            var nav = Prompt(
                new SelectionPrompt<string>()
                    .Title($"[dim]Страница {currentPage + 1}/{totalPages}[/]")
                    .PageSize(8)
                    .AddChoices(navChoices));

            if (nav.Contains("Предыдущая")) currentPage--;
            else if (nav.Contains("Следующая")) currentPage++;
            else if (nav.Contains("В начало")) currentPage = 0;
            else if (nav.Contains("В конец")) currentPage = totalPages - 1;
            else if (nav.Contains("Экспортировать")) { await ExportStoryToTxt(storyInfo.DisplayName, allEntries); }
            else break;

            currentPage = Math.Clamp(currentPage, 0, totalPages - 1);
        }
    }

    private static string FormatStoryEntryAsText(Services.StoryEntry e)
    {
        if (e.Turn < 0)
        {
            // Marker entry
            var marker = e.Player.Trim('[', ']');
            return $"══════════════ {marker} ══════════════\n{e.Narrative}\n";
        }

        var sb = new System.Text.StringBuilder();
        var tsDisplay = DateTime.TryParse(e.Timestamp, out var dt)
            ? dt.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
            : e.Timestamp;
        var locStr = !string.IsNullOrEmpty(e.Location) ? $" | {e.Location}" : "";

        sb.AppendLine($"--- Ход {e.Turn} | {tsDisplay}{locStr} ---");
        sb.AppendLine($"> {e.Player}");
        if (!string.IsNullOrEmpty(e.Narrative))
        {
            sb.AppendLine();
            sb.AppendLine(e.Narrative);
        }
        sb.AppendLine();
        return sb.ToString();
    }

    private async Task ExportStoryToTxt(string chapterName, List<Services.StoryEntry> entries)
    {
        try
        {
            var safeName = string.Join("_", chapterName.Split(Path.GetInvalidFileNameChars()));
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{safeName}_{timestamp}.txt";
            var relativePath = $"stories/export/{fileName}";
            var fullPath = _fs.ResolvePath(relativePath);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"╔══════════════════════════════════════════╗");
            sb.AppendLine($"║  {chapterName}");
            sb.AppendLine($"║  Экспорт: {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine($"║  Записей: {entries.Count}");
            sb.AppendLine($"╚══════════════════════════════════════════╝");
            sb.AppendLine();

            foreach (var e in entries)
                sb.Append(FormatStoryEntryAsText(e));

            var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            Exception? operationFailure = null;
            var completed = false;
            try
            {
                await _fs.WriteFileAtomicAsync(lease, relativePath, sb.ToString());
                completed = true;
            }
            catch (Exception failure)
            {
                operationFailure = failure;
                throw;
            }
            finally
            {
                await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, lease, completed, operationFailure);
            }

            MarkupLine($"\n[green]Экспортировано:[/] [link]{Markup.Escape(fullPath)}[/]");
            MarkupLine($"[dim]{entries.Count} записей сохранено.[/]");
            WaitForKey();
        }
        catch (Exception ex) when (ex is not (CoordinatedStatePublicationUncertainException or SessionReplacedException))
        {
            MarkupLine($"[red]Ошибка экспорта: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
        }
    }

    private async Task ExportAllStoriesToTxt(List<Services.StoryFileInfo> stories)
    {
        if (_storyService == null) return;

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"Полная_История_{timestamp}.txt";
            var relativePath = $"stories/export/{fileName}";
            var fullPath = _fs.ResolvePath(relativePath);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("╔══════════════════════════════════════════╗");
            sb.AppendLine("║     Книга Вечности — Полная История     ║");
            sb.AppendLine($"║  Экспорт: {DateTime.Now:dd.MM.yyyy HH:mm}                  ║");
            sb.AppendLine("╚══════════════════════════════════════════╝");
            sb.AppendLine();

            var totalEntries = 0;
            var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            Exception? operationFailure = null;
            var completed = false;
            try
            {
                foreach (var story in stories)
                {
                    var entries = await _storyService.ReadStoryAsync(lease, story.RelativePath);
                    if (entries.Count == 0) continue;

                    sb.AppendLine();
                    sb.AppendLine($"████████████████████████████████████████████");
                    sb.AppendLine($"  {story.DisplayName}");
                    sb.AppendLine($"  ({entries.Count} записей)");
                    sb.AppendLine($"████████████████████████████████████████████");
                    sb.AppendLine();

                    foreach (var e in entries)
                        sb.Append(FormatStoryEntryAsText(e));

                    totalEntries += entries.Count;
                }

                await _fs.WriteFileAtomicAsync(lease, relativePath, sb.ToString());
                completed = true;
            }
            catch (Exception failure)
            {
                operationFailure = failure;
                throw;
            }
            finally
            {
                await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, lease, completed, operationFailure);
            }

            MarkupLine($"\n[green]Экспортировано:[/] [link]{Markup.Escape(fullPath)}[/]");
            MarkupLine($"[dim]{stories.Count} глав, {totalEntries} записей сохранено.[/]");
            WaitForKey();
        }
        catch (Exception ex) when (ex is not (CoordinatedStatePublicationUncertainException or SessionReplacedException))
        {
            MarkupLine($"[red]Ошибка экспорта: {Markup.Escape(ex.Message)}[/]");
            WaitForKey();
        }
    }

    private async Task ShowChronicle()
    {
        var text = new List<string>();

        var chrDoc = await _stateManager.LoadGameStateFileAsync("game_state/meta/character_chronicle.json");
        if (chrDoc != null)
        {
            int idx = 0;
            EnumerateJsonItems(chrDoc.RootElement, item =>
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString() ?? "";
                    if (!string.IsNullOrEmpty(s))
                        text.Add($"  📖 {Markup.Escape(s)}");
                    return;
                }

                // Structured entry with title/content/timestamp
                var title = GetStr(item, "title", "");
                var content = GetStr(item, "content",
                    GetStr(item, "summary",
                        GetStr(item, "entryToAppend",
                            GetStr(item, "entry",
                                GetStr(item, "description", "")))));
                var timestamp = GetStr(item, "timestamp", "");
                var chapterId = GetStr(item, "chapterId", "");

                if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(content))
                    return;

                if (idx > 0) text.Add("");

                // Chapter header
                if (!string.IsNullOrEmpty(title))
                    text.Add($"  [bold yellow]📖 {Markup.Escape(title)}[/]");

                // Timestamp / turn number
                var turnNumber = GetStr(item, "turnNumber", GetStr(item, "turn", ""));
                if (!string.IsNullOrEmpty(timestamp) || !string.IsNullOrEmpty(turnNumber))
                {
                    var tsLine = "    [dim]";
                    if (!string.IsNullOrEmpty(turnNumber))
                        tsLine += $"🔄 Ход {Markup.Escape(turnNumber)}";
                    if (!string.IsNullOrEmpty(timestamp))
                    {
                        var tsDisplay = DateTime.TryParse(timestamp, out var dt)
                            ? dt.ToString("dd.MM.yyyy HH:mm")
                            : timestamp;
                        if (!string.IsNullOrEmpty(turnNumber)) tsLine += " — ";
                        tsLine += $"🕐 {Markup.Escape(tsDisplay)}";
                    }
                    tsLine += "[/]";
                    text.Add(tsLine);
                }

                // Content body
                if (!string.IsNullOrEmpty(content))
                    text.Add($"    {Markup.Escape(content)}");

                // Any extra fields we don't explicitly handle
                if (item.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in item.EnumerateObject())
                    {
                        if (prop.Name is "title" or "content" or "timestamp" or "chapterId"
                            or "summary" or "entryToAppend" or "entry" or "description"
                            or "entryId" or "eventType" or "turnNumber" or "turn"
                            || prop.Name.StartsWith("_")) continue;
                        var label = NpcFieldToRussian(prop.Name);
                        var val = prop.Value.ValueKind == JsonValueKind.String
                            ? prop.Value.GetString() ?? ""
                            : prop.Value.ToString();
                        if (!string.IsNullOrEmpty(val))
                            text.Add($"    [dim]{Markup.Escape(label)}: {Markup.Escape(val)}[/]");
                    }
                }

                idx++;
            });
        }

        var playerChronicleDoc = await _stateManager.LoadGameStateFileAsync("lore/chaos_sea/player_chronicle.json");
        if (playerChronicleDoc != null &&
            playerChronicleDoc.RootElement.TryGetProperty("entries", out var chronicleEntries) &&
            chronicleEntries.ValueKind == JsonValueKind.Array &&
            chronicleEntries.GetArrayLength() > 0)
        {
            if (text.Count > 0)
                text.Add("");
            text.Add("[bold]🌊 Хроника Душ:[/]");

            foreach (var entry in chronicleEntries.EnumerateArray())
            {
                var title = GetStr(entry, "title", GetStr(entry, "lifeTitle", ""));
                var summary = GetStr(entry, "summary", GetStr(entry, "description", GetStr(entry, "content", "")));
                var timestamp = GetStr(entry, "timestamp", GetStr(entry, "completedAtUtc", ""));

                if (!string.IsNullOrWhiteSpace(title))
                    text.Add($"  [bold yellow]{Markup.Escape(title)}[/]");
                if (!string.IsNullOrWhiteSpace(summary))
                    text.Add($"  {Markup.Escape(summary)}");
                if (!string.IsNullOrWhiteSpace(timestamp))
                    text.Add($"  [dim]{Markup.Escape(timestamp)}[/]");
                text.Add("");
            }
        }

        // Plot outline (Block 22 — mainArc, characterSubplots, loomingThreatsOrOpportunities)
        var plotDoc = await _stateManager.LoadGameStateFileAsync("game_state/quests/plot_outline.json");
        if (plotDoc != null)
        {
            var plotItems = new List<string>();
            var root = plotDoc.RootElement;

            // Main Arc
            if (root.TryGetProperty("mainArc", out var mainArc) && mainArc.ValueKind == JsonValueKind.Object)
            {
                var summary = GetStr(mainArc, "summary", "");
                var nextStep = GetStr(mainArc, "nextImmediateStep", "");
                var climax = GetStr(mainArc, "potentialClimax", "");
                if (!string.IsNullOrEmpty(summary))
                {
                    plotItems.Add($"  [bold]🎯 Главная арка:[/] [white]{Markup.Escape(summary)}[/]");
                    if (!string.IsNullOrEmpty(nextStep))
                        plotItems.Add($"    ➤ Следующий шаг: [green]{Markup.Escape(nextStep)}[/]");
                    if (!string.IsNullOrEmpty(climax))
                        plotItems.Add($"    ⚡ Возможная кульминация: [dim]{Markup.Escape(climax)}[/]");
                }
            }

            // Character Subplots
            if (root.TryGetProperty("characterSubplots", out var subplots) && subplots.ValueKind == JsonValueKind.Array && subplots.GetArrayLength() > 0)
            {
                plotItems.Add("");
                plotItems.Add("  [bold]👤 Подсюжеты персонажей:[/]");
                foreach (var sp in subplots.EnumerateArray())
                {
                    var charName = GetStr(sp, "characterName", "?");
                    var arcSummary = GetStr(sp, "arcSummary", "");
                    var nextDev = GetStr(sp, "nextStep", GetStr(sp, "nextDevelopment", ""));
                    var conflict = GetStr(sp, "potentialConflictOrResolution", "");
                    plotItems.Add($"    [cyan]{Markup.Escape(charName)}[/]: {Markup.Escape(arcSummary)}");
                    if (!string.IsNullOrEmpty(nextDev))
                        plotItems.Add($"      ➤ {Markup.Escape(nextDev)}");
                    if (!string.IsNullOrEmpty(conflict))
                        plotItems.Add($"      [dim]⚡ {Markup.Escape(conflict)}[/]");
                }
            }

            // Looming Threats or Opportunities
            if (root.TryGetProperty("loomingThreatsOrOpportunities", out var threats) && threats.ValueKind == JsonValueKind.Array && threats.GetArrayLength() > 0)
            {
                plotItems.Add("");
                plotItems.Add("  [bold]⚠ Угрозы и возможности:[/]");
                foreach (var t in threats.EnumerateArray())
                {
                    var tText = t.ValueKind == JsonValueKind.String ? (t.GetString() ?? "") : t.GetRawText();
                    if (!string.IsNullOrEmpty(tText))
                        plotItems.Add($"    • [yellow]{Markup.Escape(tText)}[/]");
                }
            }

            // Fallback: generic enumeration for non-Block-22 format
            if (plotItems.Count == 0)
            {
                EnumerateJsonItems(root, item =>
                {
                    var title = GetStr(item, "title", GetStr(item, "name", ""));
                    var desc = GetStr(item, "description", "");
                    if (string.IsNullOrEmpty(title)) return;
                    plotItems.Add($"  📌 [white]{Markup.Escape(title)}[/]");
                    if (!string.IsNullOrEmpty(desc))
                        plotItems.Add($"    [dim]{Markup.Escape(desc)}[/]");
                });
            }

            if (plotItems.Count > 0)
            {
                text.Add("");
                text.Add("[bold yellow]📌 Сюжетная линия:[/]");
                text.AddRange(plotItems);
            }
        }

        if (text.Count == 0) text.Add("[dim]Хроника пуста — ваша история ещё не написана.[/]");

        var panel = new Panel(GameInterface.SafeMarkup(string.Join("\n", text)))
        {
            Header = new PanelHeader(" 📖 Хроника персонажа ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Blue),
            Padding = new Padding(2, 1),
            Expand = true
        };
        Write(panel);
        WaitForKey();
    }

    private async Task ShowBehaviorAssessment()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/player_behavior.json");
        if (doc == null)
        {
            ShowEmptyPanel("Оценка поведения", "Данные оценки поведения недоступны");
            return;
        }

        var root = doc.RootElement;
        var assessment = root.TryGetProperty("playerBehaviorAssessment", out var pba) && pba.ValueKind == JsonValueKind.Object
            ? pba
            : root;
        var coeff = root.TryGetProperty("historyManipulationCoefficient", out var hm) && hm.ValueKind == JsonValueKind.Number
            ? hm.GetDouble()
            : (assessment.TryGetProperty("historyManipulationCoefficient", out var nestedHm) && nestedHm.ValueKind == JsonValueKind.Number
                ? nestedHm.GetDouble()
                : 0.0);

        var lines = new List<string>
        {
            "[bold cyan]🧠 Оценка поведения игрока[/]",
            ""
        };

        var coeffColor = coeff switch
        {
            >= 1.0 => "red",
            >= 0.5 => "yellow",
            > 0.0 => "green",
            _ => "grey"
        };
        lines.Add($"  Коэффициент манипуляции историей: [{coeffColor}]{coeff:F2}[/]");

        var coeffMeaning = coeff switch
        {
            >= 1.0 => "Высокий риск грубого вмешательства в историю или правила",
            >= 0.5 => "Заметная попытка повлиять на историю/мета-слой",
            > 0.0 => "Слабые признаки манипуляции историей",
            _ => "Манипулирование историей не обнаружено"
        };
        lines.Add($"  [dim]{Markup.Escape(coeffMeaning)}[/]");

        var known = new[] { "historyManipulationCoefficient" };
        RenderExtraFields(lines, assessment, known, "  ");

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🧠 Поведение игрока ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
    }

    private async Task ShowStorageAccess()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/misc/storage_access.json");
        if (doc == null)
        {
            ShowEmptyPanel("Доступ к хранилищам", "Данные доступа к хранилищам недоступны");
            return;
        }

        var lines = new List<string> { "[bold cyan]📦 Доступ к хранилищам[/]" };
        var rendered = false;
        void RenderAccessArray(string title, JsonElement arr, string color)
        {
            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return;
            rendered = true;
            lines.Add("");
            lines.Add($"[bold {color}]{title}:[/]");
            foreach (var item in arr.EnumerateArray())
            {
                var storageId = GetStr(item, "storageId", GetStr(item, "storageName", "хранилище"));
                var playerId = GetStr(item, "playerId", GetStr(item, "targetPlayerId", GetStr(item, "sharedWithPlayerId", "")));
                var line = $"  • {Markup.Escape(storageId)}";
                if (!string.IsNullOrWhiteSpace(playerId))
                    line += $" → [white]{Markup.Escape(playerId)}[/]";
                lines.Add(line);
                RenderExtraFields(lines, item, new[] { "storageId", "storageName", "playerId", "targetPlayerId", "sharedWithPlayerId" }, "    ");
            }
        }

        var root = doc.RootElement;
        if (root.TryGetProperty("grantStorageAccess", out var grants))
            RenderAccessArray("Выдан доступ", grants, "green");
        if (root.TryGetProperty("shareStorageAccess", out var shares))
            RenderAccessArray("Совместный доступ", shares, "yellow");
        if (root.TryGetProperty("revokeStorageAccess", out var revokes))
            RenderAccessArray("Отозван доступ", revokes, "red");

        if (!rendered)
            lines.Add("\n[dim]Нет данных о доступах к хранилищам[/]");

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 📦 Доступ к хранилищам ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
    }

    private async Task ShowPlayerInteractions()
    {
        var commandLine = string.IsNullOrWhiteSpace(_currentCommandRemainder)
            ? "/взаимодействия"
            : "/взаимодействия " + _currentCommandRemainder;
        var result = await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(commandLine, _stateManager, _fs);
        if (result != null)
            ExplorerCommandResultConsoleRenderer.Render(_console, result);

        WaitForKey();
    }

    // ═══ Meta-info and additional helpers ═══

    private async Task<bool> IsHistoryManipulationEnabled()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/player_behavior.json");
        if (doc == null) return false;
        var root = doc.RootElement;
        var coeff = root.TryGetProperty("historyManipulationCoefficient", out var hm) && hm.ValueKind == JsonValueKind.Number
            ? hm.GetDouble()
            : (root.TryGetProperty("playerBehaviorAssessment", out var pba) &&
               pba.ValueKind == JsonValueKind.Object &&
               pba.TryGetProperty("historyManipulationCoefficient", out var nestedHm) &&
               nestedHm.ValueKind == JsonValueKind.Number
                ? nestedHm.GetDouble()
                : 0.0);
        return coeff > 0.0;
    }

    private async Task ShowNpcMetaInfo()
    {
        if (!await IsHistoryManipulationEnabled()) return;

        var metaText = new List<string>();
        metaText.Add("[dim italic]🔮 Режим манипулирования историей активен[/]");

        // NPC personality
        var persDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_personality.json");
        if (persDoc != null)
        {
            EnumerateJsonItems(persDoc.RootElement, item =>
            {
                var name = GetStr(item, "NPCName", GetStr(item, "name", "?"));
                var traits = GetStr(item, "traits", GetStr(item, "description", ""));
                if (!string.IsNullOrEmpty(traits))
                    metaText.Add($"  🧠 [magenta]{Markup.Escape(name)}[/]: {Markup.Escape(traits)}");
            });
        }

        // NPC journals (thought diaries)
        var jourDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_journals.json");
        if (jourDoc != null)
        {
            EnumerateJsonItems(jourDoc.RootElement, item =>
            {
                var name = GetStr(item, "NPCName", GetStr(item, "name", "?"));
                var entry = GetStr(item, "lastJournalNote", "");
                if (!string.IsNullOrEmpty(entry))
                    metaText.Add($"  📓 [dim]{Markup.Escape(name)}: «{Markup.Escape(entry)}»[/]");
            });
        }

        // NPC masks
        var maskDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_masks.json");
        if (maskDoc != null)
        {
            EnumerateJsonItems(maskDoc.RootElement, item =>
            {
                var name = GetStr(item, "NPCName", GetStr(item, "name", "?"));
                var mask = GetStr(item, "activeMask", GetStr(item, "maskName", ""));
                if (!string.IsNullOrEmpty(mask))
                    metaText.Add($"  🎭 [red]{Markup.Escape(name)}[/]: маска «{Markup.Escape(mask)}»");
            });
        }

        // NPC memory
        var memDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_memory.json");
        if (memDoc != null)
        {
            EnumerateJsonItems(memDoc.RootElement, item =>
            {
                var name = GetStr(item, "NPCName", GetStr(item, "name", "?"));
                var mem = GetStr(item, "content", "");
                if (!string.IsNullOrEmpty(mem))
                    metaText.Add($"  💭 [dim]{Markup.Escape(name)}: {Markup.Escape(mem)}[/]");
            });
        }

        // Item journals (sentient items)
        var itemJDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/item_journals.json");
        if (itemJDoc != null)
        {
            EnumerateJsonItems(itemJDoc.RootElement, item =>
            {
                var name = GetStr(item, "itemName", GetStr(item, "name", "?"));
                var entry = GetStr(item, "entry", GetStr(item, "journal", ""));
                if (!string.IsNullOrEmpty(entry))
                    metaText.Add($"  📖 [cyan]{Markup.Escape(name)}[/]: «{Markup.Escape(entry)}»");
                else if (item.TryGetProperty("journalEntries", out var journalEntries) &&
                         journalEntries.ValueKind == JsonValueKind.Array &&
                         journalEntries.GetArrayLength() > 0)
                {
                    var latest = journalEntries.EnumerateArray().Last();
                    var latestText = latest.ValueKind == JsonValueKind.String
                        ? latest.GetString() ?? ""
                        : GetStr(latest, "description", GetStr(latest, "text", GetStr(latest, "spiritVoice", "")));
                    if (!string.IsNullOrWhiteSpace(latestText))
                        metaText.Add($"  📖 [cyan]{Markup.Escape(name)}[/]: «{Markup.Escape(latestText)}»");
                }
            });
        }

        if (metaText.Count > 1)
        {
            WriteLine();
            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", metaText)))
            {
                Header = new PanelHeader(" 🔮 Мета-информация (манипулирование историей) ", Justify.Center),
                Border = BoxBorder.Heavy,
                BorderStyle = new Style(Color.Magenta1),
                Padding = new Padding(2, 1)
            });
        }
    }

    private async Task ShowItemTexts()
    {
        var itemTextDoc = await _stateManager.LoadGameStateFileAsync("game_state/inventory/item_text_updates.json");
        var itemsDoc = await _stateManager.LoadGameStateFileAsync("game_state/inventory/items.json");
        var itemJournalsDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/item_journals.json");

        var inventoryRoot = ToJsonNode(itemsDoc);
        var itemTextRoot = ToJsonNode(itemTextDoc);
        var itemJournalRoot = ToJsonNode(itemJournalsDoc);
        var shelf = ReadableInventoryDocumentShelfProjection.Build(inventoryRoot, itemTextRoot, itemJournalRoot);

        if (shelf.Items.Count == 0)
        {
            ShowEmptyPanel("Книги и записи", "Нет читаемых предметов (книг, писем, свитков и т.д.)");
            WaitForKey();
            return;
        }

        if (!string.IsNullOrWhiteSpace(_currentCommandRemainder))
        {
            var selectedItem = ReadableInventoryDocumentShelfProjection.FindBySelector(shelf, _currentCommandRemainder);
            if (selectedItem == null)
            {
                MarkupLine("[yellow]Такой документ не найден на книжной полке.[/]");
                WaitForKey();
                return;
            }

            ShowReadableDocumentDetail(selectedItem);
            WaitForKey();
            return;
        }

        while (true)
        {
            ShowReadableDocumentShelf(shelf);
            var choices = shelf.Items
                .Select(static item => Markup.Escape(item.ChoiceLabel))
                .Append("← Назад")
                .ToList();
            var selected = Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]📜 Книги и записи[/]")
                    .PageSize(15)
                    .AddChoices(choices));

            if (selected.Contains("← Назад", StringComparison.Ordinal))
                break;

            var selectedIndex = choices.IndexOf(selected);
            if (selectedIndex < 0 || selectedIndex >= shelf.Items.Count)
                break;

            ShowReadableDocumentDetail(shelf.Items[selectedIndex]);
            WaitForKey();
        }

        static JsonNode? ToJsonNode(JsonDocument? document) =>
            document == null ? null : JsonNode.Parse(document.RootElement.GetRawText());
    }

    private void ShowReadableDocumentShelf(ReadableDocumentShelf shelf)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow)
            .Expand()
            .AddColumn("Документ")
            .AddColumn("Источник")
            .AddColumn("Доступ")
            .AddColumn("Кратко");

        foreach (var item in shelf.Items)
        {
            table.AddRow(
                GameInterface.SafeMarkupText(item.Title),
                GameInterface.SafeMarkupText(item.Source),
                GameInterface.SafeMarkupText(item.AccessStatus),
                GameInterface.SafeMarkupText(item.Summary));
        }

        Write(new Panel(table)
        {
            Header = new PanelHeader(" 📜 Книжная полка ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(1, 0)
        });
    }

    private void ShowReadableDocumentDetail(ReadableDocumentShelfItem item)
    {
        var lines = new List<string>
        {
            $"[bold yellow]📜 {Markup.Escape(item.Title)}[/]",
            $"[dim]Источник: {Markup.Escape(item.Source)}[/]",
            $"[dim]Доступ: {Markup.Escape(item.AccessStatus)}[/]",
            string.Empty
        };

        if (item.HasReadableContent)
        {
            for (var index = 0; index < item.Entries.Count; index++)
            {
                if (item.Entries.Count > 1)
                    lines.Add($"[dim]Запись {index + 1}[/]");

                lines.Add(Markup.Escape(item.Entries[index]));
                lines.Add(string.Empty);
            }
        }
        else
        {
            lines.Add($"[yellow]{Markup.Escape(item.UnreadableReason ?? "Текст пока недоступен.")}[/]");
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines), "readable document detail"))
        {
            Header = new PanelHeader(" Чтение документа ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(2, 1)
        });
    }
}

