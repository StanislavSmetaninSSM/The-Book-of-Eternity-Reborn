using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class MortalWoundTreatmentCompletion
{
    /// <summary>
    /// Reads complete historical heal snapshots from strictly parsed publication,
    /// History, identity and carrier DATA within one display read. It neither
    /// restores an owner nor checks current payment or surviving legacy state.
    /// Invalid or unsupported historical DATA produces no display evidence.
    /// </summary>
    internal static bool TryReadHistoricalHeal(MortalWoundTreatmentPersistedResult result,
        WoundHistoryTransition primary, WoundHistoryState history, WoundIdentityState identities,
        WoundCarrierCatalog wounds, EffectCarrierCatalog effects, EffectIdentityState effectIndex,
        [NotNullWhen(true)] out WoundMaterializationEnvelope? before,
        [NotNullWhen(true)] out WoundMaterializationEnvelope? after)
    {
        before = null;
        after = null;
        try
        {
            if (result is null || primary is null || history is null || identities is null ||
                wounds is null || effects is null || effectIndex is null ||
                wounds.Issues.Count != 0 || effects.Issues.Count != 0 ||
                !history.TryResolveExactTransition(primary.TransitionId, out var recorded) || recorded is null ||
                (primary with { TransitionResult = null }) != (recorded with { TransitionResult = null }) ||
                !HistoricalResultAgrees(primary.TreatmentResult, result) ||
                !HistoricalResultAgrees(recorded.TreatmentResult, result)) return false;

            // Reconstruction reads the result's frozen canonical payload, not
            // mutable exposed Request/Receipt projections or the current clock.
            var comparison = result.ReconstructHistoricalPublication();
            var resolution = comparison.Resolution;
            var preparation = comparison.Preparation;
            var request = resolution.RequestAuthority;
            var coordinates = request.Coordinates;
            var source = preparation.Before;
            if (!preparation.PreparesTerminalHeal || coordinates.Purpose != "treatment" ||
                resolution.AttemptDisposition != "AcceptedTerminal" ||
                source.Lifecycle != "active" || source.Classification.Domain != "physical" ||
                source.WoundId != coordinates.WoundId || source.Owner.Realm != coordinates.Realm ||
                coordinates.Realm != "mortal_world" || source.Owner.OwnerKind != coordinates.TargetKind ||
                source.Owner.OwnerId != coordinates.TargetId ||
                WoundIdentityState.ComputeSemanticFingerprint(source) != coordinates.ExpectedBeforeFingerprint ||
                primary.Kind != "treat" || primary.Terminal || primary.WoundId != coordinates.WoundId ||
                primary.TransitionId != preparation.TransitionId || primary.Turn != coordinates.Turn ||
                primary.OperationKey != coordinates.OperationKey || primary.EventRef != coordinates.EventRef ||
                primary.AttemptId != coordinates.AttemptId || primary.CourseId != resolution.CourseId ||
                primary.CourseMilestoneOrdinal != resolution.CourseMilestoneOrdinal ||
                primary.CycleKey is not null || primary.PaymentFingerprint is not null ||
                primary.BeforeFingerprint != coordinates.ExpectedBeforeFingerprint ||
                primary.SourceFingerprint != request.RequestFingerprint ||
                primary.WoundTransitionOrdinal != checked(source.LastTransition.Ordinal + 1) ||
                !HasExactTreatmentHistoryOutput(primary)) return false;

            var treated = ReconstructFinalWound(resolution, preparation, effects, effectIndex, out var selected);
            if (selected is null || treated.Lifecycle != "active" || treated.Severity.Rank != 1 ||
                treated.WoundId != source.WoundId || treated.Owner != source.Owner ||
                treated.LastTransition.Kind != "treat" || treated.LastTransition.Turn != coordinates.Turn ||
                treated.LastTransition.Ordinal != primary.WoundTransitionOrdinal) return false;
            var linked = RequireLinkedTerminalTreatmentHistory(resolution, preparation, treated,
                history, primary, identities, wounds);
            var healed = linked.Healed;
            var child = linked.Child;
            var woundRows = history.ResolveWoundTransitions(coordinates.WoundId);
            var childIndex = child.WoundTransitionOrdinal - 1;
            if (childIndex < 1 || childIndex >= woundRows.Count ||
                woundRows[childIndex - 1] != recorded || woundRows[childIndex] != child ||
                child.CourseId is not null || child.CourseMilestoneOrdinal is not null ||
                child.CycleKey is not null || child.PaymentFingerprint is not null ||
                child.WoundTransitionOrdinal != checked(primary.WoundTransitionOrdinal + 1) ||
                !HasExactTreatmentHistoryOutput(child) || healed.Owner != source.Owner ||
                healed.WoundId != source.WoundId || healed.Lifecycle != "healed" || healed.Care.State != "healed" ||
                healed.LastTransition.Kind != "heal" || healed.LastTransition.Turn != coordinates.Turn ||
                !identities.TryGetEntry(coordinates.WoundId, out var identity) ||
                identity.Domain != source.Classification.Domain || identity.CreatedAtTurn != source.Origin.CreatedAtTurn ||
                identity.CreatedEventRef != source.Origin.EventRef ||
                identity.LastTransitionOrdinal != woundRows[^1].WoundTransitionOrdinal) return false;
            // A historical display may follow immutable legacy/archive audit
            // rows. They never reopen or change the sealed healed snapshot.
            // The current guided-settlement wrapper retains its stricter latest-row rule.
            for (var index = childIndex + 1; index < woundRows.Count; index++)
            {
                var audit = woundRows[index];
                if (audit.WoundId != child.WoundId || audit.Kind is not ("legacy" or "archive") ||
                    audit.Terminal || audit.WoundTransitionOrdinal != checked(index + 1) ||
                    audit.BeforeFingerprint != child.AfterFingerprint || audit.AfterFingerprint != child.AfterFingerprint)
                    return false;
            }

            // Reuse the exact graph returned by wound reconstruction. Original
            // and newly allocated wound effects must have their real ordered
            // terminal identities; independent legacies may lawfully evolve.
            RequireTerminalWoundEffects(resolution, preparation, selected, treated, effects, effectIndex);
            before = treated;
            after = healed;
            return true;
        }
        catch (Exception failure) when (failure is InvalidDataException or InvalidOperationException or
                                       ArgumentException or JsonException or OverflowException or FormatException or
                                       NullReferenceException or KeyNotFoundException or IndexOutOfRangeException)
        {
            return false;
        }
    }

    private static bool HistoricalResultAgrees(MortalWoundTreatmentPersistedResult? retained,
        MortalWoundTreatmentPersistedResult expected) => retained is not null &&
        (ReferenceEquals(retained, expected) || JsonNode.DeepEquals(retained.ToCanonicalJson(), expected.ToCanonicalJson()));

    private static bool HasExactTreatmentHistoryOutput(WoundHistoryTransition row) =>
        row.ReadableSummary == WoundAcceptedTurnPlanner.TreatmentPublicationSummary &&
        row.OutputFingerprint == WoundHistoryState.ComputeOutputFingerprint(row.OperationKey, row.EventRef,
            WoundAcceptedTurnPlanner.TreatmentPublicationSummary);
}
