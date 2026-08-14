using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class MortalItemAcceptedEffectSourceAuthority
{
    private static readonly ConditionalWeakTable<FileSystemManager, Cache> Caches = new();

    internal static void RegisterValidatedSources(
        FileSystemManager fs,
        string sessionId,
        string snapshotToken,
        MortalItemCarrierCatalog catalog,
        IEnumerable<string> knownItemIds)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotToken);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(knownItemIds);

        var newCandidates = catalog.Occurrences
            .Where(static occurrence =>
                occurrence.ItemId == null &&
                occurrence.CreationRef != null &&
                string.Equals(
                    occurrence.Carrier.Kind,
                    "player_inventory",
                    StringComparison.Ordinal) &&
                MortalItemLocalActionPolicy.IsCarriedByPlayer(occurrence.Item))
            .OrderBy(static occurrence => occurrence.CreationRef, StringComparer.Ordinal)
            .Select(occurrence => new NewCandidate(
                occurrence.CreationRef!,
                occurrence.Item["activeEffectDefinitions"]?.DeepClone().AsArray() ??
                    new JsonArray(),
                IsEquippedPlayerReference(catalog, occurrence.CreationRef!)))
            .ToArray();
        var stableCandidates = catalog.Occurrences
            .Where(static occurrence =>
                occurrence.ItemId != null &&
                string.Equals(
                    occurrence.Carrier.Kind,
                    "player_inventory",
                    StringComparison.Ordinal))
            .OrderBy(static occurrence => occurrence.ItemId, StringComparer.Ordinal)
            .Select(occurrence => new StableCandidate(
                occurrence.ItemId!,
                occurrence.Item["activeEffectDefinitions"]?.DeepClone().AsArray() ??
                    new JsonArray(),
                MortalItemLocalActionPolicy.IsCarriedByPlayer(occurrence.Item),
                IsEquippedPlayerReference(catalog, occurrence.ItemId!)))
            .ToArray();
        var governedItemIds = knownItemIds
            .OrderBy(static itemId => itemId, StringComparer.Ordinal)
            .ToArray();
        var fingerprint = CreateFingerprint(
            newCandidates,
            stableCandidates,
            governedItemIds);
        Caches.GetValue(fs, static _ => new Cache()).Register(
            sessionId,
            snapshotToken,
            fingerprint,
            newCandidates,
            stableCandidates,
            governedItemIds);
    }

    internal static IReadOnlyList<EffectSourceExport> GetValidatedSources(
        FileSystemManager fs,
        string sessionId,
        string snapshotToken) =>
        Caches.TryGetValue(fs, out var cache)
            ? cache.GetSources(sessionId, snapshotToken)
            : Array.Empty<EffectSourceExport>();

    internal static void InvalidateValidatedSources(FileSystemManager fs)
    {
        ArgumentNullException.ThrowIfNull(fs);
        Caches.GetValue(fs, static _ => new Cache()).InvalidateValidated();
    }

    internal static IReadOnlySet<EffectSourceOwnerKey> GetReplacedSourceOwners(
        FileSystemManager fs,
        string sessionId,
        string snapshotToken) =>
        Caches.TryGetValue(fs, out var cache)
            ? cache.GetReplacedSourceOwners(sessionId, snapshotToken)
            : new HashSet<EffectSourceOwnerKey>();

    internal static bool TryGetAllocatedItemId(
        FileSystemManager fs,
        string sessionId,
        string snapshotToken,
        string creationRef,
        out string itemId)
    {
        itemId = string.Empty;
        return Caches.TryGetValue(fs, out var cache) &&
               cache.TryGetItemId(sessionId, snapshotToken, creationRef, out itemId);
    }

    private static string CreateFingerprint(
        IEnumerable<NewCandidate> newCandidates,
        IEnumerable<StableCandidate> stableCandidates,
        IEnumerable<string> governedItemIds)
    {
        var root = new JsonObject
        {
            ["new"] = new JsonArray(newCandidates.Select(candidate => (JsonNode)new JsonObject
            {
                ["creationRef"] = candidate.CreationRef,
                ["definitions"] = candidate.Definitions.DeepClone(),
                ["equipped"] = candidate.Equipped
            }).ToArray()),
            ["stable"] = new JsonArray(stableCandidates.Select(candidate => (JsonNode)new JsonObject
            {
                ["itemId"] = candidate.ItemId,
                ["definitions"] = candidate.Definitions.DeepClone(),
                ["active"] = candidate.Active,
                ["equipped"] = candidate.Equipped
            }).ToArray()),
            ["governedItemIds"] = new JsonArray(
                governedItemIds.Select(static itemId => (JsonNode)itemId).ToArray())
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    private static bool IsEquippedPlayerReference(
        MortalItemCarrierCatalog catalog,
        string reference) =>
        catalog.ByCompanionReference.TryGetValue(reference, out var references) &&
        references.Any(candidate =>
            string.Equals(
                candidate.FilePath,
                InventoryEquipmentService.ItemsPath,
                StringComparison.Ordinal) &&
            candidate.JsonPath.StartsWith(
                InventoryEquipmentService.ItemsPath + ".equippedItems.",
                StringComparison.Ordinal) &&
            candidate.ExpectedCarrier is
            {
                Kind: "player_inventory",
                OwnerId: "player"
            });

    private static IReadOnlySet<string> ItemPredicates(bool carried, bool equipped)
    {
        var predicates = new HashSet<string>(StringComparer.Ordinal);
        if (carried)
            predicates.Add("carried");
        if (equipped)
            predicates.Add("equipped");
        return predicates;
    }

    private sealed class Cache
    {
        private readonly object _gate = new();
        private string? _sessionId;
        private string? _snapshotToken;
        private string? _fingerprint;
        private bool _validated;
        private Dictionary<string, string> _itemIdsByCreationRef = new(StringComparer.Ordinal);
        private EffectSourceExport[] _sources = Array.Empty<EffectSourceExport>();
        private HashSet<EffectSourceOwnerKey> _replacedSourceOwners = new();

        internal void Register(
            string sessionId,
            string snapshotToken,
            string fingerprint,
            IReadOnlyList<NewCandidate> newCandidates,
            IReadOnlyList<StableCandidate> stableCandidates,
            IReadOnlyList<string> governedItemIds)
        {
            lock (_gate)
            {
                if (string.Equals(_sessionId, sessionId, StringComparison.Ordinal) &&
                    string.Equals(_snapshotToken, snapshotToken, StringComparison.Ordinal) &&
                    string.Equals(_fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    _validated = true;
                    return;
                }

                var known = governedItemIds.ToHashSet(StringComparer.Ordinal);
                var allocations = new Dictionary<string, string>(StringComparer.Ordinal);
                var exports = new List<EffectSourceExport>();
                foreach (var candidate in stableCandidates)
                {
                    exports.Add(new EffectSourceExport(
                        "mortal_world",
                        "item",
                        candidate.ItemId,
                        candidate.Definitions.DeepClone().AsArray(),
                        Materializable: true,
                        Active: candidate.Active,
                        SameTurn: true,
                        SatisfiedPredicates: ItemPredicates(
                            candidate.Active,
                            candidate.Equipped)));
                }
                foreach (var candidate in newCandidates)
                {
                    string itemId;
                    do
                    {
                        itemId = "itm_" + Guid.NewGuid().ToString("N");
                    }
                    while (!known.Add(itemId));

                    allocations.Add(candidate.CreationRef, itemId);
                    exports.Add(new EffectSourceExport(
                        "mortal_world",
                        "item",
                        itemId,
                        candidate.Definitions.DeepClone().AsArray(),
                        Materializable: true,
                        Active: true,
                        SameTurn: true,
                        SourceRef: candidate.CreationRef,
                        SatisfiedPredicates: ItemPredicates(
                            carried: true,
                            equipped: candidate.Equipped)));
                }

                _sessionId = sessionId;
                _snapshotToken = snapshotToken;
                _fingerprint = fingerprint;
                _validated = true;
                _itemIdsByCreationRef = allocations;
                _sources = exports.ToArray();
                _replacedSourceOwners = governedItemIds
                    .Select(static itemId => new EffectSourceOwnerKey(
                        "mortal_world",
                        "item",
                        itemId))
                    .ToHashSet();
            }
        }

        internal void InvalidateValidated()
        {
            lock (_gate)
                _validated = false;
        }

        internal IReadOnlyList<EffectSourceExport> GetSources(
            string sessionId,
            string snapshotToken)
        {
            lock (_gate)
            {
                if (!Matches(sessionId, snapshotToken))
                    return Array.Empty<EffectSourceExport>();
                return _sources.Select(static source => source with
                {
                    Definitions = source.Definitions.DeepClone().AsArray(),
                    SatisfiedPredicates = source.SatisfiedPredicates == null
                        ? null
                        : new HashSet<string>(
                            source.SatisfiedPredicates,
                            StringComparer.Ordinal)
                }).ToArray();
            }
        }

        internal bool TryGetItemId(
            string sessionId,
            string snapshotToken,
            string creationRef,
            out string itemId)
        {
            lock (_gate)
            {
                if (Matches(sessionId, snapshotToken) &&
                    _itemIdsByCreationRef.TryGetValue(creationRef, out var value))
                {
                    itemId = value;
                    return true;
                }
                itemId = string.Empty;
                return false;
            }
        }

        internal IReadOnlySet<EffectSourceOwnerKey> GetReplacedSourceOwners(
            string sessionId,
            string snapshotToken)
        {
            lock (_gate)
            {
                return Matches(sessionId, snapshotToken)
                    ? new HashSet<EffectSourceOwnerKey>(_replacedSourceOwners)
                    : new HashSet<EffectSourceOwnerKey>();
            }
        }

        private bool Matches(string sessionId, string snapshotToken) =>
            _validated &&
            string.Equals(_sessionId, sessionId, StringComparison.Ordinal) &&
            string.Equals(_snapshotToken, snapshotToken, StringComparison.Ordinal);
    }

    private sealed record NewCandidate(
        string CreationRef,
        JsonArray Definitions,
        bool Equipped);

    private sealed record StableCandidate(
        string ItemId,
        JsonArray Definitions,
        bool Active,
        bool Equipped);
}
