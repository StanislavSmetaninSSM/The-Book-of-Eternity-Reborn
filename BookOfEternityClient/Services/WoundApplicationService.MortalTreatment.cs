using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal static partial class WoundApplicationService
{
    internal sealed record MortalTreatmentAdmission(MortalWoundTreatmentAcceptedStateAuthority Owner,
        MortalWoundTreatmentAttemptRequest Request);

    /// <summary>
    /// Resolves the authentic final original into the existing mode owners, restoring
    /// an exact held request first. Retained-only intake never allocates a new attempt.
    /// </summary>
    internal static async Task<MortalTreatmentAdmission> ResolveMortalTreatmentInputAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, SpiritualSelfTreatmentStageContext frame, bool requireRetained = false)
    {
        var control = JsonSerializer.SerializeToNode(frame.Progression)?.Deserialize<ProgressionControl>() ??
            throw new InvalidDataException("Missing complete physical control.");
        var actualFrame = frame with { Progression = control };
        var bytes = await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath) ??
            throw new InvalidDataException("Missing immutable physical selection.");
        using var document = JsonDocument.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes));
        var issues = new List<ValidationIssue>();
        if (!MortalWoundTreatmentCommandCodec.HasNoDuplicateProperties(document.RootElement, AcceptedMechanicsPlan.WoundCommandPath, issues))
            throw new InvalidDataException("Duplicate physical selection fields.");
        var input = JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
        var selected = MortalWoundGuidedTreatmentInputCodec.Read(input);
        if (!MortalWoundGuidedTreatmentInputCodec.IsBound(input, actualFrame))
            throw new InvalidDataException("Physical selection differs from the actual final request/control.");
        var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportGuidedTreatmentCurrent(files, lease,
            selected.Context, selected.Selection.WoundId);
        if (!exported.IsValid || exported.Authority is not { } owner)
            throw new InvalidDataException("Fresh physical accepted-world ownership was refused: " +
                string.Join(";", exported.Issues.Select(issue => issue.Code + ": " + issue.Actual)));
        var manifestText = await files.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath) ??
            throw new InvalidDataException("Missing actual physical original manifest.");
        var manifest = StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>(manifestText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "physical original manifest") ??
            throw new InvalidDataException("Physical original manifest is not an object.");
        var computedHash = PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(manifest, LiveTurnPreparationService.ManifestHashJsonOptions,
            value => value.ManifestPayloadHash, (value, hash) => value.ManifestPayloadHash = hash);
        var preparedRole = owner.HasCurrentGuidedTreatmentInputRole(files, lease, actualFrame.SessionId, actualFrame.RequestId,
            actualFrame.Turn, manifest.ManifestPayloadHash, computedHash);
        var required = preparedRole ? WoundAcceptedTurnSnapshotContract.RequiredPaths.Where(path => path != AcceptedMechanicsPlan.WoundCommandPath).ToArray()
            : WoundAcceptedTurnSnapshotContract.RequiredPaths;
        if (!PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(manifest, value => value.Files,
            value => value.SnapshotFileHashes, value => value.RollbackBaselineFiles, required, out var missing))
            throw new InvalidDataException("Physical allocation lacks complete canonical rollback coverage: " + missing);
        var physical = selected.Selection.MortalTreatment!;
        var operation = "mortal_treatment_operation_" + MortalWoundGuidedTreatmentInputCodec.Fingerprint(input)["sha256:".Length..];
        var eventRef = owner.Binding.AcceptedEvents.First().EventRef;
        var coordinates = MortalWoundTreatmentAttemptCoordinates.CreateFromAcceptedState(owner, owner.CurrentWound,
            operation, physical.RouteId, eventRef);
        if (!coordinates.IsValid || coordinates.Coordinates is null)
            throw new InvalidDataException("The selected physical coordinates are not current canonical treatment coordinates.");
        var held = owner.ReadHeldGuidedTreatmentRequest(coordinates.Coordinates);
        if (held is not null)
        {
            owner.RequireOriginalGuidedTreatmentRequest(held);
            return new(owner, held);
        }
        if (requireRetained) throw new InvalidDataException("Post-GM physical intake requires its exact pre-GM held request.");
        var before = owner.RequireUnheldGuidedPendingImage();
        var history = new WoundHistoryParseResult(owner.History, Array.Empty<ValidationIssue>());
        var prepared = physical.Mode switch
        {
            "procedure" => MortalWoundTreatmentPlanner.PrepareProcedureRequest(owner, history, owner.CurrentWound, operation, physical.RouteId, eventRef),
            "course" => MortalWoundTreatmentPlanner.PrepareCourseMilestoneRequest(owner, history, owner.CurrentWound, operation, physical.RouteId, eventRef),
            "guaranteed" => MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(owner, history, owner.CurrentWound, operation, physical.RouteId, eventRef),
            _ => throw new InvalidDataException("Unknown physical mode.")
        };
        if (!prepared.IsValid || prepared.Request is not { } request)
            throw new InvalidDataException("The current physical mode/requirements refused allocation: " +
                string.Join(";", prepared.Issues.Select(issue => issue.Code + ": " + issue.Actual)));
        try
        {
            owner.RequireOriginalGuidedTreatmentRequest(request);
            await owner.RetainGuidedTreatmentRequestAsync(request);
            return new(owner, request);
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException)
        {
            // Only a proven unchanged original pending image permits provisional
            // cleanup. A durable, partial or uncertain hold is never released.
            var current = await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            if (before.Bytes is { } expected ? current is not null && expected.AsSpan().SequenceEqual(current) : current is null)
                request.RollbackNewProvisionalClaims(owner);
            throw;
        }
    }
}
