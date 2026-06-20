using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

public sealed class BrowserLifecycleDashboardService
{
    private const int MaxDisplayedIssues = 200;
    private readonly FileSystemManager _fs;
    private readonly LocalWebUiSessionStatusService _sessionStatus;
    private readonly ValidationService _validation;

    public BrowserLifecycleDashboardService(
        FileSystemManager fs,
        LocalWebUiSessionStatusService sessionStatus,
        ValidationService validation)
    {
        _fs = fs;
        _sessionStatus = sessionStatus;
        _validation = validation;
    }

    public async Task<BrowserLifecycleDashboardDto> BuildDashboardAsync()
    {
        var session = await _sessionStatus.BuildStatusAsync();
        var soul = await BuildSoulSummaryAsync();
        var validation = await BuildValidationAsync();

        return new BrowserLifecycleDashboardDto(
            SchemaVersion: 1,
            Session: session,
            Soul: soul,
            PendingTurn: session.PendingTurn,
            LocalUiLock: session.LocalUiLock,
            CanStartBrowserWrite: session.CanStartBrowserWrite,
            Validation: validation,
            Guidance: BuildGuidance(session, validation),
            Entrypoints: BuildEntrypoints(session));
    }

    public async Task<BrowserValidationSummaryDto> BuildValidationAsync()
    {
        List<ValidationIssue> issues;
        try
        {
            issues = await _validation.ValidateGameStateAsync();
        }
        catch (Exception ex)
        {
            issues = new List<ValidationIssue>
            {
                new(
                    "game_session",
                    IssueSeverity.Error,
                    $"Валидация не смогла завершиться: {ex.Message}",
                    code: "browser_validation_exception",
                    section: "BrowserLifecycle")
            };
        }

        var errorCount = issues.Count(static issue => issue.Severity == IssueSeverity.Error);
        var warningCount = issues.Count(static issue => issue.Severity == IssueSeverity.Warning);
        var infoCount = issues.Count(static issue => issue.Severity == IssueSeverity.Info);
        var state = errorCount > 0 ? "errors" : warningCount > 0 ? "warnings" : "clean";
        var label = errorCount > 0
            ? "Есть ошибки валидации"
            : warningCount > 0
                ? "Есть предупреждения"
                : "Состояние валидно";

        var groups = issues
            .GroupBy(static issue => new
            {
                Severity = issue.Severity.ToString(),
                Category = issue.Category.ToString(),
                Section = string.IsNullOrWhiteSpace(issue.Section) ? "Общее" : issue.Section!
            })
            .OrderByDescending(static group => SeverityRank(group.Key.Severity))
            .ThenBy(static group => group.Key.Category, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.Section, StringComparer.Ordinal)
            .Select(static group => new BrowserValidationGroupDto(
                group.Key.Severity,
                group.Key.Category,
                group.Key.Section,
                group.Count()))
            .ToArray();

        var issueDtos = issues
            .OrderByDescending(static issue => SeverityRank(issue.Severity.ToString()))
            .ThenBy(static issue => issue.FilePath, StringComparer.OrdinalIgnoreCase)
            .Take(MaxDisplayedIssues)
            .Select(static issue => new BrowserValidationIssueDto(
                issue.FilePath,
                issue.Severity.ToString(),
                issue.Category.ToString(),
                issue.Code ?? string.Empty,
                issue.Section ?? string.Empty,
                issue.Actor ?? string.Empty,
                issue.Message,
                issue.Expected ?? string.Empty,
                issue.Actual ?? string.Empty,
                issue.RepairHint ?? string.Empty))
            .ToArray();

        return new BrowserValidationSummaryDto(
            State: state,
            StatusLabel: label,
            IssueCount: issues.Count,
            ErrorCount: errorCount,
            WarningCount: warningCount,
            InfoCount: infoCount,
            DisplayedIssueCount: issueDtos.Length,
            Groups: groups,
            Issues: issueDtos);
    }

