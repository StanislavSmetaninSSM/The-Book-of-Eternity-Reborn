using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static readonly string[] OrdinaryIdleMarkers =
    [
        LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
        LiveTurnPreparationService.TurnRequestPath, "game_state/control/validation_repair_request.json",
        "game_state/control/validation_repair_ready.json", "ready/turn_complete.json", "ready/turn_error.json"
    ];
    private static readonly string[] OrdinaryCanonicalDependencies =
    [
        MortalLocationMaterializationContract.WorldMapPath, MortalLocationIdentityState.StatePath,
        "game_state/npcs/npc_core.json", "game_state/meta/soul_state.json"
    ];

    /// <summary>Refuses active or orphan state before health can remove it; closes read ownership before returning.</summary>
    internal async Task RequireOrdinaryPreSendIdleAsync()
        => _ = await RunPendingSnapshotValidationCoreAsync(() => Task.FromResult(new List<ValidationIssue>()),
            () => _fs.BeginPendingSnapshotValidationRead(CancellationToken.None), ordinaryPreSend: true);

    /// <summary>Runs fresh full state validation in an explicitly Mortal canonical, not mutation, interpretation.</summary>
    internal Task<List<ValidationIssue>> ValidateOrdinaryPreSendGameStateAsync()
        => RunPendingSnapshotValidationCoreAsync(
            () => ValidateGameStateCoreAsync(GameStateValidationSelection.All, false, null, null),
            () => _fs.BeginPendingSnapshotValidationRead(CancellationToken.None), ordinaryPreSend: true,
            ordinaryCanonical: true);

    private OrdinaryCanonicalRead CaptureOrdinaryCanonicalRead(
        FileSystemManager.PendingSnapshotValidationReadScope physical, bool includeCanonical, bool interpret = true)
    {
        physical.RequireCurrent();
        var bytes = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in OrdinaryIdleMarkers)
        {
            var value = _fs.ReadFileBytesSync(physical.Lease!, path);
            if (value is not null) throw new InvalidDataException("Ordinary pre-send requires absent marker: " + path);
            bytes.Add(path, value);
        }
        if (_fs.DirectoryExists(physical.Lease!, LiveTurnPreparationService.PendingTurnSnapshotDirectory))
            throw new InvalidDataException("Ordinary pre-send refuses an orphan snapshot directory.");
        if (includeCanonical)
            foreach (var path in OrdinaryCanonicalDependencies)
                bytes.Add(path, _fs.ReadFileBytesSync(physical.Lease!, path));
        physical.RequireCurrent();
        return new(bytes, includeCanonical, interpret);
    }

    private OrdinaryCanonicalRead? CurrentOrdinaryCanonicalRead()
    {
        var invocation = _worldValidationInvocation.Value;
        var canonical = invocation?.OrdinaryCanonical ?? invocation?.LoadedCanonical;
        if (canonical is null) return null;
        if (invocation.Closed || !ReferenceEquals(_worldReservation, invocation))
            throw new InvalidOperationException("Ordinary canonical validation is no longer reserved.");
        _pendingSnapshotInvocation.Value!.RequireCurrent();
        return canonical;
    }

    private HashSet<string>? ReadOrdinaryCanonicalLocationIds()
        => CurrentOrdinaryCanonicalRead()?.CopyLocationIds();

    private bool IsOrdinaryCanonicalActor(JsonElement actor, string section)
        => CurrentOrdinaryCanonicalRead()?.ContainsActor(actor, section) == true;

    /// <summary>Invocation-only DATA; never a completed publication or permission to write.</summary>
    private sealed class OrdinaryCanonicalRead
    {
        private readonly Dictionary<string, byte[]?> _bytes;
        private readonly HashSet<string> _locations = new(StringComparer.Ordinal);
        private readonly bool _canonical;
        private readonly Dictionary<(string Section, string Id), string> _actors = new();
        internal OrdinaryCanonicalRead(Dictionary<string, byte[]?> bytes, bool canonical, bool interpret)
        {
            _bytes = bytes;
            _canonical = canonical;
            if (!canonical || !interpret) return;
            var soul = bytes["game_state/meta/soul_state.json"]
                ?? throw new InvalidDataException("Ordinary canonical validation requires soul state.");
            if (!RealmSemantics.IsMortalRealm(CanonicalStateNormalizer.TryReadStrictCurrentRealm(DecodePendingSnapshotText(soul))))
                throw new InvalidDataException("Ordinary canonical validation is Mortal-only.");
            if (bytes["game_state/npcs/npc_core.json"] is { } npc)
            {
                using var actors = JsonDocument.Parse(DecodePendingSnapshotText(npc));
                foreach (var section in new[] { "NPCsInScene", "UpdateNPCs" })
                    if (actors.RootElement.TryGetProperty(section, out var rows) && rows.ValueKind == JsonValueKind.Array)
                        foreach (var actor in rows.EnumerateArray())
                            if (TryReadCanonicalCurrentMortalActorId(actor, out var id) &&
                                !ActorMaterializationContract.ValidateHistoricalMortalNpc(actor, section).Any(i => i.Severity == IssueSeverity.Error))
                                _actors.TryAdd((section, id), actor.GetRawText());
            }
            if (bytes[MortalLocationMaterializationContract.WorldMapPath] is not { } map) return;
            using var document = JsonDocument.Parse(DecodePendingSnapshotText(map));
            if (!HasExactCanonicalWorldMapRoot(document.RootElement, out var locations)) return;
            var ids = ReadExactCanonicalWorldMapLocationIds(document.RootElement);
            if (ids.Count == locations.GetArrayLength()) _locations.UnionWith(ids);
        }
        internal HashSet<string> CopyLocationIds() => new(_locations, StringComparer.Ordinal);
        internal bool ContainsActor(JsonElement actor, string section)
        {
            if (!_canonical || !TryReadCanonicalCurrentMortalActorId(actor, out var id) ||
                !_actors.TryGetValue((section, id), out var original)) return false;
            var current = actor.GetRawText();
            return string.Equals(original, current, StringComparison.Ordinal) ||
                JsonValuesSemanticallyEqual(original, current);
        }
        internal void RequireSame(OrdinaryCanonicalRead fresh)
        {
            if (_canonical != fresh._canonical || _bytes.Count != fresh._bytes.Count ||
                _bytes.Any(pair => !fresh._bytes.TryGetValue(pair.Key, out var current) ||
                    (pair.Value is null ? current is not null : current is null || !pair.Value.AsSpan().SequenceEqual(current))))
                throw new InvalidDataException("Ordinary canonical dependencies changed during validation.");
        }
    }
}
