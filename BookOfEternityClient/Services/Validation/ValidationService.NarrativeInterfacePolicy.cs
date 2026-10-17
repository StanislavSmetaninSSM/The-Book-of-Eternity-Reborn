using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private bool EvaluateAcceptedNarrativePrefix(JsonElement root, List<ValidationIssue> issues, out string responseText)
    {
        responseText = string.Empty;
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                "output/narrative_response.json",
                IssueSeverity.Error,
                "output/narrative_response.json должен быть JSON object",
                code: "accepted_turn_invalid_narrative_json_root",
                section: "Narrative",
                expected: "JSON object with response and timestamp",
                actual: root.ValueKind.ToString(),
                repairHint: "Оставь output/narrative_response.json валидным JSON-объектом с полями response и timestamp."));
            return false;
        }

        if (!root.TryGetProperty("response", out var responseProp) ||
            responseProp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(responseProp.GetString()))
        {
            issues.Add(new ValidationIssue(
                "output/narrative_response.json.response",
                IssueSeverity.Error,
                "Accepted GM turn должен содержать непустой narrative response",
                code: "accepted_turn_empty_narrative_response",
                section: "Narrative",
                expected: "non-empty response string",
                actual: "missing or empty",
                repairHint: "Поле response в output/narrative_response.json должно содержать свежий текст нарратива для текущего хода."));
        }
        else
        {
            responseText = responseProp.GetString() ?? string.Empty;
        }

        return true;
    }

    private void EvaluateAcceptedNarrativeTail(JsonElement root, string responseText, List<ValidationIssue> issues)
    {
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(prop.Name, "response", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(prop.Name, "timestamp", StringComparison.OrdinalIgnoreCase))
                continue;

            issues.Add(new ValidationIssue(
                $"output/narrative_response.json.{prop.Name}",
                IssueSeverity.Error,
                "output/narrative_response.json содержит неподдерживаемое top-level поле",
                code: "narrative_response_unknown_field",
                section: "Narrative",
                expected: "response | timestamp",
                actual: prop.Name,
                repairHint: "Пиши в output/narrative_response.json только response и timestamp для текущего хода."));
        }

        if (!string.IsNullOrWhiteSpace(responseText))
        {
            if (TryFindTechnicalRepairLeakInNarrative(responseText, out var matchedTechnicalText))
            {
                issues.Add(new ValidationIssue(
                    "output/narrative_response.json.response",
                    IssueSeverity.Error,
                    "Player-facing narrative response содержит технический repair/validation/storage текст.",
                    code: "narrative_response_technical_repair_leak",
                    section: "Narrative",
                    expected: "diegetic player-facing scene text without JSON, validation, repair, canonical-state, or storage-shape wording",
                    actual: matchedTechnicalText,
                    repairHint: "Перепиши response как внутриигровую сцену. Не упоминай JSON, validation/repair, canonical state, массивы, поля, файлы или факт технической записи состояния; технические детали должны оставаться только в debug_logs/repair evidence."));
            }

        }
    }

    private bool EvaluateAcceptedInterfacePrefix(JsonElement root, string interfacePath, List<ValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                "output/interface_updates.json",
                IssueSeverity.Error,
                "output/interface_updates.json должен быть JSON object",
                code: "accepted_turn_invalid_interface_updates_root",
                section: "InterfaceUpdates",
                expected: "JSON object with dialogueOptions/image_prompt and timestamp",
                actual: root.ValueKind.ToString(),
                repairHint: "Оставь output/interface_updates.json валидным JSON-объектом. Используй только поля dialogueOptions, image_prompt и timestamp."));
            return false;
        }

        ValidateDialogueOptionsData(root, interfacePath, issues);
        ValidateOptionalNullableStringField(root, interfacePath, issues, "image_prompt");

        var hasDialogueOptions = root.TryGetProperty("dialogueOptions", out _);
        var hasImagePrompt = root.TryGetProperty("image_prompt", out _);
        if (!hasDialogueOptions && !hasImagePrompt)
        {
            issues.Add(new ValidationIssue(
                interfacePath,
                IssueSeverity.Error,
                "output/interface_updates.json не должен быть timestamp-only stub без интерфейсного payload",
                code: "interface_updates_missing_payload",
                section: "InterfaceUpdates",
                expected: "dialogueOptions and/or image_prompt, or the file should be absent when unused",
                actual: "timestamp-only or unrelated object",
                repairHint: "Если интерфейсных обновлений нет, не записывай output/interface_updates.json. Если файл нужен, добавь dialogueOptions и/или image_prompt для текущего хода."));
        }

        return true;
    }

    private void EvaluateAcceptedInterfaceTail(JsonElement root, string interfacePath, List<ValidationIssue> issues)
    {
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(prop.Name, "dialogueOptions", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(prop.Name, "image_prompt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(prop.Name, "timestamp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            issues.Add(new ValidationIssue(
                $"{interfacePath}.{prop.Name}",
                IssueSeverity.Error,
                "output/interface_updates.json содержит неподдерживаемое top-level поле",
                code: "interface_updates_unknown_field",
                section: "InterfaceUpdates",
                expected: "dialogueOptions | image_prompt | timestamp",
                actual: prop.Name,
                repairHint: "Пиши в output/interface_updates.json только dialogueOptions, image_prompt и timestamp для текущего хода."));
        }
    }
}