    private async Task<BrowserSoulSummaryDto> BuildSoulSummaryAsync()
    {
        var raw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new BrowserSoulSummaryDto(
                Name: "Неизвестная душа",
                FormDescription: string.Empty,
                CurrentRealm: "unknown",
                RealmLabel: "Царство не определено",
                CurrentIncarnation: 0,
                IsReadable: false,
                ReadError: "game_state/meta/soul_state.json отсутствует или пуст.");
        }

        try
        {
            var root = JsonNode.Parse(raw) as JsonObject;
            if (root == null)
            {
                return new BrowserSoulSummaryDto(
                    Name: "Неизвестная душа",
                    FormDescription: string.Empty,
                    CurrentRealm: "unknown",
                    RealmLabel: "Царство не определено",
                    CurrentIncarnation: 0,
                    IsReadable: false,
                    ReadError: "soul_state.json должен быть JSON-объектом.");
            }

            var realm = GetString(root, "currentRealm") ?? "unknown";
            return new BrowserSoulSummaryDto(
                Name: GetString(root, "soulName", "name") ?? "Неизвестная душа",
                FormDescription: GetString(root, "soulFormDescription") ?? string.Empty,
                CurrentRealm: realm,
                RealmLabel: RealmLabel(realm),
                CurrentIncarnation: GetInt(root, "currentIncarnation") ?? 0,
                IsReadable: true,
                ReadError: string.Empty);
        }
        catch (JsonException ex)
        {
            return new BrowserSoulSummaryDto(
                Name: "Неизвестная душа",
                FormDescription: string.Empty,
                CurrentRealm: "unknown",
                RealmLabel: "Царство не определено",
                CurrentIncarnation: 0,
                IsReadable: false,
                ReadError: $"soul_state.json повреждён: {ex.Message}");
        }
    }

    private static IReadOnlyList<BrowserLifecycleGuidanceDto> BuildGuidance(
        LocalWebUiSessionStatus session,
        BrowserValidationSummaryDto validation)
    {
        var guidance = new List<BrowserLifecycleGuidanceDto>();

        if (session.PendingTurn.HasActiveGmTurn)
        {
            var hasComplete = ArtifactExists(session.PendingTurn, BrowserPendingTurnInspector.TurnCompletePath);
            var hasError = ArtifactExists(session.PendingTurn, BrowserPendingTurnInspector.TurnErrorPath);
            guidance.Add(new BrowserLifecycleGuidanceDto(
                Severity: hasError ? "error" : hasComplete ? "success" : "warning",
                Title: hasError
                    ? "Ход ГМа завершился ошибкой"
                    : hasComplete
                        ? "Ход ГМа готов к принятию"
                        : "Ход ГМа ожидает завершения",
                Message: session.PendingTurn.Message,
                ActionLabel: hasError
                    ? "Открыть repair/rollback в консоли"
                    : hasComplete
                        ? "Принять готовый ответ через обработку хода"
                        : "Дождаться ответа ГМа или отменить ход",
                Command: string.Empty));
        }
        else if (session.CanStartBrowserWrite)
        {
            guidance.Add(new BrowserLifecycleGuidanceDto(
                Severity: "success",
                Title: "Локальные записи из браузера доступны",
                Message: "Активный ход ГМа и свежая чужая UI-блокировка не обнаружены.",
                ActionLabel: "Можно запускать перенесённые браузерные формы",
                Command: string.Empty));
        }

        if (validation.ErrorCount > 0)
        {
            guidance.Add(new BrowserLifecycleGuidanceDto(
                Severity: "error",
                Title: "Требуется repair перед продолжением",
                Message: $"Валидация нашла ошибок: {validation.ErrorCount}. Откройте группировку ниже и исправьте state/contract файлы.",
                ActionLabel: "Проверить валидацию",
                Command: "/validate"));
        }
        else if (validation.WarningCount > 0)
        {
            guidance.Add(new BrowserLifecycleGuidanceDto(
                Severity: "warning",
                Title: "Есть предупреждения валидации",
                Message: $"Предупреждений: {validation.WarningCount}. Их стоит разобрать до длинного игрового цикла.",
                ActionLabel: "Проверить валидацию",
                Command: "/validate"));
        }

        return guidance;
    }

    private static IReadOnlyList<BrowserLifecycleEntrypointDto> BuildEntrypoints(LocalWebUiSessionStatus session) =>
        new[]
        {
            new BrowserLifecycleEntrypointDto(
                Label: "Проверить валидацию",
                Command: "/validate",
                Endpoint: "/api/lifecycle/validate",
                Enabled: true,
                Description: "Запускает тот же ValidationService, что и консоль."),
            new BrowserLifecycleEntrypointDto(
                Label: "Настройка следующей жизни",
                Command: "/world_setup",
                Endpoint: "/api/explorer/command",
                Enabled: session.CanStartBrowserWrite,
                Description: "Открывает браузерную форму настройки мира, если нет активного хода ГМа."),
            new BrowserLifecycleEntrypointDto(
                Label: "Духовное действие",
                Command: "/spiritual_action",
                Endpoint: "/api/explorer/command",
                Enabled: session.CanStartBrowserWrite,
                Description: "Открывает форму духовного действия, если локальная запись не заблокирована.")
        };

    private static bool ArtifactExists(BrowserPendingTurnStatus pending, string path) =>
        pending.Artifacts.Any(artifact =>
            artifact.Exists &&
            string.Equals(artifact.Path, path, StringComparison.OrdinalIgnoreCase));

    private static string? GetString(JsonObject root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetPropertyValue(name, out var node) && node is JsonValue value)
            {
                try
                {
                    return value.GetValue<string>();
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        return null;
    }

    private static int? GetInt(JsonObject root, string name)
    {
        if (!root.TryGetPropertyValue(name, out var node) || node is not JsonValue value)
            return null;

        try
        {
            return value.GetValue<int>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string RealmLabel(string realm) =>
        realm.Equals("Mortal World", StringComparison.OrdinalIgnoreCase)
            ? "Смертный мир"
            : realm.Equals("Chaos Sea", StringComparison.OrdinalIgnoreCase)
                ? "Море Хаоса"
                : realm.Equals("Shining Abode", StringComparison.OrdinalIgnoreCase)
                    ? "Сияющая Обитель"
                    : "Царство не определено";

    private static int SeverityRank(string severity) =>
        severity.Equals(nameof(IssueSeverity.Error), StringComparison.OrdinalIgnoreCase)
            ? 3
            : severity.Equals(nameof(IssueSeverity.Warning), StringComparison.OrdinalIgnoreCase)
                ? 2
                : 1;
}

public sealed record BrowserLifecycleDashboardDto(
    int SchemaVersion,
    LocalWebUiSessionStatus Session,
    BrowserSoulSummaryDto Soul,
    BrowserPendingTurnStatus PendingTurn,
    BrowserLocalUiLockStatus LocalUiLock,
    bool CanStartBrowserWrite,
    BrowserValidationSummaryDto Validation,
    IReadOnlyList<BrowserLifecycleGuidanceDto> Guidance,
    IReadOnlyList<BrowserLifecycleEntrypointDto> Entrypoints);

public sealed record BrowserSoulSummaryDto(
    string Name,
    string FormDescription,
    string CurrentRealm,
    string RealmLabel,
    int CurrentIncarnation,
    bool IsReadable,
    string ReadError);

public sealed record BrowserValidationSummaryDto(
    string State,
    string StatusLabel,
    int IssueCount,
    int ErrorCount,
    int WarningCount,
    int InfoCount,
    int DisplayedIssueCount,
    IReadOnlyList<BrowserValidationGroupDto> Groups,
    IReadOnlyList<BrowserValidationIssueDto> Issues);

public sealed record BrowserValidationGroupDto(
    string Severity,
    string Category,
    string Section,
    int Count);

public sealed record BrowserValidationIssueDto(
    string FilePath,
    string Severity,
    string Category,
    string Code,
    string Section,
    string Actor,
    string Message,
    string Expected,
    string Actual,
    string RepairHint);

public sealed record BrowserLifecycleGuidanceDto(
    string Severity,
    string Title,
    string Message,
    string ActionLabel,
    string Command);

public sealed record BrowserLifecycleEntrypointDto(
    string Label,
    string Command,
    string Endpoint,
    bool Enabled,
    string Description);
