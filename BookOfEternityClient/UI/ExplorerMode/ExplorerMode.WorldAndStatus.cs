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
{
    private async Task ShowMap()
    {
        var map = await LocalMapViewService.BuildCurrentRealmMapAsync(_fs);
        ExplorerCommandResultConsoleRenderer.Render(
            _console,
            new ExplorerCommandResult
            {
                Command = "/map",
                State = CommandExecutionState.Completed,
                Blocks =
                [
                    new UiMapBlock
                    {
                        Title = "Карта",
                        Map = map
                    },
                    ExplorerMortalWorldCommandResultBuilder.BuildMapSummaryDossier(map)
                ]
            });

        var launch = await LocalMapViewerLauncher.WriteAndOpenAsync(_fs, map);
        MarkupLine(launch.Opened
            ? "[green]Локальная карта открыта.[/]"
            : "[yellow]Локальная карта подготовлена, но не открылась автоматически.[/]");
        WaitForKey();
    }

    private async Task ShowLocationDetailPanel(JsonElement loc, bool isCurrent)
    {
        var name = GetLocationName(loc);
        var playerLevel = await GetPlayerLevelAsync();
        var lines = new List<string>();
        lines.Add($"[bold green]{(isCurrent ? "📍" : "🗺")} {Markup.Escape(name)}[/]");
        lines.Add("");

        // Description
        var desc = GetStr(loc, "description", "");
        if (!string.IsNullOrEmpty(desc))
        {
            lines.Add($"[white]{Markup.Escape(desc)}[/]");
            lines.Add("");
        }

        var lastEvents = GetStr(loc, "lastEventsDescription", "");
        if (!string.IsNullOrEmpty(lastEvents))
        {
            lines.Add($"[dim italic]📋 {Markup.Escape(ExplorerPlayerFacingLabels.HistoricalEntry(lastEvents))}[/]");
            lines.Add("");
        }

        // Basic info
        var locType = GetStr(loc, "locationType", "");
        if (!string.IsNullOrEmpty(locType))
            lines.Add($"  📋 Тип: [cyan]{Markup.Escape(FormatLocationTypeForPlayer(locType))}[/]");

        var indoorType = GetStr(loc, "indoorType", "");
        if (!string.IsNullOrEmpty(indoorType))
        {
            var indoorLabel = indoorType switch
            {
                "Building" => "🏠 Здание",
                "Dungeon" => "🏰 Подземелье",
                "CaveSystem" => "🕳 Пещера",
                "Vehicle" => "🚗 Транспорт",
                "UniqueIndoor" => "✨ Уникальное",
                _ => Markup.Escape(indoorType)
            };
            lines.Add($"  {indoorLabel}");
        }

        var biome = GetStr(loc, "biome", "");
        if (!string.IsNullOrEmpty(biome))
            lines.Add($"  🌿 Биом: [green]{Markup.Escape(biome)}[/]");

        // Features
        if (loc.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Array && features.GetArrayLength() > 0)
        {
            var featureStrs = new List<string>();
            foreach (var f in features.EnumerateArray())
            {
                var fStr = f.ValueKind == JsonValueKind.String ? f.GetString() ?? "" : f.ToString();
                if (!string.IsNullOrEmpty(fStr)) featureStrs.Add(fStr);
            }
            if (featureStrs.Count > 0)
                lines.Add($"  ✦ Особенности: [cyan]{Markup.Escape(string.Join(", ", featureStrs))}[/]");
        }

        if (loc.TryGetProperty("coordinates", out var coords))
            lines.Add($"  📐 Координаты: [dim][[{GetInt(coords, "x", 0)}, {GetInt(coords, "y", 0)}, {GetInt(coords, "z", 0)}]][/]");

        // Faction control
        if (loc.TryGetProperty("factionControl", out var fc) && fc.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in fc.EnumerateArray())
            {
                var fName = GetStr(f, "factionName", GetStr(f, "factionId", GetStr(f, "name", "?")));
                var fLevel = GetStr(f, "controlLevel", "");
                var fType = GetStr(f, "controlType", "");
                var ctLabel = fType.ToLower() switch
                {
                    "military" => "⚔ Военный",
                    "economic" => "💰 Экономический",
                    "social" => "💬 Социальный",
                    "covert" => "🕵 Скрытый",
                    _ => !string.IsNullOrEmpty(fType) ? Markup.Escape(fType) : ""
                };
                var line = $"  🏰 Фракция: [yellow]{Markup.Escape(fName)}[/]";
                if (!string.IsNullOrEmpty(ctLabel)) line += $" [dim]({ctLabel})[/]";
                if (!string.IsNullOrEmpty(fLevel)) line += $" контроль: [white]{Markup.Escape(fLevel)}%[/]";
                lines.Add(line);
            }
        }

        // Difficulty profiles with visual bars + human-readable labels
        void ShowDifficulty(string label, string propName)
        {
            if (!loc.TryGetProperty(propName, out var diff) || diff.ValueKind != JsonValueKind.Object) return;
            var combat = GetInt(diff, "combat", 0);
            var env = GetInt(diff, "environment", 0);
            var social = GetInt(diff, "social", 0);
            var explore = GetInt(diff, "exploration", 0);

            var (overallLabel, overallColor) = GetProfileDifficultyLabel(diff, playerLevel);

            lines.Add("");
            lines.Add($"  [bold]{label}:[/]  [{overallColor}]{overallLabel}[/] [dim](ур. {playerLevel})[/]");
            lines.Add($"    ⚔ Бой:          {DifficultyBar(combat)}  {DifficultyWithLabel(combat, playerLevel)}");
            lines.Add($"    🌿 Окружение:    {DifficultyBar(env)}  {DifficultyWithLabel(env, playerLevel)}");
            lines.Add($"    💬 Социальная:   {DifficultyBar(social)}  {DifficultyWithLabel(social, playerLevel)}");
            lines.Add($"    🔍 Исследование: {DifficultyBar(explore)}  {DifficultyWithLabel(explore, playerLevel)}");
        }

        ShowDifficulty("🔒 Сложность (для своих)", "internalDifficultyProfile");
        ShowDifficulty("⚠ Сложность (для чужих)", "externalDifficultyProfile");

        // Active threats — FULL detail
        if (loc.TryGetProperty("activeThreats", out var threats) && threats.ValueKind == JsonValueKind.Array && threats.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold red]⚠ Активные угрозы ({threats.GetArrayLength()}):[/]");
            foreach (var t in threats.EnumerateArray())
                RenderThreatFull(lines, t);
        }

        // Adjacent locations — enriched with linkType, description, estimated difficulty
        if (loc.TryGetProperty("adjacencyMap", out var adj) && adj.ValueKind == JsonValueKind.Array && adj.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]🧭 Соседние локации:[/]");
            foreach (var entry in adj.EnumerateArray())
            {
                var aName = GetStr(entry, "name", GetStr(entry, "targetLocationName", GetStr(entry, "targetLocationId", "?")));
                var dir = GetStr(entry, "direction", "");
                var dist = GetStr(entry, "distance", "");
                var linkState = GetStr(entry, "linkState", "");
                var linkType = GetStr(entry, "linkType", "");
                var shortDesc = GetStr(entry, "shortDescription", "");
                var linkStateLabel = ExplorerPlayerFacingLabels.LocationLinkState(linkState);
                var linkColor = ExplorerPlayerFacingLabels.LocationLinkStateColor(linkState);
                var line = $"    → [{linkColor}]{Markup.Escape(aName)}[/]";
                if (!string.IsNullOrEmpty(linkType)) line += $" [dim]⟨{Markup.Escape(linkType)}⟩[/]";
                if (!string.IsNullOrEmpty(dir)) line += $" ({Markup.Escape(dir)})";
                if (!string.IsNullOrEmpty(dist)) line += $" [dim]{Markup.Escape(dist)}[/]";
                if (!string.IsNullOrEmpty(linkStateLabel))
                    line += $" [{linkColor}]({Markup.Escape(linkStateLabel)})[/]";
                lines.Add(line);
                if (!string.IsNullOrEmpty(shortDesc))
                    lines.Add($"      [dim]{Markup.Escape(shortDesc)}[/]");

                // Estimated difficulty for adjacent location
                if (entry.TryGetProperty("estimatedExternalDifficultyProfile", out var estExt) && estExt.ValueKind == JsonValueKind.Object)
                {
                    var (estLabel, estColor) = GetProfileDifficultyLabel(estExt, playerLevel);
                    lines.Add($"      [dim]Сложность: [{estColor}]{estLabel}[/][/]");
                }
                else if (entry.TryGetProperty("estimatedInternalDifficultyProfile", out var estInt) && estInt.ValueKind == JsonValueKind.Object)
                {
                    var (estLabel, estColor) = GetProfileDifficultyLabel(estInt, playerLevel);
                    lines.Add($"      [dim]Сложность: [{estColor}]{estLabel}[/][/]");
                }
            }
        }

        // Location storages
        if (loc.TryGetProperty("locationStorages", out var storages) && storages.ValueKind == JsonValueKind.Array && storages.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]📦 Хранилища:[/]");
            foreach (var st in storages.EnumerateArray())
            {
                var sName = GetStr(st, "name", "Хранилище");
                var sCap = GetStr(st, "capacity", "");
                var sVol = GetStr(st, "volume", "");
                var sOwnerName = "";
                var sOwnerType = "";
                if (st.TryGetProperty("owner", out var own) && own.ValueKind == JsonValueKind.Object)
                {
                    sOwnerName = GetStr(own, "ownerName", "");
                    sOwnerType = GetStr(own, "ownerType", "");
                }
                var hasAccess = st.TryGetProperty("hasFullAccess", out var ha) && ha.ValueKind == JsonValueKind.True;
                var accessIcon = hasAccess ? "[green]✓ доступ[/]" : "[red]✗ нет доступа[/]";

                // Owner type label
                var ownerTypeLabel = sOwnerType.ToLower() switch
                {
                    "player" => "👤 Личное",
                    "faction" => "🏛️ Фракционное",
                    "shared" => "🤝 Общее",
                    _ => ""
                };

                var sLine = $"    📦 [white]{Markup.Escape(sName)}[/] {accessIcon}";
                if (!string.IsNullOrEmpty(ownerTypeLabel)) sLine += $" [dim]{ownerTypeLabel}[/]";
                lines.Add(sLine);

                // Capacity and volume on detail line
                var detailParts = new List<string>();
                if (!string.IsNullOrEmpty(sCap)) detailParts.Add($"вместимость: {Markup.Escape(sCap)} стаков");
                if (!string.IsNullOrEmpty(sVol)) detailParts.Add($"объём: {Markup.Escape(sVol)} дм³");
                if (!string.IsNullOrEmpty(sOwnerName)) detailParts.Add($"владелец: {Markup.Escape(sOwnerName)}");
                if (detailParts.Count > 0)
                    lines.Add($"      [dim]{string.Join(" │ ", detailParts)}[/]");

                var sDesc = GetStr(st, "description", "");
                if (!string.IsNullOrEmpty(sDesc))
                    lines.Add($"      [dim italic]{Markup.Escape(sDesc)}[/]");

                // Authorized users for shared storages
                if (st.TryGetProperty("authorizedUsers", out var authUsers) &&
                    authUsers.ValueKind == JsonValueKind.Array && authUsers.GetArrayLength() > 0)
                {
                    var userNames = new List<string>();
                    foreach (var u in authUsers.EnumerateArray())
                    {
                        var uName = GetStr(u, "playerName", GetStr(u, "name", ""));
                        if (!string.IsNullOrEmpty(uName)) userNames.Add(Markup.Escape(uName));
                    }
                    if (userNames.Count > 0)
                        lines.Add($"      🤝 Доступ: [cyan]{string.Join(", ", userNames)}[/]");
                }

                // Contents — show item names, not just count
                if (st.TryGetProperty("contents", out var cont) && cont.ValueKind == JsonValueKind.Array)
                {
                    var contCount = cont.GetArrayLength();
                    if (contCount == 0)
                    {
                        lines.Add($"      [dim]Пусто[/]");
                    }
                    else
                    {
                        lines.Add($"      Предметов: [white]{contCount}[/]");
                        var shown = 0;
                        foreach (var ci in cont.EnumerateArray())
                        {
                            if (++shown > 8)
                            {
                                lines.Add($"        [dim]...и ещё {contCount - 8}[/]");
                                break;
                            }
                            var ciName = GetStr(ci, "name", "?");
                            var ciQty = GetStr(ci, "quantity", "1");
                            var ciLine = $"        • {Markup.Escape(ciName)}";
                            if (!string.IsNullOrEmpty(ciQty) && ciQty != "1") ciLine += $" ×{Markup.Escape(ciQty)}";
                            lines.Add(ciLine);
                        }
                    }
                }
            }
        }

        // Event log / history
        if (loc.TryGetProperty("eventDescriptions", out var events) && events.ValueKind == JsonValueKind.Array && events.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]📜 Журнал событий:[/]");
            var evCount = 0;
            foreach (var ev in events.EnumerateArray())
            {
                if (++evCount > 10) { lines.Add($"    [dim]...и ещё {events.GetArrayLength() - 10}[/]"); break; }
                var evStr = ev.ValueKind == JsonValueKind.String ? ev.GetString() ?? "" : GetStr(ev, "description", ev.GetRawText());
                evStr = ExplorerPlayerFacingLabels.HistoricalEntry(evStr);
                lines.Add($"    [dim]• {Markup.Escape(evStr)}[/]");
            }
        }

        // Image prompt hint
        var imgPrompt = GetStr(loc, "image_prompt", "");
        if (!string.IsNullOrEmpty(imgPrompt))
        {
            lines.Add("");
            lines.Add($"  [dim italic]🖼️ {Markup.Escape(imgPrompt)}[/]");
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader($" {(isCurrent ? "📍" : "🗺")} {Markup.Escape(name)} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1),
            Expand = true
        });
        await WaitForKeyWithImage("location", name, imgPrompt, GetStr(loc, "locationId", GetStr(loc, "targetLocationId", name)));
    }

    /// <summary>Returns difficulty value with a colored label, e.g. "25 Нормально".</summary>
    private static string DifficultyWithLabel(int value, int playerLevel)
    {
        var (label, color) = GetDifficultyLabel(value, playerLevel);
        return $"[white]{value}[/] [{color}]{label}[/]";
    }

    /// <summary>Renders a colored bar from 0..200 for difficulty display.</summary>
    private static string DifficultyBar(int value)
    {
        var clamped = Math.Clamp(value, 0, 200);
        var filled = Math.Min(clamped / 10, 10);
        var empty = 10 - filled;
        var color = value switch { <= 20 => "green", <= 40 => "yellow", <= 60 => "orange1", _ => "red" };
        return ConsoleLayout.CreateBar(filled, 10, color);
    }

    /// <summary>
    /// Returns a human-readable difficulty label based on the difficulty value and player level.
    /// Uses the scaling table from Block 20 (Rule 20.0.A).
    /// </summary>
    private static (string label, string color) GetDifficultyLabel(int difficulty, int playerLevel)
    {
        // Scaling thresholds from Block 20.0.A
        var (standardMax, hardMax) = playerLevel switch
        {
            <= 5  => (25, 40),
            <= 10 => (40, 55),
            <= 20 => (55, 70),
            <= 30 => (70, 85),
            <= 45 => (85, 100),
            <= 60 => (100, 120),
            <= 80 => (120, 140),
            <= 100 => (150, 180),
            _ => (150, 180)
        };

        if (difficulty <= 0)
            return ("Безопасно", "green");
        if (difficulty <= standardMax / 2)
            return ("Легко", "green3");
        if (difficulty <= standardMax)
            return ("Нормально", "yellow");
        if (difficulty <= hardMax)
            return ("Сложно", "orange1");
        if (difficulty <= hardMax + (hardMax - standardMax))
            return ("Очень сложно", "red");
        return ("☠ СМЕРТЕЛЬНО", "bold red");
    }

    /// <summary>
    /// Returns the overall difficulty label from a difficulty profile (max of all facets).
    /// </summary>
    private static (string label, string color) GetProfileDifficultyLabel(JsonElement profile, int playerLevel)
    {
        var maxDiff = 0;
        foreach (var facet in new[] { "combat", "environment", "social", "exploration" })
        {
            if (profile.TryGetProperty(facet, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var fv))
                maxDiff = Math.Max(maxDiff, fv);
        }
        return GetDifficultyLabel(maxDiff, playerLevel);
    }

    /// <summary>
    /// Reads the current player level from experience.json or player_status.json.
    /// </summary>
    private async Task<int> GetPlayerLevelAsync()
    {
        var expJson = await _stateManager.LoadGameStateFileAsync("game_state/player/experience.json");
        if (expJson != null)
        {
            if (expJson.RootElement.TryGetProperty("level", out var lvl) && lvl.ValueKind == JsonValueKind.Number && lvl.TryGetInt32(out var lv))
                return lv;
            if (expJson.RootElement.TryGetProperty("playerLevel", out var pl) && pl.ValueKind == JsonValueKind.Number && pl.TryGetInt32(out var plv))
                return plv;
        }
        var statusJson = await _stateManager.LoadGameStateFileAsync("game_state/core/player_status.json");
        if (statusJson != null)
        {
            if (statusJson.RootElement.TryGetProperty("level", out var lvl) && lvl.ValueKind == JsonValueKind.Number && lvl.TryGetInt32(out var slv))
                return slv;
        }
        return 1;
    }

    /// <summary>Renders a compact one-line threat summary for location overview.</summary>
    private static void RenderThreatSummary(List<string> lines, JsonElement t)
    {
        var tName = GetStr(t, "name", GetStr(t, "threatName", "Неизвестная угроза"));
        var intensity = GetInt(t, "intensity", -1);

        var line = $"    🔥 [red]{Markup.Escape(tName)}[/]";
        if (intensity >= 0)
            line += $" [dim](сила: {intensity})[/]";

        // Show current activity if present
        if (t.TryGetProperty("currentActivity", out var act) && act.ValueKind == JsonValueKind.Object)
        {
            var actName = GetStr(act, "activityName", GetStr(act, "name", ""));
            if (!string.IsNullOrEmpty(actName))
                line += $" — [yellow]{Markup.Escape(actName)}[/]";
        }

        lines.Add(line);

        // Long-term goal
        var goal = GetStr(t, "longTermGoal", "");
        if (!string.IsNullOrEmpty(goal))
            lines.Add($"      [dim]Цель: {Markup.Escape(goal)}[/]");
    }

    /// <summary>Renders full threat details for the detail panel.</summary>
    private static void RenderThreatFull(List<string> lines, JsonElement t)
    {
        var tName = GetStr(t, "name", GetStr(t, "threatName", "Неизвестная угроза"));
        var intensity = GetInt(t, "intensity", -1);

        lines.Add("");
        var header = $"    🔥 [bold red]{Markup.Escape(tName)}[/]";
        if (intensity >= 0)
            header += $"  [dim](сила: {intensity})[/]";
        lines.Add(header);

        // Threat archetype
        if (t.TryGetProperty("threatArchetype", out var arch) && arch.ValueKind == JsonValueKind.Object)
        {
            var motivation = GetStr(arch, "motivation", GetStr(arch, "customMotivation", ""));
            var method = GetStr(arch, "method", GetStr(arch, "customMethod", ""));
            if (!string.IsNullOrEmpty(motivation) || !string.IsNullOrEmpty(method))
            {
                var archStr = "";
                if (!string.IsNullOrEmpty(motivation)) archStr += $"Мотивация: {Markup.Escape(motivation)}";
                if (!string.IsNullOrEmpty(method)) archStr += (archStr.Length > 0 ? " | " : "") + $"Метод: {Markup.Escape(method)}";
                lines.Add($"      [dim]{archStr}[/]");
            }
        }

        // Long-term goal
        var goal = GetStr(t, "longTermGoal", "");
        if (!string.IsNullOrEmpty(goal))
            lines.Add($"      🎯 Цель: [yellow]{Markup.Escape(goal)}[/]");

        // Current activity with progress
        if (t.TryGetProperty("currentActivity", out var act) && act.ValueKind == JsonValueKind.Object)
        {
            var actName = GetStr(act, "activityName", GetStr(act, "name", ""));
            var actDesc = GetStr(act, "description", "");
            var totalTime = GetInt(act, "totalTimeCostMinutes", 0);
            var spentTime = GetInt(act, "timeSpentMinutes", 0);
            var activeState = GetStr(act, "activeState", "");

            if (!string.IsNullOrEmpty(actName))
            {
                var actLine = $"      ⚡ Действие: [cyan]{Markup.Escape(actName)}[/]";
                if (!string.IsNullOrEmpty(activeState))
                    actLine += $" [dim]({Markup.Escape(activeState)})[/]";
                lines.Add(actLine);
            }
            if (!string.IsNullOrEmpty(actDesc))
                lines.Add($"        [dim italic]{Markup.Escape(actDesc)}[/]");

            if (totalTime > 0)
            {
                var pct = (int)Math.Clamp((long)spentTime * 100 / totalTime, 0, 100);
                var filled = pct / 10;
                var empty = 10 - filled;
                var barColor = pct >= 80 ? "red" : pct >= 50 ? "orange1" : "yellow";
                lines.Add($"        Прогресс: [{barColor}]{new string('█', filled)}[/][dim]{new string('░', empty)}[/] {pct}% ({FormatMinutes(spentTime)}/{FormatMinutes(totalTime)})");
            }
        }
        else
        {
            lines.Add($"      [dim]💤 Угроза неактивна (бездействует)[/]");
        }

        // Impact profile
        if (t.TryGetProperty("impactProfile", out var imp) && imp.ValueKind == JsonValueKind.Object)
        {
            var target = GetStr(imp, "primaryTargetName", GetStr(imp, "primaryTargetId", ""));
            var targetType = GetStr(imp, "primaryTargetType", "");
            var impact = GetStr(imp, "primaryImpact", "");
            var impValue = GetInt(imp, "baseImpactValue", -1);

            if (!string.IsNullOrEmpty(target) || !string.IsNullOrEmpty(impact))
            {
                var impLine = "      💥 Эффект:";
                if (!string.IsNullOrEmpty(impact)) impLine += $" [orange1]{Markup.Escape(impact)}[/]";
                if (impValue >= 0) impLine += $" (сила: {impValue})";
                if (!string.IsNullOrEmpty(target)) impLine += $" → [white]{Markup.Escape(target)}[/]";
                if (!string.IsNullOrEmpty(targetType)) impLine += $" [dim]({Markup.Escape(targetType)})[/]";
                lines.Add(impLine);
            }
        }
    }

    private static void RenderWorldEventDetailed(List<string> lines, JsonElement item)
    {
        var title = GetStr(item, "eventTitle", GetStr(item, "title", GetStr(item, "name", "")));
        var summary = GetStr(item, "summary", GetStr(item, "narrativeSummary", ""));
        var desc = GetStr(item, "description", "");
        var time = GetStr(item, "timestamp", GetStr(item, "dateTime", GetStr(item, "date", "")));
        var visibility = GetStr(item, "visibility", "");
        var location = GetStr(item, "location", GetStr(item, "eventLocation", ""));
        var category = GetStr(item, "category", GetStr(item, "eventCategory", GetStr(item, "type", "")));

        var headline = !string.IsNullOrEmpty(title) ? title
            : !string.IsNullOrEmpty(summary) ? summary
            : desc;
        if (string.IsNullOrEmpty(headline)) return;

        var visColor = visibility.ToLowerInvariant() switch
        {
            "public" => "green",
            "regional" => "cyan",
            "player_known" => "yellow",
            "secret" => "red",
            "faction-internal" => "orange1",
            _ => "dim"
        };

        var line = $"[yellow bold]• {Markup.Escape(headline)}[/]";
        if (!string.IsNullOrEmpty(time))
            line = $"[dim]{Markup.Escape(time)}[/] " + line;
        lines.Add(line);

        if (!string.IsNullOrEmpty(desc) && desc != headline)
            lines.Add($"  [white]{Markup.Escape(desc)}[/]");
        if (!string.IsNullOrEmpty(summary) && summary != headline && summary != desc)
            lines.Add($"  [white]{Markup.Escape(summary)}[/]");
        if (HasRelatedRivalArc(item))
            lines.Add("  [purple]🧵 Чужая нить судьбы[/] [dim]Это заметный след параллельной судьбы другой души, а не случайный шум мира.[/]");

        var meta = new List<string>();
        if (!string.IsNullOrEmpty(visibility))
            meta.Add($"[{visColor}]{Markup.Escape(visibility)}[/]");
        if (!string.IsNullOrEmpty(location))
            meta.Add($"📍 {Markup.Escape(location)}");
        if (!string.IsNullOrEmpty(category))
            meta.Add($"📂 {Markup.Escape(category)}");
        if (meta.Count > 0)
            lines.Add($"  [dim]{string.Join(" │ ", meta)}[/]");

        AppendWorldNewsFlexibleField(lines, "👥 Участники", item, "involvedNPCs");
        AppendWorldNewsFlexibleField(lines, "🏛️ Затронутые фракции", item, "affectedFactions");
        AppendWorldNewsFlexibleField(lines, "📍 Затронутые локации", item, "affectedLocations");
        AppendWorldNewsFlexibleField(lines, "⚖ Последствия", item, "consequences");
        AppendWorldNewsFlexibleField(lines, "🏁 Итог", item, "outcome");
        AppendWorldNewsFlexibleField(lines, "➡ Продолжение", item, "followUp", "followUpEvent", "nextStep");
        AppendWorldNewsFlexibleField(lines, "💥 Влияние", item, "impact");
        if (item.TryGetProperty("impactProfile", out var impactProfile))
            AppendWorldNewsFlexibleValue(lines, "💥 Профиль влияния", impactProfile, "  ");

        lines.Add("");
    }

    private static void RenderNpcActivityNewsDetailed(List<string> lines, JsonElement item)
    {
        var npcName = GetStr(item, "NPCName", GetStr(item, "npcName", GetStr(item, "name", "")));
        if (string.IsNullOrEmpty(npcName)) return;

        JsonElement details = item;
        if (item.TryGetProperty("activityUpdate", out var upd) && upd.ValueKind == JsonValueKind.Object)
            details = upd;

        var actName = GetStr(details, "activityName", GetStr(details, "name", GetStr(item, "activityName", GetStr(item, "activity", ""))));
        if (string.IsNullOrEmpty(actName)) return;

        var activeState = GetStr(details, "activeState", GetStr(details, "status", GetStr(item, "activeState", GetStr(item, "status", ""))));
        var stColor = activeState.ToLowerInvariant() switch
        {
            "completed" => "green",
            "abandoned" => "red",
            _ => "yellow"
        };

        var line = $"  👤 [white]{Markup.Escape(npcName)}[/] → ⚡ [cyan]{Markup.Escape(actName)}[/]";
        if (!string.IsNullOrEmpty(activeState))
            line += $" [{stColor}]({Markup.Escape(activeState)})[/]";
        lines.Add(line);

        var desc = GetStr(details, "description", GetStr(item, "description", ""));
        if (!string.IsNullOrEmpty(desc))
            lines.Add($"    [white]{Markup.Escape(desc)}[/]");

        var location = GetStr(details, "location", GetStr(item, "location", GetStr(details, "locationId", "")));
        if (!string.IsNullOrEmpty(location))
            lines.Add($"    📍 [dim]{Markup.Escape(location)}[/]");

        var step = GetInt(details, "currentStep", 0);
        var totalSteps = GetInt(details, "totalSteps", 0);
        if (totalSteps > 0)
            lines.Add($"    📊 Этапы: [cyan]{step}/{totalSteps}[/]");

        var spent = GetInt(details, "timeSpentMinutes", 0);
        var totalTime = GetInt(details, "totalTimeCostMinutes", 0);
        if (totalTime > 0)
            lines.Add($"    🕐 Время: [cyan]{FormatMinutes(spent)}[/] / [cyan]{FormatMinutes(totalTime)}[/]");

        var narrative = GetStr(item, "narrativeSummary", GetStr(details, "narrativeSummary", ""));
        if (!string.IsNullOrEmpty(narrative))
            lines.Add($"    📝 [dim]{Markup.Escape(narrative)}[/]");
    }

    private static void RenderFactionProjectNewsDetailed(List<string> lines, JsonElement item)
    {
        var factionName = GetStr(item, "factionName", GetStr(item, "name", ""));
        var projectName = GetStr(item, "projectName", GetStr(item, "name", ""));
        if (string.IsNullOrEmpty(projectName)) return;

        var state = GetStr(item, "activeState", GetStr(item, "finalState", GetStr(item, "status", "")));
        var stColor = state.ToLowerInvariant() switch
        {
            "completed" => "green",
            "abandoned" => "red",
            _ => "yellow"
        };

        var line = $"  🏛️ [white]{Markup.Escape(factionName)}[/] → 🔨 [orange1]{Markup.Escape(projectName)}[/]";
        if (!string.IsNullOrEmpty(state))
            line += $" [{stColor}]({Markup.Escape(state)})[/]";
        lines.Add(line);

        var desc = GetStr(item, "description", "");
        if (!string.IsNullOrEmpty(desc))
            lines.Add($"    [white]{Markup.Escape(desc)}[/]");

        var narrative = GetStr(item, "narrativeSummary", GetStr(item, "outcome", ""));
        if (!string.IsNullOrEmpty(narrative))
            lines.Add($"    📝 [dim]{Markup.Escape(narrative)}[/]");

        var step = GetInt(item, "currentStep", 0);
        var totalSteps = GetInt(item, "totalSteps", 0);
        if (totalSteps > 0)
            lines.Add($"    📊 Этапы: [cyan]{step}/{totalSteps}[/]");

        var timeSpent = GetInt(item, "timeSpentMinutes", 0);
        var timeTotal = GetInt(item, "totalTimeCostMinutes", 0);
        if (timeTotal > 0)
            lines.Add($"    🕐 Время: [cyan]{FormatMinutes(timeSpent)}[/] / [cyan]{FormatMinutes(timeTotal)}[/]");

        var etaTurn = GetStr(item, "estimatedCompletionTurn", "");
        if (!string.IsNullOrEmpty(etaTurn))
            lines.Add($"    ⏳ Примерное завершение: [dim]ход {Markup.Escape(etaTurn)}[/]");

        var canAssist = item.TryGetProperty("playerCanAssist", out var assist) && assist.ValueKind == JsonValueKind.True;
        var assistDesc = GetStr(item, "assistDescription", "");
        if (canAssist || !string.IsNullOrEmpty(assistDesc))
        {
            var assistLine = canAssist ? "    🤝 [green]Игрок может помочь[/]" : "    🤝 [dim]Помощь игрока[/]";
            if (!string.IsNullOrEmpty(assistDesc))
                assistLine += $" — {Markup.Escape(assistDesc)}";
            lines.Add(assistLine);
        }

        if (item.TryGetProperty("totalResourceCost", out var rc))
            AppendWorldNewsFlexibleValue(lines, "💰 Стоимость", rc, "    ");
        if (item.TryGetProperty("resourcesSpent", out var rs))
            AppendWorldNewsFlexibleValue(lines, "📉 Потрачено", rs, "    ");
    }

    private static void RenderWorldProgressNewsDetailed(List<string> lines, JsonElement item, string scopeLabel)
    {
        var name = GetStr(item, "trackerName", GetStr(item, "name", "?"));
        var cur = GetStr(item, "currentValue", GetStr(item, "progress", "0"));
        var max = GetStr(item, "maxValue", GetStr(item, "target", ""));
        var line = $"  {scopeLabel} → 📈 [white]{Markup.Escape(name)}[/]: [cyan]{Markup.Escape(cur)}[/]" +
            (!string.IsNullOrEmpty(max) ? $"/{Markup.Escape(max)}" : "");
        lines.Add(line);

        var desc = GetStr(item, "description", GetStr(item, "summary", ""));
        if (!string.IsNullOrEmpty(desc))
            lines.Add($"    [white]{Markup.Escape(desc)}[/]");

        var stage = GetStr(item, "stageName", GetStr(item, "currentStage", GetStr(item, "stage", "")));
        if (!string.IsNullOrEmpty(stage))
            lines.Add($"    🏷️ Стадия: [yellow]{Markup.Escape(stage)}[/]");

        var reason = GetStr(item, "changeReason", GetStr(item, "lastChangeReason", GetStr(item, "reason", "")));
        if (!string.IsNullOrEmpty(reason))
            lines.Add($"    📝 [dim]{Markup.Escape(reason)}[/]");

        var milestone = GetStr(item, "nextMilestone", GetStr(item, "milestone", ""));
        if (!string.IsNullOrEmpty(milestone))
            lines.Add($"    🎯 Следующая веха: [dim]{Markup.Escape(milestone)}[/]");
    }

    private static void AppendWorldNewsFlexibleField(List<string> lines, string label, JsonElement parent, params string[] propNames)
    {
        foreach (var propName in propNames)
        {
            if (parent.TryGetProperty(propName, out var value))
            {
                AppendWorldNewsFlexibleValue(lines, label, value, "  ");
                return;
            }
        }
    }

    private static void AppendWorldNewsFlexibleValue(List<string> lines, string label, JsonElement value, string indent)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                var text = value.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(text))
                    lines.Add($"{indent}{label}: [white]{Markup.Escape(text)}[/]");
                break;

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                lines.Add($"{indent}{label}: [white]{Markup.Escape(value.ToString())}[/]");
                break;

            case JsonValueKind.Array:
                if (value.GetArrayLength() == 0) return;
                lines.Add($"{indent}{label}:");
                foreach (var item in value.EnumerateArray())
                {
                    var itemText = ExtractWorldNewsDisplayText(item);
                    if (!string.IsNullOrWhiteSpace(itemText))
                        lines.Add($"{indent}  • [white]{Markup.Escape(itemText)}[/]");
                }
                break;

            case JsonValueKind.Object:
                var objectText = ExtractWorldNewsDisplayText(value);
                if (!string.IsNullOrWhiteSpace(objectText))
                    lines.Add($"{indent}{label}: [white]{Markup.Escape(objectText)}[/]");
                else
                {
                    var inner = new List<string>();
                    RenderExtraFields(inner, value, Array.Empty<string>(), $"{indent}  ");
                    if (inner.Count > 0)
                    {
                        lines.Add($"{indent}{label}:");
                        lines.AddRange(inner);
                    }
                }
                break;
        }
    }

    private static string ExtractWorldNewsDisplayText(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            JsonValueKind.Object => GetStr(value, "name",
                GetStr(value, "title",
                    GetStr(value, "eventTitle",
                        GetStr(value, "summary",
                            GetStr(value, "description",
                                GetStr(value, "content",
                                    GetStr(value, "factionName",
                                        GetStr(value, "locationName",
                                            GetStr(value, "npcName",
                                                GetStr(value, "value", value.ToString())))))))))),
            _ => ""
        };
    }

    /// <summary>Formats minutes into a human-readable string (e.g. "2ч 30м").</summary>
    private static string FormatMinutes(int totalMinutes)
    {
        if (totalMinutes < 60) return $"{totalMinutes}м";
        var hours = totalMinutes / 60;
        var mins = totalMinutes % 60;
        if (hours < 24) return mins > 0 ? $"{hours}ч {mins}м" : $"{hours}ч";
        var days = hours / 24;
        hours %= 24;
        return hours > 0 ? $"{days}д {hours}ч" : $"{days}д";
    }

    private async Task RenderAsciiMap(int playerX, int playerY, int playerZ, string playerLocName, JsonElement curLoc, JsonDocument? mapDoc)
    {
        // Collect all known points on this z-level
        var points = new Dictionary<(int x, int y), (string name, bool isCurrent)>();
        points[(playerX, playerY)] = (playerLocName, true);

        // From adjacencyMap of current location
        if (curLoc.TryGetProperty("adjacencyMap", out var adj) && adj.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in adj.EnumerateArray())
            {
                var tx = playerX; var ty = playerY; var tz = playerZ;
                if (entry.TryGetProperty("targetCoordinates", out var tc))
                {
                    tx = GetInt(tc, "x", playerX); ty = GetInt(tc, "y", playerY); tz = GetInt(tc, "z", playerZ);
                }
                if (tz == playerZ && !points.ContainsKey((tx, ty)))
                    points[(tx, ty)] = (GetStr(entry, "name", "?"), false);
            }
        }

        // From world_map newLocations on same z-level
        if (mapDoc != null && mapDoc.RootElement.TryGetProperty("newLocations", out var newLocs) && newLocs.ValueKind == JsonValueKind.Array)
        {
            foreach (var loc in newLocs.EnumerateArray())
            {
                if (loc.TryGetProperty("coordinates", out var lc))
                {
                    var lx = GetInt(lc, "x", 0); var ly = GetInt(lc, "y", 0); var lz = GetInt(lc, "z", 0);
                    if (lz == playerZ && !points.ContainsKey((lx, ly)))
                        points[(lx, ly)] = (GetStr(loc, "name", "?"), false);
                }
            }
        }

        if (points.Count < 2) return; // No map to show with only 1 point

        // Determine bounds
        var minX = points.Keys.Min(p => p.x);
        var maxX = points.Keys.Max(p => p.x);
        var minY = points.Keys.Min(p => p.y);
        var maxY = points.Keys.Max(p => p.y);

        // Clamp to reasonable size (±5 from player)
        minX = Math.Max(minX, playerX - 5); maxX = Math.Min(maxX, playerX + 5);
        minY = Math.Max(minY, playerY - 5); maxY = Math.Min(maxY, playerY + 5);

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;

        // Build grid — note: Y increases going North, so render top-to-bottom = maxY first
        var lines = new List<string>();
        var legend = new List<string>();
        int legendIdx = 1;
        var legendMap = new Dictionary<(int, int), int>();

        for (int y = maxY; y >= minY; y--)
        {
            var row = "";
            for (int x = minX; x <= maxX; x++)
            {
                if (points.TryGetValue((x, y), out var pt))
                {
                    if (pt.isCurrent)
                        row += "[bold green][@][/]";
                    else
                    {
                        legendMap[(x, y)] = legendIdx;
                        row += $"[cyan][{legendIdx}][/]";
                        legend.Add($"  [cyan][{legendIdx}][/] {Markup.Escape(pt.name)}");
                        legendIdx++;
                    }
                }
                else
                {
                    row += "[dim] · [/]";
                }
            }
            lines.Add(row);
        }

        // Compass labels
        var mapText = new List<string>();
        var centerPad = new string(' ', Math.Max(0, (width * 3) / 2 - 1));
        mapText.Add($"{centerPad}[dim]N[/]");
        mapText.Add($"{centerPad}[dim]↑[/]");
        foreach (var line in lines) mapText.Add($"  {line}");
        mapText.Add($"{centerPad}[dim]↓[/]");
        mapText.Add($"{centerPad}[dim]S[/]");
        if (legend.Count > 0)
        {
            mapText.Add("");
            mapText.Add($"[bold green][[@]][/] = Вы здесь");
            mapText.AddRange(legend);
        }

        var zLabel = playerZ > 0 ? $"↑{playerZ}" : playerZ < 0 ? $"↓{Math.Abs(playerZ)}" : "наземный";

        WriteLine();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", mapText)))
        {
            Header = new PanelHeader($" 🗺 Карта (уровень: {zLabel}) ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1)
        });
    }

    private async Task ShowDetailedStatus()
    {
        await _stateManager.RefreshGameStateAsync();
        var state = _stateManager.CurrentState;
        if (state.IsInAfterlifeRealm)
        {
            await ShowAfterlifeDetailedStatusAsync(IsAfterlifeStatusAuditRequested(_currentCommandRemainder));
            return;
        }

        // ── Load supplementary data ──
        var expDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/experience.json");
        var itemsDoc = await _stateManager.LoadGameStateFileAsync("game_state/inventory/items.json");
        var weightDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/weight_calc.json");
        var transDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/transformation.json");
        var stealthDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/stealth.json");
        var scDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/status_changes.json");
        var effDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/effects.json");
        var wndDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/wounds.json");
        var customStatesDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/custom_states.json");

        // ── Extract level and XP ──
        int level = 1; int totalXp = 0; int xpForNext = 0;
        if (expDoc != null)
        {
            var er = expDoc.RootElement;
            level = GetInt(er, "level", GetInt(er, "playerLevel", 1));
            totalXp = GetInt(er, "totalExperience", 0);
            xpForNext = GetInt(er, "experienceForNextLevel", 0);
        }

        // ── Extract total money from player_status.json first, then items.json fallback ──
        int totalMoney = 0;
        var statusDoc = await _stateManager.LoadGameStateFileAsync("game_state/core/player_status.json");
        if (statusDoc != null)
            totalMoney = GetInt(statusDoc.RootElement, "money", 0);
        if (totalMoney == 0 && itemsDoc != null)
        {
            totalMoney = GetInt(itemsDoc.RootElement, "money", 0);
            if (totalMoney == 0 && itemsDoc.RootElement.TryGetProperty("resources", out var res) && res.ValueKind == JsonValueKind.Object)
                totalMoney = GetInt(res, "gold", GetInt(res, "money", GetInt(res, "coins", 0)));
        }

        // ── Extract weight ──
        int totalWeight = 0; int maxWeight = 0; bool isOverloaded = false;
        int additionalEnergyExpenditure = 0;
        if (weightDoc != null)
        {
            var wr = weightDoc.RootElement;
            totalWeight = GetInt(wr, "totalWeight", GetInt(wr, "currentWeight", 0));
            maxWeight = GetInt(wr, "maxWeight", GetInt(wr, "maximumWeight", 0));
            isOverloaded = wr.TryGetProperty("isOverloaded", out var ov) && ov.ValueKind == JsonValueKind.True;
            if (!isOverloaded && wr.TryGetProperty("overloaded", out var oldOv) && oldOv.ValueKind == JsonValueKind.True)
                isOverloaded = true;
            additionalEnergyExpenditure = GetInt(wr, "additionalEnergyExpenditure", 0);
        }
        else if (itemsDoc != null)
        {
            var ir = itemsDoc.RootElement;
            totalWeight = GetInt(ir, "totalWeight", 0);
            maxWeight = GetInt(ir, "maxWeight", 0);
            isOverloaded = ir.TryGetProperty("isOverloaded", out var ov) && ov.ValueKind == JsonValueKind.True;
        }

        // ── Extract auto-combat skill ──
        string autoCombatSkill = "";
        if (transDoc != null)
            autoCombatSkill = GetStr(transDoc.RootElement, "playerAutoCombatSkillChange", GetStr(transDoc.RootElement, "autoCombatSkill", ""));

        var content = new Grid().AddColumn(new GridColumn());

        var leftContent = new Grid().AddColumn(new GridColumn());
        var identityName = !string.IsNullOrWhiteSpace(state.CharacterName)
            ? state.CharacterName
            : state.SoulName;
        if (!string.IsNullOrWhiteSpace(identityName))
            leftContent.AddRow(new Markup($"[bold white]👤 {Markup.Escape(identityName)}[/]"));

        var identityTable = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn(new TableColumn("").NoWrap().Width(18))
            .AddColumn(new TableColumn(""));
        if (!string.IsNullOrWhiteSpace(state.CharacterRace))
            identityTable.AddRow(new Markup("[dim]Раса[/]"), new Markup($"[white]{Markup.Escape(state.CharacterRace)}[/]"));
        if (!string.IsNullOrWhiteSpace(state.CharacterClass))
            identityTable.AddRow(new Markup("[dim]Класс[/]"), new Markup($"[white]{Markup.Escape(state.CharacterClass)}[/]"));
        if (string.IsNullOrWhiteSpace(state.CharacterRace) &&
            string.IsNullOrWhiteSpace(state.CharacterClass) &&
            !string.IsNullOrWhiteSpace(state.SoulFormDescription))
        {
            identityTable.AddRow(new Markup("[dim]Форма души[/]"), new Markup($"[white]{Markup.Escape(state.SoulFormDescription)}[/]"));
        }
        identityTable.AddRow(new Markup("[dim]Уровень[/]"), new Markup($"[cyan]{level}[/]"));
        leftContent.AddRow(identityTable);

        var summaryTable = ConsoleLayout.CreateBarMetricTable(labelWidth: 18, barWidth: 18, valueWidth: 18);

        if (xpForNext > 0)
        {
            var xpPct = (int)Math.Clamp((long)totalXp * 100 / Math.Max(1, xpForNext), 0, 100);
            summaryTable.AddRow(
                new Markup("[yellow]Опыт[/]"),
                new Markup(ConsoleLayout.CreateBarFromPercent(xpPct, 18, "yellow")),
                new Markup($"[yellow]{totalXp}/{xpForNext} ({xpPct}%)[/]"),
                new Markup(string.Empty));
        }
        else if (totalXp > 0)
        {
            summaryTable.AddRow(
                new Markup("[yellow]Опыт[/]"),
                new Markup(""),
                new Markup($"[yellow]{totalXp}[/]"),
                new Markup(string.Empty));
        }

        var hpPctValue = int.TryParse(state.PlayerStatus.HealthPercentage.Replace("%", "").Trim(), out var hpV) ? hpV : 100;
        var enPctValue = int.TryParse(state.PlayerStatus.EnergyPercentage.Replace("%", "").Trim(), out var enV) ? enV : 100;
        var poPctValue = int.TryParse(state.PlayerStatus.PoisePercentage.Replace("%", "").Trim(), out var poV) ? poV : 100;
        summaryTable.AddRow(
            new Markup("[red]Здоровье[/]"),
            new Markup(ConsoleLayout.CreateBarFromPercent(hpPctValue, 18, hpPctValue > 60 ? "green" : hpPctValue > 30 ? "yellow" : "red")),
            new Markup($"[red]{Markup.Escape(state.PlayerStatus.HealthPercentage)}[/]"),
            new Markup(string.Empty));
        summaryTable.AddRow(
            new Markup("[cyan]Энергия[/]"),
            new Markup(ConsoleLayout.CreateBarFromPercent(enPctValue, 18, enPctValue > 60 ? "deepskyblue1" : enPctValue > 30 ? "yellow" : "red")),
            new Markup($"[cyan]{Markup.Escape(state.PlayerStatus.EnergyPercentage)}[/]"),
            new Markup(string.Empty));
        summaryTable.AddRow(
            new Markup("[blue]Равновесие[/]"),
            new Markup(ConsoleLayout.CreateBarFromPercent(poPctValue, 18, poPctValue > 60 ? "steelblue" : poPctValue > 30 ? "yellow" : "red")),
            new Markup($"[blue]{Markup.Escape(state.PlayerStatus.PoisePercentage)}[/]"),
            new Markup(string.Empty));
        summaryTable.AddRow(
            new Markup("[yellow]Состояние[/]"),
            new Markup(""),
            new Markup($"[yellow]{Markup.Escape(state.PlayerStatus.CurrentCondition)}[/]"),
            new Markup(string.Empty));

        if (totalMoney > 0)
            summaryTable.AddRow(new Markup("[gold1]Деньги[/]"), new Markup(""), new Markup($"[gold1]{totalMoney}[/]"), new Markup(string.Empty));

        if (maxWeight > 0)
        {
            var weightPct = Math.Clamp(totalWeight * 100 / Math.Max(1, maxWeight), 0, 100);
            var wColor = isOverloaded ? "red" : weightPct > 80 ? "yellow" : "green";
            summaryTable.AddRow(
                new Markup($"[{wColor}]Вес[/]"),
                new Markup(ConsoleLayout.CreateBarFromPercent(weightPct, 18, wColor)),
                new Markup($"[{wColor}]{totalWeight}/{maxWeight} кг{(isOverloaded ? " (ПЕРЕГРУЗКА)" : "")}[/]"),
                new Markup(string.Empty));
            if (additionalEnergyExpenditure > 0)
                summaryTable.AddRow(new Markup("[yellow]Доп. расход[/]"), new Markup(""), new Markup($"[yellow]+{additionalEnergyExpenditure}/ход[/]"), new Markup(string.Empty));
        }

        leftContent.AddRow(summaryTable);

        if (stealthDoc != null)
        {
            var sr = stealthDoc.RootElement;
            var isActive = (sr.TryGetProperty("isActive", out var ia) && ia.ValueKind == JsonValueKind.True)
                        || (sr.TryGetProperty("isHidden", out var ih) && ih.ValueKind == JsonValueKind.True);
            var detLevel = GetInt(sr, "detectionLevel", -1);
            var stDesc = GetStr(sr, "description", GetStr(sr, "state", ""));
            if (isActive || detLevel >= 0 || !string.IsNullOrEmpty(stDesc))
            {
                var stealthTable = ConsoleLayout.CreateBarMetricTable(labelWidth: 18, barWidth: 18, valueWidth: 18);

                if (detLevel >= 0)
                {
                    var (label, color) = detLevel switch
                    {
                        <= 25 => ("Невидим", "green"),
                        <= 50 => ("Незамечен", "green"),
                        <= 75 => ("Подозрение", "yellow"),
                        <= 99 => ("Тревога", "orange1"),
                        _ => ("Обнаружен", "red")
                    };
                    stealthTable.AddRow(
                        new Markup("[green]Скрытность[/]"),
                        new Markup(ConsoleLayout.CreateBarFromPercent(detLevel, 18, color)),
                        new Markup($"[{color}]{label} ({detLevel}%)[/]"),
                        new Markup(string.Empty));
                }
                else
                {
                    stealthTable.AddRow(new Markup("[green]Скрытность[/]"), new Markup(""), new Markup(isActive ? "[green]Скрыт[/]" : $"[dim]{Markup.Escape(stDesc)}[/]"), new Markup(string.Empty));
                }
                leftContent.AddRow(stealthTable);
            }
        }

        if (!string.IsNullOrEmpty(autoCombatSkill))
            leftContent.AddRow(new Markup($"[cyan]⚔ Авто-бой:[/] {Markup.Escape(autoCombatSkill)}"));

        if (state.PlayerStatus.ActiveConditions.Length > 0)
        {
            leftContent.AddRow(new Markup("[yellow]Активные состояния:[/]"));
            foreach (var c in state.PlayerStatus.ActiveConditions)
                leftContent.AddRow(new Markup($"[yellow]•[/] {Markup.Escape(c)}"));
        }

        // ── Right: characteristics (use computed if available) ──
        var rightContent = new Grid().AddColumn(new GridColumn());
        rightContent.AddRow(new Markup("[bold]Характеристики:[/]"));
        var compCharDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/computed_characteristics.json");
        var charDoc = await _stateManager.LoadGameStateFileAsync("game_state/misc/characteristics.json");
        // Try to extract base and modified values
        JsonElement? baseChars = null, permChars = null, modChars = null;
        var unspentStatPoints = 0;
        if (compCharDoc != null)
        {
            var cr = compCharDoc.RootElement;
            if (cr.TryGetProperty("characteristics", out var bc) && bc.ValueKind == JsonValueKind.Object) baseChars = bc;
            if (cr.TryGetProperty("permanentlyModifiedCharacteristics", out var pm) && pm.ValueKind == JsonValueKind.Object) permChars = pm;
            if (cr.TryGetProperty("modifiedCharacteristics", out var mc) && mc.ValueKind == JsonValueKind.Object) modChars = mc;
            unspentStatPoints = GetInt(cr, "unspentStatPoints", 0);
        }
        var charSource = modChars ?? permChars ?? baseChars ?? (charDoc != null ? charDoc.RootElement : (JsonElement?)null);
        int statPermCon = 0, statPermStr = 0, statPermInt = 0, statPermWis = 0, statPermFai = 0, statPermLuck = 0;
        if (charSource.HasValue)
        {
            var charTable = new Table()
                .Border(TableBorder.None)
                .HideHeaders()
                .Expand()
                .AddColumn(new TableColumn("").NoWrap().Width(18))
                .AddColumn(new TableColumn("").NoWrap().Width(12))
                .AddColumn(new TableColumn("").RightAligned().NoWrap().Width(10))
                .AddColumn(new TableColumn(""));

            foreach (var charName in Characteristics.All)
            {
                var ruName = Characteristics.RussianNames[charName];
                var modVal = charSource.Value.TryGetProperty(charName, out var mv) ? mv.GetInt32() : 0;
                var baseVal = baseChars.HasValue && baseChars.Value.TryGetProperty(charName, out var bv) ? bv.GetInt32() : modVal;
                var diff = modVal - baseVal;
                var diffStr = diff > 0 ? $" [green](+{diff})[/]" : diff < 0 ? $" [red]({diff})[/]" : "";
                var barClr = modVal >= 14 ? "green" : modVal >= 8 ? "yellow" : "red";
                var filled = Math.Clamp(modVal * 10 / 20, 0, 10);
                charTable.AddRow(
                    GameInterface.SafeMarkupText(ruName),
                    new Markup(ConsoleLayout.CreateBar(filled, 10, barClr)),
                    new Markup($"[white]{modVal}[/]{diffStr}"),
                    new Markup("[dim][/]")
                );
                // Cache for derived stats
                if (charName == Characteristics.Constitution) statPermCon = modVal;
                else if (charName == Characteristics.Strength) statPermStr = modVal;
                else if (charName == Characteristics.Intelligence) statPermInt = modVal;
                else if (charName == Characteristics.Wisdom) statPermWis = modVal;
                else if (charName == Characteristics.Faith) statPermFai = modVal;
                else if (charName == Characteristics.Luck) statPermLuck = modVal;
            }
            rightContent.AddRow(charTable);

            // ── Derived stats summary (compact) ──
            rightContent.AddRow(new Markup("[bold]Производные параметры:[/]"));
            var dMaxHp = 100 + statPermCon * 2 + statPermStr;
            var dMaxEn = 100 + (int)(statPermCon * 0.75) + (int)(statPermInt * 0.75) + (int)(statPermWis * 0.75) + (int)(statPermFai * 0.75);
            var dMaxPoise = 100 + (int)(statPermStr * 1.5) + (int)(statPermCon * 1.5) + (int)(statPermInt * 1.5) + (int)(statPermWis * 1.5);
            var dMaxWeight = 30 + (int)(statPermStr * 1.8 + statPermCon * 0.4);
            var dCritThr = 20 - statPermLuck / 20;
            var derivedTable = new Table()
                .Border(TableBorder.None)
                .HideHeaders()
                .Expand()
                .AddColumn(new TableColumn("").NoWrap().Width(18))
                .AddColumn(new TableColumn("").RightAligned().NoWrap().Width(16))
                .AddColumn(new TableColumn("").NoWrap().Width(18))
                .AddColumn(new TableColumn("").RightAligned().NoWrap().Width(16));
            derivedTable.AddRow(new Markup("[dim]Макс. здоровье[/]"), new Markup($"[red]{dMaxHp}%[/]"), new Markup("[dim]Энергия[/]"), new Markup($"[cyan]{dMaxEn}%[/]"));
            derivedTable.AddRow(new Markup("[dim]Равновесие[/]"), new Markup($"[blue]{dMaxPoise}%[/]"), new Markup("[dim]Вес[/]"), new Markup($"[white]{dMaxWeight} кг[/]"));
            derivedTable.AddRow(new Markup("[dim]Критический диапазон[/]"), new Markup($"[gold1]{dCritThr}-20[/]"), new Markup(""), new Markup(""));
            rightContent.AddRow(derivedTable);
            if (unspentStatPoints > 0)
                rightContent.AddRow(new Markup($"[green]Свободные очки: {unspentStatPoints}[/] [dim](/распределить)[/]"));
            rightContent.AddRow(new Markup("[dim](подробнее: /статы)[/]"));
        }
        else
        {
            rightContent.AddRow(new Markup("[dim]Нет данных[/]"));
        }

        // Effort tracker
        if (expDoc != null)
        {
            var er = expDoc.RootElement;
            if (er.TryGetProperty("playerEffortTrackerChange", out var eft) && eft.ValueKind == JsonValueKind.Object)
            {
                var lastChar = GetStr(eft, "lastUsedCharacteristic", "");
                var consec = GetInt(eft, "consecutivePartialSuccesses", 0);
                if (consec > 0 || !string.IsNullOrEmpty(lastChar))
                {
                    rightContent.AddRow(new Markup("[bold]📊 Трекер усилий:[/]"));
                    if (!string.IsNullOrEmpty(lastChar))
                    {
                        var ruChar = Characteristics.RussianNames.GetValueOrDefault(lastChar.ToLowerInvariant(), lastChar);
                        rightContent.AddRow(new Markup($"[cyan]Последняя характеристика:[/] {Markup.Escape(ruChar)}"));
                    }
                    rightContent.AddRow(new Markup($"[yellow]Частичных успехов:[/] {consec}/3"));
                }
            }
        }

        // Spectre.Console 0.49 can hang while measuring two expandable grid columns
        // that contain fixed/no-wrap child tables. Keep status sections vertical.
        content.AddRow(leftContent);
        content.AddRow(new Text(""));
        content.AddRow(rightContent);

        var panel = new Panel(content)
        {
            Header = new PanelHeader($" {_loc.T("status")} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        };

        Write(panel);

        // ── Additional panel: recent changes ──
        var extraText = new List<string>();

        // Transformation (appearance changes)
        if (transDoc != null)
        {
            var t = transDoc.RootElement;
            var appearance = GetStr(t, "playerAppearanceChange", "");
            var raceDesc = GetStr(t, "playerRaceDescriptionChange", "");
            var classDesc = GetStr(t, "playerClassDescriptionChange", "");
            if (!string.IsNullOrEmpty(appearance))
                extraText.Add($"🎭 Внешность: [white]{Markup.Escape(appearance)}[/]");
            if (!string.IsNullOrEmpty(raceDesc))
                extraText.Add($"🧬 Раса: [white]{Markup.Escape(raceDesc)}[/]");
            if (!string.IsNullOrEmpty(classDesc))
                extraText.Add($"⚔️ Класс: [white]{Markup.Escape(classDesc)}[/]");
        }

        // Status changes (deltas)
        if (scDoc != null)
        {
            var sc = scDoc.RootElement;
            var moneyDelta = GetInt(sc, "moneyChange", 0);
            var hpDelta = GetInt(sc, "currentHealthChange", 0);
            var energyDelta = GetInt(sc, "currentEnergyChange", 0);
            var poiseDelta = GetInt(sc, "currentPoiseChange", 0);
            if (moneyDelta != 0)
                extraText.Add($"💰 Деньги (последнее): [{(moneyDelta > 0 ? "green" : "red")}]{(moneyDelta > 0 ? "+" : "")}{moneyDelta}[/]");
            if (hpDelta != 0)
                extraText.Add($"❤️ Здоровье (последнее): [{(hpDelta > 0 ? "green" : "red")}]{(hpDelta > 0 ? "+" : "")}{hpDelta}[/]");
            if (energyDelta != 0)
                extraText.Add($"⚡ Энергия (последнее): [{(energyDelta > 0 ? "green" : "red")}]{(energyDelta > 0 ? "+" : "")}{energyDelta}[/]");
            if (poiseDelta != 0)
                extraText.Add($"🛡️ Равновесие (последнее): [{(poiseDelta > 0 ? "green" : "red")}]{(poiseDelta > 0 ? "+" : "")}{poiseDelta}[/]");
            var statsUp = FormatCharacteristicArray(sc, "statsIncreased");
            var statsDown = FormatCharacteristicArray(sc, "statsDecreased");
            if (!string.IsNullOrEmpty(statsUp))
                extraText.Add($"[green]📈 Повышены: {statsUp}[/]");
            if (!string.IsNullOrEmpty(statsDown))
                extraText.Add($"[red]📉 Понижены: {statsDown}[/]");
        }

        if (expDoc != null)
        {
            var xpDelta = GetInt(expDoc.RootElement, "experienceGained", 0);
            if (xpDelta != 0)
                extraText.Add($"✨ Опыт (последнее): [yellow]+{xpDelta}[/]");
        }

        // Combat alert — check if enemies exist
        var combatEnemDoc = await _stateManager.LoadGameStateFileAsync("game_state/combat/enemies.json");
        if (combatEnemDoc != null)
        {
            var enemyCount = 0;
            EnumerateJsonItems(combatEnemDoc.RootElement, _ => enemyCount++);
            if (enemyCount > 0)
                extraText.Add($"[bold red]⚔️ ВЫ В БОЮ! Врагов: {enemyCount}[/] [dim](подробнее: /бой)[/]");
        }

        if (effDoc != null)
        {
            if (extraText.Count > 0) extraText.Add("");
            AppendStatusEffectPreview(extraText, effDoc.RootElement);
        }

        if (wndDoc != null)
        {
            if (extraText.Count > 0) extraText.Add("");
            AppendStatusWoundPreview(extraText, wndDoc.RootElement);
        }

        if (customStatesDoc != null)
        {
            if (extraText.Count > 0) extraText.Add("");
            AppendStatusCustomStatePreview(extraText, customStatesDoc.RootElement);
        }

        var shiningBlessingLines = await ShiningBlessingEffectState.BuildStatusLinesAsync(_fs, _stateManager.CurrentState.TurnNumber);
        if (shiningBlessingLines.Count > 0)
        {
            if (extraText.Count > 0) extraText.Add("");
            extraText.Add("[bold gold1]✨ Благословения Сияющей Обители[/]");
            foreach (var line in shiningBlessingLines)
                extraText.Add($"[gold1]•[/] {Markup.Escape(line)}");
        }

        if (extraText.Count > 0)
        {
            WriteLine();
            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", extraText)))
            {
                Header = new PanelHeader(" 📊 Дополнительно ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Grey),
                Padding = new Padding(1, 0),
                Expand = true
            });
        }

        // Player portrait
        var playerImgPrompt = "";
        if (transDoc != null)
            playerImgPrompt = GetStr(transDoc.RootElement, "playerImagePromptChange",
                GetStr(transDoc.RootElement, "image_prompt", ""));
        await WaitForKeyWithImage("player", _stateManager.CurrentState.CharacterName ?? "player", playerImgPrompt, "player_portrait");
    }

    private async Task ShowSkills()
    {
        var activeDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/skills_active.json");
        var passiveDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/skills_passive.json");
        var masteryDoc = await _stateManager.LoadGameStateFileAsync("game_state/player/skill_mastery.json");

        // Build mastery lookup: skillName -> element
        var masteryLookup = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (masteryDoc != null)
        {
            if (masteryDoc.RootElement.TryGetProperty("skillMasteryChanges", out var masteryArr) &&
                masteryArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in masteryArr.EnumerateArray())
                {
                    var mName = GetStr(item, "skillName", GetStr(item, "name", ""));
                    if (!string.IsNullOrEmpty(mName))
                        masteryLookup[mName] = item;
                }
            }
            else
            {
                EnumerateJsonItems(masteryDoc.RootElement, item =>
                {
                    var mName = GetStr(item, "skillName", GetStr(item, "name", ""));
                    if (!string.IsNullOrEmpty(mName))
                        masteryLookup[mName] = item;
                });
            }
        }

        // Collect all skills: (displayLabel, element, isActive)
        var skills = new List<(string label, JsonElement el, bool isActive)>();

        if (activeDoc != null)
        {
            void AddActiveSkill(JsonElement item)
            {
                var name = GetStr(item, "skillName", GetStr(item, "name", "???"));
                var rarity = GetStr(item, "rarity", "");
                var sLvl = GetStr(item, "level", "");
                var cat = GetStr(item, "category", "");
                var catTag = cat switch
                {
                    "Magical" => "Магический",
                    "Combat" => "Боевой",
                    "Utility" => "Утилитарный",
                    _ => cat
                };
                skills.Add((ConsoleLayout.PlainChoiceLabel(
                    $"⚡ {name}",
                    string.IsNullOrEmpty(rarity) ? "" : FormatSkillRarityLabel(rarity),
                    string.IsNullOrEmpty(sLvl) ? "" : $"Уровень {sLvl}",
                    catTag), item, true));
            }

            if (activeDoc.RootElement.TryGetProperty("activeSkillChanges", out var activeArr) &&
                activeArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in activeArr.EnumerateArray())
                    AddActiveSkill(item);
            }
            else
            {
                EnumerateJsonItems(activeDoc.RootElement, AddActiveSkill);
            }
        }

        if (passiveDoc != null)
        {
            void AddPassiveSkill(JsonElement item)
            {
                var name = GetStr(item, "skillName", GetStr(item, "name", "???"));
                var rarity = GetStr(item, "rarity", "");
                var pType = GetStr(item, "type", "");
                var typeTag = pType switch
                {
                    "KnowledgeBased" => "Знание",
                    "CharacteristicBonus" => "Бонус к характеристике",
                    "BodyModification" => "Модификация тела",
                    "CombatEnhancement" => "Боевое улучшение",
                    "Utility" => "Утилитарный",
                    _ => pType
                };
                skills.Add((ConsoleLayout.PlainChoiceLabel(
                    $"🔮 {name}",
                    string.IsNullOrEmpty(rarity) ? "" : FormatSkillRarityLabel(rarity),
                    typeTag), item, false));
            }

            if (passiveDoc.RootElement.TryGetProperty("passiveSkillChanges", out var passiveArr) &&
                passiveArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in passiveArr.EnumerateArray())
                    AddPassiveSkill(item);
            }
            else
            {
                EnumerateJsonItems(passiveDoc.RootElement, AddPassiveSkill);
            }
        }

        if (skills.Count == 0)
        {
            ShowEmptyPanel(_loc.T("skills"), "Навыки не обнаружены");
            WaitForKey();
            return;
        }

        while (true)
        {
            var choices = new List<string>();
            foreach (var (label, _, _) in skills)
                choices.Add(label);
            choices.Add("← Назад");

            var selected = Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold yellow]🎓 {_loc.T("skills")}[/]")
                    .PageSize(15)
                    .AddChoices(choices));

            if (selected == "← Назад") break;

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= skills.Count) break;

            var (_, el, isActive) = skills[selIdx];
            if (isActive)
                await ShowActiveSkillDetailPanel(el, masteryLookup);
            else
                ShowPassiveSkillDetailPanel(el, masteryLookup);
        }
    }

    private async Task ShowActiveSkillDetailPanel(JsonElement s, Dictionary<string, JsonElement> masteryLookup)
    {
        var lines = new List<string>();
        var name = GetStr(s, "skillName", GetStr(s, "name", "???"));
        lines.Add($"[bold yellow]⚡ {Markup.Escape(name)}[/]");

        var rarity = GetStr(s, "rarity", "");
        if (!string.IsNullOrEmpty(rarity))
            lines.Add($"  ⭐ Качество: [{GetRarityColor(rarity)}]{Markup.Escape(FormatSkillRarityLabel(rarity))}[/]");

        var category = GetStr(s, "category", "");
        if (!string.IsNullOrEmpty(category))
        {
            var catLabel = category switch
            {
                "Magical" => "Магический",
                "Combat" => "Боевой",
                "Utility" => "Утилитарный",
                _ => category
            };
            lines.Add($"  📂 Категория: [cyan]{Markup.Escape(catLabel)}[/]");
        }

        var skillLevel = GetStr(s, "level", "");
        if (!string.IsNullOrEmpty(skillLevel))
            lines.Add($"  📊 Уровень навыка: [yellow]{Markup.Escape(skillLevel)}[/]");

        var desc = GetStr(s, "skillDescription", GetStr(s, "description", ""));
        if (!string.IsNullOrEmpty(desc))
            lines.Add($"  {Markup.Escape(desc)}");

        AppendStructuredSkillBonusLines(lines, s);

        lines.Add("");

        var actionCost = GetStr(s, "actionCost", "");
        if (!string.IsNullOrEmpty(actionCost))
        {
            var costColor = actionCost.ToLower() switch
            {
                "main" or "основное" => "red",
                "fast" or "быстрое" => "yellow",
                "free" or "свободное" => "green",
                _ => "white"
            };
            lines.Add($"  ⏱ Действие: [{costColor}]{Markup.Escape(FormatSkillActionCostLabel(actionCost))}[/]");
        }

        var energyCost = GetStr(s, "energyCost", "");
        if (!string.IsNullOrEmpty(energyCost))
            lines.Add($"  🔋 Энергия: [cyan]{Markup.Escape(energyCost)}[/]");

        var cooldown = GetStr(s, "cooldownTurns", "");
        if (!string.IsNullOrEmpty(cooldown) && cooldown != "0")
            lines.Add($"  ⏳ Перезарядка: [yellow]{Markup.Escape(cooldown)} ход(ов)[/]");

        var timeCost = GetStr(s, "timeCost", "");
        if (!string.IsNullOrEmpty(timeCost) && timeCost != "0")
            lines.Add($"  🕐 Время: [yellow]{FormatMinutes(int.TryParse(timeCost, out var tc) ? tc : 0)}[/]");

        var scaling = GetStr(s, "scalingCharacteristic", "");
        if (!string.IsNullOrEmpty(scaling))
            lines.Add($"  📈 Масштабирование: [cyan]{Markup.Escape(StructuredBonusDisplay.FormatCharacteristicName(scaling))}[/]");

        // Combat effect → effects[]
        if (s.TryGetProperty("combatEffect", out var ce) && ce.ValueKind == JsonValueKind.Object)
        {
            var targetPriority = GetStr(ce, "targetPriority", "");
            if (ce.TryGetProperty("effects", out var effects) && effects.ValueKind == JsonValueKind.Array && effects.GetArrayLength() > 0)
            {
                lines.Add("");
                lines.Add("  [bold]⚔ Боевые эффекты:[/]");
                if (!string.IsNullOrEmpty(targetPriority))
                    lines.Add($"    🎯 Приоритет цели: [white]{Markup.Escape(targetPriority)}[/]");
                foreach (var eff in effects.EnumerateArray())
                {
                    var effType = GetStr(eff, "effectType", "???");
                    var effVal = GetStr(eff, "value", "");
                    var effTarget = GetStr(eff, "targetTypeDisplayName", GetStr(eff, "targetType", ""));
                    var effDur = GetStr(eff, "duration", "");
                    var effDesc = GetStr(eff, "effectDescription", "");
                    var poiseDmg = GetStr(eff, "poiseDamage", "");
                    var tgtCount = GetStr(eff, "targetsCount", "");

                    var effLine = $"    • [cyan]{Markup.Escape(effType)}[/]";
                    if (!string.IsNullOrEmpty(effVal)) effLine += $" [yellow]{Markup.Escape(effVal)}[/]";
                    if (!string.IsNullOrEmpty(effTarget)) effLine += $" → {Markup.Escape(effTarget)}";
                    if (!string.IsNullOrEmpty(poiseDmg) && poiseDmg != "0") effLine += $" [dim](🛡️ -{Markup.Escape(poiseDmg)} стойк.)[/]";
                    if (!string.IsNullOrEmpty(tgtCount) && tgtCount != "1") effLine += $" [dim](×{Markup.Escape(tgtCount)} целей)[/]";
                    if (!string.IsNullOrEmpty(effDur)) effLine += $" [dim]({Markup.Escape(effDur)} ход.)[/]";
                    lines.Add(effLine);
                    if (!string.IsNullOrEmpty(effDesc))
                        lines.Add($"      [dim]{Markup.Escape(effDesc)}[/]");
                }
            }
        }

        // ── Scaling estimate: show calculated damage with player's current stats ──
        if (!string.IsNullOrEmpty(scaling) && _charService != null)
        {
            try
            {
                var computed = await _charService.ComputeAsync();
                var scalingLower = scaling.ToLowerInvariant();
                // Map Russian scaling names to English
                var scalingKey = scalingLower switch
                {
                    "сила" or "strength" => Characteristics.Strength,
                    "ловкость" or "dexterity" => Characteristics.Dexterity,
                    "выносливость" or "constitution" => Characteristics.Constitution,
                    "интеллект" or "intelligence" => Characteristics.Intelligence,
                    "мудрость" or "wisdom" => Characteristics.Wisdom,
                    "вера" or "faith" => Characteristics.Faith,
                    "привлекательность" or "attractiveness" => Characteristics.Attractiveness,
                    "торговля" or "trade" => Characteristics.Trade,
                    "убеждение" or "persuasion" => Characteristics.Persuasion,
                    "восприятие" or "perception" => Characteristics.Perception,
                    "удача" or "luck" => Characteristics.Luck,
                    "скорость" or "speed" => Characteristics.Speed,
                    _ => scalingLower
                };

                if (computed.Stats.TryGetValue(scalingKey, out var scaleStat))
                {
                    var charVal = scaleStat.PermanentlyModified + scaleStat.TemporaryBonus;
                    var lvl = computed.PlayerLevel;
                    var mastery = GetSkillMasterySnapshot(name, s, masteryLookup);
                    var mastLvl = mastery.Level > 0 ? mastery.Level : 1;

                    // CharBonusPercent = floor(charVal / 10) * 5
                    var charBonusPct = charVal / 10 * 5;
                    // LevelBonusPercent = floor(level / 5) * 8
                    var lvlBonusPct = lvl / 5 * 8;
                    // MasteryBonusPercent = masteryLevel * 4
                    var mastBonusPct = mastLvl * 4;
                    var totalMultiplier = 1.0 + charBonusPct / 100.0 + lvlBonusPct / 100.0 + mastBonusPct / 100.0;

                    lines.Add("");
                    lines.Add("  [bold green]📐 Расчёт масштабирования (Block 7):[/]");
                    var ruScaling = Characteristics.RussianNames.GetValueOrDefault(scalingKey, scaling);
                    lines.Add($"    {Markup.Escape(ruScaling)}: [white]{charVal}[/] → бонус [green]+{charBonusPct}%[/] [dim](значение/10 × 5)[/]");
                    lines.Add($"    Уровень: [white]{lvl}[/] → бонус [green]+{lvlBonusPct}%[/] [dim](уровень/5 × 8)[/]");
                    lines.Add($"    Мастерство: [white]{mastLvl}[/] → бонус [green]+{mastBonusPct}%[/] [dim](мастерство × 4)[/]");
                    lines.Add($"    [bold]Итого множитель: [yellow]×{totalMultiplier:F2}[/][/] [dim](базовый эффект × {totalMultiplier:F2})[/]");

                    // Try to show actual estimated damage for Damage effects
                    if (s.TryGetProperty("combatEffect", out var ce2) && ce2.ValueKind == JsonValueKind.Object
                        && ce2.TryGetProperty("effects", out var effs2) && effs2.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var eff in effs2.EnumerateArray())
                        {
                            var eType = GetStr(eff, "effectType", "").ToLowerInvariant();
                            if (!eType.Contains("damage") && !eType.Contains("heal") && !eType.Contains("урон")) continue;
                            var baseValStr = GetStr(eff, "value", "");
                            if (int.TryParse(baseValStr.Replace("%", "").Trim(), out var baseVal))
                            {
                                var scaledVal = (int)Math.Round(baseVal * totalMultiplier);
                                lines.Add($"    → [bold]{Markup.Escape(GetStr(eff, "effectType", "???"))}[/]: база {baseVal}% × {totalMultiplier:F2} = [bold yellow]{scaledVal}%[/]");
                            }
                        }
                    }
                }
            }
            catch { /* If char service fails, just skip scaling estimate */ }
        }

        // Scaling flags
        var scalesVal = s.TryGetProperty("scalesValue", out var svf) && svf.ValueKind == JsonValueKind.True;
        var scalesDur = s.TryGetProperty("scalesDuration", out var sdf) && sdf.ValueKind == JsonValueKind.True;
        var scalesChn = s.TryGetProperty("scalesChance", out var scf) && scf.ValueKind == JsonValueKind.True;
        if (scalesVal || scalesDur || scalesChn)
        {
            var flags = new List<string>();
            if (scalesVal) flags.Add("значение");
            if (scalesDur) flags.Add("длительность");
            if (scalesChn) flags.Add("шанс");
            lines.Add($"  [dim]Масштабируется: {string.Join(", ", flags)}[/]");
        }

        // Mastery
        AppendMasteryInfo(lines, name, s, masteryLookup);

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader($" ⚡ {Markup.Escape(name)} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(1, 1),
            Expand = true
        });
        WaitForKey();
    }

    private void ShowPassiveSkillDetailPanel(JsonElement s, Dictionary<string, JsonElement> masteryLookup)
    {
        var lines = new List<string>();
        var name = GetStr(s, "skillName", GetStr(s, "name", "???"));
        lines.Add($"[bold blue]🔮 {Markup.Escape(name)}[/]");

        var rarity = GetStr(s, "rarity", "");
        if (!string.IsNullOrEmpty(rarity))
            lines.Add($"  ⭐ Качество: [{GetRarityColor(rarity)}]{Markup.Escape(FormatSkillRarityLabel(rarity))}[/]");

        var type = GetStr(s, "type", "");
        if (!string.IsNullOrEmpty(type))
        {
            var typeLabel = type switch
            {
                "KnowledgeBased" => "Знание",
                "CharacteristicBonus" => "Бонус к характеристике",
                "BodyModification" => "Модификация тела",
                "CombatEnhancement" => "Боевое улучшение",
                "Utility" => "Утилита",
                _ => type
            };
            lines.Add($"  📂 Тип: [cyan]{Markup.Escape(typeLabel)}[/]");
        }

        var group = GetStr(s, "group", "");
        if (!string.IsNullOrEmpty(group))
            lines.Add($"  🏷 Группа: [cyan]{Markup.Escape(group)}[/]");

        var desc = GetStr(s, "skillDescription", GetStr(s, "description", ""));
        if (!string.IsNullOrEmpty(desc))
        {
            lines.Add("");
            lines.Add($"  {Markup.Escape(desc)}");
        }

        AppendStructuredSkillBonusLines(lines, s);

        // Fallback: playerStatBonus (legacy summary) when structuredBonuses is absent
        if (!(s.TryGetProperty("structuredBonuses", out _)))
        {
            var statBonus = GetStr(s, "playerStatBonus", "");
            if (!string.IsNullOrEmpty(statBonus))
            {
                lines.Add("");
                lines.Add($"  [bold]📊 Бонус:[/] [green]{Markup.Escape(statBonus)}[/]");
            }
        }

        // Effect details
        var effectDetails = GetStr(s, "effectDetails", "");
        if (!string.IsNullOrEmpty(effectDetails))
        {
            lines.Add("");
            lines.Add($"  [bold]✨ Эффект:[/] {Markup.Escape(effectDetails)}");
        }

        // Knowledge domain
        var knowledgeDomain = GetStr(s, "knowledgeDomain", "");
        if (!string.IsNullOrEmpty(knowledgeDomain))
            lines.Add($"  📚 Область знаний: [cyan]{Markup.Escape(knowledgeDomain)}[/]");

        // Unlocked active skills
        var unlockedCount = GetStr(s, "unlockedActiveSkillsCount", "");
        var maxUnlock = GetStr(s, "maxUnlockableActiveSkills", "");
        if (!string.IsNullOrEmpty(unlockedCount) || !string.IsNullOrEmpty(maxUnlock))
            lines.Add($"  🔓 Активных навыков: [yellow]{(string.IsNullOrEmpty(unlockedCount) ? "0" : Markup.Escape(unlockedCount))}[/] / [yellow]{(string.IsNullOrEmpty(maxUnlock) ? "?" : Markup.Escape(maxUnlock))}[/]");

        // Combat effect (if passive has one)
        if (s.TryGetProperty("combatEffect", out var ce) && ce.ValueKind == JsonValueKind.Object)
        {
            var ceDesc = GetStr(ce, "description", GetStr(ce, "effectDescription", ""));
            if (!string.IsNullOrEmpty(ceDesc))
            {
                lines.Add("");
                lines.Add($"  [bold]⚔ Боевой эффект:[/] {Markup.Escape(ceDesc)}");
            }
            // Effects array
            if (ce.TryGetProperty("effects", out var efx) && efx.ValueKind == JsonValueKind.Array && efx.GetArrayLength() > 0)
            {
                foreach (var ef in efx.EnumerateArray())
                {
                    var efType = GetStr(ef, "effectType", GetStr(ef, "type", "?"));
                    var efVal = GetStr(ef, "value", "");
                    var efTarget = GetStr(ef, "targetTypeDisplayName", GetStr(ef, "targetType", GetStr(ef, "target", "")));
                    var efDesc = GetStr(ef, "effectDescription", "");
                    var efLine = $"    • [cyan]{Markup.Escape(efType)}[/]";
                    if (!string.IsNullOrEmpty(efVal)) efLine += $": [yellow]{Markup.Escape(efVal)}[/]";
                    if (!string.IsNullOrEmpty(efTarget)) efLine += $" [dim]({Markup.Escape(efTarget)})[/]";
                    lines.Add(efLine);
                    if (!string.IsNullOrEmpty(efDesc))
                        lines.Add($"      [dim]{Markup.Escape(efDesc)}[/]");
                }
            }
        }

        // Mastery
        AppendMasteryInfo(lines, name, s, masteryLookup);

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader($" 🔮 {Markup.Escape(name)} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue),
            Padding = new Padding(1, 1),
            Expand = true
        });
        WaitForKey();
    }

    private static void AppendStructuredSkillBonusLines(List<string> lines, JsonElement skill)
    {
        if (!skill.TryGetProperty("structuredBonuses", out var bonuses) ||
            bonuses.ValueKind != JsonValueKind.Array ||
            bonuses.GetArrayLength() == 0)
        {
            return;
        }

        lines.Add("");
        lines.Add("  [bold]📊 Структурные бонусы:[/]");
        var index = 0;
        foreach (var bonus in bonuses.EnumerateArray())
        {
            index++;
            if (bonus.ValueKind != JsonValueKind.Object)
            {
                lines.Add($"    • [green]{Markup.Escape(StructuredBonusDisplay.FormatValue(bonus))}[/]");
                continue;
            }

            var summary = GetStr(bonus, "summary", "");
            var title = string.IsNullOrWhiteSpace(summary)
                ? $"Бонус #{index}"
                : summary;
            lines.Add($"    • [green]{Markup.Escape(title)}[/]");
            foreach (var property in bonus.EnumerateObject())
            {
                lines.Add(
                    $"      [dim]{Markup.Escape(StructuredBonusDisplay.FieldLabel(property.Name))}:[/] [white]{Markup.Escape(StructuredBonusDisplay.FormatValue(property.Value, property.Name))}[/]");
            }
        }
    }

    private void AppendMasteryInfo(List<string> lines, string skillName, JsonElement s, Dictionary<string, JsonElement> masteryLookup)
    {
        var mastery = GetSkillMasterySnapshot(skillName, s, masteryLookup);

        if (mastery.Level > 0 || mastery.Needed > 0)
        {
            lines.Add("");
            var masteryLine = $"  📈 Мастерство: [bold cyan]{mastery.Level}[/]";
            if (mastery.MaxLevel > 0)
                masteryLine += $" / {mastery.MaxLevel}";
            lines.Add(masteryLine);

            if (mastery.Needed > 0)
            {
                var pct = Math.Min(100, mastery.Progress * 100 / Math.Max(1, mastery.Needed));
                lines.Add($"  Прогресс мастерства: {ConsoleLayout.CreateBarFromPercent(pct, 10, "cyan")} {mastery.Progress}/{mastery.Needed} ({pct}%)");
            }
        }
    }

    private static SkillMasterySnapshot GetSkillMasterySnapshot(
        string skillName,
        JsonElement skill,
        Dictionary<string, JsonElement> masteryLookup)
    {
        var level = GetFirstInt(skill, 0, "currentMasteryLevel", "masteryLevel");
        var progress = GetFirstInt(skill, 0, "currentMasteryProgress", "masteryProgress", "progress");
        var needed = GetFirstInt(skill, 0, "masteryProgressNeeded", "progressNeeded");
        var maxLevel = GetFirstInt(skill, 0, "maxMasteryLevel");

        if (masteryLookup.TryGetValue(skillName, out var mastery))
        {
            if (level <= 0)
                level = GetFirstInt(mastery, 0, "newMasteryLevel", "currentMasteryLevel", "currentMastery", "level");
            if (progress <= 0)
                progress = GetFirstInt(mastery, 0, "newCurrentMasteryProgress", "currentMasteryProgress", "masteryProgress", "progress");
            if (needed <= 0)
                needed = GetFirstInt(mastery, 0, "newMasteryProgressNeeded", "masteryProgressNeeded", "progressNeeded");
            if (maxLevel <= 0)
                maxLevel = GetFirstInt(mastery, 0, "newMaxMasteryLevel", "maxMasteryLevel");
        }

        if (level <= 0 && (progress > 0 || needed > 0))
            level = 1;

        return new SkillMasterySnapshot(level, progress, needed, maxLevel);
    }

    private static int GetFirstInt(JsonElement source, int fallback, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetInt(source, propertyName, int.MinValue);
            if (value != int.MinValue)
                return value;
        }

        return fallback;
    }

    private static string FormatSkillRarityLabel(string rarity) =>
        (rarity ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "common" or "обычный" or "обычная" or "обычное" => "обычное",
            "good" or "хороший" or "хорошая" or "хорошее" => "хорошее",
            "uncommon" or "необычный" or "необычная" or "необычное" => "необычное",
            "rare" or "редкий" or "редкая" or "редкое" => "редкое",
            "epic" or "эпический" or "эпическая" or "эпическое" => "эпическое",
            "legendary" or "легендарный" or "легендарная" or "легендарное" => "легендарное",
            "unique" or "уникальный" or "уникальная" or "уникальное" => "уникальное",
            _ => rarity ?? string.Empty
        };

    private static string FormatSkillActionCostLabel(string actionCost) =>
        (actionCost ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "main" or "основное" => "основное",
            "fast" or "быстрое" => "быстрое",
            "free" or "свободное" => "свободное",
            _ => actionCost ?? string.Empty
        };

    private readonly record struct SkillMasterySnapshot(int Level, int Progress, int Needed, int MaxLevel);

    private async Task ShowPlayerStats()
    {
        if (_charService == null)
        {
            ShowEmptyPanel(_loc.T("stats"), "Сервис характеристик недоступен");
            WaitForKey();
            return;
        }

        var result = await _charService.ComputeAsync();
        if (result.Stats.Count == 0)
        {
            ShowEmptyPanel(_loc.T("stats"), "Характеристики не определены");
            return;
        }

        var hasBonuses = result.Stats.Values.Any(s => s.PermanentBonus != 0 || s.TemporaryBonus != 0);
        var lines = new List<string>();

        // Header with level and unspent points
        lines.Add($"  [bold]Уровень:[/] [cyan]{result.PlayerLevel}[/]" +
            (result.UnspentStatPoints > 0
                ? $"  │  [bold yellow]⭐ Нераспределённых очков: {result.UnspentStatPoints}[/] [dim](используйте /распределить)[/]"
                : ""));
        lines.Add("");

        // Build table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .Expand()
            .AddColumn(new TableColumn("[bold]Характеристика[/]").NoWrap());

        if (hasBonuses)
        {
            table.AddColumn(new TableColumn("[bold]Базовое значение[/]").Centered().NoWrap());
            table.AddColumn(new TableColumn("[bold]Постоянный бонус[/]").Centered().NoWrap());
            table.AddColumn(new TableColumn("[bold]Итоговое значение[/]").Centered().NoWrap());
        }
        else
        {
            table.AddColumn(new TableColumn("[bold]Значение[/]").Centered().NoWrap());
        }
        table.AddColumn(new TableColumn("[bold]Шкала[/]").NoWrap());
        table.AddColumn(new TableColumn("[bold]Описание[/]"));

        foreach (var charName in Characteristics.All)
        {
            if (!result.Stats.TryGetValue(charName, out var stat)) continue;
            var ruName = Characteristics.RussianNames[charName];
            var displayVal = hasBonuses ? stat.PermanentlyModified : stat.BaseValue;

            // Visual bar based on the permanently modified value
            int filled = Math.Clamp(displayVal / 5, 0, 20);
            int empty = 20 - filled;
            var barColor = displayVal switch
            {
                >= 80 => "gold1",
                >= 50 => "green",
                >= 25 => "yellow",
                _ => "grey"
            };
            var bar = ConsoleLayout.CreateBar(filled, 20, barColor);

            var desc = Characteristics.Descriptions.TryGetValue(charName, out var d) ? $"[dim]{Markup.Escape(d)}[/]" : "";

            if (hasBonuses)
            {
                var bonusStr = stat.PermanentBonus == 0
                    ? "[dim]—[/]"
                    : stat.PermanentBonus > 0
                        ? $"[green]+{stat.PermanentBonus}[/]"
                        : $"[red]{stat.PermanentBonus}[/]";
                var totalColor = stat.PermanentlyModified > stat.BaseValue ? "green" :
                    stat.PermanentlyModified < stat.BaseValue ? "red" : "white";

                // Add temp bonus indicator if present
                var totalStr = $"[bold {totalColor}]{stat.PermanentlyModified}[/]";
                if (stat.TemporaryBonus != 0)
                {
                    var tmpColor = stat.TemporaryBonus > 0 ? "aqua" : "red";
                    totalStr += $" [{tmpColor}]({(stat.TemporaryBonus > 0 ? "+" : "")}{stat.TemporaryBonus})[/]";
                }

                table.AddRow(ruName, $"[white]{stat.BaseValue}[/]", bonusStr, totalStr, bar, desc);
            }
            else
            {
                table.AddRow(ruName, $"[bold]{stat.BaseValue}[/]", bar, desc);
            }
        }

        // Add the table to lines
        lines.Add(""); // will be replaced by table rendering below

        // Render bonus sources detail if any exist
        var sourceLines = new List<string>();
        foreach (var charName in Characteristics.All)
        {
            if (!result.Stats.TryGetValue(charName, out var stat)) continue;
            if (stat.PermanentSources.Count == 0 && stat.TemporarySources.Count == 0) continue;

            var ruName = Characteristics.RussianNames[charName];
            sourceLines.Add($"  [bold]{Markup.Escape(ruName)}:[/]");
            foreach (var src in stat.PermanentSources)
            {
                var sign = src.Value > 0 ? "+" : "";
                sourceLines.Add($"    [green]📌 {Markup.Escape(src.Origin)}:[/] [white]{sign}{src.Value}[/] [dim](пост.)[/]");
            }
            foreach (var src in stat.TemporarySources)
            {
                var sign = src.Value > 0 ? "+" : "";
                sourceLines.Add($"    [aqua]⏳ {Markup.Escape(src.Origin)}:[/] [white]{sign}{src.Value}[/] [dim](врем.)[/]");
            }
        }

        // Render everything
        var headerPanel = new Panel(GameInterface.SafeMarkup(string.Join("\n", lines.Take(2))))
        {
            Border = BoxBorder.None,
            Padding = new Padding(0, 0)
        };
        Write(headerPanel);
        Write(table);

        if (sourceLines.Count > 0)
        {
            WriteLine();
            var detailPanel = new Panel(GameInterface.SafeMarkup(string.Join("\n", sourceLines)))
            {
                Header = new PanelHeader(" 📊 Источники бонусов ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Grey),
                Padding = new Padding(1, 0),
                Expand = true
            };
            Write(detailPanel);
        }

        // ── Derived combat parameters (from Rules Block 5, 13, 14) ──
        {
            int GetPerm(string name) => result.Stats.TryGetValue(name, out var s) ? s.PermanentlyModified : 0;
            int GetMod(string name) => result.Stats.TryGetValue(name, out var s) ? (s.PermanentlyModified + s.TemporaryBonus) : 0;
            var lvl = result.PlayerLevel;

            var permStr = GetPerm(Characteristics.Strength);
            var permDex = GetPerm(Characteristics.Dexterity);
            var permCon = GetPerm(Characteristics.Constitution);
            var permInt = GetPerm(Characteristics.Intelligence);
            var permWis = GetPerm(Characteristics.Wisdom);
            var permFai = GetPerm(Characteristics.Faith);
            var permLuck = GetPerm(Characteristics.Luck);
            var permSpd = GetPerm(Characteristics.Speed);

            var modStr = GetMod(Characteristics.Strength);
            var modDex = GetMod(Characteristics.Dexterity);
            var modCon = GetMod(Characteristics.Constitution);
            var modInt = GetMod(Characteristics.Intelligence);
            var modLuck = GetMod(Characteristics.Luck);
            var modSpd = GetMod(Characteristics.Speed);

            // MaxHealth% = 100 + floor(PermanentlyModifiedConstitution * 2.0) + floor(PermanentlyModifiedStrength * 1.0)
            var maxHp = 100 + (int)(permCon * 2.0) + permStr;
            // MaxEnergy% = 100 + floor(Con*0.75) + floor(Int*0.75) + floor(Wis*0.75) + floor(Faith*0.75)
            var maxEnergy = 100 + (int)(permCon * 0.75) + (int)(permInt * 0.75) + (int)(permWis * 0.75) + (int)(permFai * 0.75);
            // MaxPoise% = 100 + floor(Str*1.5) + floor(Con*1.5) + floor(Int*1.5) + floor(Wis*1.5)
            var maxPoise = 100 + (int)(permStr * 1.5) + (int)(permCon * 1.5) + (int)(permInt * 1.5) + (int)(permWis * 1.5);
            // Poise regen per turn
            var poiseRegen = 10 + maxPoise / 10;
            // MaxWeight = 30 + floor(Str*1.8 + Con*0.4)
            var maxWeightKg = 30 + (int)(permStr * 1.8 + permCon * 0.4);

            // Critical hit threshold (lower = better) - d20 must roll >= this
            var critThreshold = 20 - permLuck / 20;
            var critRange = critThreshold <= 20 ? $"{critThreshold}-20" : "20";
            // Critical damage multiplier
            var critBonusPct = modLuck / 2;
            var critMult = 1.5 + critBonusPct / 100.0;

            // Attack bonuses
            var levelAtkBonus = 5 + lvl / 10 * 2;
            var strAtkBonus = modStr / 2;   // floor(Str/2.5) but using int division for simplicity
            var dexAtkBonus = modDex / 2;
            var spdAtkBonus = modSpd / 2;
            // More precise: floor(X / 2.5)
            strAtkBonus = (int)(modStr / 2.5);
            dexAtkBonus = (int)(modDex / 2.5);
            spdAtkBonus = (int)(modSpd / 2.5);

            // Innate resistance
            var levelRes = lvl / 10 * 2;
            var conRes = modCon / 10;
            var innateRes = levelRes + conRes;

            var dLines = new List<string>();
            dLines.Add("[bold white]❤️ Пулы:[/]");
            dLines.Add($"  Максимальное здоровье:   [red]{maxHp}%[/]  [dim](100 + Выносливость×2 + Сила×1)[/]");
            dLines.Add($"  Максимальная энергия:    [cyan]{maxEnergy}%[/]  [dim](100 + Выносливость×0.75 + Интеллект×0.75 + Мудрость×0.75 + Вера×0.75)[/]");
            dLines.Add($"  Максимальное равновесие: [blue]{maxPoise}%[/]  [dim](100 + Сила×1.5 + Выносливость×1.5 + Интеллект×1.5 + Мудрость×1.5)[/]");
            dLines.Add($"  Восстановление равновесия: [blue]{poiseRegen}%/ход[/]  [dim](10 + Максимальное равновесие/10)[/]");
            dLines.Add($"  Грузоподъёмность: [white]{maxWeightKg} кг[/]  [dim](30 + Сила×1.8 + Выносливость×0.4)[/]");

            dLines.Add("");
            dLines.Add("[bold white]⚔️ Атака:[/]");
            dLines.Add($"  Бонус от уровня:  [yellow]+{levelAtkBonus}%[/]  [dim](5 + Уровень/10 × 2)[/]");
            dLines.Add($"  Тяжёлое оружие (Сила):        [orange3]+{strAtkBonus}%[/]  [dim](Сила / 2.5)[/]");
            dLines.Add($"  Точное и дальнобойное оружие: [green]+{dexAtkBonus}%[/]  [dim](Ловкость / 2.5)[/]");
            dLines.Add($"  Лёгкое оружие (Скорость):     [cyan]+{spdAtkBonus}%[/]  [dim](Скорость / 2.5)[/]");

            dLines.Add("");
            dLines.Add("[bold white]🎯 Критические удары:[/]");
            dLines.Add($"  Порог крит. удара: [gold1]{critRange}[/] на d20  [dim](20 − Удача/20)[/]");
            dLines.Add($"  Множитель крита:   [gold1]×{critMult:F2}[/]  [dim](1.5 + Удача/200)[/]");

            dLines.Add("");
            dLines.Add("[bold white]🛡️ Защита:[/]");
            dLines.Add($"  Врождённое сопротивление: [blue]{innateRes}%[/]  [dim](Уровень/10×2 + Выносливость/10)[/]");
            dLines.Add($"  [dim]+ бонусы брони, навыков и эффектов (до макс. 90%)[/]");

            WriteLine();
            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", dLines)))
            {
                Header = new PanelHeader(" 📐 Производные боевые параметры ", Justify.Center),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow),
                Padding = new Padding(2, 1),
                Expand = true
            });
        }

        WaitForKey();
    }

    private async Task ShowStatDistributionCommand()
    {
        if (_charService == null)
        {
            MarkupLine("[red]Сервис характеристик недоступен.[/]");
            WaitForKey();
            return;
        }

        var unspent = await _charService.GetUnspentStatPoints();
        if (unspent <= 0)
        {
            MarkupLine("[yellow]Нет нераспределённых очков характеристик.[/]");
            WaitForKey();
            return;
        }

        // Read current base stats
        var statsJson = await _stateManager.LoadGameStateFileAsync("game_state/misc/characteristics.json");
        if (statsJson == null)
        {
            MarkupLine("[red]Характеристики не найдены.[/]");
            WaitForKey();
            return;
        }

        var allStats = Characteristics.All;
        var russianNames = Characteristics.RussianNames;
        var currentValues = new int[allStats.Length];
        var allocated = new int[allStats.Length];

        for (int i = 0; i < allStats.Length; i++)
        {
            if (statsJson.RootElement.TryGetProperty(allStats[i], out var val) &&
                val.ValueKind == JsonValueKind.Number)
                currentValues[i] = val.TryGetInt32(out var iv) ? iv : 1;
            else
                currentValues[i] = 1;
        }

        int remaining = unspent;
        int selected = 0;

        while (remaining > 0)
        {
            Clear();
            Write(new Rule($"[gold1]Распределение очков ({remaining} осталось)[/]").RuleStyle("gold1"));
            WriteLine();

            var table = new Table().Expand().Border(TableBorder.Rounded);
            table.AddColumn("Характеристика");
            table.AddColumn(new TableColumn("База").Centered());
            table.AddColumn(new TableColumn("+").Centered());
            table.AddColumn(new TableColumn("= Итого").Centered());

            for (int i = 0; i < allStats.Length; i++)
            {
                var name = russianNames.GetValueOrDefault(allStats[i], allStats[i]);
                var baseVal = currentValues[i];
                var alloc = allocated[i];
                var total = baseVal + alloc;

                var marker = i == selected ? "► " : "  ";
                var nameStr = i == selected ? $"[bold cyan]{marker}{Markup.Escape(name)}[/]" : $"  {Markup.Escape(name)}";
                var allocStr = alloc > 0 ? $"[green]+{alloc}[/]" : "[dim]0[/]";
                var totalStr = alloc > 0 ? $"[bold]{total}[/]" : $"{total}";

                table.AddRow(nameStr, $"{baseVal}", allocStr, totalStr);
            }

            Write(table);
            MarkupLine("[dim]↑↓ выбор  →/+ добавить  ←/- убрать  Enter подтвердить[/]");

            var key = ReadKey();
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    selected = (selected - 1 + allStats.Length) % allStats.Length;
                    break;
                case ConsoleKey.DownArrow:
                    selected = (selected + 1) % allStats.Length;
                    break;
                case ConsoleKey.RightArrow:
                case ConsoleKey.OemPlus:
                    if (remaining > 0 && currentValues[selected] + allocated[selected] < 100)
                    {
                        allocated[selected]++;
                        remaining--;
                    }
                    break;
                case ConsoleKey.LeftArrow:
                case ConsoleKey.OemMinus:
                    if (allocated[selected] > 0)
                    {
                        allocated[selected]--;
                        remaining++;
                    }
                    break;
                case ConsoleKey.Enter:
                    if (remaining == 0 || Confirm($"[yellow]Осталось {remaining} очков. Оставить на потом?[/]"))
                        goto done;
                    break;
            }
        }

        done:
        // Apply allocations (send increments, not final values)
        var allocDict = new Dictionary<string, int>();
        for (int i = 0; i < allStats.Length; i++)
        {
            if (allocated[i] > 0)
                allocDict[allStats[i]] = allocated[i];
        }

        if (allocDict.Count > 0)
            await _charService.DistributePointsAsync(allocDict);
        MarkupLine("[green]✓ Характеристики обновлены![/]");
        WaitForKey();
    }

    /// <summary>Set a strategic directive for a companion NPC.</summary>
    private async Task SetCompanionDirective()
    {
        // Load NPC core data
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_core.json");
        if (doc == null) { ShowEmptyPanel("Компаньоны", "НПС не найдены"); return; }
        var renameMap = BuildNpcRenameMap(doc);

        // Collect companions (progressionType == "Companion")
        var companions = new List<(string originalName, string displayName, string npcId, string currentDirective)>();
        foreach (var item in CollectNpcListEntries(doc))
        {
            var progType = GetStr(item, "progressionType", "");
            if (progType.Equals("Companion", StringComparison.OrdinalIgnoreCase))
            {
                var name = GetStr(item, "name", "???");
                var displayName = ResolveNpcDisplayName(item, renameMap);
                var id = GetStr(item, "npcId", GetStr(item, "id", ""));
                var directive = GetStr(item, "playerCompanionDirective", "");
                companions.Add((name, displayName, id, directive));
            }
        }

        if (companions.Count == 0)
        {
            MarkupLine("[yellow]У вас нет активных компаньонов.[/]");
            WaitForKey();
            return;
        }

        // Select companion
        var choices = companions.Select(c =>
        {
            var directiveLabel = !string.IsNullOrEmpty(c.currentDirective)
                ? $"Текущая директива: {c.currentDirective}"
                : "Директива не задана";
            return ConsoleLayout.PlainChoiceLabel($"👤 {c.displayName}", directiveLabel);
        }).ToList();
        choices.Add("← Назад");

        var selected = Prompt(
            new SelectionPrompt<string>()
                .Title("[bold cyan]Выберите компаньона для директивы:[/]")
                .PageSize(10)
                .AddChoices(choices));

        if (selected == "← Назад") return;

        var selIdx = choices.IndexOf(selected);
        if (selIdx < 0 || selIdx >= companions.Count) return;

        var comp = companions[selIdx];

        // Show current directive
        if (!string.IsNullOrEmpty(comp.currentDirective))
        {
            MarkupLine($"[yellow]Текущая директива для {Markup.Escape(comp.displayName)}:[/]");
            MarkupLine($"  [italic]{Markup.Escape(comp.currentDirective)}[/]");
            WriteLine();
        }

        // Input new directive
        var newDirective = Ask("[cyan]Новая директива (или пусто для очистки):[/]", "");

        // Write to npc_core.json
        const string path = "game_state/npcs/npc_core.json";
        var rawJson = await _fs.ReadFileAsync(path);
        if (rawJson == null) { MarkupLine("[red]Ошибка чтения файла НПС.[/]"); WaitForKey(); return; }

        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node == null) return;

            bool updated = false;
            void UpdateInArray(JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item == null) continue;
                    var itemName = item["name"]?.GetValue<string>() ?? "";
                    var itemId = item["npcId"]?.GetValue<string>() ?? item["id"]?.GetValue<string>() ?? "";
                    if (itemName.Equals(comp.originalName, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(comp.npcId) && itemId.Equals(comp.npcId, StringComparison.OrdinalIgnoreCase)))
                    {
                        item["playerCompanionDirective"] = string.IsNullOrWhiteSpace(newDirective) ? null : newDirective;
                        updated = true;
                        break;
                    }
                }
            }

            if (node is JsonArray rootArr)
            {
                MarkupLine("[red]Невалидный npc_core.json: корень не должен быть массивом.[/]");
                WaitForKey();
                return;
            }
            else if (node is JsonObject obj)
            {
                foreach (var arr in GetNpcCoreArrays(obj))
                    UpdateInArray(arr);
            }

            if (updated)
            {
                var opts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;
                await _fs.WriteFileAtomicAsync(path, node.ToJsonString(opts));
                if (string.IsNullOrWhiteSpace(newDirective))
                    MarkupLine($"[green]✓ Директива для {Markup.Escape(comp.displayName)} очищена.[/]");
                else
                    MarkupLine($"[green]✓ Директива для {Markup.Escape(comp.displayName)} задана:[/] [italic]{Markup.Escape(newDirective)}[/]");
            }
            else
            {
                MarkupLine("[yellow]НПС не найден в файле. Директива будет передана ГМ с вашим действием.[/]");
            }
        }
        catch (Exception ex)
        {
            MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
        }
        WaitForKey();
    }

    /// <summary>Set a strategic directive for a player-owned faction.</summary>
    private async Task SetFactionDirective()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/factions/faction_core.json");
        if (doc == null) { ShowEmptyPanel("Фракции", "Фракции не обнаружены"); return; }

        // Collect player factions (isPlayerFaction or isPlayerMember)
        var factions = new List<(string name, string factionId, bool isOwner, string currentDirective)>();
        EnumerateFactionCoreEntries(doc.RootElement, item =>
        {
            var isOwner = item.TryGetProperty("isPlayerFaction", out var pf) && pf.ValueKind == JsonValueKind.True;
            var isMember = item.TryGetProperty("isPlayerMember", out var pm) && pm.ValueKind == JsonValueKind.True;
            if (isOwner || isMember)
            {
                var name = GetStr(item, "name", "???");
                var id = GetStr(item, "factionId", "");
                var directive = GetStr(item, "playerStrategyDirective", "");
                factions.Add((name, id, isOwner, directive));
            }
        });

        if (factions.Count == 0)
        {
            MarkupLine("[yellow]У вас нет своих фракций или членства.[/]");
            WaitForKey();
            return;
        }

        // Select faction
        var choices = factions.Select(f =>
        {
            var labelParts = new List<string> { $"🏛️ {f.name}" };
            if (f.isOwner) labelParts.Add("Лидер");
            if (!string.IsNullOrEmpty(f.currentDirective))
                labelParts.Add($"Стратегия: {f.currentDirective}");
            return ConsoleLayout.PlainChoiceLabel(labelParts.ToArray());
        }).ToList();
        choices.Add("← Назад");

        var selected = Prompt(
            new SelectionPrompt<string>()
                .Title("[bold orange1]Выберите фракцию для стратегической директивы:[/]")
                .PageSize(10)
                .AddChoices(choices));

        if (selected == "← Назад") return;

        var selIdx = choices.IndexOf(selected);
        if (selIdx < 0 || selIdx >= factions.Count) return;

        var faction = factions[selIdx];

        if (!faction.isOwner)
        {
            MarkupLine("[yellow]⚠ Вы не являетесь лидером этой фракции. Директива может быть проигнорирована.[/]");
        }

        // Show current
        if (!string.IsNullOrEmpty(faction.currentDirective))
        {
            MarkupLine($"[yellow]Текущая стратегия {Markup.Escape(faction.name)}:[/]");
            MarkupLine($"  [italic]{Markup.Escape(faction.currentDirective)}[/]");
            WriteLine();
        }

        var newDirective = Ask("[cyan]Новая стратегическая директива (или пусто для очистки):[/]", "");

        // Write to faction_core.json
        const string path = "game_state/factions/faction_core.json";
        var rawJson = await _fs.ReadFileAsync(path);
        if (rawJson == null) { MarkupLine("[red]Ошибка чтения файла фракций.[/]"); WaitForKey(); return; }

        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node == null) return;

            bool updated = false;
            void UpdateInArray(JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item is not JsonObject itemObj) continue;
                    var itemName = itemObj["name"]?.GetValue<string>() ?? "";
                    var itemId = itemObj["factionId"]?.GetValue<string>() ?? "";
                    if ((!string.IsNullOrEmpty(faction.factionId) && itemId.Equals(faction.factionId, StringComparison.OrdinalIgnoreCase)) ||
                        itemName.Equals(faction.name, StringComparison.OrdinalIgnoreCase))
                    {
                        itemObj["playerStrategyDirective"] = string.IsNullOrWhiteSpace(newDirective) ? null : newDirective;
                        updated = true;
                    }
                }
            }

            if (node is JsonArray rootArr)
                UpdateInArray(rootArr);
            else if (node is JsonObject obj)
            {
                if (obj["factionDataChanges"] is JsonArray fd) UpdateInArray(fd);
                if (obj["factions"] is JsonArray fa) UpdateInArray(fa);

                if (!updated)
                {
                    // Single faction object
                    var itemName = obj["name"]?.GetValue<string>() ?? "";
                    var itemId = obj["factionId"]?.GetValue<string>() ?? "";
                    if ((!string.IsNullOrEmpty(faction.factionId) && itemId.Equals(faction.factionId, StringComparison.OrdinalIgnoreCase)) ||
                        itemName.Equals(faction.name, StringComparison.OrdinalIgnoreCase))
                    {
                        obj["playerStrategyDirective"] = string.IsNullOrWhiteSpace(newDirective) ? null : newDirective;
                        updated = true;
                    }
                }
            }

            if (updated)
            {
                var opts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;
                await _fs.WriteFileAtomicAsync(path, node.ToJsonString(opts));
                if (string.IsNullOrWhiteSpace(newDirective))
                    MarkupLine($"[green]✓ Стратегия {Markup.Escape(faction.name)} очищена.[/]");
                else
                    MarkupLine($"[green]✓ Стратегия {Markup.Escape(faction.name)} задана:[/] [italic]{Markup.Escape(newDirective)}[/]");
            }
            else
            {
                MarkupLine("[yellow]Фракция не найдена в файле.[/]");
            }
        }
        catch (Exception ex)
        {
            MarkupLine($"[red]❌ Ошибка: {Markup.Escape(ex.Message)}[/]");
        }
        WaitForKey();
    }
}

