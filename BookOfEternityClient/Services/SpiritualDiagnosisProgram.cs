using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>One immutable diagnosis/ordinary-recovery program, never a world or publication owner.</summary>
internal sealed class SpiritualDiagnosisProgram
{
    private readonly JsonObject _request;
    private readonly SpiritualWoundRecoveryMember[] _original;
    private readonly SpiritualWoundRecoveryMember[] _natural;
    private readonly SpiritualWoundStage[] _stages;
    private readonly WoundMaterializationEnvelope _before;
    private readonly WoundMaterializationEnvelope _after;
    private readonly string _realm;
    private readonly int _ordinal;
    private readonly JsonObject _projection;

    private SpiritualDiagnosisProgram(JsonObject request, WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after, SpiritualWoundRecoveryMember[] original,
        SpiritualWoundRecoveryMember[] natural, SpiritualWoundStage[] stages, string reference, string realm, int ordinal)
    {
        _request = request.DeepClone().AsObject(); _before = before; _after = after;
        _original = original; _natural = natural; _stages = stages;
        NaturalProgramReference = reference; _realm = realm; _ordinal = ordinal;
        var recovery = SpiritualHealingRequestProjection.CreateNatural(_realm, _ordinal, reference, _natural);
        _projection = new()
        {
            ["schemaVersion"] = 1, ["mode"] = "diagnose", ["requestFingerprint"] = _request["requestFingerprint"]!.DeepClone(),
            ["cycleOrdinal"] = _ordinal, ["cycleKey"] = AfterlifeWoundTreatmentAuthority.CycleKey(_realm, _ordinal),
            ["diagnosisPathName"] = _before.Treatment.DiagnosisPaths.Single(p => p.DiagnosisPathId ==
                _request["diagnosis"]!["diagnosisPathId"]!.GetValue<string>()).DisplayName,
            ["woundLabel"] = _before.Display.Name, ["destinationGraphs"] = recovery["destinationGraphs"]!.DeepClone(),
            ["naturalRecovery"] = recovery
        };
        if (_before.Treatment.DiagnosisPaths.Single(path => path.DiagnosisPathId == _request["diagnosis"]!["diagnosisPathId"]!.GetValue<string>())
            .Requirements.Any(row => SpiritualDiagnosisCanonicalSources.IsCanonicalKind(row.GetProperty("kind").GetString()!)))
            _projection[PendingTurnSnapshotPathPresenceV1.CanonicalDiagnosisHint] = true;
    }

    internal WoundMaterializationEnvelope KnowledgeBefore => WoundAcceptedTurnData.CloneWound(_before)!;
    internal WoundMaterializationEnvelope KnowledgeAfter => WoundAcceptedTurnData.CloneWound(_after)!;
    internal IReadOnlyList<SpiritualWoundRecoveryMember> OriginalMembers => _original.ToArray();
    internal IReadOnlyList<SpiritualWoundRecoveryMember> NaturalMembers => _natural.ToArray();
    internal IReadOnlyList<SpiritualWoundStage> NaturalStages => _stages.Skip(1).ToArray();
    internal IReadOnlyList<SpiritualWoundStage> Stages => _stages.ToArray();
    internal string NaturalProgramReference { get; }
    internal JsonObject Projection => _projection.DeepClone().AsObject();

