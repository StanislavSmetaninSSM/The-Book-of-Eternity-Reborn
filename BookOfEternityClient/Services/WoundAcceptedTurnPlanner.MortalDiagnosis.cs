using System.Collections.Immutable;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    internal const string DiagnosisPublicationSummary =
        "The declared Mortal diagnosis and elapsed recovery are retained for replay.";

    // A closed purpose within the existing wound preparation owner. Its child
    // recovery views retain this actual companion, never a new accepted state.
    internal sealed class MortalDiagnosisContinuation
    {
        private readonly WoundAcceptedTurnInput _input;
        private readonly IReadOnlyDictionary<string, JsonNode?> _roots;
        private readonly object[] _recoveries;
        private readonly ProgressionScheduleService.FrozenMortalCalculation _progression;
        private MortalDiagnosisContinuation(MortalWoundTreatmentAcceptedStateAuthority owner,
            MortalWoundDiagnosisOutcome outcome, MortalWoundDiagnosisWorkingProjection working,
            ProgressionScheduleService.FrozenMortalCalculation progression, WoundAcceptedTurnInput input,
            IReadOnlyDictionary<string, JsonNode?> roots)
        {
            Owner = owner; Outcome = outcome; Working = working; _progression = CloneProgression(progression);
            _input = WoundAcceptedTurnData.CloneInput(input)!;
            _roots = CloneRecoverySourceRoots(roots);
            Fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            {
                "book_of_eternity.mortal_diagnosis.continuation", "1", working.Fingerprint,
                outcome.Resolution.ResultFingerprint, progression.MutationFingerprint,
                WoundAcceptedTurnFingerprints.ComputeInput(_input)
            }.Concat(_roots.OrderBy(pair => pair.Key, StringComparer.Ordinal).SelectMany(pair =>
                new[] { pair.Key, WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value) })));
            _recoveries = working.Members.Select(member => (object)new RecoveryContinuationAuthority(owner,
                member.Resolution, _input, _roots, this, member)).ToArray();
        }
        internal MortalWoundTreatmentAcceptedStateAuthority Owner { get; }
        internal MortalWoundDiagnosisOutcome Outcome { get; }
        internal MortalWoundDiagnosisWorkingProjection Working { get; }
        internal ProgressionScheduleService.FrozenMortalCalculation Progression => CloneProgression(_progression);
        internal string Fingerprint { get; }
        internal WoundAcceptedTurnInput Input => WoundAcceptedTurnData.CloneInput(_input)!;
        internal IReadOnlyDictionary<string, JsonNode?> SourceRoots => CloneRecoverySourceRoots(_roots);
        internal ImmutableArray<string> TransitionIds => new[] { MortalWoundDiagnosisOutcome.TransitionId(Outcome.Request) }
            .Concat(_recoveries.SelectMany(value => ((RecoveryContinuationAuthority)value).TransitionIds)).ToImmutableArray();
        internal bool OwnsSharedBaselines(WoundAcceptedTurnInput input, IReadOnlyDictionary<string, JsonNode?> roots) =>
            ReferenceEquals(input, _input) && ReferenceEquals(roots, _roots);
        internal IReadOnlyList<(object Authority, RecoveryContinuationView View)> ReadRecoveryProgram()
        {
            // One detached baseline export per execution, shared only within
            // this disposable operation; each rule still checks its real parent.
            var input = Input; var roots = SourceRoots;
            return _recoveries.Select(value => (value, ((RecoveryContinuationAuthority)value).ReadForDiagnosis(this, input, roots))).ToArray();
        }

        internal void RequireCurrent() => Working.RequireCurrent(Owner);
        internal WoundAcceptedTurnPreparationResult Prepare()
        {
            var inputFingerprint = WoundAcceptedTurnFingerprints.ComputeInput(_input);
            var baseline = WoundAcceptedTurnPlannerCore.CreateBaselineAuthority(inputFingerprint, _input);
            WoundPreparedAcceptedTurnPlan Build(string seal) => new(_input.Binding,
                WoundAcceptedTurnFingerprints.ComputeBinding(_input.Binding), inputFingerprint, seal,
                Working.Members.Select(member => member.Original.WoundId).ToArray(), TransitionIds,
                Working.Members.Select(member => member.Original).ToArray(), Array.Empty<WoundEffectOperationBatch>(), baseline,
                mortalDiagnosisAuthority: this);
            return new(Build(WoundAcceptedTurnFingerprints.ComputePreparation(Build(string.Empty))), Array.Empty<ValidationIssue>());
        }
        private static ProgressionScheduleService.FrozenMortalCalculation CloneProgression(ProgressionScheduleService.FrozenMortalCalculation value) =>
            new(new(value.ScheduleBefore.Existed, value.ScheduleBefore.Bytes?.ToArray()), new(value.ReportBefore.Existed, value.ReportBefore.Bytes?.ToArray()),
                new(value.ScheduleAfter.Existed, value.ScheduleAfter.Bytes?.ToArray()), new(value.ReportAfter.Existed, value.ReportAfter.Bytes?.ToArray()), value.MutationFingerprint);
        internal bool PreparedAgrees(WoundPreparedAcceptedTurnPlan prepared) =>
            ReferenceEquals(prepared.MortalDiagnosisAuthority, this) &&
            prepared.InputFingerprint == WoundAcceptedTurnFingerprints.ComputeInput(_input) &&
            prepared.BindingFingerprint == WoundAcceptedTurnFingerprints.ComputeBinding(_input.Binding) &&
            prepared.AllocatedTransitionIds.SequenceEqual(TransitionIds, StringComparer.Ordinal) &&
            prepared.AllocatedWoundIds.SequenceEqual(Working.Members.Select(member => member.Original.WoundId), StringComparer.Ordinal) &&
            prepared.PreparedWounds.Select(wound => WoundMaterializationContract.SerializeCanonical(wound)).SequenceEqual(
                Working.Members.Select(member => WoundMaterializationContract.SerializeCanonical(member.Original)), StringComparer.Ordinal) &&
            prepared.EffectOperationBatches.Count == 0 &&
            prepared.WoundPreparationFingerprint == WoundAcceptedTurnFingerprints.ComputePreparation(prepared);

        internal static MortalDiagnosisContinuation Admit(MortalWoundTreatmentAcceptedStateAuthority owner,
            MortalWoundDiagnosisOutcome outcome, MortalWoundDiagnosisWorkingProjection working,
            ProgressionScheduleService.FrozenMortalCalculation progression)
        {
            working.RequireCurrent(owner);
            outcome.RequireCurrent(owner, new(owner.History, Array.Empty<ValidationIssue>()));
            var (input, roots) = owner.ReadDiagnosisPublicationBaselines(outcome.Request);
            var preparedAt = progression.ScheduleAfter.Bytes is { } afterBytes
                ? JsonNode.Parse(FileSystemManager.DecodeFileBytes(afterBytes))?["lastUpdatedUtc"]?.GetValue<string>() : null;
            var expectedProgression = ProgressionScheduleService.ReconstructDiagnosisOutcomeData(working.OriginalControl,
                working.ReadOriginalBytes(ProgressionScheduleService.SchedulePath),
                working.ReadOriginalBytes(EffectAcceptedTurnInputComposer.WorldTimePath), progression.ReportBefore.Bytes,
                owner.Binding.SessionId, owner.Binding.RequestId, owner.Binding.Turn,
                checked(working.ResultingMinute - working.OriginalMinute), preparedAt ?? throw new InvalidDataException("Missing actual progression preparation time."));
            static bool Same(CanonicalBeforeImage first, CanonicalBeforeImage second) => first.Existed == second.Existed &&
                (first.Bytes is null ? second.Bytes is null : second.Bytes is not null && first.Bytes.AsSpan().SequenceEqual(second.Bytes));
            if (expectedProgression.MutationFingerprint != progression.MutationFingerprint ||
                !Same(expectedProgression.ScheduleBefore, progression.ScheduleBefore) || !Same(expectedProgression.ScheduleAfter, progression.ScheduleAfter) ||
                !Same(expectedProgression.ReportBefore, progression.ReportBefore) || !Same(expectedProgression.ReportAfter, progression.ReportAfter))
                throw new InvalidDataException("Diagnosis progression differs from its original control, declared duration or exact verified clock/schedule images.");
            if (input.Opportunities.Count != 0 || input.Transitions.Count != 0 ||
                WoundAcceptedTurnFingerprints.ComputeBinding(input.Binding) != WoundAcceptedTurnFingerprints.ComputeBinding(owner.Binding))
                throw new InvalidDataException("Diagnosis preparation must retain its exact original binding and no authored wound operations.");
            var valid = WoundAcceptedTurnPlannerCore.ValidateInput(input);
            if (valid.Issues.Count != 0) throw new InvalidDataException("Diagnosis original wound/effect baselines are invalid.");
            var catalog = WoundCarrierCatalog.Build(input.PreTurnCarriers);
            var original = WoundCarrierCatalog.Build(owner.ReadDiagnosisOriginalCarriers());
            if (catalog.Issues.Count != 0 || original.Issues.Count != 0 ||
                !catalog.Occurrences.Select(value => (value.JsonPath, WoundMaterializationContract.SerializeCanonical(value.Wound)))
                    .SequenceEqual(original.Occurrences.Select(value => (value.JsonPath, WoundMaterializationContract.SerializeCanonical(value.Wound)))) ||
                input.PreTurnHistory.ToJsonString() != JsonNode.Parse(WoundHistoryState.SerializeCanonical(owner.History))!.ToJsonString())
                throw new InvalidDataException("Diagnosis preparation changed its complete signed original wound cohort or History.");
            return new(owner, outcome, working, progression, input, roots);
        }
    }

    internal static WoundAcceptedTurnPreparationResult PrepareMortalDiagnosisContinuation(
        MortalWoundTreatmentAcceptedStateAuthority owner, MortalWoundDiagnosisOutcome outcome,
        MortalWoundDiagnosisWorkingProjection working, ProgressionScheduleService.FrozenMortalCalculation progression)
    {
        var continuation = MortalDiagnosisContinuation.Admit(owner, outcome, working, progression);
        return continuation.Prepare();
    }

    internal static EffectAcceptedTurnInput ComposeMortalDiagnosisEffectInput(WoundPreparedAcceptedTurnPlan prepared)
    {
        var owner = prepared.MortalDiagnosisAuthority;
        if (owner is null || !owner.PreparedAgrees(prepared)) throw new InvalidDataException("Diagnosis lost its exact private prepared program.");
        var input = owner.Input;
        var carriers = input.PreTurnEffectCarriers ?? throw new InvalidDataException("Diagnosis requires complete original effect carriers.");
        return EffectAcceptedTurnInputComposer.Compose(input.Binding.SessionId, input.Binding.SnapshotToken, input.Binding.Turn,
            new JsonObject(), carriers, carriers, input.PreTurnEffectIdentityIndex, owner.SourceRoots,
            currentWorldTime: owner.Working.ResultingMinute, publicationCarrierBaselines: carriers,
            realm: input.Binding.Realm, preparedWoundPlan: prepared);
    }
}
