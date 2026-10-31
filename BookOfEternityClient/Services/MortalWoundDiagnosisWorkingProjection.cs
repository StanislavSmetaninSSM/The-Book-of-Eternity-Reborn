using System.Collections.Immutable;
using System.Globalization;
using BookOfEternityClient.Core;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundDiagnosisRecoveryMember(string OriginalSourcePath,
    WoundMaterializationEnvelope Original, WoundMaterializationEnvelope Working,
    long ConsumedRecovery, long ConsumedDeterioration, MortalWoundRecoveryResolution Resolution);

// Private-constructor, one-operation working DATA. No accepted-state instance is
// rewritten or manufactured for the future clock or knowledge-after image.
internal sealed class MortalWoundDiagnosisWorkingProjection
{
    private readonly MortalWoundTreatmentAcceptedStateAuthority _owner;
    private readonly MortalWoundDiagnosisOutcome _outcome;
    private readonly JsonObject _clockAfter;
    private readonly JsonObject _originalControl;
    private readonly JsonObject _originalSchedule;
    private MortalWoundDiagnosisWorkingProjection(MortalWoundTreatmentAcceptedStateAuthority owner,
        MortalWoundDiagnosisOutcome outcome, MortalWoundRecoverySourceEvidence originalSource,
        JsonObject clockAfter, JsonObject control, JsonObject schedule,
        ImmutableArray<MortalWoundDiagnosisRecoveryMember> members, string fingerprint)
    {
        _owner = owner; _outcome = outcome; OriginalSource = originalSource;
        _clockAfter = clockAfter; _originalControl = control; _originalSchedule = schedule;
        Members = members; Fingerprint = fingerprint;
    }
    internal MortalWoundRecoverySourceEvidence OriginalSource { get; }
    internal ImmutableArray<MortalWoundDiagnosisRecoveryMember> Members { get; }
    internal long OriginalMinute => _outcome.OriginalMinute;
    internal long ResultingMinute => _outcome.ResultingMinute;
    internal string Fingerprint { get; }
    internal JsonObject ClockAfter => _clockAfter.DeepClone().AsObject();
    internal JsonObject OriginalControl => _originalControl.DeepClone().AsObject();
    internal JsonObject OriginalSchedule => _originalSchedule.DeepClone().AsObject();

    internal static MortalWoundDiagnosisWorkingProjection Build(MortalWoundTreatmentAcceptedStateAuthority owner,
        MortalWoundDiagnosisOutcome outcome)
    {
        owner.RequireOriginalDiagnosisRequest(outcome.Request);
        outcome.RequireCurrent(owner, new WoundHistoryParseResult(owner.History, Array.Empty<ValidationIssue>()));
        var source = MortalWoundRecoverySourceEvidence.FromAcceptedState(owner);
        var clock = owner.ReadDiagnosisOriginalRoot(EffectAcceptedTurnInputComposer.WorldTimePath);
        if (clock["currentTimeInMinutes"] is not JsonValue time || !time.TryGetValue<long>(out var originalMinute) ||
            originalMinute != source.CurrentGameMinute || originalMinute != outcome.OriginalMinute)
            throw new InvalidDataException("The diagnosis working clock differs from its authenticated numeric original.");
        var control = owner.ReadDiagnosisOriginalControl();
        if (control["currentWorldTimeInMinutes"] is not JsonValue start || !start.TryGetValue<int>(out var controlMinute) || controlMinute != originalMinute)
            throw new InvalidDataException("The signed original control does not retain the exact starting Mortal clock.");
        var schedule = owner.ReadDiagnosisOriginalRoot(ProgressionScheduleService.SchedulePath);
        var originals = owner.ReadDiagnosisOriginalCohort();
        if (originals.Count == 0 || originals.Count(value => value.Wound.WoundId == outcome.Request.Coordinates.WoundId) != 1 ||
            originals.Select(value => value.Wound.WoundId).Distinct(StringComparer.Ordinal).Count() != originals.Count)
            throw new InvalidDataException("Diagnosis requires its complete unique active physical original cohort.");
        var knowledge = outcome.KnowledgeAfter;
        var members = ImmutableArray.CreateBuilder<MortalWoundDiagnosisRecoveryMember>();
        foreach (var original in originals)
        {
            var working = original.Wound.WoundId == knowledge.WoundId ? knowledge : original.Wound;
            var consumed = owner.History.GetRecoveryConsumption(original.Wound);
            var calculated = MortalWoundRecoveryPlanner.ReconstructAtMinute(source, owner.Binding, working,
                outcome.ResultingMinute, consumed.Recovery, consumed.Deterioration);
            if (calculated.Disposition != MortalWoundRecoveryPlanningDisposition.Resolved || calculated.Resolution is null)
                throw new InvalidDataException("Ordinary recovery working calculation refused: " + string.Join(";", calculated.Issues.Select(value => value.Code)));
            members.Add(new(original.Path, original.Wound, working, consumed.Recovery, consumed.Deterioration, calculated.Resolution));
        }
        clock["currentTimeInMinutes"] = outcome.ResultingMinute;
        var fingerprint = ComputeComparisonFingerprint(outcome.Request, outcome.Resolution, owner.AcceptedStateFingerprint,
            outcome.OriginalMinute, outcome.ResultingMinute, control, schedule, clock, members);
        return new(owner, outcome, source, clock, control, schedule, members.ToImmutable(), fingerprint);
    }

    internal static string ComputeComparisonFingerprint(MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution, string acceptedFingerprint, long originalMinute, long resultingMinute,
        JsonObject control, JsonObject schedule, JsonObject clock, IEnumerable<MortalWoundDiagnosisRecoveryMember> members) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "mortal_diagnosis.working_projection", "1", request.RequestFingerprint, resolution.ResultFingerprint, acceptedFingerprint,
            originalMinute.ToString(CultureInfo.InvariantCulture), resultingMinute.ToString(CultureInfo.InvariantCulture),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(control), WoundAcceptedTurnFingerprintWriter.CanonicalJson(schedule),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(clock)
        }.Concat(members.SelectMany(value => new[] { value.OriginalSourcePath, WoundMaterializationContract.SerializeCanonical(value.Original),
            WoundMaterializationContract.SerializeCanonical(value.Working), value.Resolution.AuthorityFingerprint })));

    internal MortalWoundTreatmentAttemptCoordinates Coordinates => _outcome.Request.Coordinates;
    internal byte[] ReadOriginalBytes(string path) => _owner.ReadDiagnosisOriginalBytes(path);
    internal void RequireOperation(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        if (!_owner.IsLeaseBoundTo(files, lease)) throw new InvalidDataException("The diagnosis working image cannot cross its actual FS owner or lease.");
        RequireCurrent(_owner);
    }

    internal void RequireCurrent(MortalWoundTreatmentAcceptedStateAuthority owner)
    {
        if (!ReferenceEquals(owner, _owner)) throw new InvalidDataException("The diagnosis working projection belongs to another operation owner.");
        owner.RequireOriginalDiagnosisRequest(_outcome.Request);
        _outcome.RequireCurrent(owner, new WoundHistoryParseResult(owner.History, Array.Empty<ValidationIssue>()));
    }
}
