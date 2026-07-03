using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using Spectre.Console;
using Spectre.Console.Rendering;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{private async Task ShowFactions()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/factions/faction_core.json");
        if (doc == null) { ShowEmptyPanel(_loc.T("factions"), "Фракции не обнаружены"); return; }

        // Collect factions
        var factions = new List<(string name, JsonElement el)>();
        EnumerateFactionCoreEntries(doc.RootElement, item =>
        {
            if (!IsFactionPlayerVisible(item))
                return;

            factions.Add((GetStr(item, "name", "???"), item));
        });

        if (factions.Count == 0) { ShowEmptyPanel(_loc.T("factions"), "Фракции не обнаружены"); return; }

        // Load supplementary files
        var projDoc = await _stateManager.LoadGameStateFileAsync("game_state/factions/faction_projects.json");
        var strDoc = await _stateManager.LoadGameStateFileAsync("game_state/factions/faction_structure.json");
        var chrDoc = await _stateManager.LoadGameStateFileAsync("game_state/factions/faction_chronicles.json");
        var resDoc = await _stateManager.LoadGameStateFileAsync("game_state/factions/faction_resources.json");
        var custDoc = await _stateManager.LoadGameStateFileAsync("game_state/factions/faction_custom.json");

        // Interactive selector loop
        while (true)
        {
            var factionNamesById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (factionName, factionEl) in factions)
            {
                var id = GetStr(factionEl, "factionId", "");
                if (!string.IsNullOrWhiteSpace(id) && !factionNamesById.ContainsKey(id))
                    factionNamesById[id] = factionName;
            }

            var choices = new List<string>();
            foreach (var (name, el) in factions)
            {
                var rep = GetInt(el, "reputation", 0);
                var isMember = el.TryGetProperty("isPlayerMember", out var pm) && pm.ValueKind == JsonValueKind.True;
                var lvl = GetStr(el, "level", "");
                var labelParts = new List<string> { $"🏛️ {name}" };
                if (!string.IsNullOrEmpty(lvl))
                    labelParts.Add($"Уровень {lvl}");
                labelParts.Add(ReputationDisplay.BuildPlainValueLabel(rep, ReputationScaleKind.Faction));
                if (isMember)
                    labelParts.Add("Вы связаны с этой фракцией");
                choices.Add(ConsoleLayout.PlainChoiceLabel(labelParts.ToArray()));
            }
            choices.Add("← Назад");

            var selected = Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold orange1]⚔ Фракции[/]")
                    .PageSize(15)
                    .AddChoices(choices));

            if (selected == "← Назад") break;

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= factions.Count) break;

            await ShowFactionDetailPanel(factions[selIdx].el, projDoc, strDoc, chrDoc, resDoc, custDoc, factionNamesById);
        }
    }

    /// <summary>Full detailed faction panel with all subsystems.</summary>
    private async Task ShowFactionDetailPanel(JsonElement f, JsonDocument? projDoc,
        JsonDocument? strDoc, JsonDocument? chrDoc, JsonDocument? resDoc, JsonDocument? custDoc,
        Dictionary<string, string> factionNamesById)
    {
        var name = GetStr(f, "name", "???");
        var factionId = GetStr(f, "factionId", "");
        var content = new Grid().AddColumn(new GridColumn());
        var lines = new List<string>();

        // ═══ Header ═══
        var desc = GetStr(f, "description", "");
        var factionColor = GetStr(f, "factionColor", "");
        // Use faction color for header if valid HEX
        var headerColor = "orange1";
        if (!string.IsNullOrEmpty(factionColor) && factionColor.StartsWith("#") && factionColor.Length >= 7)
        {
            headerColor = factionColor; // Spectre.Console supports #RRGGBB
        }
        content.AddRow(new Markup($"[bold {headerColor}]🏛️ {Markup.Escape(name)}[/]"));
        if (!string.IsNullOrEmpty(factionColor))
            content.AddRow(new Markup($"[dim]Цвет фракции: [{headerColor}]██[/] {Markup.Escape(factionColor)}[/]"));
        if (!string.IsNullOrEmpty(desc))
        {
            content.AddRow(new Markup($"[dim italic]{Markup.Escape(desc)}[/]"));
        }

        // ═══ Core stats ═══
        var lvl = GetInt(f, "level", 0);
        var xp = GetInt(f, "experience", 0);
        var xpNext = GetInt(f, "experienceForNextLevel", 0);
        var rep = GetInt(f, "reputation", 0);
        var repDesc = GetStr(f, "reputationDescription", "");
        var playerRank = GetStr(f, "playerRank", "");
        var playerBranch = GetStr(f, "playerBranch", "");
        var isMember = f.TryGetProperty("isPlayerMember", out var pm) && pm.ValueKind == JsonValueKind.True;
        var isPlayerFaction = f.TryGetProperty("isPlayerFaction", out var pf) && pf.ValueKind == JsonValueKind.True;
        var archetype = GetStr(f, "developmentArchetype", "");
        var summaryTable = ConsoleLayout.CreateBarMetricTable(labelWidth: 24, barWidth: 18, valueWidth: 16);

        // Level + XP bar
        if (lvl > 0)
        {
            var translatedArchetype = TranslateFactionDevelopmentArchetypeForConsole(archetype);
            var archetypeTag = string.IsNullOrEmpty(translatedArchetype)
                ? string.Empty
                : $"[dim]({Markup.Escape(translatedArchetype)})[/]";
            summaryTable.AddRow(
                new Markup("[yellow]Уровень развития[/]"),
                new Markup(string.Empty),
                new Markup($"[bold yellow]{lvl}[/]"),
                new Markup(archetypeTag));

            if (xpNext > 0)
            {
                var pct = Math.Min(100, xp * 100 / Math.Max(1, xpNext));
                summaryTable.AddRow(
                    new Markup("[cyan]Прогресс развития[/]"),
                    new Markup(ConsoleLayout.CreateBarFromPercent(pct, 18, "cyan")),
                    new Markup($"[cyan]{xp}/{xpNext}[/]"),
                    new Markup($"[dim]{pct}%[/]"));
            }

            // Custom archetype priorities (Rule 21.1.2)
            if (f.TryGetProperty("customArchetypePriorities", out var cap) && cap.ValueKind == JsonValueKind.Object)
            {
                var primary = GetStr(cap, "primary", "");
                var secondary = GetStr(cap, "secondary", "");
                var tertiary = GetStr(cap, "tertiary", "");
                if (!string.IsNullOrEmpty(primary))
                    summaryTable.AddRow(
                        new Markup("[dim]Приоритеты развития[/]"),
                        new Markup(string.Empty),
                        new Markup(string.Empty),
                        new Markup($"[bold]{Markup.Escape(primary)}[/] > [yellow]{Markup.Escape(secondary)}[/] > [dim]{Markup.Escape(tertiary)}[/]"));
            }
        }

        // Reputation with label
        var factionTier = ReputationDisplay.GetTier(ReputationScaleKind.Faction, rep);
        summaryTable.AddRow(
            new Markup($"[{factionTier.Color}]Репутация[/]"),
            new Markup(ReputationDisplay.BuildBarMarkup(rep, ReputationScaleKind.Faction, 18)),
            new Markup($"[{factionTier.Color}]{rep}[/]"),
            new Markup($"[{factionTier.Color}]{Markup.Escape(factionTier.Label)}[/]"));
        if (!string.IsNullOrEmpty(repDesc))
            summaryTable.AddRow(
                new Markup("[dim]Пояснение[/]"),
                new Markup(string.Empty),
                new Markup(string.Empty),
                new Markup($"[dim]{Markup.Escape(repDesc)}[/]"));

        // Membership
        if (isPlayerFaction)
            summaryTable.AddRow(
                new Markup("[gold1]Статус игрока[/]"),
                new Markup(string.Empty),
                new Markup("[bold gold1]Лидер[/]"),
                new Markup("[bold gold1]Вы — лидер этой фракции[/]"));
        else if (isMember)
        {
            var memberDetails = new List<string>();
            if (!string.IsNullOrEmpty(playerRank))
                memberDetails.Add($"Ранг: [yellow]{Markup.Escape(playerRank)}[/]");
            if (!string.IsNullOrEmpty(playerBranch))
                memberDetails.Add($"[dim]({Markup.Escape(ResolveFactionBranchDisplayName(f, strDoc, name, factionId, playerBranch))})[/]");
            summaryTable.AddRow(
                new Markup("[green]Статус игрока[/]"),
                new Markup(string.Empty),
                new Markup("[green]Член[/]"),
                new Markup(string.Join(" | ", memberDetails)));
        }

        // Strategy directive
        var directive = GetStr(f, "playerStrategyDirective", "");
        if (!string.IsNullOrEmpty(directive))
        {
            summaryTable.AddRow(
                new Markup("[cyan]Стратегическая директива[/]"),
                new Markup(string.Empty),
                new Markup(string.Empty),
                new Markup($"[italic cyan]{Markup.Escape(directive)}[/]"));
        }
        else if (isPlayerFaction)
        {
            summaryTable.AddRow(
                new Markup("[dim]Стратегическая директива[/]"),
                new Markup(string.Empty),
                new Markup(string.Empty),
                new Markup("[dim italic]не задана (используйте /директива_фракции)[/]"));
        }

        if (summaryTable.Rows.Count > 0)
            content.AddRow(summaryTable);

        // ═══ Power Profile ═══
        if (f.TryGetProperty("powerProfile", out var pp) && pp.ValueKind == JsonValueKind.Object)
        {
            var powerNames = new Dictionary<string, string>
            {
                ["military"] = "⚔ Военная",
                ["economic"] = "💰 Экономика",
                ["social"] = "💬 Социальная",
                ["covert"] = "🗡 Тайная",
                ["logistics"] = "📦 Логистика",
                ["stability"] = "🛡 Стабильность",
                ["arcane_tech"] = "✨ Магия/Тех",
                ["exploration"] = "🔍 Исследование"
            };
            var powerTable = ConsoleLayout.CreateBarMetricTable(labelWidth: 18, barWidth: 10, valueWidth: 5);
            foreach (var (key, label) in powerNames)
            {
                if (pp.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var val))
                {
                    var tier = GetPowerTierLabel(val);
                    powerTable.AddRow(
                        GameInterface.SafeMarkupText(label),
                        new Markup(PowerBar(val)),
                        new Markup($"[white]{val}[/]"),
                        new Markup($"[dim]{Markup.Escape(tier)}[/]"));
                }
            }

            if (powerTable.Rows.Count > 0)
            {
                content.AddRow(new Markup(""));
                content.AddRow(new Markup("  [bold]📊 Профиль силы:[/]"));
                content.AddRow(powerTable);
            }
        }

        // ═══ Resources ═══
        RenderFactionResources(lines, f, resDoc, name, factionId);

        // ═══ Controlled Territories ═══
        if (f.TryGetProperty("controlledTerritories", out var terr) && terr.ValueKind == JsonValueKind.Array && terr.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]🗺 Территории:[/]");
            foreach (var t in terr.EnumerateArray())
            {
                var locName = GetFactionTerritoryDisplayName(t);
                if (string.IsNullOrWhiteSpace(locName))
                    continue;

                lines.Add($"    📍 [cyan]{Markup.Escape(locName)}[/]");
            }
        }

        // ═══ Relations ═══
        if (f.TryGetProperty("relations", out var rels) && rels.ValueKind == JsonValueKind.Array && rels.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]🤝 Отношения:[/]");
            foreach (var rel in rels.EnumerateArray())
            {
                var targetFactionId = GetStr(rel, "targetFactionId", "");
                var target = !string.IsNullOrWhiteSpace(targetFactionId) &&
                             factionNamesById.TryGetValue(targetFactionId, out var targetFactionName)
                    ? targetFactionName
                    : GetStr(rel, "targetFactionName", targetFactionId);
                var status = GetStr(rel, "status", "Neutral");
                var relDesc = GetStr(rel, "description", "");
                var (relIcon, relColor) = status.ToLowerInvariant() switch
                {
                    "allied" => ("🤝", "green"),
                    "patron" => ("👑", "green"),
                    "vassal" => ("🔗", "yellow"),
                    "war" => ("⚔", "bold red"),
                    "rivalry" => ("💢", "red"),
                    _ => ("↔", "grey")
                };
                var line = $"    {relIcon} [{relColor}]{Markup.Escape(status)}[/] → [white]{Markup.Escape(target)}[/]";
                if (!string.IsNullOrEmpty(relDesc))
                    line += $" — [dim]{Markup.Escape(relDesc)}[/]";
                lines.Add(line);
            }
        }

        // ═══ Active Projects ═══
        RenderFactionProjects(lines, f, projDoc, name, factionId);

        // ═══ Structured Bonuses ═══
        RenderFactionStructuredBonuses(lines, f, strDoc, name, factionId);

        // ═══ Custom States ═══
        RenderFactionCustomStates(lines, f, custDoc, name, factionId);

        // ═══ Rank Hierarchy ═══
        RenderFactionRanks(lines, f, strDoc, name, factionId, playerRank);

        // ═══ Chronicles ═══
        RenderFactionChronicles(lines, f, chrDoc, name, factionId);

        if (lines.Count > 0)
            content.AddRow(GameInterface.SafeMarkup(string.Join("\n", lines)));

        Write(new Panel(content)
        {
            Header = new PanelHeader($" 🏛️ {Markup.Escape(name)} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Orange1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        ShowFactionDetailSectionMenu(name, BuildFactionDetailSections(f, projDoc, strDoc, chrDoc, resDoc, name, factionId, playerRank));
        await WaitForKeyWithImage("faction", name, GetStr(f, "image_prompt", ""), GetStr(f, "factionId", name));
    }

    private void ShowFactionDetailSectionMenu(string factionName, IReadOnlyList<FactionDetailSection> sections)
    {
        if (sections.Count == 0)
            return;

        while (true)
        {
            var choices = sections
                .Select(section => (Section: (FactionDetailSection?)section, Choice: GameInterface.SafePromptChoice(section.ChoiceLabel)))
                .ToList();
            choices.Add((null, "← Закрыть разделы фракции"));

            var selected = Prompt(new SelectionPrompt<string>()
                .Title($"[bold orange1]Разделы фракции: {GameInterface.EscapeMarkup(factionName)}[/]")
                .PageSize(12)
                .HighlightStyle(new Style(Color.Orange1))
                .AddChoices(choices.Select(static choice => choice.Choice)));

            if (selected.Contains("←", StringComparison.Ordinal) ||
                selected.Contains("Назад", StringComparison.OrdinalIgnoreCase) ||
                selected.Contains("Закрыть", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var section = choices.FirstOrDefault(choice => choice.Choice == selected).Section;
            if (section == null)
                return;

            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", section.Lines), section.Title))
            {
                Header = GameInterface.SafePanelHeader($"{section.Title}: {factionName}"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Orange1),
                Padding = new Padding(1, 1),
                Expand = true
            });
            WaitForKey();
        }
    }

    private static IReadOnlyList<FactionDetailSection> BuildFactionDetailSections(
        JsonElement faction,
        JsonDocument? projDoc,
        JsonDocument? strDoc,
        JsonDocument? chrDoc,
        JsonDocument? resDoc,
        string factionName,
        string factionId,
        string currentPlayerRank) =>
    [
        BuildFactionResourcesSection(faction, resDoc, factionName, factionId),
        BuildFactionChroniclesSection(faction, chrDoc, factionName, factionId),
        BuildFactionRanksSection(faction, strDoc, factionName, factionId, currentPlayerRank),
        BuildFactionProjectsSection(faction, projDoc, factionName, factionId),
        BuildFactionStrategySection(faction),
        BuildFactionTerritorySection(faction)
    ];

    private static FactionDetailSection BuildFactionResourcesSection(
        JsonElement faction,
        JsonDocument? resDoc,
        string factionName,
        string factionId)
    {
        var lines = new List<string> { "[bold]💰 Ресурсы и экономика[/]" };
        var resourceCount = RenderFactionResourceDetailLines(lines, faction, resDoc, factionName, factionId);
        var ledgerCount = RenderFactionResourceLedgerLines(lines, faction, resDoc, factionName, factionId);

        if (resourceCount == 0 && ledgerCount == 0)
            lines.Add("[dim]Открытые сведения о ресурсах этой фракции пока не внесены.[/]");

        return CreateFactionDetailSection(
            "resources",
            "💰",
            "Ресурсы и экономика",
            resourceCount > 0 ? FormatRussianCount(resourceCount, "ресурс", "ресурса", "ресурсов") : "нет данных",
            lines);
    }

    private static FactionDetailSection BuildFactionChroniclesSection(
        JsonElement faction,
        JsonDocument? chrDoc,
        string factionName,
        string factionId)
    {
        var entries = CollectFactionChronicleEntries(faction, chrDoc, factionName, factionId);
        var lines = new List<string> { "[bold]📜 Хроники[/]" };
        if (entries.Count == 0)
        {
            lines.Add("[dim]Открытых хроник этой фракции пока нет.[/]");
        }
        else
        {
            for (var index = 0; index < entries.Count; index++)
                lines.Add($"  {index + 1}. {Markup.Escape(entries[index])}");
        }

        return CreateFactionDetailSection(
            "chronicles",
            "📜",
            "Хроники",
            entries.Count > 0 ? FormatRussianCount(entries.Count, "запись", "записи", "записей") : "нет данных",
            lines);
    }

    private static FactionDetailSection BuildFactionRanksSection(
        JsonElement faction,
        JsonDocument? strDoc,
        string factionName,
        string factionId,
        string currentPlayerRank)
    {
        var lines = new List<string> { "[bold]👑 Ранги и иерархия[/]" };
        var before = lines.Count;
        RenderFactionRanks(lines, faction, strDoc, factionName, factionId, currentPlayerRank);
        if (lines.Count == before)
            lines.Add("[dim]Открытая иерархия этой фракции пока не описана.[/]");

        var branchCount = CountFactionRankBranches(faction, strDoc, factionName, factionId);
        return CreateFactionDetailSection(
            "ranks",
            "👑",
            "Ранги и иерархия",
            branchCount > 0 ? FormatRussianCount(branchCount, "ветвь", "ветви", "ветвей") : "нет данных",
            lines);
    }

    private static FactionDetailSection BuildFactionProjectsSection(
        JsonElement faction,
        JsonDocument? projDoc,
        string factionName,
        string factionId)
    {
        var lines = new List<string> { "[bold]🔨 Проекты и операции[/]" };
        var before = lines.Count;
        RenderFactionProjects(lines, faction, projDoc, factionName, factionId);
        if (lines.Count == before)
            lines.Add("[dim]Открытых проектов и операций этой фракции пока нет.[/]");

        var (activeProjects, completedProjects) = CollectFactionProjectEntries(faction, projDoc, factionName, factionId);
        var projectCount = activeProjects.Count + completedProjects.Count;
        return CreateFactionDetailSection(
            "projects",
            "🔨",
            "Проекты и операции",
            projectCount > 0 ? FormatRussianCount(projectCount, "проект", "проекта", "проектов") : "нет данных",
            lines);
    }

    private static FactionDetailSection BuildFactionStrategySection(JsonElement faction)
    {
        var lines = new List<string> { "[bold]🧭 Стратегия и память[/]" };
        var entryCount = 0;

        var directive = GetStr(faction, "playerStrategyDirective", "");
        if (!string.IsNullOrWhiteSpace(directive))
        {
            lines.Add($"  [cyan]Стратегическая директива:[/] {Markup.Escape(directive)}");
            entryCount++;
        }

        var archetype = TranslateFactionDevelopmentArchetypeForConsole(GetStr(faction, "developmentArchetype", ""));
        if (!string.IsNullOrWhiteSpace(archetype))
            lines.Add($"  [dim]Архетип развития:[/] {Markup.Escape(archetype)}");

        if (faction.TryGetProperty("customArchetypePriorities", out var priorities) &&
            priorities.ValueKind == JsonValueKind.Object)
        {
            var priorityParts = new[]
                {
                    GetStr(priorities, "primary", ""),
                    GetStr(priorities, "secondary", ""),
                    GetStr(priorities, "tertiary", "")
                }
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            if (priorityParts.Length > 0)
                lines.Add($"  [dim]Приоритеты:[/] {Markup.Escape(string.Join(" > ", priorityParts))}");
        }

        if (faction.TryGetProperty("powerProfile", out var powerProfile) &&
            powerProfile.ValueKind == JsonValueKind.Object)
        {
            lines.Add("");
            lines.Add("  [bold]Профиль силы:[/]");
            foreach (var power in powerProfile.EnumerateObject())
            {
                if (power.Value.ValueKind != JsonValueKind.Number || !power.Value.TryGetInt32(out var value))
                    continue;

                lines.Add($"    • {Markup.Escape(TranslateFactionPowerKeyForConsole(power.Name))}: {value} ({Markup.Escape(GetPowerTierLabel(value))})");
            }
        }

        var memoryLines = CollectFactionStrategicMemoryLines(faction);
        if (memoryLines.Count > 0)
        {
            lines.Add("");
            lines.Add("  [bold]Открытая память стратегии:[/]");
            foreach (var memory in memoryLines)
                lines.Add($"    • {Markup.Escape(memory)}");
            entryCount += memoryLines.Count;
        }

        if (entryCount == 0 && lines.Count == 1)
            lines.Add("[dim]Открытая стратегия этой фракции пока не описана.[/]");

        return CreateFactionDetailSection(
            "strategy",
            "🧭",
            "Стратегия и память",
            entryCount > 0 ? FormatRussianCount(entryCount, "запись", "записи", "записей") : "нет данных",
            lines);
    }

    private static FactionDetailSection BuildFactionTerritorySection(JsonElement faction)
    {
        var lines = new List<string> { "[bold]🗺 Территории и влияние[/]" };
        var territoryCount = 0;

        if (faction.TryGetProperty("controlledTerritories", out var territories) &&
            territories.ValueKind == JsonValueKind.Array)
        {
            foreach (var territory in territories.EnumerateArray())
            {
                if (!IsFactionKnowledgeEntryVisible(territory))
                    continue;

                var name = GetFactionTerritoryDisplayName(territory);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                territoryCount++;
                var details = new List<string>();
                var summary = string.Empty;
                if (territory.ValueKind == JsonValueKind.Object)
                {
                    AddDetailPart(details, "влияние", GetStr(territory, "influence", ""));
                    AddDetailPart(details, "контроль", TranslateFactionTerritoryControl(GetStr(territory, "controlLevel", GetStr(territory, "status", ""))));
                    summary = GetStr(territory, "summary", GetStr(territory, "description", ""));
                }

                var line = $"  • [white]{Markup.Escape(name)}[/]";
                if (details.Count > 0)
                    line += $" [dim]({Markup.Escape(string.Join(", ", details))})[/]";
                lines.Add(line);
                if (!string.IsNullOrWhiteSpace(summary))
                    lines.Add($"    [dim]{Markup.Escape(summary)}[/]");
            }
        }

        var influenceLines = CollectFactionInfluenceLines(faction);
        if (influenceLines.Count > 0)
        {
            lines.Add("");
            lines.Add("  [bold]Открытая летопись влияния:[/]");
            foreach (var influence in influenceLines)
                lines.Add($"    • {Markup.Escape(influence)}");
        }

        if (territoryCount == 0 && influenceLines.Count == 0)
            lines.Add("[dim]Открытые сведения о территориях и влиянии этой фракции пока не внесены.[/]");

        return CreateFactionDetailSection(
            "territory",
            "🗺",
            "Территории и влияние",
            territoryCount > 0 ? FormatRussianCount(territoryCount, "территория", "территории", "территорий") : "нет данных",
            lines);
    }

    private static string GetFactionTerritoryDisplayName(JsonElement territory)
    {
        if (territory.ValueKind == JsonValueKind.String)
        {
            var id = territory.GetString();
            return string.IsNullOrWhiteSpace(id) ? string.Empty : $"Локация {id}";
        }

        if (territory.ValueKind != JsonValueKind.Object)
            return string.Empty;

        return FirstNonEmpty(
            GetStr(territory, "locationName", ""),
            GetStr(territory, "displayName", ""),
            GetStr(territory, "name", ""),
            GetStr(territory, "region", ""),
            PrefixIfPresent("Локация ", GetStr(territory, "locationId", "")));
    }

    private static string PrefixIfPresent(string prefix, string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : prefix + value;

    private static FactionDetailSection CreateFactionDetailSection(
        string id,
        string icon,
        string title,
        string summary,
        IReadOnlyList<string> lines) =>
        new(id, title, $"{icon} {title} — {summary}", lines);

    private sealed class FactionDetailSection
    {
        public FactionDetailSection(string id, string title, string choiceLabel, IReadOnlyList<string> lines)
        {
            Id = id;
            Title = title;
            ChoiceLabel = choiceLabel;
            Lines = lines;
        }

        public string Id { get; }
        public string Title { get; }
        public string ChoiceLabel { get; }
        public IReadOnlyList<string> Lines { get; }
    }

    // ═══════════════════════════════════════════════════════════
    //  Faction Detail Sub-Renderers
    // ═══════════════════════════════════════════════════════════

    private static bool FactionSidecarMatches(JsonElement item, string factionName, string factionId)
    {
        var itemFactionId = GetStr(item, "factionId", "");
        return !string.IsNullOrWhiteSpace(factionId) &&
               !string.IsNullOrWhiteSpace(itemFactionId) &&
               itemFactionId.Equals(factionId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFactionPlayerVisible(JsonElement item) => IsFactionKnowledgeEntryVisible(item);

    private static bool IsFactionKnowledgeEntryVisible(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return true;

        if (IsFalseFlag(item, "isPlayerVisible") ||
            IsFalseFlag(item, "playerVisible") ||
            IsFalseFlag(item, "visibleToPlayer") ||
            IsTrueFlag(item, "hidden") ||
            IsTrueFlag(item, "gmOnly") ||
            IsTrueFlag(item, "isGmOnly"))
        {
            return false;
        }

        var visibility = GetStr(item, "visibility", "");
        return !IsHiddenFactionVisibility(visibility);
    }

    private static bool IsFalseFlag(JsonElement item, string propertyName) =>
        TryGetFactionBool(item, propertyName, out var value) && !value;

    private static bool IsTrueFlag(JsonElement item, string propertyName) =>
        TryGetFactionBool(item, propertyName, out var value) && value;

    private static bool TryGetFactionBool(JsonElement item, string propertyName, out bool value)
    {
        value = false;
        if (!item.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
        {
            value = property.GetBoolean();
            return true;
        }

        if (property.ValueKind == JsonValueKind.String &&
            bool.TryParse(property.GetString(), out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static bool IsHiddenFactionVisibility(string visibility)
    {
        var normalized = visibility.Trim();
        return normalized.Equals("hidden", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("gm_only", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("private", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("secret", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("concealed", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("spoiler", StringComparison.OrdinalIgnoreCase);
    }

    private static int RenderFactionResourceDetailLines(
        List<string> lines,
        JsonElement faction,
        JsonDocument? resDoc,
        string factionName,
        string factionId)
    {
        var resourceCount = 0;
        var sidecarAvailable = resDoc != null;
        var sidecarMatched = false;

        if (resDoc != null)
        {
            EnumerateJsonItems(resDoc.RootElement, item =>
            {
                if (!FactionSidecarMatches(item, factionName, factionId) || !IsFactionKnowledgeEntryVisible(item))
                    return;

                sidecarMatched = true;
                resourceCount += RenderFactionResourceContainer(lines, item);
            });
        }

        if ((!sidecarAvailable || !sidecarMatched) &&
            faction.TryGetProperty("resources", out var resources) &&
            resources.ValueKind == JsonValueKind.Object)
        {
            resourceCount += RenderFactionResourceContainer(lines, resources);
        }

        if ((!sidecarAvailable || !sidecarMatched) && resourceCount == 0)
            resourceCount += RenderFactionResourceContainer(lines, faction);

        return resourceCount;
    }

    private static int RenderFactionResourceContainer(List<string> lines, JsonElement container)
    {
        var count = 0;
        if (container.TryGetProperty("metaResources", out var metaResources))
            count += RenderFactionResourceArray(lines, metaResources, "Основные ресурсы", "💎");
        if (container.TryGetProperty("strategicGoods", out var strategicGoods))
            count += RenderFactionResourceArray(lines, strategicGoods, "Стратегические запасы", "📦");
        return count;
    }

    private static int RenderFactionResourceArray(List<string> lines, JsonElement array, string label, string icon)
    {
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0)
            return 0;

        var count = 0;
        lines.Add("");
        lines.Add($"  [bold]{Markup.Escape(label)}:[/]");
        foreach (var resource in array.EnumerateArray())
        {
            if (!IsFactionKnowledgeEntryVisible(resource))
                continue;

            var resourceName = GetStr(resource, "resourceName", GetStr(resource, "displayName", GetStr(resource, "name", "")));
            if (string.IsNullOrWhiteSpace(resourceName))
                continue;

            count++;
            var stock = GetStr(resource, "currentStockpile", GetStr(resource, "currentStock", GetStr(resource, "stock", "")));
            var income = GetStr(resource, "incomePerCycle", GetStr(resource, "incomePerTurn", GetStr(resource, "income", "")));
            var upkeep = GetStr(resource, "upkeepPerCycle", GetStr(resource, "upkeepPerTurn", GetStr(resource, "upkeep", "")));

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(stock))
                parts.Add($"запас {stock}");
            if (!string.IsNullOrWhiteSpace(income) && income != "0")
                parts.Add($"+{income}/цикл");
            if (!string.IsNullOrWhiteSpace(upkeep) && upkeep != "0")
                parts.Add($"-{upkeep}/цикл");

            var suffix = parts.Count > 0 ? $" [dim]({Markup.Escape(string.Join(", ", parts))})[/]" : string.Empty;
            lines.Add($"    {icon} [white]{Markup.Escape(resourceName)}[/]{suffix}");
        }

        return count;
    }

    private static int RenderFactionResourceLedgerLines(
        List<string> lines,
        JsonElement faction,
        JsonDocument? resDoc,
        string factionName,
        string factionId)
    {
        var ledgerCount = 0;
        if (resDoc != null)
        {
            EnumerateJsonItems(resDoc.RootElement, item =>
            {
                if (!FactionSidecarMatches(item, factionName, factionId) || !IsFactionKnowledgeEntryVisible(item))
                    return;

                if (item.TryGetProperty("resourceLedger", out var ledger))
                    ledgerCount += RenderFactionLedgerArray(lines, ledger);
            });
        }

        if (ledgerCount == 0 && faction.TryGetProperty("resourceLedger", out var coreLedger))
            ledgerCount += RenderFactionLedgerArray(lines, coreLedger);

        return ledgerCount;
    }

    private static int RenderFactionLedgerArray(List<string> lines, JsonElement ledger)
    {
        if (ledger.ValueKind != JsonValueKind.Array || ledger.GetArrayLength() == 0)
            return 0;

        var count = 0;
        var ledgerLines = new List<string>();
        foreach (var entry in ledger.EnumerateArray())
        {
            if (!IsFactionKnowledgeEntryVisible(entry))
                continue;

            var title = GetStr(entry, "title", GetStr(entry, "summary", GetStr(entry, "description", "")));
            var resourceName = GetStr(entry, "resourceName", GetStr(entry, "resource", ""));
            var amount = GetStr(entry, "amount", GetStr(entry, "delta", ""));
            var balance = GetStr(entry, "balanceAfter", GetStr(entry, "stockAfter", ""));
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(resourceName))
                continue;

            count++;
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(resourceName))
                parts.Add(resourceName);
            if (!string.IsNullOrWhiteSpace(amount))
                parts.Add(amount);
            if (!string.IsNullOrWhiteSpace(balance))
                parts.Add($"остаток {balance}");

            var line = $"    • {Markup.Escape(FirstNonEmpty(title, resourceName))}";
            if (parts.Count > 0)
                line += $" [dim]({Markup.Escape(string.Join(", ", parts))})[/]";
            ledgerLines.Add(line);
        }

        if (ledgerLines.Count == 0)
            return 0;

        lines.Add("");
        lines.Add("  [bold]Открытый журнал ресурсов:[/]");
        lines.AddRange(ledgerLines);
        return count;
    }

    private static List<string> CollectFactionChronicleEntries(
        JsonElement faction,
        JsonDocument? chrDoc,
        string factionName,
        string factionId)
    {
        var entries = new List<string>();

        if (faction.TryGetProperty("scribeChronicle", out var coreChronicle) &&
            coreChronicle.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in coreChronicle.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object && !IsFactionKnowledgeEntryVisible(entry))
                    continue;

                var text = entry.ValueKind == JsonValueKind.String
                    ? entry.GetString() ?? string.Empty
                    : BuildFactionKnowledgeEntryLine(entry);
                if (!string.IsNullOrWhiteSpace(text) && !entries.Contains(text, StringComparer.Ordinal))
                    entries.Add(text);
            }
        }

        if (chrDoc != null)
        {
            EnumerateJsonItems(chrDoc.RootElement, item =>
            {
                if (!FactionSidecarMatches(item, factionName, factionId) || !IsFactionKnowledgeEntryVisible(item))
                    return;

                var entry = BuildFactionKnowledgeEntryLine(item);
                if (!string.IsNullOrWhiteSpace(entry) && !entries.Contains(entry, StringComparer.Ordinal))
                    entries.Add(entry);
            });
        }

        return entries;
    }

    private static (List<JsonElement> Active, List<JsonElement> Completed) CollectFactionProjectEntries(
        JsonElement faction,
        JsonDocument? projDoc,
        string factionName,
        string factionId)
    {
        var activeProjects = new List<JsonElement>();
        var completedProjects = new List<JsonElement>();
        var sidecarAvailable = projDoc != null;
        var sidecarMatched = false;

        if (projDoc != null)
        {
            EnumerateJsonItems(projDoc.RootElement, item =>
            {
                if (!FactionSidecarMatches(item, factionName, factionId) || !IsFactionKnowledgeEntryVisible(item))
                    return;

                sidecarMatched = true;
                if (item.TryGetProperty("finalState", out _) || item.TryGetProperty("completionTurn", out _))
                    completedProjects.Add(item);
                else
                    activeProjects.Add(item);
            });
        }

        if (!sidecarAvailable || !sidecarMatched)
        {
            if (faction.TryGetProperty("activeProjects", out var active) && active.ValueKind == JsonValueKind.Array)
                foreach (var project in active.EnumerateArray())
                    if (IsFactionKnowledgeEntryVisible(project))
                        activeProjects.Add(project);
            if (faction.TryGetProperty("completedProjects", out var completed) && completed.ValueKind == JsonValueKind.Array)
                foreach (var project in completed.EnumerateArray())
                    if (IsFactionKnowledgeEntryVisible(project))
                        completedProjects.Add(project);
        }

        return (activeProjects, completedProjects);
    }

    private static JsonElement? ResolveFactionRanksElement(
        JsonElement faction,
        JsonDocument? structureDoc,
        string factionName,
        string factionId)
    {
        JsonElement? ranksEl = null;
        var sidecarAvailable = structureDoc != null;
        var sidecarMatched = false;
        if (structureDoc != null)
        {
            EnumerateJsonItems(structureDoc.RootElement, item =>
            {
                if (FactionSidecarMatches(item, factionName, factionId) &&
                    item.TryGetProperty("ranks", out var sidecarRanks))
                {
                    sidecarMatched = true;
                    ranksEl = sidecarRanks;
                }
            });
        }

        if ((!sidecarAvailable || !sidecarMatched) &&
            ranksEl == null &&
            faction.TryGetProperty("ranks", out var ranks))
        {
            ranksEl = ranks;
        }

        return ranksEl;
    }

    private static int CountFactionRankBranches(
        JsonElement faction,
        JsonDocument? structureDoc,
        string factionName,
        string factionId)
    {
        var ranksEl = ResolveFactionRanksElement(faction, structureDoc, factionName, factionId);
        if (ranksEl == null)
            return 0;

        if (ranksEl.Value.ValueKind == JsonValueKind.Object &&
            ranksEl.Value.TryGetProperty("branches", out var branches) &&
            branches.ValueKind == JsonValueKind.Array)
        {
            return branches.GetArrayLength();
        }

        if (ranksEl.Value.ValueKind == JsonValueKind.Array && ranksEl.Value.GetArrayLength() > 0)
            return 1;

        return 0;
    }

    private static List<string> CollectFactionStrategicMemoryLines(JsonElement faction)
    {
        var lines = new List<string>();
        if (!faction.TryGetProperty("strategicMemory", out var memory))
            return lines;

        CollectFactionKnowledgeLines(memory, lines);
        return lines;
    }

    private static List<string> CollectFactionInfluenceLines(JsonElement faction)
    {
        var lines = new List<string>();
        foreach (var propertyName in new[] { "territorialInfluence", "influenceLedger", "influenceLog" })
        {
            if (faction.TryGetProperty(propertyName, out var node))
                CollectFactionKnowledgeLines(node, lines);
        }

        return lines;
    }

    private static void CollectFactionKnowledgeLines(JsonElement node, List<string> lines)
    {
        if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in node.EnumerateArray())
            {
                if (!IsFactionKnowledgeEntryVisible(item))
                    continue;

                var entryLine = item.ValueKind == JsonValueKind.String
                    ? item.GetString() ?? string.Empty
                    : BuildFactionKnowledgeEntryLine(item);
                if (!string.IsNullOrWhiteSpace(entryLine))
                    lines.Add(entryLine);
            }
            return;
        }

        if (node.ValueKind != JsonValueKind.Object || !IsFactionKnowledgeEntryVisible(node))
            return;

        foreach (var propertyName in new[] { "entries", "memories", "records", "notes" })
        {
            if (node.TryGetProperty(propertyName, out var nested) && nested.ValueKind == JsonValueKind.Array)
            {
                CollectFactionKnowledgeLines(nested, lines);
                return;
            }
        }

        var line = BuildFactionKnowledgeEntryLine(node);
        if (!string.IsNullOrWhiteSpace(line))
            lines.Add(line);
    }

    private static string BuildFactionKnowledgeEntryLine(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.String)
            return item.GetString() ?? string.Empty;

        if (item.ValueKind != JsonValueKind.Object)
            return string.Empty;

        var title = GetStr(item, "title", GetStr(item, "name", ""));
        var summary = FirstNonEmpty(
            GetStr(item, "playerVisibleText", ""),
            GetStr(item, "summary", ""),
            GetStr(item, "entry", ""),
            GetStr(item, "chronicle", ""),
            GetStr(item, "text", ""),
            GetStr(item, "description", ""),
            GetStr(item, "memory", ""),
            GetStr(item, "note", ""));
        var turn = GetStr(item, "turn", GetStr(item, "turnNumber", ""));

        var prefix = string.IsNullOrWhiteSpace(turn) ? string.Empty : $"ход {turn}: ";
        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(summary))
            return $"{prefix}{title} — {summary}";
        if (!string.IsNullOrWhiteSpace(summary))
            return $"{prefix}{summary}";
        if (!string.IsNullOrWhiteSpace(title))
            return $"{prefix}{title}";

        return string.Empty;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static void AddDetailPart(List<string> parts, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add($"{label}: {value}");
    }

    private static string FormatRussianCount(int count, string one, string few, string many)
    {
        var mod100 = Math.Abs(count) % 100;
        var mod10 = Math.Abs(count) % 10;
        var word = mod100 is >= 11 and <= 14
            ? many
            : mod10 switch
            {
                1 => one,
                >= 2 and <= 4 => few,
                _ => many
            };
        return $"{count} {word}";
    }

    private static string TranslateFactionDevelopmentArchetypeForConsole(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "economic" => "экономическое развитие",
            "military" => "военное развитие",
            "social" => "социальное влияние",
            "covert" => "скрытое влияние",
            "arcane" or "arcane_tech" => "магическое развитие",
            "exploration" => "исследования",
            "scribe_household" => "дом переписчика",
            _ => value.Trim()
        };

    private static string TranslateFactionPowerKeyForConsole(string key) =>
        key switch
        {
            "military" => "Военная сила",
            "economic" => "Экономика",
            "social" => "Социальное влияние",
            "covert" => "Скрытые операции",
            "logistics" => "Логистика",
            "stability" => "Устойчивость",
            "arcane" or "arcaneTech" or "arcane_tech" => "Магия и техника",
            "exploration" => "Разведка",
            _ => key
        };

    private static string TranslateFactionTerritoryControl(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "strong" => "прочный",
            "contested" => "оспаривается",
            "weak" => "слабый",
            "lost" => "утрачен",
            "secured" => "закреплён",
            _ => value.Trim()
        };

    private static string ResolveFactionBranchDisplayName(JsonElement faction, JsonDocument? structureDoc,
        string factionName, string factionId, string branchId)
    {
        if (string.IsNullOrWhiteSpace(branchId))
            return branchId;

        JsonElement? ranksEl = null;

        if (structureDoc != null)
        {
            EnumerateJsonItems(structureDoc.RootElement, item =>
            {
                if (ranksEl != null)
                    return;

                if (FactionSidecarMatches(item, factionName, factionId) &&
                    item.TryGetProperty("ranks", out var sidecarRanks) &&
                    sidecarRanks.ValueKind == JsonValueKind.Object)
                {
                    ranksEl = sidecarRanks;
                }
            });
        }

        if (ranksEl == null &&
            faction.TryGetProperty("ranks", out var coreRanks) &&
            coreRanks.ValueKind == JsonValueKind.Object)
        {
            ranksEl = coreRanks;
        }

        if (ranksEl.HasValue &&
            ranksEl.Value.TryGetProperty("branches", out var branches) &&
            branches.ValueKind == JsonValueKind.Array)
        {
            foreach (var branch in branches.EnumerateArray())
            {
                var candidateId = GetStr(branch, "branchId", "");
                if (candidateId.Equals(branchId, StringComparison.OrdinalIgnoreCase))
                    return GetStr(branch, "displayName", candidateId);
            }
        }

        return branchId;
    }

	    private static void RenderFactionStructuredBonuses(List<string> lines, JsonElement f,
	        JsonDocument? strDoc, string factionName, string factionId)
	    {
	        JsonElement? bonusesEl = null;
            var sidecarAvailable = strDoc != null;
            var sidecarMatched = false;

	        if (strDoc != null)
	        {
	            EnumerateJsonItems(strDoc.RootElement, item =>
	            {
	                if (FactionSidecarMatches(item, factionName, factionId) &&
	                    item.TryGetProperty("structuredBonuses", out var sidecarBonuses) &&
	                    sidecarBonuses.ValueKind == JsonValueKind.Array)
	                {
                        sidecarMatched = true;
	                    bonusesEl = sidecarBonuses;
	                }
	            });
	        }

	        if ((!sidecarAvailable || !sidecarMatched) &&
	            bonusesEl == null &&
	            f.TryGetProperty("structuredBonuses", out var bonuses) &&
	            bonuses.ValueKind == JsonValueKind.Array &&
	            bonuses.GetArrayLength() > 0)
	        {
	            bonusesEl = bonuses;
	        }

	        if (bonusesEl == null || bonusesEl.Value.ValueKind != JsonValueKind.Array || bonusesEl.Value.GetArrayLength() == 0)
	            return;

        lines.Add("");
        lines.Add("  [bold]✨ Бонусы:[/]");
        foreach (var b in bonusesEl.Value.EnumerateArray())
        {
            var bDesc = GetStr(b, "description", "");
            var bType = GetStr(b, "bonusType", "");
            var bTarget = GetStr(b, "target", "");
            var bValueType = GetStr(b, "valueType", "");
            var bVal = GetStr(b, "value", "0");
            var bApp = GetStr(b, "application", "");
            var bCond = GetStr(b, "condition", "");

            var line = $"    ✦ [cyan]{Markup.Escape(bDesc)}[/]";
            if (string.IsNullOrEmpty(bDesc))
                line = $"    ✦ [cyan]{Markup.Escape(bType)}: {Markup.Escape(bTarget)} +{Markup.Escape(bVal)}[/]";
            if (!string.IsNullOrEmpty(bValueType))
                line += $" [dim]{Markup.Escape($"[{bValueType}]")}[/]";
            if (!string.IsNullOrEmpty(bApp) && bApp.ToLowerInvariant() == "conditional" && !string.IsNullOrEmpty(bCond))
                line += $" [dim](если: {Markup.Escape(bCond)})[/]";
            lines.Add(line);
        }
    }

	    private static void RenderFactionResources(List<string> lines, JsonElement f,
	        JsonDocument? resDoc, string factionName, string factionId)
	    {
	        var hasResources = false;
            var sidecarAvailable = resDoc != null;
            var sidecarMatched = false;

	        void RenderResourceArray(JsonElement arr, string label, string icon)
	        {
	            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return;
            if (!hasResources) { lines.Add(""); lines.Add("  [bold]💰 Ресурсы:[/]"); hasResources = true; }
            lines.Add($"    [dim]{label}:[/]");
            foreach (var r in arr.EnumerateArray())
            {
                var rName = GetStr(r, "resourceName", "?");
                var stock = GetStr(r, "currentStockpile", "0");
                var income = GetStr(r, "incomePerCycle", "");
                var upkeep = GetStr(r, "upkeepPerCycle", "");

                var line = $"      {icon} [white]{Markup.Escape(rName)}[/]: [cyan]{Markup.Escape(stock)}[/]";
                if (!string.IsNullOrEmpty(income) && income != "0")
                    line += $" [green](+{Markup.Escape(income)}/цикл)[/]";
                if (!string.IsNullOrEmpty(upkeep) && upkeep != "0")
                    line += $" [red](-{Markup.Escape(upkeep)}/цикл)[/]";
                lines.Add(line);
            }
        }

	        if (resDoc != null)
	        {
	            EnumerateJsonItems(resDoc.RootElement, item =>
	            {
	                if (!FactionSidecarMatches(item, factionName, factionId)) return;
                    sidecarMatched = true;
	                if (item.TryGetProperty("metaResources", out var mr2))
	                    RenderResourceArray(mr2, "Основные", "💎");
	                if (item.TryGetProperty("strategicGoods", out var sg2))
	                    RenderResourceArray(sg2, "Стратегические товары", "📦");
	            });
	        }

	        if ((!sidecarAvailable || !sidecarMatched) &&
	            f.TryGetProperty("resources", out var res) &&
	            res.ValueKind == JsonValueKind.Object)
	        {
	            if (res.TryGetProperty("metaResources", out var mr))
	                RenderResourceArray(mr, "Основные", "💎");
	            if (res.TryGetProperty("strategicGoods", out var sg))
	                RenderResourceArray(sg, "Стратегические товары", "📦");
	        }
	    }

	    private static void RenderFactionProjects(List<string> lines, JsonElement f,
	        JsonDocument? projDoc, string factionName, string factionId)
	    {
	        var (activeProjects, completedProjects) = CollectFactionProjectEntries(f, projDoc, factionName, factionId);

        if (activeProjects.Count == 0 && completedProjects.Count == 0) return;

        if (activeProjects.Count > 0)
        {
            lines.Add("");
            lines.Add("  [bold]🔨 Активные проекты:[/]");
            foreach (var p in activeProjects)
            {
                var pName = GetStr(p, "projectName", GetStr(p, "name", "?"));
                var pState = GetStr(p, "activeState", "");
                var pDesc = GetStr(p, "description", "");
                var step = GetInt(p, "currentStep", 0);
                var totalSteps = GetInt(p, "totalSteps", 0);
                var timeSpent = GetInt(p, "timeSpentMinutes", 0);
                var timeTotal = GetInt(p, "totalTimeCostMinutes", 0);

                var stateColor = pState.ToLowerInvariant() switch
                {
                    "completed" => "green",
                    "abandoned" => "red",
                    _ => "yellow"
                };
                lines.Add($"    🔨 [white]{Markup.Escape(pName)}[/] [{stateColor}]({Markup.Escape(pState)})[/]");

                if (!string.IsNullOrEmpty(pDesc))
                    lines.Add($"      [dim italic]{Markup.Escape(pDesc)}[/]");

                if (totalSteps > 0)
                {
                    var stepPct = Math.Min(100, step * 100 / totalSteps);
                    lines.Add($"      Этапы выполнения: {ConsoleLayout.CreateBarFromPercent(stepPct, 10, "cyan")} {step}/{totalSteps}");
                }

                if (timeTotal > 0)
                {
                    var timePct = Math.Min(100, timeSpent * 100 / timeTotal);
                    var barColor = timePct >= 80 ? "green" : timePct >= 50 ? "yellow" : "cyan";
                    lines.Add($"      Время выполнения: {ConsoleLayout.CreateBarFromPercent(timePct, 10, barColor)} {FormatMinutes(timeSpent)}/{FormatMinutes(timeTotal)}");
                }

                if (p.TryGetProperty("totalResourceCost", out var rc))
                {
                    var costs = new List<string>();
                    if (rc.ValueKind == JsonValueKind.Array)
                        foreach (var c in rc.EnumerateArray())
                            costs.Add($"{Markup.Escape(GetStr(c, "resourceName", "?"))}: {GetStr(c, "totalAmount", "?")}");
                    else if (rc.ValueKind == JsonValueKind.Object)
                        foreach (var c in rc.EnumerateObject())
                            costs.Add($"{Markup.Escape(c.Name)}: {c.Value}");
                    if (costs.Count > 0)
                    {
                        var spentParts = new List<string>();
                        if (p.TryGetProperty("resourcesSpent", out var rs))
                        {
                            if (rs.ValueKind == JsonValueKind.Array)
                                foreach (var c in rs.EnumerateArray())
                                    spentParts.Add($"{Markup.Escape(GetStr(c, "resourceName", "?"))}: {GetStr(c, "amountSpent", "?")}");
                            else if (rs.ValueKind == JsonValueKind.Object)
                                foreach (var c in rs.EnumerateObject())
                                    spentParts.Add($"{Markup.Escape(c.Name)}: {c.Value}");
                        }
                        var spentStr = spentParts.Count > 0 ? $" [dim](потрачено: {string.Join(", ", spentParts)})[/]" : "";
                        lines.Add($"      💰 Стоимость: {string.Join(", ", costs)}{spentStr}");
                    }
                }
            }
        }

        if (completedProjects.Count > 0)
        {
            lines.Add("    [dim]─── Завершённые: ───[/]");
            foreach (var p in completedProjects)
            {
                var pName = GetStr(p, "projectName", GetStr(p, "name", "?"));
                var finalState = GetStr(p, "finalState", "");
                var turn = GetStr(p, "completionTurn", "");
                var stColor = finalState.ToLowerInvariant() == "abandoned" ? "red" : "green";
                var line = $"    ✓ [dim]{Markup.Escape(pName)}[/] [{stColor}]{Markup.Escape(finalState)}[/]";
                if (!string.IsNullOrEmpty(turn)) line += $" [dim](ход {Markup.Escape(turn)})[/]";
                lines.Add(line);
            }
        }
    }

    private static void RenderFactionCustomStates(List<string> lines,
        JsonElement f, JsonDocument? custDoc, string factionName, string factionId)
    {
        // Collect state items for this faction (supports both flat and nested formats per Rule 21.F.1)
        var stateItems = new List<JsonElement>();
        if (custDoc != null)
        {
            EnumerateJsonItems(custDoc.RootElement, item =>
            {
                if (!FactionSidecarMatches(item, factionName, factionId)) return;

                // Nested: statesToAddOrUpdate array (Rule 21.F.1)
                if (item.TryGetProperty("statesToAddOrUpdate", out var nested) && nested.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in nested.EnumerateArray()) stateItems.Add(s);
                }
                else if (item.TryGetProperty("customStates", out var customStates) && customStates.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in customStates.EnumerateArray()) stateItems.Add(s);
                }
                else if (item.TryGetProperty("stateName", out _) || item.TryGetProperty("currentValue", out _))
                {
                    // Flat format: entry itself is a state
                    stateItems.Add(item);
                }
            });
        }

        if (stateItems.Count == 0 &&
            f.TryGetProperty("customStates", out var coreStates) &&
            coreStates.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in coreStates.EnumerateArray())
                stateItems.Add(s);
        }

        if (stateItems.Count == 0) return;

        lines.Add("");
        lines.Add("  [bold magenta]📊 Особые состояния:[/]");
        foreach (var s in stateItems)
            RenderCustomStateItem(lines, s, "    ");
    }

	    private static void RenderFactionRanks(List<string> lines, JsonElement f,
	        JsonDocument? strDoc, string factionName, string factionId, string currentPlayerRank)
	    {
	        JsonElement? ranksEl = null;
            var sidecarAvailable = strDoc != null;
            var sidecarMatched = false;
	        if (strDoc != null)
	        {
	            EnumerateJsonItems(strDoc.RootElement, item =>
	            {
	                if (FactionSidecarMatches(item, factionName, factionId) &&
	                    item.TryGetProperty("ranks", out var sr))
                    {
                        sidecarMatched = true;
	                    ranksEl = sr;
                    }
	            });
	        }

	        if ((!sidecarAvailable || !sidecarMatched) &&
	            ranksEl == null &&
	            f.TryGetProperty("ranks", out var r) &&
	            r.ValueKind == JsonValueKind.Object)
	        {
	            ranksEl = r;
	        }

        if (ranksEl == null) return;
        var re = ranksEl.Value;

        // Branching hierarchy
        if (re.TryGetProperty("branches", out var branches) && branches.ValueKind == JsonValueKind.Array && branches.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]👑 Иерархия рангов:[/]");
            var branchNamesById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var branch in branches.EnumerateArray())
            {
                var branchId = GetStr(branch, "branchId", "");
                var branchDisplayName = GetStr(branch, "displayName", branchId);
                if (!string.IsNullOrWhiteSpace(branchId) && !branchNamesById.ContainsKey(branchId))
                    branchNamesById[branchId] = branchDisplayName;
            }

            foreach (var branch in branches.EnumerateArray())
            {
                var brId = GetStr(branch, "branchId", "");
                var brName = GetStr(branch, "displayName", brId);
                var isCore = branch.TryGetProperty("isCoreBranch", out var cb) && cb.ValueKind == JsonValueKind.True;
                var brLabel = isCore ? $"[bold]{Markup.Escape(brName)}[/] [dim](основная)[/]" : Markup.Escape(brName);
                lines.Add($"    🔹 {brLabel}");

                if (branch.TryGetProperty("ranks", out var rankArr) && rankArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var rank in rankArr.EnumerateArray())
                    {
                        var rankNameM = GetStr(rank, "rankNameMale", GetStr(rank, "name", "?"));
                        var rankNameF = GetStr(rank, "rankNameFemale", "");
                        var reqRep = GetStr(rank, "requiredReputation", "");
                        var unlockCond = GetStr(rank, "unlockCondition", "");
                        var isJunction = rank.TryGetProperty("isJunctionPoint", out var jp) && jp.ValueKind == JsonValueKind.True;
                        var isCurrent = rankNameM.Equals(currentPlayerRank, StringComparison.OrdinalIgnoreCase) ||
                                        (!string.IsNullOrEmpty(rankNameF) && rankNameF.Equals(currentPlayerRank, StringComparison.OrdinalIgnoreCase));

                        // Rank name (male / female)
                        var displayName = rankNameM;
                        if (!string.IsNullOrEmpty(rankNameF) && rankNameF != rankNameM)
                            displayName += $" / {rankNameF}";

                        var line = isCurrent
                            ? $"      [bold green]► {Markup.Escape(displayName)}[/] [green](ваш ранг)[/]"
                            : $"      • {Markup.Escape(displayName)}";
                        if (!string.IsNullOrEmpty(reqRep)) line += $" [dim](реп. {Markup.Escape(reqRep)}+)[/]";
                        if (isJunction) line += " [yellow]⚡ развилка[/]";
                        lines.Add(line);

                        // Unlock condition (quest-like requirement)
                        if (!string.IsNullOrEmpty(unlockCond))
                            lines.Add($"        🔑 [italic yellow]{Markup.Escape(unlockCond)}[/]");

                        // Benefits (array of strings per Block 21)
                        if (rank.TryGetProperty("benefits", out var benefitsEl))
                        {
                            if (benefitsEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var b in benefitsEl.EnumerateArray())
                                {
                                    var bStr = b.ValueKind == JsonValueKind.String ? (b.GetString() ?? "") : "";
                                    if (!string.IsNullOrEmpty(bStr))
                                        lines.Add($"        ✓ [dim]{Markup.Escape(bStr)}[/]");
                                }
                            }
                            else if (benefitsEl.ValueKind == JsonValueKind.String)
                            {
                                var bStr = benefitsEl.GetString() ?? "";
                                if (!string.IsNullOrEmpty(bStr))
                                    lines.Add($"        ✓ [dim]{Markup.Escape(bStr)}[/]");
                            }
                        }

                        // Available branches at junction point
                        if (isJunction && rank.TryGetProperty("availableBranches", out var avBranches)
                            && avBranches.ValueKind == JsonValueKind.Array && avBranches.GetArrayLength() > 0)
                        {
                            lines.Add("        [yellow]Доступные ветки:[/]");
                            foreach (var ab in avBranches.EnumerateArray())
                            {
                                var abName = ab.ValueKind == JsonValueKind.String
                                    ? branchNamesById.GetValueOrDefault(ab.GetString() ?? "", ab.GetString() ?? "?")
                                    : GetStr(ab, "displayName", branchNamesById.GetValueOrDefault(GetStr(ab, "branchId", ""), GetStr(ab, "branchId", "?")));
                                lines.Add($"          ↳ [yellow]{Markup.Escape(abName)}[/]");
                            }
                        }
                    }
                }
            }
        }
        else if (re.ValueKind == JsonValueKind.Array)
        {
            // Simple rank array fallback
            lines.Add("");
            lines.Add("  [bold]👑 Ранги:[/]");
            foreach (var rank in re.EnumerateArray())
            {
                if (rank.ValueKind == JsonValueKind.String)
                {
                    var rn = rank.GetString() ?? "?";
                    var isCur = rn.Equals(currentPlayerRank, StringComparison.OrdinalIgnoreCase);
                    lines.Add(isCur
                        ? $"    [bold green]► {Markup.Escape(rn)}[/] [green](ваш ранг)[/]"
                        : $"    • {Markup.Escape(rn)}");
                }
                else if (rank.ValueKind == JsonValueKind.Object)
                {
                    var rnM = GetStr(rank, "rankNameMale", GetStr(rank, "name", "?"));
                    var rnF = GetStr(rank, "rankNameFemale", "");
                    var displayName = rnM;
                    if (!string.IsNullOrEmpty(rnF) && rnF != rnM)
                        displayName += $" / {rnF}";
                    var reqRep = GetStr(rank, "requiredReputation", "");
                    var unlockCond = GetStr(rank, "unlockCondition", "");
                    var isCur = rnM.Equals(currentPlayerRank, StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrEmpty(rnF) && rnF.Equals(currentPlayerRank, StringComparison.OrdinalIgnoreCase));

                    var line = isCur
                        ? $"    [bold green]► {Markup.Escape(displayName)}[/] [green](ваш ранг)[/]"
                        : $"    • {Markup.Escape(displayName)}";
                    if (!string.IsNullOrEmpty(reqRep)) line += $" [dim](реп. {Markup.Escape(reqRep)}+)[/]";
                    lines.Add(line);

                    if (!string.IsNullOrEmpty(unlockCond))
                        lines.Add($"      🔑 [italic yellow]{Markup.Escape(unlockCond)}[/]");
                    if (rank.TryGetProperty("benefits", out var benefitsEl2))
                    {
                        if (benefitsEl2.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var b in benefitsEl2.EnumerateArray())
                            {
                                var bStr = b.ValueKind == JsonValueKind.String ? (b.GetString() ?? "") : "";
                                if (!string.IsNullOrEmpty(bStr))
                                    lines.Add($"      ✓ [dim]{Markup.Escape(bStr)}[/]");
                            }
                        }
                        else if (benefitsEl2.ValueKind == JsonValueKind.String)
                        {
                            var bStr = benefitsEl2.GetString() ?? "";
                            if (!string.IsNullOrEmpty(bStr))
                                lines.Add($"      ✓ [dim]{Markup.Escape(bStr)}[/]");
                        }
                    }
                }
            }
        }
    }

    private static void RenderFactionChronicles(List<string> lines, JsonElement f,
        JsonDocument? chrDoc, string factionName, string factionId)
    {
        var entries = CollectFactionChronicleEntries(f, chrDoc, factionName, factionId);

        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold]📜 Хроники ({entries.Count}):[/]");
        for (var i = 0; i < entries.Count; i++)
            lines.Add($"    [dim]{Markup.Escape(entries[i])}[/]");
    }

    // ═══════════════════════════════════════════════════════════
    //  Faction Helper Methods
    // ═══════════════════════════════════════════════════════════

    /// <summary>Power tier label from calibration matrix (Rule 21.3.0).</summary>
    private static string GetPowerTierLabel(int val) => val switch
    {
        <= 10 => "Незначительная",
        <= 30 => "Мелкая",
        <= 60 => "Региональная",
        <= 80 => "Крупная",
        <= 100 => "Мировая угроза",
        _ => "Трансцендентная"
    };

    /// <summary>Colored power bar for 0..100+ values.</summary>
    private static string PowerBar(int value)
    {
        var clamped = Math.Clamp(value, 0, 120);
        var filled = Math.Min(clamped / 10, 10);
        var color = value switch { <= 20 => "grey", <= 50 => "yellow", <= 80 => "orange1", _ => "red" };
        return ConsoleLayout.CreateBar(filled, 10, color);
    }

    private async Task ShowWorldNews()
    {
        var commandLine = string.IsNullOrWhiteSpace(_currentCommandRemainder)
            ? "/новости_мира"
            : "/новости_мира " + _currentCommandRemainder;
        var result = await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(commandLine, _stateManager, _fs);
        if (result == null)
        {
            ShowEmptyPanel(_loc.T("world_news"), "Данные новостей мира недоступны");
            return;
        }

        var isOverview = string.IsNullOrWhiteSpace(_currentCommandRemainder);
        ExplorerCommandResultConsoleRenderer.Render(_console, WithoutActions(result));
        if (isOverview)
            await PromptWorldNewsDetailAsync(result);

        WaitForKey();
    }

    private async Task PromptWorldNewsDetailAsync(ExplorerCommandResult overviewResult)
    {
        if (overviewResult.Actions.Count == 0)
            return;

        while (true)
        {
            var selectedAction = PromptWorldNewsAction(overviewResult);
            if (selectedAction == null)
                return;

            var detailResult = await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(selectedAction.Command, _stateManager, _fs);
            if (detailResult == null)
                return;

            WriteLine();
            ExplorerCommandResultConsoleRenderer.Render(_console, WithoutActions(detailResult));

            var next = Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold cyan]Новости мира: запись[/]")
                    .PageSize(3)
                    .AddChoices("← Назад к списку", "← Закрыть"));

            if (!next.Contains("списку", StringComparison.OrdinalIgnoreCase))
                return;

            WriteLine();
        }
    }

    private UiAction? PromptWorldNewsAction(ExplorerCommandResult overviewResult)
    {
        const string backChoice = "← Назад";
        var choices = new List<string>();
        var actionsByChoice = new Dictionary<string, UiAction>(StringComparer.Ordinal);
        foreach (var action in overviewResult.Actions)
        {
            if (string.IsNullOrWhiteSpace(action.Command) || string.IsNullOrWhiteSpace(action.Label))
                continue;

            var label = action.Label;
            if (actionsByChoice.ContainsKey(label))
                label = $"{label} ({action.Command})";

            choices.Add(label);
            actionsByChoice[label] = action;
        }

        if (choices.Count == 0)
            return null;

        choices.Add(backChoice);
        var selected = Prompt(
            new SelectionPrompt<string>()
                .Title("[bold cyan]Действие: Новости мира[/]")
                .PageSize(Math.Clamp(choices.Count, 3, 15))
                .AddChoices(choices));

        if (string.Equals(selected, backChoice, StringComparison.Ordinal) ||
            !actionsByChoice.TryGetValue(selected, out var selectedAction))
        {
            return null;
        }

        return selectedAction;
    }

    private static ExplorerCommandResult WithoutActions(ExplorerCommandResult result) =>
        new()
        {
            Command = result.Command,
            State = result.State,
            Blocks = result.Blocks,
            Prompts = result.Prompts,
            Notifications = result.Notifications,
            InteractiveSession = result.InteractiveSession
        };

    private async Task ShowCraftMenu()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/inventory/recipes.json");
        if (doc == null) { ShowEmptyPanel(_loc.T("craft"), "Рецептов нет"); return; }

        var text = new List<string>();
        var recipeCount = 0;

        // Try both "recipes" array and "knownRecipes" array (Block 9)
        Action<JsonElement> renderRecipe = item =>
        {
            recipeCount++;
            var name = GetStr(item, "recipeName", GetStr(item, "name", "???"));
            var desc = GetStr(item, "description", "");
            var craftedItem = GetStr(item, "craftedItemName", "");
            var rank = GetStr(item, "recipeRank", "");
            var outputQty = GetStr(item, "outputQuantity", "1");
            var timeCost = GetStr(item, "timeCost", "");
            var diffMod = GetStr(item, "difficultyModifier", "");

            text.Add($"[bold orange3]📜 {Markup.Escape(name)}[/]" +
                (!string.IsNullOrEmpty(rank) ? $" [dim]({Markup.Escape(rank)})[/]" : ""));
            if (!string.IsNullOrEmpty(desc))
                text.Add($"  [dim]{Markup.Escape(desc)}[/]");
            if (!string.IsNullOrEmpty(craftedItem))
                text.Add($"  ➤ Результат: [white]{Markup.Escape(craftedItem)}[/]" +
                    (outputQty != "1" ? $" ×{Markup.Escape(outputQty)}" : ""));

            // Required knowledge skill
            if (item.TryGetProperty("requiredKnowledgeSkill", out var rks) && rks.ValueKind == JsonValueKind.Object)
            {
                var skillName = GetStr(rks, "skillName", "");
                var masteryLvl = GetStr(rks, "requiredMasteryLevel", "");
                if (!string.IsNullOrEmpty(skillName))
                    text.Add($"  📚 Навык: [cyan]{Markup.Escape(skillName)}[/] (уровень {Markup.Escape(masteryLvl)})");
            }

            // Required materials
            if (item.TryGetProperty("requiredMaterials", out var mats) && mats.ValueKind == JsonValueKind.Array)
            {
                text.Add("  🧱 Материалы:");
                foreach (var m in mats.EnumerateArray())
                {
                    var matName = GetStr(m, "materialName", "?");
                    var matQty = GetStr(m, "quantity", "1");
                    var matLine = $"    • [white]{Markup.Escape(matName)}[/] ×{Markup.Escape(matQty)}";
                    if (m.TryGetProperty("alternatives", out var alts) && alts.ValueKind == JsonValueKind.Array && alts.GetArrayLength() > 0)
                    {
                        var altNames = new List<string>();
                        foreach (var a in alts.EnumerateArray())
                            if (a.ValueKind == JsonValueKind.String) altNames.Add(a.GetString() ?? "");
                        if (altNames.Count > 0)
                            matLine += $" [dim](или: {Markup.Escape(string.Join(", ", altNames))})[/]";
                    }
                    text.Add(matLine);
                }
            }

            // Required tools
            if (item.TryGetProperty("requiredTools", out var tools) && tools.ValueKind == JsonValueKind.Object)
            {
                var toolParts = new List<string>();
                foreach (var category in new[] { "portable", "stationary" })
                {
                    if (tools.TryGetProperty(category, out var arr) && arr.ValueKind == JsonValueKind.Array)
                        foreach (var t in arr.EnumerateArray())
                            toolParts.Add(GetStr(t, "example", GetStr(t, "function", "?")));
                }
                if (toolParts.Count > 0)
                    text.Add($"  🔨 Инструменты: [white]{Markup.Escape(string.Join(", ", toolParts))}[/]");
                if (tools.TryGetProperty("optional", out var opt) && opt.ValueKind == JsonValueKind.Array)
                    foreach (var t in opt.EnumerateArray())
                    {
                        var bonus = GetStr(t, "bonus", "");
                        text.Add($"    [dim]+ {Markup.Escape(GetStr(t, "example", GetStr(t, "function", "?")))} (бонус: {Markup.Escape(bonus)})[/]");
                    }
            }

            // Time cost & difficulty
            var extras = new List<string>();
            if (!string.IsNullOrEmpty(timeCost)) extras.Add($"⏱ {Markup.Escape(timeCost)} мин");
            if (!string.IsNullOrEmpty(diffMod) && diffMod != "0") extras.Add($"⚙ Сложность: {Markup.Escape(diffMod)}");
            if (extras.Count > 0)
                text.Add($"  [dim]{string.Join("  |  ", extras)}[/]");

            text.Add("");
        };

        EnumerateArray(doc.RootElement, "recipes", renderRecipe);
        EnumerateArray(doc.RootElement, "knownRecipes", renderRecipe);
        // Fallback: if root is array
        if (recipeCount == 0 && doc.RootElement.ValueKind == JsonValueKind.Array)
            foreach (var item in doc.RootElement.EnumerateArray()) renderRecipe(item);

        if (recipeCount == 0) { ShowEmptyPanel(_loc.T("craft"), "Рецептов нет"); return; }

        var panel = new Panel(GameInterface.SafeMarkup(string.Join("\n", text)))
        {
            Header = new PanelHeader($" 📜 Рецепты ({recipeCount}) ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Orange3),
            Padding = new Padding(1, 1),
            Expand = true
        };
        Write(panel);
        WaitForKey();
    }
}

