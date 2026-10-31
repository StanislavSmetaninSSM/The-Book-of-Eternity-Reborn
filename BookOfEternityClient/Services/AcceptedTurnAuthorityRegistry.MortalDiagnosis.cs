using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedTurnAuthorityRegistry
{
    internal static bool RegisterHeldMortalDiagnosis(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        MortalWoundTreatmentAcceptedStateAuthority accepted, object capability, MortalWoundTreatmentAttemptRequest request) =>
        GetState(files, lease).RegisterHeldMortalDiagnosis(files, lease, accepted, capability, request);

    internal static MortalWoundTreatmentAttemptRequest? ReadHeldMortalDiagnosis(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, MortalWoundTreatmentAcceptedStateAuthority accepted,
        MortalWoundTreatmentAttemptCoordinates coordinates) =>
        GetState(files, lease).ReadHeldMortalDiagnosis(files, lease, accepted, coordinates);

    private sealed partial class AcceptedTurnAuthorityState
    {
        internal bool RegisterHeldMortalDiagnosis(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            MortalWoundTreatmentAcceptedStateAuthority accepted, object capability, MortalWoundTreatmentAttemptRequest request)
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
                    !coordinates.MatchesAcceptedState(accepted)) throw new InvalidDataException("Diagnosis request lookup has no current accepted owner.");
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
