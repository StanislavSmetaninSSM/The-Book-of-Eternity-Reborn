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
        lines.Add("[bold]Матрица правил мира Моря Хаоса:[/]");
        lines.Add("  • Жизненный цикл: это обычный контракт загробного хода; ГМ не запускает смертный мир, TriggerLifeEnd или смену currentRealm без явного /incarnate или передачи в Сияющую Обитель.");
        lines.Add("  • Исключение принудительного воплощения Хранителем: враждебный активный Хранитель может записать TriggerIncarnation.source=guardian_forced только в обычный ход Моря Хаоса, с явным доказательством провокации, репутацией <= -21 и без активного/блокирующего afterlife_return_guard.json.");
        lines.Add("  • Авторитет состояния: ГМ закрывает машинный контракт каноническими state/receipt-полями, а не только художественным описанием.");
        lines.Add("  • Авторитет снимка: проверка принятого хода сверяется с копиями game_state/control/pending_turn_snapshot; после создания запроса нельзя опираться на уже изменённые живые pending-файлы.");
        lines.Add("  • Блокировка идентичности: все id/requestId/cost/actionTag/source/target из этого предпросмотра должны совпасть с turn_request и output файлами.");
        lines.Add("  • Локация/время/погода смертного мира: запрещены currentLocationData, world_time timeChange/currentWeather/worldEventsLog и смертное путешествие.");
        lines.Add("  • НПС/фракции смертного мира: запрещены UpdateNPCs, фракции смертного мира, материализация спутников и создание локальных встреч, если нет отдельного MortalWorldProfile-only bootstrap-контракта.");
        lines.Add("  • Инвентарь/деньги/опыт/навыки смертного мира: запрещены money, mortal inventory, equipment, XP, level, skill, wounds/combat/status mutations; загробье использует soul_state, guardians, residents, archive и receipt-поверхности.");
        lines.Add("  • Бой смертного мира: нельзя писать combat enemies/allies/round state; конфликт в Море Хаоса должен быть представлен через состояние Хранителей/резидентов/социальных сцен/проектов, а не через файлы смертного боя.");
        lines.Add("  • Планировщик: циклы мира/прогресса могут идти только через progressionControl + progression_report.json; ГМ не должен придумывать несвязанные дельты живого мира.");
        lines.Add("  • Режим ремонта: если pending/control file повреждён, нужно остановиться и запросить ремонт, а не молча выбрасывать контракт.");
    }

    private static void AppendChaosSeaLocalPreviewRules(List<string> lines)
    {
        lines.Add("");
        lines.Add("[bold]Правила локального предпросмотра Посмертия:[/]");
        lines.Add("  • Это локальное изменение клиента, а не контракт загробного хода.");
        lines.Add("  • Ход ГМ не отправляется; ГМ не пишет receipts, progression_report, gm_thoughts_markdown или output files.");
        lines.Add("  • Pending/control file не создаётся и никакой существующий pending contract не закрывается.");
        lines.Add("  • После подтверждения клиент меняет только перечисленные локальные поверхности состояния.");
    }

    private static void AppendChaosSeaPendingFileRule(List<string> lines, string path)
    {
        lines.Add($"  • Pending/control файл: [dim]{Markup.Escape(path)}[/]");
        lines.Add("  • До receipt-а этот pending contract блокирует повторное создание такого же живого запроса.");
    }
}
