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
{private async Task ShowAchievements()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/achievements.json");
        var rarityColors = new Dictionary<string, string>
        {
            ["common"] = "white", ["uncommon"] = "green", ["rare"] = "blue",
            ["epic"] = "purple", ["legendary"] = "yellow"
        };
        var rarityLabels = new Dictionary<string, string>
        {
            ["common"] = "Обычное", ["uncommon"] = "Необычное", ["rare"] = "Редкое",
            ["epic"] = "Эпическое", ["legendary"] = "Легендарное"
        };
        var categoryLabels = new Dictionary<string, string>
        {
            ["combat"] = "⚔️ Бой", ["exploration"] = "🗺️ Исследование", ["story"] = "📖 Сюжет",
            ["social"] = "🤝 Социальное", ["crafting"] = "🔨 Крафт", ["meta"] = "🌌 Мета",
            ["death"] = "💀 Смерть", ["secret"] = "❓ Секрет"
        };

        var unlocked = new List<(string id, string name, string desc, string category, string rarity, string icon, string date, int incarnation, bool hidden, string rewardType, string rewardValue)>();
	        var tracked = new List<(string id, string name, string desc, string category, string rarity, string icon, int current, int target, bool hidden, string rewardType, string rewardValue)>();
        var statsSummary = new List<string>();

        if (doc != null)
        {
            if (doc.RootElement.TryGetProperty("unlockedAchievements", out var uArr) &&
                uArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in uArr.EnumerateArray())
                {
                    unlocked.Add((
                        GetStr(a, "achievementId", ""),
                        GetStr(a, "name", "???"),
                        GetStr(a, "description", ""),
                        GetStr(a, "category", "other"),
                        GetStr(a, "rarity", "common"),
                        GetStr(a, "icon", "🏆"),
                        GetStr(a, "unlockedAt", ""),
                        GetInt(a, "incarnation", -1),
                        a.TryGetProperty("hidden", out var hidden) && hidden.ValueKind == JsonValueKind.True,
                        a.TryGetProperty("reward", out var reward) && reward.ValueKind == JsonValueKind.Object
                            ? GetStr(reward, "type", "")
                            : "",
                        a.TryGetProperty("reward", out reward) && reward.ValueKind == JsonValueKind.Object
                            ? GetStr(reward, "value", "")
                            : ""
                    ));
                }
            }

            if (doc.RootElement.TryGetProperty("trackedProgress", out var tArr) &&
                tArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in tArr.EnumerateArray())
                {
                    int cur = 0, tar = 1;
                    if (t.TryGetProperty("progress", out var prog))
                    {
                        if (prog.TryGetProperty("current", out var cv)) cur = cv.TryGetInt32(out var ci) ? ci : 0;
                        if (prog.TryGetProperty("target", out var tv)) tar = tv.TryGetInt32(out var ti) ? ti : 1;
                    }
	                    tracked.Add((
	                        GetStr(t, "achievementId", ""),
	                        GetStr(t, "name", "???"),
	                        GetStr(t, "description", ""),
	                        GetStr(t, "category", "other"),
	                        GetStr(t, "rarity", "common"),
	                        GetStr(t, "icon", "📊"),
	                        cur, tar,
	                        t.TryGetProperty("hidden", out var hidden) && hidden.ValueKind == JsonValueKind.True,
	                        t.TryGetProperty("reward", out var reward) && reward.ValueKind == JsonValueKind.Object
	                            ? GetStr(reward, "type", "")
	                            : "",
                        t.TryGetProperty("reward", out reward) && reward.ValueKind == JsonValueKind.Object
                            ? GetStr(reward, "value", "")
                            : ""
                    ));
                }
            }

            if (doc.RootElement.TryGetProperty("stats", out var stats) && stats.ValueKind == JsonValueKind.Object)
            {
                var totalUnlocked = GetInt(stats, "totalUnlocked", unlocked.Count);
                statsSummary.Add($"[bold]Всего открыто:[/] [yellow]{totalUnlocked}[/]");

                if (stats.TryGetProperty("byCategory", out var byCategory) && byCategory.ValueKind == JsonValueKind.Object)
                {
                    var categoryParts = byCategory.EnumerateObject()
                        .Where(prop => prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var value) && value > 0)
                        .Select(prop => $"{Markup.Escape(categoryLabels.GetValueOrDefault(prop.Name, prop.Name))}: {prop.Value}")
                        .ToList();
                    if (categoryParts.Count > 0)
                        statsSummary.Add($"[dim]По категориям: {string.Join(", ", categoryParts)}[/]");
                }

                if (stats.TryGetProperty("byRarity", out var byRarity) && byRarity.ValueKind == JsonValueKind.Object)
                {
                    var rarityParts = byRarity.EnumerateObject()
                        .Where(prop => prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var value) && value > 0)
                        .Select(prop => $"{Markup.Escape(rarityLabels.GetValueOrDefault(prop.Name, prop.Name))}: {prop.Value}")
                        .ToList();
                    if (rarityParts.Count > 0)
                        statsSummary.Add($"[dim]По редкости: {string.Join(", ", rarityParts)}[/]");
                }
            }
        }

        if (unlocked.Count == 0 && tracked.Count == 0)
        {
            ShowEmptyPanel(_loc.T("achievements"), "Пока нет достижений — совершайте подвиги!");
            WaitForKey();
            return;
        }

        while (true)
        {
            Clear();
            var items = new List<string>();

            if (statsSummary.Count > 0)
            {
                Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", statsSummary)))
                {
                    Header = new PanelHeader(" 📊 Сводка достижений ", Justify.Center),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Gold1),
                    Padding = new Padding(1, 0)
                });
                WriteLine();
            }

            // Group by category
	            var visibleTracked = tracked.Where(t => !t.hidden).ToList();
	            var allCategories = unlocked.Select(a => a.category)
	                .Concat(visibleTracked.Select(t => t.category))
	                .Distinct()
	                .OrderBy(c => c)
	                .ToList();

            foreach (var cat in allCategories)
            {
                var catLabel = categoryLabels.GetValueOrDefault(cat, cat);
                items.Add($"[dim]── {catLabel} ──[/]");

                foreach (var a in unlocked.Where(a => a.category == cat))
                {
                    var color = rarityColors.GetValueOrDefault(a.rarity, "white");
                    items.Add($"{a.icon} [{color}]{Markup.Escape(a.name)}[/] [dim]({rarityLabels.GetValueOrDefault(a.rarity, a.rarity)})[/]");
                }
	                foreach (var t in visibleTracked.Where(t => t.category == cat))
	                {
	                    var pct = t.target > 0 ? (int)(100.0 * t.current / t.target) : 0;
	                    var color = rarityColors.GetValueOrDefault(t.rarity, "white");
	                    items.Add($"{t.icon} [{color}]{Markup.Escape(t.name)}[/] [dim]({rarityLabels.GetValueOrDefault(t.rarity, t.rarity)}, {pct}%)[/]");
	                }
            }

            items.Add("← Назад");

            var choice = Prompt(
                new SelectionPrompt<string>()
	                    .Title($"[bold yellow]🏆 {_loc.T("achievements")}[/] [dim]({unlocked.Count} открыто, {visibleTracked.Count} в процессе)[/]")
                    .HighlightStyle(new Style(Color.Yellow))
                    .PageSize(15)
                    .AddChoices(items));

            if (choice == "← Назад") break;

            // Skip category separator lines
            if (choice.Contains("──")) continue;

            // Find which achievement was selected by matching the choice text
            var uMatch = unlocked.FirstOrDefault(a =>
                choice.Contains(Markup.Escape(a.name)));
            if (uMatch.id != null)
            {
                var color = rarityColors.GetValueOrDefault(uMatch.rarity, "white");
                var cat = categoryLabels.GetValueOrDefault(uMatch.category, uMatch.category);
                var text = new List<string>
                {
                    $"[bold {color}]{uMatch.icon} {Markup.Escape(uMatch.name)}[/]",
                    $"[dim]{rarityLabels.GetValueOrDefault(uMatch.rarity, uMatch.rarity)} • {cat}[/]",
                    "",
                    Markup.Escape(uMatch.desc)
                };
                if (!string.IsNullOrEmpty(uMatch.date))
                {
                    text.Add("");
                    text.Add($"[dim]Получено: {Markup.Escape(uMatch.date)}[/]");
                }
                if (uMatch.incarnation >= 0)
                    text.Add($"[dim]Инкарнация: {uMatch.incarnation}[/]");
                if (uMatch.hidden)
                    text.Add("[dim]Это достижение было скрытым до разблокировки.[/]");
                if (!string.IsNullOrWhiteSpace(uMatch.rewardType) || !string.IsNullOrWhiteSpace(uMatch.rewardValue))
                {
                    text.Add("");
                    text.Add($"[yellow]Награда:[/] {Markup.Escape(FormatAchievementRewardText(uMatch.rewardType, uMatch.rewardValue))}");
                }

                var panel = new Panel(GameInterface.SafeMarkup(string.Join("\n", text)))
                {
                    Header = new PanelHeader(" 🏆 Достижение ", Justify.Center),
                    Border = BoxBorder.Double,
                    BorderStyle = new Style(Color.Yellow),
                    Padding = new Padding(2, 1)
                };
                Write(panel);
                WaitForKey();
                continue;
            }
	            var tMatch = visibleTracked.FirstOrDefault(t =>
	                choice.Contains(Markup.Escape(t.name)));
	            if (tMatch.name != null)
	            {
	                var pct = tMatch.target > 0 ? (int)(100.0 * tMatch.current / tMatch.target) : 0;
	                var bar = new string('█', pct / 5) + new string('░', 20 - pct / 5);
	                var cat = categoryLabels.GetValueOrDefault(tMatch.category, tMatch.category);
	                var text = new List<string>
	                {
	                    $"[bold]{tMatch.icon} {Markup.Escape(tMatch.name)}[/]",
	                    $"[dim]{cat} • {Markup.Escape(rarityLabels.GetValueOrDefault(tMatch.rarity, tMatch.rarity))}[/]",
                    "",
                    Markup.Escape(tMatch.desc),
                    "",
                    $"[yellow]{bar}[/] {tMatch.current}/{tMatch.target} ({pct}%)"
                };
                if (!string.IsNullOrWhiteSpace(tMatch.rewardType) || !string.IsNullOrWhiteSpace(tMatch.rewardValue))
                {
                    text.Add("");
                    text.Add($"[yellow]Награда:[/] {Markup.Escape(FormatAchievementRewardText(tMatch.rewardType, tMatch.rewardValue))}");
                }

                var panel = new Panel(GameInterface.SafeMarkup(string.Join("\n", text)))
                {
                    Header = new PanelHeader(" 📊 Прогресс ", Justify.Center),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Yellow),
                    Padding = new Padding(2, 1)
                };
                Write(panel);
                WaitForKey();
            }
        }
    }

    private async Task ShowGmThoughts()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("output/debug_logs.json");
        if (doc == null) { ShowEmptyPanel(_loc.T("gm_thoughts"), "Нет данных ГМ"); return; }

        var text = GetStr(doc.RootElement, "gm_thoughts_markdown", "Нет данных");
        var panel = new Panel(new Markup(Markup.Escape(text)))
        {
            Header = new PanelHeader($" {_loc.T("gm_thoughts")} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(2, 1)
        };

        Write(panel);
        WaitForKey();
    }

    private async Task ShowDebugInfo()
    {
        var files = _fs.GetAllGameStateFiles();
        var text = new List<string>
        {
            $"[bold]Файлов состояния:[/] {files.Length}",
            $"[bold]Сессия:[/] {_stateManager.CurrentState.SessionId}",
            $"[bold]Язык:[/] {_loc.CurrentLanguage}",
            "",
            "[yellow]Файлы:[/]"
        };

        foreach (var file in files.Take(20))
        {
            long size = 0;
            try { size = new FileInfo(file).Length; } catch { /* file may have been deleted */ }
            text.Add($"  {Markup.Escape(Path.GetFileName(file))} [dim]({size} байт)[/]");
        }

        if (files.Length > 20)
            text.Add($"  [dim]...и ещё {files.Length - 20} файлов[/]");

        // Multipliers
        var multDoc = await _stateManager.LoadGameStateFileAsync("game_state/misc/multipliers.json");
        if (multDoc != null)
        {
            text.Add("");
            text.Add("[yellow]Множители:[/]");
            foreach (var prop in multDoc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array || prop.Value.ValueKind == JsonValueKind.Object)
                {
                    EnumerateJsonItems(prop.Value, item =>
                    {
                        var name = GetStr(item, "name", GetStr(item, "id", prop.Name));
                        var val = GetStr(item, "value", GetStr(item, "multiplier", "?"));
                        text.Add($"  {Markup.Escape(name)}: [cyan]{Markup.Escape(val)}[/]");
                    });
                }
                else
                {
                    text.Add($"  {Markup.Escape(prop.Name)}: [cyan]{Markup.Escape(prop.Value.ToString())}[/]");
                }
            }
        }

        var panel = new Panel(GameInterface.SafeMarkup(string.Join("\n", text)))
        {
            Header = new PanelHeader($" {_loc.T("debug_info")} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(2, 1)
        };

        Write(panel);
        WaitForKey();
    }

    private async Task ShowCurrentLocation()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/world/current_location.json");
        if (doc == null)
        {
            ShowEmptyPanel(_loc.T("where_am_i"), "Местоположение неизвестно");
            return;
        }

        var root = GetCurrentLocationRoot(doc.RootElement);
        var playerLevel = await GetPlayerLevelAsync();
        var text = new List<string>
        {
            $"[bold green]📍 {Markup.Escape(GetStr(root, "name", "Неизвестно"))}[/]",
        };

        var locType = GetStr(root, "locationType", "");
        var biome = GetStr(root, "biome", "");
        var typeInfo = new List<string>();
        if (!string.IsNullOrEmpty(locType)) typeInfo.Add(locType);
        if (!string.IsNullOrEmpty(biome)) typeInfo.Add(biome);
        if (typeInfo.Count > 0)
            text.Add($"  [dim]{Markup.Escape(string.Join(" • ", typeInfo))}[/]");

        // Difficulty assessment (use external profile first, fall back to internal)
        var profileProp = root.TryGetProperty("externalDifficultyProfile", out var extP) ? extP
            : root.TryGetProperty("internalDifficultyProfile", out var intP) ? intP
            : (JsonElement?)null;

        if (profileProp.HasValue && profileProp.Value.ValueKind == JsonValueKind.Object)
        {
            var (label, color) = GetProfileDifficultyLabel(profileProp.Value, playerLevel);
            text.Add($"  ⚠ Опасность: [{color}]{label}[/]  [dim](ур. {playerLevel})[/]");
        }
        else
        {
            // Simple difficulty field fallback
            var simpleDiff = GetInt(root, "difficulty", -1);
            if (simpleDiff >= 0)
            {
                var (label, color) = GetDifficultyLabel(simpleDiff, playerLevel);
                text.Add($"  ⚠ Опасность: [{color}]{label}[/]  [dim](ур. {playerLevel})[/]");
            }
        }

        text.Add("");

        var desc = GetStr(root, "description", "");
        if (!string.IsNullOrEmpty(desc))
            text.Add(Markup.Escape(desc));

        // Features
        if (root.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Array && features.GetArrayLength() > 0)
        {
            text.Add("");
            text.Add("[bold]Особенности:[/]");
            foreach (var f in features.EnumerateArray())
            {
                var fStr = f.ValueKind == JsonValueKind.String ? f.GetString() ?? "" : f.ToString();
                if (!string.IsNullOrEmpty(fStr))
                    text.Add($"  ✦ [cyan]{Markup.Escape(fStr)}[/]");
            }
        }

        // Faction control
        if (root.TryGetProperty("factionControl", out var fc) && fc.ValueKind == JsonValueKind.Array && fc.GetArrayLength() > 0)
        {
            text.Add("");
            foreach (var f in fc.EnumerateArray())
            {
                var fName = GetStr(f, "factionName", GetStr(f, "factionId", GetStr(f, "name", "?")));
                var fLevel = GetStr(f, "controlLevel", "");
                var fType = GetStr(f, "controlType", "");
                var fLine = $"  🏰 Фракция: [yellow]{Markup.Escape(fName)}[/]";
                if (!string.IsNullOrEmpty(fType)) fLine += $" [dim]({Markup.Escape(fType)})[/]";
                if (!string.IsNullOrEmpty(fLevel)) fLine += $" контроль: [white]{Markup.Escape(fLevel)}%[/]";
                text.Add(fLine);
            }
        }

        // Active threats
        if (root.TryGetProperty("activeThreats", out var threats) && threats.ValueKind == JsonValueKind.Array && threats.GetArrayLength() > 0)
        {
            text.Add("");
            text.Add("[bold red]⚠ Активные угрозы:[/]");
            foreach (var t in threats.EnumerateArray())
                RenderThreatSummary(text, t);
        }

        var events = GetStr(root, "lastEventsDescription", "");
        if (!string.IsNullOrEmpty(events))
        {
            text.Add("");
            text.Add("[yellow]Последние события:[/]");
            text.Add(Markup.Escape(events));
        }

        // World time
        var timeDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/world_time.json");
        if (timeDoc != null)
        {
            AppendWorldTimeLines(text, timeDoc.RootElement, "");
        }

        // Weather
        var wDoc = await _stateManager.LoadGameStateFileAsync("game_state/world/weather.json");
        if (wDoc != null)
        {
            var weatherRoot = GetWeatherRoot(wDoc.RootElement);
            var wDesc = GetStr(weatherRoot, "description", "");
            if (!string.IsNullOrEmpty(wDesc))
                text.Add($"🌤️ Погода: [cyan]{Markup.Escape(wDesc)}[/]");
        }

        var panel = new Panel(GameInterface.SafeMarkup(string.Join("\n", text)))
        {
            Header = new PanelHeader($" {_loc.T("where_am_i")} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1),
            Expand = true
        };

        Write(panel);
        WaitForKey();
    }

    // ═════════════════════════════════════════════════════════
    // WORLD SETUP / DIRECTIVES
    // ═════════════════════════════════════════════════════════

    private async Task ShowWorldSetup()
    {
        if (_worldDirectiveService == null)
        {
            ShowEmptyPanel("Настройка мира", "Сервис world setup недоступен");
            return;
        }

        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Настройка мира"))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Настройка мира", "Подготовка следующего мира доступна только в Море Хаоса или Сияющей Обители. Для текущего мира используйте /world_rules.");
            return;
        }

        while (true)
        {
            Clear();
            var pending = await _worldDirectiveService.ReadPendingSetupAsync();
            var scenarioCore = _scenarioCoreService == null ? null : await _scenarioCoreService.ReadAsync();
            var profiles = await _worldDirectiveService.GetAvailableProfilesAsync();
            var profilesDir = _worldDirectiveService.GetProfilesDirectoryPath();

            var lines = new List<string>
            {
                "[bold cyan]🌍 Подготовка следующего мира[/]",
                "",
                "[white]Здесь можно заранее задать сеттинг следующей смертной жизни.[/]",
                "[white]Эти данные сохраняются в client-authored файле [bold]game_state/control/incarnation_world_setup.json[/].[/]",
                "[white]Во время воплощения GM обязан читать этот файл и затем клиент перенесёт его в [bold]lore/current_world/world_directives.json[/].[/]",
                $"[dim]Папка профилей: {Markup.Escape(profilesDir)}[/]"
            };

            if (pending == null)
            {
                lines.Add("");
                lines.Add("[yellow]Подготовка следующего мира пока не задана.[/]");
            }
            else
            {
                lines.Add("");
                var pendingTitle = string.IsNullOrWhiteSpace(pending.WorldDirectives.WorldTitle)
                    ? "Без названия"
                    : pending.WorldDirectives.WorldTitle;
                lines.Add($"[green]Текущая подготовка мира:[/] [bold]{Markup.Escape(pendingTitle)}[/]");
                lines.Add($"[dim]Режим: {Markup.Escape(pending.Mode)}[/]");
                if (!string.IsNullOrWhiteSpace(pending.ProfileName))
                    lines.Add($"[dim]Профиль: {Markup.Escape(pending.ProfileName)} ({Markup.Escape(pending.ProfileId ?? "")})[/]");
                if (!string.IsNullOrWhiteSpace(pending.CharacterDescription))
                    lines.Add($"[bold]Персонаж:[/] {Markup.Escape(TruncateForUi(pending.CharacterDescription, 220))}");
                if (!string.IsNullOrWhiteSpace(pending.StartingCircumstances))
                    lines.Add($"[bold]Старт:[/] {Markup.Escape(TruncateForUi(pending.StartingCircumstances, 220))}");
                AppendWorldDirectiveLines(lines, pending.WorldDirectives, concise: true);
                if (scenarioCore != null)
                {
                    lines.Add("");
                    lines.Add($"[magenta]Сценарное ядро:[/] {scenarioCore.ScenarioCoreAssertions.Count} подтверждённых фактов");
                    lines.Add($"[dim]Извлечённых, но не подтверждённых фактов: {scenarioCore.CandidateAssertions.Count} • Открытых correction slots: {scenarioCore.OpenCorrectionSlots.Count}[/]");
                }
            }

            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 🌍 Настройка мира ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(2, 1),
                Expand = true
            });
            WriteLine();

            var actions = new List<string>
            {
                "👁 Полный просмотр подготовки мира",
                "🧩 Просмотреть сценарное ядро",
                "✅ Подтвердить извлечённые факты",
                "📚 Просмотреть профили миров",
                "✅ Применить профиль мира",
                "✏️ Создать / редактировать подготовку мира",
                "🧹 Очистить подготовку мира",
                "📂 Открыть папку профилей",
                "← Назад"
            };

            var choice = Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Действие:[/]")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(actions));

            if (choice == "← Назад")
                return;

            if (choice == "👁 Полный просмотр подготовки мира")
            {
                if (pending == null)
                {
                    ShowEmptyPanel("Подготовка мира", "Подготовка следующего мира пока не задана.");
                }
                else
                {
                    var detailLines = new List<string>();
                    var pendingTitle = string.IsNullOrWhiteSpace(pending.WorldDirectives.WorldTitle)
                        ? "Без названия"
                        : pending.WorldDirectives.WorldTitle;
                    detailLines.Add($"[bold cyan]{Markup.Escape(pendingTitle)}[/]");
                    detailLines.Add($"[dim]Режим: {Markup.Escape(pending.Mode)}[/]");
                    if (!string.IsNullOrWhiteSpace(pending.ProfileName))
                        detailLines.Add($"[dim]Профиль: {Markup.Escape(pending.ProfileName)} ({Markup.Escape(pending.ProfileId ?? "")})[/]");
                    if (!string.IsNullOrWhiteSpace(pending.CharacterDescription))
                        detailLines.Add($"[bold]Персонаж:[/] {Markup.Escape(pending.CharacterDescription)}");
                    if (!string.IsNullOrWhiteSpace(pending.StartingCircumstances))
                        detailLines.Add($"[bold]Стартовые обстоятельства:[/] {Markup.Escape(pending.StartingCircumstances)}");
                    detailLines.Add("");
                    AppendWorldDirectiveLines(detailLines, pending.WorldDirectives, concise: false);

                    Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", detailLines)))
                    {
                        Header = new PanelHeader(" 👁 Подготовка мира ", Justify.Center),
                        Border = BoxBorder.Double,
                        BorderStyle = new Style(Color.Cyan1),
                        Padding = new Padding(2, 1),
                        Expand = true
                    });
                    WaitForKey();
                }
                continue;
            }

            if (choice == "🧩 Просмотреть сценарное ядро")
            {
                await ShowScenarioCoreReviewAsync();
                continue;
            }

            if (choice == "✅ Подтвердить извлечённые факты")
            {
                await ConfirmScenarioCoreCandidatesAsync();
                continue;
            }

            if (choice == "📚 Просмотреть профили миров")
            {
                await ShowWorldProfiles(profiles);
                continue;
            }

            if (choice == "✅ Применить профиль мира")
            {
                if (profiles.Count == 0)
                {
                    MarkupLine("[yellow]В папке world_profiles пока нет профилей.[/]");
                    WaitForKey();
                    continue;
                }

                var selectedLabel = Prompt(
                    new SelectionPrompt<string>()
                        .Title("[cyan]Выберите профиль:[/]")
                        .HighlightStyle(new Style(Color.Cyan1))
                        .AddChoices(profiles.Select(profile => $"{profile.Name} ({profile.FileName})")));
                var profile = profiles.First(p => $"{p.Name} ({p.FileName})" == selectedLabel);
                var setup = _worldDirectiveService.CreatePendingSetupFromProfile(profile);
                if (!ConfirmPendingWorldSetupWritePreview("Применить профиль мира", pending, setup, "profile_apply"))
                    continue;

                await _worldDirectiveService.WritePendingSetupAsync(setup);
                if (_scenarioCoreService != null)
                    await _scenarioCoreService.RefreshFromPendingSetupAsync();
                MarkupLine($"[green]Профиль мира «{Markup.Escape(profile.Name)}» применён к подготовке следующего мира.[/]");
                WaitForKey();
                continue;
            }

            if (choice == "✏️ Создать / редактировать подготовку мира")
            {
                await EditPendingWorldSetupAsync(pending);
                continue;
            }

            if (choice == "🧹 Очистить подготовку мира")
            {
                if (ConfirmPendingWorldSetupWritePreview("Очистить подготовку мира", pending, null, "clear"))
                {
                    _worldDirectiveService.ClearPendingSetup();
                    if (_scenarioCoreService != null)
                        await _scenarioCoreService.ClearAsync();
                    MarkupLine("[green]Подготовка следующего мира очищена.[/]");
                    WaitForKey();
                }
                continue;
            }

            if (choice == "📂 Открыть папку профилей")
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = profilesDir,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    MarkupLine($"[yellow]{Markup.Escape(profilesDir)}[/]");
                    WaitForKey();
                }
            }
        }
    }

    private async Task ShowWorldRules()
    {
        if (_worldDirectiveService == null)
        {
            ShowEmptyPanel("Правила мира", "Сервис досье мира недоступен");
            return;
        }

        if (_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Правила мира", "Во время загробного цикла используйте /world_setup для подготовки следующего мира. Активное досье мира появляется в смертной жизни.");
            return;
        }

        while (true)
        {
            Clear();
            var directives = await _worldDirectiveService.ReadActiveWorldDirectivesAsync();
            var lines = new List<string>
            {
                "[bold green]📜 Досье текущего мира[/]",
                "",
                "[white]Это постоянное досье текущего мира, заданное игроком для этой смертной жизни.[/]",
                "[white]GM должен читать [bold]lore/current_world/world_directives.json[/] на каждом ходе.[/]"
            };

            if (directives == null)
            {
                lines.Add("");
                lines.Add("[yellow]Файл world_directives.json ещё не создан.[/]");
                lines.Add("[dim]Вы можете создать его сейчас и зафиксировать описание мира, ограничения и поправки.[/]");
            }
            else
            {
                lines.Add("");
                AppendWorldDirectiveLines(lines, directives, concise: false);
            }

            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 📜 Досье мира ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Green3),
                Padding = new Padding(2, 1),
                Expand = true
            });
            WriteLine();

            var choice = Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Действие:[/]")
                    .HighlightStyle(new Style(Color.Green3))
                    .AddChoices("✏️ Создать / редактировать досье мира", "🧹 Очистить досье мира", "← Назад"));

            if (choice == "← Назад")
                return;

            if (choice == "✏️ Создать / редактировать досье мира")
            {
                var edited = await PromptWorldDirectivesAsync(directives ?? new WorldDirectiveService.WorldDirectives(), allowProfileMetadataEdit: false);
                await _worldDirectiveService.WriteActiveWorldDirectivesAsync(edited);
                MarkupLine("[green]Досье мира сохранено.[/]");
                WaitForKey();
                continue;
            }

            if (choice == "🧹 Очистить досье мира")
            {
                if (Confirm("[yellow]Удалить активное досье текущего мира?[/]", false))
                {
                    _fs.DeleteFile(WorldDirectiveService.ActiveDirectivesPath);
                    MarkupLine("[green]Досье мира очищено.[/]");
                    WaitForKey();
                }
            }
        }
    }

    private async Task ShowGuardianCorrections()
    {
        if (_guardianCorrectionService == null)
        {
            ShowEmptyPanel("Коррективы Хранителя", "Сервис корректив хранителя недоступен.");
            return;
        }

        if (_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Коррективы Хранителя", "Эта команда показывает коррективы только для текущей смертной жизни.");
            return;
        }

        var state = await _guardianCorrectionService.ReadAsync();
        if (state == null)
        {
            ShowEmptyPanel("Коррективы Хранителя", "Для этой жизни ещё не записано явных Корректив Хранителя.");
            return;
        }

        var lines = new List<string>
        {
            $"[bold magenta]Коррективы текущей жизни[/]",
            "",
            $"[bold]Хранитель:[/] {Markup.Escape(state.GuardianName)}",
            $"[bold]Намерение:[/] {Markup.Escape(state.Intent)}",
            $"[bold]Репутация при применении:[/] {state.ReputationAtApplication}",
            $"[bold]Сила Обители:[/] {state.PowerBefore} → {state.PowerAfter}",
            $"[bold]Бюджет:[/] {state.BaseBudgetPoints} очк. • Осталось: {state.RemainingBudgetPoints}",
            $"[bold]Потрачено силы Обители:[/] {state.TotalAbodePowerSpent}",
            $"[bold]Итог:[/] {Markup.Escape(state.Summary)}"
        };

        lines.Add("");
        lines.Add("[bold]Подтверждённое ядро сценария:[/]");
        if (state.ScenarioCoreSnapshot.ScenarioCoreAssertions.Count == 0)
        {
            lines.Add("[dim]Не записано.[/]");
        }
        else
        {
            foreach (var assertion in state.ScenarioCoreSnapshot.ScenarioCoreAssertions.Take(12))
                lines.Add($"  • [magenta]{Markup.Escape(assertion.Category)}[/]: {Markup.Escape(assertion.Value)}");
            if (state.ScenarioCoreSnapshot.ScenarioCoreAssertions.Count > 12)
                lines.Add($"  [dim]… и ещё {state.ScenarioCoreSnapshot.ScenarioCoreAssertions.Count - 12}[/]");
        }

        lines.Add("");
        lines.Add("[bold]Применённые коррективы:[/]");
        if (state.Corrections.Count == 0)
        {
            lines.Add("[dim]Явных корректив не применено.[/]");
        }
        else
        {
            foreach (var correction in state.Corrections)
            {
                lines.Add($"  • [bold]{Markup.Escape(correction.Title)}[/]");
                lines.Add($"    [dim]{Markup.Escape(correction.Summary)}[/]");
                lines.Add($"    [dim]Slot: {Markup.Escape(correction.SlotType)} • Severity: {Markup.Escape(correction.Severity)} • Budget: {correction.BudgetCostPoints} • Power cost: {correction.AbodePowerCost}[/]");
                lines.Add($"    [dim]Причина: {Markup.Escape(correction.Reason)}[/]");
            }
        }

        if (state.Claimants.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Claimants:[/]");
            foreach (var claimant in state.Claimants)
            {
                lines.Add($"  • [white]{Markup.Escape(claimant.GuardianName)}[/] [dim]({Markup.Escape(claimant.Intent)})[/]");
                lines.Add($"    [dim]Power {claimant.CurrentPower} → {claimant.PowerAfter} • Budget {claimant.BaseBudgetPoints}+{claimant.PreparationBudgetPoints} → {claimant.RemainingBudgetPoints} • Claim base {claimant.ClaimStrengthBase}[/]");
                if (!string.IsNullOrWhiteSpace(claimant.SourceSummary))
                    lines.Add($"    [dim]{Markup.Escape(claimant.SourceSummary)}[/]");
            }
        }

        if (state.ContestedSlots.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Contested slots:[/]");
            foreach (var contest in state.ContestedSlots.Where(item => item.Candidates.Count > 1))
            {
                lines.Add($"  • [white]{Markup.Escape(contest.SlotType)}[/] → [magenta]{Markup.Escape(contest.WinnerGuardianName)}[/]");
                foreach (var candidate in contest.Candidates.Take(4))
                    lines.Add($"    [dim]{Markup.Escape(candidate.SourceGuardianName)} • {Markup.Escape(candidate.Severity)} • claim {candidate.ClaimStrength}[/]");
            }
        }

        if (state.ResolutionOrder.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Порядок резолюции:[/]");
            foreach (var step in state.ResolutionOrder)
                lines.Add($"  • [dim]{Markup.Escape(step)}[/]");
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🧭 Коррективы Хранителя ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Magenta1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
    }

    private async Task ShowWorldProfiles(List<WorldDirectiveService.WorldProfileDescriptor> profiles)
    {
        if (profiles.Count == 0)
        {
            ShowEmptyPanel("Профили миров", "В папке world_profiles пока нет профилей.");
            return;
        }

        while (true)
        {
            Clear();
            var choice = Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Профили миров:[/]")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .PageSize(15)
                    .AddChoices(profiles.Select(profile => $"{profile.Name} ({profile.FileName})").Append("← Назад")));

            if (choice == "← Назад")
                return;

            var profile = profiles.First(p => $"{p.Name} ({p.FileName})" == choice);
            var lines = new List<string>
            {
                $"[bold cyan]{Markup.Escape(profile.Name)}[/]",
                $"[dim]{Markup.Escape(profile.FileName)}[/]"
            };
            if (!string.IsNullOrWhiteSpace(profile.Description))
            {
                lines.Add("");
                lines.Add(GameInterface.EscapeMarkup(profile.Description));
            }

            lines.Add("");
            AppendWorldDirectiveLines(lines, profile.Directives, concise: false);

            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 📚 Профиль мира ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(2, 1),
                Expand = true
            });
            WaitForKey();
        }
    }

    private async Task EditPendingWorldSetupAsync(WorldDirectiveService.PendingWorldSetup? existing)
    {
        if (_worldDirectiveService == null)
            return;

        var seed = existing?.WorldDirectives != null
            ? WorldDirectiveService.CloneDirectives(existing.WorldDirectives)
            : new WorldDirectiveService.WorldDirectives();
        var characterDescription = PromptLargeTextBlock("Описание персонажа следующей жизни", existing?.CharacterDescription ?? "");
        var startingCircumstances = PromptLargeTextBlock("Стартовые обстоятельства", existing?.StartingCircumstances ?? "");
        var edited = await PromptWorldDirectivesAsync(seed, allowProfileMetadataEdit: true);
        var mode = existing?.Mode ?? "manual";
        if (!string.IsNullOrWhiteSpace(existing?.ProfileId))
            mode = "mixed";

        var setup = new WorldDirectiveService.PendingWorldSetup
        {
            Mode = mode,
            ProfileId = existing?.ProfileId,
            ProfileName = existing?.ProfileName,
            CharacterDescription = characterDescription,
            StartingCircumstances = startingCircumstances,
            WorldDirectives = edited
        };
        if (!ConfirmPendingWorldSetupWritePreview("Сохранить подготовку мира", existing, setup, "manual_edit"))
            return;

        await _worldDirectiveService.WritePendingSetupAsync(setup);
        if (_scenarioCoreService != null)
            await _scenarioCoreService.RefreshFromPendingSetupAsync();
        MarkupLine("[green]Подготовка следующего мира сохранена.[/]");
        WaitForKey();
    }

    private bool ConfirmPendingWorldSetupWritePreview(
        string title,
        WorldDirectiveService.PendingWorldSetup? before,
        WorldDirectiveService.PendingWorldSetup? after,
        string operation)
    {
        Clear();
        var lines = new List<string>
        {
            $"[bold cyan]{Markup.Escape(title)}[/]",
            "",
            "[bold]Тип изменения:[/] client-local подготовка следующей смертной жизни; GM turn не отправляется.",
            $"[bold]Операция:[/] {Markup.Escape(operation)}",
            "[bold]Affected files:[/]",
            $"  • {WorldDirectiveService.PendingSetupPath}",
            $"  • {ScenarioCoreService.ManifestPath} [dim](перестраивается из pending setup или удаляется при очистке)[/]",
            "",
            "[bold]Последствия:[/]",
            "  • Эти данные будут прочитаны при следующем /incarnate или bootstrap новой жизни.",
            "  • Отмена на этом экране ничего не пишет и не очищает.",
            "  • Это не Ascension, не New Game+ и не GM-authored contract."
        };

        if (before != null)
        {
            lines.Add("");
            lines.Add($"[bold]Было:[/] mode={Markup.Escape(before.Mode)}, profile={Markup.Escape(before.ProfileName ?? before.ProfileId ?? "нет")}, worldTitle={Markup.Escape(before.WorldDirectives.WorldTitle)}");
        }

        if (after != null)
        {
            lines.Add($"[bold]Станет:[/] mode={Markup.Escape(after.Mode)}, profile={Markup.Escape(after.ProfileName ?? after.ProfileId ?? "нет")}, worldTitle={Markup.Escape(after.WorldDirectives.WorldTitle)}");
            if (!string.IsNullOrWhiteSpace(after.CharacterDescription))
                lines.Add($"  Character: [dim]{Markup.Escape(TruncateForUi(after.CharacterDescription, 220))}[/]");
            if (!string.IsNullOrWhiteSpace(after.StartingCircumstances))
                lines.Add($"  Circumstances: [dim]{Markup.Escape(TruncateForUi(after.StartingCircumstances, 220))}[/]");
        }
        else
        {
            lines.Add("[bold]Станет:[/] pending setup и scenario core будут очищены.");
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🌍 Предпросмотр подготовки мира ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        WriteJsonAuditPanel(
            "До: game_state/control/incarnation_world_setup.json",
            before == null ? new JsonObject { ["exists"] = false } : JsonSerializer.SerializeToNode(before, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
            Color.Grey);
        WriteJsonAuditPanel(
            "После: game_state/control/incarnation_world_setup.json",
            after == null ? new JsonObject { ["exists"] = false, ["deleted"] = true } : JsonSerializer.SerializeToNode(after, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
            Color.Cyan1);

        var isClearOperation = after == null;
        return Confirm(
            isClearOperation
                ? "[yellow]Очистить локальную подготовку следующего мира?[/]"
                : "[yellow]Записать локальную подготовку следующего мира?[/]",
            defaultValue: !isClearOperation);
    }

    private async Task<WorldDirectiveService.WorldDirectives> PromptWorldDirectivesAsync(
        WorldDirectiveService.WorldDirectives seed,
        bool allowProfileMetadataEdit)
    {
        var directives = WorldDirectiveService.CloneDirectives(seed);
        directives.WorldTitle = PromptOptionalText("Название мира", directives.WorldTitle);
        directives.Genre = PromptOptionalText("Жанр", directives.Genre);
        directives.Era = PromptOptionalText("Эпоха", directives.Era);
        directives.Tone = PromptOptionalText("Тон", directives.Tone);
        directives.SettingSummary = PromptOptionalText("Краткая сводка / сеттинг", directives.SettingSummary);
        directives.DetailedWorldDescription = PromptLargeTextBlock("Подробное описание мира", directives.DetailedWorldDescription);
        directives.HardRules = PromptCsvList("Жёсткие правила мира", directives.HardRules);
        directives.RequiredElements = PromptCsvList("Обязательные элементы", directives.RequiredElements);
        directives.ForbiddenElements = PromptCsvList("Запрещённые элементы", directives.ForbiddenElements);
        directives.SpecialMechanics = PromptCsvList("Особые механики", directives.SpecialMechanics);
        directives.ContinuityNotes = PromptCsvList("Ноты непрерывности / важные уточнения", directives.ContinuityNotes);
        directives.PlayerAmendments = PromptCsvList("Поправки игрока", directives.PlayerAmendments);

        if (allowProfileMetadataEdit)
        {
            directives.SourceProfileId = directives.SourceProfileId?.Trim();
            directives.SourceProfileName = directives.SourceProfileName?.Trim();
        }

        directives.LastUpdated = DateTime.UtcNow.ToString("o");
        await Task.CompletedTask;
        return directives;
    }

    private static void AppendWorldDirectiveLines(List<string> lines, WorldDirectiveService.WorldDirectives directives, bool concise)
    {
        if (!string.IsNullOrWhiteSpace(directives.WorldTitle))
            lines.Add($"[bold]Название:[/] {Markup.Escape(directives.WorldTitle)}");
        if (!string.IsNullOrWhiteSpace(directives.Genre))
            lines.Add($"[bold]Жанр:[/] {Markup.Escape(directives.Genre)}");
        if (!string.IsNullOrWhiteSpace(directives.Era))
            lines.Add($"[bold]Эпоха:[/] {Markup.Escape(directives.Era)}");
        if (!string.IsNullOrWhiteSpace(directives.Tone))
            lines.Add($"[bold]Тон:[/] {Markup.Escape(directives.Tone)}");
        if (!string.IsNullOrWhiteSpace(directives.SettingSummary))
            lines.Add($"[bold]Краткая сводка:[/] {Markup.Escape(directives.SettingSummary)}");
        if (!string.IsNullOrWhiteSpace(directives.DetailedWorldDescription))
        {
            if (concise)
                lines.Add($"[bold]Подробное описание:[/] {Markup.Escape(TruncateForUi(directives.DetailedWorldDescription, 260))}");
            else
            {
                lines.Add("[bold]Подробное описание мира:[/]");
                lines.Add(Markup.Escape(directives.DetailedWorldDescription));
            }
        }
        if (!string.IsNullOrWhiteSpace(directives.SourceProfileName))
            lines.Add($"[dim]Источник: {Markup.Escape(directives.SourceProfileName)} ({Markup.Escape(directives.SourceProfileId ?? "")})[/]");

        AppendStringList(lines, "Жёсткие правила", directives.HardRules, concise);
        AppendStringList(lines, "Обязательные элементы", directives.RequiredElements, concise);
        AppendStringList(lines, "Запрещённые элементы", directives.ForbiddenElements, concise);
        AppendStringList(lines, "Особые механики", directives.SpecialMechanics, concise);
        AppendStringList(lines, "Ноты непрерывности", directives.ContinuityNotes, concise);
        AppendStringList(lines, "Поправки игрока", directives.PlayerAmendments, concise);
    }

    private static void AppendStringList(List<string> lines, string label, IReadOnlyList<string> items, bool concise)
    {
        if (items.Count == 0)
            return;

        if (concise)
        {
            var shown = items.Take(4).Select(Markup.Escape).ToList();
            var suffix = items.Count > 4 ? $" [dim](+{items.Count - 4})[/]" : "";
            lines.Add($"[bold]{label}:[/] {string.Join("; ", shown)}{suffix}");
            return;
        }

        lines.Add($"[bold]{label}:[/]");
        foreach (var item in items)
            lines.Add($"  • {Markup.Escape(item)}");
    }

    private string PromptOptionalText(string title, string current)
    {
        return Ask($"[cyan]{Markup.Escape(title)}:[/]", current ?? string.Empty).Trim();
    }

    private string PromptLargeTextBlock(string title, string current)
    {
        while (true)
        {
            Clear();
            var currentLength = string.IsNullOrWhiteSpace(current) ? 0 : current.Length;
            var lines = new List<string>
            {
                $"[bold cyan]{Markup.Escape(title)}[/]",
                "",
                "[white]Это большое поле для подробного досье мира. В него можно вставлять абзацы и страницы текста целиком.[/]"
            };

            if (currentLength > 0)
            {
                lines.Add("");
                lines.Add($"[dim]Сейчас сохранено: {currentLength} символов[/]");
                lines.Add($"[dim]{Markup.Escape(TruncateForUi(current, 280))}[/]");
            }

            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 📝 Большое поле описания ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(2, 1),
                Expand = true
            });
            WriteLine();

            var actions = new List<string>
            {
                "✏️ Изменить текст",
                "↩️ Оставить текущее значение",
                "🧹 Очистить поле"
            };

            var choice = Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Способ заполнения поля:[/]")
                    .HighlightStyle(new Style(Color.Cyan1))
                    .AddChoices(actions));

            if (choice == "↩️ Оставить текущее значение")
                return current ?? string.Empty;

            if (choice == "🧹 Очистить поле")
                return string.Empty;

            return TextComposer.Read(
                _console,
                _clipboardService,
                new TextComposerOptions
                {
                    PromptMarkup = "[cyan]Текст:[/]",
                    DefaultValue = current ?? string.Empty,
                    PreserveNewlines = true,
                    Mode = TextComposerMode.MultilineEditor,
                    AllowClearCommand = true,
                    HelpMarkup = "[dim]Вставка из буфера работает напрямую. Вводите текст построчно; две пустые строки подряд сохраняют его. Пустой Enter сразу оставляет текущее значение. /clear очищает поле.[/]"
                });
        }
    }

    private List<string> PromptCsvList(string title, IReadOnlyCollection<string> current)
    {
        var currentValue = string.Join(", ", current);
        var raw = Ask($"[cyan]{Markup.Escape(title)} (через запятую):[/]", currentValue);

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string TruncateForUi(string value, int maxLength)
    {
        var normalized = value.Replace("\r\n", "\n").Trim();
        if (normalized.Length <= maxLength)
            return normalized;
        return normalized[..maxLength] + "...";
    }

    // ═════════════════════════════════════════════════════════
    // SYSTEM MODS — read-only global mod inspector
    // ═════════════════════════════════════════════════════════
}

