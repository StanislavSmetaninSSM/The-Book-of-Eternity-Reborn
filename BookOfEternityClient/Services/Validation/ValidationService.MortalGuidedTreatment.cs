using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static bool IsMortalGuidedTreatmentInput(string? text)
    {
        if (text is null) return false;
        try { return MortalWoundGuidedTreatmentInputCodec.IsKind(StrictJsonAuthority.Deserialize<JsonObject>(
            text.TrimStart('\uFEFF'), new JsonSerializerOptions(), "physical guided input")); }
        catch (Exception failure) when (failure is JsonException or InvalidDataException) { return false; }
    }

    private async Task<WoundApplicationService.MortalTreatmentAdmission> ReadRetainedMortalGuidedTreatmentAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidationPendingTurnSnapshotManifest manifest)
    {
        var text = await _fs.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath) ??
            throw new InvalidDataException("Physical intake has no final request.");
        var request = StrictJsonAuthority.Deserialize<TurnRequest>(text.TrimStart('\uFEFF'),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "physical final request") ??
            throw new InvalidDataException("Physical final request is not an object.");
        if (request.SessionId != manifest.SessionId || request.RequestId != manifest.RequestId ||
            request.TurnNumber != manifest.TurnNumber || request.ProgressionControl is null)
            throw new InvalidDataException("Physical intake request differs from its validated original.");
        return await WoundApplicationService.ResolveMortalTreatmentInputAsync(_fs, lease,
            new(request.SessionId, request.RequestId, request.TurnNumber, request.ProgressionControl), requireRetained: true);
    }

    private async Task ValidateGuidedMortalTreatmentDynamicCoverageAsync(FileSystemManager.CanonicalWriteLease lease,
        AcceptedMechanicsPlan plan, MortalWoundTreatmentResourcePublicationAuthority authority, List<ValidationIssue> issues)
    {
        if (!authority.AcceptedStateAuthority.HasOriginalGuidedTreatmentInput) return;
        try
        {
            var companion = plan.WoundStageBundle?.MortalProcedureContinuation;
            if (companion is null || !ReferenceEquals(companion, authority.ContinuationAuthority) ||
                !WoundAcceptedTurnPlanner.TryReadMortalProcedurePublication(companion, out var publication) ||
                !ReferenceEquals(publication.ReservationAuthority, authority.PublicationReservationAuthority))
                throw new InvalidDataException("Physical dynamic plan has no exact private prepared companion.");
            var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync(lease);
            if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest is not { } manifest)
                throw new InvalidDataException("Physical dynamic coverage has no usable actual original.");
            var current = authority.AcceptedStateAuthority.IsLeaseBoundTo(_fs, lease)
                ? new WoundApplicationService.MortalTreatmentAdmission(authority.AcceptedStateAuthority, authority.RequestAuthority)
                : await ReadRetainedMortalGuidedTreatmentAsync(lease, manifest);
            if (current.Request.RequestFingerprint != authority.RequestFingerprint ||
                !ReferenceEquals(current.Owner, authority.AcceptedStateAuthority) &&
                !AcceptedTurnAuthorityRegistry.CanReuseMortalDiagnosisPublication(_fs, lease, current.Owner, current.Request, authority))
                throw new InvalidDataException("Physical reentry cannot substitute another held request or common plan.");
            var required = WoundAcceptedTurnSnapshotContract.BuildRequiredPaths(plan.TouchedPaths);
            if (current.Owner.HasCurrentGuidedTreatmentPreparedCommandRole(_fs, lease, current.Request,
                manifest.SessionId, manifest.RequestId, manifest.TurnNumber, manifest.ManifestPayloadHash, ComputeManifestPayloadHash(manifest)))
                required = required.Where(path => path != AcceptedMechanicsPlan.WoundCommandPath).ToArray();
            if (!PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(manifest, value => value.Files,
                value => value.SnapshotFileHashes, value => value.RollbackBaselineFiles, required, out var missing))
                throw new InvalidDataException("Physical common plan lacks exact canonical rollback coverage: " + missing);
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (InvalidDataException or InvalidOperationException or ArgumentException or JsonException))
        { issues.Add(WoundIssue(AcceptedMechanicsPlan.WoundCommandPath, "mortal_guided_treatment_dynamic_owner_invalid",
            "the exact current physical owner, retained request, private common plan and complete original coverage", failure.Message)); }
        if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
        {
            AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, lease);
            EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs, lease);
        }
    }
}
