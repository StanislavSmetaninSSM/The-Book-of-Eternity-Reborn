using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private bool EvaluateAcceptedReasoningDocumentPrefix(JsonElement root, List<ValidationIssue> issues, out string gmThoughts)
    {
        gmThoughts = string.Empty;
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "output/debug_logs.json должен быть JSON object",
                code: "invalid_debug_logs_json_root",
                section: "gm_thoughts_markdown",
                expected: "JSON object with gm_thoughts_markdown and timestamp",
                actual: root.ValueKind.ToString(),
                repairHint: "Оставь output/debug_logs.json валидным JSON-объектом с полями gm_thoughts_markdown и timestamp."));
            return false;
        }

        gmThoughts = root.TryGetProperty("gm_thoughts_markdown", out var gm) &&
                     gm.ValueKind == JsonValueKind.String
            ? gm.GetString() ?? string.Empty
            : string.Empty;

        return true;
    }

    private void AppendAcceptedReasoningUnknownFields(JsonElement root, List<ValidationIssue> issues)
    {
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(prop.Name, "gm_thoughts_markdown", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(prop.Name, "timestamp", StringComparison.OrdinalIgnoreCase))
                continue;

            issues.Add(new ValidationIssue(
                $"output/debug_logs.json.{prop.Name}",
                IssueSeverity.Error,
                "output/debug_logs.json содержит неподдерживаемое top-level поле",
                code: "debug_logs_unknown_field",
                section: "gm_thoughts_markdown",
                expected: "gm_thoughts_markdown | timestamp",
                actual: prop.Name,
                repairHint: "Пиши в output/debug_logs.json только gm_thoughts_markdown и timestamp для текущего хода."));
        }
    }

    private bool EvaluateAcceptedReasoningScopePrefix(string gmThoughts, List<ValidationIssue> issues,
        out string normalizedThoughts, out ReasoningScopeManifest scope,
        out ReasoningScopeMode scopeMode, out string actorType)
    {
        normalizedThoughts = string.Empty;
        scope = default!;
        scopeMode = default;
        actorType = string.Empty;
        if (string.IsNullOrWhiteSpace(gmThoughts))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "Отсутствует gm_thoughts_markdown при обязательной проверке actor reasoning scope",
                code: "missing_gm_thoughts",
                section: "gm_thoughts_markdown",
                expected: "gm_thoughts_markdown with NPC scope declaration",
                actual: "missing or empty",
                repairHint: "Добавь debug_logs.json.gm_thoughts_markdown с секцией 'Охват NPC-анализа' и reasoning blocks."));
            return false;
        }

        normalizedThoughts = gmThoughts.Replace("\r\n", "\n");
        if (!TryParseReasoningScope(normalizedThoughts, out scope))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "Отсутствует обязательная секция 'Охват NPC-анализа' / 'NPC Scope' в gm_thoughts_markdown",
                code: "missing_npc_scope",
                section: "npc_scope",
                expected: "NPC scope declaration section",
                actual: "missing",
                repairHint: "Добавь секцию '## Охват NPC-анализа' с режимом, релевантными акторами и обоснованием."));
            return false;
        }

        scopeMode = ParseReasoningScopeMode(scope.Mode);
        if (!scope.HasModeField || scopeMode == ReasoningScopeMode.Unknown)
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                !scope.HasModeField || string.IsNullOrWhiteSpace(scope.Mode)
                    ? "В секции охвата NPC-анализа отсутствует поле 'Режим' / 'Mode'"
                    : $"Неизвестный режим NPC scope: '{scope.Mode}'",
                code: !scope.HasModeField || string.IsNullOrWhiteSpace(scope.Mode) ? "missing_scope_mode" : "invalid_scope_mode",
                section: "npc_scope",
                expected: "Mode = Scene-local, World-progression, Guardian-centric or Mixed",
                actual: !scope.HasModeField || string.IsNullOrWhiteSpace(scope.Mode) ? "missing" : scope.Mode,
                repairHint: "Укажи режим: Scene-local, World-progression, Guardian-centric или Mixed."));
        }

        if (!scope.HasRelevantActorsField)
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "В секции охвата NPC-анализа отсутствует поле 'Релевантные акторы' / 'Relevant actors'",
                code: "missing_relevant_actors_field",
                section: "npc_scope",
                expected: "Relevant actors field",
                actual: "missing",
                repairHint: "Явно укажи 'Релевантные акторы: ...' или 'Релевантные акторы: нет' для Scene-local хода."));
        }

        if (!scope.HasWhyRelevantField || string.IsNullOrWhiteSpace(scope.WhyRelevant))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "В секции охвата NPC-анализа отсутствует обоснование релевантных акторов",
                code: "missing_scope_relevance_reason",
                section: "npc_scope",
                expected: "Why relevant field",
                actual: "missing",
                repairHint: "Добавь строку 'Почему они релевантны' с объяснением выбора акторов."));
        }

        if (!scope.HasOutOfScopeActorsField)
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "В секции охвата NPC-анализа отсутствует поле 'Акторы вне охвата' / 'Actors outside scope'",
                code: "missing_out_of_scope_actors_field",
                section: "npc_scope",
                expected: "Actors outside scope field",
                actual: "missing",
                repairHint: "Явно укажи 'Акторы вне охвата: ...' или 'Акторы вне охвата: нет'."));
        }

        if (!scope.HasOutOfScopeReasonField || string.IsNullOrWhiteSpace(scope.OutOfScopeReason))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "В секции охвата NPC-анализа отсутствует обоснование акторов вне охвата",
                code: "missing_scope_out_of_scope_reason",
                section: "npc_scope",
                expected: "Why outside scope field",
                actual: "missing",
                repairHint: "Добавь строку 'Почему они вне охвата' с объяснением, почему остальные акторы не обрабатываются."));
        }

        actorType = scopeMode == ReasoningScopeMode.GuardianCentric ? "Guardian" : "Actor";

        if ((scopeMode == ReasoningScopeMode.WorldProgression ||
             scopeMode == ReasoningScopeMode.GuardianCentric ||
             scopeMode == ReasoningScopeMode.Mixed) &&
            scope.RelevantActors.Count == 0)
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                $"Режим '{scope.Mode}' требует непустой список релевантных акторов",
                code: "empty_relevant_actors_for_mode",
                section: "npc_scope",
                expected: "At least one relevant actor",
                actual: "empty relevant actor list",
                repairHint: "Либо добавь релевантных акторов, либо используй режим Scene-local с явным обоснованием пустого scope."));
        }

        if (scope.RelevantActors.Count > 0 &&
            !ContainsAny(normalizedThoughts.ToLowerInvariant(),
                "## размышления npc", "## 🎭 обязательно: размышления npc", "## npc thoughts",
                "## npc brain", "## actor brain", "## actor brain 2.0", "## reasoning",
                "## размышления акторов", "## мозг акторов", "## guardian thoughts"))
        {
            issues.Add(new ValidationIssue(
                "output/debug_logs.json", IssueSeverity.Error,
                "Для задекларированных акторов отсутствует допустимая reasoning section",
                code: "missing_actor_reasoning_section",
                section: "npc_reasoning",
                expected: "Reasoning section with actor blocks",
                actual: "missing",
                repairHint: "Добавь отдельную reasoning section ('Размышления NPC', 'Размышления акторов', 'Guardian Thoughts' или эквивалентный heading) и подпункты ### [Actor Name] для всех релевантных акторов."));
        }

        return true;
    }
}
