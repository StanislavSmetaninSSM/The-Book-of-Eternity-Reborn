using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>One ordinary operation's authenticated original. Never issued from backups or progression DATA.</summary>
internal sealed class MortalNpcActivityCompletionSource
{
    private const string CorePath = "game_state/npcs/npc_core.json";
    private const string SoulPath = "game_state/meta/soul_state.json";
    private static readonly PendingTurnSnapshotPathSelection Selection =
        PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths([CorePath, SoulPath], []);
    private readonly FileSystemManager _files;
    private readonly FileSystemManager.CanonicalWriteLease _lease;
    private readonly string _token, _session, _request;
    private readonly byte[] _originalCore;
    private readonly byte[] _originalSoul;
    private readonly Dictionary<string, byte[]?> _guards;
    internal int Turn { get; }
    internal JsonObject OriginalCore => Parse(_originalCore);

    private MortalNpcActivityCompletionSource(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        string token, string session, string request, int turn, byte[] core, byte[] soul, Dictionary<string, byte[]?> guards)
    { _files = files; _lease = lease; _token = token; _session = session; _request = request;
      Turn = turn; _originalCore = core; _originalSoul = soul; _guards = guards; }

    // The outer owner calls this only after selecting its ordinary (non-specialized) route.
    internal static async Task<MortalNpcActivityCompletionSource?> CaptureOrdinaryAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        files.VerifyCurrentSessionOperation(lease);
        using var fresh = FileSystemManager.SuppressValidationReuse();
        var commands = await files.ReadFileBytesAsync(lease, "game_state/npcs/npc_activities.json");
        if (commands is null) return null;
        var sidecar = Parse(commands);
        if (sidecar["completeNPCActivities"] is null or JsonArray { Count: 0 }) return null;
        if (sidecar["completeNPCActivities"] is not JsonArray)
            throw new InvalidDataException("Terminal NPC commands must be an array.");
        var read = PendingTurnSnapshotReader.ReadCurrent(files, lease, Selection);
        if (!read.Success || read.Snapshot is not { } original || original.Realm != "mortal_world")
            throw new InvalidDataException("Terminal NPC publication requires the authenticated ordinary Mortal original.");
        var originalSoul = original.ReadRequiredBytes(SoulPath);
        if (!RealmSemantics.IsMortalRealm(Parse(originalSoul)["currentRealm"]?.GetValue<string>()))
            throw new InvalidDataException("Terminal NPC source requires an original Mortal soul.");
        var guards = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[] { LiveTurnPreparationService.TurnRequestPath,
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath })
            guards.Add(path, await files.ReadFileBytesAsync(lease, path));
        var result = new MortalNpcActivityCompletionSource(files, lease, original.SnapshotToken,
            original.SessionId, original.RequestId, original.TurnNumber, original.ReadRequiredBytes(CorePath),
            originalSoul, guards);
        await result.RequireCurrentAsync(files, lease);
        return result;
    }

    internal async Task RequireCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        if (!ReferenceEquals(files, _files) || !ReferenceEquals(lease, _lease))
            throw new InvalidDataException("Terminal NPC source belongs to another exact operation.");
        files.EnsureCanonicalWriteLeaseActive(lease);
        files.VerifyCurrentSessionOperation(lease);
        using var fresh = FileSystemManager.SuppressValidationReuse();
        var read = PendingTurnSnapshotReader.ReadCurrent(files, lease, Selection);
        if (!read.Success || read.Snapshot is not { } original || original.Realm != "mortal_world" ||
            original.SnapshotToken != _token || original.SessionId != _session || original.RequestId != _request ||
            original.TurnNumber != Turn || !Same(_originalCore, original.ReadRequiredBytes(CorePath)) ||
            !Same(_originalSoul, original.ReadRequiredBytes(SoulPath)))
            throw new InvalidDataException("Terminal NPC original changed during publication.");
        foreach (var pair in _guards)
            if (!Same(pair.Value, await files.ReadFileBytesAsync(lease, pair.Key)))
                throw new InvalidDataException("Terminal NPC request authority changed during publication.");
        var soul = await files.ReadFileBytesAsync(lease, SoulPath);
        if (soul is null || !RealmSemantics.IsMortalRealm(Parse(soul)["currentRealm"]?.GetValue<string>()))
            throw new InvalidDataException("Terminal NPC publication left its Mortal realm.");
    }

    private static bool Same(byte[]? left, byte[]? right) => left is null ? right is null :
        right is not null && left.AsSpan().SequenceEqual(right);
    private static JsonObject Parse(byte[] bytes) => JsonNode.Parse(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF')) as JsonObject
        ?? throw new InvalidDataException("Terminal NPC source requires a JSON object.");
}
