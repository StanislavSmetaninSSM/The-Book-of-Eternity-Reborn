using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ProgressionScheduleService
{
    // Returns existing common-publication calculation DATA, never a separate
    // commit permit. The original control/report obligations remain unchanged;
    // newly elapsed cycles become ordinary pending work at the verified T1.
    internal async Task<FrozenMortalCalculation> PrepareDiagnosisOutcomeAsync(
        FileSystemManager.CanonicalWriteLease lease, MortalWoundDiagnosisWorkingProjection working)
    {
        working.RequireOperation(_fs, lease);
        var sources = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in MortalJointCalculationSourcePaths)
            sources.Add(path, await _fs.ReadFileBytesAsync(lease, path));
        var scheduleBytes = sources[SchedulePath] ?? throw new InvalidDataException("Diagnosis cannot initialize a missing progression original.");
        var clockBytes = sources[EffectAcceptedTurnInputComposer.WorldTimePath] ?? throw new InvalidDataException("Diagnosis has no current original clock.");
        if (!scheduleBytes.AsSpan().SequenceEqual(working.ReadOriginalBytes(SchedulePath)) ||
            !clockBytes.AsSpan().SequenceEqual(working.ReadOriginalBytes(EffectAcceptedTurnInputComposer.WorldTimePath)))
            throw new InvalidDataException("A competing schedule or timeChange/setWorldTime cannot replace diagnosis original time.");
        var realm = ResolveFrozenRealm(sources["game_state/meta/soul_state.json"]);
        var context = ReadFrozenRequestContext(sources);
        var coordinates = working.Coordinates;
        if (realm != "Mortal World" || context is not { } selected || selected.SessionId != coordinates.SessionId ||
            selected.RequestId != coordinates.RequestId || selected.TurnNumber != coordinates.Turn)
            throw new InvalidDataException("The diagnosis progression report/request context belongs to another realm or turn.");
        var calculation = ReconstructDiagnosisOutcomeData(working.OriginalControl, scheduleBytes, clockBytes, sources[ReportPath],
            coordinates.SessionId, coordinates.RequestId, coordinates.Turn,
            checked(working.ResultingMinute - working.OriginalMinute), DateTime.UtcNow.ToString("o"));
        working.RequireOperation(_fs, lease);
        return calculation;
    }

    // Historical comparison DATA only. The actual caller above proves ownership
    // and physical originals; this pure reconstruction cannot grant a mutation.
    internal static FrozenMortalCalculation ReconstructDiagnosisOutcomeData(JsonObject originalControl,
        byte[] scheduleBytes, byte[] clockBytes, byte[]? reportBytes, string sessionId, string requestId, int turn,
        long admittedDuration, string preparedAtUtc)
    {
        if (admittedDuration < 0) throw new InvalidDataException("Diagnosis duration is not an explicit nonnegative method value.");
        RequireCapturedUtc(preparedAtUtc);
        var control = originalControl.Deserialize<ProgressionControl>(JsonOpts) ?? throw new InvalidDataException("Missing original diagnosis control.");
        if (!JsonNode.DeepEquals(originalControl, JsonSerializer.SerializeToNode(control, JsonOpts)) || control.CurrentRealm != "Mortal World")
            throw new InvalidDataException("Diagnosis original control is not its complete exact Mortal representation.");
        var schedule = JsonSerializer.Deserialize<ProgressionScheduleState>(CanonicalJsonUtf8.DecodeOneOptionalBom(scheduleBytes), JsonOpts)
            ?? throw new InvalidDataException("Missing original diagnosis schedule.");
        var clock = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(clockBytes))?.AsObject()
            ?? throw new InvalidDataException("Missing original diagnosis clock.");
        if (clock["currentTimeInMinutes"] is not JsonValue value || !value.TryGetValue<long>(out var originalMinute) ||
            originalMinute < 0 || originalMinute != control.CurrentWorldTimeInMinutes || clock.ContainsKey("timeChange") || clock.ContainsKey("setWorldTime"))
            throw new InvalidDataException("Diagnosis requires its settled numeric original clock, with no competing time command.");
        var initialSchedule = JsonSerializer.SerializeToNode(schedule, JsonOpts);
        SanitizeFrozenSchedule(schedule, "Mortal World", clockBytes, null);
        if (!JsonNode.DeepEquals(initialSchedule, JsonSerializer.SerializeToNode(schedule, JsonOpts)) ||
            schedule.CurrentRealm != "Mortal World" || schedule.CurrentWorldTimeInMinutes != originalMinute ||
            schedule.WorldCycleMinutes <= 0 || schedule.FactionCycleMinutes <= 0 ||
            schedule.LastWorldSimulationTimeInMinutes > originalMinute || schedule.LastFactionSimulationTimeInMinutes > originalMinute)
            throw new InvalidDataException("The diagnosis original schedule requires settled canonical preparation.");
        var expectedControl = BuildProgressionControl(schedule,
            ComputeElapsedCycles(checked((int)originalMinute), schedule.LastWorldSimulationTimeInMinutes, schedule.WorldCycleMinutes),
            ComputeElapsedCycles(checked((int)originalMinute), schedule.LastFactionSimulationTimeInMinutes, schedule.FactionCycleMinutes),
            new AfterlifeCatchupContext(false, 0, "none", 0, Array.Empty<string>()));
        if (!JsonNode.DeepEquals(originalControl, JsonSerializer.SerializeToNode(expectedControl, JsonOpts)))
            throw new InvalidDataException("Full signed diagnosis control does not reconstruct from the original schedule and T0.");
        var future = checked((int)checked(originalMinute + admittedDuration));
        var report = ReadFrozenReport(reportBytes);
        if (report.FilePresent && report.State != ProgressionFileReadState.Valid)
            throw new InvalidDataException("An explicitly supplied diagnosis progression report must be valid.");
        var context = new PendingTurnRequestContext(sessionId, requestId, turn);
        var issues = new List<ValidationIssue>();
        ValidateMortalOutcomeData(control, report, context, future, issues);
        if (issues.Any(row => row.Severity == IssueSeverity.Error) ||
            report.Report?.NewLastWorldSimulationTimeInMinutes > future || report.Report?.NewLastFactionSimulationTimeInMinutes > future)
            throw new InvalidDataException("Diagnosis progression report contradicts its original obligations or verified T1: " +
                string.Join(";", issues.Select(row => row.Code)));
        var consumed = ApplyMortalOutcomeFromInputs(schedule, control, report.Report,
            report.State == ProgressionFileReadState.Valid && ProgressionReportMatchesCurrentTurn(report.Report, context),
            new WorldTimeResolutionResult(future, false));
        schedule.CurrentRealm = "Mortal World"; schedule.LastUpdatedUtc = preparedAtUtc;
        var after = EncodeUtf8WithPreamble(JsonSerializer.Serialize(schedule, JsonOpts));
        var reportAfter = consumed && report.FilePresent ? null : reportBytes;
        return new(new(true, scheduleBytes), new(reportBytes is not null, reportBytes), new(true, after),
            new(reportAfter is not null, reportAfter), ComputeAcceptedOutcomeMutationFingerprint(JsonSerializer.Serialize(control, JsonOpts),
                new(true, scheduleBytes), new(reportBytes is not null, reportBytes), new(true, after), new(reportAfter is not null, reportAfter)));
    }
}
