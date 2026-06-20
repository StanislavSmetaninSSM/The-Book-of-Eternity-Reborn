using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
        var auditLines = lines.ToList();
        var playerLines = BuildChaosSeaPlayerPreviewLines(auditLines);
        var showAudit = false;
        while (true)
        {
            Clear();
            var renderedTitle = showAudit
                ? $"Технический контракт: {previewTitle}"
                : BuildChaosSeaPlayerPreviewTitle(previewTitle);
            var renderedLines = showAudit ? auditLines : playerLines;
            Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", renderedLines)))
            {
                Header = new PanelHeader($" 🌊 {Markup.Escape(renderedTitle)} ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(2, 1),
                Expand = true
            });

            if (showAudit && auditNode != null)
                WriteJsonAuditPanel(auditTitle ?? "Полный JSON контракта Моря Хаоса", auditNode, Color.Cyan1);

            var choices = new List<string> { confirmChoice };
            if (auditNode != null || auditLines.Count != playerLines.Count)
                choices.Add(showAudit ? "← Вернуться к обычному виду" : "🔧 Показать технический контракт");
            choices.Add("← Отмена");

            var choice = Prompt(new SelectionPrompt<string>()
                .Title($"[bold cyan]{Markup.Escape(confirmationTitle)}[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(choices));

            if (choice.Contains("техническ", StringComparison.OrdinalIgnoreCase))
            {
                showAudit = true;
                continue;
            }

            if (choice.Contains("обычному виду", StringComparison.OrdinalIgnoreCase))
            {
                showAudit = false;
                continue;
            }

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
    }

    private static IReadOnlyList<string> BuildChaosSeaPlayerPreviewLines(IReadOnlyList<string> auditLines)
    {
        var result = new List<string>();
        foreach (var sourceLine in auditLines)
        {
            if (IsChaosSeaAuditSectionStart(sourceLine))
                break;

            var line = NormalizeChaosSeaPlayerPreviewLine(sourceLine);
            if (IsChaosSeaTechnicalPreviewLine(line))
                continue;

            result.Add(line);
        }

        while (result.Count > 0 && string.IsNullOrWhiteSpace(result[^1]))
            result.RemoveAt(result.Count - 1);

        return result.Count == 0
            ? new[] { "Действие подготовлено. Перед подтверждением можно открыть технический контракт отдельным пунктом." }
            : result;
    }

    private static string BuildChaosSeaPlayerPreviewTitle(string previewTitle)
    {
        var title = previewTitle
            .Replace("Полный предпросмотр", "Предпросмотр", StringComparison.OrdinalIgnoreCase)
            .Replace("контракта", "действия", StringComparison.OrdinalIgnoreCase)
            .Trim();
        return string.IsNullOrWhiteSpace(title) ? "Предпросмотр действия" : title;
    }

    private static bool IsChaosSeaAuditSectionStart(string line)
    {
        var plain = RemoveSpectreMarkup(line);
        return plain.Contains("Контракт", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("Техническое закрытие", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("Accepted state changes", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("Canonical accepted outcome", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("Предварительное локальное изменение клиента", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("Матрица правил мира", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("Правила локального предпросмотра", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsChaosSeaTechnicalPreviewLine(string line)
    {
        var plain = RemoveSpectreMarkup(line);
        return plain.Contains("requestId", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("guardianId", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("abodeId", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("residentId", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("archiveId", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("projectId", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("offeringType", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("interactionType", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("transferMode", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("selectionMode", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("requestMode", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("discoveredAbodes", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("pending_", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains("Update", StringComparison.Ordinal) ||
               plain.Contains("Receipts", StringComparison.OrdinalIgnoreCase) ||
               plain.Contains(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeChaosSeaPlayerPreviewLine(string line)
    {
        var normalized = line
            .Replace("Guardian/Abode:", "Хранитель/Обитель:", StringComparison.Ordinal)
            .Replace("Source activeGuardian:", "Текущий Хранитель:", StringComparison.Ordinal)
            .Replace("Source abode:", "Текущая Обитель:", StringComparison.Ordinal)
            .Replace("Target abode:", "Новая Обитель:", StringComparison.Ordinal)
            .Replace("Guardian:", "Хранитель:", StringComparison.Ordinal)
            .Replace("Resident:", "Обитатель:", StringComparison.Ordinal)
            .Replace("Archive entry:", "Запись Архива:", StringComparison.Ordinal)
            .Replace("Project:", "Проект:", StringComparison.Ordinal)
            .Replace("Source:", "Откуда:", StringComparison.Ordinal)
            .Replace("Target:", "Куда:", StringComparison.Ordinal)
            .Replace("current devotion/restlessness:", "Текущая преданность/беспокойство:", StringComparison.Ordinal);

        return DimParenthesizedIdentityRegex().Replace(normalized, string.Empty);
    }

    private static string RemoveSpectreMarkup(string line) =>
        SpectreMarkupRegex().Replace(line, string.Empty);

    [GeneratedRegex(@"\s*\[dim\]\([^)]*\)\[/\]", RegexOptions.Compiled)]
    private static partial Regex DimParenthesizedIdentityRegex();

    [GeneratedRegex(@"\[[^\]]+\]", RegexOptions.Compiled)]
    private static partial Regex SpectreMarkupRegex();

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
