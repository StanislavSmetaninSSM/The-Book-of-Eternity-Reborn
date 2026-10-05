namespace BookOfEternityClient.Services.GmRuntime;

internal enum GmSessionRunOperation { Unspecified, DiagnosticRead, QuiescentMutation, StartRun, ActiveRunMutation }
internal enum GmSessionRunAdmissionDecision { Blocked, SlotConditionSatisfied }

/// <summary>Trusted current target; its backend controls root comparison, independently of stored data.</summary>
internal sealed record GmSessionRunTarget(string RootKey, GmSessionRunBackend Backend, string? GenerationId);

/// <summary>Main-slot condition only. A satisfied decision never grants whole-root mutation authority.</summary>
internal static class GmSessionRunAdmission
{
    /// <summary>
    /// Evaluates an observation at the future mutation fence, not a reusable authority token.
    /// Active callers supply an independently authenticated live identity, never one copied from decoded evidence.
    /// DiagnosticRead is nonmutating: it must not invoke recovery writers. Other worker owners are separate conditions.
    /// </summary>
    internal static GmSessionRunAdmissionDecision Evaluate(GmSessionRunObservation? observation,
        GmSessionRunTarget? target, GmSessionRunOperation operation, GmSessionRunIdentity? caller = null)
    {
        if (operation == GmSessionRunOperation.DiagnosticRead) return GmSessionRunAdmissionDecision.SlotConditionSatisfied;
        if (observation is null || target is null || !GmSessionRunValidation.IsRootKey(target.RootKey) ||
            !GmSessionRunValidation.IsBackend(target.Backend) ||
            target.GenerationId is not null && !GmSessionRunValidation.IsId(target.GenerationId, allowEmpty: true))
            return GmSessionRunAdmissionDecision.Blocked;
        if (observation.Kind is not (GmSessionRunObservationKind.Missing or GmSessionRunObservationKind.Valid))
            return GmSessionRunAdmissionDecision.Blocked;
        var record = observation.Record;
        if (observation.Kind == GmSessionRunObservationKind.Valid)
        {
            try { GmSessionRunValidation.Validate(record); }
            catch (InvalidDataException) { return GmSessionRunAdmissionDecision.Blocked; }
            if (!MatchesTarget(record!.Identity, target, requireGeneration: false)) return GmSessionRunAdmissionDecision.Blocked;
        }
        var quiescent = observation.Kind == GmSessionRunObservationKind.Missing || record?.Disposition == GmSessionRunDisposition.Stopped;
        if (operation == GmSessionRunOperation.QuiescentMutation) return Decision(quiescent);
        if (operation is not (GmSessionRunOperation.StartRun or GmSessionRunOperation.ActiveRunMutation))
            return GmSessionRunAdmissionDecision.Blocked;
        try { GmSessionRunValidation.ValidateIdentity(caller); }
        catch (InvalidDataException) { return GmSessionRunAdmissionDecision.Blocked; }
        if (!MatchesTarget(caller!, target, requireGeneration: true)) return GmSessionRunAdmissionDecision.Blocked;
        if (operation == GmSessionRunOperation.ActiveRunMutation)
            return Decision(record?.Disposition == GmSessionRunDisposition.Running &&
                GmSessionRunValidation.IdentityMatches(record.Identity, caller!));
        if (!quiescent) return GmSessionRunAdmissionDecision.Blocked;
        return Decision(record is null ? caller!.Epoch == 1 :
            record.Identity.Epoch < long.MaxValue && caller!.Epoch == record.Identity.Epoch + 1 && caller.RunId != record.Identity.RunId);
    }

    // The current target's validated backend selects semantics only after backend equality.
    private static bool MatchesTarget(GmSessionRunIdentity identity, GmSessionRunTarget target, bool requireGeneration) =>
        identity.Backend == target.Backend && GmSessionRunValidation.RootMatches(identity.RootKey, target.RootKey, target.Backend) &&
        (!requireGeneration || target.GenerationId is not null && identity.GenerationId == target.GenerationId);

    private static GmSessionRunAdmissionDecision Decision(bool allowed) => allowed
        ? GmSessionRunAdmissionDecision.SlotConditionSatisfied : GmSessionRunAdmissionDecision.Blocked;
}
