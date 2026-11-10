using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedTurnAuthorityRegistry
{
    internal static bool PrepareMortalDiagnosisOuterRollback(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease) =>
        GetState(files, lease).PrepareMortalDiagnosisOuterRollback(files, lease);
    internal static void CompleteMortalDiagnosisOuterRollback(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease) =>
        GetState(files, lease).CompleteMortalDiagnosisOuterRollback(files, lease);
    internal static bool HasMortalDiagnosisOuterRollbackCheckpoint(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease) =>
        GetState(files, lease).HasMortalDiagnosisOuterRollbackCheckpoint();
    internal static Task ResumeMortalDiagnosisAfterOuterRollbackAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, SpiritualSelfTreatmentStageContext frame) =>
        GetState(files, lease).ResumeMortalDiagnosisAfterOuterRollbackAsync(files, lease, frame);

    private sealed partial class AcceptedTurnAuthorityState
    {
        internal bool HasMortalDiagnosisOuterRollbackCheckpoint()
        {
            lock (_gate) return _retainedMortalDiagnosis is { OuterRollbackPhase: 2 };
        }

        private static bool DiagnosisRetrySame(byte[]? actual, byte[]? expected) =>
            actual is null ? expected is null : expected is not null && actual.AsSpan().SequenceEqual(expected);

        private void RequireDiagnosisRetryClaims(RetainedMortalDiagnosis retained)
        {
            if (!ReferenceEquals(retained, _retainedMortalDiagnosis) || _openTreatmentPublicationReceipt is not null ||
                _treatmentPublicationRestartBlockerFingerprint is not null ||
                !RetainedMortalRequestFamilyAgrees(retained.Owner, retained.Request) || !HasLiveRetainedMode(retained.Request) ||
                !_treatmentResources.ConfirmPersisted(TreatmentResourceRegistryCapability, new[] { retained.Request }).IsValid)
                throw new InvalidDataException("Retry checkpoint lost its exact produced diagnosis span, Fate or confirmed resource hold.");
        }

        private static PendingTurnSnapshotObservedReadResult RequireDiagnosisRetryOriginal(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            RetainedMortalDiagnosis retained)
        {
            if (!files.IsCurrentSessionGeneration(lease, retained.SessionGeneration))
                throw new InvalidDataException("Diagnosis retry belongs to another physical generation.");
            var original = PendingTurnSnapshotReader.ReadObservedCurrent(files, lease, new[] { AcceptedMechanicsPlan.WoundCommandPath, ProgressionScheduleService.SchedulePath });
            var coordinates = retained.Request.Coordinates;
            if (!original.Success || original.Snapshot is not { } snapshot || snapshot.SessionId != coordinates.SessionId ||
                snapshot.RequestId != coordinates.RequestId || snapshot.TurnNumber != coordinates.Turn ||
                snapshot.SnapshotToken != coordinates.SnapshotToken || snapshot.Realm != coordinates.Realm ||
                !snapshot.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath).AsSpan().SequenceEqual(retained.OriginalCommandBytes) ||
                !snapshot.ReadRequiredBytes(ProgressionScheduleService.SchedulePath).AsSpan().SequenceEqual(retained.PreparedScheduleBytes))
                throw new InvalidDataException("Diagnosis retry lost the exact actual signed original selection/source.");
            return original;
        }

        internal bool PrepareMortalDiagnosisOuterRollback(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            lock (_gate)
            {
                if (_retainedMortalDiagnosis is not { } retained) return false;
                RequireDiagnosisRetryOriginal(files, lease, retained);
                RequireDiagnosisRetryClaims(retained); // An open C4 receipt must compensate/rearm first.
                if (!DiagnosisRetrySame(files.ReadFileBytesSync(lease, AcceptedMechanicsPlan.WoundCommandPath), retained.OriginalCommandBytes) ||
                    !DiagnosisRetrySame(files.ReadFileBytesSync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath), retained.HeldBytes))
                    throw new InvalidDataException("Retryable outer rollback must begin from the exact already-held request image.");
                retained.OuterRollbackPhase = 1;
                return true;
            }
        }

        internal void CompleteMortalDiagnosisOuterRollback(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            lock (_gate)
            {
                var retained = _retainedMortalDiagnosis ?? throw new InvalidDataException("The owned retry checkpoint disappeared.");
                if (retained.OuterRollbackPhase != 1) throw new InvalidDataException("No owned outer rollback is in progress.");
                RequireDiagnosisRetryOriginal(files, lease, retained);
                RequireDiagnosisRetryClaims(retained);
                if (!retained.Owner.DiagnosisOriginalCanonicalImagesAgree(files, lease) ||
                    !DiagnosisRetrySame(files.ReadFileBytesSync(lease, AcceptedMechanicsPlan.WoundCommandPath), retained.OriginalCommandBeforeImage.Bytes) ||
                    !DiagnosisRetrySame(files.ReadFileBytesSync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath), retained.OriginalPending.Bytes))
                    throw new InvalidDataException("Outer rollback did not restore the exact original canonical images and command/pending absence or bytes.");
                retained.OuterRollbackPhase = 2;
            }
        }

        internal async Task ResumeMortalDiagnosisAfterOuterRollbackAsync(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, SpiritualSelfTreatmentStageContext frame)
        {
            RetainedMortalDiagnosis retained;
            lock (_gate) retained = _retainedMortalDiagnosis ?? throw new InvalidDataException("No owned diagnosis retry checkpoint.");
            // Validate once, then recheck the same issued observations at the
            // exact-byte publication fences; no repeated signature/JSON parsing.
            var original = RequireDiagnosisRetryOriginal(files, lease, retained);
            void RequireCurrent()
            {
                lock (_gate)
                {
                    if (retained.OuterRollbackPhase != 2) throw new InvalidDataException("Diagnosis retry checkpoint is not in its restored-original phase.");
                    original.Evidence!.RequireCurrent(files, lease, original.Snapshot!);
                    RequireDiagnosisRetryClaims(retained);
                    var input = retained.Owner.ReadDiagnosisOriginalInput();
                    var bound = retained.Owner.HasOriginalGuidedTreatmentInput
                        ? MortalWoundGuidedTreatmentInputCodec.IsBound(input, frame)
                        : MortalWoundDiagnosisInputCodec.IsBound(input, frame);
                    if (!bound ||
                        !retained.Owner.DiagnosisOriginalCanonicalImagesAgree(files, lease) ||
                        !DiagnosisRetrySame(files.ReadFileBytesSync(lease, AcceptedMechanicsPlan.WoundCommandPath), retained.OriginalCommandBeforeImage.Bytes) ||
                        !DiagnosisRetrySame(files.ReadFileBytesSync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath), retained.OriginalPending.Bytes))
                        throw new InvalidDataException("Diagnosis retry differs from the current original control, source, or restored input images.");
                }
            }
            RequireCurrent();
            // One existing trusted-local byte publication, never a new dice or
            // reservation operation. The original before maps remain untouched.
            var writes = new[]
            {
                new CanonicalLocalFileChange(AcceptedMechanicsPlan.WoundCommandPath,
                    retained.OriginalCommandBeforeImage.Bytes, retained.OriginalCommandBytes),
                new CanonicalLocalFileChange(WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                    retained.OriginalPending.Bytes, retained.HeldBytes),
                new CanonicalLocalFileChange(ProgressionScheduleService.SchedulePath,
                    retained.OriginalScheduleBeforeImage.Bytes, retained.PreparedScheduleBytes)
            };
            files.RequireCommittedLocalPublication(await files.PublishLocalFilesAsync(lease, writes,
                validatePreparedNamespace: () => { RequireCurrent(); return Task.CompletedTask; }));
            lock (_gate)
            {
                RequireDiagnosisRetryClaims(retained);
                if (!DiagnosisRetrySame(files.ReadFileBytesSync(lease, AcceptedMechanicsPlan.WoundCommandPath), retained.OriginalCommandBytes) ||
                    !DiagnosisRetrySame(files.ReadFileBytesSync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath), retained.HeldBytes) ||
                    !DiagnosisRetrySame(files.ReadFileBytesSync(lease, ProgressionScheduleService.SchedulePath), retained.PreparedScheduleBytes))
                    throw new CoordinatedStatePublicationUncertainException(new InvalidDataException("Restored diagnosis retry input readback differs."));
                retained.OuterRollbackPhase = 0;
            }
        }
    }
}
