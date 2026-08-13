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
{
    private async Task ShowLoreCodex()
    {
        var codexDoc = await _stateManager.LoadGameStateFileAsync("lore/codex_entries.json");
        if (codexDoc == null)
        {
            ShowEmptyPanel("📚 Кодекс", "Записи кодекса не обнаружены");
            WaitForKey();
            return;
        }

        while (true)
        {
            var choice = Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold purple]📚 Кодекс[/]")
                    .HighlightStyle(new Style(Color.Purple))
                    .AddChoices(
                        "📚 Просмотреть записи",
                        "🔍 Поиск по кодексу",
                        "← Назад"));

            if (choice.Contains("Назад", StringComparison.Ordinal))
                return;

            if (choice.Contains("Поиск", StringComparison.Ordinal))
            {
                var query = Ask("[purple]🔍 Поиск по кодексу[/]");
                var normalizedQuery = query?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedQuery))
                    continue;

                var results = new List<string>();
                SearchJsonElement(codexDoc.RootElement, normalizedQuery.ToLowerInvariant(), "Кодекс", results, maxDepth: 6);
                if (results.Count == 0)
                    results.Add("[dim]Совпадения не найдены.[/]");

                Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", results)))
                {
                    Header = new PanelHeader(" 🔍 Результаты поиска ", Justify.Center),
                    Border = BoxBorder.Double,
                    BorderStyle = new Style(Color.Purple),
                    Padding = new Padding(2, 1)
                });
                WaitForKey();
                continue;
            }

            await ShowCodexEntries(codexDoc);
        }
    }

    private static void SearchJsonElement(JsonElement el, string queryLower, string path, List<string> results, int maxDepth, int depth = 0)
    {
        if (depth > maxDepth || results.Count >= 20) return;
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                var str = el.GetString() ?? "";
                if (str.ToLowerInvariant().Contains(queryLower))
                {
                    var snippet = str.Length > 120 ? str[..117] + "..." : str;
                    results.Add($"  {path}: [dim]{Markup.Escape(snippet)}[/]");
                }
                break;
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                    SearchJsonElement(prop.Value, queryLower, $"{path} → {prop.Name}", results, maxDepth, depth + 1);
                break;
            case JsonValueKind.Array:
                var idx = 0;
                foreach (var item in el.EnumerateArray())
                {
                    var itemName = item.ValueKind == JsonValueKind.Object
                        ? GetStr(item, "name", GetStr(item, "title", $"[{idx}]"))
                        : $"[{idx}]";
                    SearchJsonElement(item, queryLower, $"{path} → {itemName}", results, maxDepth, depth + 1);
                    idx++;
                }
                break;
        }
    }

    private void ShowLoreFileDetail(string title, JsonDocument doc)
    {
        Clear();
        var text = new List<string>();
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Object)
        {
            RenderLoreElement(root, text, depth: 0);
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                RenderLoreElement(item, text, depth: 0);
        }

        if (text.Count == 0) text.Add("[dim italic]Файл пуст[/]");

        var panel = new Panel(GameInterface.SafeMarkup(string.Join("\n", text)))
        {
            Header = GameInterface.SafePanelHeader(title),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Purple),
            Padding = new Padding(2, 1)
        };
        Write(panel);
        WaitForKey();
    }

    /// <summary>Recursively renders a JSON element into styled text lines for lore display.</summary>
    private void RenderLoreElement(JsonElement el, List<string> lines, int depth)
    {
        var indent = new string(' ', depth * 2);
        var sectionColors = new[] { "mediumpurple1", "steelblue1_1", "darkseagreen", "lightsalmon1", "plum1", "lightskyblue1" };
        var sectionColor = sectionColors[Math.Min(depth, sectionColors.Length - 1)];

        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                var prettyName = FormatLorePropertyName(prop.Name);

                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.String:
                        var val = prop.Value.GetString() ?? "";
                        if (string.IsNullOrWhiteSpace(val)) break;
                        if (depth == 0)
                        {
                            lines.Add($"[bold {sectionColor}]{Markup.Escape(prettyName)}[/]");
                            lines.Add($"{indent}  [white]{Markup.Escape(val)}[/]");
                        }
                        else
                        {
                            lines.Add($"{indent}[{sectionColor}]{Markup.Escape(prettyName)}:[/] [white]{Markup.Escape(val)}[/]");
                        }
                        lines.Add("");
                        break;

                    case JsonValueKind.Number:
                        lines.Add($"{indent}[{sectionColor}]{Markup.Escape(prettyName)}:[/] [yellow]{prop.Value}[/]");
                        lines.Add("");
                        break;

                    case JsonValueKind.True or JsonValueKind.False:
                        var boolVal = prop.Value.GetBoolean() ? "да" : "нет";
                        lines.Add($"{indent}[{sectionColor}]{Markup.Escape(prettyName)}:[/] [yellow]{boolVal}[/]");
                        lines.Add("");
                        break;

                    case JsonValueKind.Array:
                        lines.Add($"{indent}[bold {sectionColor}]{Markup.Escape(prettyName)}:[/]");
                        RenderLoreArray(prop.Value, lines, depth + 1, sectionColor);
                        lines.Add("");
                        break;

                    case JsonValueKind.Object:
                        // Section header with decorative line
                        if (depth == 0)
                        {
                            lines.Add($"[bold {sectionColor}]━━━ {Markup.Escape(prettyName)} ━━━[/]");
                        }
                        else
                        {
                            lines.Add($"{indent}[bold {sectionColor}]▸ {Markup.Escape(prettyName)}[/]");
                        }
                        RenderLoreElement(prop.Value, lines, depth + 1);
                        break;
                }
            }
        }
        else if (el.ValueKind == JsonValueKind.String)
        {
            var sv = el.GetString() ?? "";
            if (!string.IsNullOrWhiteSpace(sv))
                lines.Add($"{indent}[white]{Markup.Escape(sv)}[/]");
        }
    }

    /// <summary>Renders a JSON array into styled text lines for lore display.</summary>
    private void RenderLoreArray(JsonElement arr, List<string> lines, int depth, string parentColor)
    {
        var indent = new string(' ', depth * 2);
        int idx = 0;
        foreach (var item in arr.EnumerateArray())
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                    lines.Add($"{indent}[dim]•[/] [white]{Markup.Escape(item.GetString() ?? "")}[/]");
                    break;

                case JsonValueKind.Object:
                    // Try to find a "name"/"title" field to use as sub-header
                    var itemName = GetStr(item, "name", GetStr(item, "title", ""));
                    var itemDesc = GetStr(item, "description", GetStr(item, "content", GetStr(item, "overview", "")));

                    if (!string.IsNullOrEmpty(itemName))
                    {
                        lines.Add($"{indent}[bold white]◆ {Markup.Escape(itemName)}[/]");
                        if (!string.IsNullOrEmpty(itemDesc))
                            lines.Add($"{indent}  [white]{Markup.Escape(itemDesc)}[/]");

                        // Render remaining properties (skip name/title/description/content/overview)
                        var skipProps = new HashSet<string> { "name", "title", "description", "content", "overview" };
                        foreach (var prop in item.EnumerateObject())
                        {
                            if (skipProps.Contains(prop.Name)) continue;
                            var pName = FormatLorePropertyName(prop.Name);
                            if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                var pv = prop.Value.GetString() ?? "";
                                if (!string.IsNullOrWhiteSpace(pv))
                                    lines.Add($"{indent}  [dim]{Markup.Escape(pName)}:[/] [white]{Markup.Escape(pv)}[/]");
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                lines.Add($"{indent}  [dim]{Markup.Escape(pName)}:[/]");
                                RenderLoreArray(prop.Value, lines, depth + 2, parentColor);
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Object)
                            {
                                lines.Add($"{indent}  [dim]{Markup.Escape(pName)}:[/]");
                                RenderLoreElement(prop.Value, lines, depth + 2);
                            }
                            else
                            {
                                lines.Add($"{indent}  [dim]{Markup.Escape(pName)}:[/] [yellow]{Markup.Escape(prop.Value.ToString())}[/]");
                            }
                        }
                    }
                    else
                    {
                        // No name/title — render all props inline
                        RenderLoreElement(item, lines, depth + 1);
                    }
                    if (idx < arr.GetArrayLength() - 1)
                        lines.Add("");
                    break;

                default:
                    lines.Add($"{indent}[dim]•[/] [yellow]{Markup.Escape(item.ToString())}[/]");
                    break;
            }
            idx++;
        }
    }

    /// <summary>Converts camelCase/snake_case JSON property names into readable Russian-friendly labels.</summary>
    private static string FormatLorePropertyName(string name)
    {
        // Known translations for common lore property names
        var knownNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "Название",
            ["title"] = "Заголовок",
            ["description"] = "Описание",
            ["overview"] = "Обзор",
            ["content"] = "Содержание",
            ["summary"] = "Сводка",
            ["type"] = "Тип",
            ["genre"] = "Жанр",
            ["tone"] = "Тон",
            ["structure"] = "Структура",
            ["nature"] = "Природа",
            ["cosmology"] = "Космология",
            ["chaosSea"] = "Море Хаоса",
            ["chaos_sea"] = "Море Хаоса",
            ["mortalWorlds"] = "Смертные Миры",
            ["mortal_worlds"] = "Смертные Миры",
            ["radiantAbode"] = "Сияющая Обитель",
            ["radiant_abode"] = "Сияющая Обитель",
            ["soulMechanics"] = "Механика Душ",
            ["soul_mechanics"] = "Механика Душ",
            ["reincarnationCycle"] = "Цикл Реинкарнации",
            ["reincarnation_cycle"] = "Цикл Реинкарнации",
            ["enlightenment"] = "Просветление",
            ["soulRelics"] = "Реликвии Души",
            ["soul_relics"] = "Реликвии Души",
            ["inkFeathers"] = "Чернильные Перья",
            ["ink_feathers"] = "Чернильные Перья",
            ["guardians"] = "Хранители",
            ["artifacts"] = "Артефакты",
            ["history"] = "История",
            ["geography"] = "География",
            ["cultures"] = "Культуры",
            ["threats"] = "Угрозы",
            ["factions"] = "Фракции",
            ["magic"] = "Магия",
            ["creatures"] = "Существа",
            ["characters"] = "Персонажи",
            ["tags"] = "Метки",
            ["category"] = "Категория",
            ["subcategory"] = "Подкатегория",
            ["sourceFile"] = "Источник",
            ["source_file"] = "Источник",
            ["discoveredAt"] = "Обнаружено",
            ["discovered_at"] = "Обнаружено",
            ["discoveryContext"] = "Контекст открытия",
            ["discovery_context"] = "Контекст открытия",
            ["incarnation"] = "Инкарнация",
            ["entries"] = "Записи",
            ["totalEntries"] = "Всего записей",
            ["categories"] = "Категории",
        };

        if (knownNames.TryGetValue(name, out var translated))
            return translated;

        // Convert camelCase / snake_case to readable form
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c == '_')
            {
                sb.Append(' ');
            }
            else if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                sb.Append(' ');
                sb.Append(c);
            }
            else
            {
                sb.Append(i == 0 ? char.ToUpper(c) : c);
            }
        }
        return sb.ToString();
    }

    private async Task ShowCodexEntries(JsonDocument codexDoc)
    {
        var entriesList = CollectCodexEntries(codexDoc.RootElement);
        var codexTitlesById = entriesList
            .Select(entry => (entryId: GetStr(entry, "entryId", ""), title: GetStr(entry, "title", "")))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.entryId) && !string.IsNullOrWhiteSpace(pair.title))
            .GroupBy(pair => pair.entryId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().title, StringComparer.OrdinalIgnoreCase);

        if (entriesList.Count == 0)
        {
            ShowEmptyPanel("📚 Записи кодекса", "Пока нет записей");
            WaitForKey();
            return;
        }

        // Group by category
        var categoryIcons = new Dictionary<string, string>
        {
            ["cosmology"] = "🌊", ["geography"] = "🗺️", ["history"] = "📖",
            ["cultures"] = "🎭", ["creatures"] = "🐉", ["characters"] = "👤",
            ["artifacts"] = "💎", ["factions"] = "⚔️", ["magic"] = "🔮", ["other"] = "📝"
        };
        var categoryNames = new Dictionary<string, string>
        {
            ["cosmology"] = "Космология", ["geography"] = "География", ["history"] = "История",
            ["cultures"] = "Культуры", ["creatures"] = "Существа", ["characters"] = "Персонажи",
            ["artifacts"] = "Артефакты", ["factions"] = "Фракции", ["magic"] = "Магия", ["other"] = "Прочее"
        };

        var grouped = entriesList
            .GroupBy(e => GetStr(e, "category", "other"))
            .OrderBy(g => g.Key)
            .ToList();

        while (true)
        {
            Clear();
            var items = new List<(string label, int idx)>();
            foreach (var g in grouped)
            {
                var icon = categoryIcons.GetValueOrDefault(g.Key, "📝");
                var catName = categoryNames.GetValueOrDefault(g.Key, g.Key);
                foreach (var e in g)
                {
                    var idx = entriesList.IndexOf(e);
                    var title = GetStr(e, "title", "Без названия");
                    items.Add(($"{icon} [dim]{catName}[/]  {title}", idx));
                }
            }

            var selectList = items.Select(i => i.label).Append("← Назад").ToList();
            var choice = Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold purple]📚 Записи кодекса[/] [dim]({entriesList.Count} записей)[/]")
                    .HighlightStyle(new Style(Color.Purple))
                    .PageSize(15)
                    .AddChoices(selectList));

            if (choice == "← Назад") break;

            var sel = items.FirstOrDefault(i => i.label == choice);
            if (sel.idx >= 0 && sel.idx < entriesList.Count)
            {
                Clear();
                var entry = entriesList[sel.idx];
                var title = GetStr(entry, "title", "Без названия");
                var content = GetStr(entry, "content", "");
                var category = GetStr(entry, "category", "other");
                var subcategory = GetStr(entry, "subcategory", "");
                var context = GetStr(entry, "discoveryContext", "");
                var discoveredAt = GetStr(entry, "discoveredAt", "");
                var incarnation = GetInt(entry, "incarnation", -1);
                var sourceFile = GetStr(entry, "sourceFile", "");
                var icon = categoryIcons.GetValueOrDefault(category, "📝");
                var catName = categoryNames.GetValueOrDefault(category, category);

                var text = new List<string>();

                // Title
                text.Add($"[bold white]{Markup.Escape(title)}[/]");
                text.Add("");

                // Category / Subcategory line
                var catLine = $"[dim]Категория:[/] [{(category == "cosmology" ? "mediumpurple1" : "steelblue1_1")}]{Markup.Escape(catName)}[/]";
                if (!string.IsNullOrEmpty(subcategory))
                    catLine += $" [dim]>[/] [steelblue1_1]{Markup.Escape(subcategory)}[/]";
                text.Add(catLine);
                text.Add("");

                // Main content with separator
                if (!string.IsNullOrEmpty(content))
                {
                    text.Add("[dim]────────────────────────────────[/]");
                    text.Add("");
                    text.Add($"[white]{Markup.Escape(content)}[/]");
                    text.Add("");
                }

                // Metadata footer
                var metaLines = new List<string>();
                if (!string.IsNullOrEmpty(context))
                    metaLines.Add($"[dim italic]📍 {Markup.Escape(context)}[/]");
                if (!string.IsNullOrEmpty(discoveredAt))
                {
                    if (DateTime.TryParse(discoveredAt, out var dt))
                        metaLines.Add($"[dim italic]🕐 Обнаружено: {dt:dd.MM.yyyy HH:mm}[/]");
                    else
                        metaLines.Add($"[dim italic]🕐 {Markup.Escape(discoveredAt)}[/]");
                }
                if (incarnation >= 0)
                    metaLines.Add($"[dim italic]🔄 Инкарнация: {incarnation}[/]");
                if (!string.IsNullOrWhiteSpace(sourceFile))
                    metaLines.Add($"[dim italic]📂 Источник: {Markup.Escape(sourceFile)}[/]");

                // Tags
                if (entry.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                {
                    var tagStrs = new List<string>();
                    foreach (var t in tags.EnumerateArray())
                    {
                        if (t.ValueKind == JsonValueKind.String)
                            tagStrs.Add($"[grey on grey11] {Markup.Escape(t.GetString() ?? "")} [/]");
                    }
                    if (tagStrs.Count > 0)
                        metaLines.Add(string.Join(" ", tagStrs));
                }

                if (entry.TryGetProperty("relatedEntries", out var relatedEntries) && relatedEntries.ValueKind == JsonValueKind.Array)
                {
                    var links = relatedEntries.EnumerateArray()
                        .Where(link => link.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(link.GetString()))
                        .Select(link =>
                        {
                            var linkId = link.GetString() ?? "";
                            if (codexTitlesById.TryGetValue(linkId, out var relatedTitle) && !string.IsNullOrWhiteSpace(relatedTitle))
                                return $"{Markup.Escape(relatedTitle)} [dim]({Markup.Escape(linkId)})[/]";

                            return Markup.Escape(linkId);
                        })
                        .ToList();
                    if (links.Count > 0)
                        metaLines.Add($"[dim italic]🔗 Связанные записи: {string.Join(", ", links)}[/]");
                }

                if (metaLines.Count > 0)
                {
                    text.Add("[dim]────────────────────────────────[/]");
                    text.AddRange(metaLines);
                }

                var panel = new Panel(GameInterface.SafeMarkup(string.Join("\n", text)))
                {
                    Header = new PanelHeader($" {icon} {Markup.Escape(catName)} ", Justify.Center),
                    Border = BoxBorder.Double,
                    BorderStyle = new Style(Color.Purple),
                    Padding = new Padding(2, 1)
                };
                Write(panel);
                WaitForKey();
            }
        }
    }

    private async Task ShowLocations()
    {
        var catalog = await ReadMortalLocationPlayerCatalogAsync();

        while (true)
        {
            var menuItems = new List<(string Label, MortalLocationPlayerLocation Location)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (catalog.CurrentLocationId != null &&
                catalog.TryGetLocation(catalog.CurrentLocationId, out var current) &&
                current != null)
            {
                seen.Add(current.Identity);
                menuItems.Add(($"[bold green]📍 {Markup.Escape(current.Label)}[/] [dim](текущая)[/]", current));
                foreach (var link in catalog.Links.Where(link =>
                             string.Equals(link.SourceIdentity, current.Identity, StringComparison.Ordinal)))
                {
                    if (!catalog.TryGetLocation(link.TargetIdentity, out var target) || target == null ||
                        !seen.Add(target.Identity))
                        continue;
                    var direction = ReadPlayerNodeString(link.Data, "directionLabel");
                    var access = link.Data["access"] as JsonObject;
                    var accessState = ReadPlayerNodeString(access, "state");
                    var stateLabel = ExplorerPlayerFacingLabels.LocationLinkState(accessState);
                    var stateColor = ExplorerPlayerFacingLabels.LocationLinkStateColor(accessState);
                    var directionLabel = string.IsNullOrEmpty(direction)
                        ? string.Empty
                        : $" ({Markup.Escape(direction)})";
                    var accessLabel = string.IsNullOrEmpty(stateLabel)
                        ? string.Empty
                        : $" [{stateColor}][[{Markup.Escape(stateLabel)}]][/]";
                    menuItems.Add(($"  🧭 [{stateColor}]{Markup.Escape(target.Label)}[/]{directionLabel}{accessLabel}", target));
                }
            }

            foreach (var location in catalog.Locations)
            {
                if (!seen.Add(location.Identity))
                    continue;
                if (location.DiscoveryTier == "rumored")
                {
                    menuItems.Add(($"  ❔ [dim]{Markup.Escape(location.Label)} (слух)[/]", location));
                    continue;
                }
                var locationType = FormatLocationTypeForPlayer(
                    ReadPlayerNodeString(location.Data, "locationType"));
                var typeLabel = string.IsNullOrEmpty(locationType)
                    ? string.Empty
                    : $" [dim]({Markup.Escape(locationType)})[/]";
                menuItems.Add(($"  🗺 [dim]{Markup.Escape(location.Label)}[/]{typeLabel}", location));
            }

            if (menuItems.Count == 0)
            {
                ShowEmptyPanel(_loc.T("locations"), "Локации не обнаружены");
                return;
            }

            var choices = menuItems.Select(m => m.Label).ToList();
            choices.Add("[grey]← Назад[/]");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title($"[bold green]🗺 {_loc.T("locations")}[/]  [dim](выберите локацию для подробностей)[/]")
                .PageSize(20)
                .HighlightStyle(new Style(Color.Green))
                .AddChoices(choices));

            if (selected.Contains("← Назад", StringComparison.Ordinal))
                break;

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= menuItems.Count)
                break;

            await ShowLocationDetailPanel(menuItems[selIdx].Location, catalog);
        }
    }

    private async Task<MortalLocationPlayerCatalog> ReadMortalLocationPlayerCatalogAsync()
    {
        var worldMapTask = _fs.ReadFileAsync(MortalLocationMaterializationContract.WorldMapPath);
        var currentTask = _fs.ReadFileAsync(MortalLocationMaterializationContract.CurrentLocationPath);
        var identityTask = _fs.ReadFileAsync(MortalLocationIdentityState.StatePath);
        await Task.WhenAll(worldMapTask, currentTask, identityTask);
        return MortalLocationPlayerProjection.Create(
            await worldMapTask,
            await currentTask,
            await identityTask);
    }

    private static string ReadPlayerNodeString(JsonObject? root, string propertyName) =>
        root?[propertyName] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : string.Empty;

    private async Task ShowTransport()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/misc/vehicles.json");
        if (doc == null) { ShowEmptyPanel(_loc.T("transport"), "Транспорта нет"); return; }

        var vehicles = new List<(string name, string type, bool active, JsonElement el)>();
        EnumerateArray(doc.RootElement, "vehicles", item =>
        {
            var name = GetStr(item, "name", "???");
            var vtype = GetStr(item, "type", "");
            var active = item.TryGetProperty("isActive", out var ia) && ia.ValueKind == JsonValueKind.True;
            vehicles.Add((name, vtype, active, item));
        });
        // Also try root-level array
        if (vehicles.Count == 0)
            EnumerateJsonItems(doc.RootElement, item =>
            {
                vehicles.Add((GetStr(item, "name", "???"), GetStr(item, "type", ""), false, item));
            });

        if (vehicles.Count == 0)
        {
            ShowEmptyPanel(_loc.T("transport"), "Транспорта нет");
            return;
        }

        while (true)
        {
            var choices = vehicles.Select(v =>
            {
                var label = v.active ? $"[green]✓[/] {Markup.Escape(v.name)}" : $"  {Markup.Escape(v.name)}";
                if (!string.IsNullOrEmpty(v.type)) label += $" [dim]({Markup.Escape(v.type)})[/]";
                return label;
            }).ToList();
            choices.Add("[dim]← Назад[/]");

            var selected = Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]🚗 Транспорт[/]")
                    .PageSize(10)
                    .AddChoices(choices));

            if (selected.Contains("← Назад")) break;

            var idx = choices.IndexOf(selected);
            if (idx < 0 || idx >= vehicles.Count) break;

            var v = vehicles[idx];
            var lines = new List<string>();
            lines.Add($"[bold]{Markup.Escape(v.name)}[/]");
            if (!string.IsNullOrEmpty(v.type))
            {
                var typeLabel = v.type.ToLower() switch
                {
                    "mount" => "🐴 Ездовое животное",
                    "vehicle" => "🚗 Транспорт",
                    "summonable" => "✨ Призываемый",
                    _ => Markup.Escape(v.type)
                };
                lines.Add($"  Тип: {typeLabel}");
            }
            // Availability (translated)
            var availability = GetStr(v.el, "availability", "");
            var availLabel = availability.ToLower() switch
            {
                "active" => "[green]Активен (оседлан/управляется)[/]",
                "parked" => "[yellow]Припаркован[/]",
                "pocket" => "[cyan]В кармане (призываемый)[/]",
                _ when v.active => "[green]Активен[/]",
                _ when !string.IsNullOrEmpty(availability) => Markup.Escape(availability),
                _ => "[dim]Неактивен[/]"
            };
            lines.Add($"  Статус: {availLabel}");

            var isSentient = v.el.TryGetProperty("isSentient", out var sent) && sent.ValueKind == JsonValueKind.True;
            if (isSentient)
                lines.Add($"  🧠 [mediumpurple2]Разумный[/] [dim](действует самостоятельно в бою)[/]");
            else if (v.el.TryGetProperty("isSentient", out var sentF) && sentF.ValueKind == JsonValueKind.False)
                lines.Add($"  ⚙ [dim]Неразумный (требует действия игрока для управления в бою)[/]");

            var desc = GetStr(v.el, "description", "");
            if (!string.IsNullOrEmpty(desc))
            {
                lines.Add("");
                lines.Add($"  [white]{Markup.Escape(desc)}[/]");
            }

            // Health with visual bar
            var health = GetStr(v.el, "currentHealth", GetStr(v.el, "health", ""));
            var maxHealth = GetStr(v.el, "maxHealth", "");
            if (!string.IsNullOrEmpty(health))
            {
                var hpNum = int.TryParse(health.Replace("%", "").Trim(), out var hv) ? hv : 0;
                var maxHpNum = int.TryParse(maxHealth.Replace("%", "").Trim(), out var mv) ? mv : hpNum;
                var hpPct = maxHpNum > 0 ? Math.Clamp(hpNum * 100 / maxHpNum, 0, 100) : 100;
                var hpColor = hpPct > 60 ? "green" : hpPct > 30 ? "yellow" : "red";
                var barW = 15;
                var filled = Math.Clamp(hpPct * barW / 100, 0, barW);
                var hpBar = $"[{hpColor}]{new string('━', filled)}[/][dim grey]{new string('┄', barW - filled)}[/]";
                var hpLabel = !string.IsNullOrEmpty(maxHealth) ? $"{Markup.Escape(health)}/{Markup.Escape(maxHealth)}" : Markup.Escape(health);
                lines.Add($"  ❤️ Здоровье: {hpBar}  [{hpColor}]{hpLabel}[/]");
            }

            var speed = GetStr(v.el, "speed", "");
            if (!string.IsNullOrEmpty(speed))
                lines.Add($"  💨 Скорость: [cyan]{Markup.Escape(speed)}[/]");

            var speedBonus = GetStr(v.el, "speedBonus", "");
            if (!string.IsNullOrEmpty(speedBonus) && speedBonus != "0")
                lines.Add($"  💨 Бонус скорости: [cyan]+{Markup.Escape(speedBonus)}[/] [dim](к инициативе игрока)[/]");

            var cap = GetStr(v.el, "capacity", "");
            if (!string.IsNullOrEmpty(cap))
                lines.Add($"  📦 Вместимость: [white]{Markup.Escape(cap)}[/]");

            var curLoc = GetStr(v.el, "currentLocationId", GetStr(v.el, "currentLocation", ""));
            if (!string.IsNullOrEmpty(curLoc))
                lines.Add($"  📍 Местоположение: [white]{Markup.Escape(curLoc)}[/]");

            // Resistances
            if (v.el.TryGetProperty("resistances", out var vRes) && vRes.ValueKind == JsonValueKind.Array && vRes.GetArrayLength() > 0)
            {
                lines.Add(""); lines.Add("  [bold]🛡️ Сопротивления:[/]");
                foreach (var r in vRes.EnumerateArray())
                {
                    var rName = GetStr(r, "resistTypeDisplayName", GetStr(r, "resistanceName", GetStr(r, "resistType", "?")));
                    var rVal = GetStr(r, "resistanceValue", GetStr(r, "value", GetStr(r, "percentage", "")));
                    lines.Add($"    • {Markup.Escape(rName)}: [white]{Markup.Escape(rVal)}[/]");
                }
            }

            // Actions / combat abilities
            if (v.el.TryGetProperty("actions", out var vAct) && vAct.ValueKind == JsonValueKind.Array && vAct.GetArrayLength() > 0)
            {
                lines.Add(""); lines.Add("  [bold]⚔️ Действия:[/]");
                foreach (var a in vAct.EnumerateArray())
                {
                    var aName = GetStr(a, "actionName", GetStr(a, "name", "?"));
                    var aCost = GetStr(a, "actionCost", "");
                    var aCostLabel = aCost.ToLower() switch
                    {
                        "main" => "[yellow]осн.[/]",
                        "fast" => "[cyan]быстр.[/]",
                        "free" => "[green]своб.[/]",
                        _ when !string.IsNullOrEmpty(aCost) => $"[dim]{Markup.Escape(aCost)}[/]",
                        _ => ""
                    };
                    var aLine = $"    • [white]{Markup.Escape(aName)}[/]";
                    if (!string.IsNullOrEmpty(aCostLabel)) aLine += $" {aCostLabel}";

                    // Parse effects array for damage/type info
                    if (a.TryGetProperty("effects", out var effs) && effs.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var eff in effs.EnumerateArray())
                        {
                            var eType = GetStr(eff, "effectType", "");
                            var eVal = GetStr(eff, "value", "");
                            var eTarget = GetStr(eff, "targetTypeDisplayName", GetStr(eff, "targetType", ""));
                            var ePoise = GetStr(eff, "poiseDamage", "");
                            var eDur = GetStr(eff, "duration", "");
                            var eDesc = GetStr(eff, "effectDescription", "");

                            if (!string.IsNullOrEmpty(eVal))
                            {
                                var eColor = eType.ToLower().Contains("heal") ? "green" : "red";
                                aLine += $" [{eColor}]{Markup.Escape(eVal)}[/]";
                            }
                            if (!string.IsNullOrEmpty(eTarget)) aLine += $" [dim]({Markup.Escape(eTarget)})[/]";
                            if (!string.IsNullOrEmpty(ePoise) && ePoise != "0%") aLine += $" 🛡️[yellow]{Markup.Escape(ePoise)}[/]";
                            if (!string.IsNullOrEmpty(eDur) && eDur != "0") aLine += $" [dim]{Markup.Escape(eDur)} ход.[/]";
                            if (!string.IsNullOrEmpty(eDesc)) aLine += $" [dim]— {Markup.Escape(eDesc)}[/]";
                        }
                    }
                    else
                    {
                        // Fallback: simple damage/description fields
                        var aDmg = GetStr(a, "damage", "");
                        var aDesc = GetStr(a, "description", "");
                        if (!string.IsNullOrEmpty(aDmg)) aLine += $" [red]{Markup.Escape(aDmg)}[/]";
                        if (!string.IsNullOrEmpty(aDesc)) aLine += $" [dim]— {Markup.Escape(aDesc)}[/]";
                    }
                    lines.Add(aLine);
                }
            }

            // Special abilities
            if (v.el.TryGetProperty("specialAbilities", out var sa) && sa.ValueKind == JsonValueKind.Array)
            {
                lines.Add(""); lines.Add("  [bold]✨ Способности:[/]");
                foreach (var a in sa.EnumerateArray())
                {
                    if (a.ValueKind == JsonValueKind.String)
                        lines.Add($"    • {Markup.Escape(a.GetString() ?? "")}");
                    else
                        lines.Add($"    • {Markup.Escape(GetStr(a, "name", a.GetRawText()))}");
                }
            }

            // Inventory
            if (v.el.TryGetProperty("inventory", out var vInv) && vInv.ValueKind == JsonValueKind.Array && vInv.GetArrayLength() > 0)
            {
                var acceptedItems = vInv.EnumerateArray()
                    .Where(item => MortalItemMaterializationContract.TryReadAcceptedIdentity(item, out _))
                    .ToArray();
                if (acceptedItems.Length > 0)
                {
                    lines.Add(""); lines.Add($"  [bold]🎒 Содержимое ({acceptedItems.Length}):[/]");
                    foreach (var item in acceptedItems)
                    {
                        var iName = GetStr(item, "name", "?");
                        var iQty = GetStr(item, "quantity", GetStr(item, "count", ""));
                        var iLine = $"    • {Markup.Escape(iName)}";
                        if (!string.IsNullOrEmpty(iQty) && iQty != "1") iLine += $" ×{Markup.Escape(iQty)}";
                        lines.Add(iLine);
                    }
                }
            }

            // Catch-all for other properties
            var known = new HashSet<string> { "name", "type", "isActive", "description", "speed", "speedBonus",
                "capacity", "currentHealth", "maxHealth", "health", "specialAbilities", "id", "image_prompt",
                "availability", "isSentient", "currentLocationId", "currentLocation", "resistances",
                "actions", "inventory", "vehicleId", "actionName" };
            foreach (var prop in v.el.EnumerateObject())
            {
                if (known.Contains(prop.Name)) continue;
                var pVal = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() ?? ""
                    : (prop.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object) ? "" : prop.Value.GetRawText();
                if (pVal.Length > 0 && pVal.Length < 200)
                    lines.Add($"  [dim]{Markup.Escape(prop.Name)}: {Markup.Escape(pVal)}[/]");
            }

            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
            {
                Header = new PanelHeader(" 🚗 Транспорт ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Grey),
                Padding = new Padding(2, 1),
                Expand = true
            });

            var vehicleId = GetStr(v.el, "vehicleId", GetStr(v.el, "id", ""));
            var hasInventory = v.el.TryGetProperty("inventory", out var vehicleInventory) &&
                               vehicleInventory.ValueKind == JsonValueKind.Array;

            if (hasInventory)
            {
                var action = Prompt(
                    new SelectionPrompt<string>()
                        .Title("[bold]Действие с транспортом:[/]")
                        .HighlightStyle(new Style(Color.Yellow))
                        .AddChoices("🎒 Управлять инвентарём транспорта", "← Назад"));

                if (action.Contains("инвентарём"))
                {
                    var modified = await ShowVehicleInventoryInteractivePanel(v.name, vehicleId);
                    if (modified)
                    {
                        await _stateManager.RefreshGameStateAsync();
                        await ShowTransport();
                        return;
                    }
                    continue;
                }
            }

            await WaitForKeyWithImage("vehicle", v.name, GetStr(v.el, "image_prompt", ""), vehicleId);
        }
    }
}

