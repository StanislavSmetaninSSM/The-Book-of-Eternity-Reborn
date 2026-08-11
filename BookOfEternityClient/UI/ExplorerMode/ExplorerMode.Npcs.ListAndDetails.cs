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
private async Task ShowNPCs()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_core.json");
        if (doc == null)
        {
            if (await TryShowNpcJournalFallbackAsync())
                return;

            ShowEmptyPanel(_loc.T("npcs"), "НПС не обнаружены");
            return;
        }

        // Collect NPCs
        var npcs = CollectNpcListEntries(doc);
        var renameMap = BuildNpcRenameMap(doc);

        if (npcs.Count == 0)
        {
            if (await TryShowNpcJournalFallbackAsync())
                return;

            ShowEmptyPanel(_loc.T("npcs"), "НПС не обнаружены");
            return;
        }

        // Pre-load supplementary data
        var relDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_relationships.json");
        var goalDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_goals.json");
        var actDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_activities.json");
        var npcInvDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_inventory.json");
        var npcEffDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_effects.json");
        var npcSkillDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_skills.json");

        var debugMode = _stateManager.Settings.AllowHistoryManipulation;

        // All NPC documents are loaded here as per API Spec and Rule 19.9.4
        var persDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_personality.json");
        var jourDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_journals.json");
        var npcInteractionDoc = await _stateManager.LoadGameStateFileAsync(NpcInteractionJournalState.StatePath);
        var memDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_memory.json");
        var maskDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_masks.json");
        var fateDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_fate_cards.json");
        var customDoc = await _stateManager.LoadGameStateFileAsync("game_state/npcs/npc_custom_states.json");

        while (true)
        {
            var choices = npcs.Select(n =>
            {
                var name = ResolveNpcDisplayName(n, renameMap);
                return BuildNpcChoiceLabel(n, name);
            }).ToList();
            choices.Add("← Назад");

            var selected = Prompt(new SelectionPrompt<string>()
                .Title($"[bold purple]👥 {_loc.T("npcs")}[/]  [dim](выберите для подробностей)[/]")
                .PageSize(15)
                .HighlightStyle(new Style(Color.Purple))
                .AddChoices(choices));

            if (selected == "← Назад") break;

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= npcs.Count) break;

            await ShowNpcDetailPanel(npcs[selIdx], renameMap, relDoc, goalDoc, actDoc, npcInvDoc, npcEffDoc, npcSkillDoc,
                debugMode, persDoc, jourDoc, npcInteractionDoc, maskDoc, memDoc, fateDoc, customDoc);
        }
    }

    private async Task<bool> TryShowNpcJournalFallbackAsync()
    {
        var fallbackEntries = await NpcJournalFallbackProjection.ReadAsync(_stateManager);
        if (fallbackEntries.Count == 0)
            return false;

        ShowNpcJournalFallback(fallbackEntries);
        return true;
    }

    private void ShowNpcJournalFallback(IReadOnlyList<NpcJournalFallbackEntry> fallbackEntries)
    {
        var rows = NpcJournalFallbackProjection.BuildConsoleRows(fallbackEntries);
        var result = new ExplorerCommandResult
        {
            Command = "/npc",
            State = CommandExecutionState.Completed,
            Blocks = NpcJournalFallbackProjection.BuildBlocks(rows).ToList()
        };

        ExplorerCommandResultConsoleRenderer.Render(_console, result);
    }


    private async Task ShowNpcDetailPanel(JsonElement npc, Dictionary<string, string> renameMap,
        JsonDocument? relDoc, JsonDocument? goalDoc,
        JsonDocument? actDoc, JsonDocument? invDoc, JsonDocument? effDoc, JsonDocument? skillDoc,
        bool debugMode = false, JsonDocument? persDoc = null, JsonDocument? jourDoc = null, JsonDocument? npcInteractionDoc = null,
        JsonDocument? maskDoc = null, JsonDocument? memDoc = null,
        JsonDocument? fateDoc = null, JsonDocument? customDoc = null)
    {
        var originalName = GetStr(npc, "name", "???");
        var name = ResolveNpcDisplayName(npc, renameMap);
        var npcId = GetPrimaryNpcId(npc);
        var content = new Grid().AddColumn(new GridColumn());
        var lines = new List<string>();

        if (debugMode)
        {
            content.AddRow(new Markup("[dim italic magenta1]🔮 Режим манипулирования историей — полные данные НПС[/]"));
        }

        // ── Основная информация (npc_core) ──
        content.AddRow(new Markup($"[bold white]👤 {Markup.Escape(name)}[/]"));
        var summaryTable = ConsoleLayout.CreateInfoTable();

        var desc = GetStr(npc, "shortDescription", GetStr(npc, "description", ""));
        if (!string.IsNullOrEmpty(desc))
            content.AddRow(new Markup($"[white]{Markup.Escape(desc)}[/]"));

        var appearance = GetStr(npc, "appearance", "");
        if (!string.IsNullOrEmpty(appearance))
            summaryTable.AddRow(new Markup("[white]Внешность[/]"), new Markup($"[white]{Markup.Escape(appearance)}[/]"));

        var loc = GetStr(npc, "currentLocation", "");
        if (!string.IsNullOrEmpty(loc))
            summaryTable.AddRow(new Markup("[cyan]Локация[/]"), new Markup($"[cyan]{Markup.Escape(loc)}[/]"));

        // Relationship level — rendered as full progression display in RenderNpcRelationships
        // Here just show a quick summary if numeric
        var relVal = GetStr(npc, "relationshipLevel", "");
        if (!string.IsNullOrEmpty(relVal))
        {
            if (int.TryParse(relVal, out var relNum))
            {
                var tier = ReputationDisplay.GetTier(ReputationScaleKind.NpcRelationship, relNum);
                summaryTable.AddRow(new Markup($"[{tier.Color}]Отношение[/]"), new Markup(ReputationDisplay.BuildValueLabelMarkup(relNum, ReputationScaleKind.NpcRelationship)));
            }
            else
            {
                summaryTable.AddRow(new Markup("[yellow]Отношение[/]"), new Markup($"[yellow]{Markup.Escape(relVal)}[/]"));
            }
        }

        var race = GetStr(npc, "race", "");
        if (!string.IsNullOrEmpty(race))
            summaryTable.AddRow(new Markup("[white]Раса[/]"), new Markup($"[white]{Markup.Escape(race)}[/]"));

        var npcClass = GetStr(npc, "class", "");
        if (!string.IsNullOrEmpty(npcClass))
            summaryTable.AddRow(new Markup("[white]Класс[/]"), new Markup($"[white]{Markup.Escape(npcClass)}[/]"));

        var role = GetStr(npc, "role", GetStr(npc, "occupation", ""));
        if (!string.IsNullOrEmpty(role))
            summaryTable.AddRow(new Markup("[white]Роль[/]"), new Markup($"[white]{Markup.Escape(role)}[/]"));

        var npcRarity = GetStr(npc, "rarity", "");
        if (!string.IsNullOrEmpty(npcRarity))
        {
            var rarColor = npcRarity.ToLowerInvariant() switch
            {
                "common" => "white",
                "uncommon" => "green",
                "rare" => "blue",
                "epic" => "purple",
                "legendary" => "gold1",
                _ => "white"
            };
            summaryTable.AddRow(new Markup($"[{rarColor}]Редкость[/]"), new Markup($"[{rarColor}]{Markup.Escape(TranslateNpcRarity(npcRarity))}[/]"));
        }

        var npcAge = GetStr(npc, "age", "");
        if (!string.IsNullOrEmpty(npcAge))
            summaryTable.AddRow(new Markup("[white]Возраст[/]"), new Markup($"[white]{Markup.Escape(npcAge)}[/]"));

        var status = GetStr(npc, "status", "");
        if (!string.IsNullOrEmpty(status))
            summaryTable.AddRow(new Markup("[white]Статус[/]"), new Markup($"[white]{Markup.Escape(status)}[/]"));

        // Progression type (Companion/PlotDriven/Static)
        var progType = GetStr(npc, "progressionType", "");
        if (!string.IsNullOrEmpty(progType))
        {
            var (ptLabel, ptColor) = progType.ToLowerInvariant() switch
            {
                "companion" => ("Компаньон", "green"),
                "plotdriven" => ("Сюжетный", "yellow"),
                "plot_driven" => ("Сюжетный", "yellow"),
                "static" => ("Статичный", "grey"),
                "static_social_npc" => ("Статичный социальный персонаж", "grey"),
                "static_social" => ("Статичный социальный персонаж", "grey"),
                _ => (HumanizeNpcStableToken(progType), "white")
            };
            summaryTable.AddRow(new Markup($"[{ptColor}]Тип развития[/]"), new Markup($"[{ptColor}]{Markup.Escape(ptLabel)}[/]"));
        }

        // NPC level/XP
        var npcLevel = GetStr(npc, "level", "");
        if (!string.IsNullOrEmpty(npcLevel))
        {
            var lvlLine = $"  📈 Уровень: [yellow]{Markup.Escape(npcLevel)}[/]";
            var npcXp = GetStr(npc, "experience", "");
            var npcXpNext = GetStr(npc, "experienceForNextLevel", "");
            if (!string.IsNullOrEmpty(npcXp) && !string.IsNullOrEmpty(npcXpNext))
                lvlLine += $" [dim]({Markup.Escape(npcXp)}/{Markup.Escape(npcXpNext)} XP)[/]";
            summaryTable.AddRow(new Markup("[yellow]Уровень[/]"), new Markup(lvlLine.Replace("  📈 Уровень: ", "")));
        }

        // Companion directive
        var compDirective = GetStr(npc, "playerCompanionDirective", "");
        if (!string.IsNullOrEmpty(compDirective) && !IsDefaultNonCompanionDirective(compDirective))
        {
            summaryTable.AddRow(new Markup("[cyan]Директива игрока[/]"), new Markup($"[italic cyan]{Markup.Escape(TranslateNpcCompanionDirective(compDirective))}[/]"));
        }
        else if (progType.Equals("Companion", StringComparison.OrdinalIgnoreCase))
        {
            summaryTable.AddRow(new Markup("[dim]Директива игрока[/]"), new Markup("[dim italic]не задана (используйте /директива_компаньону)[/]"));
        }

        // ── Health (embedded in npc_core) ──
        var curHp = GetStr(npc, "currentHealthPercentage", "");
        var maxHp = GetStr(npc, "maxHealthPercentage", "");
        if (!string.IsNullOrEmpty(curHp) || !string.IsNullOrEmpty(maxHp))
        {
            var hpCur = int.TryParse(curHp.Replace("%", "").Trim(), out var hpC) ? hpC : 100;
            var hpMax = int.TryParse(maxHp.Replace("%", "").Trim(), out var hpM) ? hpM : 100;
            var hpPct = hpMax > 0 ? hpCur * 100 / hpMax : 100;
            var hpColor = hpPct > 60 ? "green" : hpPct > 30 ? "yellow" : "red";
            var hpTable = ConsoleLayout.CreateBarMetricTable();
            hpTable.AddRow(
                new Markup($"[{hpColor}]Здоровье[/]"),
                new Markup(ConsoleLayout.CreateBarFromPercent(hpPct, 16, hpColor)),
                new Markup($"[{hpColor}]{hpCur}%/{hpMax}%[/]"),
                new Markup("[dim]Текущее состояние тела NPC[/]"));
            content.AddRow(summaryTable);
            content.AddRow(hpTable);
            summaryTable = ConsoleLayout.CreateInfoTable();
        }

        if (summaryTable.Rows.Count > 0)
            content.AddRow(summaryTable);

        var (currentLocationId, currentLocationName) = await ReadCurrentLocationIdentityAsync();
        var npcTradeAvailability = Services.NpcTradeService.EvaluateTradeAvailability(npc, currentLocationId, currentLocationName);
        if (npcTradeAvailability.IsMerchant)
        {
            lines.Add("");
            lines.Add("  [bold]🛒 Локальная торговля:[/]");
            if (!string.IsNullOrWhiteSpace(npcTradeAvailability.BlockReason))
                lines.Add($"    [dim]{Markup.Escape(npcTradeAvailability.BlockReason)}[/]");
            else if (npcTradeAvailability.TradeAvailable)
                lines.Add($"    [white]Доступна. Профиль торговца: {Markup.Escape(npcTradeAvailability.MerchantProfileDisplay)}. Витрина обновляется каждые 30 игровых дней.[/]");
        }

        // ── Appearance description (detailed, separate from short appearance) ──
        var appearDesc = GetStr(npc, "appearanceDescription", "");
        if (!string.IsNullOrEmpty(appearDesc))
            lines.Add($"  🎨 Внешность (подробно): [white]{Markup.Escape(appearDesc)}[/]");

        // ── Race & class descriptions ──
        var raceDesc = GetStr(npc, "raceDescription", "");
        if (!string.IsNullOrEmpty(raceDesc))
            lines.Add($"  🧬 Раса (подробно): [dim]{Markup.Escape(raceDesc)}[/]");
        var classDesc = GetStr(npc, "classDescription", "");
        if (!string.IsNullOrEmpty(classDesc))
            lines.Add($"  ⚔ Класс (подробно): [dim]{Markup.Escape(classDesc)}[/]");

        // ── History ──
        var history = GetStr(npc, "history", "");
        if (!string.IsNullOrEmpty(history))
            lines.Add($"  📜 Предыстория: [white italic]{Markup.Escape(history)}[/]");

        // ── Worldview / alignment ──
        var worldview = GetStr(npc, "worldview", "");
        if (!string.IsNullOrEmpty(worldview))
            lines.Add($"  ⚖️ Мировоззрение: [white]{Markup.Escape(worldview)}[/]");

        // ── Attitude (derived from relationship tier) ──
        var attitude = GetStr(npc, "attitude", "");
        if (!string.IsNullOrEmpty(attitude))
            lines.Add($"  🗣️ Отношение: [yellow]{Markup.Escape(FormatNpcAttitudeLabel(attitude))}[/]");

        // ── Cultural layer ──
        var cultural = GetStr(npc, "culturalLayer", "");
        if (!string.IsNullOrEmpty(cultural))
            lines.Add($"  🌍 Культурный слой: [white]{Markup.Escape(cultural)}[/]");
        var culturalStance = GetStr(npc, "culturalStance", "");
        if (!string.IsNullOrEmpty(culturalStance))
        {
            var stanceRu = culturalStance.ToLowerInvariant() switch
            {
                "conformist" => "Конформист",
                "pragmatist" => "Прагматик",
                "dissident" => "Диссидент",
                _ => culturalStance
            };
            lines.Add($"  🏛️ Культурная позиция: [white]{Markup.Escape(stanceRu)}[/]");
        }

        // ── Personality archetype & traits (embedded in npc_core) ──
        var persArchetype = GetStr(npc, "personalityArchetype", "");
        var playerFacingArchetype = FormatNpcPlayerFacingFreeText(persArchetype);
        if (!string.IsNullOrEmpty(playerFacingArchetype))
            lines.Add($"  🧠 Архетип личности: [magenta1]{Markup.Escape(playerFacingArchetype)}[/]");

        if (npc.TryGetProperty("personalityTraits", out var pTraits) && pTraits.ValueKind == JsonValueKind.Array && pTraits.GetArrayLength() > 0)
        {
            lines.Add($"  [bold magenta1]🏷️ Черты личности:[/]");
            foreach (var trait in pTraits.EnumerateArray())
            {
                var traitName = GetStr(trait, "traitName", GetStr(trait, "name", ""));
                var traitDesc = GetStr(trait, "description", "");
                var traitVal = GetInt(trait, "value", -1);
                var traitValDesc = GetStr(trait, "valueDescription", "");
                if (string.IsNullOrEmpty(traitName)) continue;
                var line = $"    • [white]{Markup.Escape(traitName)}[/]";
                if (traitVal >= 0)
                {
                    var tBarW = 10;
                    var tFilled = Math.Clamp(traitVal * tBarW / 10, 0, tBarW);
                    var tColor = traitVal >= 7 ? "green" : traitVal >= 4 ? "yellow" : "red";
                    line += $" [{tColor}]{new string('█', tFilled)}[/][dim]{new string('░', tBarW - tFilled)}[/] [{tColor}]{traitVal}/10[/]";
                }
                if (!string.IsNullOrEmpty(traitValDesc))
                    line += $" [dim]({Markup.Escape(traitValDesc)})[/]";
                lines.Add(line);
                if (!string.IsNullOrEmpty(traitDesc))
                    lines.Add($"      [dim]{Markup.Escape(traitDesc)}[/]");
            }
        }

        // ── Characteristics (12 stats, embedded in npc_core) ──
        if (npc.TryGetProperty("characteristics", out var chars) && chars.ValueKind == JsonValueKind.Object)
        {
            lines.Add("");
            lines.Add($"  [bold yellow]📊 Характеристики:[/]");
            foreach (var charName in Characteristics.All)
            {
                var rusName = Characteristics.RussianNames.TryGetValue(charName, out var rn) ? rn : charName;
                var stdProp = $"standard{char.ToUpper(charName[0])}{charName[1..]}";
                var modProp = $"modified{char.ToUpper(charName[0])}{charName[1..]}";
                var stdVal = chars.TryGetProperty(stdProp, out var sv) && sv.ValueKind == JsonValueKind.Number ? sv.GetInt32() : -1;
                var modVal = chars.TryGetProperty(modProp, out var mv) && mv.ValueKind == JsonValueKind.Number ? mv.GetInt32() : -1;
                // Also try flat format: just "strength": 10
                if (stdVal < 0 && modVal < 0)
                {
                    if (chars.TryGetProperty(charName, out var flat) && flat.ValueKind == JsonValueKind.Number)
                    {
                        stdVal = flat.GetInt32();
                        modVal = stdVal;
                    }
                    else continue;
                }
                if (stdVal < 0) stdVal = modVal;
                if (modVal < 0) modVal = stdVal;

                var diff = modVal - stdVal;
                var diffStr = diff > 0 ? $" [green](+{diff})[/]" : diff < 0 ? $" [red]({diff})[/]" : "";
                var barW = 10;
                var filled = Math.Clamp(modVal * barW / 20, 0, barW); // scale: 0-20 typical range
                var barColor = modVal >= 14 ? "green" : modVal >= 8 ? "yellow" : "red";
                lines.Add($"    {Markup.Escape(rusName),-18} [{barColor}]{new string('█', filled)}[/][dim]{new string('░', barW - filled)}[/] [white]{modVal}[/]{diffStr}");
            }
        }

        // ── Relationship lock (embedded in npc_core) ──
        if (npc.TryGetProperty("relationshipLock", out var rLock) && rLock.ValueKind == JsonValueKind.Object)
        {
            var rlIsLocked = rLock.TryGetProperty("isLocked", out var rlL) && rlL.ValueKind == JsonValueKind.True;
            if (rlIsLocked)
            {
                var rlCap = GetInt(rLock, "currentCap", 0);
                var rlQuest = GetStr(rLock, "breakthroughQuestId", "");
                var capInfo = rlCap != 0 ? $" (порог: {rlCap})" : "";
                if (rlQuest == "__UNBREAKABLE__")
                    lines.Add($"  [bold red]🔒 Отношение НЕОБРАТИМО ЗАБЛОКИРОВАНО[/]");
                else
                {
                    lines.Add($"  [bold yellow]🔒 Отношение заблокировано{capInfo}[/]");
                    if (!string.IsNullOrEmpty(rlQuest) && rlQuest != "_clear_")
                        lines.Add($"  [yellow]📜 Квест прорыва назначен[/]");
                }
            }
        }

        // ── Goals (embedded in npc_core) ──
        if (npc.TryGetProperty("goals", out var goals) && goals.ValueKind == JsonValueKind.Object)
        {
            var longTerm = GetStr(goals, "longTerm", "");
            var shortTerm = GetStr(goals, "shortTerm", "");
            if (!string.IsNullOrEmpty(longTerm) || !string.IsNullOrEmpty(shortTerm))
            {
                lines.Add("");
                lines.Add($"  [bold green]🎯 Цели:[/]");
                if (!string.IsNullOrEmpty(longTerm))
                    lines.Add($"    🏆 Долгосрочная: [white]{Markup.Escape(longTerm)}[/]");
                if (!string.IsNullOrEmpty(shortTerm))
                    lines.Add($"    ⚡ Краткосрочная: [white]{Markup.Escape(shortTerm)}[/]");
            }
        }
        var plans = GetStr(npc, "plans", "");
        if (!string.IsNullOrEmpty(plans))
            lines.Add($"    📝 План: [dim]{Markup.Escape(plans)}[/]");

        // ── Personal quests (embedded in npc_core) ──
        if (npc.TryGetProperty("personalQuests", out var pQuests) && pQuests.ValueKind == JsonValueKind.Array && pQuests.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold gold1]📜 Личные квесты:[/]");
            foreach (var q in pQuests.EnumerateArray())
            {
                var qName = GetStr(q, "questName", GetStr(q, "name", "?"));
                var qStatus = GetStr(q, "status", "");
                var qDesc = GetStr(q, "description", "");
                var qBg = GetStr(q, "questBackground", "");
                var qRewards = GetStr(q, "rewards", "");
                var qFail = GetStr(q, "failureConsequences", "");
                var qColor = qStatus.ToLowerInvariant() switch
                {
                    "active" or "активен" => "yellow",
                    "completed" or "завершён" => "green",
                    "failed" or "провален" => "red",
                    _ => "white"
                };
                lines.Add($"    📜 [bold {qColor}]{Markup.Escape(qName)}[/] [{qColor}]({Markup.Escape(qStatus)})[/]");
                if (!string.IsNullOrEmpty(qDesc))
                    lines.Add($"      [white]{Markup.Escape(qDesc)}[/]");
                if (!string.IsNullOrEmpty(qBg))
                    lines.Add($"      [dim italic]Предпосылка: {Markup.Escape(qBg)}[/]");
                // Objectives
                if (q.TryGetProperty("objectives", out var objs) && objs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var obj in objs.EnumerateArray())
                    {
                        var objDesc = GetStr(obj, "description", "");
                        var objSt = GetStr(obj, "status", "");
                        var objIcon = objSt.ToLowerInvariant() switch
                        {
                            "completed" or "завершён" => "[green]✓[/]",
                            "failed" or "провален" => "[red]✗[/]",
                            _ => "[yellow]○[/]"
                        };
                        if (!string.IsNullOrEmpty(objDesc))
                            lines.Add($"      {objIcon} {Markup.Escape(objDesc)}");
                    }
                }
                if (!string.IsNullOrEmpty(qRewards))
                    lines.Add($"      [green]Награда: {Markup.Escape(qRewards)}[/]");
                if (!string.IsNullOrEmpty(qFail))
                    lines.Add($"      [red]При провале: {Markup.Escape(qFail)}[/]");
            }
        }

        // ── Current activity (embedded in npc_core) ──
        if (npc.TryGetProperty("currentActivity", out var curAct) && curAct.ValueKind == JsonValueKind.Object)
        {
            var actName = GetStr(curAct, "activityName", GetStr(curAct, "name", ""));
            if (!string.IsNullOrEmpty(actName))
            {
                lines.Add("");
                lines.Add($"  [bold yellow]🏃 Текущая активность:[/]");
                var actDesc = GetStr(curAct, "description", "");
                lines.Add($"    ⚡ [white]{Markup.Escape(actName)}[/]");
                if (!string.IsNullOrEmpty(actDesc))
                    lines.Add($"      [dim]{Markup.Escape(actDesc)}[/]");
                var totalTime = GetInt(curAct, "totalTimeCostMinutes", 0);
                var spentTime = GetInt(curAct, "timeSpentMinutes", 0);
                var curStep = GetInt(curAct, "currentStepNumber", 0);
                var totalSteps = GetInt(curAct, "totalStepsInActivity", 0);
                if (totalTime > 0)
                {
                    var pct = Math.Min(100, spentTime * 100 / totalTime);
                    var filled = pct / 10;
                    var barColor = pct >= 80 ? "green" : pct >= 50 ? "yellow" : "cyan";
                    lines.Add($"      Прогресс: [{barColor}]{new string('█', filled)}[/][dim]{new string('░', 10 - filled)}[/] {pct}% ({FormatMinutes(spentTime)}/{FormatMinutes(totalTime)})");
                }
                if (totalSteps > 0)
                    lines.Add($"      Шаг: [yellow]{curStep}[/]/{totalSteps}");
            }
        }

        // ── Completed activities ──
        if (npc.TryGetProperty("completedActivities", out var compActs) && compActs.ValueKind == JsonValueKind.Array && compActs.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold dim]✅ Завершённые активности:[/]");
            foreach (var ca in compActs.EnumerateArray())
            {
                if (ca.ValueKind == JsonValueKind.String)
                {
                    lines.Add($"    • [dim]{Markup.Escape(ca.GetString() ?? "")}[/]");
                }
                else if (ca.ValueKind == JsonValueKind.Object)
                {
                    var caName = GetStr(ca, "activityName", GetStr(ca, "name", "?"));
                    var caResult = GetStr(ca, "result", GetStr(ca, "outcome", ""));
                    var caLine = $"    • [dim]{Markup.Escape(caName)}[/]";
                    if (!string.IsNullOrEmpty(caResult)) caLine += $" — [dim italic]{Markup.Escape(caResult)}[/]";
                    lines.Add(caLine);
                }
            }
        }

        // ── Faction affiliations (embedded in npc_core) ──
        if (npc.TryGetProperty("factionAffiliations", out var fAff) && fAff.ValueKind == JsonValueKind.Array && fAff.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold blue]🏛️ Фракции:[/]");
            foreach (var fa in fAff.EnumerateArray())
            {
                var fName = GetStr(fa, "factionName", GetStr(fa, "name", "?"));
                var fRank = GetStr(fa, "rank", "");
                var fBranch = GetStr(fa, "branch", "");
                var fMemStatus = GetStr(fa, "membershipStatus", "");
                var fColor = fMemStatus.ToLowerInvariant() switch
                {
                    "active" or "активен" => "green",
                    "former" or "бывший" => "grey",
                    "exiled" or "изгнан" => "red",
                    "undercover" or "под прикрытием" => "yellow",
                    "ally" or "союзник" => "cyan",
                    "enemy" or "враг" => "red",
                    _ => "white"
                };
                var line = $"    🏛️ [white]{Markup.Escape(fName)}[/]";
                if (!string.IsNullOrEmpty(fRank))
                    line += $" — [{fColor}]{Markup.Escape(fRank)}[/]";
                if (!string.IsNullOrEmpty(fBranch))
                    line += $" [dim]({Markup.Escape(fBranch)})[/]";
                if (!string.IsNullOrEmpty(fMemStatus))
                    line += $" [{fColor}]({Markup.Escape(fMemStatus)})[/]";
                lines.Add(line);
            }
        }

        // ── NPC-to-NPC relationships (embedded in npc_core) ──
        if (npc.TryGetProperty("npcRelationships", out var npcRels) && npcRels.ValueKind == JsonValueKind.Array && npcRels.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold steelblue1]🤝 Связи с другими НПС:[/]");
            foreach (var nr in npcRels.EnumerateArray())
            {
                var tgtName = GetStr(nr, "targetNpcName", GetStr(nr, "name", "?"));
                var relSt = GetStr(nr, "relationshipStatus", GetStr(nr, "status", ""));
                var reason = GetStr(nr, "statusReason", GetStr(nr, "reason", ""));
                var relIcon = relSt.ToLowerInvariant() switch
                {
                    "ally" or "союзник" => "🤝",
                    "friend" or "друг" => "😊",
                    "rival" or "соперник" => "⚔",
                    "enemy" or "враг" => "💀",
                    "subordinate" or "подчинённый" => "👇",
                    "superior" or "начальник" => "👆",
                    "family" or "семья" => "👨‍👩‍👧",
                    _ => "👤"
                };
                var line = $"    {relIcon} [white]{Markup.Escape(tgtName)}[/] — [yellow]{Markup.Escape(relSt)}[/]";
                if (!string.IsNullOrEmpty(reason))
                    line += $" [dim]({Markup.Escape(reason)})[/]";
                lines.Add(line);
            }
        }

        // ── Embedded fate cards (in npc_core) ──
        if (npc.TryGetProperty("fateCards", out var embFate) && embFate.ValueKind == JsonValueKind.Array && embFate.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold mediumpurple2]🃏 Карты Судьбы:[/]");
            foreach (var fc in embFate.EnumerateArray())
            {
                var fcName = GetStr(fc, "name", "?");
                var fcDesc = GetStr(fc, "description", "");
                var fcUnlocked = fc.TryGetProperty("isUnlocked", out var uv) && uv.ValueKind == JsonValueKind.True;
                var lockIcon = fcUnlocked ? "[green]🔓[/]" : "[red]🔒[/]";
                lines.Add($"    {lockIcon} [mediumpurple2]{Markup.Escape(fcName)}[/]");
                if (fcUnlocked && !string.IsNullOrEmpty(fcDesc))
                    lines.Add($"      [white]{Markup.Escape(fcDesc)}[/]");
                else if (!fcUnlocked)
                {
                    // Show unlock conditions as a hint
                    if (fc.TryGetProperty("unlockConditions", out var uc) && uc.ValueKind == JsonValueKind.Object)
                    {
                        var reqRel = GetInt(uc, "requiredRelationshipLevel", 0);
                        var plotCond = GetStr(uc, "plotConditionDescription", "");
                        if (reqRel > 0)
                            lines.Add($"      [dim]Требуется отношение: {reqRel}+[/]");
                        if (!string.IsNullOrEmpty(plotCond))
                            lines.Add($"      [dim]Условие: {Markup.Escape(plotCond)}[/]");
                    }
                }
                // Show rewards for unlocked cards
                if (fcUnlocked && fc.TryGetProperty("rewards", out var rw) && rw.ValueKind == JsonValueKind.Object)
                {
                    var rwDesc = GetStr(rw, "description", "");
                    if (!string.IsNullOrEmpty(rwDesc))
                        lines.Add($"      [green]Награда: {Markup.Escape(rwDesc)}[/]");
                }
            }
        }

        // ── Embedded skills (activeSkills/passiveSkills in npc_core) ──
        if (npc.TryGetProperty("activeSkills", out var aSkills) && aSkills.ValueKind == JsonValueKind.Array && aSkills.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add($"  [bold cyan]⚔ Активные навыки:[/]");
            foreach (var s in aSkills.EnumerateArray())
            {
                var sn = s.ValueKind == JsonValueKind.String ? (s.GetString() ?? "") : GetStr(s, "name", "?");
                var sDesc = s.ValueKind == JsonValueKind.Object ? GetStr(s, "description", "") : "";
                lines.Add($"    • [cyan]{Markup.Escape(sn)}[/]");
                if (!string.IsNullOrEmpty(sDesc))
                    lines.Add($"      [dim]{Markup.Escape(sDesc)}[/]");
            }
        }
        if (npc.TryGetProperty("passiveSkills", out var pSkills) && pSkills.ValueKind == JsonValueKind.Array && pSkills.GetArrayLength() > 0)
        {
            lines.Add($"  [bold dim]🛡️ Пассивные навыки:[/]");
            foreach (var s in pSkills.EnumerateArray())
            {
                var sn = s.ValueKind == JsonValueKind.String ? (s.GetString() ?? "") : GetStr(s, "name", "?");
                var sDesc = s.ValueKind == JsonValueKind.Object ? GetStr(s, "description", "") : "";
                lines.Add($"    • [white]{Markup.Escape(sn)}[/]");
                if (!string.IsNullOrEmpty(sDesc))
                    lines.Add($"      [dim]{Markup.Escape(sDesc)}[/]");
            }
        }

        // ── Embedded masks (in npc_core) ──
        var activeMask = GetStr(npc, "activeMaskId", "");
        if (npc.TryGetProperty("masks", out var embMasks) && embMasks.ValueKind == JsonValueKind.Array && embMasks.GetArrayLength() > 0 && debugMode)
        {
            lines.Add("");
            lines.Add($"  [bold red]🎭 Маски (персоны):[/]");
            foreach (var m in embMasks.EnumerateArray())
            {
                var mId = GetStr(m, "maskId", "");
                var mName = GetStr(m, "maskName", GetStr(m, "name", "?"));
                var mArch = GetStr(m, "personalityArchetype", "");
                var mAtt = GetStr(m, "attitude", "");
                var isActive = !string.IsNullOrEmpty(activeMask) && mId == activeMask;
                var activeTag = isActive ? " [green]● АКТИВНА[/]" : "";
                lines.Add($"    🎭 [red]{Markup.Escape(mName)}[/]{activeTag}");
                if (!string.IsNullOrEmpty(mArch))
                    lines.Add($"      [dim]Архетип: {Markup.Escape(mArch)}[/]");
                if (!string.IsNullOrEmpty(mAtt))
                    lines.Add($"      [dim]Отношение: {Markup.Escape(FormatNpcAttitudeLabel(mAtt))}[/]");
            }
        }

        // ── Guardian-specific fields (personalityProfile, relationshipData, questManagement, gachaSystem) ──
        var guardianDomain = GetStr(npc, "domain", "");
        if (!string.IsNullOrEmpty(guardianDomain))
            lines.Add($"  🔮 Домен: [magenta1]{Markup.Escape(guardianDomain)}[/]");

        if (npc.TryGetProperty("personalityProfile", out var pp) && pp.ValueKind == JsonValueKind.Object)
        {
            var ppArch = GetStr(pp, "archetype", "");
            var ppSpeech = GetStr(pp, "speechPattern", "");
            if (!string.IsNullOrEmpty(ppArch))
                lines.Add($"  🧠 Архетип: [magenta1]{Markup.Escape(ppArch)}[/]");
            if (!string.IsNullOrEmpty(ppSpeech))
                lines.Add($"  🗣️ Стиль речи: [white]{Markup.Escape(ppSpeech)}[/]");
            if (pp.TryGetProperty("coreValues", out var cv) && cv.ValueKind == JsonValueKind.Array)
            {
                var vals = new List<string>();
                foreach (var v in cv.EnumerateArray())
                    if (v.ValueKind == JsonValueKind.String) vals.Add(v.GetString() ?? "");
                if (vals.Count > 0)
                    lines.Add($"  💎 Ценности: [white]{Markup.Escape(string.Join(", ", vals))}[/]");
            }
        }

        var guardianReputation = GetInt(npc, "reputation", 0);
        if (npc.TryGetProperty("relationshipData", out var rd) && rd.ValueKind == JsonValueKind.Object)
        {
            var curRep = GetInt(rd, "currentReputation", int.MinValue);
            if (curRep != int.MinValue)
            {
                guardianReputation = curRep;
                lines.Add($"  ♥ Репутация: {ReputationDisplay.BuildBarMarkup(curRep, ReputationScaleKind.Guardian, 20)} {ReputationDisplay.BuildValueLabelMarkup(curRep, ReputationScaleKind.Guardian)}");
            }
            if (rd.TryGetProperty("reputationHistory", out var rh) && rh.ValueKind == JsonValueKind.Array && rh.GetArrayLength() > 0)
            {
                lines.Add($"    [dim]История ({rh.GetArrayLength()}):[/]");
                foreach (var e in rh.EnumerateArray())
                {
                    var change = GetInt(e, "change", 0);
                    var reason = GetStr(e, "reason", "");
                    if (!string.IsNullOrEmpty(reason))
                    {
                        var chStr = change > 0 ? $"[green]+{change}[/]" : change < 0 ? $"[red]{change}[/]" : "[dim]±0[/]";
                        lines.Add($"    {chStr} [dim]{Markup.Escape(reason)}[/]");
                    }
                }
            }
        }

        if (npc.TryGetProperty("questManagement", out var qm) && qm.ValueKind == JsonValueKind.Object)
        {
            if (qm.TryGetProperty("availableQuests", out var aq) && aq.ValueKind == JsonValueKind.Array && aq.GetArrayLength() > 0)
            {
                lines.Add("");
                lines.Add($"  [bold gold1]📜 Доступные квесты:[/]");
                foreach (var q in aq.EnumerateArray())
                {
                    var qStr = q.ValueKind == JsonValueKind.String ? (q.GetString() ?? "") : GetStr(q, "name", GetStr(q, "questId", "?"));
                    lines.Add($"    • [yellow]{Markup.Escape(qStr)}[/]");
                }
            }
            if (qm.TryGetProperty("completedQuests", out var cq) && cq.ValueKind == JsonValueKind.Array && cq.GetArrayLength() > 0)
            {
                lines.Add($"  [dim]✓ Завершённые квесты: {cq.GetArrayLength()}[/]");
            }
        }

        if (npc.TryGetProperty("gachaSystem", out var gs) && gs.ValueKind == JsonValueKind.Object)
        {
            var chargesPerReturn = gs.TryGetProperty("chargesPerReturn", out var cpr) && cpr.ValueKind == JsonValueKind.Number && cpr.TryGetInt32(out var parsedCharges)
                ? parsedCharges
                : GuardianGachaChargeRules.GetChargesPerReturnForGuardian(npc);
            var chargesUsedThisReturn = gs.TryGetProperty("chargesUsedThisReturn", out var cur) && cur.ValueKind == JsonValueKind.Number && cur.TryGetInt32(out var parsedUsed)
                ? GuardianGachaChargeRules.ClampUsedCharges(parsedUsed, chargesPerReturn)
                : 0;
            var remainingCharges = Math.Max(0, chargesPerReturn - chargesUsedThisReturn);

            if (chargesPerReturn <= 0)
            {
                lines.Add("  🎰 Вытягивание реликвий: [red]заблокировано репутацией[/]");
            }
            else
            {
                lines.Add($"  🎰 Попытки в этом возвращении: [yellow]{remainingCharges}[/]/[white]{chargesPerReturn}[/]");
                if (remainingCharges <= 0)
                    lines.Add("    [dim]Лимит у этого Хранителя исчерпан до следующего возвращения из смертной жизни.[/]");
            }
        }

        // ── Weight info ──
        var maxWeight = GetStr(npc, "maxWeight", "");
        var totalWeight = GetStr(npc, "totalWeight", "");
        if (!string.IsNullOrEmpty(maxWeight) || !string.IsNullOrEmpty(totalWeight))
        {
            var wLine = "  ⚖️ Вес:";
            if (!string.IsNullOrEmpty(totalWeight))
                wLine += $" [white]{Markup.Escape(totalWeight)}[/]";
            if (!string.IsNullOrEmpty(maxWeight))
                wLine += $"/[dim]{Markup.Escape(maxWeight)} кг[/]";
            var isOver = npc.TryGetProperty("isOverloaded", out var ov) && ov.ValueKind == JsonValueKind.True;
            if (isOver)
                wLine += " [bold red]⚠ ПЕРЕГРУЖЕН[/]";
            lines.Add(wLine);
        }

        // Show remaining non-core string/number/bool properties (catch-all for unknown fields)
        var coreProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "name", "NPCName", "npcName", "NPCId", "npcId", "id", "initialId", "shortDescription", "description",
            "appearance", "currentLocation", "relationshipLevel", "race", "role", "occupation", "status",
            "lastInteraction", "progressionType", "level", "experience", "experienceForNextLevel",
            "playerCompanionDirective", "image_prompt", "currentHealthPercentage", "maxHealthPercentage",
            "appearanceDescription", "raceDescription", "classDescription", "history", "worldview",
            "attitude", "culturalLayer", "culturalStance", "personalityArchetype", "personalityTraits",
            "characteristics", "relationshipLock", "goals", "plans", "personalQuests", "currentActivity",
            "completedActivities", "factionAffiliations", "npcRelationships", "fateCards",
            "activeSkills", "passiveSkills", "inventory", "equippedItems", "masks", "activeMaskId",
            "customStates", "maxWeight", "totalWeight", "isOverloaded", "progressionTrackers",
            "personalityProfile", "guardianId", "domain", "relationshipData", "questManagement",
            "gachaSystem", "rarity", "age", "class", "currentLocationId", "currentLocationName",
            "initialLocationId", "initialLocationName", "sceneStatus", "lastSeenAtUtc", "turn", "materialization" };
        foreach (var prop in npc.EnumerateObject())
        {
            if (coreProps.Contains(prop.Name)) continue;
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var val = prop.Value.GetString() ?? "";
                if (val.Length > 0)
                    lines.Add($"  📋 {Markup.Escape(NpcFieldToRussian(prop.Name))}: [white]{Markup.Escape(val)}[/]");
            }
            else if (prop.Value.ValueKind == JsonValueKind.Number)
            {
                lines.Add($"  📋 {Markup.Escape(NpcFieldToRussian(prop.Name))}: [yellow]{prop.Value}[/]");
            }
            else if (prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                var boolColor = prop.Value.ValueKind == JsonValueKind.True ? "green" : "red";
                var boolText = prop.Value.ValueKind == JsonValueKind.True ? "да" : "нет";
                lines.Add($"  📋 {Markup.Escape(NpcFieldToRussian(prop.Name))}: [{boolColor}]{boolText}[/]");
            }
        }

        if (debugMode)
        {
            var lastInt = GetStr(npc, "lastInteraction", "");
            if (!string.IsNullOrEmpty(lastInt))
                lines.Add($"  🕐 Последнее взаимодействие: [dim]{Markup.Escape(lastInt)}[/]");

            if (!string.IsNullOrEmpty(npcId))
                lines.Add($"  🆔 ID: [dim]{Markup.Escape(npcId)}[/]");
        }

        // ── Отношения (npc_relationships) ──
        RenderNpcRelationships(lines, relDoc, npcId, originalName, debugMode);

        // ── Цели (npc_goals) ──
        RenderNpcGoals(lines, goalDoc, npcId, originalName, debugMode);

        // ── Активность (npc_activities) ──
        RenderNpcActivities(lines, actDoc, npcId, originalName, debugMode);

        // ── Принятый канонический инвентарь NPC ──
        RenderNpcInventory(lines, npc, debugMode);

        // ── Эффекты (npc_effects) ──
        RenderNpcEffects(lines, effDoc, npcId, originalName, debugMode);

        // ── Навыки (npc_skills) ──
        RenderNpcSkills(lines, skillDoc, npcId, originalName, debugMode);

        // ── Черты характера (npc_personality) ──
        RenderNpcPersonality(lines, persDoc, npcId, originalName, debugMode);

        // ── Воспоминания (npc_memory) ──
        RenderNpcMemories(lines, memDoc, npcId, originalName, debugMode);

        // ── Дневник / Мысли (npc_journals) ──
        RenderNpcJournals(lines, jourDoc, npcId, originalName, debugMode);

        // ── Память взаимодействий (npc_interaction_journal) ──
        RenderNpcInteractionJournal(lines, npcInteractionDoc, npcId);

        // ── Карты судьбы (npc_fate_cards) — разблокированные видны игроку ──
        RenderNpcFateCards(lines, fateDoc, npcId, originalName, debugMode);

        // ── Особые состояния (npc_custom_states) ──
        RenderNpcCustomStates(lines, customDoc, npcId, originalName);

        // ── Debug-only sections ──
        if (debugMode)
        {
            RenderNpcMasks(lines, maskDoc, npcId, originalName);
        }

        if (lines.Count > 0)
            content.AddRow(GameInterface.SafeMarkup(string.Join("\n", lines)));

        Write(new Panel(content)
        {
            Header = new PanelHeader($" 👤 {Markup.Escape(name)} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(debugMode ? Color.Magenta1 : Color.Purple),
            Padding = new Padding(2, 1),
            Expand = true
        });

        var projection = NpcDetailSectionProjection.Build(
            JsonNode.Parse(npc.GetRawText()),
            new NpcDetailSectionDocuments(
                Relationships: ToJsonNode(relDoc),
                Goals: ToJsonNode(goalDoc),
                Activities: ToJsonNode(actDoc),
                Inventory: ToJsonNode(invDoc),
                Effects: ToJsonNode(effDoc),
                Skills: ToJsonNode(skillDoc),
                Personality: ToJsonNode(persDoc),
                Journals: ToJsonNode(jourDoc),
                InteractionJournal: ToJsonNode(npcInteractionDoc),
                Masks: ToJsonNode(maskDoc),
                Memory: ToJsonNode(memDoc),
                FateCards: ToJsonNode(fateDoc),
                CustomStates: ToJsonNode(customDoc)));
        ShowNpcDetailSectionMenu(name, projection.Sections);

        await ShowNpcDetailActions(npc, originalName, npcTradeAvailability.TradeAvailable);
    }

    private void ShowNpcDetailSectionMenu(string npcName, IReadOnlyList<NpcDetailSection> sections)
    {
        if (sections.Count == 0)
            return;

        while (true)
        {
            var choices = sections
                .Select(section => ((NpcDetailSection?)section, GameInterface.SafePromptChoice(section.ChoiceLabel)))
                .ToList();
            choices.Add((null, "← Закрыть разделы НПС"));

            var selected = Prompt(new SelectionPrompt<string>()
                .Title($"[bold purple]Разделы НПС: {GameInterface.EscapeMarkup(npcName)}[/]")
                .PageSize(12)
                .HighlightStyle(new Style(Color.Purple))
                .AddChoices(choices.Select(static choice => choice.Item2)));

            if (selected.Contains("←", StringComparison.Ordinal) ||
                selected.Contains("Назад", StringComparison.OrdinalIgnoreCase) ||
                selected.Contains("К списку", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var section = choices.FirstOrDefault(choice => choice.Item2 == selected).Item1;
            if (section == null)
                return;

            ExplorerCommandResultConsoleRenderer.Render(_console, new ExplorerCommandResult
            {
                Command = "/npc",
                State = CommandExecutionState.Completed,
                Blocks = section.Blocks.ToList()
            });
            WaitForKey();
        }
    }

    private static JsonNode? ToJsonNode(JsonDocument? document) =>
        document == null ? null : JsonNode.Parse(document.RootElement.GetRawText());


    private async Task ShowNpcDetailActions(JsonElement npc, string npcName, bool tradeAvailable)
    {
        var imagePrompt = GetStr(npc, "image_prompt", "");
        var npcImageKey = GetPrimaryNpcId(npc);
        if (string.IsNullOrWhiteSpace(npcImageKey))
            npcImageKey = npcName;
        var hasImagePrompt = !string.IsNullOrWhiteSpace(imagePrompt);
        var hasExistingImage = _imageService?.EntityImageExists("npc", npcImageKey) == true;
        var hasImageSupport = _imageService != null && (hasImagePrompt || hasExistingImage);
        var npcId = GetPrimaryNpcId(npc);
        var npcInventoryDisplay = BuildNpcInventoryDisplay(npc);
        var hasInspectableItems = npcInventoryDisplay.Items.Count > 0;
        var socialAvailable = !string.IsNullOrWhiteSpace(npcId) && (tradeAvailable || hasImageSupport || hasInspectableItems);

        if (!tradeAvailable && !hasImageSupport && !hasInspectableItems && !socialAvailable)
        {
            WaitForKey();
            return;
        }

        while (true)
        {
            var pendingNpcTalkRequest = socialAvailable
                ? await ActorSocialInteractionRequestState.FindPendingNpcRequestAsync(_fs, npcId, ActorSocialInteractionRequestState.NpcInteractionTypeTalk)
                : null;

            var actions = new List<string>();
            if (socialAvailable)
                actions.Add(pendingNpcTalkRequest == null ? "💬 Поговорить" : "[dim]💬 Разговор ожидает ответа GM[/]");
            if (tradeAvailable)
                actions.Add("🛒 Торговать");
            if (hasInspectableItems)
                actions.Add("📦 Осмотреть предметы");

            if (hasImageSupport)
            {
                var hasImage = _imageService!.EntityImageExists("npc", npcImageKey);
                actions.Add(hasImage ? "🖼 Показать сохранённое изображение" : "🖼 Показать/создать изображение");
                if (hasImage)
                    actions.Add("💾 Экспортировать изображение");
                if (hasImage && hasImagePrompt)
                    actions.Add("♻ Пересоздать изображение");
            }

            actions.Add("← Назад");

            var action = Prompt(new SelectionPrompt<string>()
                .Title("[bold]Действие:[/]")
                .HighlightStyle(new Style(Color.Purple))
                .AddChoices(actions));

            if (action.Contains("Назад"))
                return;

            if (action.Contains("Поговорить", StringComparison.OrdinalIgnoreCase))
            {
                if (pendingNpcTalkRequest != null)
                {
                    MarkupLine("[yellow]Уже есть незакрытый разговор с этим NPC. Дождитесь ответа GM.[/]");
                    return;
                }

                var request = new ActorSocialInteractionRequestState.PendingNpcSocialInteractionRequest
                {
                    NpcId = npcId,
                    NpcName = npcName,
                    InteractionType = ActorSocialInteractionRequestState.NpcInteractionTypeTalk,
                    CreatedAtTurn = await TryReadCurrentTurnNumberAsync()
                };
                await ActorSocialInteractionRequestState.WriteNpcRequestAsync(_fs, request);
                _pendingGmAction =
                    $"[NPC_SOCIAL_TALK_REQUEST] Игрок начинает разговор с NPC '{npcName}' (npcId={npcId}, requestId={request.RequestId}). " +
                    "В accepted turn отыграй сцену и обязательно закрой запрос через npcInteractionJournalUpdates entry с requestId, npcId, interactionType=talk, status=accepted|rejected|cancelled, optional responseMode=talk_scene|warning|refusal|attitude_shift, title, summary, turn и timestamp. " +
                    "NPCJournals остаётся рекомендуемым для внутреннего состояния, но matching npcInteractionJournalUpdates entry обязателен.";
                MarkupLine("[cyan]Разговор с NPC отправлен GM.[/]");
                return;
            }

            if (action.Contains("Торговать"))
            {
                if (!string.IsNullOrWhiteSpace(npcId))
                    await ShowNpcTradePanel(npcId);
                return;
            }

            if (action.Contains("Осмотреть предметы"))
            {
                await ShowNpcHeldItemInspector(npc, npcName);
                continue;
            }

            if (!hasImageSupport)
                continue;

            var imageExists = _imageService!.EntityImageExists("npc", npcImageKey);
            if (action.Contains("Экспортировать", StringComparison.OrdinalIgnoreCase) && imageExists)
            {
                await ExportEntityImageAsync("npc", npcImageKey);
                WaitForKey();
                return;
            }

            if (action.Contains("Пересоздать") && imageExists && hasImagePrompt)
            {
                await RegenerateEntityImageAsync(imagePrompt, "npc", npcImageKey);
                WaitForKey();
                return;
            }

            if (action.Contains("Показать"))
            {
                if (imageExists)
                    _imageService.ShowEntityImage("npc", npcImageKey, forceDisplay: true);
                else if (hasImagePrompt)
                    await _imageService.ShowOrGenerateEntityImageAsync(imagePrompt, "npc", npcImageKey, forceDisplay: true);
                WaitForKey();
                return;
            }
        }
    }


    private async Task ShowNpcHeldItemInspector(JsonElement npc, string npcName)
    {
        while (true)
        {
            var display = BuildNpcInventoryDisplay(npc);
            if (display.IsEmpty || display.Items.Count == 0)
            {
                ShowEmptyPanel("Инвентарь NPC", "У NPC нет предметов для осмотра");
                WaitForKey();
                return;
            }

            var choices = new List<string>();
            foreach (var npcItem in display.Items)
            {
                var choiceItemName = GetNodeStr(npcItem.Data, "name", "?");
                var qty = GetNodeStr(npcItem.Data, "quantity", GetNodeStr(npcItem.Data, "count", ""));
                var itemType = GetNodeStr(npcItem.Data, "type", GetNodeStr(npcItem.Data, "category", ""));
                var meta = new List<string>();
                if (!string.IsNullOrWhiteSpace(itemType))
                    meta.Add(itemType);
                if (!string.IsNullOrWhiteSpace(qty) && qty != "1")
                    meta.Add($"×{qty}");
                if (npcItem.IsEquipped)
                    meta.Add("экипировано");

                choices.Add(meta.Count > 0
                    ? ConsoleLayout.PlainChoiceLabel($"📦 {choiceItemName}", string.Join(" • ", meta))
                    : GameInterface.SafePromptChoice($"📦 {choiceItemName}"));
            }

            choices.Add("← Назад");

            var selected = Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold orange3]🎒 Предметы NPC[/]")
                    .HighlightStyle(new Style(Color.Orange3))
                    .PageSize(15)
                    .AddChoices(choices));

            if (selected == "← Назад")
                return;

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0 || selIdx >= display.Items.Count)
                continue;

            var selectedItem = display.Items[selIdx];
            var selectedItemName = GetNodeStr(selectedItem.Data, "name", "?");
            var readOnlyStatus = selectedItem.IsEquipped ? "⚔ Экипировано у NPC" : "👤 Находится у NPC";

            await ShowItemDetailPanel(
                selectedItem.Key,
                selectedItemName,
                JsonObjectToElement(selectedItem.Data),
                null,
                readOnly: true,
                readOnlyStatusOverride: readOnlyStatus,
                allowInventorySidecars: false);
        }
    }


    private sealed class NpcSkillDisplay
    {
        public List<NpcSkillDisplayEntry> Active { get; } = new();
        public List<NpcSkillDisplayEntry> Passive { get; } = new();
        public bool IsEmpty => Active.Count == 0 && Passive.Count == 0;
    }


    private sealed class NpcSkillDisplayEntry
    {
        public JsonObject Data { get; init; } = new();
        public int? MasteryLevel { get; set; }
        public int? CurrentMasteryProgress { get; set; }
        public int? MasteryProgressNeeded { get; set; }
        public int? MaxMasteryLevel { get; set; }
    }


    private sealed class NpcInventoryDisplay
    {
        public List<NpcInventoryItemDisplay> Items { get; } = new();
        public List<NpcEquipmentDisplay> Equipment { get; } = new();
        public bool IsEmpty => Items.Count == 0 && Equipment.Count == 0;
    }


    private sealed class NpcInventoryItemDisplay
    {
        public string Key { get; init; } = "";
        public JsonObject Data { get; init; } = new();
        public bool IsEquipped { get; set; }
    }


    private sealed class NpcEquipmentDisplay
    {
        public string Slot { get; init; } = "";
        public string ItemName { get; init; } = "";
    }


    private static Dictionary<string, string> BuildNpcRenameMap(JsonDocument doc)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;

        if (!doc.RootElement.TryGetProperty("NPCsRenameData", out var renames) || renames.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in renames.EnumerateArray())
        {
            var oldName = GetStr(item, "oldName", "");
            var newName = GetStr(item, "newName", "");
            if (!string.IsNullOrWhiteSpace(oldName) && !string.IsNullOrWhiteSpace(newName))
                result[oldName] = newName;
        }

        return result;
    }


    private static string ResolveNpcDisplayName(JsonElement npc, IReadOnlyDictionary<string, string> renameMap)
    {
        var name = GetStr(npc, "name", "???");
        return renameMap.TryGetValue(name, out var renamed) ? renamed : name;
    }

    private static string TranslateNpcRarity(string rarity) =>
        rarity.Trim().ToLowerInvariant() switch
        {
            "common" => "обычный",
            "uncommon" => "необычный",
            "rare" => "редкий",
            "epic" => "эпический",
            "legendary" => "легендарный",
            "unique" => "уникальный",
            _ => HumanizeNpcStableToken(rarity)
        };

    private static bool IsDefaultNonCompanionDirective(string directive)
    {
        var normalized = directive.Trim();
        return normalized.Equals("not_companion", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("none", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("no_companion", StringComparison.OrdinalIgnoreCase);
    }

    private static string TranslateNpcCompanionDirective(string directive) =>
        directive.Trim().ToLowerInvariant() switch
        {
            "companion" => "компаньон",
            "active_companion" => "активный компаньон",
            "potential_companion" => "потенциальный компаньон",
            "not_companion" => "не компаньон",
            _ => HumanizeNpcStableToken(directive)
        };

    private static string HumanizeNpcStableToken(string value)
    {
        var text = value.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return text;

        return text
            .Replace('_', ' ')
            .Replace('-', ' ');
    }

    private static string FormatNpcPlayerFacingFreeText(string value)
    {
        var text = value.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return LooksLikeNpcTechnicalToken(text) ? string.Empty : text;
    }

    private static string BuildNpcChoiceLabel(JsonElement npc, string displayName)
    {
        var rel = GetStr(npc, "relationshipLevel", "");
        if (string.IsNullOrEmpty(rel) && npc.TryGetProperty("relationshipData", out var relationshipData) && relationshipData.ValueKind == JsonValueKind.Object)
            rel = GetStr(relationshipData, "currentReputation", "0");
        if (string.IsNullOrEmpty(rel)) rel = "0";

        var readableLocation = FirstNonEmptyString(
            FormatNpcPlayerFacingFreeText(GetStr(npc, "currentLocationName", "")),
            FormatNpcPlayerFacingFreeText(GetStr(npc, "currentLocation", "")));
        var domain = FormatNpcPlayerFacingFreeText(GetStr(npc, "domain", ""));
        var locStr = !string.IsNullOrWhiteSpace(readableLocation) ? $"@ {readableLocation}"
            : !string.IsNullOrWhiteSpace(domain) ? $"🔮 {domain}" : "";
        var relLabel = int.TryParse(rel, out var relNum)
            ? $"♥ {ReputationDisplay.BuildPlainValueLabel(relNum, ReputationScaleKind.NpcRelationship)}"
            : $"♥ {rel}";

        return ConsoleLayout.PlainChoiceLabel($"👤 {displayName}", relLabel, locStr);
    }

    private static string FirstNonEmptyString(params string[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static bool LooksLikeNpcTechnicalToken(string value)
    {
        var hasSeparator = value.Contains('_', StringComparison.Ordinal) ||
                           value.Contains('-', StringComparison.Ordinal);
        if (!hasSeparator)
            return false;

        return value.All(ch => ch is '_' or '-' || char.IsAsciiLetterOrDigit(ch));
    }

    /// <summary>
    /// Collects list-worthy NPC objects from npc_core.json.
    /// </summary>

    private List<JsonElement> CollectNpcListEntries(JsonDocument doc)
    {
        var result = new List<JsonElement>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddNpc(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object) return;

            var name = GetStr(item, "name", "");
            var npcId = GetPrimaryNpcId(item);
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(npcId))
                return;

            var key = !string.IsNullOrWhiteSpace(npcId)
                ? $"id:{npcId}"
                : $"name:{name}";

            if (seenKeys.Add(key))
                result.Add(item);
        }

        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in GetNpcCoreArrayKeys())
            {
                if (doc.RootElement.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                        AddNpc(item);
                }
            }
        }

        return result;
    }


    private static IEnumerable<string> GetNpcCoreArrayKeys()
        => GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections;


    private static IEnumerable<JsonArray> GetNpcCoreArrays(JsonObject root)
    {
        foreach (var key in GetNpcCoreArrayKeys())
            if (root[key] is JsonArray arr)
                yield return arr;
    }

    /// <summary>
    /// Collects all JSON items matching an NPC by ID first, then by name.
    /// </summary>

    private List<JsonElement> CollectNpcEntries(JsonDocument doc, string npcId, string npcName)
    {
        var result = new List<JsonElement>();
        EnumerateJsonItems(doc.RootElement, item =>
        {
            if (MatchesNpcEntry(item, npcId, npcName))
                result.Add(item);
        });
        return result;
    }


    private static string GetPrimaryNpcId(JsonElement item)
    {
        return GetStr(item, "NPCId",
            GetStr(item, "npcId",
                GetStr(item, "id",
                    GetStr(item, "initialId", ""))));
    }


    private static bool MatchesNpcEntry(JsonElement item, string npcId, string npcName)
    {
        var entryId = GetPrimaryNpcId(item);
        if (!string.IsNullOrWhiteSpace(npcId) &&
            !string.IsNullOrWhiteSpace(entryId) &&
            string.Equals(entryId, npcId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var entryName = GetStr(item, "NPCName",
            GetStr(item, "npcName",
                GetStr(item, "name", "")));

        return !string.IsNullOrWhiteSpace(entryName) &&
               string.Equals(entryName, npcName, StringComparison.OrdinalIgnoreCase);
    }


    private NpcSkillDisplay BuildNpcSkillDisplay(JsonDocument doc, string npcId, string npcName)
    {
        var display = new NpcSkillDisplay();
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return display;

        var active = new Dictionary<string, NpcSkillDisplayEntry>(StringComparer.OrdinalIgnoreCase);
        var passive = new Dictionary<string, NpcSkillDisplayEntry>(StringComparer.OrdinalIgnoreCase);

        PopulateNpcSkillChanges(doc.RootElement, "NPCActiveSkillChanges", active, npcId, npcName);
        PopulateNpcSkillChanges(doc.RootElement, "NPCPassiveSkillChanges", passive, npcId, npcName);
        ApplyNpcSkillMastery(doc.RootElement, "NPCSkillMasteryChanges", active, npcId, npcName);
        ApplyNpcSkillMastery(doc.RootElement, "NPCPassiveSkillMasteryChanges", passive, npcId, npcName);

        foreach (var item in active.Values.OrderBy(v => GetNodeStr(v.Data, "skillName", GetNodeStr(v.Data, "name", "")), StringComparer.OrdinalIgnoreCase))
            display.Active.Add(item);
        foreach (var item in passive.Values.OrderBy(v => GetNodeStr(v.Data, "skillName", GetNodeStr(v.Data, "name", "")), StringComparer.OrdinalIgnoreCase))
            display.Passive.Add(item);

        return display;
    }


    private void PopulateNpcSkillChanges(JsonElement root, string propertyName,
        Dictionary<string, NpcSkillDisplayEntry> target, string npcId, string npcName)
    {
        if (!root.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return;

        foreach (var entry in arr.EnumerateArray())
        {
            if (!MatchesNpcEntry(entry, npcId, npcName))
                continue;

            if (entry.TryGetProperty("skillChanges", out var changes) && changes.ValueKind == JsonValueKind.Array)
            {
                foreach (var skill in changes.EnumerateArray())
                {
                    var skillName = GetStr(skill, "skillName", GetStr(skill, "name", ""));
                    if (string.IsNullOrWhiteSpace(skillName))
                        continue;

                    target[skillName] = new NpcSkillDisplayEntry
                    {
                        Data = CloneJsonObject(skill)
                    };
                }
            }

            if (entry.TryGetProperty("skillsToRemove", out var removals) && removals.ValueKind == JsonValueKind.Array)
            {
                foreach (var skillName in removals.EnumerateArray()
                    .Where(v => v.ValueKind == JsonValueKind.String)
                    .Select(v => v.GetString())
                    .Where(v => !string.IsNullOrWhiteSpace(v)))
                {
                    target.Remove(skillName!);
                }
            }
        }
    }


    private void ApplyNpcSkillMastery(JsonElement root, string propertyName,
        Dictionary<string, NpcSkillDisplayEntry> target, string npcId, string npcName)
    {
        if (!root.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return;

        foreach (var entry in arr.EnumerateArray())
        {
            if (!MatchesNpcEntry(entry, npcId, npcName))
                continue;

            var skillName = GetStr(entry, "skillName", "");
            if (string.IsNullOrWhiteSpace(skillName))
                continue;

            if (!target.TryGetValue(skillName, out var skill))
            {
                var placeholder = new JsonObject
                {
                    ["skillName"] = skillName
                };
                skill = new NpcSkillDisplayEntry { Data = placeholder };
                target[skillName] = skill;
            }

            var mastery = GetInt(entry, "newMasteryLevel", int.MinValue);
            if (mastery != int.MinValue) skill.MasteryLevel = mastery;

            var progress = GetInt(entry, "newCurrentMasteryProgress", int.MinValue);
            if (progress != int.MinValue) skill.CurrentMasteryProgress = progress;

            var needed = GetInt(entry, "newMasteryProgressNeeded", int.MinValue);
            if (needed != int.MinValue) skill.MasteryProgressNeeded = needed;

            var maxLevel = GetInt(entry, "newMaxMasteryLevel", int.MinValue);
            if (maxLevel != int.MinValue) skill.MaxMasteryLevel = maxLevel;
        }
    }


    private NpcInventoryDisplay BuildNpcInventoryDisplay(JsonElement npc)
    {
        var display = new NpcInventoryDisplay();
        if (npc.ValueKind != JsonValueKind.Object)
            return display;

        var itemsByKey = new Dictionary<string, NpcInventoryItemDisplay>(StringComparer.Ordinal);
        var order = new List<string>();
        if (npc.TryGetProperty("inventory", out var inventory) && inventory.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in inventory.EnumerateArray())
            {
                if (!MortalItemMaterializationContract.TryReadAcceptedIdentity(item, out var key))
                    continue;

                itemsByKey[key] = new NpcInventoryItemDisplay
                {
                    Key = key,
                    Data = (MortalItemPlayerProjection.CloneItemSemanticValue(CloneJsonObject(item)) as JsonObject) ?? new JsonObject()
                };
                if (!order.Contains(key, StringComparer.Ordinal))
                    order.Add(key);
            }
        }

        var equippedSlots = new Dictionary<string, string>(StringComparer.Ordinal);
        ReadCanonicalEquipment();

        foreach (var key in order)
        {
            if (!itemsByKey.TryGetValue(key, out var item))
                continue;

            item.IsEquipped = equippedSlots.Values.Contains(key, StringComparer.Ordinal);
            display.Items.Add(item);
        }

        foreach (var slot in equippedSlots.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var itemName = itemsByKey.TryGetValue(slot.Value, out var item)
                ? GetNodeStr(item.Data, "name", slot.Value)
                : slot.Value;
            display.Equipment.Add(new NpcEquipmentDisplay
            {
                Slot = slot.Key,
                ItemName = itemName
            });
        }

        return display;

        void ReadCanonicalEquipment()
        {
            var owner = JsonNode.Parse(npc.GetRawText()) as JsonObject;
            if (owner == null ||
                !MortalItemEquipmentAuthority.TryRead(
                    owner,
                    owner["inventory"] as JsonArray,
                    "npc_core.inventory",
                    out var equipmentState,
                    out _))
            {
                return;
            }

            foreach (var slot in equipmentState.Slots)
            {
                if (slot.ItemId == null)
                    continue;

                if (itemsByKey.ContainsKey(slot.ItemId))
                    equippedSlots[slot.StoredSlot] = slot.ItemId;
            }
        }
    }

    private static JsonObject CloneJsonObject(JsonElement item)
    {
        return JsonNode.Parse(item.GetRawText())?.AsObject() ?? new JsonObject();
    }

    private static string GetNodeStr(JsonObject obj, string prop, string def)
    {
        var node = obj[prop];
        if (node == null) return def;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var s) && s != null) return s;
            if (value.TryGetValue<int>(out var i)) return i.ToString();
            if (value.TryGetValue<long>(out var l)) return l.ToString();
            if (value.TryGetValue<double>(out var d)) return d.ToString();
            if (value.TryGetValue<bool>(out var b)) return b ? "true" : "false";
        }

        return node.ToJsonString();
    }

    private static string FormatNpcAttitudeLabel(string value)
    {
        var clean = value.Trim();
        return clean.ToLowerInvariant() switch
        {
            "" => string.Empty,
            "neutral" => "Нейтралитет",
            "friendly" => "Дружелюбие",
            "friend" => "Дружелюбие",
            "ally" => "Союзник",
            "hostile" => "Враждебность",
            "enemy" => "Враг",
            "suspicious" => "Настороженность",
            "trusting" => "Доверие",
            "loyal" => "Верность",
            "rival" => "Соперничество",
            _ => clean
        };
    }


    private static int GetNodeInt(JsonObject obj, string prop, int def = int.MinValue)
    {
        var node = obj[prop];
        if (node == null) return def;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var i)) return i;
            if (value.TryGetValue<long>(out var l) && l <= int.MaxValue && l >= int.MinValue) return (int)l;
            if (value.TryGetValue<string>(out var s) && int.TryParse(s, out var parsed)) return parsed;
        }

        return def;
    }


    private static JsonElement JsonObjectToElement(JsonObject obj)
    {
        return JsonSerializer.SerializeToElement(obj);
    }

    /// <summary>
    /// Renders any JSON object fields that weren't handled by explicit code.
    /// Only shows non-empty string/number/bool fields not in the exclusion set.
    /// </summary>

}
