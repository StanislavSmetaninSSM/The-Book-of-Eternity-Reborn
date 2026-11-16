using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal sealed record MortalLivingWorldPreparation(
    IReadOnlyList<ValidationIssue> Issues, MortalLivingWorldSource? Source);

/// <summary>Lease-local capture issued only by the actual authenticated original reader; not serializable permission.</summary>
internal sealed class MortalLivingWorldSource
{
    internal const string ReportPath = "game_state/control/mortal_living_world_report.json";
    internal const string HistoryPath = "game_state/world/mortal_living_world_history.json";
    internal const string WorldTimePath = "game_state/world/world_time.json";
    internal static IReadOnlyList<string> DomainPaths { get; } = Array.AsReadOnly(new[]
    {
        "game_state/npcs/npc_core.json", "game_state/factions/faction_core.json",
        "game_state/factions/faction_projects.json", "game_state/world/world_map.json",
        "game_state/world/world_flags.json", "game_state/world/world_events.json"
    });
    internal static IReadOnlyList<string> ObservedPaths { get; } = Array.AsReadOnly(DomainPaths
        .Concat([ReportPath, HistoryPath, WorldTimePath, ProgressionScheduleService.SchedulePath]).ToArray());
    private static readonly string[] RequiredPaths =
        ["game_state/meta/soul_state.json", WorldTimePath, ProgressionScheduleService.SchedulePath];
    private static readonly string[] GuardPaths =
    [LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
        "input/turn_request.json", "ready/turn_complete.json", "game_state/control/validation_repair_request.json"];
    private readonly FileSystemManager _files;
    private readonly FileSystemManager.CanonicalWriteLease _lease;
    private readonly string _generation;
    private readonly string _snapshotToken;
    private readonly Dictionary<string, byte[]?> _original;
    private readonly Dictionary<string, byte[]?> _opening;
    private readonly PendingTurnSnapshotPathSelection _selection;

    private MortalLivingWorldSource(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        PendingTurnSnapshotReadAuthority original, Dictionary<string, byte[]?> originals,
        Dictionary<string, byte[]?> opening, MortalLivingWorldAssessmentContext context,
        PendingTurnSnapshotPathSelection selection)
    {
        _files = files; _lease = lease; _generation = files.ReadLocalGenerationSnapshot(lease).Binding.Id;
        _snapshotToken = original.SnapshotToken; _original = originals; _opening = opening;
        Context = context; _selection = selection;
    }

