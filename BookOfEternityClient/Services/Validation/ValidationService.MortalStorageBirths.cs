using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    // Candidate registration is revoked on every failure. Completion here admits
    // DATA only; the existing normalizer repeats completion with the common binding.
    private void ValidateAdmittedCurrentStorageBirths(MortalLocationAcceptedTurnPlan prepared,
        ValidationPendingTurnSnapshotManifest manifest, FileSystemManager.CanonicalWriteLease lease,
        List<ValidationIssue> issues)
    {
        var admitted = false;
        try
        {
            if (!MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(_fs, lease,
                    manifest.SessionId, manifest.ManifestPayloadHash, manifest.TurnNumber, out var snapshot) ||
                !snapshot.TryProjectItemPhase(out var phase, out var phaseIssues) || phase is null)
            {
                issues.Add(MortalLocationStorageBirthEvidence.Issue(MortalLocationIdentityState.StatePath,
                    "mortal_location_storage_birth_item_admission_missing", "exact admitted item phase, including empty births"));
                return;
            }
            var catalog = MortalItemCanonicalProjectionPlanner.BuildCatalog(phase.ItemPhaseAfterImages, true,
                MortalVehicleCargoProjection.BirthIds(snapshot.CloneRouteCatalog()));
            var source = new MortalStorageBirthSourceBinding(manifest.SessionId, manifest.RequestId,
                manifest.ManifestPayloadHash, manifest.TurnNumber, snapshot.ProjectionProofFingerprint);
            var finalized = MortalLocationStorageBirthEvidence.Finalize(prepared, phase, catalog,
                snapshot.CloneItemIdsByCreationRef(), source);
            issues.AddRange(finalized.Issues);
            admitted = finalized.IsValid;
        }
        finally
        {
            if (!admitted) MortalItemAcceptedTurnAuthority.InvalidateValidatedItems(_fs, lease);
        }
    }
}
