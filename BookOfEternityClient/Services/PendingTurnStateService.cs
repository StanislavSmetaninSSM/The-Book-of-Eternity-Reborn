using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

/// <summary>
/// Persists the pending dice/gacha state for the next ordinary player turn.
/// This allows Reveal Fate / Rewrite Fate to survive menus, cancellations, and save/load.
/// </summary>
public sealed class PendingTurnStateService
{
    public const string PendingDiceStatePath = "game_state/control/pending_dice_state.json";

    private readonly FileSystemManager _fs;
    private readonly ILogger<PendingTurnStateService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public PendingTurnStateService(FileSystemManager fs, ILogger<PendingTurnStateService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task<PendingTurnState> GetOrCreateAsync()
    {
        var existing = await ReadAsync();
        if (IsValid(existing))
            return existing!;

        var created = CreateFreshState();
        await WriteAsync(created);
        return created;
    }

    public async Task<PendingTurnState> RevealAsync()
    {
        var state = await GetOrCreateAsync();
        if (!state.IsFateLocked)
        {
            state.IsFateLocked = true;
            state.FateLockedAtUtc = DateTime.UtcNow.ToString("o");
            state.LastUpdatedUtc = state.FateLockedAtUtc;
            await WriteAsync(state);
        }

        return state;
    }

    public async Task<PendingTurnState> RewriteAsync()
    {
        var rewritten = CreateFreshState();
        rewritten.IsFateLocked = true;
        rewritten.FateLockedAtUtc = DateTime.UtcNow.ToString("o");
        rewritten.LastUpdatedUtc = rewritten.FateLockedAtUtc;
        await WriteAsync(rewritten);
        return rewritten;
    }

    public async Task<PendingTurnState> RotateAfterAcceptedTurnAsync()
    {
        var next = CreateFreshState();
        await WriteAsync(next);
        return next;
    }

    public void Clear() => _fs.DeleteFile(PendingDiceStatePath);

    private PendingTurnState CreateFreshState()
    {
        var now = DateTime.UtcNow.ToString("o");
        var visibleDice = GameLoop.GenerateSecureRandomDice();
        var hiddenGachaDice = GameLoop.GenerateSecureRandomDice(4);

        return new PendingTurnState
        {
            PreGeneratedDices1d20 = visibleDice,
            GachaBaseResult = GameLoop.ComputeGachaBase(hiddenGachaDice),
            IsFateLocked = false,
            CreatedAtUtc = now,
            LastUpdatedUtc = now
        };
    }

    private async Task<PendingTurnState?> ReadAsync()
    {
        var json = await _fs.ReadFileAsync(PendingDiceStatePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PendingTurnState>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось прочитать pending dice state");
            return null;
        }
    }

    private async Task WriteAsync(PendingTurnState state)
    {
        state.LastUpdatedUtc = DateTime.UtcNow.ToString("o");
        await _fs.WriteFileAtomicAsync(PendingDiceStatePath, JsonSerializer.Serialize(state, JsonOpts));
    }

    private static bool IsValid(PendingTurnState? state)
    {
        return state != null &&
               state.PreGeneratedDices1d20.Length == 20 &&
               state.GachaBaseResult != null &&
               !string.IsNullOrWhiteSpace(state.GachaBaseResult.BaseRarity);
    }
}

public sealed class PendingTurnState
{
    [JsonPropertyName("preGeneratedDices1d20")]
    public int[] PreGeneratedDices1d20 { get; set; } = Array.Empty<int>();

    [JsonPropertyName("gachaBaseResult")]
    public GachaResult? GachaBaseResult { get; set; }

    [JsonPropertyName("isFateLocked")]
    public bool IsFateLocked { get; set; }

    [JsonPropertyName("createdAtUtc")]
    public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("fateLockedAtUtc")]
    public string? FateLockedAtUtc { get; set; }

    [JsonPropertyName("lastUpdatedUtc")]
    public string LastUpdatedUtc { get; set; } = DateTime.UtcNow.ToString("o");
}
