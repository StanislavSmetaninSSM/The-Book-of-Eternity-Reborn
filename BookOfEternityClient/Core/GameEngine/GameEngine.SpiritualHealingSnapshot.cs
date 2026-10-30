using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // Classifies authenticated snapshot DATA only. Actual source, execution and publication authority
    // remain in the accepted-turn owner; no live kind or caller projection grants this input role.
    private async Task<bool> IsAuthenticatedSpiritualPreparationSnapshotAsync(ValidatedPendingTurnSnapshotContext context)
    {
        const string path = AcceptedMechanicsPlan.WoundCommandPath;
        var payload = context.Payload;
        if (!payload.Files.ContainsKey(path) || payload.RollbackBaselineFiles.Contains(path, StringComparer.OrdinalIgnoreCase) ||
            payload.RollbackBackups.ContainsKey(path) || payload.RollbackBackupHashes.ContainsKey(path) ||
            payload.SnapshotHashMode != PendingTurnSnapshotAuthority.ExactSnapshotHashMode || context.ProgressionControl is null)
            return false;
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        _fs.VerifyCurrentSessionOperation(lease);
        var original = PendingTurnSnapshotReader.ReadObservedCurrent(_fs, lease, new[] { path });
        if (!original.Success || original.Snapshot is not { } snapshot || original.Evidence is not { } evidence ||
            snapshot.SessionId != context.SessionId || snapshot.RequestId != context.RequestId ||
            snapshot.TurnNumber != context.TurnNumber || snapshot.SnapshotToken != payload.ManifestPayloadHash ||
            context.Manifest.ManifestPayloadHash != payload.ManifestPayloadHash ||
            !payload.SnapshotFileHashes.TryGetValue(path, out var expectedHash))
            return false;
        var bytes = snapshot.ReadRequiredBytes(path);
        if (PendingTurnSnapshotAuthority.ComputeSnapshotFileHash(payload, bytes) != expectedHash)
            return false;
        try
        {
            var request = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
                new JsonSerializerOptions(), "signed spiritual preparation input")!;
            if (SpiritualCombatHealingIntent.IsKind(request))
            {
                var intent = SpiritualCombatHealingIntent.Parse(request);
                var combatBound = intent.IsBound(new(context.SessionId, context.RequestId, context.TurnNumber,
                    context.ProgressionControl), intent.Selection);
                evidence.RequireCurrent(_fs, lease, snapshot);
                return combatBound;
            }
            if (SpiritualDiagnosisRequestCodec.IsKind(request))
            {
                var diagnosisBound = SpiritualDiagnosisRequestCodec.IsBound(request,
                    new(context.SessionId, context.RequestId, context.TurnNumber, context.ProgressionControl));
                evidence.RequireCurrent(_fs, lease, snapshot);
                return diagnosisBound;
            }
            if (!SpiritualSelfTreatmentRequestCodec.IsActiveKind(request) || request["attempt"] is not JsonObject ||
                !AfterlifeEntityProfileState.TryNormalizeEffectRealm(context.ProgressionControl.CurrentRealm, out var realm))
                return false;
            var selection = SpiritualSelfTreatmentRequestCodec.ReadActiveSelection(request);
            var bound = SpiritualSelfTreatmentRequestCodec.IsBoundActive(request,
                new(context.SessionId, context.RequestId, context.TurnNumber, context.ProgressionControl),
                selection, realm, context.ProgressionControl.NextChaosSeaTurnOrdinal);
            evidence.RequireCurrent(_fs, lease, snapshot);
            return bound;
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (JsonException or InvalidDataException or InvalidOperationException or
                ArgumentException or KeyNotFoundException or NullReferenceException))
        {
            return false;
        }
    }
}
