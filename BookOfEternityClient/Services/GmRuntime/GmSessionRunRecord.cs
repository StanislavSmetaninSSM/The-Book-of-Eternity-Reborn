namespace BookOfEternityClient.Services.GmRuntime;

internal enum GmSessionRunBackend { Unspecified, WindowsJob, LinuxSupervisor }
internal enum GmSessionRunDisposition { Unspecified, Prepared, Running, Stopping, Uncertain, Stopped }
internal enum GmSessionRunStopKind { Unspecified, OwnedScopeEmpty, VerifiedHostReboot }

/// <summary>Immutable persistent-main identity; it describes one ownership slot, not every writer on the root.</summary>
internal sealed record GmSessionRunIdentity(string RootKey, string RunId, string GenerationId, long Epoch,
    GmSessionRunBackend Backend, string HostInstanceId, string BootId);

/// <summary>A trusted adapter observation; serialization does not authenticate stop or reboot.</summary>
internal sealed record GmSessionRunStopEvidence(GmSessionRunIdentity Identity, GmSessionRunStopKind Kind, string ObservedBootId);

/// <summary>Durable schema only. Persistence and canonical/process consumer integration are separate prerequisites.</summary>
internal sealed record GmSessionRunRecord(int SchemaVersion, GmSessionRunIdentity Identity,
    GmSessionRunDisposition Disposition, GmSessionRunStopEvidence? StopEvidence);

// Deliberately permissive RED baseline; not wired to any runtime consumer.
internal static class GmSessionRunTransitions
{
    internal static GmSessionRunRecord MarkRunning(GmSessionRunRecord record, GmSessionRunIdentity owner) =>
        record with { Disposition = GmSessionRunDisposition.Running };
    internal static GmSessionRunRecord MarkStopping(GmSessionRunRecord record, GmSessionRunIdentity owner) =>
        record with { Disposition = GmSessionRunDisposition.Stopping };
    internal static GmSessionRunRecord MarkUncertain(GmSessionRunRecord record) =>
        record with { Disposition = GmSessionRunDisposition.Uncertain };
    internal static GmSessionRunRecord ConfirmStopped(GmSessionRunRecord record, GmSessionRunStopEvidence evidence) =>
        record with { Disposition = GmSessionRunDisposition.Stopped, StopEvidence = evidence };
    internal static GmSessionRunRecord InterpretCold(GmSessionRunRecord record) => record;
}
