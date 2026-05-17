using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private async Task ShowAfterlifeEntityProfilesAsync()
    {
        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Профили сущностей посмертия", "Профили сущностей посмертия доступны только в Море Хаоса и Сияющей Обители.");
            return;
        }

        await _stateManager.RefreshGameStateAsync();
        var read = await ReadJsonObjectForAfterlifeStatusResultAsync(AfterlifeEntityProfileState.StatePath);
        if (read.Error != null)
        {
            ShowEmptyPanel(
                "Профили сущностей посмертия",
                $"{AfterlifeEntityProfileState.StatePath} повреждён ({read.Error}). Сначала выполните repair состояния.");
            if (!string.IsNullOrWhiteSpace(read.RawPayload))
                WriteJsonAuditPanel($"Raw {AfterlifeEntityProfileState.StatePath}", JsonValue.Create(read.RawPayload), Color.Red);
            return;
        }

        var profiles = read.Root?[AfterlifeEntityProfileState.ProfilesProperty] as JsonArray;
        if (profiles == null || profiles.Count == 0)
        {
            ShowEmptyPanel(
                "Профили сущностей посмертия",
                "Профили сущностей посмертия пока не созданы. ГМ создаёт их через afterlifeEntityProfileUpdates для значимых Хранителей, резидентов, глав фракций и особых акторов.");
            return;
        }

        var lines = new List<string>
        {
            "[bold cyan]Профили сущностей посмертия[/]",
            "",
            "Это полные игровые профили значимых сущностей загробья: ресурсы, прогрессия, духовные искусства, особые искусства, стратегия прокачки и опасность развеивания души.",
            ""
        };

        foreach (var profile in profiles.OfType<JsonObject>()
                     .OrderBy(profile => AfterlifeEntityProfileState.GetNodeString(profile["displayName"]) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            AppendAfterlifeEntityProfile(lines, profile);
            lines.Add("");
        }

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Профили сущностей посмертия ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(_stateManager.CurrentState.IsInShiningAbode ? Color.Gold1 : Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        if (read.Root != null)
            WriteJsonAuditPanel($"Полный JSON {AfterlifeEntityProfileState.StatePath}", read.Root, Color.Cyan1);

        WaitForKey();
    }

    private void AppendAfterlifeEntityProfile(List<string> lines, JsonObject profile)
    {
        var displayName = AfterlifeEntityProfileState.GetNodeString(profile["displayName"]) ?? "Без имени";
        var actorType = AfterlifeEntityProfileState.GetNodeString(profile["actorType"]) ?? "?";
        var actorId = AfterlifeEntityProfileState.GetNodeString(profile["actorId"]) ??
                      AfterlifeEntityProfileState.GetNodeString(profile["actorRef"]) ??
                      "?";
        var realm = AfterlifeEntityProfileState.GetNodeString(profile["realm"]) ?? "?";
        var location = AfterlifeEntityProfileState.GetNodeString(profile["location"]) ??
                       AfterlifeEntityProfileState.GetNodeString(profile["abodeName"]) ??
                       "место не указано";

        lines.Add($"[bold white]{Markup.Escape(displayName)}[/] [dim]({Markup.Escape(actorId)})[/]");
        lines.Add($"  • Тип: [white]{Markup.Escape(DescribeAfterlifeEntityActorType(actorType))}[/]");
        lines.Add($"  • Область: [white]{Markup.Escape(DescribeAfterlifeEntityRealm(realm))}[/]; место: [dim]{Markup.Escape(location)}[/]");

        var currencies = profile["currencies"] as JsonObject;
        lines.Add($"  • Чернильные Перья: [gold1]{AfterlifeEntityProfileState.GetNodeInt(currencies?["inkFeathers"])}[/]; Искры Света: [gold1]{AfterlifeEntityProfileState.GetNodeInt(currencies?["lightSparks"])}[/]");

        var progression = profile["progression"] as JsonObject;
        var enlightenment = progression?["enlightenment"] as JsonObject;
        var radiance = progression?["radiance"] as JsonObject;
        lines.Add($"  • Просветление: тир {AfterlifeEntityProfileState.GetNodeInt(enlightenment?["tier"])}, опыт {AfterlifeEntityProfileState.GetNodeInt(enlightenment?["experience"])}");
        lines.Add($"  • Сияние: тир {AfterlifeEntityProfileState.GetNodeInt(radiance?["tier"])}, опыт {AfterlifeEntityProfileState.GetNodeInt(radiance?["experience"])}");

        AppendAfterlifeEntityStandardArts(lines, profile["standardArts"] as JsonObject);
        AppendAfterlifeEntitySpecialArts(lines, profile["specialArts"] as JsonArray);
        AppendAfterlifeEntityCustomStates(lines, profile[AfterlifeEntityProfileState.CustomStatesProperty] as JsonArray);

        var dissipationTier = AfterlifeEntityProfileState.GetNodeInt(profile["soulDissipationTier"]);
        var stabilityCoefficient = AfterlifeEntityProfileState.ResolveSoulStabilityCoefficient(profile);
        lines.Add($"  • Развеивание души: тир {dissipationTier}; устойчивость души цели: коэффициент {stabilityCoefficient}");

        var warnings = ReadProfileStringArray(profile["warnings"] as JsonArray).ToList();
        if (dissipationTier > 0)
            warnings.Insert(0, $"ОПАСНО: эта сущность потенциально может окончательно развеять душу после победы, если её tier Развеивания выше коэффициента устойчивости цели и её мотивы это допускают. Решение не автоматическое.");
        foreach (var warning in warnings.Distinct(StringComparer.OrdinalIgnoreCase))
            lines.Add($"  • [red]{Markup.Escape(warning)}[/]");

        var strategy = profile["progressionStrategy"] as JsonObject;
        var strategySummary = AfterlifeEntityProfileState.GetNodeString(strategy?["summary"]);
        if (!string.IsNullOrWhiteSpace(strategySummary))
            lines.Add($"  • Стратегия прокачки: [dim]{Markup.Escape(strategySummary)}[/]");

        var priorities = ReadProfileStringArray(strategy?["priorityOrder"] as JsonArray).ToList();
        if (priorities.Count > 0)
            lines.Add($"  • Приоритеты: [dim]{Markup.Escape(string.Join(" → ", priorities))}[/]");

        var lastCycleKey = AfterlifeEntityProfileState.GetNodeString(strategy?["lastAutoProgressionCycleKey"]);
        var latestProgression = ReadLatestProgressionLedgerEntry(profile[AfterlifeEntityProfileState.ProgressionLedgerProperty] as JsonArray);
        if (latestProgression != null)
        {
            var cycleKey = AfterlifeEntityProfileState.GetNodeString(latestProgression["cycleKey"]) ?? lastCycleKey ?? "?";
            var summary = AfterlifeEntityProfileState.GetNodeString(latestProgression["summary"]) ?? "детали не указаны";
            lines.Add($"  • Последняя автопрокачка: [white]{Markup.Escape(cycleKey)}[/] — [dim]{Markup.Escape(summary)}[/]");
        }
        else if (!string.IsNullOrWhiteSpace(lastCycleKey))
        {
            lines.Add($"  • Последняя автопрокачка: [white]{Markup.Escape(lastCycleKey)}[/]");
        }
    }

    private static void AppendAfterlifeEntityStandardArts(List<string> lines, JsonObject? arts)
    {
        if (arts == null || arts.Count == 0)
        {
            lines.Add("  • Духовные искусства: [dim]не указаны[/]");
            return;
        }

        lines.Add("  • Духовные искусства:");
        foreach (var art in arts.OrderBy(item => DescribeAfterlifeEntityArt(item.Key), StringComparer.OrdinalIgnoreCase))
            lines.Add($"    - {Markup.Escape(DescribeAfterlifeEntityArt(art.Key))}: {AfterlifeEntityProfileState.GetNodeInt(art.Value)}");
    }

    private static void AppendAfterlifeEntitySpecialArts(List<string> lines, JsonArray? arts)
    {
        if (arts == null || arts.Count == 0)
            return;

        lines.Add("  • Особые духовные искусства:");
        foreach (var art in arts.OfType<JsonObject>())
        {
            var name = AfterlifeEntityProfileState.GetNodeString(art["displayName"]) ??
                       AfterlifeEntityProfileState.GetNodeString(art["artId"]) ??
                       "Без названия";
            var baseOperation = AfterlifeEntityProfileState.GetNodeString(art["baseOperation"]) ?? "?";
            var tier = AfterlifeEntityProfileState.GetNodeInt(art["tier"]);
            var effect = AfterlifeEntityProfileState.GetNodeString(art["effectSummary"]);
            var costMultiplier = AfterlifeEntityProfileState.GetNodeInt(art["costMultiplierPercent"]);
            var canTeach = art["canTeachPlayer"] is JsonValue canTeachValue &&
                           canTeachValue.TryGetValue<bool>(out var teachValue) &&
                           teachValue;
            lines.Add($"    - {Markup.Escape(name)}: тир {tier}, основа — {Markup.Escape(DescribeAfterlifeEntityArt(baseOperation))}");
            if (costMultiplier > 0)
                lines.Add($"      [dim]Стоимость применения: {costMultiplier}% от базовой стоимости действия.[/]");
            if (canTeach)
                lines.Add("      [green]может обучать игрока[/]");
            var trainingConditions = ReadProfileStringArray(art["trainingConditions"] as JsonArray).ToList();
            if (trainingConditions.Count > 0)
                lines.Add($"      [dim]Условия обучения: {Markup.Escape(string.Join("; ", trainingConditions))}[/]");
            if (!string.IsNullOrWhiteSpace(effect))
                lines.Add($"      [dim]{Markup.Escape(effect)}[/]");
        }
    }

    private static void AppendAfterlifeEntityCustomStates(List<string> lines, JsonArray? states)
    {
        if (states == null || states.Count == 0)
            return;

        lines.Add("  • Кастомные состояния:");
        foreach (var state in states.OfType<JsonObject>())
        {
            var name = AfterlifeEntityProfileState.GetNodeString(state["stateName"]) ??
                       AfterlifeEntityProfileState.GetNodeString(state["name"]) ??
                       AfterlifeEntityProfileState.GetNodeString(state["title"]) ??
                       AfterlifeEntityProfileState.GetNodeString(state["stateId"]) ??
                       "Без названия";
            var current = AfterlifeEntityProfileState.GetNodeString(state["currentValue"]) ??
                          AfterlifeEntityProfileState.GetNodeInt(state["currentValue"]).ToString();
            var max = AfterlifeEntityProfileState.GetNodeString(state["maxValue"]) ??
                      AfterlifeEntityProfileState.GetNodeInt(state["maxValue"]).ToString();
            var description = AfterlifeEntityProfileState.GetNodeString(state["description"]) ??
                              AfterlifeEntityProfileState.GetNodeString(state["summary"]);

            lines.Add($"    - {Markup.Escape(name)}: {Markup.Escape(current)}/{Markup.Escape(max)}");
            if (!string.IsNullOrWhiteSpace(description))
                lines.Add($"      [dim]{Markup.Escape(description)}[/]");
        }
    }

    private static IEnumerable<string> ReadProfileStringArray(JsonArray? array)
    {
        if (array == null)
            yield break;

        foreach (var item in array)
        {
            var value = AfterlifeEntityProfileState.GetNodeString(item);
            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }
    }

    private static JsonObject? ReadLatestProgressionLedgerEntry(JsonArray? ledger)
    {
        if (ledger == null || ledger.Count == 0)
            return null;

        return ledger.OfType<JsonObject>().LastOrDefault();
    }

    private static string DescribeAfterlifeEntityActorType(string? actorType) =>
        actorType?.Trim().ToLowerInvariant() switch
        {
            "player_soul" => "Душа игрока",
            "guardian" => "Хранитель",
            "resident" => "Резидент",
            "shining_faction_head" => "Глава фракции",
            "radiant_actor" => "Сияющий актор",
            "custom_afterlife_actor" => "Особая сущность",
            _ => actorType ?? "?"
        };

    private static string DescribeAfterlifeEntityRealm(string? realm) =>
        realm?.Trim().ToLowerInvariant() switch
        {
            "chaos sea" => "Море Хаоса",
            "море хаоса" => "Море Хаоса",
            "shining abode" => "Сияющая Обитель",
            "сияющая обитель" => "Сияющая Обитель",
            _ => realm ?? "?"
        };

    private static string DescribeAfterlifeEntityArt(string? artId) =>
        artId?.Trim().ToLowerInvariant() switch
        {
            "pressure" => "Давление",
            "counter" => "Контрприём",
            "guard" => "Защита",
            "maneuver" => "Манёвр",
            "binding" => "Оковы",
            "force_binding" => "Силовые оковы",
            "break_binding" => "Разрыв оков",
            "incarnation_resistance" => "Сопротивление воплощению",
            "champion_coordination" => "Координация чемпиона",
            "recover_spiritual_power" => "Собрать Средоточие",
            _ => artId ?? "?"
        };
}
