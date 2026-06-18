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
        var summaryTable = ConsoleLayout.CreateInfoTable();

        // Level + XP bar
        if (lvl > 0)
        {
            var xpLine = $"[bold yellow]{lvl}[/]";
            if (!string.IsNullOrEmpty(archetype))
                xpLine += $" [dim]({Markup.Escape(archetype)})[/]";
            summaryTable.AddRow(new Markup("[yellow]Уровень развития[/]"), new Markup(xpLine));

            if (xpNext > 0)
            {
                var pct = Math.Min(100, xp * 100 / Math.Max(1, xpNext));
                var progressTable = ConsoleLayout.CreateBarMetricTable();
                progressTable.AddRow(
                    new Markup("[cyan]Прогресс развития[/]"),
                    new Markup(ConsoleLayout.CreateBarFromPercent(pct, 16, "cyan")),
                    new Markup($"[cyan]{xp}/{xpNext}[/]"),
                    new Markup($"[dim]{pct}%[/]"));
                content.AddRow(summaryTable);
                content.AddRow(progressTable);
                summaryTable = ConsoleLayout.CreateInfoTable();
            }

            // Custom archetype priorities (Rule 21.1.2)
            if (f.TryGetProperty("customArchetypePriorities", out var cap) && cap.ValueKind == JsonValueKind.Object)
            {
                var primary = GetStr(cap, "primary", "");
                var secondary = GetStr(cap, "secondary", "");
                var tertiary = GetStr(cap, "tertiary", "");
                if (!string.IsNullOrEmpty(primary))
                    summaryTable.AddRow(new Markup("[dim]Приоритеты развития[/]"), new Markup($"[bold]{Markup.Escape(primary)}[/] > [yellow]{Markup.Escape(secondary)}[/] > [dim]{Markup.Escape(tertiary)}[/]"));
            }
        }

        // Reputation with label
        var factionTier = ReputationDisplay.GetTier(ReputationScaleKind.Faction, rep);
        summaryTable.AddRow(new Markup($"[{factionTier.Color}]Репутация[/]"), new Markup(ReputationDisplay.BuildValueLabelMarkup(rep, ReputationScaleKind.Faction)));
        if (!string.IsNullOrEmpty(repDesc))
            summaryTable.AddRow(new Markup("[dim]Пояснение[/]"), new Markup($"[dim]{Markup.Escape(repDesc)}[/]"));

        // Membership
        if (isPlayerFaction)
            summaryTable.AddRow(new Markup("[gold1]Статус игрока[/]"), new Markup("[bold gold1]Вы — лидер этой фракции[/]"));
        else if (isMember)
        {
            var memberLine = "[green]Член фракции[/]";
            if (!string.IsNullOrEmpty(playerRank))
                memberLine += $" | Ранг: [yellow]{Markup.Escape(playerRank)}[/]";
            if (!string.IsNullOrEmpty(playerBranch))
                memberLine += $" [dim]({Markup.Escape(ResolveFactionBranchDisplayName(f, strDoc, name, factionId, playerBranch))})[/]";
            summaryTable.AddRow(new Markup("[green]Статус игрока[/]"), new Markup(memberLine));
        }

        // Strategy directive
        var directive = GetStr(f, "playerStrategyDirective", "");
        if (!string.IsNullOrEmpty(directive))
        {
            summaryTable.AddRow(new Markup("[cyan]Стратегическая директива[/]"), new Markup($"[italic cyan]{Markup.Escape(directive)}[/]"));
        }
        else if (isPlayerFaction)
        {
            summaryTable.AddRow(new Markup("[dim]Стратегическая директива[/]"), new Markup("[dim italic]не задана (используйте /директива_фракции)[/]"));
        }

        if (summaryTable.Rows.Count > 0)
            content.AddRow(summaryTable);

        // ═══ Power Profile ═══
        if (f.TryGetProperty("powerProfile", out var pp) && pp.ValueKind == JsonValueKind.Object)
        {
            lines.Add("");
            lines.Add("  [bold]📊 Профиль силы:[/]");
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
            foreach (var (key, label) in powerNames)
            {
                if (pp.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var val))
                {
                    var tier = GetPowerTierLabel(val);
                    lines.Add($"    {Markup.Escape(label)}: {PowerBar(val)}  [white]{val}[/] [dim]{tier}[/]");
                }
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
                var locName = GetStr(t, "locationName", GetStr(t, "locationId", "?"));
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
        await WaitForKeyWithImage("faction", name, GetStr(f, "image_prompt", ""), GetStr(f, "factionId", name));
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
	        var activeProjects = new List<JsonElement>();
	        var completedProjects = new List<JsonElement>();
            var sidecarAvailable = projDoc != null;
            var sidecarMatched = false;

	        if (projDoc != null)
	        {
	            EnumerateJsonItems(projDoc.RootElement, item =>
	            {
	                if (FactionSidecarMatches(item, factionName, factionId))
	                {
                        sidecarMatched = true;
	                    if (item.TryGetProperty("finalState", out _) || item.TryGetProperty("completionTurn", out _))
	                        completedProjects.Add(item);
	                    else
	                        activeProjects.Add(item);
	                }
	            });
	        }

	        if (!sidecarAvailable || !sidecarMatched)
	        {
	            if (f.TryGetProperty("activeProjects", out var ap) && ap.ValueKind == JsonValueKind.Array)
	                foreach (var p in ap.EnumerateArray()) activeProjects.Add(p);
	            if (f.TryGetProperty("completedProjects", out var cpCore) && cpCore.ValueKind == JsonValueKind.Array)
	                foreach (var p in cpCore.EnumerateArray()) completedProjects.Add(p);
	        }

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
        var entries = new List<string>();

        // From core scribeChronicle
        if (f.TryGetProperty("scribeChronicle", out var sc) && sc.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in sc.EnumerateArray())
            {
                var txt = e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.ToString();
                if (!string.IsNullOrEmpty(txt)) entries.Add(txt);
            }
        }

        // From faction_chronicles.json
        if (chrDoc != null)
        {
            EnumerateJsonItems(chrDoc.RootElement, item =>
            {
                if (!FactionSidecarMatches(item, factionName, factionId)) return;
                var entry = GetStr(item, "entry", GetStr(item, "chronicle", GetStr(item, "text", "")));
                if (!string.IsNullOrEmpty(entry) && !entries.Contains(entry))
                    entries.Add(entry);
            });
        }

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
        ExplorerCommandResultConsoleRenderer.Render(_console, isOverview ? WithoutActions(result) : result);
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

