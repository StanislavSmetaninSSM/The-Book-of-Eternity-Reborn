using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedTurnAuthorityRegistry
{
    internal static bool RegisterHeldMortalDiagnosis(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        MortalWoundTreatmentAcceptedStateAuthority accepted, object capability, MortalWoundTreatmentAttemptRequest request, byte[] heldBytes) =>
        GetState(files, lease).RegisterHeldMortalDiagnosis(files, lease, accepted, capability, request, heldBytes);

    internal static bool DiagnosisPendingBeforeAgrees(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        MortalWoundTreatmentAcceptedStateAuthority accepted, object capability, MortalWoundTreatmentAttemptRequest request, byte[]? before) =>
        GetState(files, lease).DiagnosisPendingBeforeAgrees(files, lease, accepted, capability, request, before);

    internal static MortalWoundTreatmentAttemptRequest? ReadHeldMortalDiagnosis(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, MortalWoundTreatmentAcceptedStateAuthority accepted,
        MortalWoundTreatmentAttemptCoordinates coordinates) =>
        GetState(files, lease).ReadHeldMortalDiagnosis(files, lease, accepted, coordinates);

    private sealed partial class AcceptedTurnAuthorityState
    {
        private sealed class RetainedMortalDiagnosis
        {
            internal RetainedMortalDiagnosis(MortalWoundTreatmentAcceptedStateAuthority owner,
                MortalWoundTreatmentAttemptRequest request, byte[] held)
            {
                AcceptedFingerprint = owner.AcceptedStateFingerprint; SessionGeneration = owner.SessionGeneration;
                Request = request; HeldBytes = held.ToArray();
                OriginalCommandBytes = owner.ReadDiagnosisOriginalBytes(AcceptedMechanicsPlan.WoundCommandPath);
                var original = owner.ReadDiagnosisOriginalPending();
                OriginalPending = new(original.Existed, original.Bytes?.ToArray());
            }
            internal string AcceptedFingerprint { get; }
            internal string SessionGeneration { get; }
            internal MortalWoundTreatmentAttemptRequest Request { get; }
            internal byte[] HeldBytes { get; }
            internal byte[] OriginalCommandBytes { get; }
            internal CanonicalBeforeImage OriginalPending { get; }
            internal bool Matches(MortalWoundTreatmentAcceptedStateAuthority owner) =>
                owner.HasOriginalDiagnosisInput && owner.AcceptedStateFingerprint == AcceptedFingerprint && owner.SessionGeneration == SessionGeneration &&
                owner.ReadDiagnosisOriginalBytes(AcceptedMechanicsPlan.WoundCommandPath).AsSpan().SequenceEqual(OriginalCommandBytes);
        }
        private RetainedMortalDiagnosis? _retainedMortalDiagnosis;

        internal bool DiagnosisPendingBeforeAgrees(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            MortalWoundTreatmentAcceptedStateAuthority accepted, object capability, MortalWoundTreatmentAttemptRequest request, byte[]? before)
        {
            lock (_gate)
            {
                if (!MortalWoundTreatmentAcceptedStateAuthority.IsDiagnosisRetentionCapability(capability) ||
                    !ReferenceEquals(_mortalWoundTreatmentAcceptedState, accepted) || !accepted.IsLeaseBoundTo(files, lease) ||
                    request.Mode != "diagnosis" || !request.Coordinates.MatchesAcceptedState(accepted)) return false;
                if (_retainedMortalDiagnosis is { } previous && previous.Matches(accepted) && previous.Request.RequestFingerprint != request.RequestFingerprint)
                    return false;
                var expected = _retainedMortalDiagnosis is { } retained && retained.Matches(accepted)
                    ? retained.HeldBytes : accepted.ReadDiagnosisOriginalPending().Bytes;
                return expected is null ? before is null : before is not null && expected.AsSpan().SequenceEqual(before);
            }
        }

        private bool RetainedDiagnosisCurrentImagesAgree(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            MortalWoundTreatmentAcceptedStateAuthority accepted)
        {
            var retained = _retainedMortalDiagnosis;
            if (retained is null || !retained.Matches(accepted)) return true;
            var pending = files.ReadFileBytesSync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            var command = files.ReadFileBytesSync(lease, AcceptedMechanicsPlan.WoundCommandPath);
            return pending is not null && pending.AsSpan().SequenceEqual(retained.HeldBytes) && command is not null &&
                command.AsSpan().SequenceEqual(retained.OriginalCommandBytes);
        }

        internal bool RegisterHeldMortalDiagnosis(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            MortalWoundTreatmentAcceptedStateAuthority accepted, object capability, MortalWoundTreatmentAttemptRequest request, byte[] heldBytes)
        {
            lock (_gate)
            {
                if (!MortalWoundTreatmentAcceptedStateAuthority.IsDiagnosisRetentionCapability(capability) ||
                    !ReferenceEquals(_mortalWoundTreatmentAcceptedState, accepted) || !accepted.IsLeaseBoundTo(files, lease) ||
                    !_procedureClaimRecoveryCompleted || request.Mode != "diagnosis" || request.Coordinates.Purpose != "diagnosis" ||
                    !request.Coordinates.MatchesAcceptedState(accepted) || !MortalWoundTreatmentDetachedSealValidator.IsValid(request) ||
                    request.ModeAuthority is not MortalWoundProcedureCheckAuthority check ||
                    !check.HasLiveReservationAgreement(ProcedureReservationLiveCheckCapability, _procedureDice, _criticalReactions) ||
                    _openTreatmentPublicationReceipt is not null || _validatedTreatmentPublicationHandoff is not null ||
                    _treatmentPublicationRestartBlockerFingerprint is not null)
                    return false;
                var existing = _recoveredTreatmentRequests.Where(value => value.Coordinates.OperationKey == request.Coordinates.OperationKey).ToArray();
                if (existing.Length > 1 || existing.Length == 1 && existing[0].RequestFingerprint != request.RequestFingerprint) return false;
                var confirmed = _treatmentResources.ConfirmPersisted(TreatmentResourceRegistryCapability, new[] { request });
                if (!confirmed.IsValid) return false;
                if (existing.Length == 0)
                {
                    _recoveredTreatmentRequests = _recoveredTreatmentRequests.Append(request).ToArray();
                    _recoveredHeldTreatmentRequests = _recoveredHeldTreatmentRequests.Append(request).ToArray();
                }
                _retainedMortalDiagnosis = new(accepted, request, heldBytes);
                return true;
            }
        }

        internal MortalWoundTreatmentAttemptRequest? ReadHeldMortalDiagnosis(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, MortalWoundTreatmentAcceptedStateAuthority accepted,
            MortalWoundTreatmentAttemptCoordinates coordinates)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_mortalWoundTreatmentAcceptedState, accepted) || !accepted.IsLeaseBoundTo(files, lease) ||
                    !coordinates.MatchesAcceptedState(accepted) || !RetainedDiagnosisCurrentImagesAgree(files, lease, accepted))
                    throw new InvalidDataException("Diagnosis request lookup has no current accepted owner or exact retained input images.");
                var matches = _recoveredHeldTreatmentRequests.Where(value => value.Coordinates.OperationKey == coordinates.OperationKey).ToArray();
                if (matches.Length == 0) return null;
                if (matches.Length != 1 || matches[0].Mode != "diagnosis" ||
                    matches[0].Coordinates.CoordinatesFingerprint != coordinates.CoordinatesFingerprint ||
                    matches[0].ModeAuthority is not MortalWoundProcedureCheckAuthority check ||
                    !check.HasLiveReservationAgreement(ProcedureReservationLiveCheckCapability, _procedureDice, _criticalReactions))
                    throw new InvalidDataException("A retained diagnosis cannot change purpose, source or its exact die/Fate span.");
                var request = matches[0];
                var confirmed = _treatmentResources.ConfirmPersisted(TreatmentResourceRegistryCapability, new[] { request });
                if (!confirmed.IsValid) throw new InvalidDataException("Retained diagnosis resource agreement no longer exists.");
                return request;
            }
        }
    }
}