    internal static SpiritualDiagnosisProgram Build(SpiritualSelfTreatmentStageContext context, JsonObject retained,
        SpiritualDiagnosisAdmission admission, IReadOnlyList<SpiritualWoundRecoveryMember> originalMembers)
    {
        ArgumentNullException.ThrowIfNull(originalMembers);
        var realm = admission.Wound.Owner.Realm;
        var members = originalMembers.Select(m => m with { Wound = WoundAcceptedTurnData.CloneWound(m.Wound)! }).ToArray();
        if (members.Length < 1 || members.Any(m => m.HealingTier is < 0 or > 5 ||
                m.Wound.Lifecycle != "active" || m.Wound.Classification.Domain != "spiritual" || m.Wound.Owner.Realm != realm ||
                m.Wound.Owner.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath || m.Wound.Severity.Rank is < 1 or > 4) ||
            members.Select(m => m.Wound.WoundId).Distinct(StringComparer.Ordinal).Count() != members.Length ||
            members.GroupBy(m => m.Wound.Owner).Any(g => g.Select(m => m.HealingTier).Distinct().Count() != 1) ||
            !members.Select(m => m.Wound.WoundId).SequenceEqual(members.OrderBy(m => m.Wound.Owner.OwnerKind, StringComparer.Ordinal)
                .ThenBy(m => m.Wound.Owner.OwnerId, StringComparer.Ordinal).ThenBy(m => m.Wound.WoundId, StringComparer.Ordinal)
                .Select(m => m.Wound.WoundId), StringComparer.Ordinal))
            throw new InvalidDataException("Diagnosis requires the complete nonempty ordered ordinary original cohort and bounded owner tiers.");
        var selected = members.Where(m => m.Wound.WoundId == admission.Wound.WoundId).ToArray();
        if (selected.Length != 1 || selected[0].Wound.Owner.OwnerKind == "player_soul" && selected[0].HealingTier != admission.HealingTier ||
            WoundMaterializationContract.SerializeCanonical(selected[0].Wound) !=
                WoundMaterializationContract.SerializeCanonical(admission.Wound))
            throw new InvalidDataException("The original diagnosis wound must occur exactly in its ordinary cohort.");
        var request = retained.DeepClone().AsObject();
        if (!SpiritualDiagnosisRequestCodec.IsExact(request, context, admission.Selection, admission))
            throw new InvalidDataException("Diagnosis program differs from its retained request/context/admission.");
        var path = admission.Wound.Treatment.DiagnosisPaths.Single(p => p.DiagnosisPathId == admission.Selection.Diagnosis!.DiagnosisPathId);
        var routes = path.Reveals.Where(f => f.StartsWith("route:", StringComparison.Ordinal)).Select(f => f[6..]);
        var complications = path.Reveals.Where(f => f.StartsWith("complication:", StringComparison.Ordinal)).Select(f => f[13..]).ToHashSet(StringComparer.Ordinal);
        var reference = request["requestFingerprint"]!.GetValue<string>();
        var operation = SpiritualWoundRecoveryPlanner.Coordinate("spiritual_diagnosis_operation", reference, admission.Wound.WoundId, 0);
        var transition = SpiritualWoundRecoveryPlanner.Coordinate("spiritual_diagnosis_transition", reference, admission.Wound.WoundId, 0);
        var after = admission.Wound with
        {
            Treatment = admission.Wound.Treatment with
            { KnownRouteIds = admission.Wound.Treatment.KnownRouteIds.Concat(routes.Where(r => !admission.Wound.Treatment.KnownRouteIds.Contains(r, StringComparer.Ordinal))).ToArray() },
            Complications = admission.Wound.Complications.Select(c => complications.Contains(c.ComplicationId) &&
                c.Visibility is not ("public" or "known_to_player") ? c with { Visibility = "known_to_player" } : c).ToArray(),
            LastTransition = new(transition, checked(admission.Wound.LastTransition.Ordinal + 1), context.Turn, "diagnose")
        };
        var reduced = WoundTransitionReducer.Reduce(SpiritualDiagnosisTransitionPlanner.CreateKnowledgeRequest(context,
            request, admission, after, transition, operation, "diagnosis_local_program"));
        if (!reduced.IsValid) throw new InvalidDataException("Diagnosis knowledge delta rejected: " + string.Join(";", reduced.Issues.Select(i => i.Code)));
        after = reduced.ProposedAfter!;
        var natural = members.Select(m => m.Wound.WoundId == after.WoundId ? m with { Wound = after } : m).ToArray();
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "spiritual_diagnosis.natural_program", "1", reference,
            WoundMaterializationContract.SerializeCanonical(after)
        }.Concat(members.SelectMany(m => new[] { WoundMaterializationContract.SerializeCanonical(m.Wound),
            m.HealingTier.ToString(System.Globalization.CultureInfo.InvariantCulture) })));
        var suffix = SpiritualHealingRequestProjection.BuildNaturalProgram(realm, context.Progression.NextChaosSeaTurnOrdinal, natural).Stages;
        if (suffix.Length is < 1 or > SpiritualHealingPersistedResult.MaxStageCount)
            throw new InvalidDataException("Diagnosis requires the complete natural suffix within its existing persisted-stage bound.");
        var first = new SpiritualWoundStage(after.WoundId, "diagnose", after.Severity.Rank, after.Recovery.CurrentStepProgress,
            false, operation, transition, string.Empty, request["diagnosis"]!["attemptId"]!.GetValue<string>());
        return new(request, admission.Wound, after, members, natural, new[] { first }.Concat(suffix).ToArray(),
            fingerprint, realm, context.Progression.NextChaosSeaTurnOrdinal);
    }
}
