using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>Reads invocation-local canonical location DATA only for explicitly idle inspection; never supplies authoring authority.</summary>
    /// <param name="original">Already validated original lookup, reused without another authentication pass.</param>
    /// <param name="isResponseCreationValidation">Raw responses cannot use the idle inspection exception.</param>
    private IReadOnlySet<string>? ReadIdleInspectionHistoricalLocations(
        MortalActorMaterializationPreTurnAuthority original, bool isResponseCreationValidation)
    {
        if (isResponseCreationValidation ||
            _activeGameStateValidationSelection.Value?.IsReadOnlyInspection != true ||
            original.Status != ValidatedPendingTurnSnapshotStatus.Missing)
            return null;

        foreach (var path in new[]
        {
            LiveTurnPreparationService.TurnRequestPath,
            PendingTurnSnapshotAuthority.AuthorityPath,
            "game_state/control/validation_repair_request.json",
            "game_state/control/validation_repair_ready.json",
            "ready/turn_complete.json", "ready/turn_error.json"
        })
        {
            // Read failures propagate through the established validation pipeline;
            // an empty or malformed existing marker is still active, not absent.
            if (ReadInspectionCurrentText(path) != null)
                return null;
        }

        var json = ReadInspectionCurrentText(MortalLocationMaterializationContract.WorldMapPath);
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var document = JsonDocument.Parse(json);
        if (!HasExactCanonicalWorldMapRoot(document.RootElement, out var locations)) return null;
        var ids = ReadExactCanonicalWorldMapLocationIds(document.RootElement);
        // Do not silently accept a partially invalid or ambiguous map.
        if (ids.Count != locations.GetArrayLength()) return null;
        // The existing parser returns an array (IReadOnlyList), not a set.
        // Build one invocation-local ordinal membership set, reused across all actor rows.
        return ids.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Reads through the existing owner/phase or ordinary reader; only actual absence is null and read failures remain failures.</summary>
    /// <param name="path">Exact canonical dependency; these observations do not grant publication authority.</param>
    private string? ReadInspectionCurrentText(string path) =>
        _sameTurnOwnerInputs is { } inputs ? inputs.ReadText(path) :
        CurrentWorldReadPhase() is { } phase ? phase.ReadTextSync(path) : _fs.ReadFileSync(path);

    /// <summary>Recognizes exact signed retention, or validated idle historical DATA, without changing actor shape or local interaction rules.</summary>
    /// <param name="actor">Current full canonical actor row.</param>
    /// <param name="section">Its current canonical section.</param>
    /// <param name="original">The already authenticated original actor lookup.</param>
    /// <param name="idleLocations">Validated current map membership, available only for eligible idle inspection.</param>
    private static bool IsRetainedHistoricalSceneRow(
        JsonElement actor, string section,
        MortalActorMaterializationPreTurnAuthority original,
        IReadOnlySet<string>? idleLocations)
    {
        if (!string.Equals(section, "NPCsInScene", StringComparison.Ordinal) ||
            !TryReadCanonicalCurrentMortalActorId(actor, out var id) ||
            !actor.TryGetProperty("NPCId", out var identity) ||
            identity.ValueKind != JsonValueKind.String ||
            !string.Equals(identity.GetString(), id, StringComparison.Ordinal))
            return false;

        if (original.Status == ValidatedPendingTurnSnapshotStatus.Usable)
            return original.Actors != null && original.Actors.TryGetValue(id, out var previous) &&
                previous.HistoricalActorJsonBySection.TryGetValue(section, out var json) &&
                JsonValuesSemanticallyEqual(json, actor.GetRawText());

        return original.Status == ValidatedPendingTurnSnapshotStatus.Missing && idleLocations != null &&
            actor.TryGetProperty("currentLocationId", out var location) &&
            location.ValueKind == JsonValueKind.String &&
            idleLocations.Contains(location.GetString()!) &&
            !ActorMaterializationContract.ValidateHistoricalMortalNpc(actor, section)
                .Any(issue => issue.Severity == IssueSeverity.Error);
    }
}
