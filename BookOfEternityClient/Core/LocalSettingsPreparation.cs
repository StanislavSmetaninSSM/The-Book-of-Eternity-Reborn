using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

internal sealed class LocalSettingsBaselineChangedException()
    : IOException("The config changed since this settings draft was opened; reload before saving.") { }

internal sealed record LocalSettingsBaseline(string Generation, byte[] ConfigBytes);
internal sealed record PreparedLocalSettings(GameSettings Settings, IReadOnlyList<CanonicalLocalFileChange> Changes);

/// <summary>Prepares the complete settings/projection/mod-manifest set without publishing or changing runtime settings.</summary>
internal sealed class LocalSettingsPreparation
{
    internal const string ProjectionPath = "game_state/core/game_settings.json";
    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;
    private readonly FileSystemManager _files;
    private readonly StateManager _state;
    private readonly SystemModService _mods;

    internal LocalSettingsPreparation(FileSystemManager files, StateManager state, SystemModService mods)
    {
        _files = files;
        _state = state;
        _mods = mods;
    }

    internal async Task<PreparedLocalSettings> PrepareAsync(FileSystemManager.CanonicalWriteLease lease,
        LocalSettingsBaseline baseline, GameSettings requested)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(baseline.ConfigBytes);
        ArgumentNullException.ThrowIfNull(requested);
        if (string.IsNullOrWhiteSpace(baseline.Generation))
            throw new ArgumentException("Settings preparation requires an existing generation.", nameof(baseline));
        _files.EnsureCanonicalWriteLeaseActive(lease);
        VerifyGeneration(lease, baseline.Generation);

        var snapshot = await _state.ReadLocalSettingsAsync(lease);
        if (!snapshot.Bytes.AsSpan().SequenceEqual(baseline.ConfigBytes))
            throw new LocalSettingsBaselineChangedException();

        // Clone the complete draft, including nested worker/profile collections.
        // Later normalization must not mutate the menu draft or live references.
        var candidate = JsonSerializer.Deserialize<GameSettings>(JsonSerializer.Serialize(requested, JsonOpts), JsonOpts)
            ?? throw new InvalidDataException("The requested settings are not a settings object.");
        var projectionBefore = await _files.ReadLocalFileBytesAsync(lease, ProjectionPath);
        var manifestBefore = await _files.ReadLocalFileBytesAsync(lease, SystemModService.ManifestPath);
        var manifest = await _mods.PrepareManifestForGmAsync(lease, candidate.EnabledSystemMods, manifestBefore);
        candidate.EnabledSystemMods = manifest.EnabledFiles.ToList();

        var projection = JsonSerializer.SerializeToNode(new
        {
            hardMode = candidate.Difficulty == "hard",
            impossibleMode = candidate.Difficulty == "impossible",
            difficulty = candidate.Difficulty,
            qteEventsEnabled = candidate.EnableQteEvents,
            enabledSystemMods = manifest.ActiveMods.Select(mod => new { mod.FileName, mod.ModId, mod.Name }).ToArray()
        }, JsonOpts)!.AsObject();
        byte[] projectionAfter;
        if (ProjectionMatches(projectionBefore, projection))
            projectionAfter = projectionBefore!;
        else
        {
            projection["_lastUpdated"] = DateTime.UtcNow.ToString("o");
            projectionAfter = EncodeText(projection.ToJsonString(JsonOpts));
        }

        var encodedCandidate = _state.EncodeLocalSettings(candidate);
        var configAfter = encodedCandidate.AsSpan().SequenceEqual(_state.EncodeLocalSettings(snapshot.Settings))
            ? snapshot.Bytes : encodedCandidate;
        VerifyGeneration(lease, baseline.Generation);
        return new(candidate,
        [
            new("config.json", snapshot.Bytes, configAfter),
            new(ProjectionPath, projectionBefore, projectionAfter),
            new(SystemModService.ManifestPath, manifestBefore, manifest.Bytes)
        ]);
    }

    private void VerifyGeneration(FileSystemManager.CanonicalWriteLease lease, string expected)
    {
        _files.VerifyCurrentSessionOperation(lease);
        var actual = _files.ReadExistingSessionGeneration(lease);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new SessionReplacedException("The session changed since this settings draft was opened.", expected, actual);
    }

    private static bool ProjectionMatches(byte[]? before, JsonObject expected)
    {
        if (before == null) return false;
        try
        {
            if (JsonNode.Parse(DecodeText(before)) is not JsonObject current) return false;
            current.Remove("_lastUpdated");
            return JsonNode.DeepEquals(current, expected);
        }
        catch (JsonException) { return false; }
        catch (ArgumentException) { return false; }
    }

    internal static string DecodeText(byte[] bytes)
    {
        using var reader = new StreamReader(new MemoryStream(bytes, writable: false), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    internal static byte[] EncodeText(string text) => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();
}
