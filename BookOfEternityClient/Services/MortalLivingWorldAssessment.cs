using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Detached clock/source DATA supplied by the owning caller; this record is not authentication or publication authority.</summary>
internal sealed record MortalLivingWorldAssessmentContext(string SessionId, string RequestId, int Turn,
    long OriginalMinute, long ResultMinute, long LastWorldMinute, long LastFactionMinute,
    int WorldCycleMinutes, int FactionCycleMinutes);

/// <summary>Admission-only assessment shape and timing checks; canonical effects and source ownership require separate runtime validation.</summary>
internal static class MortalLivingWorldAssessment
{
    /// <summary>Checks a detached report against already acquired clock/source DATA without reading, writing or granting authority.</summary>
    /// <param name="context">The caller's original/result clock and source identity.</param>
    /// <param name="report">The candidate private report, or null when absent.</param>
    /// <returns>Bounded admission issues, not canonical effect or publication acceptance.</returns>
    internal static IReadOnlyList<ValidationIssue> Validate(MortalLivingWorldAssessmentContext context, JsonObject? report)
    {
        if (!ExactIdentity(context.SessionId) || !ExactIdentity(context.RequestId) || context.Turn < 0 ||
            context.OriginalMinute < 0 || context.ResultMinute < context.OriginalMinute ||
            context.LastWorldMinute < 0 || context.LastWorldMinute > context.OriginalMinute ||
            context.LastFactionMinute < 0 || context.LastFactionMinute > context.OriginalMinute ||
            context.WorldCycleMinutes <= 0 || context.FactionCycleMinutes <= 0)
            return Invalid("context_invalid", "$", "Некорректный исходный контекст времени или хода.",
                "Проверьте исходный контекст хода; не исправляйте его выдуманными событиями.");

        // Subtraction is safe after the ordering/nonnegative checks; adding cycles could overflow.
        var worldDue = context.ResultMinute - context.LastWorldMinute >= context.WorldCycleMinutes;
        var factionDue = context.ResultMinute - context.LastFactionMinute >= context.FactionCycleMinutes;
        var from = context.OriginalMinute;
        if (worldDue) from = Math.Min(from, context.LastWorldMinute);
        if (factionDue) from = Math.Min(from, context.LastFactionMinute);
        if (report is null)
            return worldDue || factionDue
                ? Invalid("report_required", "$", "Для истекшего мирового цикла требуется оценка живого мира.",
                    "Предоставьте полный отчёт за необработанный интервал; обоснованный noSignificantChange допустим.")
                : Array.Empty<ValidationIssue>();

        try
        {
            return ValidateReport(context, report, from);
        }
        catch (ArgumentException)
        {
            // JsonObject can defer detecting duplicate parsed properties until materialization.
            return Invalid("report_invalid", "$", "Отчёт содержит повторяющиеся свойства JSON.",
                "Удалите повторяющиеся свойства в отчёте, сохранив единственное точное значение каждого поля.");
        }
    }

    private static IReadOnlyList<ValidationIssue> ValidateReport(
        MortalLivingWorldAssessmentContext context, JsonObject report, long from)
    {
        if (report.Count != 9 || report.Any(pair => pair.Key is not
                ("schemaVersion" or "sessionId" or "requestId" or "turn" or "fromMinute" or "toMinute" or "resolution" or "audit" or "events")) ||
            !Integer(report["schemaVersion"], out var version) || version != 1)
            return Invalid("report_invalid", "$", "Требуется закрытая структура отчёта версии 1.",
                "Предоставьте ровно schemaVersion, sessionId, requestId, turn, fromMinute, toMinute, resolution, audit и events.");

        if (!Text(report["sessionId"], out var session) || !ExactIdentity(session) || session != context.SessionId ||
            !Text(report["requestId"], out var request) || !ExactIdentity(request) || request != context.RequestId ||
            !Integer(report["turn"], out var turn) || turn != context.Turn)
            return Invalid("source_invalid", "$.sessionId/requestId/turn", "Отчёт не соответствует текущему ходу.",
                "Используйте точные sessionId, requestId и turn текущего запроса без изменения регистра или пробелов.");

        if (!Integer(report["fromMinute"], out var actualFrom) || actualFrom != from ||
            !Integer(report["toMinute"], out var actualTo) || actualTo != context.ResultMinute)
            return Invalid("window_invalid", "$.fromMinute/toMinute", "Интервал отчёта не совпадает с необработанным временем.",
                $"Укажите fromMinute={from} и toMinute={context.ResultMinute}, включая ожидающие мировые циклы.");

        if (!Text(report["resolution"], out var resolution) || resolution is not ("noSignificantChange" or "changes"))
            return Invalid("report_invalid", "$.resolution", "Неизвестный результат оценки живого мира.",
                "Укажите noSignificantChange либо changes; не создавайте событие только для заполнения отчёта.");

        if (report["audit"] is not JsonArray audit || audit.Count != 5)
            return AuditInvalid("$.audit");
        var seen = 0;
        for (var index = 0; index < audit.Count; index++)
        {
            if (audit[index] is not JsonObject row || row.Count != 2 ||
                row.Any(pair => pair.Key is not ("scope" or "reason")) ||
                !Text(row["scope"], out var scope) || !Text(row["reason"], out var reason) || string.IsNullOrWhiteSpace(reason))
                return AuditInvalid($"$.audit[{index}]");
            var bit = scope switch { "npcs" => 1, "factions" => 2, "locations" => 4, "projects" => 8, "world" => 16, _ => 0 };
            if (bit == 0 || (seen & bit) != 0) return AuditInvalid($"$.audit[{index}].scope");
            seen |= bit;
        }

        if (report["events"] is not JsonArray events ||
            (resolution == "noSignificantChange" ? events.Count != 0 : events.Count == 0 || events.Any(row => row is not JsonObject)))
            return Invalid("events_invalid", "$.events", "Список событий не соответствует результату оценки.",
                "Для noSignificantChange используйте пустой events; для changes предоставьте непустой массив объектов событий.");

        // Object rows are admitted, not validated effects: the separate event contract owns their semantics.
        return Array.Empty<ValidationIssue>();
    }

    private static bool ExactIdentity(string? value) =>
        !string.IsNullOrWhiteSpace(value) && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool Text(JsonNode? node, out string? value)
    {
        value = null;
        return node is JsonValue scalar && scalar.TryGetValue(out value);
    }

    private static bool Integer(JsonNode? node, out long value)
    {
        value = 0;
        if (node is not JsonValue scalar) return false;
        if (scalar.TryGetValue(out value)) return true;
        // Programmatically constructed JsonValue<int> does not widen through TryGetValue<long>.
        if (!scalar.TryGetValue<int>(out var integer)) return false;
        value = integer;
        return true;
    }

    private static IReadOnlyList<ValidationIssue> AuditInvalid(string section) =>
        Invalid("audit_invalid", section, "Оценка должна охватывать все пять областей ровно один раз.",
            "Предоставьте npcs, factions, locations, projects и world; каждая строка содержит только scope и непустое reason.");

    private static IReadOnlyList<ValidationIssue> Invalid(string code, string section, string message, string repair) =>
        new[] { new ValidationIssue("game_state/control/mortal_living_world_report.json", IssueSeverity.Error,
            message, code: "mortal_living_world_" + code, section: section, repairHint: repair) };
}
