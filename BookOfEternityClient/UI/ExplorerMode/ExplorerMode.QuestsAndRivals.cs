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
{private async Task ShowQuests()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/quests/regular_quests.json");
        var soulDoc = await _stateManager.LoadGameStateFileAsync("game_state/quests/soul_quests.json");
        var histDoc = await _stateManager.LoadGameStateFileAsync("game_state/quests/quest_history.json");

        // Collect all quests: (displayLabel, element, isSoul)
        var quests = new List<(string label, JsonElement el, bool isSoul)>();

        if (doc != null)
        {
            EnumerateArray(doc.RootElement, "quests", item =>
            {
                var name = GetStr(item, "questName", GetStr(item, "title", "???"));
                var status = GetStr(item, "status", "Active").ToLower();
                var icon = status switch { "completed" => "✅", "failed" => "❌", _ => "🔄" };
                quests.Add(($"{icon} {name}", item, false));
            });
            // Also try root-level array
            if (quests.Count == 0)
                EnumerateJsonItems(doc.RootElement, item =>
                {
                    var name = GetStr(item, "questName", GetStr(item, "title", "???"));
                    var status = GetStr(item, "status", "Active").ToLower();
                    var icon = status switch { "completed" => "✅", "failed" => "❌", _ => "🔄" };
                    quests.Add(($"{icon} {name}", item, false));
                });
        }

        if (soulDoc != null)
        {
            EnumerateArray(soulDoc.RootElement, "quests", item =>
            {
                var name = GetStr(item, "questName", GetStr(item, "title", "???"));
                var status = GetStr(item, "status", "Active").ToLower();
                var icon = status switch { "completed" => "✅", "failed" => "❌", _ => "🔄" };
                var rivalPrefix = HasRelatedRivalArc(item) ? "🧵 " : "";
                quests.Add(($"{rivalPrefix}🌟 {icon} {name}", item, true));
            });
            if (!quests.Any(q => q.isSoul))
                EnumerateJsonItems(soulDoc.RootElement, item =>
                {
                    var name = GetStr(item, "questName", GetStr(item, "title", "???"));
                    var status = GetStr(item, "status", "Active").ToLower();
                    var icon = status switch { "completed" => "✅", "failed" => "❌", _ => "🔄" };
                    var rivalPrefix = HasRelatedRivalArc(item) ? "🧵 " : "";
                    quests.Add(($"{rivalPrefix}🌟 {icon} {name}", item, true));
                });
        }

        // Add history quests
        var historyQuests = new List<(string label, JsonElement el, JsonElement? rewardInfo, List<JsonElement> relatedChains)>();
        if (histDoc != null)
        {
            var rewardByQuestId = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            var chainEntries = new List<JsonElement>();

            if (histDoc.RootElement.TryGetProperty("questRewards", out var questRewards) &&
                questRewards.ValueKind == JsonValueKind.Array)
            {
                foreach (var reward in questRewards.EnumerateArray())
                {
                    if (reward.ValueKind != JsonValueKind.Object) continue;
                    var rewardQuestId = GetStr(reward, "questId", "");
                    if (!string.IsNullOrWhiteSpace(rewardQuestId))
                        rewardByQuestId[rewardQuestId] = reward;
                }
            }

            if (histDoc.RootElement.TryGetProperty("questChains", out var questChains) &&
                questChains.ValueKind == JsonValueKind.Array)
            {
                foreach (var chain in questChains.EnumerateArray())
                {
                    if (chain.ValueKind == JsonValueKind.Object)
                        chainEntries.Add(chain);
                }
            }

            if (histDoc.RootElement.TryGetProperty("questHistory", out var questHistory) &&
                questHistory.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in questHistory.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var name = GetStr(item, "questName", GetStr(item, "title", GetStr(item, "name", "?")));
                    var outcome = GetStr(item, "outcome", GetStr(item, "status", "")).ToLowerInvariant();
                    var icon = outcome switch { "completed" or "завершён" or "success" => "✅", "failed" or "провален" => "❌", _ => "📋" };
                    var questId = GetStr(item, "questId", "");
                    rewardByQuestId.TryGetValue(questId, out var rewardInfo);
                    var relatedChains = chainEntries
                        .Where(chain => HistoryChainMatchesQuest(chain, questId, name))
                        .Select(chain => chain.Clone())
                        .ToList();
                    historyQuests.Add(($"{icon} 📋 {name}", item, rewardInfo.ValueKind == JsonValueKind.Object ? rewardInfo : (JsonElement?)null, relatedChains));
                }
            }
            else if (histDoc.RootElement.TryGetProperty("quests", out var legacyHistory) &&
                     legacyHistory.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in legacyHistory.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var name = GetStr(item, "questName", GetStr(item, "title", GetStr(item, "name", "?")));
                    var outcome = GetStr(item, "outcome", GetStr(item, "status", "")).ToLowerInvariant();
                    var icon = outcome switch { "completed" or "завершён" or "success" => "✅", "failed" or "провален" => "❌", _ => "📋" };
                    historyQuests.Add(($"{icon} 📋 {name}", item, null, new List<JsonElement>()));
                }
            }
        }

        if (quests.Count == 0 && historyQuests.Count == 0)
        {
            ShowEmptyPanel(_loc.T("quests"), "Квесты не обнаружены");
            WaitForKey();
            return;
        }

        while (true)
        {
            var choices = new List<string>();
            foreach (var (label, _, isSoul) in quests)
                choices.Add(isSoul ? $"[purple]{Markup.Escape(label)}[/]" : Markup.Escape(label));
            foreach (var (label, _, _, _) in historyQuests)
                choices.Add($"[dim]{Markup.Escape(label)}[/]");
            choices.Add("[dim]← Назад[/]");

            var selected = Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold green]📜 {_loc.T("quests")}[/]")
                    .PageSize(15)
                    .AddChoices(choices));

            if (selected.Contains("← Назад")) break;

            var selIdx = choices.IndexOf(selected);
            if (selIdx < 0) break;

            if (selIdx < quests.Count)
            {
                await ShowQuestDetailPanel(quests[selIdx].el, quests[selIdx].isSoul, false);
            }
            else
            {
                var histIdx = selIdx - quests.Count;
                if (histIdx >= 0 && histIdx < historyQuests.Count)
                    await ShowQuestDetailPanel(historyQuests[histIdx].el, false, true, historyQuests[histIdx].rewardInfo, historyQuests[histIdx].relatedChains);
            }
        }
    }

    private static void RenderReadableJsonValue(List<string> lines, string label, JsonElement value, string indent, HashSet<string>? excluded = null, int depth = 0)
    {
        if (depth > 5)
            return;

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(label))}:[/]");
                foreach (var prop in value.EnumerateObject())
                {
                    if (prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (excluded != null && excluded.Contains(prop.Name))
                        continue;
                    RenderReadableJsonValue(lines, prop.Name, prop.Value, indent + "  ", null, depth + 1);
                }
                break;
            case JsonValueKind.Array:
                lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(label))}:[/]");
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        RenderReadableJsonValue(lines, $"элемент {++index}", item, indent + "  ", null, depth + 1);
                    }
                    else
                    {
                        var itemText = item.ValueKind switch
                        {
                            JsonValueKind.String => item.GetString() ?? "",
                            JsonValueKind.Number => item.ToString(),
                            JsonValueKind.True => "да",
                            JsonValueKind.False => "нет",
                            _ => item.ToString()
                        };

                        if (!string.IsNullOrWhiteSpace(itemText))
                            lines.Add($"{indent}  [dim]• {Markup.Escape(itemText)}[/]");
                    }
                }
                break;
            case JsonValueKind.String:
                var sv = value.GetString() ?? "";
                if (sv.Length > 0)
                    lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(label))}: {Markup.Escape(sv)}[/]");
                break;
            case JsonValueKind.Number:
                lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(label))}: {value}[/]");
                break;
            case JsonValueKind.True:
                lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(label))}: да[/]");
                break;
            case JsonValueKind.False:
                lines.Add($"{indent}[dim]{Markup.Escape(NpcFieldToRussian(label))}: нет[/]");
                break;
        }
    }

    private async Task ShowQuestDetailPanel(JsonElement q, bool isSoul, bool isHistory, JsonElement? historyRewardInfo = null, List<JsonElement>? relatedChains = null)
    {
        var lines = new List<string>();
        var name = GetStr(q, "questName", GetStr(q, "title", "???"));
        var prefix = isSoul ? "[purple]🌟 Квест души[/] — " : "";
        lines.Add($"{prefix}[bold green]{Markup.Escape(name)}[/]");

        var questGiver = GetStr(q, "questGiver", "");
        if (!string.IsNullOrEmpty(questGiver))
            lines.Add($"  👤 Квестодатель: [cyan]{Markup.Escape(questGiver)}[/]");

        var status = GetStr(q, "status", "Active");
        var statusColor = status.ToLower() switch
        {
            "completed" or "завершён" => "green",
            "failed" or "провален" => "red",
            _ => "yellow"
        };
        lines.Add($"  📌 Статус: [{statusColor}]{Markup.Escape(status)}[/]");

        var relatedRivalArcId = GetStr(q, "relatedRivalArcId", "");
        var counterToRivalArc = q.TryGetProperty("counterToRivalArc", out var counterNode) &&
                                counterNode.ValueKind == JsonValueKind.True;
        if (isSoul && !string.IsNullOrWhiteSpace(relatedRivalArcId))
        {
            var rivalArcLabel = counterToRivalArc
                ? "Это контр-квест против чужой линии судьбы."
                : "Этот квест связан с параллельной судьбой другой души.";
            lines.Add($"  [purple]🧵 Чужая нить судьбы[/] [dim]{Markup.Escape(rivalArcLabel)}[/]");
        }

        if (isHistory)
        {
            var outcome = GetStr(q, "outcome", "");
            if (!string.IsNullOrEmpty(outcome))
            {
                var oColor = outcome.ToLower() switch { "completed" or "завершён" => "green", "failed" or "провален" => "red", _ => "grey" };
                lines.Add($"  🏁 Исход: [{oColor}]{Markup.Escape(outcome)}[/]");
            }
            var completionTurn = GetStr(q, "completionTurn", "");
            if (!string.IsNullOrEmpty(completionTurn))
                lines.Add($"  🔢 Ход завершения: [cyan]{Markup.Escape(completionTurn)}[/]");
            var completionDate = GetStr(q, "completionDate", "");
            if (!string.IsNullOrEmpty(completionDate))
                lines.Add($"  🕒 Дата завершения: [cyan]{Markup.Escape(completionDate)}[/]");
            var historyExperience = GetStr(q, "experience", "");
            if (!string.IsNullOrEmpty(historyExperience))
                lines.Add($"  ⭐ Получено опыта: [yellow]{Markup.Escape(historyExperience)}[/]");
            var reputation = GetStr(q, "reputation", "");
            if (!string.IsNullOrEmpty(reputation))
                lines.Add($"  🤝 Репутация: [yellow]{Markup.Escape(reputation)}[/]");
            var incarnationNumber = GetStr(q, "incarnationNumber", "");
            if (!string.IsNullOrEmpty(incarnationNumber))
                lines.Add($"  🔄 Инкарнация: [white]{Markup.Escape(incarnationNumber)}[/]");
        }

        var background = GetStr(q, "questBackground", "");
        if (!string.IsNullOrEmpty(background))
        {
            lines.Add("");
            lines.Add($"  [dim italic]📖 {Markup.Escape(background)}[/]");
        }

        var desc = GetStr(q, "description", "");
        if (!string.IsNullOrEmpty(desc))
        {
            lines.Add("");
            lines.Add($"  {Markup.Escape(desc)}");
        }

        // Objectives
        if (q.TryGetProperty("objectives", out var objectives) && objectives.ValueKind == JsonValueKind.Array && objectives.GetArrayLength() > 0)
        {
            lines.Add("");
            lines.Add("  [bold]🎯 Цели:[/]");
            foreach (var obj in objectives.EnumerateArray())
            {
                var objDesc = GetStr(obj, "description", "???");
                var objStatus = GetStr(obj, "status", "Active").ToLower();
                var objIcon = objStatus switch { "completed" => "✅", "failed" => "❌", _ => "🔄" };
                lines.Add($"    {objIcon} {Markup.Escape(objDesc)}");
            }
        }

        // Rewards
        if (q.TryGetProperty("rewards", out var rewards) && rewards.ValueKind == JsonValueKind.Object)
        {
            lines.Add("");
            lines.Add("  [bold]🎁 Награды:[/]");
            var xp = GetInt(rewards, "experience", 0);
            if (xp > 0) lines.Add($"    ⭐ Опыт: [yellow]{xp}[/]");
            var money = GetInt(rewards, "money", 0);
            if (money > 0) lines.Add($"    💰 Деньги: [yellow]{money}[/]");
            if (rewards.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var itemStr = item.ValueKind == JsonValueKind.String ? item.GetString() ?? "?" : item.GetRawText();
                    lines.Add($"    📦 {Markup.Escape(itemStr)}");
                }
            }
            var other = GetStr(rewards, "other", "");
            if (!string.IsNullOrEmpty(other))
                lines.Add($"    🔹 {Markup.Escape(other)}");
        }

        // Failure consequences
        var failCons = GetStr(q, "failureConsequences", "");
        if (!string.IsNullOrEmpty(failCons))
        {
            lines.Add("");
            lines.Add($"  [bold red]⚠ Последствия провала:[/] {Markup.Escape(failCons)}");
        }

        // Details log
        if (q.TryGetProperty("detailsLog", out var detailsLog) && detailsLog.ValueKind == JsonValueKind.Array)
        {
            var logEntries = new List<string>();
            foreach (var entry in detailsLog.EnumerateArray())
            {
                var entryStr = entry.ValueKind == JsonValueKind.String ? entry.GetString() ?? "" : entry.GetRawText();
                if (!string.IsNullOrEmpty(entryStr))
                    logEntries.Add(entryStr);
            }
            if (logEntries.Count > 0)
            {
                lines.Add("");
                lines.Add($"  [bold]📝 Журнал ({logEntries.Count} записей):[/]");
                foreach (var entry in logEntries)
                    lines.Add($"    [dim]• {Markup.Escape(entry)}[/]");
            }
        }

        if (isHistory && historyRewardInfo.HasValue && historyRewardInfo.Value.ValueKind == JsonValueKind.Object)
        {
            lines.Add("");
            lines.Add("  [bold]🎁 Фактически получено:[/]");

            if (historyRewardInfo.Value.TryGetProperty("itemsReceived", out var itemsReceived) &&
                itemsReceived.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsReceived.EnumerateArray())
                    lines.Add($"    📦 {Markup.Escape(item.ValueKind == JsonValueKind.String ? item.GetString() ?? "?" : item.ToString())}");
            }

            if (historyRewardInfo.Value.TryGetProperty("skillsUnlocked", out var skillsUnlocked) &&
                skillsUnlocked.ValueKind == JsonValueKind.Array)
            {
                foreach (var skill in skillsUnlocked.EnumerateArray())
                    lines.Add($"    ⚔️ Навык: {Markup.Escape(skill.ValueKind == JsonValueKind.String ? skill.GetString() ?? "?" : skill.ToString())}");
            }

            if (historyRewardInfo.Value.TryGetProperty("relationshipChanges", out var relationshipChanges) &&
                relationshipChanges.ValueKind == JsonValueKind.Array)
            {
                foreach (var relation in relationshipChanges.EnumerateArray())
                    lines.Add($"    🤝 Отношение: {Markup.Escape(relation.ValueKind == JsonValueKind.String ? relation.GetString() ?? "?" : relation.ToString())}");
            }
        }

        if (isHistory && relatedChains is { Count: > 0 })
        {
            lines.Add("");
            lines.Add("  [bold]🔗 Связанные цепочки:[/]");
            foreach (var chain in relatedChains)
            {
                var chainId = GetStr(chain, "chainId", "chain");
                var currentQuest = GetStr(chain, "currentQuest", "");
                var progress = GetStr(chain, "progress", "");
                var unlocked = chain.TryGetProperty("unlocked", out var unlockedEl) && unlockedEl.ValueKind == JsonValueKind.True;
                var unlockedLabel = unlocked ? "[green]разблокирована[/]" : "[dim]скрыта[/]";
                var chainLine = $"    🔗 [white]{Markup.Escape(chainId)}[/] — {unlockedLabel}";
                if (!string.IsNullOrEmpty(currentQuest))
                    chainLine += $" [dim](текущий квест: {Markup.Escape(currentQuest)})[/]";
                if (!string.IsNullOrEmpty(progress))
                    chainLine += $" [dim]• {Markup.Escape(progress)}[/]";
                lines.Add(chainLine);
            }
        }

        var borderColor = isSoul ? Color.Purple : Color.Green;
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader($" 📜 {Markup.Escape(name)} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor),
            Padding = new Padding(1, 1),
            Expand = true
        });
        await WaitForKeyWithImage("quest", name, GetStr(q, "image_prompt", ""), GetStr(q, "questId", name));
    }

    private static bool HistoryChainMatchesQuest(JsonElement chain, string questId, string questName)
    {
        var currentQuest = GetStr(chain, "currentQuest", "");
        if (!string.IsNullOrWhiteSpace(questId) &&
            string.Equals(currentQuest, questId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(questName) &&
            string.Equals(currentQuest, questName, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool HasRelatedRivalArc(JsonElement item) =>
        !string.IsNullOrWhiteSpace(GetStr(item, "relatedRivalArcId", ""));

    private static bool IsPlayerVisibleRivalSignal(JsonElement signal) =>
        signal.ValueKind == JsonValueKind.Object &&
        signal.TryGetProperty("visibleToPlayer", out var visibleNode) &&
        visibleNode.ValueKind == JsonValueKind.True;

    private static bool IsPlayerVisibleRivalWorldEvent(JsonElement worldEvent)
    {
        if (worldEvent.ValueKind != JsonValueKind.Object)
            return false;

        var visibility = GetStr(worldEvent, "visibility", "");
        return string.Equals(visibility, "Public", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "Regional", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(visibility, "player_known", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildRivalClueManifestationKey(string arcId, JsonElement source, bool isWorldEvent)
    {
        var revealId = GetStr(source, "bonusClueRevealId", "");
        if (!string.IsNullOrWhiteSpace(revealId))
            return $"{arcId}::reveal::{revealId}";

        if (!isWorldEvent)
        {
            var signalId = GetStr(source, "signalId", "");
            if (!string.IsNullOrWhiteSpace(signalId))
                return $"{arcId}::signal::{signalId}";

            return $"{arcId}::signal::{GetInt(source, "stage", 0)}::{GetStr(source, "source", "")}::{GetStr(source, "description", "")}";
        }

        var eventId = GetStr(source, "eventId", "");
        if (!string.IsNullOrWhiteSpace(eventId))
            return $"{arcId}::world_event::{eventId}";

        return $"{arcId}::world_event::{GetStr(source, "eventTitle", GetStr(source, "title", GetStr(source, "name", "")))}::{GetStr(source, "summary", GetStr(source, "description", ""))}";
    }

    private static IEnumerable<JsonElement> EnumerateRelatedRivalWorldEvents(JsonElement root, string arcId)
    {
        JsonElement events;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("worldEventsLog", out var worldEvents))
            events = worldEvents;
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("events", out var eventArray))
            events = eventArray;
        else if (root.ValueKind == JsonValueKind.Array)
            events = root;
        else
            yield break;

        if (events.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var worldEvent in events.EnumerateArray())
        {
            if (worldEvent.ValueKind != JsonValueKind.Object)
                continue;
            if (!string.Equals(GetStr(worldEvent, "relatedRivalArcId", ""), arcId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!IsPlayerVisibleRivalWorldEvent(worldEvent))
                continue;

            yield return worldEvent;
        }
    }

    private static List<VisibleRivalSoulThread> ReadVisibleRivalSoulThreads(JsonElement root, JsonElement? worldEventsRoot = null)
    {
        JsonElement arcs;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("arcs", out var canonicalArcs))
        {
            arcs = canonicalArcs;
        }
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("UpdateRivalSoulArcs", out var updateArcs))
        {
            arcs = updateArcs;
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            arcs = root;
        }
        else
        {
            return new List<VisibleRivalSoulThread>();
        }

        if (arcs.ValueKind != JsonValueKind.Array)
            return new List<VisibleRivalSoulThread>();

        var result = new List<VisibleRivalSoulThread>();
        foreach (var arc in arcs.EnumerateArray())
        {
            if (arc.ValueKind != JsonValueKind.Object)
                continue;

            var hasRivalSoul = arc.TryGetProperty("rivalSoul", out var rivalSoulNode) && rivalSoulNode.ValueKind == JsonValueKind.Object;
            var rivalName = hasRivalSoul ? GetStr(rivalSoulNode, "displayNameOrMoniker", "") : string.Empty;
            if (string.IsNullOrWhiteSpace(rivalName))
                continue;

            var manifestations = new List<RivalManifestationEntry>();
            var manifestationClueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (arc.TryGetProperty("milestones", out var milestones) && milestones.ValueKind == JsonValueKind.Array)
            {
                foreach (var milestone in milestones.EnumerateArray())
                {
                    if (milestone.ValueKind != JsonValueKind.Object)
                        continue;
                    var visible = milestone.TryGetProperty("visibleToPlayer", out var visibleNode) &&
                                  visibleNode.ValueKind == JsonValueKind.True;
                    if (!visible)
                        continue;

                    manifestations.Add(new RivalManifestationEntry(
                        GetInt(milestone, "stage", 0),
                        "Этап",
                        GetStr(milestone, "title", "Проявление"),
                        GetStr(milestone, "summary", ""),
                        GetChronologyLabel(milestone)));
                }
            }

            if (arc.TryGetProperty("publicSignals", out var signals) && signals.ValueKind == JsonValueKind.Array)
            {
                foreach (var signal in signals.EnumerateArray())
                {
                    if (!IsPlayerVisibleRivalSignal(signal))
                        continue;

                    var clueKey = BuildRivalClueManifestationKey(GetStr(arc, "arcId", ""), signal, isWorldEvent: false);
                    if (!manifestationClueKeys.Add(clueKey))
                        continue;

                    manifestations.Add(new RivalManifestationEntry(
                        GetInt(signal, "stage", 0),
                        "Сигнал",
                        GetStr(signal, "source", "Признак"),
                        GetStr(signal, "description", ""),
                        GetChronologyLabel(signal)));
                }
            }

            if (worldEventsRoot.HasValue)
            {
                var currentArcStage = GetInt(arc, "currentStage", 0);
                var arcId = GetStr(arc, "arcId", "");
                foreach (var worldEvent in EnumerateRelatedRivalWorldEvents(worldEventsRoot.Value, arcId))
                {
                    var clueKey = BuildRivalClueManifestationKey(arcId, worldEvent, isWorldEvent: true);
                    if (!manifestationClueKeys.Add(clueKey))
                        continue;

                    manifestations.Add(new RivalManifestationEntry(
                        GetInt(worldEvent, "stage", currentArcStage),
                        "Новость мира",
                        GetStr(worldEvent, "eventTitle", GetStr(worldEvent, "title", GetStr(worldEvent, "name", "Событие мира"))),
                        GetStr(worldEvent, "summary", GetStr(worldEvent, "description", "")),
                        GetChronologyLabel(worldEvent)));
                }
            }

            var isKnownToPlayer = hasRivalSoul &&
                                  rivalSoulNode.TryGetProperty("isKnownToPlayer", out var knownNode) &&
                                  knownNode.ValueKind == JsonValueKind.True;
            if (!isKnownToPlayer && manifestations.Count == 0)
                continue;

            manifestations = manifestations
                .OrderBy(entry => entry.Stage)
                .ThenBy(entry => entry.Kind)
                .ToList();

            var lastManifestation = manifestations
                .OrderByDescending(entry => entry.Stage)
                .ThenByDescending(entry => entry.Kind.Equals("Сигнал", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .FirstOrDefault();
            var lastManifestationSummary = manifestations.Count > 0
                ? BuildRivalManifestationSummary(lastManifestation!)
                : "Нить ощущается, но явных проявлений пока мало.";
            var currentStage = GetInt(arc, "currentStage", -1);
            var hasFreshSignal = lastManifestation is not null &&
                                 lastManifestation.Kind.Equals("Сигнал", StringComparison.OrdinalIgnoreCase) &&
                                 currentStage >= 0 &&
                                 lastManifestation.Stage >= currentStage;

            var scope = GetStr(arc, "scope", "");
            var status = GetStr(arc, "status", "");
            var scopeLabel = GetRivalArcScopeLabel(scope);
            var statusLabel = GetRivalArcStatusLabel(status);
            var freshMarker = hasFreshSignal ? "[green]🆕[/] " : string.Empty;
            var listLabel = $"{freshMarker}🧵 {GetRivalArcStatusIcon(status)} {Markup.Escape(rivalName)} [dim]({Markup.Escape(scopeLabel)} • {Markup.Escape(statusLabel)})[/] [grey]— {Markup.Escape(lastManifestationSummary)}[/]";

            var sponsorGuardianName = string.Empty;
            if (arc.TryGetProperty("sponsorGuardianRef", out var sponsorRef) && sponsorRef.ValueKind == JsonValueKind.Object)
                sponsorGuardianName = GetStr(sponsorRef, "displayName", GetStr(sponsorRef, "guardianId", GetStr(sponsorRef, "presetId", "")));

            var roleSummary = hasRivalSoul ? GetStr(rivalSoulNode, "roleSummary", "") : string.Empty;
            var objective = GetStr(arc, "objective", "");
            var arcType = GetStr(arc, "arcType", "");
            var stakes = string.Empty;
            var targetsPlayerDirectly = false;
            if (arc.TryGetProperty("playerIntersection", out var intersection) && intersection.ValueKind == JsonValueKind.Object)
            {
                stakes = GetStr(intersection, "stakes", "");
                targetsPlayerDirectly = intersection.TryGetProperty("targetsPlayerDirectly", out var targetsNode) &&
                                        targetsNode.ValueKind == JsonValueKind.True;
            }

            result.Add(new VisibleRivalSoulThread(
                GetStr(arc, "arcId", ""),
                rivalName,
                roleSummary,
                objective,
                scope,
                scopeLabel,
                status,
                statusLabel,
                arcType,
                GetRivalArcTypeLabel(arcType),
                sponsorGuardianName,
                stakes,
                targetsPlayerDirectly,
                hasFreshSignal,
                listLabel,
                lastManifestationSummary,
                manifestations));
        }

        return result
            .OrderByDescending(GetRivalThreadDangerScore)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task ShowRivalSoulThreadDetailPanel(VisibleRivalSoulThread arc)
    {
        var relatedSoulQuests = await ReadRelatedRivalSoulQuestsAsync(arc.ArcId);
        var relatedWorldEvents = await ReadRelatedRivalWorldEventsAsync(arc.ArcId);

        var lines = new List<string> { $"[bold purple]🧵 {Markup.Escape(arc.DisplayName)}[/]" };
        if (!string.IsNullOrWhiteSpace(arc.RoleSummary))
            lines.Add($"[dim]{Markup.Escape(arc.RoleSummary)}[/]");

        lines.Add("");
        lines.Add("[bold]👤 Кто это:[/]");
        lines.Add($"  📚 Тип нити: [cyan]{Markup.Escape(arc.TypeLabel)}[/]");
        lines.Add($"  📏 Масштаб: [white]{Markup.Escape(arc.ScopeLabel)}[/]");
        lines.Add($"  📌 Статус: [{GetRivalArcStatusColor(arc.Status)}]{Markup.Escape(arc.StatusLabel)}[/]");
        if (!string.IsNullOrWhiteSpace(arc.SponsorGuardianName))
            lines.Add($"  👁 Покровительствующий Хранитель: [purple]{Markup.Escape(arc.SponsorGuardianName)}[/]");

        lines.Add("");
        lines.Add("[bold]🧠 Что уже известно:[/]");
        if (!string.IsNullOrWhiteSpace(arc.Objective))
            lines.Add($"  🎯 Предполагаемая цель: [white]{Markup.Escape(arc.Objective)}[/]");
        lines.Add($"  📍 Последнее проявление: [white]{Markup.Escape(arc.LastManifestationSummary)}[/]");
        lines.Add($"  🔎 Видимых проявлений: [white]{arc.Manifestations.Count}[/]");

        lines.Add("");
        lines.Add("[bold]⚖ Что это значит для игрока:[/]");
        if (!string.IsNullOrWhiteSpace(arc.Stakes))
            lines.Add($"  ⚖ Ставки: [white]{Markup.Escape(arc.Stakes)}[/]");
        if (arc.TargetsPlayerDirectly)
            lines.Add("  [red]⚠ Эта нить судьбы направлена прямо против игрока.[/]");
        else
            lines.Add("  [dim]Пока нить не бьёт по игроку напрямую, но способна изменить мир вокруг него.[/]");
        if (arc.HasFreshSignal)
            lines.Add("  [green]🆕 По этой нити только что проявился свежий сигнал текущей стадии.[/]");

        if (relatedSoulQuests.Count > 0 || relatedWorldEvents.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]🔗 Связанные проявления:[/]");
            if (relatedSoulQuests.Count > 0)
            {
                lines.Add("  [bold]🌟 Квесты души:[/]");
                foreach (var quest in relatedSoulQuests)
                {
                    var line = $"    • [white]{Markup.Escape(quest.Title)}[/] [dim]({Markup.Escape(quest.Status)})[/]";
                    if (quest.IsCounterQuest)
                        line += " [purple]— контр-квест[/]";
                    lines.Add(line);
                }
            }

            if (relatedWorldEvents.Count > 0)
            {
                lines.Add("  [bold]📰 Новости мира:[/]");
                foreach (var worldEvent in relatedWorldEvents)
                {
                    var line = $"    • [white]{Markup.Escape(worldEvent.Headline)}[/]";
                    var meta = new List<string>();
                    if (!string.IsNullOrWhiteSpace(worldEvent.TimeLabel))
                        meta.Add(worldEvent.TimeLabel);
                    if (!string.IsNullOrWhiteSpace(worldEvent.Visibility))
                        meta.Add(worldEvent.Visibility);
                    if (!string.IsNullOrWhiteSpace(worldEvent.Location))
                        meta.Add(worldEvent.Location);
                    if (!string.IsNullOrWhiteSpace(worldEvent.Category))
                        meta.Add(worldEvent.Category);
                    if (meta.Count > 0)
                        line += $" [dim]({Markup.Escape(string.Join(" • ", meta))})[/]";
                    lines.Add(line);
                    if (!string.IsNullOrWhiteSpace(worldEvent.Summary))
                        lines.Add($"      [dim]{Markup.Escape(worldEvent.Summary)}[/]");
                }
            }
        }

        var worldChanges = relatedWorldEvents
            .SelectMany(worldEvent => worldEvent.ChangeEffects)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (worldChanges.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]🌍 Что уже изменилось в мире:[/]");
            foreach (var change in worldChanges)
                lines.Add($"  • [white]{Markup.Escape(change)}[/]");
        }

        lines.Add("");
        lines.Add("[bold]📜 Хронология проявлений:[/]");
        if (arc.Manifestations.Count == 0)
        {
            lines.Add("  [dim]Игрок пока ощущает наличие этой нити, но подробные следы ещё не проявились.[/]");
        }
        else
        {
            foreach (var manifestation in arc.Manifestations)
            {
                var headline = manifestation.Kind switch
                {
                    "Сигнал" => $"Сигнал: {manifestation.Title}",
                    "Новость мира" => $"Новость мира: {manifestation.Title}",
                    _ => $"Этап: {manifestation.Title}"
                };
                var line = $"    • [purple]Стадия {manifestation.Stage + 1}[/] — [white]{Markup.Escape(headline)}[/]";
                if (!string.IsNullOrWhiteSpace(manifestation.TimeLabel))
                    line += $" [dim]({Markup.Escape(manifestation.TimeLabel)})[/]";
                if (!string.IsNullOrWhiteSpace(manifestation.Summary))
                    line += $" [dim]• {Markup.Escape(manifestation.Summary)}[/]";
                lines.Add(line);
            }
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader($" 🧵 {Markup.Escape(arc.DisplayName)} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Purple),
            Padding = new Padding(1, 1),
            Expand = true
        });

        WaitForKey();
    }

    private async Task<List<RelatedRivalQuestSummary>> ReadRelatedRivalSoulQuestsAsync(string arcId)
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/quests/soul_quests.json");
        if (doc == null)
            return new List<RelatedRivalQuestSummary>();

        JsonElement quests;
        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("quests", out var questArray))
            quests = questArray;
        else if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("UpdateSoulQuests", out var updateQuestArray))
            quests = updateQuestArray;
        else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            quests = doc.RootElement;
        else
            return new List<RelatedRivalQuestSummary>();

        if (quests.ValueKind != JsonValueKind.Array)
            return new List<RelatedRivalQuestSummary>();

        var result = new List<RelatedRivalQuestSummary>();
        foreach (var quest in quests.EnumerateArray())
        {
            if (quest.ValueKind != JsonValueKind.Object)
                continue;
            if (!string.Equals(GetStr(quest, "relatedRivalArcId", ""), arcId, StringComparison.OrdinalIgnoreCase))
                continue;

            var title = GetStr(quest, "questName", GetStr(quest, "title", GetStr(quest, "name", "")));
            if (string.IsNullOrWhiteSpace(title))
                continue;

            var status = GetStr(quest, "status", "active");
            var isCounterQuest = quest.TryGetProperty("counterToRivalArc", out var counterNode) &&
                                 counterNode.ValueKind == JsonValueKind.True;
            result.Add(new RelatedRivalQuestSummary(title, status, isCounterQuest));
        }

        return result;
    }

    private async Task<List<RelatedRivalWorldEventSummary>> ReadRelatedRivalWorldEventsAsync(string arcId)
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/world/world_events.json");
        if (doc == null)
            return new List<RelatedRivalWorldEventSummary>();

        JsonElement events;
        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("worldEventsLog", out var worldEvents))
            events = worldEvents;
        else if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("events", out var eventsArray))
            events = eventsArray;
        else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            events = doc.RootElement;
        else
            return new List<RelatedRivalWorldEventSummary>();

        if (events.ValueKind != JsonValueKind.Array)
            return new List<RelatedRivalWorldEventSummary>();

        var result = new List<RelatedRivalWorldEventSummary>();
        foreach (var item in events.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            if (!string.Equals(GetStr(item, "relatedRivalArcId", ""), arcId, StringComparison.OrdinalIgnoreCase))
                continue;

            var headline = GetStr(item, "eventTitle", GetStr(item, "title", GetStr(item, "name", "")));
            if (string.IsNullOrWhiteSpace(headline))
                headline = GetStr(item, "summary", GetStr(item, "description", ""));
            if (string.IsNullOrWhiteSpace(headline))
                continue;

            var summary = GetStr(item, "summary", GetStr(item, "description", ""));
            result.Add(new RelatedRivalWorldEventSummary(
                headline,
                summary,
                GetStr(item, "visibility", ""),
                GetStr(item, "category", GetStr(item, "eventCategory", GetStr(item, "type", ""))),
                GetStr(item, "location", GetStr(item, "eventLocation", "")),
                GetChronologyLabel(item),
                ReadWorldEventChangeEffects(item)));
        }

        return result;
    }

    private static string BuildRivalManifestationSummary(RivalManifestationEntry manifestation)
    {
        var headline = manifestation.Kind.Equals("Сигнал", StringComparison.OrdinalIgnoreCase)
            ? manifestation.Title
            : manifestation.Title;
        if (!string.IsNullOrWhiteSpace(manifestation.Summary))
            return manifestation.Summary;

        return $"{manifestation.Kind}: {headline}";
    }

    private static string GetChronologyLabel(JsonElement item)
    {
        var parts = new List<string>();

        var turn = GetStr(item, "turn", GetStr(item, "turnNumber", ""));
        if (!string.IsNullOrWhiteSpace(turn))
            parts.Add($"ход {turn}");

        var time = GetStr(item, "timestamp", GetStr(item, "dateTime", GetStr(item, "date", "")));
        if (!string.IsNullOrWhiteSpace(time))
            parts.Add(time);

        return string.Join(" • ", parts);
    }

    private static List<string> ReadWorldEventChangeEffects(JsonElement item)
    {
        var result = new List<string>();
        foreach (var propName in new[] { "consequences", "outcome", "impact", "followUp", "followUpEvent", "nextStep" })
        {
            if (!item.TryGetProperty(propName, out var value))
                continue;

            AppendJsonValueSummaries(result, value);
        }

        return result
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AppendJsonValueSummaries(List<string> result, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
            {
                var text = value.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                    result.Add(text);
                break;
            }
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                    AppendJsonValueSummaries(result, item);
                break;
            case JsonValueKind.Object:
            {
                var text = GetStr(value, "summary",
                    GetStr(value, "description",
                        GetStr(value, "title",
                            GetStr(value, "name", GetStr(value, "content", "")))));
                if (!string.IsNullOrWhiteSpace(text))
                    result.Add(text);
                break;
            }
        }
    }

    private static string GetRivalArcTypeLabel(string arcType) => arcType.ToLowerInvariant() switch
    {
        "hostile_hunt" => "Враждебная охота",
        "rival_ascension" => "Восхождение соперника",
        "political_claim" => "Претензия на власть",
        "artifact_race" => "Гонка за артефактом",
        "ideological_mission" => "Идеологическая миссия",
        "custom" => "Особая нить",
        _ => string.IsNullOrWhiteSpace(arcType) ? "Неизвестная нить" : arcType
    };

    private static string GetRivalArcScopeLabel(string scope) => scope.ToLowerInvariant() switch
    {
        "major" => "Основная",
        "minor" => "Малая",
        _ => string.IsNullOrWhiteSpace(scope) ? "Неизвестно" : scope
    };

    private static string GetRivalArcStatusLabel(string status) => status.ToLowerInvariant() switch
    {
        "latent" => "Скрытая",
        "rising" => "Нарастающая",
        "intersecting" => "Пересекается с жизнью игрока",
        "resolved" => "Развязана",
        "failed" => "Сорвана",
        _ => string.IsNullOrWhiteSpace(status) ? "Неизвестно" : status
    };

    private static string GetRivalArcStatusColor(string status) => status.ToLowerInvariant() switch
    {
        "latent" => "grey",
        "rising" => "yellow",
        "intersecting" => "red",
        "resolved" => "green",
        "failed" => "grey",
        _ => "white"
    };

    private static string GetRivalArcStatusIcon(string status) => status.ToLowerInvariant() switch
    {
        "latent" => "👁",
        "rising" => "📈",
        "intersecting" => "⚔",
        "resolved" => "✅",
        "failed" => "❌",
        _ => "•"
    };

    private static int GetRivalThreadDangerScore(VisibleRivalSoulThread thread)
    {
        var score = 0;
        if (thread.TargetsPlayerDirectly)
            score += 100;

        score += thread.Status.ToLowerInvariant() switch
        {
            "intersecting" => 50,
            "rising" => 35,
            "latent" => 20,
            "resolved" => 5,
            "failed" => 0,
            _ => 10
        };

        if (thread.Scope.Equals("major", StringComparison.OrdinalIgnoreCase))
            score += 20;
        else if (thread.Scope.Equals("minor", StringComparison.OrdinalIgnoreCase))
            score += 10;

        if (thread.HasFreshSignal)
            score += 5;

        return score;
    }
}

