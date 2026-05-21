using System.Globalization;
using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static readonly HashSet<string> AllowedMathRequestApplicationStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "requested_only"
    };

    private static readonly HashSet<string> AllowedMathAuditApplicationStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "calculated_only",
        "applied_to_state",
        "mismatch_repair_blocking"
    };

    private static readonly HashSet<string> MathAssistantAppliedDeltaReferenceFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "currentHealthChange",
        "currentPoiseChange",
        "currentEnergyChange"
    };

    private sealed class MathContractRequestSnapshot
    {
        public string RequestId { get; init; } = "";
        public string Purpose { get; init; } = "";
        public string Expression { get; init; } = "";
        public Dictionary<string, decimal> Variables { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public MathAssistantRoundingMode RoundingMode { get; init; }
        public int? DecimalPlaces { get; init; }
    }

    private void ValidateMathAssistantContractRoot(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return;

        var requestsById = ValidateMathAssistantRequests(root, contextPrefix, issues);
        ValidateMathAssistantAudit(root, contextPrefix, requestsById, issues);
        ValidateMathAssistantAppliedResponseDeltas(root, contextPrefix, issues);
    }

    private Dictionary<string, MathContractRequestSnapshot> ValidateMathAssistantRequests(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues)
    {
        var requestsById = new Dictionary<string, MathContractRequestSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(MathAssistantContractState.RequestsProperty, out var requestsNode))
            return requestsById;

        var context = $"{contextPrefix}.{MathAssistantContractState.RequestsProperty}";
        if (requestsNode.ValueKind != JsonValueKind.Array)
        {
            AddMathIssue(issues, context, "math_requests_invalid_shape",
                "mathRequests должен быть массивом объектов расчёта.",
                "JSON array", requestsNode.ValueKind.ToString(),
                "Запиши mathRequests как массив объектов с requestId, purpose, expression, variables и rounding.");
            return requestsById;
        }

        var index = 0;
        foreach (var request in requestsNode.EnumerateArray())
        {
            var itemContext = $"{context}[{index}]";
            index++;

            if (request.ValueKind != JsonValueKind.Object)
            {
                AddMathIssue(issues, itemContext, "math_request_invalid_shape",
                    "Элемент mathRequests должен быть JSON object.",
                    "JSON object", request.ValueKind.ToString(),
                    "Перепиши элемент mathRequests как объект расчёта.");
                continue;
            }

            var requestId = ReadRequiredMathString(request, itemContext, "requestId", "math_request_missing_request_id", issues);
            var purpose = ReadRequiredMathString(request, itemContext, "purpose", "math_request_missing_purpose", issues);
            var expression = ReadRequiredMathString(request, itemContext, "expression", "math_request_missing_expression", issues);
            var variables = ReadMathVariables(request, itemContext, "variables", required: true, issues);
            var rounding = ReadMathRounding(request, itemContext, required: false, issues);

            if (!string.IsNullOrWhiteSpace(requestId))
            {
                if (requestsById.ContainsKey(requestId))
                {
                    AddMathIssue(issues, $"{itemContext}.requestId", "math_request_duplicate_id",
                        "mathRequests содержит повторяющийся requestId.",
                        "unique requestId", requestId,
                        "Сделай requestId уникальным, чтобы аудит можно было однозначно связать с запросом.");
                }
                else
                {
                    requestsById[requestId] = new MathContractRequestSnapshot
                    {
                        RequestId = requestId,
                        Purpose = purpose ?? "",
                        Expression = expression ?? "",
                        Variables = variables ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase),
                        RoundingMode = rounding.Mode,
                        DecimalPlaces = rounding.DecimalPlaces
                    };
                }
            }

            if (request.TryGetProperty("applicationState", out var requestStateNode) &&
                (!TryReadMathString(requestStateNode, out var requestState) ||
                 string.IsNullOrWhiteSpace(requestState) ||
                 !AllowedMathRequestApplicationStates.Contains(requestState)))
            {
                AddMathIssue(issues, $"{itemContext}.applicationState", "math_request_invalid_application_state",
                    "mathRequests может быть только запросом на расчёт, а не подтверждением изменения состояния.",
                    "requested_only", requestStateNode.GetRawText(),
                    "Для уже выполненного расчёта используй mathAudit.applicationState.");
            }

            if (string.IsNullOrWhiteSpace(expression) || variables == null || rounding.Invalid)
                continue;

            var evaluation = EvaluateMathContract(expression, variables, rounding.Mode, rounding.DecimalPlaces);
            if (!evaluation.Success)
            {
                AddMathIssue(issues, $"{itemContext}.expression", "math_request_evaluation_failed",
                    "Формула mathRequests не может быть вычислена локальным Математиком.",
                    "valid deterministic expression", evaluation.ErrorCode ?? "unknown_error",
                    evaluation.ErrorMessage ?? "Проверь имена переменных, числа, скобки, деление на ноль и поддержанные функции.");
                continue;
            }

            if (request.TryGetProperty("expectedResult", out var expectedResultNode))
            {
                if (!TryReadMathDecimal(expectedResultNode, out var expectedResult))
                {
                    AddMathIssue(issues, $"{itemContext}.expectedResult", "math_request_expected_result_invalid",
                        "expectedResult должен быть числом.",
                        "number", expectedResultNode.ValueKind.ToString(),
                        "Укажи expectedResult как JSON number или убери поле.");
                }
                else if (evaluation.Result != expectedResult)
                {
                    AddMathIssue(issues, $"{itemContext}.expectedResult", "math_request_expected_result_mismatch",
                        "expectedResult не совпадает с локальным результатом Математика.",
                        FormatMathDecimal(evaluation.Result), FormatMathDecimal(expectedResult),
                        "Исправь expectedResult или пометь расхождение через mathAudit с applicationState=mismatch_repair_blocking.");
                }
            }
        }

        return requestsById;
    }

    private void ValidateMathAssistantAudit(
        JsonElement root,
        string contextPrefix,
        Dictionary<string, MathContractRequestSnapshot> requestsById,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(MathAssistantContractState.AuditProperty, out var auditNode))
            return;

        var context = $"{contextPrefix}.{MathAssistantContractState.AuditProperty}";
        if (auditNode.ValueKind != JsonValueKind.Array)
        {
            AddMathIssue(issues, context, "math_audit_invalid_shape",
                "mathAudit должен быть массивом объектов аудита.",
                "JSON array", auditNode.ValueKind.ToString(),
                "Запиши mathAudit как массив объектов с auditId, requestId, expression, variables, result и formulaVersion.");
            return;
        }

        var auditIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var audit in auditNode.EnumerateArray())
        {
            var itemContext = $"{context}[{index}]";
            index++;

            if (audit.ValueKind != JsonValueKind.Object)
            {
                AddMathIssue(issues, itemContext, "math_audit_item_invalid_shape",
                    "Элемент mathAudit должен быть JSON object.",
                    "JSON object", audit.ValueKind.ToString(),
                    "Перепиши элемент mathAudit как объект аудита расчёта.");
                continue;
            }

            var auditId = ReadRequiredMathString(audit, itemContext, "auditId", "math_audit_missing_audit_id", issues);
            var requestId = ReadRequiredMathString(audit, itemContext, "requestId", "math_audit_missing_request_id", issues);
            var purpose = ReadRequiredMathString(audit, itemContext, "purpose", "math_audit_missing_purpose", issues);
            var expression = ReadRequiredMathString(audit, itemContext, "expression", "math_audit_missing_expression", issues);
            var normalizedExpression = ReadRequiredMathString(audit, itemContext, "normalizedExpression", "math_audit_missing_normalized_expression", issues);
            var variables = ReadMathVariables(audit, itemContext, "variables", required: true, issues);
            var rounding = ReadMathRounding(audit, itemContext, required: true, issues);
            var formulaVersion = ReadRequiredMathString(audit, itemContext, "formulaVersion", "math_audit_missing_formula_version", issues);
            var applicationState = ReadRequiredMathString(audit, itemContext, "applicationState", "math_audit_missing_application_state", issues);

            if (!string.IsNullOrWhiteSpace(auditId) && !auditIds.Add(auditId))
            {
                AddMathIssue(issues, $"{itemContext}.auditId", "math_audit_duplicate_id",
                    "mathAudit содержит повторяющийся auditId.",
                    "unique auditId", auditId,
                    "Сделай auditId уникальным.");
            }

            if (!string.Equals(formulaVersion, MathAssistantContractState.FormulaVersion, StringComparison.OrdinalIgnoreCase))
            {
                AddMathIssue(issues, $"{itemContext}.formulaVersion", "math_audit_invalid_formula_version",
                    "mathAudit использует неподдержанную версию формулы.",
                    MathAssistantContractState.FormulaVersion, formulaVersion ?? "missing",
                    "Сейчас поддержан только локальный контракт math_assistant_v1.");
            }

            if (string.IsNullOrWhiteSpace(applicationState) ||
                !AllowedMathAuditApplicationStates.Contains(applicationState))
            {
                AddMathIssue(issues, $"{itemContext}.applicationState", "math_audit_invalid_application_state",
                    "mathAudit.applicationState должен явно отделять расчёт от применения к состоянию.",
                    string.Join(", ", AllowedMathAuditApplicationStates.OrderBy(x => x)), applicationState ?? "missing",
                    "Используй calculated_only, applied_to_state или mismatch_repair_blocking.");
            }

            ValidateMathStringArray(audit, itemContext, "referencedBy", "math_audit_invalid_reference", issues);
            ValidateMathStringArray(audit, itemContext, "warnings", "math_audit_invalid_warnings", issues, allowEmptyStrings: true);

            if (requestsById.TryGetValue(requestId ?? "", out var requestSnapshot))
            {
                ValidateMathAuditMatchesRequest(audit, itemContext, requestSnapshot, expression, purpose, variables, rounding, issues);
            }

            if (!audit.TryGetProperty("result", out var resultNode))
            {
                AddMathIssue(issues, $"{itemContext}.result", "math_audit_missing_result",
                    "mathAudit должен фиксировать итоговый result.",
                    "number", "missing",
                    "Добавь result, рассчитанный локальным Математиком.");
            }
            else if (!TryReadMathDecimal(resultNode, out var actualResult))
            {
                AddMathIssue(issues, $"{itemContext}.result", "math_audit_result_invalid",
                    "mathAudit.result должен быть числом.",
                    "number", resultNode.ValueKind.ToString(),
                    "Запиши result как JSON number.");
            }

            if (string.IsNullOrWhiteSpace(expression) || variables == null || rounding.Invalid)
                continue;

            var evaluation = EvaluateMathContract(expression, variables, rounding.Mode, rounding.DecimalPlaces);
            if (!evaluation.Success)
            {
                AddMathIssue(issues, $"{itemContext}.expression", "math_audit_evaluation_failed",
                    "Формула mathAudit не может быть вычислена локальным Математиком.",
                    "valid deterministic expression", evaluation.ErrorCode ?? "unknown_error",
                    evaluation.ErrorMessage ?? "Проверь переменные, числа, скобки, деление на ноль и поддержанные функции.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(normalizedExpression) &&
                !string.Equals(normalizedExpression, evaluation.NormalizedExpression, StringComparison.Ordinal))
            {
                AddMathIssue(issues, $"{itemContext}.normalizedExpression", "math_audit_normalized_expression_mismatch",
                    "normalizedExpression не совпадает с нормализованной формулой локального Математика.",
                    evaluation.NormalizedExpression, normalizedExpression,
                    "Перезапиши normalizedExpression из результата локального Математика.");
            }

            if (audit.TryGetProperty("rawResult", out var rawResultNode))
            {
                if (!TryReadMathDecimal(rawResultNode, out var rawResult))
                {
                    AddMathIssue(issues, $"{itemContext}.rawResult", "math_audit_raw_result_invalid",
                        "mathAudit.rawResult должен быть числом.",
                        "number", rawResultNode.ValueKind.ToString(),
                        "Запиши rawResult как JSON number или убери поле.");
                }
                else if (evaluation.RawResult != rawResult)
                {
                    AddMathIssue(issues, $"{itemContext}.rawResult", "math_audit_raw_result_mismatch",
                        "mathAudit.rawResult не совпадает с локальным неокруглённым результатом.",
                        FormatMathDecimal(evaluation.RawResult), FormatMathDecimal(rawResult),
                        "Исправь rawResult по локальному результату Математика.");
                }
            }

            if (audit.TryGetProperty("result", out var checkedResultNode) &&
                TryReadMathDecimal(checkedResultNode, out var checkedResult) &&
                evaluation.Result != checkedResult)
            {
                AddMathIssue(issues, $"{itemContext}.result", "math_audit_result_mismatch",
                    "mathAudit.result не совпадает с локальным результатом Математика.",
                    FormatMathDecimal(evaluation.Result), FormatMathDecimal(checkedResult),
                    "Исправь result или оставь applicationState=mismatch_repair_blocking, чтобы ремонт явно заблокировал принятие хода.");
            }
        }
    }

    private static void ValidateMathAssistantAppliedResponseDeltas(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues)
    {
        if (!string.Equals(contextPrefix, "response", StringComparison.OrdinalIgnoreCase) ||
            !root.TryGetProperty(MathAssistantContractState.AuditProperty, out var auditNode) ||
            auditNode.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var context = $"{contextPrefix}.{MathAssistantContractState.AuditProperty}";
        var auditIndex = 0;
        foreach (var audit in auditNode.EnumerateArray())
        {
            var itemContext = $"{context}[{auditIndex}]";
            auditIndex++;

            if (audit.ValueKind != JsonValueKind.Object ||
                !audit.TryGetProperty("applicationState", out var applicationStateNode) ||
                !TryReadMathString(applicationStateNode, out var applicationState) ||
                !string.Equals(applicationState, "applied_to_state", StringComparison.OrdinalIgnoreCase) ||
                !audit.TryGetProperty("result", out var resultNode) ||
                !TryReadMathDecimal(resultNode, out var auditResult) ||
                !audit.TryGetProperty("referencedBy", out var referencesNode) ||
                referencesNode.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var checkedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var referenceIndex = 0;
            foreach (var referenceNode in referencesNode.EnumerateArray())
            {
                var referenceContext = $"{itemContext}.referencedBy[{referenceIndex}]";
                referenceIndex++;

                if (!TryReadMathString(referenceNode, out var reference) ||
                    !TryResolveMathAssistantAppliedDeltaReference(reference, out var deltaFieldName) ||
                    !checkedFields.Add(deltaFieldName))
                {
                    continue;
                }

                if (!root.TryGetProperty(deltaFieldName, out var deltaNode))
                {
                    AddMathIssue(issues, referenceContext, "math_audit_missing_referenced_delta",
                        "mathAudit помечен как применённый к боевому delta-полю, но само поле отсутствует в ответе.",
                        deltaFieldName, "missing",
                        "Добавь целевое delta-поле в ответ или убери ссылку из referencedBy, если число не применялось к состоянию.");
                    continue;
                }

                if (!TryReadMathDecimal(deltaNode, out var appliedDelta))
                {
                    AddMathIssue(issues, $"{contextPrefix}.{deltaFieldName}", "math_audit_referenced_delta_invalid",
                        "Боевое delta-поле, связанное с mathAudit, должно быть числом.",
                        "number", deltaNode.ValueKind.ToString(),
                        "Запиши применённое изменение как JSON number.");
                    continue;
                }

                if (appliedDelta != auditResult)
                {
                    AddMathIssue(issues, referenceContext, "math_audit_applied_delta_mismatch",
                        "Применённое боевое изменение не совпадает с mathAudit.result.",
                        FormatMathDecimal(auditResult), FormatMathDecimal(appliedDelta),
                        "Для currentHealthChange/currentPoiseChange/currentEnergyChange mathAudit.result должен быть числом со знаком, точно равным применённому полю.");
                }
            }
        }
    }

    private static bool TryResolveMathAssistantAppliedDeltaReference(string? reference, out string deltaFieldName)
    {
        deltaFieldName = "";

        var token = (reference ?? "").Trim();
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var lastSeparator = token.LastIndexOfAny(new[] { ':', '/', '\\', '.' });
        var candidate = lastSeparator >= 0 && lastSeparator + 1 < token.Length
            ? token[(lastSeparator + 1)..].Trim()
            : token;

        if (!MathAssistantAppliedDeltaReferenceFields.Contains(candidate))
            return false;

        deltaFieldName = candidate;
        return true;
    }

    private static void ValidateMathAuditMatchesRequest(
        JsonElement audit,
        string itemContext,
        MathContractRequestSnapshot request,
        string? expression,
        string? purpose,
        Dictionary<string, decimal>? variables,
        (MathAssistantRoundingMode Mode, int? DecimalPlaces, bool Invalid) rounding,
        List<ValidationIssue> issues)
    {
        if (!string.Equals(expression, request.Expression, StringComparison.Ordinal))
        {
            AddMathIssue(issues, $"{itemContext}.expression", "math_audit_request_expression_mismatch",
                "mathAudit.expression не совпадает с mathRequests.expression для того же requestId.",
                request.Expression, expression ?? "missing",
                "Синхронизируй audit с исходным requestId или используй новый requestId.");
        }

        if (!string.Equals(purpose, request.Purpose, StringComparison.Ordinal))
        {
            AddMathIssue(issues, $"{itemContext}.purpose", "math_audit_request_purpose_mismatch",
                "mathAudit.purpose не совпадает с mathRequests.purpose для того же requestId.",
                request.Purpose, purpose ?? "missing",
                "Синхронизируй purpose между request и audit.");
        }

        if (variables != null && !MathVariablesEqual(variables, request.Variables))
        {
            AddMathIssue(issues, $"{itemContext}.variables", "math_audit_request_variables_mismatch",
                "mathAudit.variables не совпадает с mathRequests.variables для того же requestId.",
                FormatMathVariables(request.Variables), FormatMathVariables(variables),
                "Синхронизируй variables между request и audit.");
        }

        if (!rounding.Invalid &&
            (rounding.Mode != request.RoundingMode || rounding.DecimalPlaces != request.DecimalPlaces))
        {
            AddMathIssue(issues, $"{itemContext}.rounding", "math_audit_request_rounding_mismatch",
                "mathAudit.rounding не совпадает с mathRequests.rounding для того же requestId.",
                FormatMathRounding(request.RoundingMode, request.DecimalPlaces),
                FormatMathRounding(rounding.Mode, rounding.DecimalPlaces),
                "Синхронизируй rounding между request и audit.");
        }

        _ = audit;
    }

    private static (MathAssistantRoundingMode Mode, int? DecimalPlaces, bool Invalid) ReadMathRounding(
        JsonElement owner,
        string context,
        bool required,
        List<ValidationIssue> issues)
    {
        if (!owner.TryGetProperty("rounding", out var roundingNode))
        {
            if (required)
            {
                AddMathIssue(issues, $"{context}.rounding", "math_rounding_missing",
                    "rounding обязателен для mathAudit.",
                    "rounding object", "missing",
                    "Добавь rounding: { \"mode\": \"none\" } или другой поддержанный режим.");
                return (MathAssistantRoundingMode.None, null, true);
            }

            return (MathAssistantRoundingMode.None, null, false);
        }

        if (roundingNode.ValueKind != JsonValueKind.Object)
        {
            AddMathIssue(issues, $"{context}.rounding", "math_rounding_invalid_shape",
                "rounding должен быть JSON object.",
                "JSON object", roundingNode.ValueKind.ToString(),
                "Используй rounding.mode и optional rounding.decimalPlaces.");
            return (MathAssistantRoundingMode.None, null, true);
        }

        if (!roundingNode.TryGetProperty("mode", out var modeNode) ||
            !TryReadMathString(modeNode, out var modeText) ||
            !TryParseMathRoundingMode(modeText, out var mode))
        {
            AddMathIssue(issues, $"{context}.rounding.mode", "math_rounding_invalid_mode",
                "Неподдержанный режим округления.",
                "none, floor, ceiling, to_zero, away_from_zero, to_nearest",
                roundingNode.TryGetProperty("mode", out var invalidModeNode) ? invalidModeNode.GetRawText() : "missing",
                "Выбери один из поддержанных режимов округления.");
            return (MathAssistantRoundingMode.None, null, true);
        }

        int? decimalPlaces = null;
        if (roundingNode.TryGetProperty("decimalPlaces", out var placesNode))
        {
            if (placesNode.ValueKind != JsonValueKind.Number ||
                !placesNode.TryGetInt32(out var parsedPlaces) ||
                parsedPlaces is < 0 or > 8)
            {
                AddMathIssue(issues, $"{context}.rounding.decimalPlaces", "math_rounding_invalid_decimal_places",
                    "decimalPlaces должен быть целым числом от 0 до 8.",
                    "integer 0..8", placesNode.GetRawText(),
                    "Исправь decimalPlaces или убери его для mode=none.");
                return (mode, null, true);
            }

            decimalPlaces = parsedPlaces;
        }

        return (mode, decimalPlaces, false);
    }

    private static Dictionary<string, decimal>? ReadMathVariables(
        JsonElement owner,
        string context,
        string propertyName,
        bool required,
        List<ValidationIssue> issues)
    {
        if (!owner.TryGetProperty(propertyName, out var variablesNode))
        {
            if (required)
            {
                AddMathIssue(issues, $"{context}.{propertyName}", "math_variables_missing",
                    $"{propertyName} обязателен для расчёта.",
                    "JSON object", "missing",
                    "Добавь объект variables, даже если он пустой.");
                return null;
            }

            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        if (variablesNode.ValueKind != JsonValueKind.Object)
        {
            AddMathIssue(issues, $"{context}.{propertyName}", "math_variables_invalid_shape",
                $"{propertyName} должен быть JSON object.",
                "JSON object", variablesNode.ValueKind.ToString(),
                "Укажи переменные как объект вида { \"имя\": 123 }.");
            return null;
        }

        var variables = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in variablesNode.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(variable.Name))
            {
                AddMathIssue(issues, $"{context}.{propertyName}", "math_variable_invalid_name",
                    "Имя переменной не может быть пустым.",
                    "non-empty identifier", "empty",
                    "Используй понятное имя переменной.");
                continue;
            }

            if (!TryReadMathDecimal(variable.Value, out var value))
            {
                AddMathIssue(issues, $"{context}.{propertyName}.{variable.Name}", "math_variable_non_numeric",
                    "Значение переменной должно быть числом.",
                    "number", variable.Value.ValueKind.ToString(),
                    "Передавай только числовые значения; текстовые причины оставляй в purpose/warnings.");
                continue;
            }

            variables[variable.Name] = value;
        }

        return variables;
    }

    private static string? ReadRequiredMathString(
        JsonElement owner,
        string context,
        string propertyName,
        string code,
        List<ValidationIssue> issues)
    {
        if (!owner.TryGetProperty(propertyName, out var node) ||
            !TryReadMathString(node, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            AddMathIssue(issues, $"{context}.{propertyName}", code,
                $"{propertyName} обязателен и должен быть непустой строкой.",
                "non-empty string",
                owner.TryGetProperty(propertyName, out var actualNode) ? actualNode.ValueKind.ToString() : "missing",
                $"Добавь непустой {propertyName}.");
            return null;
        }

        return value;
    }

    private static void ValidateMathStringArray(
        JsonElement owner,
        string context,
        string propertyName,
        string code,
        List<ValidationIssue> issues,
        bool allowEmptyStrings = false)
    {
        if (!owner.TryGetProperty(propertyName, out var node))
            return;

        if (node.ValueKind != JsonValueKind.Array)
        {
            AddMathIssue(issues, $"{context}.{propertyName}", code,
                $"{propertyName} должен быть массивом строк.",
                "array of strings", node.ValueKind.ToString(),
                $"Используй {propertyName}: [] или массив строк.");
            return;
        }

        var index = 0;
        foreach (var item in node.EnumerateArray())
        {
            if (!TryReadMathString(item, out var value) ||
                (!allowEmptyStrings && string.IsNullOrWhiteSpace(value)))
            {
                AddMathIssue(issues, $"{context}.{propertyName}[{index}]", code,
                    $"{propertyName} должен содержать только строки.",
                    allowEmptyStrings ? "string" : "non-empty string", item.ValueKind.ToString(),
                    "Исправь элемент массива или убери его.");
            }

            index++;
        }
    }

    private static MathAssistantEvaluationResult EvaluateMathContract(
        string expression,
        IReadOnlyDictionary<string, decimal> variables,
        MathAssistantRoundingMode roundingMode,
        int? decimalPlaces)
    {
        var service = new MathAssistantService();
        return service.Evaluate(new MathAssistantEvaluationRequest(expression, variables, roundingMode, decimalPlaces));
    }

    private static bool TryParseMathRoundingMode(string? value, out MathAssistantRoundingMode mode)
    {
        switch ((value ?? "").Trim().ToLowerInvariant())
        {
            case "none":
                mode = MathAssistantRoundingMode.None;
                return true;
            case "floor":
                mode = MathAssistantRoundingMode.Floor;
                return true;
            case "ceiling":
            case "ceil":
                mode = MathAssistantRoundingMode.Ceiling;
                return true;
            case "to_zero":
            case "tozero":
                mode = MathAssistantRoundingMode.ToZero;
                return true;
            case "away_from_zero":
            case "awayfromzero":
                mode = MathAssistantRoundingMode.AwayFromZero;
                return true;
            case "to_nearest":
            case "tonearest":
                mode = MathAssistantRoundingMode.ToNearest;
                return true;
            default:
                mode = MathAssistantRoundingMode.None;
                return false;
        }
    }

    private static bool TryReadMathString(JsonElement node, out string? value)
    {
        if (node.ValueKind == JsonValueKind.String)
        {
            value = node.GetString();
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryReadMathDecimal(JsonElement node, out decimal value)
    {
        if (node.ValueKind == JsonValueKind.Number && node.TryGetDecimal(out value))
            return true;

        value = 0;
        return false;
    }

    private static bool MathVariablesEqual(
        IReadOnlyDictionary<string, decimal> expected,
        IReadOnlyDictionary<string, decimal> actual)
    {
        if (expected.Count != actual.Count)
            return false;

        foreach (var pair in expected)
        {
            if (!actual.TryGetValue(pair.Key, out var actualValue) || actualValue != pair.Value)
                return false;
        }

        return true;
    }

    private static string FormatMathVariables(IReadOnlyDictionary<string, decimal> variables) =>
        string.Join(", ", variables.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{pair.Key}={FormatMathDecimal(pair.Value)}"));

    private static string FormatMathRounding(MathAssistantRoundingMode mode, int? decimalPlaces) =>
        decimalPlaces.HasValue
            ? $"{mode}, decimalPlaces={decimalPlaces.Value}"
            : mode.ToString();

    private static string FormatMathDecimal(decimal? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "null";

    private static void AddMathIssue(
        List<ValidationIssue> issues,
        string path,
        string code,
        string message,
        string? expected = null,
        string? actual = null,
        string? repairHint = null)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "MathAssistant",
            expected: expected,
            actual: actual,
            repairHint: repairHint,
            category: IssueCategory.ProtocolViolation));
    }
}
