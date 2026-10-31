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
        var controlNode = working.OriginalControl;
        var control = controlNode.Deserialize<ProgressionControl>(JsonOpts) ?? throw new InvalidDataException("The diagnosis original control is null.");
        var schedule = JsonSerializer.Deserialize<ProgressionScheduleState>(CanonicalJsonUtf8.DecodeOneOptionalBom(scheduleBytes), JsonOpts)
            ?? throw new InvalidDataException("The diagnosis original schedule is null.");
        var originalSchedule = JsonSerializer.SerializeToNode(schedule, JsonOpts);
        var realm = ResolveFrozenRealm(sources["game_state/meta/soul_state.json"]);
        if (realm != "Mortal World" || control.CurrentRealm != realm ||
            control.CurrentWorldTimeInMinutes != working.OriginalMinute)
            throw new InvalidDataException("The current realm/control no longer agrees with original diagnosis time.");
        // Reuse the ordinary pure sanitization but refuse any repair of a signed
        // diagnosis original. Preparation must settle it before the snapshot.
        SanitizeFrozenSchedule(schedule, realm, clockBytes, null);
        if (!JsonNode.DeepEquals(originalSchedule, JsonSerializer.SerializeToNode(schedule, JsonOpts)))
            throw new InvalidDataException("The signed diagnosis schedule requires preparation before admission.");
        var report = ReadFrozenReport(sources[ReportPath]);
        var context = ReadFrozenRequestContext(sources);
        var coordinates = working.Coordinates;
        if (context is not { } selected || selected.SessionId != coordinates.SessionId ||
            selected.RequestId != coordinates.RequestId || selected.TurnNumber != coordinates.Turn)
            throw new InvalidDataException("The diagnosis progression report/request context belongs to another turn.");
        var future = checked((int)working.ResultingMinute); // Existing progression numeric domain, no clamping/default.
        var issues = new List<ValidationIssue>();
        ValidateMortalOutcome(control, report, context, future, issues);
        if (issues.Any(value => value.Severity == IssueSeverity.Error))
            throw new InvalidDataException("Diagnosis progression report refused: " + string.Join(";", issues.Select(value => value.Code)));
        var consumed = ApplyMortalOutcomeFromInputs(schedule, control, report.Report,
            report.State == ProgressionFileReadState.Valid && ProgressionReportMatchesCurrentTurn(report.Report, context),
            new WorldTimeResolutionResult(future, false));
        schedule.CurrentRealm = realm;
        schedule.LastUpdatedUtc = DateTime.UtcNow.ToString("o");
        var after = EncodeUtf8WithPreamble(JsonSerializer.Serialize(schedule, JsonOpts));
        var reportAfter = consumed && report.FilePresent ? null : sources[ReportPath];
        working.RequireOperation(_fs, lease);
        return new(new(true, scheduleBytes), new(sources[ReportPath] is not null, sources[ReportPath]),
            new(true, after), new(reportAfter is not null, reportAfter),
            ComputeAcceptedOutcomeMutationFingerprint(JsonSerializer.Serialize(control, JsonOpts), new(true, scheduleBytes),
                new(sources[ReportPath] is not null, sources[ReportPath]), new(true, after), new(reportAfter is not null, reportAfter)));
    }
}
