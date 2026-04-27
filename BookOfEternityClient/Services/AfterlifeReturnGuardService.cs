using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed class AfterlifeReturnGuardService
{
    public const string GuardPath = "game_state/control/afterlife_return_guard.json";
    public const string PostLifeReturnReason = "post_life_return";

    private readonly FileSystemManager _fs;
    private readonly ILogger<AfterlifeReturnGuardService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public AfterlifeReturnGuardService(FileSystemManager fs, ILogger<AfterlifeReturnGuardService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task ActivatePostLifeReturnAsync(string? guardianId, string? guardianName, int activatedAtTurnNumber)
    {
        var state = new AfterlifeReturnGuardState
        {
            Reason = PostLifeReturnReason,
            RemainingProtectedTurns = 1,
            ActivatedAtUtc = DateTime.UtcNow.ToString("o"),
            ActivatedAtTurnNumber = activatedAtTurnNumber,
            GuardianId = guardianId ?? "",
            GuardianName = guardianName ?? ""
        };

        await WriteAsync(state);
    }

    public async Task ConsumeAfterAcceptedAfterlifeTurnAsync(int completedTurnNumber)
    {
        var (semanticState, state) = await ReadSemanticStateAsync();
        if (semanticState == AfterlifeReturnGuardSemanticState.Absent ||
            semanticState == AfterlifeReturnGuardSemanticState.BlockingInvalid ||
            state == null)
        {
            return;
        }

        if (semanticState == AfterlifeReturnGuardSemanticState.InactiveValid)
        {
            await ClearAsync();
            return;
        }

        state.RemainingProtectedTurns = Math.Max(0, state.RemainingProtectedTurns - 1);
        state.ConsumedAtTurnNumber = completedTurnNumber;

        if (state.RemainingProtectedTurns <= 0)
        {
            await ClearAsync();
            return;
        }

        await WriteAsync(state);
    }

    public async Task EnsureHealthyAsync(string? currentRealm)
    {
        var raw = await _fs.ReadFileAsync(GuardPath);
        if (string.IsNullOrWhiteSpace(raw))
            return;

        if (!RealmSemantics.HasResolvedRealm(currentRealm))
        {
            _logger.LogWarning("afterlife_return_guard.json найден при unresolved currentRealm. Guard сохраняется fail-closed до восстановления realm authority.");
            return;
        }

        if (!RealmSemantics.IsAfterlifeRealm(currentRealm))
        {
            _logger.LogInformation("afterlife_return_guard.json найден вне afterlife realm. Очистка stale guard state.");
            await ClearAsync();
            return;
        }

        var semanticState = Classify(raw, out _);
        if (semanticState == AfterlifeReturnGuardSemanticState.InactiveValid)
        {
            _logger.LogInformation("afterlife_return_guard.json больше не активен. Очистка stale guard state.");
            await ClearAsync();
        }
        else if (semanticState == AfterlifeReturnGuardSemanticState.BlockingInvalid)
        {
            _logger.LogWarning("afterlife_return_guard.json повреждён или семантически невалиден. Guard сохраняется fail-closed и продолжает блокировать защищённый return path.");
        }
    }

    public async Task<AfterlifeReturnGuardState?> ReadAsync()
    {
        var (semanticState, state) = await ReadSemanticStateAsync();
        return semanticState is AfterlifeReturnGuardSemanticState.ActiveValid or
            AfterlifeReturnGuardSemanticState.InactiveValid
            ? state
            : null;
    }

    public async Task<string?> BuildSystemReminderFragmentAsync(string? currentRealm)
    {
        if (!RealmSemantics.IsAfterlifeRealm(currentRealm))
            return null;

        var (semanticState, state) = await ReadSemanticStateAsync();
        if (semanticState == AfterlifeReturnGuardSemanticState.BlockingInvalid)
        {
            return "AFTERLIFE RETURN SAFETY: " +
                   "game_state/control/afterlife_return_guard.json is malformed or semantically invalid. " +
                   "Treat this as blocking corruption: do NOT write game_state/control/incarnation_trigger.json with source='guardian_forced' " +
                   "and do NOT re-enter Shining Abode until the guard file is repaired.";
        }

        if (semanticState != AfterlifeReturnGuardSemanticState.ActiveValid || state == null)
            return null;

        var guardianFragment = string.IsNullOrWhiteSpace(state.GuardianName)
            ? ""
            : $" Current guardian: {state.GuardianName}.";

        return "AFTERLIFE RETURN SAFETY: " +
               "game_state/control/afterlife_return_guard.json is active. " +
               "The soul has just returned from a mortal life and MUST receive at least one ordinary afterlife turn before any Guardian-forced incarnation." +
               guardianFragment +
               " Do NOT write game_state/control/incarnation_trigger.json with source='guardian_forced' on this protected turn.";
    }

    public async Task<(string GuardianId, string GuardianName)> ReadActiveGuardianContextAsync()
    {
        var json = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(json))
            return ("", "");

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("activeGuardian", out var activeGuardian) ||
                activeGuardian.ValueKind != JsonValueKind.Object)
            {
                return ("", "");
            }

            var guardianId = activeGuardian.TryGetProperty("guardianId", out var guardianIdNode) &&
                             guardianIdNode.ValueKind == JsonValueKind.String
                ? guardianIdNode.GetString() ?? ""
                : "";
            var guardianName = GuardianManifestation.GetDisplayName(activeGuardian);

            return (guardianId, guardianName);
        }
        catch
        {
            return ("", "");
        }
    }

    public Task ClearAsync()
    {
        _fs.DeleteFile(GuardPath);
        return Task.CompletedTask;
    }

    public static bool TryParse(string raw, out AfterlifeReturnGuardState state)
    {
        state = new AfterlifeReturnGuardState();

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            var parsed = JsonSerializer.Deserialize<AfterlifeReturnGuardState>(raw, JsonOpts);
            if (parsed == null)
                return false;

            state = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static AfterlifeReturnGuardSemanticState Classify(string? raw, out AfterlifeReturnGuardState? state)
    {
        state = null;

        if (string.IsNullOrWhiteSpace(raw))
            return AfterlifeReturnGuardSemanticState.Absent;

        if (!TryParse(raw, out var parsed))
            return AfterlifeReturnGuardSemanticState.BlockingInvalid;

        if (!string.Equals(parsed.Reason, PostLifeReturnReason, StringComparison.OrdinalIgnoreCase))
            return AfterlifeReturnGuardSemanticState.BlockingInvalid;

        state = parsed;
        return parsed.RemainingProtectedTurns > 0
            ? AfterlifeReturnGuardSemanticState.ActiveValid
            : AfterlifeReturnGuardSemanticState.InactiveValid;
    }

    private async Task<(AfterlifeReturnGuardSemanticState SemanticState, AfterlifeReturnGuardState? State)> ReadSemanticStateAsync()
    {
        var raw = await _fs.ReadFileAsync(GuardPath);
        var semanticState = Classify(raw, out var state);
        return (semanticState, state);
    }

    private async Task WriteAsync(AfterlifeReturnGuardState state)
    {
        await _fs.WriteFileAtomicAsync(GuardPath, JsonSerializer.Serialize(state, JsonOpts));
    }

}

public enum AfterlifeReturnGuardSemanticState
{
    Absent,
    ActiveValid,
    InactiveValid,
    BlockingInvalid
}

public sealed class AfterlifeReturnGuardState
{
    public string Reason { get; set; } = AfterlifeReturnGuardService.PostLifeReturnReason;
    public int RemainingProtectedTurns { get; set; } = 1;
    public string ActivatedAtUtc { get; set; } = "";
    public int ActivatedAtTurnNumber { get; set; }
    public string GuardianId { get; set; } = "";
    public string GuardianName { get; set; } = "";
    public int? ConsumedAtTurnNumber { get; set; }
}
