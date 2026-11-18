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
    internal string SnapshotToken => _snapshotToken;
    /// <summary>Returns an independent copy of captured opening DATA for a selected path.</summary>
    internal byte[]? ReadOpeningBytes(string path) => _opening.TryGetValue(path, out var bytes)
        ? bytes?.ToArray() : throw new ArgumentException("Path was not captured by this source.", nameof(path));

    /// <summary>Detached exact guard DATA for an enclosing owner's later fresh comparison, never a reusable lease capability.</summary>
    internal IReadOnlyDictionary<string, byte[]?> ReadOpeningGuardData() => GuardPaths.ToDictionary(
        path => path, path => _opening[path]?.ToArray(), StringComparer.Ordinal);
    internal IReadOnlyDictionary<string, byte[]?> ReadOpeningData() => _opening.ToDictionary(
        pair => pair.Key, pair => pair.Value?.ToArray(), StringComparer.Ordinal);

    internal JsonObject? ReadOriginalDocument(string path) => _original.TryGetValue(path, out var bytes)
        ? Root(bytes, path) : throw new ArgumentException("Path was not captured by this source.", nameof(path));

    internal static Task<MortalLivingWorldPreparation> PrepareAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease) => PrepareCoreAsync(files, lease, null, null, null);

    internal static Task<MortalLivingWorldPreparation> PrepareForDiagnosisAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, MortalWoundTreatmentAcceptedStateAuthority owner,
        MortalWoundDiagnosisWorkingProjection working, ProgressionScheduleService.FrozenMortalCalculation progression)
    {
        working.RequireOperation(files, lease);
        working.RequireCurrent(owner);
        return PrepareCoreAsync(files, lease, owner, working, progression);
    }

    private static async Task<MortalLivingWorldPreparation> PrepareCoreAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, MortalWoundTreatmentAcceptedStateAuthority? diagnosisOwner,
        MortalWoundDiagnosisWorkingProjection? diagnosisWorking, ProgressionScheduleService.FrozenMortalCalculation? diagnosisProgression)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        files.VerifyCurrentSessionOperation(lease);
        try
        {
            // Authority capture must not borrow provisional working bytes from an ambient DATA owner.
            using var fresh = FileSystemManager.SuppressValidationReuse();
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
            var request = Root(opening["input/turn_request.json"], "input/turn_request.json");
            RequireMortalSoul(originals["game_state/meta/soul_state.json"]);
            RequireMortalSoul(opening["game_state/meta/soul_state.json"]);
            if (!RealmSemantics.IsMortalRealm(controlRoot["currentRealm"]?.GetValue<string>()) ||
                !JsonNode.DeepEquals(controlRoot, request?["progressionControl"]))
                return Refuse("source_invalid", "Current progression control differs from the signed Mortal control.");
            var control = controlRoot.Deserialize<ProgressionControl>() ?? throw new InvalidDataException("Malformed signed progression control.");
            var baseline = Root(originals[WorldTimePath], WorldTimePath)
                ?? throw new InvalidDataException("Missing original clock.");
            var command = Root(opening[WorldTimePath], WorldTimePath)
                ?? throw new InvalidDataException("Missing current clock.");
            if (baseline.ContainsKey("timeChange") || baseline.ContainsKey("setWorldTime") ||
                baseline["currentTimeInMinutes"] is not JsonValue originalTime ||
                !originalTime.TryGetValue<int>(out var originalMinute) || originalMinute < 0 ||
                originalMinute != control.CurrentWorldTimeInMinutes)
                return Refuse("source_invalid", "Original clock must be canonical numeric time matching its signed control.");
            var originalClock = ProgressionScheduleService.ResolveFrozenWorldTime(originals[WorldTimePath],
                control.CurrentWorldTimeInMinutes, allowIncrementalTimeChange: false);
            var currentClock = ProgressionScheduleService.ResolveFrozenWorldTime(opening[WorldTimePath], control.CurrentWorldTimeInMinutes);
            var resultingMinute = currentClock.Minutes;
            if (command.ContainsKey("timeChange"))
            {
                // Same retained-baseline command contract as the ordinary clock owner, not absolute-first resolution.
                if (command["timeChange"] is not JsonValue delta || !delta.TryGetValue<int>(out var elapsed) ||
                    elapsed < 0 || command.ContainsKey("setWorldTime"))
                    return Refuse("clock_unresolved", "A clock delta requires a nonnegative integer without an absolute override.");
                foreach (var field in new[] { "year", "monthName", "dayOfMonth", "timeOfDay", "currentTimeInMinutes" })
                    if (command.ContainsKey(field) && !JsonNode.DeepEquals(command[field], baseline[field]))
                        return Refuse("clock_unresolved", "Clock delta has a competing absolute field: " + field);
                resultingMinute = checked(originalMinute + elapsed);
            }
            if (originalClock.HasUnresolvedAbsoluteOverride || currentClock.HasUnresolvedAbsoluteOverride ||
                originalClock.Minutes != control.CurrentWorldTimeInMinutes)
                return Refuse("clock_unresolved", "Provide comparable numeric original and resulting Mortal clock values; calendar prose is not elapsed-time evidence.");
            var schedule = Root(originals[ProgressionScheduleService.SchedulePath], ProgressionScheduleService.SchedulePath)!;
            if (!RealmSemantics.IsMortalRealm(schedule["currentRealm"]?.GetValue<string>()) ||
                !ExactNumber(schedule, "currentWorldTimeInMinutes", control.CurrentWorldTimeInMinutes) ||
                !ExactNumber(schedule, "lastWorldSimulationTimeInMinutes", control.LastWorldSimulationTimeInMinutes) ||
                !ExactNumber(schedule, "lastFactionSimulationTimeInMinutes", control.LastFactionSimulationTimeInMinutes) ||
                !ExactNumber(schedule, "worldCycleMinutes", control.WorldCycleMinutes) ||
                !ExactNumber(schedule, "factionCycleMinutes", control.FactionCycleMinutes))
                return Refuse("source_invalid", "Signed progression control and original schedule disagree.");
            if (diagnosisWorking is not null)
            {
                diagnosisWorking.RequireOperation(files, lease);
                diagnosisWorking.RequireCurrent(diagnosisOwner!);
                var planned = diagnosisProgression ?? throw new InvalidDataException("Missing actual diagnosis progression.");
                var after = Root(planned.ScheduleAfter.Bytes, ProgressionScheduleService.SchedulePath)!;
                var preparedAt = after["lastUpdatedUtc"]?.GetValue<string>() ?? throw new InvalidDataException("Missing progression preparation time.");
                var expected = ProgressionScheduleService.ReconstructDiagnosisOutcomeData(diagnosisWorking.OriginalControl,
                    diagnosisWorking.ReadOriginalBytes(ProgressionScheduleService.SchedulePath),
                    diagnosisWorking.ReadOriginalBytes(WorldTimePath), planned.ReportBefore.Bytes,
                    original.SessionId, original.RequestId, original.TurnNumber,
                    checked(diagnosisWorking.ResultingMinute - diagnosisWorking.OriginalMinute), preparedAt);
                if (diagnosisOwner!.Binding.SessionId != original.SessionId || diagnosisOwner.Binding.RequestId != original.RequestId ||
                    diagnosisOwner.Binding.Turn != original.TurnNumber || diagnosisWorking.OriginalMinute != originalMinute ||
                    !JsonNode.DeepEquals(diagnosisWorking.OriginalControl, controlRoot) ||
                    !Same(diagnosisWorking.ReadOriginalBytes(WorldTimePath), originals[WorldTimePath]) ||
                    !Same(diagnosisWorking.ReadOriginalBytes(ProgressionScheduleService.SchedulePath), originals[ProgressionScheduleService.SchedulePath]) ||
                    planned.MutationFingerprint != expected.MutationFingerprint ||
                    !Same(planned.ScheduleBefore.Bytes, expected.ScheduleBefore.Bytes) || !Same(planned.ScheduleAfter.Bytes, expected.ScheduleAfter.Bytes) ||
                    !Same(planned.ReportBefore.Bytes, expected.ReportBefore.Bytes) || !Same(planned.ReportAfter.Bytes, expected.ReportAfter.Bytes))
                    return Refuse("source_invalid", "Diagnosis assessment requires its exact genuine owner, originals and reconstructed progression.");
                resultingMinute = checked((int)diagnosisWorking.ResultingMinute);
            }
            var context = new MortalLivingWorldAssessmentContext(original.SessionId, original.RequestId, original.TurnNumber,
                originalClock.Minutes, resultingMinute, control.LastWorldSimulationTimeInMinutes,
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
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or InvalidDataException or OverflowException)
        { return Refuse("source_invalid", "Current authenticated living-world source cannot be established: " + error.Message); }
    }

    /// <summary>Rechecks both original authority and every opening input before any owner mutation.</summary>
    internal async Task RequireUnchangedInputsAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        using var fresh = FileSystemManager.SuppressValidationReuse();
        await RequireOriginalCurrentAsync(files, lease);
        foreach (var pair in _opening)
            if (!Same(pair.Value, await files.ReadFileBytesAsync(lease, pair.Key)))
                throw new InvalidDataException("Living-world opening input changed: " + pair.Key);
    }

    /// <summary>Rechecks original authority and protected guards, not legitimate working-domain or owner output changes.</summary>
    internal async Task RequireOriginalCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        using var fresh = FileSystemManager.SuppressValidationReuse();
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
        foreach (var path in GuardPaths)
            if (!Same(_opening[path], await files.ReadFileBytesAsync(lease, path)))
                throw new InvalidDataException("Living-world guard changed: " + path);
        RequireMortalSoul(await files.ReadFileBytesAsync(lease, "game_state/meta/soul_state.json"));
    }

    private static void RequireMortalSoul(byte[]? bytes)
    {
        var soul = Root(bytes, "game_state/meta/soul_state.json");
        if (soul?["currentRealm"] is not JsonValue realm || !realm.TryGetValue<string>(out var value) ||
            !RealmSemantics.IsMortalRealm(value))
            throw new InvalidDataException("Living-world source requires a canonical Mortal soul.");
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
