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
            IReadOnlyDictionary<string, JsonNode?> roots, MortalDiagnosisPublicationBinding? publication)
        {
            Owner = owner; Outcome = outcome; Working = working; _progression = CloneProgression(progression);
            Publication = publication;
            _input = WoundAcceptedTurnData.CloneInput(input)!;
            _roots = CloneRecoverySourceRoots(roots);
            Fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            {
                "book_of_eternity.mortal_diagnosis.continuation", "1", working.Fingerprint,
                outcome.Resolution.ResultFingerprint, progression.MutationFingerprint,
                WoundAcceptedTurnFingerprints.ComputeInput(_input), publication?.SemanticFingerprint,
                publication?.Finalization.FinalizationFingerprint, publication?.ItemCommands.Fingerprint,
                publication?.CriticalReaction.Fingerprint
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
        internal MortalDiagnosisPublicationBinding? Publication { get; }
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
        internal void RequireComposedClock(IReadOnlyDictionary<string, JsonObject> companions)
        {
            if (!companions.TryGetValue(EffectAcceptedTurnInputComposer.WorldTimePath, out var clock) ||
                !JsonNode.DeepEquals(clock, Working.ClockAfter) ||
                !companions.TryGetValue(ProgressionScheduleService.SchedulePath, out var schedule) ||
                !JsonNode.DeepEquals(schedule, JsonNode.Parse(FileSystemManager.DecodeFileBytes(_progression.ScheduleAfter.Bytes!))))
                throw new InvalidDataException("Diagnosis common after-images changed T0/duration/T1 or the exact ordinary progression result.");
            if (_progression.ReportAfter.Bytes is { } report &&
                (!companions.TryGetValue(ProgressionScheduleService.ReportPath, out var actual) ||
                 !JsonNode.DeepEquals(actual, JsonNode.Parse(FileSystemManager.DecodeFileBytes(report)))))
                throw new InvalidDataException("Diagnosis common report differs from its request-bound ordinary progression calculation.");
            if (_progression.ReportAfter.Bytes is null && companions.ContainsKey(ProgressionScheduleService.ReportPath))
                throw new InvalidDataException("Diagnosis cannot reintroduce a consumed or originally absent progression report.");
        }
        internal void RequirePublishedProgressionData(JsonNode originalControl,
            IReadOnlyList<MortalWoundTreatmentPublicationBeforeImage> published)
        {
            if (!JsonNode.DeepEquals(originalControl, Working.OriginalControl))
                throw new InvalidDataException("Diagnosis completion control differs from its complete signed original.");
            JsonNode? Read(string path)
            {
                var image = published.SingleOrDefault(value => value.Path == path) ??
                    throw new InvalidDataException("Diagnosis published agreement omitted " + path);
                return image.Bytes is { } bytes ? JsonNode.Parse(FileSystemManager.DecodeFileBytes(bytes)) : null;
            }
            if (!JsonNode.DeepEquals(Read(EffectAcceptedTurnInputComposer.WorldTimePath), Working.ClockAfter) ||
                !JsonNode.DeepEquals(Read(ProgressionScheduleService.SchedulePath),
                    JsonNode.Parse(FileSystemManager.DecodeFileBytes(_progression.ScheduleAfter.Bytes!))) ||
                !JsonNode.DeepEquals(Read(ProgressionScheduleService.ReportPath), _progression.ReportAfter.Bytes is { } report
                    ? JsonNode.Parse(FileSystemManager.DecodeFileBytes(report)) : null))
                throw new InvalidDataException("Diagnosis completion cannot advance duration or ordinary progression a second time.");
        }
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
            ProgressionScheduleService.FrozenMortalCalculation progression, MortalDiagnosisPublicationBinding? publication = null)
        {
            working.RequireCurrent(owner);
            outcome.RequireCurrent(owner, new(owner.History, Array.Empty<ValidationIssue>()));
            if (publication is not null)
            {
                var finalization = MortalWoundTreatmentResourceComposer.Finalize(outcome.Resolution);
                if (!finalization.IsValid || finalization.Finalization is null ||
                    finalization.Finalization.FinalizationFingerprint != publication.Finalization.FinalizationFingerprint ||
                    string.IsNullOrWhiteSpace(publication.SemanticFingerprint) || publication.ReservationAuthority is null ||
                    publication.SkillProjectionAuthority is null || publication.ItemCommands is null)
                    throw new InvalidDataException("Diagnosis publication binding differs from its actual terminal resource result.");
                if (!publication.CriticalReaction.IsValid || !MortalWoundCriticalReactionPublicationPlanner.HasMatchingFingerprint(
                    outcome.Request, outcome.Resolution, publication.CriticalReaction.LifecycleEvents, publication.CriticalReaction.Fingerprint!))
                    throw new InvalidDataException("Diagnosis publication binding changed its exact retained Fate lifecycle decision.");
            }
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
                !catalog.Occurrences.Where(value => value.Wound.Owner.Realm == "mortal_world" && value.Wound.Classification.Domain == "physical")
                    .OrderBy(value => value.JsonPath, StringComparer.Ordinal).Select(value => (value.JsonPath, WoundMaterializationContract.SerializeCanonical(value.Wound)))
                    .SequenceEqual(original.Occurrences.OrderBy(value => value.JsonPath, StringComparer.Ordinal)
                        .Select(value => (value.JsonPath, WoundMaterializationContract.SerializeCanonical(value.Wound)))) ||
                !JsonNode.DeepEquals(input.PreTurnHistory, JsonNode.Parse(WoundHistoryState.SerializeCanonical(owner.History))))
                throw new InvalidDataException("Diagnosis preparation changed its complete signed original wound cohort or History.");
            return new(owner, outcome, working, progression, input, roots, publication);
        }
    }

    internal static WoundAcceptedTurnPreparationResult PrepareMortalDiagnosisContinuation(
        MortalWoundTreatmentAcceptedStateAuthority owner, MortalWoundDiagnosisOutcome outcome,
        MortalWoundDiagnosisWorkingProjection working, ProgressionScheduleService.FrozenMortalCalculation progression,
        MortalDiagnosisPublicationBinding? publication = null)
    {
        var continuation = MortalDiagnosisContinuation.Admit(owner, outcome, working, progression, publication);
        return continuation.Prepare();
    }

    internal static EffectAcceptedTurnInput ComposeMortalDiagnosisEffectInput(WoundPreparedAcceptedTurnPlan prepared)
    {
        var owner = prepared.MortalDiagnosisAuthority;
        if (owner is null || !owner.PreparedAgrees(prepared)) throw new InvalidDataException("Diagnosis lost its exact private prepared program.");
        var input = owner.Input;
        var carriers = input.PreTurnEffectCarriers ?? throw new InvalidDataException("Diagnosis requires complete original effect carriers.");
        IReadOnlyDictionary<string, JsonNode?>? skillRoots = null;
        if (owner.Publication is { } publication)
        {
            if (!TryReadTreatmentSkillProjection(owner, publication.ReservationAuthority, out _, out var afterImages, out _))
                throw new InvalidDataException("Diagnosis lost its actual ordinary skill projection.");
            skillRoots = afterImages.ToDictionary(pair => pair.Key, pair => (JsonNode?)pair.Value, StringComparer.Ordinal);
        }
        return EffectAcceptedTurnInputComposer.Compose(input.Binding.SessionId, input.Binding.SnapshotToken, input.Binding.Turn,
            new JsonObject(), carriers, carriers, input.PreTurnEffectIdentityIndex, owner.SourceRoots,
            currentWorldTime: owner.Working.ResultingMinute, publicationCarrierBaselines: carriers,
            realm: input.Binding.Realm, preparedWoundPlan: prepared, acceptedSourceRoots: skillRoots,
            acceptedReportedLifecycleEvents: owner.Publication?.CriticalReaction.LifecycleEvents);
    }
}
