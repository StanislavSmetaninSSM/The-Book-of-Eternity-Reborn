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
    private static readonly (int cap, string nextTier, bool isPositive)[] RelationshipCaps = {
        (100,  "Доверие и Расположение", true),
        (250,  "Глубокая Связь", true),
        (350,  "Легендарная Преданность", true),
        (-50,  "Противник", false),
        (-200, "Непримиримый Враг", false),
    };


    private void RenderNpcRelationships(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold cyan]💬 Отношения:[/]");

        foreach (var entry in entries)
        {
            var lvlStr = GetStr(entry, "newRelationshipLevel", GetStr(entry, "relationshipLevel", ""));
            var reason = GetStr(entry, "changeReason", "");
            var relType = GetStr(entry, "relationshipType", "");
            var turn = GetStr(entry, "turn", GetStr(entry, "turnNumber", ""));

            // ── Numeric relationship level with full progression display ──
            if (int.TryParse(lvlStr, out var lvlNum))
            {
                var tier = ReputationDisplay.GetTier(ReputationScaleKind.NpcRelationship, lvlNum);
                var tierIcon = tier.Icon ?? "♥";
                lines.Add($"    {tierIcon} {ReputationDisplay.BuildTierMarkup(lvlNum, ReputationScaleKind.NpcRelationship)}: {ReputationDisplay.BuildBarMarkup(lvlNum, ReputationScaleKind.NpcRelationship, 20)} [{tier.Color}]{lvlNum}[/]/400");

                // Show relationship type if inter-NPC
                if (!string.IsNullOrEmpty(relType) && !relType.Equals("player", StringComparison.OrdinalIgnoreCase))
                    lines.Add($"      [dim]Тип: {Markup.Escape(relType)}[/]");

                // Last change reason
                if (!string.IsNullOrEmpty(reason))
                    lines.Add($"      [dim italic]Причина: {Markup.Escape(reason)}[/]");

                // ── Lock status (critical for player!) — show ALWAYS, not just debug ──
                var isLocked = entry.TryGetProperty("isLocked", out var lk) && lk.ValueKind == JsonValueKind.True;
                var currentCap = GetInt(entry, "currentCap", 0);
                var questId = GetStr(entry, "breakthroughQuestId", "");
                var isUnbreakable = questId == "__UNBREAKABLE__";

                // Also check nested lockUpdate / relationshipLock
                if (!isLocked && entry.TryGetProperty("relationshipLock", out var rl) && rl.ValueKind == JsonValueKind.Object)
                {
                    isLocked = rl.TryGetProperty("isLocked", out var lk2) && lk2.ValueKind == JsonValueKind.True;
                    if (currentCap == 0) currentCap = GetInt(rl, "currentCap", 0);
                    if (string.IsNullOrEmpty(questId)) questId = GetStr(rl, "breakthroughQuestId", "");
                    isUnbreakable = questId == "__UNBREAKABLE__";
                }

                if (isLocked)
                {
                    if (isUnbreakable)
                    {
                        lines.Add($"      [bold red]🔒 НЕОБРАТИМО ЗАБЛОКИРОВАНО — примирение невозможно[/]");
                    }
                    else
                    {
                        var capInfo = currentCap != 0 ? $" (порог: {currentCap})" : "";
                        lines.Add($"      [bold yellow]🔒 Заблокировано{capInfo} — требуется квест прорыва[/]");
                        if (!string.IsNullOrEmpty(questId) && questId != "_clear_")
                            lines.Add($"      [yellow]📜 Квест прорыва назначен — выполните его для продвижения[/]");
                        else
                            lines.Add($"      [red]⚠ Квест прорыва НЕ назначен — напомните Мастеру Игры![/]");
                    }
                }

                // ── Progression roadmap — show next cap/tier ──
                if (!isUnbreakable)
                {
                    // Find next positive cap above current level
                    var nextCap = RelationshipCaps
                        .Where(c => c.isPositive && c.cap > lvlNum)
                        .OrderBy(c => c.cap)
                        .FirstOrDefault();

                    if (nextCap != default && lvlNum >= 0)
                    {
                        var pointsToNext = nextCap.cap - lvlNum;
                        if (isLocked && currentCap == nextCap.cap)
                        {
                            lines.Add($"      [dim]→ Следующий ранг: [white]{nextCap.nextTier}[/] (порог {nextCap.cap}) — [yellow]заблокирован, нужен квест[/][/]");
                        }
                        else
                        {
                            lines.Add($"      [dim]→ Следующий ранг: [white]{nextCap.nextTier}[/] (порог {nextCap.cap}, осталось {pointsToNext} очков)[/]");
                        }
                    }
                    else if (lvlNum > 350)
                    {
                        lines.Add($"      [dim]→ [gold1]Максимальный ранг достигнут[/][/]");
                    }

                    // Show negative danger zone
                    if (lvlNum < 0 && lvlNum > -400)
                    {
                        var nextNegCap = RelationshipCaps
                            .Where(c => !c.isPositive && c.cap > lvlNum)
                            .OrderByDescending(c => c.cap)
                            .FirstOrDefault();

                        if (nextNegCap != default)
                        {
                            var pointsToNeg = lvlNum - nextNegCap.cap;
                            lines.Add($"      [dim]→ До [red]{nextNegCap.nextTier}[/]: {Math.Abs(pointsToNeg)} очков[/]");
                        }
                    }
                }
            }
            else
            {
                // Non-numeric relationship level (string-based fallback)
                var line = $"    ♥ [yellow]{Markup.Escape(lvlStr)}[/]";
                if (!string.IsNullOrEmpty(relType))
                    line += $" [dim]({Markup.Escape(relType)})[/]";
                if (!string.IsNullOrEmpty(reason))
                    line += $" — {Markup.Escape(reason)}";
                lines.Add(line);

                // Still check for lock in string-based mode
                var isLockedFallback = entry.TryGetProperty("isLocked", out var lkf) && lkf.ValueKind == JsonValueKind.True;
                if (isLockedFallback)
                    lines.Add($"      [bold yellow]🔒 Отношение заблокировано — требуется квест прорыва[/]");
            }

            if (debugMode && !string.IsNullOrEmpty(turn))
                lines.Add($"      [dim grey](ход {Markup.Escape(turn)})[/]");

            if (debugMode)
            {
                RenderExtraFields(lines, entry, new[] { "NPCName", "npcName", "name",
                    "newRelationshipLevel", "relationshipLevel", "changeReason",
                    "relationshipType", "turn", "turnNumber", "isLocked", "currentCap",
                    "breakthroughQuestId", "relationshipLock" }, "      ");
            }
        }

        // ── Tier legend (compact) ──
        lines.Add("");
        lines.Add("    [dim]Диапазон отношений: -400 = непримиримый враг, 0 = нейтралитет, +400 = легендарная преданность.[/]");
    }


    private void RenderNpcGoals(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold green]🎯 Цели:[/]");
        foreach (var entry in entries)
        {
            var goal = GetStr(entry, "goal", GetStr(entry, "description", ""));
            var priority = GetStr(entry, "priority", "");
            var goalStatus = GetStr(entry, "status", "");

            var line = $"    • [white]{Markup.Escape(goal)}[/]";
            if (!string.IsNullOrEmpty(priority))
                line += $" [dim](приоритет: {Markup.Escape(priority)})[/]";
            if (!string.IsNullOrEmpty(goalStatus))
            {
                var gColor = goalStatus.ToLower() switch
                {
                    "completed" or "завершено" => "green",
                    "failed" or "провалено" => "red",
                    "active" or "активна" => "yellow",
                    _ => "grey"
                };
                line += $" [{gColor}]({Markup.Escape(goalStatus)})[/]";
            }
            lines.Add(line);

            if (debugMode)
                RenderExtraFields(lines, entry, new[] { "NPCName", "npcName", "name",
                    "goal", "description", "priority", "status" }, "      ");
        }
    }


    private void RenderNpcActivities(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold yellow]🏃 Активность:[/]");
        foreach (var entry in entries)
        {
            // Try structured activityUpdate format first (Rule 19.F.1)
            if (entry.TryGetProperty("activityUpdate", out var upd) && upd.ValueKind == JsonValueKind.Object)
            {
                var actName = GetStr(upd, "activityName", GetStr(upd, "name", ""));
                var actDesc = GetStr(upd, "description", "");
                var activeState = GetStr(upd, "activeState", "");
                var totalTime = GetInt(upd, "totalTimeCostMinutes", 0);
                var spentTime = GetInt(upd, "timeSpentMinutes", 0);

                if (!string.IsNullOrEmpty(actName))
                {
                    var statColor = activeState.ToLowerInvariant() switch
                    {
                        "completed" => "green",
                        "abandoned" => "red",
                        _ => "yellow"
                    };
                    var line = $"    ⚡ [white]{Markup.Escape(actName)}[/]";
                    if (!string.IsNullOrEmpty(activeState))
                        line += $" [{statColor}]({Markup.Escape(activeState)})[/]";
                    lines.Add(line);
                }
                if (!string.IsNullOrEmpty(actDesc))
                    lines.Add($"      [dim italic]{Markup.Escape(actDesc)}[/]");

                if (totalTime > 0)
                {
                    var pct = Math.Min(100, spentTime * 100 / totalTime);
                    var barColor = pct >= 80 ? "green" : pct >= 50 ? "yellow" : "cyan";
                    lines.Add($"      Прогресс выполнения: {ConsoleLayout.CreateBarFromPercent(pct, 10, barColor)} {pct}% ({FormatMinutes(spentTime)}/{FormatMinutes(totalTime)})");
                }
            }
            else
            {
                // Fallback: flat format
                var activity = GetStr(entry, "activityName",
                    GetStr(entry, "activity", GetStr(entry, "description", "")));
                var actLocation = GetStr(entry, "location", "");
                var actStatus = GetStr(entry, "status",
                    GetStr(entry, "activeState", GetStr(entry, "completed", "")));
                var actDesc = GetStr(entry, "description",
                    activity == GetStr(entry, "description", "") ? "" : GetStr(entry, "description", ""));

                var line = $"    ⚡ [white]{Markup.Escape(activity)}[/]";
                if (!string.IsNullOrEmpty(actLocation))
                    line += $" [dim]📍 {Markup.Escape(actLocation)}[/]";
                if (!string.IsNullOrEmpty(actStatus))
                {
                    var statColor = actStatus.ToLowerInvariant() switch
                    {
                        "completed" => "green",
                        "abandoned" => "red",
                        _ => "yellow"
                    };
                    line += $" [{statColor}]({Markup.Escape(actStatus)})[/]";
                }
                lines.Add(line);

                // Show description if different from activity name
                if (!string.IsNullOrEmpty(actDesc) && actDesc != activity)
                    lines.Add($"      [dim italic]{Markup.Escape(actDesc)}[/]");

                // Progress bar if time data available
                var totalTime = GetInt(entry, "totalTimeCostMinutes", 0);
                var spentTime = GetInt(entry, "timeSpentMinutes", 0);
                if (totalTime > 0)
                {
                    var pct = Math.Min(100, spentTime * 100 / totalTime);
                    var barColor = pct >= 80 ? "green" : pct >= 50 ? "yellow" : "cyan";
                    lines.Add($"      Прогресс выполнения: {ConsoleLayout.CreateBarFromPercent(pct, 10, barColor)} {pct}% ({FormatMinutes(spentTime)}/{FormatMinutes(totalTime)})");
                }
            }

            // Narrative summary for completed activities
            var narrative = GetStr(entry, "narrativeSummary", "");
            if (!string.IsNullOrEmpty(narrative))
                lines.Add($"      📝 [dim]{Markup.Escape(narrative)}[/]");

            if (debugMode)
                RenderExtraFields(lines, entry, new[] { "NPCName", "npcName", "name", "NPCId",
                    "activity", "activityName", "activityUpdate", "description", "location",
                    "status", "activeState", "completed", "narrativeSummary",
                    "totalTimeCostMinutes", "timeSpentMinutes" }, "      ");
        }
    }


    private void RenderNpcInventory(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode)
    {
        if (doc == null) return;
        var display = BuildNpcInventoryDisplay(doc, npcId, npcName);
        if (display.IsEmpty) return;

        if (display.Items.Count > 0)
        {
            lines.Add("");
            lines.Add($"  [bold orange3]🎒 Инвентарь:[/]");
            foreach (var item in display.Items)
            {
                var itemName = GetNodeStr(item.Data, "name", "?");
                var qty = GetNodeStr(item.Data, "quantity", GetNodeStr(item.Data, "count", ""));
                var itemType = GetNodeStr(item.Data, "type", GetNodeStr(item.Data, "category", ""));
                var resource = GetNodeStr(item.Data, "resource", "");
                var maxResource = GetNodeStr(item.Data, "maximumResource", "");
                var resourceType = GetNodeStr(item.Data, "resourceType", "");
                var durability = GetNodeStr(item.Data, "durability", "");

                var line = item.IsEquipped
                    ? $"    ⚔ [green]{Markup.Escape(itemName)}[/] [green](экипировано)[/]"
                    : $"    • [white]{Markup.Escape(itemName)}[/]";
                if (!string.IsNullOrEmpty(qty) && qty != "1")
                    line += $" ×{Markup.Escape(qty)}";
                if (!string.IsNullOrEmpty(itemType))
                    line += $" [dim]({Markup.Escape(itemType)})[/]";
                if (!string.IsNullOrEmpty(resource))
                {
                    var resourceLabel = !string.IsNullOrEmpty(resourceType) ? $" {Markup.Escape(resourceType)}" : "";
                    var maxLabel = !string.IsNullOrEmpty(maxResource) ? $"/{Markup.Escape(maxResource)}" : "";
                    line += $" [cyan]{Markup.Escape(resource)}{maxLabel}{resourceLabel}[/]";
                }
                if (!string.IsNullOrEmpty(durability))
                    line += $" [dim]прочность: {Markup.Escape(durability)}[/]";
                lines.Add(line);

                if (debugMode)
                    RenderExtraFields(lines, JsonObjectToElement(item.Data), new[] { "name", "quantity", "count",
                        "type", "category", "equipped", "resource", "maximumResource", "resourceType",
                        "durability", "existedId", "initialId" }, "      ");
            }
        }

        if (display.Equipment.Count > 0)
        {
            lines.Add("");
            lines.Add($"  [bold orange3]🛡️ Экипировка:[/]");
            foreach (var eq in display.Equipment)
                lines.Add($"    • [dim]{Markup.Escape(eq.Slot)}:[/] [white]{Markup.Escape(eq.ItemName)}[/]");
        }
    }


    private void RenderNpcEffects(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold]✨ Эффекты:[/]");
        foreach (var entry in entries)
        {
            var effType = GetStr(entry, "effectType", GetStr(entry, "type", ""));
            var effDesc = GetStr(entry, "description", GetStr(entry, "effect", ""));
            var duration = GetStr(entry, "duration", GetStr(entry, "turnsRemaining", ""));
            var isWound = effType.ToLower().Contains("wound") || effType.ToLower().Contains("ран")
                       || effDesc.ToLower().Contains("wound") || effDesc.ToLower().Contains("ран");
            var isDebuff = effType.ToLower().Contains("debuff") || effType.ToLower().Contains("негатив");
            var color = isWound ? "red" : isDebuff ? "orange3" : "green";
            var icon = isWound ? "🩸" : isDebuff ? "⚠️" : "✨";

            var displayText = !string.IsNullOrEmpty(effDesc) ? effDesc : effType;
            var line = $"    {icon} [{color}]{Markup.Escape(displayText)}[/]";
            if (!string.IsNullOrEmpty(duration))
                line += $" [dim](длительность: {Markup.Escape(duration)})[/]";
            lines.Add(line);

            if (debugMode)
                RenderExtraFields(lines, entry, new[] { "NPCName", "npcName", "name",
                    "effectType", "type", "description", "effect", "duration", "turnsRemaining" }, "      ");
        }
    }


    private void RenderNpcSkills(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode)
    {
        if (doc == null) return;
        var display = BuildNpcSkillDisplay(doc, npcId, npcName);
        if (display.IsEmpty) return;

        if (display.Active.Count > 0)
        {
            lines.Add("");
            lines.Add($"  [bold cyan]⚔ Активные навыки:[/]");
            foreach (var skill in display.Active)
                RenderNpcSkillEntry(lines, skill, "cyan", debugMode);
        }

        if (display.Passive.Count > 0)
        {
            lines.Add($"  [bold dim]🛡️ Пассивные навыки:[/]");
            foreach (var skill in display.Passive)
                RenderNpcSkillEntry(lines, skill, "white", debugMode);
        }
    }


    private void RenderNpcSkillEntry(List<string> lines, NpcSkillDisplayEntry skill, string color, bool debugMode)
    {
        var name = GetNodeStr(skill.Data, "skillName", GetNodeStr(skill.Data, "name", "?"));
        var type = GetNodeStr(skill.Data, "type", "");
        var cooldown = GetNodeStr(skill.Data, "cooldown", "");
        var description = GetNodeStr(skill.Data, "skillDescription", GetNodeStr(skill.Data, "description", ""));

        var line = $"    • [{color}]{Markup.Escape(name)}[/]";
        if (skill.MasteryLevel.HasValue)
            line += $" [yellow](мастерство {skill.MasteryLevel.Value})[/]";
        if (!string.IsNullOrEmpty(type))
            line += $" [dim]({Markup.Escape(type)})[/]";
        if (!string.IsNullOrEmpty(cooldown))
            line += $" [dim](перезарядка: {Markup.Escape(cooldown)})[/]";
        lines.Add(line);

        if (skill.CurrentMasteryProgress.HasValue || skill.MasteryProgressNeeded.HasValue || skill.MaxMasteryLevel.HasValue)
        {
            var masteryBits = new List<string>();
            if (skill.CurrentMasteryProgress.HasValue || skill.MasteryProgressNeeded.HasValue)
                masteryBits.Add($"прогресс {skill.CurrentMasteryProgress.GetValueOrDefault(0)}/{skill.MasteryProgressNeeded.GetValueOrDefault(0)}");
            if (skill.MaxMasteryLevel.HasValue)
                masteryBits.Add($"макс. {skill.MaxMasteryLevel.Value}");
            lines.Add($"      [dim]{Markup.Escape(string.Join(" • ", masteryBits))}[/]");
        }

        if (!string.IsNullOrEmpty(description))
            lines.Add($"      [dim]{Markup.Escape(description)}[/]");

        if (debugMode)
            RenderExtraFields(lines, JsonObjectToElement(skill.Data), new[] { "skillName", "name",
                "skillDescription", "description", "type", "cooldown" }, "      ");
    }

    // ── Personality (npc_personality.json) — visible to player ──

    private void RenderNpcPersonality(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode = false)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;
        lines.Add("");
        lines.Add($"  [bold magenta1]🧠 Личность / Черты характера:[/]");
        foreach (var entry in entries) {
            var tName = GetStr(entry, "traitName", "");
            var tVal = GetInt(entry, "value", -1);
            var tValDesc = GetStr(entry, "valueDescription", "");
            if (!string.IsNullOrEmpty(tName)) {
                var line = $"    • [white]{Markup.Escape(tName)}[/]";
                if (tVal >= 0) {
                    var tBarW = 10;
                    var tFilled = Math.Clamp(tVal * tBarW / 10, 0, tBarW);
                    var tColor = tVal >= 7 ? "green" : tVal >= 4 ? "yellow" : "red";
                    line += $" [{tColor}]{new string('█', tFilled)}[/][dim]{new string('░', tBarW - tFilled)}[/] [{tColor}]{tVal}/10[/]";
                }
                if (!string.IsNullOrEmpty(tValDesc)) line += $" [dim]— {Markup.Escape(tValDesc)}[/]";
                lines.Add(line);
            }
            var traits = GetStr(entry, "traits", "");
            var temperament = GetStr(entry, "temperament", "");
            var morality = GetStr(entry, "morality", GetStr(entry, "alignment", ""));
            if (!string.IsNullOrEmpty(traits)) lines.Add($"    🏷️ Черты: [white]{Markup.Escape(traits)}[/]");
            if (!string.IsNullOrEmpty(temperament)) lines.Add($"    🌡️ Темперамент: [white]{Markup.Escape(temperament)}[/]");
            if (!string.IsNullOrEmpty(morality)) lines.Add($"    ⚖️ Мораль: [white]{Markup.Escape(morality)}[/]");
            if (debugMode) RenderExtraFields(lines, entry, new[] { "NPCName", "npcName", "name", "traits", "temperament", "traitName", "value", "valueDescription", "morality", "alignment" }, "    ");
        }
    }

    // ── Journals (npc_journals.json -> NPCJournals) ──

    private void RenderNpcJournals(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode = false)
    {
        if (doc == null) return;

        JsonElement target;
        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("NPCJournals", out var journals))
            target = journals;
        else
            target = doc.RootElement;

        var entries = new List<JsonElement>();
        EnumerateJsonItems(target, item =>
        {
            if (MatchesNpcEntry(item, npcId, npcName))
                entries.Add(item);
        });

        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold steelblue1]📓 Дневник / Мысли:[/]");
        foreach (var entry in entries)
        {
            var thought = GetStr(entry, "lastJournalNote", "");
            var turn = GetStr(entry, "turn", GetStr(entry, "turnNumber", ""));
            var topic = GetStr(entry, "topic", "");

            var turnTag = !string.IsNullOrEmpty(turn) ? $"[dim]Ход {Markup.Escape(turn)}:[/] " : "";
            var topicTag = !string.IsNullOrEmpty(topic) ? $"[steelblue1]{Markup.Escape($"[{topic}]")}[/] " : "";
            
            if (!string.IsNullOrEmpty(thought))
                lines.Add($"    {turnTag}{topicTag}[italic]«{Markup.Escape(thought)}»[/]");

            if (entry.TryGetProperty("journalEntries", out var journalEntries) &&
                journalEntries.ValueKind == JsonValueKind.Array &&
                journalEntries.GetArrayLength() > 0)
            {
                foreach (var journalEntry in journalEntries.EnumerateArray())
                {
                    if (journalEntry.ValueKind != JsonValueKind.Object) continue;
                    var timestamp = GetStr(journalEntry, "timestamp", "");
                    var eventName = GetStr(journalEntry, "event", "");
                    var description = GetStr(journalEntry, "description", "");
                    var emotionalImpact = GetStr(journalEntry, "emotionalImpact", "");
                    var relationshipChange = GetStr(journalEntry, "relationshipChange", "");

                    var entryPrefix = new List<string>();
                    if (!string.IsNullOrWhiteSpace(timestamp))
                        entryPrefix.Add(Markup.Escape(timestamp));
                    if (!string.IsNullOrWhiteSpace(eventName))
                        entryPrefix.Add(Markup.Escape(eventName));
                    var prefix = entryPrefix.Count > 0
                        ? $"      [dim]{string.Join(" • ", entryPrefix)}[/]"
                        : "      [dim]Запись[/]";
                    lines.Add(prefix);
                    if (!string.IsNullOrWhiteSpace(description))
                        lines.Add($"        [white]{Markup.Escape(description)}[/]");
                    if (!string.IsNullOrWhiteSpace(emotionalImpact))
                        lines.Add($"        [magenta1]Эмоциональный след:[/] {Markup.Escape(emotionalImpact)}");
                    if (!string.IsNullOrWhiteSpace(relationshipChange))
                        lines.Add($"        [cyan]Изменение отношения:[/] {Markup.Escape(relationshipChange)}");
                }
            }

            if (debugMode)
            {
                var context = GetStr(entry, "context", "");
                if (!string.IsNullOrEmpty(context))
                    lines.Add($"      [dim grey]🔍 {Markup.Escape(context)}[/]");
            }
        }
    }

    private void RenderNpcInteractionJournal(List<string> lines, JsonDocument? doc, string npcId)
    {
        var entries = CollectActorJournalEntryElements(doc, NpcInteractionJournalState.ActorIdProperty, npcId);
        if (entries.Count == 0)
            return;

        lines.Add("");
        lines.Add("  [bold steelblue1]🗂 Память взаимодействий:[/]");
        foreach (var entry in entries.Take(5))
            lines.Add($"    • [white]{Markup.Escape(BuildActorJournalLine(entry))}[/]");
    }

    // ── Masks (npc_masks.json) — Rule 17 Social Roles ──

    private void RenderNpcMasks(List<string> lines, JsonDocument? doc, string npcId, string npcName)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold red]🎭 Маски (социальные роли):[/]");
        foreach (var entry in entries)
        {
            var maskName = GetStr(entry, "maskName", GetStr(entry, "activeMask", GetStr(entry, "name", "")));
            var maskDesc = GetStr(entry, "description", GetStr(entry, "behavior", ""));
            var isActive = entry.TryGetProperty("isActive", out var ia) && ia.ValueKind == JsonValueKind.True;
            var trigger = GetStr(entry, "trigger", GetStr(entry, "condition", ""));

            var activeStr = isActive ? " [green]● активна[/]" : "";
            if (!string.IsNullOrEmpty(maskName))
                lines.Add($"    🎭 [red]{Markup.Escape(maskName)}[/]{activeStr}");
            if (!string.IsNullOrEmpty(maskDesc))
                lines.Add($"      [white]{Markup.Escape(maskDesc)}[/]");
            if (!string.IsNullOrEmpty(trigger))
                lines.Add($"      [dim]Триггер: {Markup.Escape(trigger)}[/]");

            RenderExtraFields(lines, entry, new[] { "NPCName", "npcName", "name",
                "maskName", "activeMask", "description", "behavior", "isActive",
                "trigger", "condition" }, "      ");
        }
    }

    // ── Memories (npc_memory.json -> NPCUnlockedMemories) ──

    private void RenderNpcMemories(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode = false)
    {
        if (doc == null) return;

        JsonElement target;
        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("NPCUnlockedMemories", out var memories))
            target = memories;
        else
            target = doc.RootElement;

        var entries = new List<JsonElement>();
        EnumerateJsonItems(target, item =>
        {
            if (MatchesNpcEntry(item, npcId, npcName))
                entries.Add(item);
        });

        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold lightslateblue]✨ Воспоминания Души[/]");
        foreach (var entry in entries)
        {
            var rarity = GetStr(entry, "rarity", "Common");
            var rColor = rarity.ToLower() switch
            {
                "rare" or "редкий" => "blue",
                "epic" or "эпический" => "purple",
                "legendary" or "легендарный" => "orange1",
                _ => "white"
            };

            var title = GetStr(entry, "title", "???");
            var desc = GetStr(entry, "content", "");
            var unlockedAt = GetStr(entry, "unlockedAtRelationshipLevel", "");

            var line = $"    • [{rColor}]{Markup.Escape(title)}[/]";
            if (!string.IsNullOrEmpty(rarity))
                line += $" [dim]({Markup.Escape(rarity)})[/]";
            if (!string.IsNullOrEmpty(unlockedAt))
                line += $" [dim]репутация {Markup.Escape(unlockedAt)}[/]";
            lines.Add(line);
            if (!string.IsNullOrEmpty(desc))
                lines.Add($"      [italic]{Markup.Escape(desc)}[/]");

            if (debugMode)
                RenderExtraFields(lines, entry, new[] { "NPCId", "npcId", "NPCName", "npcName",
                    "name", "memoryId", "title", "content", "rarity",
                    "unlockedAtRelationshipLevel" }, "      ");
        }
    }

    // ── Fate Cards (npc_fate_cards.json) — unlocked cards visible to player ──

    private void RenderNpcFateCards(List<string> lines, JsonDocument? doc, string npcId, string npcName, bool debugMode = false)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold gold1]🃏 Карты судьбы:[/]");
        lines.Add($"  [dim italic]Вехи развития НПС — открываются через отношения и сюжет[/]");
        foreach (var entry in entries)
        {
            var cardName = GetStr(entry, "cardName", GetStr(entry, "name", ""));
            var cardDesc = GetStr(entry, "description", GetStr(entry, "effect", ""));
            var cardType = GetStr(entry, "cardType", GetStr(entry, "type", ""));
            var isUnlocked = (entry.TryGetProperty("isUnlocked", out var iu) && iu.ValueKind == JsonValueKind.True)
                          || (entry.TryGetProperty("isRevealed", out var ir) && ir.ValueKind == JsonValueKind.True);

            // Unlock conditions
            var reqRelLevel = GetStr(entry, "requiredRelationshipLevel", "");
            var plotCondition = GetStr(entry, "plotConditionDescription", "");
            var conjunction = GetStr(entry, "conjunction", "AND");

            var statusStr = isUnlocked
                ? " [green]● разблокирована[/]"
                : " [dim red]🔒 заблокирована[/]";

            if (!string.IsNullOrEmpty(cardName))
                lines.Add($"    🃏 [gold1]{Markup.Escape(cardName)}[/]{statusStr}");
            if (!string.IsNullOrEmpty(cardType))
                lines.Add($"      Тип: [white]{Markup.Escape(cardType)}[/]");
            if (!string.IsNullOrEmpty(cardDesc))
                lines.Add($"      [white]{Markup.Escape(cardDesc)}[/]");

            // Show unlock conditions for locked cards
            if (!isUnlocked)
            {
                var conditions = new List<string>();
                if (!string.IsNullOrEmpty(reqRelLevel))
                {
                    if (int.TryParse(reqRelLevel, out var reqRep))
                    {
                        var tierLabel = ReputationDisplay.GetTier(ReputationScaleKind.NpcRelationship, reqRep).Label;
                        conditions.Add($"отношение ≥ {reqRep} ({tierLabel})");
                    }
                    else
                        conditions.Add($"отношение: {Markup.Escape(reqRelLevel)}");
                }
                if (!string.IsNullOrEmpty(plotCondition))
                    conditions.Add(Markup.Escape(plotCondition));

                if (conditions.Count > 0)
                {
                    var conjText = conjunction.Equals("OR", StringComparison.OrdinalIgnoreCase) ? " ИЛИ " : " И ";
                    lines.Add($"      [dim]Условия: {string.Join(conjText, conditions)}[/]");
                }
            }

            // Show rewards for unlocked cards
            if (isUnlocked && entry.TryGetProperty("rewards", out var rewards) && rewards.ValueKind == JsonValueKind.Object)
            {
                var rewardDesc = GetStr(rewards, "description", "");
                if (!string.IsNullOrEmpty(rewardDesc))
                    lines.Add($"      [italic green]📜 {Markup.Escape(rewardDesc)}[/]");

                RenderFateCardRewardArray(lines, rewards, "newActiveSkills", "⚔ Новые активные навыки");
                RenderFateCardRewardArray(lines, rewards, "newPassiveSkills", "🛡 Новые пассивные навыки");
                RenderFateCardRewardArray(lines, rewards, "statBoosts", "📈 Усиления характеристик");
                RenderFateCardRewardArray(lines, rewards, "newServices", "🤝 Новые услуги");
                RenderFateCardRewardArray(lines, rewards, "otherNarrativeRewards", "🌟 Особые награды");
                RenderFateCardRewardArray(lines, rewards, "tacticalTriggers", "⚡ Тактические триггеры");
            }

            if (debugMode)
                RenderExtraFields(lines, entry, new[] { "NPCName", "npcName", "name",
                    "cardName", "cardId", "description", "effect", "cardType", "type",
                    "isRevealed", "isUnlocked", "rewards", "requiredRelationshipLevel",
                    "plotConditionDescription", "conjunction", "image_prompt" }, "      ");
        }
    }


    private void RenderFateCardRewardArray(List<string> lines, JsonElement rewards, string fieldName, string label)
    {
        if (!rewards.TryGetProperty(fieldName, out var arr)) return;
        if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
        {
            lines.Add($"      [yellow]{label}:[/]");
            foreach (var item in arr.EnumerateArray())
            {
                var text = item.ValueKind == JsonValueKind.String
                    ? item.GetString() ?? ""
                    : GetStr(item, "name", GetStr(item, "description", item.ToString()));
                if (!string.IsNullOrEmpty(text))
                    lines.Add($"        • [white]{Markup.Escape(text)}[/]");
            }
        }
        else if (arr.ValueKind == JsonValueKind.String)
        {
            var text = arr.GetString() ?? "";
            if (!string.IsNullOrEmpty(text))
                lines.Add($"      [yellow]{label}:[/] [white]{Markup.Escape(text)}[/]");
        }
    }

    // ── Custom States (npc_custom_states.json) — Rule 25.A ──

    private void RenderNpcCustomStates(List<string> lines, JsonDocument? doc, string npcId, string npcName)
    {
        if (doc == null) return;
        var entries = CollectNpcEntries(doc, npcId, npcName);
        if (entries.Count == 0) return;

        lines.Add("");
        lines.Add($"  [bold magenta]📊 Особые состояния:[/]");
        foreach (var entry in entries)
        {
            // Support both flat format and nested stateChanges array (Rule 25.A.2)
            var stateItems = new List<JsonElement>();
            if (entry.TryGetProperty("stateChanges", out var sc) && sc.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in sc.EnumerateArray()) stateItems.Add(s);
            }
            else
            {
                // Flat format: entry itself is a state object
                stateItems.Add(entry);
            }

            foreach (var item in stateItems)
                RenderCustomStateItem(lines, item, "    ");
        }
    }

    /// <summary>Renders a single Custom State Object (Rule 25.1) with progress bar, thresholds, progression.</summary>

    private static void RenderCustomStateItem(List<string> lines, JsonElement item, string indent)
    {
        var name = GetStr(item, "stateName", GetStr(item, "stateKey", GetStr(item, "key", GetStr(item, "name", ""))));
        if (string.IsNullOrEmpty(name)) return;

        // Try numeric display with progress bar
        var hasNumeric = item.TryGetProperty("currentValue", out var curProp)
            && (curProp.ValueKind == JsonValueKind.Number || curProp.ValueKind == JsonValueKind.String);
        if (hasNumeric)
        {
            var cur = GetInt(item, "currentValue", 0);
            var min = GetInt(item, "minValue", 0);
            var max = GetInt(item, "maxValue", 100);
            var range = max - min;
            var pct = range > 0 ? ((cur - min) * 100 / range) : 0;
            var barColor = pct > 66 ? "green" : pct > 33 ? "yellow" : "red";
            var barFill = Math.Clamp(pct / 5, 0, 20);
            var barEmpty = 20 - barFill;
            lines.Add($"{indent}[{barColor}]{Markup.Escape(name)}[/]: [{barColor}]{new string('█', barFill)}[/][dim]{new string('░', barEmpty)}[/] {cur}/{max}");
        }
        else
        {
            // Fallback: string value
            var stateVal = GetStr(item, "stateValue", GetStr(item, "value", GetStr(item, "currentValue", "")));
            var line = $"{indent}[white]{Markup.Escape(name)}[/]";
            if (!string.IsNullOrEmpty(stateVal))
                line += $": [cyan]{Markup.Escape(stateVal)}[/]";
            lines.Add(line);
        }

        var desc = GetStr(item, "description", "");
        if (!string.IsNullOrEmpty(desc))
            lines.Add($"{indent}  [dim]{Markup.Escape(desc)}[/]");

        // Progression rule
        if (item.TryGetProperty("progressionRule", out var pr) && pr.ValueKind == JsonValueKind.Object)
        {
            var changePerTurn = GetStr(pr, "changePerTurn", "");
            var prDesc = GetStr(pr, "description", "");
            if (!string.IsNullOrEmpty(changePerTurn))
                lines.Add($"{indent}  📈 За ход: [yellow]{Markup.Escape(changePerTurn)}[/]" +
                    (!string.IsNullOrEmpty(prDesc) ? $" [dim]({Markup.Escape(prDesc)})[/]" : ""));
        }

        // Thresholds (Block 25)
        if (item.TryGetProperty("thresholds", out var th) && th.ValueKind == JsonValueKind.Array && th.GetArrayLength() > 0)
        {
            lines.Add($"{indent}  [bold]Пороги:[/]");
            var curVal = GetInt(item, "currentValue", 0);
            foreach (var t in th.EnumerateArray())
            {
                var lvlName = GetStr(t, "levelName", "?");
                var trigVal = GetStr(t, "triggerValue", "");
                var trigCond = GetStr(t, "triggerCondition", "");
                var tColor = "dim";
                if (int.TryParse(trigVal, out var tVal))
                {
                    if (trigCond.Contains("<=") && curVal <= tVal) tColor = "red";
                    else if (trigCond.Contains(">=") && curVal >= tVal) tColor = "red";
                }
                lines.Add($"{indent}    [{tColor}]• {Markup.Escape(lvlName)}: {Markup.Escape(trigCond)} {Markup.Escape(trigVal)}[/]");
                if (t.TryGetProperty("associatedEffects", out var ae) && ae.ValueKind == JsonValueKind.Array)
                {
                    foreach (var eff in ae.EnumerateArray())
                    {
                        var eType = GetStr(eff, "effectType", "?");
                        var eVal = GetStr(eff, "value", "");
                        var eDesc = GetStr(eff, "effectDescription", "");
                        var eLine = $"{indent}      ⚡ [{(eType.ToLower().Contains("damage") ? "red" : "yellow")}]{Markup.Escape(eType)}[/] {Markup.Escape(eVal)}";
                        if (!string.IsNullOrEmpty(eDesc)) eLine += $" [dim]— {Markup.Escape(eDesc)}[/]";
                        lines.Add(eLine);
                    }
                }
            }
        }
    }

    // ═════════════════════════════════════════════════════════
    // NPC Helper Methods
    // ═════════════════════════════════════════════════════════


    private static void RenderExtraFields(List<string> lines, JsonElement obj, string[] excludeNames, string indent)
    {
        if (obj.ValueKind != JsonValueKind.Object) return;
        var exclude = new HashSet<string>(excludeNames);
        foreach (var prop in obj.EnumerateObject())
        {
            if (exclude.Contains(prop.Name)) continue;
            if (prop.Name.StartsWith("_")) continue;
            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.String:
                    var sv = prop.Value.GetString() ?? "";
                    if (sv.Length > 0)
                        lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(prop.Name))}: {Markup.Escape(sv)}[/]");
                    break;
                case JsonValueKind.Number:
                    lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(prop.Name))}: {prop.Value}[/]");
                    break;
                case JsonValueKind.True:
                    lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(prop.Name))}: да[/]");
                    break;
                case JsonValueKind.False:
                    lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(prop.Name))}: нет[/]");
                    break;
                case JsonValueKind.Array:
                    var arrItems = new List<string>();
                    foreach (var el in prop.Value.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.String)
                            arrItems.Add(el.GetString() ?? "");
                        else
                            arrItems.Add(el.GetRawText());
                    }
                    if (arrItems.Count > 0)
                        lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(prop.Name))}: {Markup.Escape(string.Join(", ", arrItems))}[/]");
                    break;
            }
        }
    }


    private static string NpcFieldToRussian(string fieldName) => fieldName switch
    {
        "npcId" or "id" => "ID",
        "role" or "occupation" => "Роль",
        "race" => "Раса",
        "appearance" => "Внешность",
        "status" => "Статус",
        "relationshipLevel" => "Уровень отношений",
        "currentLocation" or "location" => "Локация",
        "lastInteraction" => "Последнее взаимодействие",
        "interactionType" => "Тип взаимодействия",
        "playerReputation" => "Репутация у игрока",
        "loyalty" => "Лояльность",
        "trust" => "Доверие",
        "fear" => "Страх",
        "respect" => "Уважение",
        "affection" => "Привязанность",
        "mood" or "emotion" => "Настроение",
        "health" or "hitPoints" => "Здоровье",
        "energy" or "stamina" => "Энергия",
        "level" => "Уровень",
        "class" => "Класс",
        "faction" or "factionName" => "Фракция",
        "alignment" or "morality" => "Мораль",
        "isHostile" => "Враждебен",
        "isAlly" => "Союзник",
        "isEssential" => "Ключевой персонаж",
        "isHidden" => "Скрыт",
        "progressionType" => "Тип развития",
        "playerCompanionDirective" => "Директива игрока",
        "summary" => "Кратко",
        "description" => "Описание",
        "notes" => "Заметки",
        "recentSignals" => "Недавние признаки",
        "historyManipulationCoefficient" => "Коэффициент вмешательства в историю",
        "personalityArchetype" => "Архетип личности",
        "culturalStance" => "Культурная позиция",
        "worldview" => "Мировоззрение",
        "rarity" => "Редкость",
        "experience" => "Опыт",
        "items" => "Предметы",
        "age" => "Возраст",
        "goldAmount" or "money" or "gold" => "Золото",
        "combatStyle" => "Боевой стиль",
        "weakness" or "vulnerabilities" => "Уязвимости",
        "resistances" => "Сопротивления",
        "immunities" => "Иммунитеты",
        "changeReason" or "reason" => "Причина",
        "turn" or "turnNumber" => "Ход",
        "timestamp" => "Время",
        _ => fieldName
    };

}
