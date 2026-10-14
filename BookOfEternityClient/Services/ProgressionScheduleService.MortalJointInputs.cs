using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ProgressionScheduleService
{
    private static readonly object FrozenMortalInputsIssuer = new();
    internal static readonly IReadOnlyList<string> MortalJointCalculationSourcePaths = Array.AsReadOnly(new[]
    { SchedulePath, ReportPath, "game_state/meta/soul_state.json", "game_state/world/world_time.json",
        "input/turn_request.json", "ready/turn_complete.json", "game_state/control/validation_repair_request.json" });

    // Detached calculation evidence only; no accepted phase/mutation capability
    // can be deserialized or issued by this value. Raw-input recomposition checks
    // the calculation; the common owner separately authenticates engine sources.
    internal sealed class FrozenMortalOutcomeInputs
    {
        private readonly FileSystemManager _files;
        private readonly FileSystemManager.CanonicalWriteLease _lease;
        private readonly string _generation;
        private readonly Dictionary<string, byte[]?> _sources;
        private readonly string _body;
        private readonly ProgressionScheduleService _owner;
        internal FrozenMortalOutcomeInputs(object issuer, ProgressionScheduleService owner,
            FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            IReadOnlyDictionary<string, byte[]?> sources, JsonObject body)
        {
            if (!ReferenceEquals(issuer, FrozenMortalInputsIssuer)) throw new InvalidOperationException("Frozen progression inputs require the actual source reader.");
            _owner = owner; _files = files; _lease = lease; _generation = files.ReadLocalGenerationSnapshot(lease).Binding.Id;
            _sources = sources.ToDictionary(row => row.Key, row => row.Value?.ToArray(), StringComparer.Ordinal);
            _body = body.ToJsonString(JsonOpts);
            _owner.RecomposeFrozenMortalCalculation(body);
            Fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[] {
                "book_of_eternity.mortal_joint_progression.frozen_basis.v1", _generation,
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(body) });
        }
        internal string Fingerprint { get; }
        internal JsonObject ToDiagnosticJson() => JsonNode.Parse(_body)!.AsObject();
        internal byte[]? ReadSourceBytes(string path) => _sources[path]?.ToArray();
        internal IReadOnlyDictionary<string, CanonicalBeforeImage> BeforeImages => _sources.ToDictionary(
            row => row.Key, row => new CanonicalBeforeImage(row.Value is not null, row.Value?.ToArray()), StringComparer.Ordinal);
        internal async Task ValidateCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            files.EnsureCanonicalWriteLeaseActive(lease);
            if (!ReferenceEquals(files, _files) || !ReferenceEquals(lease, _lease) || !files.IsCurrentSessionGeneration(lease, _generation))
                throw new InvalidDataException("Frozen progression capture cannot cross physical owner, generation or lease.");
            _owner.RecomposeFrozenMortalCalculation(ToDiagnosticJson());
            foreach (var (path, expected) in _sources)
            {
                var actual = await files.ReadFileBytesAsync(lease, path);
                if (expected is null ? actual is not null : actual is null || !expected.AsSpan().SequenceEqual(actual))
                    throw new InvalidDataException("Progression calculation source changed: " + path);
            }
        }
    }

    internal async Task<FrozenMortalOutcomeInputs> CaptureMortalOutcomeInputsAsync(
        FileSystemManager.CanonicalWriteLease lease, ProgressionControl actualControl,
        string sessionId, string requestId, int turn)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        if (!HasResolvedRealm(actualControl.CurrentRealm) || IsAfterlifeRealm(actualControl.CurrentRealm))
            throw new InvalidDataException("Joint progression capture requires the actual Mortal original control.");
        var controlJson = JsonSerializer.Serialize(actualControl, JsonOpts);
        var control = JsonSerializer.Deserialize<ProgressionControl>(controlJson, JsonOpts)!;
        var sources = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in MortalJointCalculationSourcePaths) sources.Add(path, await _fs.ReadFileBytesAsync(lease, path));
        var scheduleSnapshot = await ReadScheduleSnapshotAsync(lease);
        if (sources[SchedulePath] is null || scheduleSnapshot.State != ProgressionFileReadState.Valid || scheduleSnapshot.Schedule is null)
            throw new InvalidDataException("Joint progression requires an existing valid schedule; no initialization fallback.");
        // Freeze the actual asynchronous sanitization result and all its raw
        // sources. Its conditional UTC fallback remains at its old source site.
        var needsSanitizationUtc = scheduleSnapshot.Schedule.LastUpdatedUtc is null;
        var schedule = await SanitizeScheduleAsync(scheduleSnapshot.Schedule, scheduleSnapshot.Schedule.CurrentRealm, lease);
        var sanitizationUtc = needsSanitizationUtc ? schedule.LastUpdatedUtc : null;
        var sanitizedJson = JsonSerializer.Serialize(schedule, JsonOpts);
        var realm = await ResolveCurrentRealmAsync(string.Empty, lease);
        var reportSnapshot = await ReadProcessingReportSnapshotAsync(lease);
        var context = await ReadCurrentTurnRequestContextAsync(lease);
        if (context is not { } current || current.SessionId != sessionId || current.RequestId != requestId || current.TurnNumber != turn)
            throw new InvalidDataException("Actual progression request-context selection does not match the original.");
        var world = await ResolveWorldTimeFromFileAsync(control.CurrentWorldTimeInMinutes, writeLease: lease);
        var validation = new List<ValidationIssue>();
        if (!world.HasUnresolvedAbsoluteOverride)
            ValidateMortalOutcome(control, reportSnapshot, context, world.Minutes, validation);
        if (validation.Any(row => row.Severity == IssueSeverity.Error))
            throw new InvalidDataException("Actual submitted progression report refused: " + string.Join(";", validation.Select(row => row.Code)));
        var consumed = ApplyMortalOutcomeFromInputs(schedule, control, reportSnapshot.Report,
            reportSnapshot.State == ProgressionFileReadState.Valid && ProgressionReportMatchesCurrentTurn(reportSnapshot.Report, context), world);
        if (HasResolvedRealm(realm)) schedule.CurrentRealm = realm;
        // Same logical placement as Prepare: after source reads, arithmetic and
        // realm assignment, before serialization/encoding/fingerprint generation.
        var preparedAtUtc = DateTime.UtcNow.ToString("o");
        schedule.LastUpdatedUtc = preparedAtUtc;
        var scheduleAfterJson = JsonSerializer.Serialize(schedule, JsonOpts);
        var reportAfter = consumed && reportSnapshot.FilePresent ? null : sources[ReportPath];
        var scheduleAfter = EncodeUtf8WithPreamble(scheduleAfterJson);
        var mutationFingerprint = ComputeAcceptedOutcomeMutationFingerprint(controlJson,
            new(true, sources[SchedulePath]), new(sources[ReportPath] is not null, sources[ReportPath]),
            new(true, scheduleAfter), new(reportAfter is not null, reportAfter));
        var body = new JsonObject
        {
            ["version"] = 1, ["control"] = JsonNode.Parse(controlJson), ["sanitizedSchedule"] = JsonNode.Parse(sanitizedJson),
            ["realmAfterTurn"] = realm, ["reportState"] = reportSnapshot.State.ToString(), ["reportPresent"] = reportSnapshot.FilePresent,
            ["parsedReport"] = reportSnapshot.Report is null ? null : JsonSerializer.SerializeToNode(reportSnapshot.Report, JsonOpts),
            ["requestContext"] = new JsonObject { ["sessionId"] = current.SessionId, ["requestId"] = current.RequestId, ["turn"] = current.TurnNumber },
            ["worldMinutes"] = world.Minutes, ["worldUnresolvedAbsolute"] = world.HasUnresolvedAbsoluteOverride,
            ["sanitizationUtc"] = sanitizationUtc,
            ["preparedAtUtc"] = preparedAtUtc, ["scheduleAfterBase64"] = Convert.ToBase64String(scheduleAfter),
            ["reportAfterBase64"] = reportAfter is null ? null : Convert.ToBase64String(reportAfter), ["mutationFingerprint"] = mutationFingerprint
        };
        var raw = new JsonObject();
        foreach (var (path, bytes) in sources) raw[path] = bytes is null ? null : Convert.ToBase64String(bytes);
        body["sourceBytes"] = raw;
        var capture = new FrozenMortalOutcomeInputs(FrozenMortalInputsIssuer, this, _fs, lease, sources, body);
        await capture.ValidateCurrentAsync(_fs, lease);
        return capture;
    }

    // Pure calculation only. The old caller still performs its actual async
    // source reads and captures its final UTC at the original statement below.
    // This helper grants no mutation, publication or cold phase authority.
    private static bool ApplyMortalOutcomeFromInputs(ProgressionScheduleState schedule,
        ProgressionControl control, ProgressionProcessingReport? report, bool reportMatches,
        WorldTimeResolutionResult worldTimeResolution)
    {
        var reportConsumed = false;
        var resultingWorldTime = worldTimeResolution.Minutes;
        if (worldTimeResolution.HasUnresolvedAbsoluteOverride)
        {
            resultingWorldTime = Math.Max(
                resultingWorldTime,
                Math.Max(
                    report?.NewLastWorldSimulationTimeInMinutes ?? control.LastWorldSimulationTimeInMinutes,
                    report?.NewLastFactionSimulationTimeInMinutes ?? control.LastFactionSimulationTimeInMinutes));
        }

        schedule.CurrentWorldTimeInMinutes = resultingWorldTime;

        if (reportMatches)
        {
            if ((report?.WorldCyclesProcessed ?? 0) > 0)
            {
                schedule.LastWorldSimulationTimeInMinutes =
                    report?.NewLastWorldSimulationTimeInMinutes
                    ?? (control.LastWorldSimulationTimeInMinutes + (report?.WorldCyclesProcessed ?? 0) * control.WorldCycleMinutes);
            }

            if ((report?.FactionCyclesProcessed ?? 0) > 0)
            {
                schedule.LastFactionSimulationTimeInMinutes =
                    report?.NewLastFactionSimulationTimeInMinutes
                    ?? (control.LastFactionSimulationTimeInMinutes + (report?.FactionCyclesProcessed ?? 0) * control.FactionCycleMinutes);
            }

            schedule.PendingWorldCycles = 0;
            schedule.PendingFactionCycles = 0;
            ClearAfterlifePendingCycles(schedule);
            reportConsumed = true;
        }
        else
        {
            schedule.PendingWorldCycles = Math.Max(
                schedule.PendingWorldCycles,
                Math.Max(0, control.WorldCyclesAlreadyPendingBeforeTurn));
            schedule.PendingFactionCycles = Math.Max(
                schedule.PendingFactionCycles,
                Math.Max(0, control.FactionCyclesAlreadyPendingBeforeTurn));
            ClearAfterlifePendingCycles(schedule);
        }
        return reportConsumed;
    }
}
