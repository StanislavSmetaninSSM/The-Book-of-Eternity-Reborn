using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed class SoulIdentityService
{
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private readonly FileSystemManager _fs;
    private readonly ILogger<SoulIdentityService> _logger;

    public SoulIdentityService(FileSystemManager fs, ILogger<SoulIdentityService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task<SoulRenameResult> RenameSoulAsync(string requestedName)
    {
        var normalizedNewName = NormalizeSoulName(requestedName);
        if (string.IsNullOrWhiteSpace(normalizedNewName))
        {
            return new SoulRenameResult(
                Success: false,
                Changed: false,
                CurrentSoulName: string.Empty,
                PreviousSoulNames: Array.Empty<string>(),
                ErrorMessage: "Имя души не может быть пустым.");
        }

        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
        {
            return new SoulRenameResult(
                Success: false,
                Changed: false,
                CurrentSoulName: string.Empty,
                PreviousSoulNames: Array.Empty<string>(),
                ErrorMessage: "Не удалось прочитать game_state/meta/soul_state.json.");
        }

        JsonObject soulState;
        try
        {
            soulState = JsonNode.Parse(soulJson) as JsonObject ?? new JsonObject();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse soul_state.json during soul rename.");
            return new SoulRenameResult(
                Success: false,
                Changed: false,
                CurrentSoulName: string.Empty,
                PreviousSoulNames: Array.Empty<string>(),
                ErrorMessage: $"soul_state.json повреждён: {ex.Message}");
        }

        var currentSoulName = NormalizeSoulName(soulState["soulName"]?.GetValue<string?>() ?? string.Empty);
        var previousSoulNames = NormalizePreviousSoulNames(soulState["previousSoulNames"], currentSoulName);

        if (string.Equals(currentSoulName, normalizedNewName, StringComparison.OrdinalIgnoreCase))
        {
            if (soulState["previousSoulNames"] is null)
            {
                soulState["previousSoulNames"] = CreateJsonArray(previousSoulNames);
                await _fs.WriteFileAtomicAsync(
                    "game_state/meta/soul_state.json",
                    GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
                        soulState,
                        GuardianPolicyContracts.SoulStatePatchConflictContext.None).ToJsonString(JsonWriteOptions));
            }

            return new SoulRenameResult(
                Success: true,
                Changed: false,
                CurrentSoulName: string.IsNullOrWhiteSpace(currentSoulName) ? normalizedNewName : currentSoulName,
                PreviousSoulNames: previousSoulNames);
        }

        previousSoulNames.RemoveAll(name => string.Equals(name, normalizedNewName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(currentSoulName) &&
            !previousSoulNames.Contains(currentSoulName, StringComparer.OrdinalIgnoreCase))
        {
            previousSoulNames.Add(currentSoulName);
        }

        soulState["soulName"] = normalizedNewName;
        soulState["previousSoulNames"] = CreateJsonArray(previousSoulNames);
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
                soulState,
                GuardianPolicyContracts.SoulStatePatchConflictContext.None).ToJsonString(JsonWriteOptions));

        await SyncPendingGuardianCreationSoulNameAsync(normalizedNewName);

        return new SoulRenameResult(
            Success: true,
            Changed: true,
            CurrentSoulName: normalizedNewName,
            PreviousSoulNames: previousSoulNames);
    }

    public async Task<SoulFormDescriptionUpdateResult> UpdateSoulFormDescriptionAsync(string requestedDescription)
    {
        var normalizedDescription = NormalizeSoulFormDescription(requestedDescription);
        if (string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return new SoulFormDescriptionUpdateResult(
                Success: false,
                Changed: false,
                CurrentSoulFormDescription: string.Empty,
                ErrorMessage: "Описание формы души не может быть пустым.");
        }

        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
        {
            return new SoulFormDescriptionUpdateResult(
                Success: false,
                Changed: false,
                CurrentSoulFormDescription: string.Empty,
                ErrorMessage: "Не удалось прочитать game_state/meta/soul_state.json.");
        }

        JsonObject soulState;
        try
        {
            soulState = JsonNode.Parse(soulJson) as JsonObject ?? new JsonObject();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse soul_state.json during soul form description update.");
            return new SoulFormDescriptionUpdateResult(
                Success: false,
                Changed: false,
                CurrentSoulFormDescription: string.Empty,
                ErrorMessage: $"soul_state.json повреждён: {ex.Message}");
        }

        var currentDescription = NormalizeSoulFormDescription(ReadOptionalString(soulState["soulFormDescription"]));
        var changed = !string.Equals(currentDescription, normalizedDescription, StringComparison.Ordinal);
        if (changed || soulState["soulFormDescription"] is null)
        {
            soulState["soulFormDescription"] = normalizedDescription;
            await _fs.WriteFileAtomicAsync(
                "game_state/meta/soul_state.json",
                GuardianPolicyContracts.CreatePatchedSoulStateWriteRoot(
                    soulState,
                    GuardianPolicyContracts.SoulStatePatchConflictContext.None).ToJsonString(JsonWriteOptions));
        }

        return new SoulFormDescriptionUpdateResult(
            Success: true,
            Changed: changed,
            CurrentSoulFormDescription: normalizedDescription);
    }

    internal static string NormalizeSoulName(string? rawName)
    {
        return TextComposer.CollapseToSingleLine(rawName ?? string.Empty).Trim();
    }

    internal static string NormalizeSoulFormDescription(string? rawDescription)
    {
        return TextComposer.CollapseToSingleLine(rawDescription ?? string.Empty).Trim();
    }

    private static string ReadOptionalString(JsonNode? node)
    {
        return node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text ?? string.Empty
            : string.Empty;
    }

    internal static List<string> NormalizePreviousSoulNames(JsonNode? previousSoulNamesNode, string currentSoulName)
    {
        var result = new List<string>();
        if (previousSoulNamesNode is not JsonArray previousSoulNames)
            return result;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in previousSoulNames)
        {
            var normalized = NormalizeSoulName(entry?.GetValue<string?>());
            if (string.IsNullOrWhiteSpace(normalized))
                continue;
            if (string.Equals(normalized, currentSoulName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!seen.Add(normalized))
                continue;
            result.Add(normalized);
        }

        return result;
    }

    private static JsonArray CreateJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
            array.Add(value);
        return array;
    }

    private async Task SyncPendingGuardianCreationSoulNameAsync(string newSoulName)
    {
        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return;

        try
        {
            if (JsonNode.Parse(guardiansJson) is not JsonObject guardiansRoot ||
                guardiansRoot["pendingGuardianCreation"] is not JsonObject pendingGuardianCreation)
                return;

            pendingGuardianCreation["soulName"] = newSoulName;
            await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", guardiansRoot.ToJsonString(JsonWriteOptions));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to sync pendingGuardianCreation soul name after local rename.");
        }
    }
}

public sealed record SoulRenameResult(
    bool Success,
    bool Changed,
    string CurrentSoulName,
    IReadOnlyList<string> PreviousSoulNames,
    string? ErrorMessage = null);

public sealed record SoulFormDescriptionUpdateResult(
    bool Success,
    bool Changed,
    string CurrentSoulFormDescription,
    string? ErrorMessage = null);
