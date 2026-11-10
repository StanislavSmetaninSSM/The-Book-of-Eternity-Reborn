using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // Negative applicability only, before the unbound Late caller can bootstrap.
    // Passing this check grants no original, generation or completion authority.
    private static async Task ValidateMortalJointGenerationBeforeBootstrapAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        var marker = ParseMortalJointHoldObject(await files.ReadFileBytesAsync(lease, MortalQteDecisionPublication.StatePath));
        if (marker?["schemaVersion"] is not JsonValue version || !version.TryGetValue<int>(out var schema) || schema != 2 ||
            marker["jointTreatment"] is not JsonObject || marker["progression"] is not JsonObject ||
            !TryReadMortalJointHoldTuple(marker, "turn", out var session, out var request, out var turn))
            return;

        var manifest = ParseMortalJointHoldObject(await files.ReadFileBytesAsync(lease,
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
        var input = ParseMortalJointHoldObject(await files.ReadFileBytesAsync(lease, "input/turn_request.json"));
        if (!MatchesMortalJointHoldHeader(manifest, session, request, turn) &&
            !MatchesMortalJointHoldHeader(input, session, request, turn))
            return;

        var generation = files.ReadLocalGenerationSnapshot(lease).Binding.Id;
        if (generation is null)
            throw new InvalidDataException("Active joint continuation generation is missing; evidence retained.");
        if (marker["generation"] is not JsonValue claimed || !claimed.TryGetValue<string>(out var value) ||
            !Guid.TryParseExact(value, "N", out var parsed) || parsed.ToString("N") != value ||
            !string.Equals(generation, value, StringComparison.Ordinal))
            throw new InvalidDataException("Active joint continuation generation is invalid or differs; evidence retained.");
    }

    // Preservation applicability only. Raw correlated claims cannot issue an
    // original, phase, receipt, accepted outcome or completion capability.
    private async Task<bool> TryHoldMortalJointWithoutValidatedOriginalAsync()
    {
        var identified = false;
        try
        {
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            var marker = ParseMortalJointHoldObject(await _fs.ReadFileBytesAsync(lease, MortalQteDecisionPublication.StatePath));
            if (marker?["schemaVersion"] is not JsonValue version || !version.TryGetValue<int>(out var schema) || schema != 2 ||
                marker["jointTreatment"] is not JsonObject || marker["progression"] is not JsonObject ||
                !TryReadMortalJointHoldTuple(marker, "turn", out var session, out var request, out var turn))
                return false;

            // Read the actual fixed headers, not the caller's cached manifest
            // or Ready. Either match is sufficient; a foreign other header does
            // not erase the matching active candidate. Stale marker alone is inert.
            var manifest = ParseMortalJointHoldObject(await _fs.ReadFileBytesAsync(lease,
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
            identified = MatchesMortalJointHoldHeader(manifest, session, request, turn);
            var input = ParseMortalJointHoldObject(await _fs.ReadFileBytesAsync(lease, "input/turn_request.json"));
            identified |= MatchesMortalJointHoldHeader(input, session, request, turn);
            if (!identified) return false;

            // Existing admission already owns the lease. Never create/rotate G
            // or turn a marker's generation/fingerprint into admission authority.
            var generation = _fs.ReadLocalGenerationSnapshot(lease);
            _logger.LogWarning("Private joint candidate retained without validated original: session={Session}, request={Request}, turn={Turn}, currentGeneration={Generation}.",
                session, request, turn, generation.Binding.Id);
            HoldMortalJointStoryTurn("A correlated active private joint candidate has no validated original; all evidence retained.");
            return true;
        }
        catch (SessionReplacedException) { throw; }
        catch (Exception error) when (identified && error is (CoordinatedStatePublicationUncertainException or InvalidDataException or
            IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or JsonException or FormatException))
        {
            // Once a matching physical candidate is identified, later read or
            // release instability cannot authorize ordinary destructive discard.
            _logger.LogError(error, "Private joint null-frame preservation became uncertain; no ordinary discard.");
            HoldMortalJointStoryTurn("The identified private joint candidate became unreadable; all evidence retained.");
            return true;
        }
    }

    private static JsonObject? ParseMortalJointHoldObject(byte[]? bytes)
    {
        if (bytes is null) return null;
        try
        {
            return StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "private joint hold header");
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or FormatException or ArgumentException)
        {
            return null;
        }
    }

    private static bool MatchesMortalJointHoldHeader(JsonObject? header, string session, string request, int turn) =>
        header is not null && TryReadMortalJointHoldTuple(header, "turnNumber", out var actualSession, out var actualRequest, out var actualTurn) &&
        string.Equals(session, actualSession, StringComparison.Ordinal) && string.Equals(request, actualRequest, StringComparison.Ordinal) && turn == actualTurn;

    private static bool TryReadMortalJointHoldTuple(JsonObject value, string turnField,
        out string session, out string request, out int turn)
    {
        session = request = string.Empty; turn = 0;
        if (value["sessionId"] is not JsonValue sessionValue || !sessionValue.TryGetValue<string>(out var actualSession) ||
            string.IsNullOrWhiteSpace(actualSession) || value["requestId"] is not JsonValue requestValue ||
            !requestValue.TryGetValue<string>(out var actualRequest) || string.IsNullOrWhiteSpace(actualRequest) ||
            value[turnField] is not JsonValue turnValue || !turnValue.TryGetValue<int>(out turn) || turn <= 0)
            return false;
        session = actualSession; request = actualRequest;
        return true;
    }
}
