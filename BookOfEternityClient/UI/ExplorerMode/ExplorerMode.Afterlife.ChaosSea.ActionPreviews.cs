using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private bool ConfirmChaosSeaContractPreview(
        string previewTitle,
        IEnumerable<string> lines,
        JsonNode? auditNode = null,
        string? auditTitle = null,
        string confirmationTitle = "Подтвердить контракт Моря Хаоса",
        string confirmChoice = "✅ Подтвердить и продолжить")
    {
        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader($" 🌊 {Markup.Escape(previewTitle)} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        if (auditNode != null)
            WriteJsonAuditPanel(auditTitle ?? "Полный JSON контракта Моря Хаоса", auditNode, Color.Cyan1);

        var choice = Prompt(new SelectionPrompt<string>()
            .Title($"[bold cyan]{Markup.Escape(confirmationTitle)}[/]")
            .HighlightStyle(new Style(Color.Cyan1))
            .AddChoices(confirmChoice, "← Отмена"));

        if (choice.Contains("Отмена", StringComparison.OrdinalIgnoreCase) ||
            choice.Contains("Назад", StringComparison.OrdinalIgnoreCase) ||
            choice.Contains("←", StringComparison.Ordinal))
        {
            return false;
        }

        return choice.Contains("Подтверд", StringComparison.OrdinalIgnoreCase) ||
               choice.Contains("продолж", StringComparison.OrdinalIgnoreCase) ||
               choice.Contains("Создать", StringComparison.OrdinalIgnoreCase) ||
               choice.Contains("Выбрать", StringComparison.OrdinalIgnoreCase) ||
               choice.Contains("Отправить", StringComparison.OrdinalIgnoreCase) ||
               choice.Contains("Да", StringComparison.OrdinalIgnoreCase) ||
               !string.IsNullOrWhiteSpace(choice);
    }

    private static JsonNode? ToChaosSeaAuditNode<T>(T value) =>
        JsonSerializer.SerializeToNode(value, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);

    private static JsonObject BuildChaosSeaDirectActionAudit(string actionTag, string playerAction, params (string Key, object? Value)[] fields)
    {
        var root = new JsonObject
        {
            ["actionTag"] = actionTag,
            ["playerAction"] = playerAction
        };

        foreach (var (key, value) in fields)
        {
            root[key] = value switch
            {
                null => null,
                string text => text,
                bool flag => flag,
                int number => number,
                long number => number,
                double number => number,
                decimal number => number,
                JsonNode node => node.DeepClone(),
                _ => JsonSerializer.SerializeToNode(value, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
            };
        }

        return root;
    }

    private static void AppendChaosSeaCommonContractRules(List<string> lines)
    {
        lines.Add("");
        lines.Add("[bold]Общие правила Моря Хаоса:[/]");
        lines.Add("  • Это afterlife-turn contract; Mortal World location/time/weather/NPC travel systems не используются.");
        lines.Add("  • GM обязан закрывать машинный контракт canonical state/receipt-полями, а не только prose-описанием.");
        lines.Add("  • Все id/requestId/cost/actionTag из этого предпросмотра должны совпасть с turn_request и output файлами.");
        lines.Add("  • Forbidden side effects: currentLocationData, worldEventsLog, Mortal World UpdateNPCs и смена смертного мира без явного bootstrap.");
    }

    private static void AppendChaosSeaLocalPreviewRules(List<string> lines)
    {
        lines.Add("");
        lines.Add("[bold]Правила локального предпросмотра Посмертия:[/]");
        lines.Add("  • Это client-local mutation, not an afterlife-turn contract.");
        lines.Add("  • GM turn не отправляется; GM не пишет receipts, progression_report, gm_thoughts_markdown или output files.");
        lines.Add("  • Pending/control file не создаётся и никакой existing pending contract не закрывается.");
        lines.Add("  • После подтверждения клиент меняет только перечисленные local state surfaces.");
    }

    private static void AppendChaosSeaPendingFileRule(List<string> lines, string path)
    {
        lines.Add($"  • Pending/control файл: [dim]{Markup.Escape(path)}[/]");
        lines.Add("  • До receipt-а этот pending contract блокирует повторное создание такого же живого запроса.");
    }
}