    internal MortalLivingWorldAssessmentContext Context { get; }
    /// <summary>Detached original binding DATA, never publication authority.</summary>
    internal string SnapshotToken => throw new NotImplementedException("#1516 source split API RED scaffold");
    /// <summary>Returns an independent copy of captured opening DATA for a selected path.</summary>
    internal byte[]? ReadOpeningBytes(string path) => throw new NotImplementedException("#1516 source split API RED scaffold");
    /// <summary>Rechecks original authority and protected guards while allowing working domain normalization.</summary>
    internal Task RequireOriginalCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease) =>
        throw new NotImplementedException("#1516 source split API RED scaffold");

    internal JsonObject? ReadOriginalDocument(string path) => _original.TryGetValue(path, out var bytes)
        ? Root(bytes, path) : throw new ArgumentException("Path was not captured by this source.", nameof(path));

    internal static async Task<MortalLivingWorldPreparation> PrepareAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        files.VerifyCurrentSessionOperation(lease);
        try
        {
            var selection = PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(RequiredPaths,
                DomainPaths.Concat([ReportPath, HistoryPath]));
            var read = PendingTurnSnapshotReader.ReadCurrent(files, lease, selection);
            if (!read.Success || read.Snapshot is not { } original)
                return new(read.Issues, null);
            if (original.Realm != "mortal_world")
                return Refuse("source_invalid", "The assessment requires an authenticated Mortal original.");
            var originals = RequiredPaths.Concat(DomainPaths).Concat([ReportPath, HistoryPath]).Distinct(StringComparer.Ordinal)
                .ToDictionary(path => path, path => original.AbsentLogicalPaths.Contains(path, StringComparer.Ordinal)
                    ? null : original.ReadRequiredBytes(path), StringComparer.Ordinal);
            var opening = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
            foreach (var path in originals.Keys.Concat(GuardPaths).Distinct(StringComparer.Ordinal))
                opening.Add(path, await files.ReadFileBytesAsync(lease, path));
            if (!Same(originals[HistoryPath], opening[HistoryPath]))
                return Refuse("history_mutation", "The client-owned assessment history differs from its authenticated original.");
            if (!Same(originals[ProgressionScheduleService.SchedulePath], opening[ProgressionScheduleService.SchedulePath]))
                return Refuse("source_invalid", "The prepared progression schedule was changed before source capture.");
            var controlRoot = original.ReadOriginalControl() ?? throw new InvalidDataException("Missing signed original progression control.");
            var control = controlRoot.Deserialize<ProgressionControl>() ?? throw new InvalidDataException("Malformed signed progression control.");
            var originalClock = ProgressionScheduleService.ResolveFrozenWorldTime(originals[WorldTimePath],
                control.CurrentWorldTimeInMinutes, allowIncrementalTimeChange: false);
            var currentClock = ProgressionScheduleService.ResolveFrozenWorldTime(opening[WorldTimePath], control.CurrentWorldTimeInMinutes);
            if (originalClock.HasUnresolvedAbsoluteOverride || currentClock.HasUnresolvedAbsoluteOverride ||
                originalClock.Minutes != control.CurrentWorldTimeInMinutes)
                return Refuse("clock_unresolved", "Provide comparable numeric original and resulting Mortal clock values; calendar prose is not elapsed-time evidence.");
            var schedule = Root(originals[ProgressionScheduleService.SchedulePath], ProgressionScheduleService.SchedulePath)!;
            if (!ExactNumber(schedule, "currentWorldTimeInMinutes", control.CurrentWorldTimeInMinutes) ||
                !ExactNumber(schedule, "lastWorldSimulationTimeInMinutes", control.LastWorldSimulationTimeInMinutes) ||
                !ExactNumber(schedule, "lastFactionSimulationTimeInMinutes", control.LastFactionSimulationTimeInMinutes) ||
                !ExactNumber(schedule, "worldCycleMinutes", control.WorldCycleMinutes) ||
                !ExactNumber(schedule, "factionCycleMinutes", control.FactionCycleMinutes))
                return Refuse("source_invalid", "Signed progression control and original schedule disagree.");
            var context = new MortalLivingWorldAssessmentContext(original.SessionId, original.RequestId, original.TurnNumber,
                originalClock.Minutes, currentClock.Minutes, control.LastWorldSimulationTimeInMinutes,
                control.LastFactionSimulationTimeInMinutes, control.WorldCycleMinutes, control.FactionCycleMinutes);
            JsonObject? report;
            try { report = Root(opening[ReportPath], ReportPath); }
            catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or InvalidDataException)
            { return Refuse("report_invalid", "The private report must be a JSON object without duplicate fields."); }
            var issues = MortalLivingWorldAssessment.Validate(context, report);
            if (issues.Count != 0) return new(issues, null);
            // Validate detached originals now; malformed roots never become an empty inferred catalog.
            foreach (var path in DomainPaths) _ = Root(originals[path], path);
            var source = new MortalLivingWorldSource(files, lease, original, originals, opening, context, selection);
            await source.RequireUnchangedInputsAsync(files, lease);
            return new([], source);
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or InvalidDataException)
        { return Refuse("source_invalid", "Current authenticated living-world source cannot be established: " + error.Message); }
    }

    internal async Task RequireUnchangedInputsAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        if (!ReferenceEquals(files, _files) || !ReferenceEquals(lease, _lease))
            throw new InvalidDataException("Living-world source cannot cross physical owner or lease.");
        files.EnsureCanonicalWriteLeaseActive(lease);
        files.VerifyCurrentSessionOperation(lease);
        if (!files.IsCurrentSessionGeneration(lease, _generation))
            throw new InvalidDataException("Living-world source generation changed.");
        // A fresh physical reader, not a caller-provided or cached validation result.
        var read = PendingTurnSnapshotReader.ReadCurrent(files, lease, _selection);
        if (!read.Success || read.Snapshot is not { } original || original.SnapshotToken != _snapshotToken ||
            original.SessionId != Context.SessionId || original.RequestId != Context.RequestId || original.TurnNumber != Context.Turn)
            throw new InvalidDataException("Living-world authenticated original source changed.");
        foreach (var pair in _original)
        {
            var bytes = original.AbsentLogicalPaths.Contains(pair.Key, StringComparer.Ordinal)
                ? null : original.ReadRequiredBytes(pair.Key);
            if (!Same(pair.Value, bytes)) throw new InvalidDataException("Living-world original changed: " + pair.Key);
        }
        foreach (var pair in _opening)
            if (!Same(pair.Value, await files.ReadFileBytesAsync(lease, pair.Key)))
                throw new InvalidDataException("Living-world opening input changed: " + pair.Key);
    }

    private static bool ExactNumber(JsonObject root, string field, int expected) =>
        root[field] is JsonValue value && value.TryGetValue<int>(out var actual) && actual == expected;
    private static bool Same(byte[]? a, byte[]? b) => a is null ? b is null : b is not null && a.AsSpan().SequenceEqual(b);
    private static JsonObject? Root(byte[]? bytes, string path) => bytes is null ? null :
        StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), new JsonSerializerOptions(), path)
            ?? throw new InvalidDataException("Null root: " + path);
    private static MortalLivingWorldPreparation Refuse(string code, string detail) => new(
        [new ValidationIssue(ReportPath, IssueSeverity.Error, detail, code: "mortal_living_world_" + code,
            section: "MortalLivingWorld", repairHint: "Use the current client-authenticated original and exact report; repair the numeric clock or report, never rewrite the original snapshot/history.")], null);
}
