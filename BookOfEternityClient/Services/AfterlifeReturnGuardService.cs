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
        var state = await ReadAsync();
        if (state == null)
            return;

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

        if (!IsAfterlifeRealm(currentRealm))
        {
            _logger.LogInformation("afterlife_return_guard.json найден вне afterlife realm. Очистка stale guard state.");
            await ClearAsync();
            return;
        }

        if (!TryParse(raw, out var state) ||
            !string.Equals(state.Reason, PostLifeReturnReason, StringComparison.OrdinalIgnoreCase) ||
            state.RemainingProtectedTurns <= 0)
        {
            _logger.LogWarning("afterlife_return_guard.json невалиден или неактуален. Очистка client-authored guard state.");
            await ClearAsync();
        }
    }

    public async Task<AfterlifeReturnGuardState?> ReadAsync()
    {
        var raw = await _fs.ReadFileAsync(GuardPath);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return TryParse(raw, out var state) ? state : null;
    }

    public async Task<string?> BuildSystemReminderFragmentAsync(string? currentRealm)
    {
        if (!IsAfterlifeRealm(currentRealm))
            return null;

        var state = await ReadAsync();
        if (state == null || state.RemainingProtectedTurns <= 0)
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

    private async Task WriteAsync(AfterlifeReturnGuardState state)
    {
        await _fs.WriteFileAtomicAsync(GuardPath, JsonSerializer.Serialize(state, JsonOpts));
    }

    private static bool IsAfterlifeRealm(string? realm) =>
        string.Equals(realm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(realm);
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
