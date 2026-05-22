using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private async Task ShowAfterlifeThreatsAsync()
    {
        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Угрозы посмертия", "Угрозы посмертия доступны только в Море Хаоса и Сияющей Обители.");
            return;
        }

        await _stateManager.RefreshGameStateAsync();
        var read = await ReadJsonObjectForAfterlifeStatusResultAsync(AfterlifeActiveThreatState.StatePath);
        if (read.Error != null)
        {
            ShowEmptyPanel(
                "Угрозы посмертия",
                $"{AfterlifeActiveThreatState.StatePath} повреждён ({read.Error}). Сначала выполните repair состояния.");
            if (!string.IsNullOrWhiteSpace(read.RawPayload))
                WriteJsonAuditPanel($"Raw {AfterlifeActiveThreatState.StatePath}", JsonValue.Create(read.RawPayload), Color.Red);
            return;
        }

        var allThreats = (read.Root?[AfterlifeActiveThreatState.ThreatsProperty] as JsonArray)?
            .OfType<JsonObject>()
            .ToList() ?? new List<JsonObject>();
        var visibleThreats = allThreats
            .Where(IsAfterlifeThreatVisibleToPlayer)
            .OrderByDescending(threat => GetThreatInt(threat["intensity"]))
            .ThenBy(threat => GetThreatString(threat["displayName"]) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (visibleThreats.Count == 0)
        {
            ShowEmptyPanel(
                "Угрозы посмертия",
                allThreats.Count == 0
                    ? "ГМ пока не завёл persistent угрозы посмертия."
                    : "Явных угроз сейчас не видно. Скрытые угрозы не раскрываются в обычном интерфейсе игрока.");
            return;
        }

        var lines = new List<string>
        {
            "[bold red]Угрозы посмертия[/]",
            "",
            "Это видимые persistent угрозы Моря Хаоса и Сияющей Обители: охотники за душами, духовные бури, тайные ячейки, заговоры и другие силы, которые могут развиваться вне кадра.",
            ""
        };

        foreach (var threat in visibleThreats)
        {
            AppendAfterlifeThreat(lines, threat);
            lines.Add("");
        }

        var hiddenCount = allThreats.Count - visibleThreats.Count;
        if (hiddenCount > 0)
            lines.Add($"[dim]Скрытых угроз не показано: {hiddenCount}. Их детали раскрываются только после игровых свидетельств или через GM/repair audit.[/]");

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Угрозы посмертия ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Red1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        WaitForKey();
    }

    private static void AppendAfterlifeThreat(List<string> lines, JsonObject threat)
    {
        var displayName = GetThreatString(threat["displayName"]) ?? "Без названия";
        var threatId = GetThreatString(threat["threatId"]) ?? "?";
        var realm = GetThreatString(threat["realm"]) ?? "?";
        var scopeId = GetThreatString(threat["scopeId"]) ?? "?";
        var intensity = GetThreatInt(threat["intensity"]);
        var archetype = threat["threatArchetype"] as JsonObject;
        var impact = threat["impactProfile"] as JsonObject;
        var activity = threat["currentActivity"] as JsonObject;

        lines.Add($"[bold white]{Markup.Escape(displayName)}[/] [dim]({Markup.Escape(threatId)})[/]");
        lines.Add($"  • Область: [white]{Markup.Escape(DescribeAfterlifeThreatRealm(realm))}[/]; зона: [dim]{Markup.Escape(scopeId)}[/]");
        lines.Add($"  • Напряжённость угрозы: [red]{intensity}[/]");

        var motivation = GetThreatString(archetype?["motivation"]);
        var method = GetThreatString(archetype?["method"]);
        if (!string.IsNullOrWhiteSpace(motivation) || !string.IsNullOrWhiteSpace(method))
            lines.Add($"  • Архетип: {Markup.Escape(DescribeAfterlifeThreatMotivation(motivation))}; метод — {Markup.Escape(DescribeAfterlifeThreatMethod(method))}");

        if (activity != null)
        {
            var activityName = GetThreatString(activity["activityName"]) ??
                               GetThreatString(activity["summary"]) ??
                               GetThreatString(activity["activityId"]) ??
                               "активность не описана";
            var description = GetThreatString(activity["description"]);
            lines.Add($"  • Сейчас делает: [white]{Markup.Escape(activityName)}[/]");
            if (!string.IsNullOrWhiteSpace(description))
                lines.Add($"    [dim]{Markup.Escape(description)}[/]");
        }
        else
        {
            lines.Add("  • Сейчас делает: [dim]угроза временно не активна[/]");
        }

        if (impact != null)
        {
            var targetName = GetThreatString(impact["primaryTargetName"]) ??
                             GetThreatString(impact["primaryTargetId"]) ??
                             "цель не указана";
            var primaryImpact = GetThreatString(impact["primaryImpact"]);
            var baseImpact = GetThreatInt(impact["baseImpactValue"]);
            lines.Add($"  • Давление на цель: [white]{Markup.Escape(targetName)}[/]; тип — {Markup.Escape(DescribeAfterlifeThreatImpact(primaryImpact))}; сила {baseImpact}");
        }

        var linkedGuardianId = GetThreatString(threat["linkedGuardianId"]);
        if (!string.IsNullOrWhiteSpace(linkedGuardianId))
            lines.Add($"  • Связанный Хранитель: [dim]{Markup.Escape(linkedGuardianId)}[/]");
    }

    private static bool IsAfterlifeThreatVisibleToPlayer(JsonObject threat) =>
        threat["visibleToPlayer"] is JsonValue visibleValue &&
        visibleValue.TryGetValue<bool>(out var visible) &&
        visible;

    private static string DescribeAfterlifeThreatRealm(string? realm) =>
        realm?.Trim().ToLowerInvariant() switch
        {
            "chaos sea" or "море хаоса" => "Море Хаоса",
            "shining abode" or "сияющая обитель" => "Сияющая Обитель",
            _ => realm ?? "неизвестно"
        };

    private static string DescribeAfterlifeThreatMotivation(string? motivation) =>
        motivation?.Trim().ToLowerInvariant() switch
        {
            "domination" => "стремится к подчинению",
            "consumption" => "пожирает или истощает",
            "preservation" => "сохраняет опасный порядок",
            "corruption" => "искажает и заражает",
            "accumulation" => "копит силу или ресурсы",
            "execution" => "исполняет приговор или приказ",
            "custom" => "особая мотивация",
            _ => "мотивация не указана"
        };

    private static string DescribeAfterlifeThreatMethod(string? method) =>
        method?.Trim().ToLowerInvariant() switch
        {
            "overt" => "открытое давление",
            "covert" => "скрытые действия",
            "deceptive" => "обман",
            "opportunistic" => "использование слабостей",
            "systemic" => "системное влияние",
            "custom" => "особый метод",
            _ => "метод не указан"
        };

    private static string DescribeAfterlifeThreatImpact(string? impact) =>
        impact?.Trim().ToLowerInvariant() switch
        {
            "military" => "военный",
            "economic" => "экономический",
            "social" => "социальный",
            "covert" => "скрытый",
            "stability" => "устойчивость",
            "environment" => "среда",
            "combat" => "бой",
            "politics" => "политика",
            "relationship" => "отношения",
            "progression" => "прогрессия",
            _ => "не указан"
        };

    private static string? GetThreatString(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var result))
            return string.IsNullOrWhiteSpace(result) ? null : result.Trim();

        return null;
    }

    private static int GetThreatInt(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var result))
            return result;

        return 0;
    }
}
