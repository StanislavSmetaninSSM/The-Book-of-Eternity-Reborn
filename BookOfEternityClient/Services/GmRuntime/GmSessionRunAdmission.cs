namespace BookOfEternityClient.Services.GmRuntime;

internal enum GmSessionRunOperation { Unspecified, DiagnosticRead, QuiescentMutation, StartRun, ActiveRunMutation }
internal enum GmSessionRunAdmissionDecision { Blocked, SlotConditionSatisfied }

/// <summary>Trusted current target; its backend controls root comparison, independently of stored data.</summary>
internal sealed record GmSessionRunTarget(string RootKey, GmSessionRunBackend Backend, string? GenerationId);

/// <summary>Main-slot condition only. A satisfied decision never grants whole-root mutation authority.</summary>
internal static class GmSessionRunAdmission
{
    // Deliberately permissive RED baseline; later integration must compose other owners under the actual mutation fence.
    internal static GmSessionRunAdmissionDecision Evaluate(GmSessionRunObservation? observation,
        GmSessionRunTarget target, GmSessionRunOperation operation, GmSessionRunIdentity? caller = null)
    {
        var allowed = operation == GmSessionRunOperation.DiagnosticRead || observation != null &&
            (operation is GmSessionRunOperation.QuiescentMutation or GmSessionRunOperation.StartRun
                ? observation.Kind == GmSessionRunObservationKind.Missing || observation.Record?.Disposition == GmSessionRunDisposition.Stopped
                : operation == GmSessionRunOperation.ActiveRunMutation && observation.Record?.Disposition == GmSessionRunDisposition.Running);
        return allowed ? GmSessionRunAdmissionDecision.SlotConditionSatisfied : GmSessionRunAdmissionDecision.Blocked;
    }
}
