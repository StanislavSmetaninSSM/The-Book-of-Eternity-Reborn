using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static void ValidateConflictDangerDeclaration(
        JsonObject conflict, string context, List<ValidationIssue> issues)
    {
        if (SpiritualConflictDangerPolicy.ReadDeclaration(conflict) is not null)
            return;
        AddInvalidConflictDangerDeclaration(
            context, conflict["dangerMode"]?.ToJsonString() ?? "missing/null", issues);
    }

    private static void ValidateStartDangerDeclaration(
        JsonElement update, string context, List<ValidationIssue> issues)
    {
        foreach (var property in new[] { "conflictState", "activeConflict", "conflictSeed" })
        {
            if (!TryGetObject(update, property, out var seed))
                continue;
            if (SpiritualConflictDangerPolicy.SeverityCap(TryGetString(seed, "dangerMode")) < 0)
            {
                AddInvalidConflictDangerDeclaration(
                    context + "." + property,
                    seed.TryGetProperty("dangerMode", out var value) ? value.GetRawText() : "missing",
                    issues);
            }
            return;
        }
    }

    private static void AddInvalidConflictDangerDeclaration(
        string context, string actual, List<ValidationIssue> issues) =>
        issues.Add(new ValidationIssue(
            context + ".dangerMode",
            IssueSeverity.Error,
            "Опасность духовного конфликта должна быть объявлена точным значением dangerMode.",
            code: "afterlife_conflict_danger_mode_invalid",
            section: "AfterlifeSpiritualConflict",
            expected: "training/controlled/hostile/annihilation; exact lowercase JSON string",
            actual: actual,
            repairHint: "Укажи dangerMode в seed нового конфликта. Не выводи режим из броска и не меняй объявленную опасность существующего боя."));

    private static void ValidateDangerModePreTurnIntegrity(
        JsonObject? preTurnRoot, JsonObject? currentRoot, List<ValidationIssue> issues)
    {
        if (preTurnRoot is null || currentRoot is null)
            return;

        var declarations = new Dictionary<string, List<string?>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (conflict, _) in EnumerateDangerConflicts(preTurnRoot))
        {
            var id = TryReadConflictId(conflict);
            if (string.IsNullOrWhiteSpace(id))
                continue;
            if (!declarations.TryGetValue(id, out var modes))
            {
                modes = new List<string?>();
                declarations.Add(id, modes);
            }
            modes.Add(SpiritualConflictDangerPolicy.ReadDeclaration(conflict));
        }

        foreach (var (conflict, context) in EnumerateDangerConflicts(currentRoot))
        {
            var id = TryReadConflictId(conflict);
            if (string.IsNullOrWhiteSpace(id) || !declarations.TryGetValue(id, out var modes))
                continue;

            var expected = modes[0];
            if (expected is null || modes.Any(mode => !string.Equals(mode, expected, StringComparison.Ordinal)))
            {
                issues.Add(new ValidationIssue(
                    context + ".dangerMode",
                    IssueSeverity.Error,
                    "Pre-turn записи одного духовного конфликта не доказывают единую допустимую опасность.",
                    code: "afterlife_conflict_danger_mode_invalid_pre_turn_authority",
                    section: "AfterlifeSpiritualConflict",
                    expected: "one valid exact dangerMode across every accepted occurrence of this conflictId",
                    actual: "missing, invalid or conflicting pre-turn declaration",
                    repairHint: "Восстанови корректное принятое состояние до хода; текущий ответ не может задать опасность задним числом."));
                continue;
            }

            var current = SpiritualConflictDangerPolicy.ReadDeclaration(conflict);
            if (current is null || string.Equals(current, expected, StringComparison.Ordinal))
                continue;

            issues.Add(new ValidationIssue(
                context + ".dangerMode",
                IssueSeverity.Error,
                "Опасность принятого духовного конфликта изменена без отдельного подтверждённого перехода.",
                code: "afterlife_conflict_danger_mode_changed_without_authority",
                section: "AfterlifeSpiritualConflict",
                expected: expected,
                actual: current,
                repairHint: "Сохрани pre-turn dangerMode. Обычный exchange, замена activeConflict, resolve и repair_cancel не разрешают эскалацию."));
        }
    }

    private static IEnumerable<(JsonObject Conflict, string Context)> EnumerateDangerConflicts(JsonObject root)
    {
        if (root["activeConflict"] is JsonObject active)
            yield return (active, AfterlifeSpiritualConflictState.StatePath + ".activeConflict");
        if (root["recentConflicts"] is not JsonArray recent)
            yield break;
        for (var index = 0; index < recent.Count; index++)
        {
            if (recent[index] is JsonObject proof)
                yield return (proof, AfterlifeSpiritualConflictState.StatePath + $".recentConflicts[{index}]");
        }
    }
}
