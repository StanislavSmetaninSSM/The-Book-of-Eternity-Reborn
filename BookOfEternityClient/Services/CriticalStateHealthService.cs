using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed class CriticalStateHealthService
{
    private const long CriticalFileSizeLimitBytes = 16L * 1024L * 1024L;

    private static readonly HashSet<string> SuspiciousPowerShellObjectKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ast",
        "StartPosition",
        "Extent",
        "Attributes",
        "DebuggerHidden",
        "PipelineElements",
        "ScriptPosition",
        "StartScriptPosition",
        "EndScriptPosition",
        "InvocationOperator",
        "CommandElements",
        "BlockKind",
        "UsingStatements",
        "ParamBlock",
        "BeginBlock",
        "ProcessBlock",
        "EndBlock"
    };

    private readonly FileSystemManager _fs;
    private readonly ILogger<CriticalStateHealthService> _logger;

    public CriticalStateHealthService(FileSystemManager fs, ILogger<CriticalStateHealthService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task<SessionHealthCheckResult> AssessCurrentSessionHealthAsync()
    {
        var issues = await ValidateCriticalCanonicalStateAsync();
        if (issues.Count == 0)
            return new SessionHealthCheckResult(false, null, issues);

        var primary = issues[0];
        var message =
            $"Текущая сессия повреждена: {primary.Message} " +
            "Continue временно скрыт. Используй New Game / Load Game или восстанови сессию из корректного сохранения.";
        return new SessionHealthCheckResult(true, message, issues);
    }

    public async Task<List<ValidationIssue>> ValidateAcceptedTurnRawStateAsync()
    {
        var issues = new List<ValidationIssue>();

        await ValidateCriticalJsonFileAsync(
            "game_state/meta/guardians.json",
            "CriticalState",
            "guardians raw accepted-turn state",
            issues,
            requireCanonicalShape: false,
            allowGuardiansCommandSurface: true);
        await ValidateCriticalJsonFileAsync(
            "game_state/meta/soul_state.json",
            "CriticalState",
            "soul_state object",
            issues,
            requireCanonicalShape: false);
        await ValidateCriticalJsonFileAsync(
            "game_state/meta/achievements.json",
            "CriticalState",
            "achievements object",
            issues,
            requireCanonicalShape: false);
        await ValidateCriticalJsonFileAsync(
            "lore/codex_entries.json",
            "CriticalState",
            "codex_entries object",
            issues,
            requireCanonicalShape: false);

        return issues;
    }

    public async Task<List<ValidationIssue>> ValidateCriticalCanonicalStateAsync()
    {
        var issues = new List<ValidationIssue>();

        await ValidateCriticalJsonFileAsync(
            "game_state/meta/guardians.json",
            "CriticalState",
            "canonical guardians object",
            issues,
            requireCanonicalShape: true,
            allowGuardiansCommandSurface: false);
        await ValidateCriticalJsonFileAsync(
            "game_state/meta/soul_state.json",
            "CriticalState",
            "canonical soul_state object",
            issues,
            requireCanonicalShape: true);
        await ValidateCriticalJsonFileAsync(
            "game_state/meta/achievements.json",
            "CriticalState",
            "canonical achievements object",
            issues,
            requireCanonicalShape: true);
        await ValidateCriticalJsonFileAsync(
            "lore/codex_entries.json",
            "CriticalState",
            "canonical codex_entries object",
            issues,
            requireCanonicalShape: true);

        return issues;
    }

    private async Task ValidateCriticalJsonFileAsync(
        string relativePath,
        string section,
        string expectedShape,
        List<ValidationIssue> issues,
        bool requireCanonicalShape,
        bool allowGuardiansCommandSurface = false)
    {
        var fullPath = _fs.ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return;

        var fileInfo = new FileInfo(fullPath);
        if (fileInfo.Length > CriticalFileSizeLimitBytes)
        {
            issues.Add(new ValidationIssue(
                relativePath,
                IssueSeverity.Error,
                $"{Path.GetFileName(relativePath)} превышает безопасный размер для critical state и выглядит как повреждённый файл",
                code: "critical_state_file_oversized",
                section: section,
                expected: $"<= {CriticalFileSizeLimitBytes} bytes",
                actual: fileInfo.Length.ToString(),
                repairHint: $"Перепиши {relativePath} как компактный canonical JSON. Не сериализуй runtime diagnostics, AST objects или другие посторонние структуры.",
                category: IssueCategory.ProtocolViolation));
            return;
        }

        var json = await _fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            issues.Add(new ValidationIssue(
                relativePath,
                IssueSeverity.Error,
                $"{Path.GetFileName(relativePath)} пуст или не читается как critical state",
                code: "critical_state_file_empty",
                section: section,
                expected: expectedShape,
                actual: "missing or empty",
                repairHint: $"Восстанови {relativePath} как valid {expectedShape}.",
                category: IssueCategory.ProtocolViolation));
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    relativePath,
                    IssueSeverity.Error,
                    $"{Path.GetFileName(relativePath)} должен быть JSON object",
                    code: "critical_state_invalid_root",
                    section: section,
                    expected: expectedShape,
                    actual: doc.RootElement.ValueKind.ToString(),
                    repairHint: $"Перепиши {relativePath} как valid JSON object без scalar/array root.",
                    category: IssueCategory.ProtocolViolation));
                return;
            }

            if (relativePath.Equals("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase))
            {
                if (ContainsSuspiciousPowerShellObjectShape(doc.RootElement))
                {
                    issues.Add(new ValidationIssue(
                        relativePath,
                        IssueSeverity.Error,
                        "guardians.json содержит признаки сериализованного PowerShell runtime/AST object вместо canonical guardian data",
                        code: "guardians_contains_powershell_runtime_object",
                        section: section,
                        expected: "canonical guardian JSON data",
                        actual: "suspicious PowerShell object shape",
                        repairHint: "Перепиши guardians.json как canonical JSON data. Не сериализуй ScriptBlock/Ast/diagnostic objects и не используй PowerShell runtime objects как данные.",
                        category: IssueCategory.ProtocolViolation));
                    return;
                }

                if (!HasValidGuardianSurface(doc.RootElement, requireCanonicalShape, allowGuardiansCommandSurface))
                {
                    issues.Add(new ValidationIssue(
                        relativePath,
                        IssueSeverity.Error,
                        requireCanonicalShape
                            ? "guardians.json не похож на canonical guardian state"
                            : "guardians.json не похож ни на допустимый raw guardian command surface, ни на guardian state",
                        code: requireCanonicalShape
                            ? "guardians_missing_canonical_surface"
                            : "guardians_missing_valid_surface",
                        section: section,
                        expected: requireCanonicalShape
                            ? "guardians array and/or activeGuardian/pendingGuardianCreation/chaosSeaNavigation object"
                            : "canonical guardians object, UpdateGuardians command surface, or guardianQuestProgressUpdates command surface",
                        actual: "unknown guardians root shape",
                        repairHint: requireCanonicalShape
                            ? "Сохрани guardians.json как canonical state object с guardians array и сопутствующими guardian sections."
                            : "Передай guardians.json либо как canonical guardian state, либо как допустимый raw surface с UpdateGuardians/guardianQuestProgressUpdates/guardian sections, без посторонних serializer artifacts.",
                        category: IssueCategory.ProtocolViolation));
                }

                return;
            }

            if (requireCanonicalShape && doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    relativePath,
                    IssueSeverity.Error,
                    $"{Path.GetFileName(relativePath)} должен быть canonical JSON object",
                    code: "critical_state_missing_canonical_object",
                    section: section,
                    expected: expectedShape,
                    actual: doc.RootElement.ValueKind.ToString(),
                    repairHint: $"Перепиши {relativePath} как canonical JSON object.",
                    category: IssueCategory.ProtocolViolation));
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Critical state file {Path} is invalid JSON", relativePath);
            issues.Add(new ValidationIssue(
                relativePath,
                IssueSeverity.Error,
                $"{Path.GetFileName(relativePath)} не является валидным JSON",
                code: "critical_state_invalid_json",
                section: section,
                expected: expectedShape,
                actual: "invalid JSON",
                repairHint: $"Восстанови {relativePath} как valid JSON. Если файл генерируется внешним daemon/script, убери non-JSON serialization artifacts.",
                category: IssueCategory.ProtocolViolation));
        }
    }

    private static bool HasValidGuardianSurface(JsonElement root, bool requireCanonicalShape, bool allowGuardiansCommandSurface)
    {
        var hasGuardians = root.TryGetProperty("guardians", out var guardians) && guardians.ValueKind == JsonValueKind.Array;
        var hasValidOptionalSections =
            HasOptionalObjectOrNull(root, "activeGuardian") &&
            HasOptionalObjectOrNull(root, "pendingGuardianCreation") &&
            HasOptionalObjectOrNull(root, "chaosSeaNavigation");

        if (requireCanonicalShape)
            return hasGuardians && hasValidOptionalSections;

        if (hasGuardians && hasValidOptionalSections)
            return true;

        return allowGuardiansCommandSurface &&
               ((root.TryGetProperty("UpdateGuardians", out var updates) &&
                 updates.ValueKind == JsonValueKind.Array) ||
                (root.TryGetProperty(GuardianProjectState.QuestProgressUpdatesProperty, out var questProgressUpdates) &&
                 questProgressUpdates.ValueKind == JsonValueKind.Array));
    }

    private static bool HasOptionalObjectOrNull(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
            return true;

        return property.ValueKind is JsonValueKind.Object or JsonValueKind.Null;
    }

    private static bool ContainsSuspiciousPowerShellObjectShape(JsonElement root)
    {
        var stack = new Stack<JsonElement>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            switch (current.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in current.EnumerateObject())
                    {
                        if (SuspiciousPowerShellObjectKeys.Contains(prop.Name))
                            return true;
                        stack.Push(prop.Value);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in current.EnumerateArray())
                        stack.Push(item);
                    break;
            }
        }

        return false;
    }
}

public sealed record SessionHealthCheckResult(
    bool HasRecoverableSessionError,
    string? UserMessage,
    IReadOnlyList<ValidationIssue> Issues);
