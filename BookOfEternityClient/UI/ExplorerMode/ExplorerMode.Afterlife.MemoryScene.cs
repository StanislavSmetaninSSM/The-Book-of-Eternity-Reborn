using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private async Task ShowSarefMemorySceneAsync()
    {
        if (!EnsureOrdinaryAfterlifeInteractionAvailable("Воспоминание"))
            return;

        await _stateManager.RefreshGameStateAsync();
        var read = await ReadJsonObjectForAfterlifeStatusResultAsync(SarefMainStoryState.StatePath);
        if (read.Error != null)
        {
            ShowEmptyPanel(
                "Воспоминание",
                $"Состояние скрытой линии повреждено ({read.Error}). Сначала нужен repair состояния.");
            if (!string.IsNullOrWhiteSpace(read.RawPayload))
                WriteJsonAuditPanel("Raw hidden main story state", JsonValue.Create(read.RawPayload), Color.Red);
            WaitForKey();
            return;
        }

        var scene = read.Root?["memoryScene"] as JsonObject;
        if (scene == null)
        {
            ShowEmptyPanel(
                "Воспоминание",
                "Активного Воспоминания нет. Это не Врата Памяти: Воспоминание появляется только как особый слой 4-го квеста Хранителя в линии Сарефа.");
            WaitForKey();
            return;
        }

        var lines = BuildSarefMemoryScenePlayerLines(scene);
        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" Воспоминание ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Gold1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
    }

    private static List<string> BuildSarefMemoryScenePlayerLines(JsonObject scene)
    {
        var title = GetNodeString(scene["title"]) ??
                    GetNodeString(scene["sceneTitle"]) ??
                    GetNodeString(scene["displayName"]) ??
                    GetNodeString(scene["sceneId"]) ??
                    "без названия";
        var status = DescribeSarefMemorySceneStatus(GetNodeString(scene["status"]));
        var role = scene["role"] as JsonObject;
        var roleName = GetNodeString(role?["displayName"]) ??
                       GetNodeString(role?["name"]) ??
                       GetNodeString(role?["roleId"]) ??
                       "не указана";
        var roleSummary = GetNodeString(role?["summary"]);
        var questOrdinal = GetNodeInt(scene["questOrdinal"]);

        var lines = new List<string>
        {
            "[bold gold1]Воспоминание[/]",
            "",
            $"Сцена: [white]{Markup.Escape(title)}[/]",
            $"Состояние: [white]{Markup.Escape(status)}[/]",
            $"Память Хранителя: [white]{Markup.Escape(GetNodeString(scene["guardianId"]) ?? "не указано")}[/]",
            $"Квест: [white]{Markup.Escape(GetNodeString(scene["questId"]) ?? "не указано")}[/]" +
            (questOrdinal > 0 ? $" [dim](ступень {questOrdinal})[/]" : string.Empty),
            $"Роль внутри сцены: [white]{Markup.Escape(roleName)}[/]",
        };

        if (!string.IsNullOrWhiteSpace(roleSummary))
            lines.Add($"  [dim]{Markup.Escape(roleSummary)}[/]");

        lines.Add("");
        lines.Add("[yellow]Это не Врата Памяти.[/]");
        lines.Add("[dim]Смертный инвентарь не переносится: в сцене работают только роль, способности и правила самого Воспоминания.[/]");
        lines.Add("[dim]Исторический факт нельзя напрямую переписать; задача игрока - прожить роль, распознать правду и закрыть узлы сцены.[/]");

        AppendSarefMemorySceneObjects(lines, "Границы сцены", scene["boundaries"] as JsonArray, "boundaryId");
        AppendSarefMemorySceneObjects(lines, "Доступные способности", scene["abilities"] as JsonArray, "abilityId", preferName: true);
        AppendSarefMemorySceneNodes(lines, scene["requiredStoryNodes"] as JsonArray);
        AppendSarefMemorySceneSuccessCondition(lines, scene["successCondition"] as JsonObject);
        AppendSarefMemorySceneClosureTarget(lines, scene["closureTarget"] as JsonObject);

        var resolution = GetNodeString(scene["resolutionSummary"]);
        if (!string.IsNullOrWhiteSpace(resolution))
        {
            lines.Add("");
            lines.Add("[bold]Итог сцены[/]");
            lines.Add($"  • {Markup.Escape(resolution)}");
        }

        return lines;
    }

    private static void AppendSarefMemorySceneObjects(
        List<string> lines,
        string title,
        JsonArray? array,
        string idProperty,
        bool preferName = false)
    {
        lines.Add("");
        lines.Add($"[bold]{Markup.Escape(title)}[/]");
        if (array == null || array.Count == 0)
        {
            lines.Add("  • не указано");
            return;
        }

        foreach (var item in array.OfType<JsonObject>())
        {
            var name = preferName
                ? GetNodeString(item["name"]) ?? GetNodeString(item["displayName"]) ?? GetNodeString(item[idProperty])
                : GetNodeString(item["displayName"]) ?? GetNodeString(item["name"]) ?? GetNodeString(item[idProperty]);
            var summary = GetNodeString(item["summary"]) ?? GetNodeString(item["description"]);
            lines.Add($"  • [white]{Markup.Escape(name ?? "без названия")}[/]");
            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add($"    [dim]{Markup.Escape(summary)}[/]");
        }
    }

    private static void AppendSarefMemorySceneNodes(List<string> lines, JsonArray? nodes)
    {
        lines.Add("");
        lines.Add("[bold]Обязательные сюжетные узлы[/]");
        if (nodes == null || nodes.Count == 0)
        {
            lines.Add("  • не указано");
            return;
        }

        foreach (var node in nodes.OfType<JsonObject>())
        {
            var status = DescribeSarefMemorySceneNodeStatus(GetNodeString(node["status"]));
            var summary = GetNodeString(node["summary"]) ??
                          GetNodeString(node["title"]) ??
                          GetNodeString(node["nodeId"]) ??
                          "без описания";
            lines.Add($"  • {Markup.Escape(status)}: [white]{Markup.Escape(summary)}[/]");
        }
    }

    private static void AppendSarefMemorySceneSuccessCondition(List<string> lines, JsonObject? condition)
    {
        lines.Add("");
        lines.Add("[bold]Условие успеха[/]");
        if (condition == null)
        {
            lines.Add("  • не указано");
            return;
        }

        var summary = GetNodeString(condition["summary"]) ??
                      GetNodeString(condition["conditionId"]) ??
                      "без описания";
        var satisfied = GetNodeBool(condition["satisfied"]) ? "выполнено" : "ещё не выполнено";
        lines.Add($"  • [white]{Markup.Escape(summary)}[/] — {Markup.Escape(satisfied)}");
    }

    private static void AppendSarefMemorySceneClosureTarget(List<string> lines, JsonObject? target)
    {
        if (target == null)
            return;

        lines.Add("");
        lines.Add("[bold]Что закрывает сцена[/]");
        lines.Add($"  • Хранитель: [white]{Markup.Escape(GetNodeString(target["guardianId"]) ?? "не указано")}[/]");
        lines.Add($"  • Квест: [white]{Markup.Escape(GetNodeString(target["questId"]) ?? "не указано")}[/]" +
                  (GetNodeInt(target["questOrdinal"]) > 0 ? $" [dim](ступень {GetNodeInt(target["questOrdinal"])})[/]" : string.Empty));
        var revelation = GetNodeString(target["revelationId"]);
        var advantage = GetNodeString(target["advantageId"]);
        if (!string.IsNullOrWhiteSpace(revelation))
            lines.Add($"  • Фрагмент истины: [white]{Markup.Escape(revelation)}[/]");
        if (!string.IsNullOrWhiteSpace(advantage))
            lines.Add($"  • Преимущество: [white]{Markup.Escape(advantage)}[/]");
    }

    private static string DescribeSarefMemorySceneStatus(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "available" => "доступно",
            "active" => "активно",
            "blocked" => "заблокировано",
            "completed" => "завершено",
            "failed" => "провалено",
            _ => status ?? "не указано"
        };

    private static string DescribeSarefMemorySceneNodeStatus(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "pending" => "ожидает",
            "active" => "активно",
            "completed" => "выполнено",
            "failed" => "провалено",
            _ => status ?? "не указано"
        };
}
