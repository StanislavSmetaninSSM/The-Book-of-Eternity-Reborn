using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private const string BirthConfirmationReportPath = "output/debug_logs.json";
    private const string BirthConfirmationNarrativePath = "output/narrative_response.json";

    // Call only after ordinary accepted materialization and any repair, before display and durable story.
    private async Task ApplyAcceptedMortalBirthConfirmationsAsync(GameResponse? response,
        ValidatedPendingTurnSnapshotContext? frame, bool repairedAfterResponse)
    {
        if (response is null || frame?.ProgressionControl is not { } progression ||
            !RealmSemantics.IsMortalRealm(progression.CurrentRealm) ||
            !RealmSemantics.IsMortalRealm(_stateManager.CurrentState.CurrentRealm))
            return;

        if (!response.HasMortalBirthConfirmation && !repairedAfterResponse)
            return;

        string? projected = null;
        FileSystemManager.CanonicalWriteLease? lease = null;
        try
        {
            lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        }
        catch (IOException ex) when (_fs.IsOriginalCanonicalLockContention(ex))
        {
            // Only exact original physical acquisition contention is optional.
        }

        if (lease is not null)
        {
            // Never suppress disposal failures, even a plain IOException: the
            // original owner may already be marked unresolved by DisposeAsync.
            await using (lease)
                projected = await MortalBirthConfirmation.ReadOptionalAsync(() => ReadProjection(lease));
        }

        if (projected is not null)
            response.Response = projected;
        else if (repairedAfterResponse)
        {
            // A repair can replace the narrative even if it removes the declaration.
            // Refuse stale prose rather than silently falling back.
            var raw = await _fs.ReadFileAsync(BirthConfirmationNarrativePath);
            response.Response = ReadNarrative(raw) ??
                throw new InvalidDataException("The repaired player-facing narrative is unavailable.");
        }

        async Task<string?> ReadProjection(FileSystemManager.CanonicalWriteLease activeLease)
        {
            var report = await _fs.ReadFileAsync(activeLease, BirthConfirmationReportPath);
            if (string.IsNullOrWhiteSpace(report)) return null;
            using var document = JsonDocument.Parse(report);
            var claims = MortalBirthConfirmation.ReadClaims(document.RootElement);
            if (claims.Length == 0) return null;
            var text = ReadNarrative(await _fs.ReadFileAsync(activeLease, BirthConfirmationNarrativePath));
            if (text is null) return null;

            var current = await ReadObject(StorageTransportMoveService.CurrentLocationPath);
            var vehicles = claims.Any(c => c.Kind == MortalBirthConfirmation.VehicleOwnership)
                ? await ReadObject(StorageTransportMoveService.VehiclesPath) : null;
            var storage = claims.Any(c => c.Kind == MortalBirthConfirmation.StorageAccess);
            var index = storage ? await ReadObject(MortalLocationIdentityState.StatePath) : null;
            var access = storage ? await ReadObject(MortalStorageAccessProjection.StatePath) : null;
            var source = new MortalBirthConfirmation.Source(frame.SessionId, frame.RequestId,
                frame.Payload.ManifestPayloadHash, frame.TurnNumber);
            return MortalBirthConfirmation.Project(text, claims, source, current, vehicles, index, access);

            async Task<JsonObject?> ReadObject(string relative)
            {
                var raw = await _fs.ReadFileAsync(activeLease, relative);
                return raw is null ? null : JsonNode.Parse(raw) as JsonObject;
            }
        }

        static string? ReadNarrative(string? raw)
        {
            if (raw is null || JsonNode.Parse(raw) is not JsonObject root ||
                root["response"] is not JsonValue value ||
                !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text)) return null;
            return PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(text);
        }
    }
}
