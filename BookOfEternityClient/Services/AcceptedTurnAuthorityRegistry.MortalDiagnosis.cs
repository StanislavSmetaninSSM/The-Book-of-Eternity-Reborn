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

    internal static bool CanReuseMortalDiagnosisPublication(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        MortalWoundTreatmentAcceptedStateAuthority accepted, MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResourcePublicationAuthority publication) =>
        GetState(files, lease).CanReuseMortalDiagnosisPublication(files, lease, accepted, request, publication);

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
                Owner = owner;
                AcceptedFingerprint = owner.AcceptedStateFingerprint; SessionGeneration = owner.SessionGeneration;
                Request = request; HeldBytes = held.ToArray();
                OriginalCommandBytes = owner.ReadDiagnosisOriginalBytes(AcceptedMechanicsPlan.WoundCommandPath);
                OriginalCommandBeforeImage = owner.ReadDiagnosisOriginalCommandBeforeImage();
                OriginalScheduleBeforeImage = owner.ReadDiagnosisOriginalScheduleBeforeImage();
                PreparedScheduleBytes = owner.ReadDiagnosisOriginalBytes(ProgressionScheduleService.SchedulePath);
                var original = owner.ReadDiagnosisOriginalPending();
                OriginalPending = new(original.Existed, original.Bytes?.ToArray());
            }
            internal MortalWoundTreatmentAcceptedStateAuthority Owner { get; }
            internal CanonicalBeforeImage OriginalCommandBeforeImage { get; }
            internal int OuterRollbackPhase { get; set; }
            internal string AcceptedFingerprint { get; }
            internal string SessionGeneration { get; }
            internal MortalWoundTreatmentAttemptRequest Request { get; set; }
            internal CanonicalBeforeImage OriginalScheduleBeforeImage { get; }
            internal byte[] PreparedScheduleBytes { get; }
            internal byte[] HeldBytes { get; }
            internal byte[] OriginalCommandBytes { get; }
            internal CanonicalBeforeImage OriginalPending { get; }
            internal bool Matches(MortalWoundTreatmentAcceptedStateAuthority owner) =>
                owner.HasOriginalDiagnosisInput && owner.AcceptedStateFingerprint == AcceptedFingerprint && owner.SessionGeneration == SessionGeneration &&
                owner.ReadDiagnosisOriginalBytes(AcceptedMechanicsPlan.WoundCommandPath).AsSpan().SequenceEqual(OriginalCommandBytes);
        }
        private RetainedMortalDiagnosis? _retainedMortalDiagnosis;

        // Only the existing successful recovery path may replace this private
        // request reference. Immutable original DATA and rollback phase survive;
        // old reservation objects are never made live again.
        private bool RebindRetainedDiagnosisAfterRecovery(MortalWoundTreatmentAcceptedStateAuthority accepted)
        {
            if (_retainedMortalDiagnosis is not { } retained) return true;
            if (!retained.Matches(accepted) || !retained.Request.Coordinates.MatchesAcceptedState(accepted) ||
                retained.Request.ModeAuthority is not MortalWoundProcedureCheckAuthority originalCheck)
                return false;
            var recovered = _recoveredHeldTreatmentRequests.Where(request =>
                request.Coordinates.OperationKey == retained.Request.Coordinates.OperationKey).ToArray();
            if (recovered.Length != 1) return false;
            var current = recovered[0];
            if (current.Mode != "diagnosis" || current.Coordinates.Purpose != "diagnosis" ||
                !current.Coordinates.MatchesAcceptedState(accepted) ||
                current.Coordinates.CoordinatesFingerprint != retained.Request.Coordinates.CoordinatesFingerprint ||
                current.RequestFingerprint != retained.Request.RequestFingerprint ||
                current.RouteSourceWoundFingerprint != retained.Request.RouteSourceWoundFingerprint ||
                current.RequirementAuthority.AuthorityFingerprint != retained.Request.RequirementAuthority.AuthorityFingerprint ||
                current.ResourceAuthority.AuthorityFingerprint != retained.Request.ResourceAuthority.AuthorityFingerprint ||
                !MortalWoundTreatmentDetachedSealValidator.IsValid(current) ||
                current.ModeAuthority is not MortalWoundProcedureCheckAuthority currentCheck ||
                currentCheck.AuthorityFingerprint != originalCheck.AuthorityFingerprint ||
                currentCheck.AcceptedStateFingerprint != originalCheck.AcceptedStateFingerprint ||
                !currentCheck.SourceIndices.SequenceEqual(originalCheck.SourceIndices) ||
                !currentCheck.SourceRolls.SequenceEqual(originalCheck.SourceRolls) ||
                currentCheck.PreparedCriticalReaction?.PreparedReactionFingerprint != originalCheck.PreparedCriticalReaction?.PreparedReactionFingerprint ||
                !currentCheck.HasLiveReservationAgreement(ProcedureReservationLiveCheckCapability, _procedureDice, _criticalReactions) ||
                !_treatmentResources.ConfirmPersisted(TreatmentResourceRegistryCapability, new[] { current }).IsValid)
                return false;
            retained.Request = current;
            return true;
        }

        internal bool CanReuseMortalDiagnosisPublication(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            MortalWoundTreatmentAcceptedStateAuthority accepted, MortalWoundTreatmentAttemptRequest request,
            MortalWoundTreatmentResourcePublicationAuthority publication)
        {
            lock (_gate)
            {
                var handoff = _validatedTreatmentPublicationHandoff;
                return handoff is not null && ReferenceEquals(handoff.PublicationAuthority, publication) &&
                    ReferenceEquals(_mortalWoundTreatmentAcceptedState, accepted) && accepted.IsLeaseBoundTo(files, lease) &&
                    CurrentTreatmentPublicationAcceptedStateAgrees(accepted) && CurrentTreatmentPublicationAcceptedStateAgrees(publication.AcceptedStateAuthority) &&
                    publication.ContinuationAuthority is WoundAcceptedTurnPlanner.MortalDiagnosisContinuation &&
                    publication.RequestAuthority.Mode == "diagnosis" && request.Mode == "diagnosis" &&
                    publication.AcceptedStateAuthority.AcceptedStateFingerprint == accepted.AcceptedStateFingerprint &&
                    publication.RequestFingerprint == request.RequestFingerprint && request.Coordinates.MatchesAcceptedState(accepted) &&
                    RetainedDiagnosisCurrentImagesAgree(files, lease, accepted) &&
                    ProcedurePublicationClaimsAgree(publication, handoff.ProcedureClaimProof) &&
                    _treatmentResources.ProbeConfirmed(TreatmentResourceRegistryCapability, request, publication.Finalization).IsValid;
            }
        }

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
