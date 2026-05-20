using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private async Task ShowSarefStoryAsync()
    {
        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
        {
            ShowEmptyPanel("Крылья над Бездной", "Эта скрытая линия отслеживается в посмертии. В смертной жизни продолжай играть обычным текстом.");
            return;
        }

        await _stateManager.RefreshGameStateAsync();
        var read = await ReadJsonObjectForAfterlifeStatusResultAsync(SarefMainStoryState.StatePath);
        if (read.Error != null)
        {
            ShowEmptyPanel(
                "Крылья над Бездной",
                $"{SarefMainStoryState.StatePath} повреждён ({read.Error}). Сначала нужен repair состояния.");
            if (!string.IsNullOrWhiteSpace(read.RawPayload))
                WriteJsonAuditPanel($"Raw {SarefMainStoryState.StatePath}", JsonValue.Create(read.RawPayload), Color.Red);
            return;
        }

        var root = read.Root;
        if (root == null || IsSarefStoryStillUnknown(root))
        {
            ShowEmptyPanel("Крылья над Бездной", "Ты пока не знаешь, что искать.");
            return;
        }

        var lines = new List<string>
        {
            "[bold gold1]Крылья над Бездной[/]",
            "",
            $"Стадия раскрытия: [white]{Markup.Escape(DescribeSarefRevealStage(GetNodeString(root["revealStage"])))}[/]",
            $"Фрагментов: [white]{CountArray(root["sarefRevelations"])}[/]; преимуществ: [white]{CountArray(root["sarefAdvantages"])}[/]",
            ""
        };

        AppendSarefRevelations(lines, root["sarefRevelations"] as JsonArray);
        AppendSarefAdvantages(lines, root["sarefAdvantages"] as JsonArray);
        AppendSarefAdvantageUses(lines, root["sarefAdvantageUses"] as JsonArray);

        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Крылья над Бездной ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        WriteJsonAuditPanel($"Полный JSON {SarefMainStoryState.StatePath}", root, Color.Gold1);
        WaitForKey();
    }

    private static bool IsSarefStoryStillUnknown(JsonObject root)
    {
        var revealStage = GetNodeString(root["revealStage"]);
        var hasContent = CountArray(root["sarefRevelations"]) > 0 ||
                         CountArray(root["sarefAdvantages"]) > 0 ||
                         CountArray(root["sarefAdvantageUses"]) > 0;
        return !hasContent &&
               (string.IsNullOrWhiteSpace(revealStage) ||
                string.Equals(revealStage, SarefMainStoryState.RevealStageUnknown, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(revealStage, SarefMainStoryState.RevealStageShadow, StringComparison.OrdinalIgnoreCase));
    }

    private static void AppendSarefRevelations(List<string> lines, JsonArray? revelations)
    {
        if (revelations == null || revelations.Count == 0)
        {
            lines.Add("[dim]Раскрытые фрагменты пока не записаны.[/]");
            lines.Add("");
            return;
        }

        lines.Add("[bold]Раскрытые фрагменты[/]");
        foreach (var revelation in revelations.OfType<JsonObject>())
        {
            var id = GetNodeString(revelation["revelationId"]) ?? "?";
            var category = GetNodeString(revelation["category"]) ?? "?";
            var summary = GetNodeString(revelation["summary"]);
            lines.Add($"  • [white]{Markup.Escape(id)}[/] — {Markup.Escape(DescribeSarefRevelationCategory(category))}");
            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add($"    [dim]{Markup.Escape(summary)}[/]");
        }
        lines.Add("");
    }

    private static void AppendSarefAdvantages(List<string> lines, JsonArray? advantages)
    {
        if (advantages == null || advantages.Count == 0)
        {
            lines.Add("[dim]Преимуществ против Сарефа пока нет.[/]");
            lines.Add("");
            return;
        }

        lines.Add("[bold]Преимущества против Сарефа[/]");
        foreach (var advantage in advantages.OfType<JsonObject>())
        {
            var id = GetNodeString(advantage["advantageId"]) ?? "?";
            var name = GetNodeString(advantage["displayName"]) ?? GetNodeString(advantage["name"]) ?? id;
            var state = GetNodeString(advantage["state"]) ?? "?";
            var scenes = ReadSarefUiStringArray(advantage["applicableScenes"] as JsonArray).ToList();
            var summary = GetNodeString(advantage["summary"]);

            lines.Add($"  • [white]{Markup.Escape(name)}[/] [dim]({Markup.Escape(id)})[/] — {FormatSarefAdvantageState(state)}");
            if (scenes.Count > 0)
                lines.Add($"    Сцены: [dim]{Markup.Escape(string.Join(", ", scenes))}[/]");
            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add($"    [dim]{Markup.Escape(summary)}[/]");

            if (advantage["spentAudit"] is JsonObject spentAudit)
            {
                var usageId = GetNodeString(spentAudit["usageId"]) ?? "?";
                var turn = GetNodeInt(spentAudit["usedAtTurn"]);
                var sceneType = GetNodeString(spentAudit["sceneType"]) ?? "?";
                lines.Add($"    Потрачено: [dim]{Markup.Escape(usageId)}; ход {turn}; сцена {Markup.Escape(sceneType)}[/]");
            }

            var suppressionReason = GetNodeString(advantage["suppressionReason"]);
            if (!string.IsNullOrWhiteSpace(suppressionReason))
                lines.Add($"    [red]Причина подавления: {Markup.Escape(suppressionReason)}[/]");
        }
        lines.Add("");
    }

    private static void AppendSarefAdvantageUses(List<string> lines, JsonArray? uses)
    {
        if (uses == null || uses.Count == 0)
            return;

        lines.Add("[bold]Журнал использования преимуществ[/]");
        foreach (var use in uses.OfType<JsonObject>()
                     .OrderByDescending(use => GetNodeInt(use["usedAtTurn"])))
        {
            var usageId = GetNodeString(use["usageId"]) ?? "?";
            var advantageId = GetNodeString(use["advantageId"]) ?? "?";
            var sceneType = GetNodeString(use["sceneType"]) ?? "?";
            var summary = GetNodeString(use["summary"]);
            lines.Add($"  • [white]{Markup.Escape(usageId)}[/]: {Markup.Escape(advantageId)} — {Markup.Escape(sceneType)}; ход {GetNodeInt(use["usedAtTurn"])}");
            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add($"    [dim]{Markup.Escape(summary)}[/]");
        }
    }

    private static IEnumerable<string> ReadSarefUiStringArray(JsonArray? array)
    {
        if (array == null)
            yield break;

        foreach (var item in array)
        {
            var value = GetNodeString(item);
            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }
    }

    private static int CountArray(JsonNode? node) =>
        node is JsonArray array ? array.Count : 0;

    private static string DescribeSarefRevealStage(string? stage) =>
        stage?.Trim().ToLowerInvariant() switch
        {
            "unknown" => "ты пока не знаешь, что искать",
            "shadow" => "тень без имени",
            "name_revealed" => "имя Сарефа раскрыто",
            "wings_revealed" => "Крылья Ангелов раскрыты",
            "infiltration_active" => "идёт внедрение в Крылья Ангелов",
            "confrontation_available" => "доступна развязка с Сарефом",
            "completed" => "сюжетная линия завершена",
            _ => stage ?? "неизвестно"
        };

    private static string DescribeSarefRevelationCategory(string? category) =>
        category?.Trim().ToLowerInvariant() switch
        {
            "identity" => "личность Сарефа",
            "method" => "метод стирания памяти и изгнания",
            "faction" => "Крылья Ангелов",
            "path" => "путь к Крыльям Ангелов",
            "oath_break" => "разрыв клятвы",
            "war_doctrine" => "военная доктрина",
            "structural_weakness" => "слабость структуры",
            "exile_survival" => "выживание после изгнания",
            "false_light_cut" => "разрез ложного света",
            _ => category ?? "неизвестно"
        };

    private static string FormatSarefAdvantageState(string? state) =>
        state?.Trim().ToLowerInvariant() switch
        {
            "available" => "[green]Доступно[/]",
            "spent" => "[yellow]Потрачено[/]",
            "passive" => "[cyan]Пассивно[/]",
            "disabled" => "[dim]Отключено[/]",
            "suppressed" => "[red]Подавлено[/]",
            _ => Markup.Escape(state ?? "неизвестно")
        };
}
