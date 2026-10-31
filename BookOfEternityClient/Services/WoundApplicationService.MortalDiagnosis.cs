using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class WoundApplicationService
{
    internal sealed record MortalDiagnosisAdmission(MortalWoundTreatmentAcceptedStateAuthority Owner,
        MortalWoundTreatmentAttemptRequest Request);

    // Called only after the actual final request and validated original exist.
    // This resolves and retains one ordinary procedure attempt; menu DATA and
    // caller-supplied seals cannot issue any part of this authority.
    internal static async Task<MortalDiagnosisAdmission> ResolveMortalDiagnosisInputAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, SpiritualSelfTreatmentStageContext frame)
    {
        var ownedControl = JsonSerializer.SerializeToNode(frame.Progression)?.Deserialize<BookOfEternityClient.Models.ProgressionControl>()
            ?? throw new InvalidDataException("Missing complete Mortal diagnosis control.");
        var ownedFrame = frame with { Progression = ownedControl };
        var bytes = await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath) ??
            throw new InvalidDataException("The exact staged Mortal diagnosis input is missing.");
        using var document = JsonDocument.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes));
        var issues = new List<ValidationIssue>();
        if (!MortalWoundTreatmentCommandCodec.HasNoDuplicateProperties(document.RootElement, AcceptedMechanicsPlan.WoundCommandPath, issues))
            throw new InvalidDataException("Mortal diagnosis input has duplicate fields.");
        var input = JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
        var selected = MortalWoundDiagnosisInputCodec.Read(input);
        if (!MortalWoundDiagnosisInputCodec.IsBound(input, ownedFrame))
            throw new InvalidDataException("Mortal diagnosis input differs from the actual final request/control.");
        var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportDiagnosisCurrent(files, lease,
            selected.Context, selected.Selection.WoundId);
        if (!exported.IsValid || exported.Authority is null)
            throw new InvalidDataException("Current signed Mortal diagnosis authority was refused: " +
                string.Join(";", exported.Issues.Select(value => value.Code + ": " + value.Actual)));
        var owner = exported.Authority;
        var manifestText = await files.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath) ??
            throw new InvalidDataException("The actual Mortal diagnosis manifest is missing.");
        var manifest = StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>(manifestText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "Mortal diagnosis original manifest") ??
            throw new InvalidDataException("The actual Mortal diagnosis manifest is not an object.");
        var computedHash = PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(manifest, LiveTurnPreparationService.ManifestHashJsonOptions,
            value => value.ManifestPayloadHash, (value, hash) => value.ManifestPayloadHash = hash);
        var preparedRole = owner.HasCurrentDiagnosisInputRole(files, lease, ownedFrame.SessionId, ownedFrame.RequestId, ownedFrame.Turn,
            manifest.ManifestPayloadHash, computedHash);
        IReadOnlyCollection<string> required = preparedRole ? WoundAcceptedTurnSnapshotContract.RequiredPaths.Where(path => path != AcceptedMechanicsPlan.WoundCommandPath).ToArray()
            : WoundAcceptedTurnSnapshotContract.RequiredPaths;
        if (!PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(manifest, value => value.Files,
            value => value.SnapshotFileHashes, value => value.RollbackBaselineFiles, required, out var missing))
            throw new InvalidDataException("Mortal diagnosis has no complete original canonical rollback coverage: " + missing);
        var operation = "mortal_diagnosis_operation_" + MortalWoundDiagnosisInputCodec.Fingerprint(input)["sha256:".Length..];
        var eventRef = owner.Binding.AcceptedEvents.First().EventRef;
        var prepared = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner,
            new(owner.History, Array.Empty<ValidationIssue>()), owner.CurrentWound, operation,
            selected.Selection.MortalDiagnosis!.DiagnosisPathId, eventRef);
        if (!prepared.IsValid || prepared.Request is null)
            throw new InvalidDataException("The selected Mortal diagnosis requirements or procedure claims were refused: " +
                string.Join(";", prepared.Issues.Select(value => value.Code + ": " + value.Actual)));
        owner.RequireOriginalDiagnosisRequest(prepared.Request);
        if (owner.ReadHeldDiagnosisRequest(prepared.Request.Coordinates) is null)
            await owner.RetainDiagnosisRequestAsync(prepared.Request);
        return new(owner, prepared.Request);
    }
}
