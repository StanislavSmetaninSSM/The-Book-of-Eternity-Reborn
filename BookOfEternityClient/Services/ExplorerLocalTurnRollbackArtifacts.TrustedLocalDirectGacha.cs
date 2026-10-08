using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public static partial class ExplorerLocalTurnRollbackArtifacts
{
    private const string DirectGachaRoot = Root + "/browser_direct_gacha";
    private const string DirectGachaSoulBackupPrefix = "game_state_meta_soul_state.json.rollback.";

    // Structural storage recognition only. This neither authorizes a new turn
    // to adopt a before-image nor grants a main/worker pin from an artifact name.
    internal static bool IsLocalDirectGachaPath(string path)
    {
        if (path == DirectGachaRoot) return true;
        if (!path.StartsWith(DirectGachaRoot + "/", StringComparison.Ordinal)) return false;
        var parts = path[(DirectGachaRoot.Length + 1)..].Split('/');
        if (parts.Length is < 1 or > 2 || !IsValidTransactionDirectoryName(parts[0])) return false;
        if (parts.Length == 1) return true;
        return parts[1].StartsWith(DirectGachaSoulBackupPrefix, StringComparison.Ordinal) &&
            Guid.TryParseExact(parts[1][DirectGachaSoulBackupPrefix.Length..], "N", out var id) &&
            parts[1][DirectGachaSoulBackupPrefix.Length..] == id.ToString("N");
    }

    internal static bool IsLocalDirectGachaBackup(string path) =>
        IsLocalDirectGachaPath(path) && path.Count(character => character == '/') == DirectGachaRoot.Count(character => character == '/') + 2;

    private static void RequireCurrentDirectGachaAdoption(FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease lease, string trackedFile, string backup)
    {
        try
        {
            byte[]? Read(string path) => PendingTurnSnapshotAuthority.IsSafeRelativePath(path)
                ? fs.ReadOriginalFileBytesAsync(lease, path).GetAwaiter().GetResult() : null;
            var bytes = Read(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)
                ?? throw new InvalidDataException("Pending manifest is absent.");
            var manifest = ParseLocal<LiveTurnPendingSnapshotManifest>(bytes);
            var authority = Read(PendingTurnSnapshotAuthority.AuthorityPath);
            if (!PendingTurnSnapshotAuthority.TryValidateManifestForDestructiveAuthority(manifest,
                    authority == null ? null : DecodeUtf8(authority), LiveTurnPreparationService.ManifestHashJsonOptions,
                    static value => value.ManifestPayloadHash, static (value, hash) => value.ManifestPayloadHash = hash,
                    static value => value.SessionId, static value => value.RequestId, static value => value.TurnNumber,
                    static value => value.Files, static value => value.SnapshotFileHashes,
                    static value => value.ClientOwnedValidationHashes, static value => value.RollbackBaselineFiles,
                    static value => value.SourceLabel, static value => value.RollbackBackups, Read, out var payload, out _) ||
                payload!.RollbackHashMode != PendingTurnSnapshotAuthority.ExactRollbackHashMode ||
                !payload.RollbackBackups.TryGetValue(trackedFile, out var mapped) || mapped != backup)
                throw new InvalidDataException("Exact existing pending authority does not map this before-image.");

            // Read the actual persisted request fields without TurnRequest's
            // construction defaults, which must never mint missing identity.
            var requestBytes = Read("input/turn_request.json") ?? throw new InvalidDataException("Current request is absent.");
            var request = StrictJsonAuthority.Deserialize<JsonElement>(DecodeUtf8(requestBytes), ManifestJsonOptions, "Current pending request");
            string? Text(string name) => request.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() : null;
            if (request.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(manifest.SessionId) ||
                string.IsNullOrWhiteSpace(manifest.RequestId) ||
                !PendingTurnSnapshotAuthority.DoesPendingTurnContextIdMatch(manifest.SessionId, Text("sessionId") ?? "") ||
                !PendingTurnSnapshotAuthority.DoesPendingTurnContextIdMatch(manifest.RequestId, Text("requestId") ?? "") ||
                !request.TryGetProperty("turnNumber", out var turn) || !turn.TryGetInt32(out var number) || number != manifest.TurnNumber ||
                Text("playerAction") != manifest.PlayerAction || Text("timestamp") != manifest.RequestTimestamp)
                throw new InvalidDataException("Current request does not bind the pending before-image.");
        }
        catch (Exception failure) when (failure is InvalidDataException or JsonException or InvalidOperationException)
        {
            throw new InvalidDataException("Pending direct-gacha before-image is not eligible for this turn; evidence retained.", failure);
        }
    }
}
