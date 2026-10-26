using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private sealed record ReasoningPrefixEvaluation(List<ValidationIssue> Issues, bool Terminal,
        string? NormalizedThoughts, ReasoningScopeManifest? Scope, ReasoningScopeMode ScopeMode, string ActorType);

    private async Task<ReasoningPrefixEvaluation> EvaluateAcceptedReasoningPrefixAsync(DebugPrefixSourceSession? source)
    {
        using var evaluationTiming = _fs.MeasureOperation("ReasoningPrefix.Evaluate");
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnTransientOutputFreshnessAsync(
            "output/debug_logs.json",
            "gm_thoughts_markdown",
            "accepted_turn_stale_debug_logs",
            "output/debug_logs.json must be freshly rewritten for the current accepted turn",
            "Не переиспользуй старый output/debug_logs.json. Перезапиши debug_logs.json заново для текущего accepted turn вместе с актуальным gm_thoughts_markdown.",
            issues, source);
        var debugJson = source is null ? await _fs.ReadFileAsync("output/debug_logs.json") : await source.ReadDebugForParseAsync();
        if (string.IsNullOrWhiteSpace(debugJson))
        {
            source?.Policy("output/debug_logs.json", "document-kind", "MissingOrBlank");
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "Отсутствует gm_thoughts_markdown при обязательной проверке actor reasoning scope",
                code: "missing_gm_thoughts",
                section: "gm_thoughts_markdown",
                expected: "gm_thoughts_markdown with NPC scope declaration",
                actual: "missing or empty",
                repairHint: "Добавь debug_logs.json.gm_thoughts_markdown с секцией 'Охват NPC-анализа' и reasoning blocks."));
            return new(issues, true, null, null, default, string.Empty);
        }

        string gmThoughts;
        try
        {
            using var doc = JsonDocument.Parse(debugJson);
            source?.Policy("output/debug_logs.json", "document-kind", doc.RootElement.ValueKind.ToString());
            if (!EvaluateAcceptedReasoningDocumentPrefix(doc.RootElement, issues, out gmThoughts))
                return new(issues, true, null, null, default, string.Empty);

            var timestampIssueStart = issues.Count;
            source?.BeginTimestampPolicy(doc.RootElement);
            ValidateRequiredIsoTimestampField(
                doc.RootElement,
                "output/debug_logs.json",
                issues,
                "timestamp",
                "gm_thoughts_markdown",
                "debug_logs_missing_timestamp",
                "debug_logs_invalid_timestamp",
                "Добавь в output/debug_logs.json поле timestamp в ISO 8601 формате вместе с актуальным gm_thoughts_markdown.");

            source?.EndTimestampPolicy(issues.Skip(timestampIssueStart));
            AppendAcceptedReasoningUnknownFields(doc.RootElement, issues);
        }
        catch (JsonException ex)
        {
            source?.Policy("output/debug_logs.json", "domain-catch", ex.GetType().Name);
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                $"output/debug_logs.json должен быть валидным JSON: {ex.Message}",
                code: "invalid_debug_logs_json",
                section: "gm_thoughts_markdown",
                expected: "valid JSON object with gm_thoughts_markdown and timestamp",
                actual: "invalid JSON",
                repairHint: "Исправь output/debug_logs.json и оставь в нём валидный JSON-объект с полями gm_thoughts_markdown и timestamp."));
            return new(issues, true, null, null, default, string.Empty);
        }

        if (!EvaluateAcceptedReasoningScopePrefix(gmThoughts, issues, out var normalizedThoughts,
                out var scope, out var scopeMode, out var actorType))
            return new(issues, true, null, null, default, string.Empty);

        source?.Policy("output/debug_logs.json", "scope-result", JsonSerializer.Serialize(scope));
        return new(issues, false, normalizedThoughts, scope, scopeMode, actorType);
    }
}
