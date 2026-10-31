using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    // Classify DATA only; the resolver below still requires the actual signed
    // source and the attempt already retained by its pre-GM accepted owner.
    private static bool IsMortalDiagnosisInput(string? text)
    {
        if (text is null) return false;
        try
        {
            return MortalWoundDiagnosisInputCodec.IsKind(StrictJsonAuthority.Deserialize<JsonObject>(
                text.TrimStart('\uFEFF'), new JsonSerializerOptions(), "Mortal diagnosis input"));
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException) { return false; }
    }

    private async Task<WoundApplicationService.MortalDiagnosisAdmission> ReadRetainedMortalDiagnosisAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidationPendingTurnSnapshotManifest manifest)
    {
        var text = await _fs.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath) ??
            throw new InvalidDataException("Mortal diagnosis has no actual final request.");
        var request = StrictJsonAuthority.Deserialize<TurnRequest>(text.TrimStart('\uFEFF'),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "Mortal diagnosis final request") ??
            throw new InvalidDataException("Mortal diagnosis final request is not an object.");
        if (request.SessionId != manifest.SessionId || request.RequestId != manifest.RequestId ||
            request.TurnNumber != manifest.TurnNumber || request.ProgressionControl is null)
            throw new InvalidDataException("Mortal diagnosis final request differs from its validated original.");
        return await WoundApplicationService.ResolveMortalDiagnosisInputAsync(_fs, lease,
            new(request.SessionId, request.RequestId, request.TurnNumber, request.ProgressionControl), requireRetained: true);
    }

    private async Task ValidateMortalDiagnosisDynamicCoverageAsync(FileSystemManager.CanonicalWriteLease lease,
        AcceptedMechanicsPlan plan, MortalWoundTreatmentResourcePublicationAuthority authority,
        List<ValidationIssue> issues)
    {
        if (authority.RequestAuthority.Mode != "diagnosis") return;
        try
        {
            var owner = plan.MortalDiagnosisContinuation;
            if (owner is null || !ReferenceEquals(owner, authority.ContinuationAuthority) ||
                !ReferenceEquals(owner.Owner, authority.AcceptedStateAuthority) ||
                owner.Outcome.Request.RequestFingerprint != authority.RequestFingerprint)
                throw new InvalidDataException("The diagnosis dynamic plan has no matching actual private continuation/owner.");
            var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
            if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest is not { } manifest)
                throw new InvalidDataException("Diagnosis dynamic coverage lost its validated original frame.");
            var required = WoundAcceptedTurnSnapshotContract.BuildRequiredPaths(plan.TouchedPaths);
            if (owner.Owner.HasCurrentDiagnosisPreparedCommandRole(_fs, lease, owner.Outcome.Request,
                    manifest.SessionId, manifest.RequestId, manifest.TurnNumber,
                    manifest.ManifestPayloadHash, ComputeManifestPayloadHash(manifest)))
                required = required.Where(path => path != AcceptedMechanicsPlan.WoundCommandPath).ToArray();
            if (!PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(manifest,
                    value => value.Files, value => value.SnapshotFileHashes, value => value.RollbackBaselineFiles,
                    required, out var missing))
                issues.Add(WoundIssue(missing ?? AcceptedMechanicsPlan.WoundCommandPath,
                    "wound_materialization_snapshot_before_image_missing",
                    "exact signed rollback evidence for every diagnosis common-plan canonical path",
                    "missing, contradictory or incompletely registered before-image", IssueCategory.ClientOwnedSurface));
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (InvalidDataException or InvalidOperationException or ArgumentException or JsonException))
        {
            issues.Add(WoundIssue(AcceptedMechanicsPlan.WoundCommandPath, "mortal_diagnosis_dynamic_owner_invalid",
                "the exact current diagnosis owner, request, full manifest and private plan", failure.Message));
        }
        if (issues.Any(value => value.Severity == IssueSeverity.Error))
        {
            AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, lease);
            EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs, lease);
        }
    }
}
