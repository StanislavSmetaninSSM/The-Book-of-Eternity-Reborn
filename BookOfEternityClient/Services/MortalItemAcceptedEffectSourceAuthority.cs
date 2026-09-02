using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record MortalItemAcceptedTurnOwner(
    string ItemId,
    string? ItemRef,
    string FilePath,
    string JsonPath,
    MortalItemCarrierCoordinate Carrier,
    JsonObject Item,
    bool SameTurn);

internal sealed class MortalItemAcceptedTurnNormalizationSnapshot
{
    private readonly Dictionary<string, string> _itemIdsByCreationRef;

    internal MortalItemAcceptedTurnNormalizationSnapshot(
        string sessionId,
        string snapshotToken,
        int turn,
        IReadOnlyDictionary<string, string> itemIdsByCreationRef)
    {
        if (!ResourceMaterializationContract.IsExactIdentifier(sessionId))
            throw new ArgumentException("Expected an exact accepted-turn session ID.", nameof(sessionId));
        if (!ResourceMaterializationContract.IsExactIdentifier(snapshotToken))
            throw new ArgumentException("Expected an exact accepted-turn snapshot token.", nameof(snapshotToken));
        if (turn <= 0)
            throw new ArgumentOutOfRangeException(nameof(turn));
        ArgumentNullException.ThrowIfNull(itemIdsByCreationRef);

        SessionId = sessionId;
        SnapshotToken = snapshotToken;
        Turn = turn;
        _itemIdsByCreationRef = itemIdsByCreationRef.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal);
    }

    internal string SessionId { get; }

    internal string SnapshotToken { get; }

    internal int Turn { get; }

    internal bool TryGetAllocatedItemId(string creationRef, out string itemId) =>
        _itemIdsByCreationRef.TryGetValue(creationRef, out itemId!);

    internal bool MatchesAcceptedOwnerAuthority(ResourceOwnerAuthority ownerAuthority)
    {
        ArgumentNullException.ThrowIfNull(ownerAuthority);
        var planned = ownerAuthority.Entries.Values
            .Where(static entry =>
                entry.Key.OwnerKind == ResourceOwnerKind.Item &&
                string.Equals(entry.Key.Realm, "mortal_world", StringComparison.Ordinal) &&
                entry.SameTurn &&
                entry.SameTurnRef != null)
            .ToArray();
        if (planned.Length != _itemIdsByCreationRef.Count)
            return false;

        foreach (var entry in planned)
        {
            if (!_itemIdsByCreationRef.TryGetValue(entry.SameTurnRef!, out var itemId) ||
                !string.Equals(
                    itemId,
                    entry.Key.ResourceOwnerId,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    internal bool MatchesExactCacheState(
        string? sessionId,
        string? snapshotToken,
        IReadOnlyDictionary<string, string> itemIdsByCreationRef)
    {
        ArgumentNullException.ThrowIfNull(itemIdsByCreationRef);
        if (!string.Equals(SessionId, sessionId, StringComparison.Ordinal) ||
            !string.Equals(SnapshotToken, snapshotToken, StringComparison.Ordinal) ||
            _itemIdsByCreationRef.Count != itemIdsByCreationRef.Count)
        {
            return false;
        }

        foreach (var allocation in _itemIdsByCreationRef)
        {
            if (!itemIdsByCreationRef.TryGetValue(
                    allocation.Key,
                    out var itemId) ||
                !string.Equals(allocation.Value, itemId, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    internal string ComputePublicationFingerprint(string cacheFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheFingerprint);
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_item.accepted_turn_publication_take",
            "1",
            SessionId,
            SnapshotToken,
            Turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
            cacheFingerprint,
            _itemIdsByCreationRef.Count.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        };
        foreach (var allocation in _itemIdsByCreationRef
                     .OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            fields.Add(allocation.Key);
            fields.Add(allocation.Value);
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }
}

internal static class MortalItemAcceptedTurnAuthority
{
    internal static bool HasValidatedItems(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        return AcceptedTurnAuthorityRegistry.HasMortalItemsValidated(
            fs,
            writeLease);
    }

    internal static void RegisterValidatedItems(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken,
        MortalItemCarrierCatalog catalog,
        IEnumerable<string> knownItemIds)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotToken);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(knownItemIds);

        var newCandidates = catalog.Occurrences
            .Where(static occurrence =>
                occurrence.ItemId == null &&
                occurrence.CreationRef != null)
            .OrderBy(static occurrence => occurrence.CreationRef, StringComparer.Ordinal)
            .Select(occurrence => new NewCandidate(
                occurrence.CreationRef!,
                occurrence.Item.DeepClone().AsObject(),
                occurrence.FilePath,
                occurrence.JsonPath,
                CloneCarrier(occurrence.Carrier),
                IsEligiblePlayerEffectSource(occurrence),
                IsEquippedPlayerReference(catalog, occurrence.CreationRef!)))
            .ToArray();
        var stableCandidates = catalog.Occurrences
            .Where(static occurrence => occurrence.ItemId != null)
            .OrderBy(static occurrence => occurrence.ItemId, StringComparer.Ordinal)
            .Select(occurrence => new StableCandidate(
                occurrence.ItemId!,
                occurrence.Item.DeepClone().AsObject(),
                occurrence.FilePath,
                occurrence.JsonPath,
                CloneCarrier(occurrence.Carrier),
                IsEligiblePlayerEffectSource(occurrence),
                IsEquippedPlayerReference(catalog, occurrence.ItemId!)))
            .ToArray();
        var governedItemIds = knownItemIds
            .OrderBy(static itemId => itemId, StringComparer.Ordinal)
            .ToArray();
        var fingerprint = CreateFingerprint(
            newCandidates,
            stableCandidates,
            governedItemIds);
        AcceptedTurnAuthorityRegistry.RegisterMortalItemsValidated(
            fs,
            writeLease,
            sessionId,
            snapshotToken,
            fingerprint,
            newCandidates,
            stableCandidates,
            governedItemIds);
    }

    internal static IReadOnlyList<ValidationIssue>
        RegisterValidatedTreatmentItems(
            FileSystemManager fs,
            FileSystemManager.CanonicalWriteLease writeLease,
            string sessionId,
            string snapshotToken,
            MortalItemCarrierCatalog catalog,
            IEnumerable<string> knownItemIds,
            object treatmentContinuationAuthority,
            object reservationAuthority)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotToken);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(knownItemIds);
        ArgumentNullException.ThrowIfNull(treatmentContinuationAuthority);
        ArgumentNullException.ThrowIfNull(reservationAuthority);

        var newCandidates = catalog.Occurrences
            .Where(static occurrence =>
                occurrence.ItemId == null &&
                occurrence.CreationRef != null)
            .OrderBy(static occurrence => occurrence.CreationRef, StringComparer.Ordinal)
            .Select(occurrence => new NewCandidate(
                occurrence.CreationRef!,
                occurrence.Item.DeepClone().AsObject(),
                occurrence.FilePath,
                occurrence.JsonPath,
                CloneCarrier(occurrence.Carrier),
                IsEligiblePlayerEffectSource(occurrence),
                IsEquippedPlayerReference(catalog, occurrence.CreationRef!)))
            .ToArray();
        var stableCandidates = catalog.Occurrences
            .Where(static occurrence => occurrence.ItemId != null)
            .OrderBy(static occurrence => occurrence.ItemId, StringComparer.Ordinal)
            .Select(occurrence => new StableCandidate(
                occurrence.ItemId!,
                occurrence.Item.DeepClone().AsObject(),
                occurrence.FilePath,
                occurrence.JsonPath,
                CloneCarrier(occurrence.Carrier),
                IsEligiblePlayerEffectSource(occurrence),
                IsEquippedPlayerReference(catalog, occurrence.ItemId!)))
            .ToArray();
        var governedItemIds = knownItemIds
            .OrderBy(static itemId => itemId, StringComparer.Ordinal)
            .ToArray();
        var fingerprint = CreateFingerprint(
            newCandidates,
            stableCandidates,
            governedItemIds);
        return AcceptedTurnAuthorityRegistry
            .RegisterMortalTreatmentItemsValidated(
                fs,
                writeLease,
                sessionId,
                snapshotToken,
                fingerprint,
                newCandidates,
                stableCandidates,
                governedItemIds,
                treatmentContinuationAuthority,
                reservationAuthority);
    }

    internal static IReadOnlyList<EffectSourceExport> GetValidatedEffectSources(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        AcceptedTurnAuthorityRegistry.GetMortalItemEffectSources(
            fs,
            writeLease,
            sessionId,
            snapshotToken);

    internal static void InvalidateValidatedItems(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        AcceptedTurnAuthorityRegistry.InvalidateMortalItemsValidated(
            fs,
            writeLease);
    }

    internal static IReadOnlySet<EffectSourceOwnerKey> GetReplacedEffectSourceOwners(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        AcceptedTurnAuthorityRegistry.GetMortalItemReplacedSourceOwners(
            fs,
            writeLease,
            sessionId,
            snapshotToken);

    internal static bool TryGetAllocatedItemId(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken,
        string creationRef,
        out string itemId)
    {
        itemId = string.Empty;
        return AcceptedTurnAuthorityRegistry.TryGetMortalItemId(
            fs,
            writeLease,
            sessionId,
            snapshotToken,
            creationRef,
            out itemId);
    }

    internal static bool TryCaptureNormalizationSnapshot(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken,
        int turn,
        out MortalItemAcceptedTurnNormalizationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotToken);
        snapshot = null!;
        return AcceptedTurnAuthorityRegistry.TryCaptureMortalItemNormalizationSnapshot(
            fs,
            writeLease,
            sessionId,
            snapshotToken,
            turn,
            out snapshot);
    }

    internal static IReadOnlyList<MortalItemAcceptedTurnOwner> GetValidatedOwners(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        AcceptedTurnAuthorityRegistry.GetMortalItemOwners(
            fs,
            writeLease,
            sessionId,
            snapshotToken);

    internal static IReadOnlySet<string> GetMissingGovernedItemIds(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string sessionId,
        string snapshotToken) =>
        AcceptedTurnAuthorityRegistry.GetMissingGovernedMortalItemIds(
            fs,
            writeLease,
            sessionId,
            snapshotToken);

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
                ["item"] = candidate.Item.DeepClone(),
                ["filePath"] = candidate.FilePath,
                ["jsonPath"] = candidate.JsonPath,
                ["carrier"] = CarrierNode(candidate.Carrier),
                ["effectEligible"] = candidate.EffectEligible,
                ["equipped"] = candidate.Equipped
            }).ToArray()),
            ["stable"] = new JsonArray(stableCandidates.Select(candidate => (JsonNode)new JsonObject
            {
                ["itemId"] = candidate.ItemId,
                ["item"] = candidate.Item.DeepClone(),
                ["filePath"] = candidate.FilePath,
                ["jsonPath"] = candidate.JsonPath,
                ["carrier"] = CarrierNode(candidate.Carrier),
                ["effectEligible"] = candidate.EffectEligible,
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

    private static bool IsEligiblePlayerEffectSource(
        MortalItemCarrierOccurrence occurrence) =>
        string.Equals(
            occurrence.Carrier.Kind,
            "player_inventory",
            StringComparison.Ordinal) &&
        MortalItemLocalActionPolicy.IsCarriedByPlayer(occurrence.Item);

    private static MortalItemCarrierCoordinate CloneCarrier(
        MortalItemCarrierCoordinate carrier) =>
        carrier with { ContainerPath = carrier.ContainerPath.ToArray() };

    private static JsonObject CarrierNode(MortalItemCarrierCoordinate carrier) =>
        new()
        {
            ["kind"] = carrier.Kind,
            ["ownerId"] = carrier.OwnerId,
            ["containerId"] = carrier.ContainerId,
            ["containerPath"] = new JsonArray(
                carrier.ContainerPath.Select(static value => (JsonNode)value).ToArray())
        };

    private static IReadOnlySet<string> ItemPredicates(bool carried, bool equipped)
    {
        var predicates = new HashSet<string>(StringComparer.Ordinal);
        if (carried)
            predicates.Add("carried");
        if (equipped)
            predicates.Add("equipped");
        return predicates;
    }

    internal sealed class Cache
    {
        internal sealed class ValidatedPublicationTakeSnapshot
        {
            internal ValidatedPublicationTakeSnapshot(
                object cacheAuthority,
                object fence,
                string cacheFingerprint,
                MortalItemAcceptedTurnNormalizationSnapshot normalizationSnapshot)
            {
                CacheAuthority = cacheAuthority;
                Fence = fence;
                CacheFingerprint = cacheFingerprint;
                NormalizationSnapshot = normalizationSnapshot;
                PublicationFingerprint = normalizationSnapshot
                    .ComputePublicationFingerprint(cacheFingerprint);
            }

            internal object CacheAuthority { get; }
            internal object Fence { get; }
            internal string CacheFingerprint { get; }
            internal MortalItemAcceptedTurnNormalizationSnapshot
                NormalizationSnapshot { get; }
            internal string PublicationFingerprint { get; }
        }

        private readonly object _gate = new();
        private readonly object _cacheAuthority = new();
        private object _validatedFence = new();
        private string? _sessionId;
        private string? _snapshotToken;
        private string? _fingerprint;
        private bool _validated;
        private Dictionary<string, string> _itemIdsByCreationRef = new(StringComparer.Ordinal);
        private EffectSourceExport[] _sources = Array.Empty<EffectSourceExport>();
        private HashSet<EffectSourceOwnerKey> _replacedSourceOwners = new();
        private MortalItemAcceptedTurnOwner[] _owners = Array.Empty<MortalItemAcceptedTurnOwner>();
        private HashSet<string> _missingGovernedItemIds = new(StringComparer.Ordinal);

        internal bool HasValidated
        {
            get
            {
                lock (_gate)
                    return _validated;
            }
        }

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
                _validatedFence = new object();
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
                var owners = new List<MortalItemAcceptedTurnOwner>();
                foreach (var candidate in stableCandidates)
                {
                    owners.Add(new MortalItemAcceptedTurnOwner(
                        candidate.ItemId,
                        ItemRef: null,
                        candidate.FilePath,
                        candidate.JsonPath,
                        CloneCarrier(candidate.Carrier),
                        candidate.Item.DeepClone().AsObject(),
                        SameTurn: false));
                    if (!candidate.EffectEligible)
                        continue;
                    exports.Add(new EffectSourceExport(
                        "mortal_world",
                        "item",
                        candidate.ItemId,
                        candidate.Item["activeEffectDefinitions"]?.DeepClone().AsArray() ??
                            new JsonArray(),
                        Materializable: true,
                        Active: true,
                        SameTurn: true,
                        SatisfiedPredicates: ItemPredicates(
                            carried: true,
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
                    owners.Add(new MortalItemAcceptedTurnOwner(
                        itemId,
                        candidate.CreationRef,
                        candidate.FilePath,
                        candidate.JsonPath,
                        CloneCarrier(candidate.Carrier),
                        candidate.Item.DeepClone().AsObject(),
                        SameTurn: true));
                    if (!candidate.EffectEligible)
                        continue;
                    exports.Add(new EffectSourceExport(
                        "mortal_world",
                        "item",
                        itemId,
                        candidate.Item["activeEffectDefinitions"]?.DeepClone().AsArray() ??
                            new JsonArray(),
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
                _owners = owners.ToArray();
                _missingGovernedItemIds = governedItemIds
                    .Except(stableCandidates.Select(static candidate => candidate.ItemId), StringComparer.Ordinal)
                    .ToHashSet(StringComparer.Ordinal);
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
            {
                _validatedFence = new object();
                _validated = false;
            }
        }

        internal bool TryTakeValidatedTreatmentPublication(
            MortalItemAcceptedTurnNormalizationSnapshot expected,
            out ValidatedPublicationTakeSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(expected);
            lock (_gate)
            {
                if (_validated &&
                    _fingerprint is not null &&
                    expected.MatchesExactCacheState(
                        _sessionId,
                        _snapshotToken,
                        _itemIdsByCreationRef))
                {
                    snapshot = new ValidatedPublicationTakeSnapshot(
                        _cacheAuthority,
                        _validatedFence,
                        _fingerprint,
                        expected);
                    _validated = false;
                    return true;
                }

                snapshot = null!;
                return false;
            }
        }

        internal bool IsTreatmentPublicationTakeCurrent(
            ValidatedPublicationTakeSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            lock (_gate)
            {
                return PublicationTakeSnapshotAgrees(snapshot) &&
                       ReferenceEquals(_validatedFence, snapshot.Fence) &&
                       !_validated &&
                       CurrentCacheStateAgrees(snapshot);
            }
        }

        internal bool TryRearmValidatedTreatmentPublication(
            ValidatedPublicationTakeSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            lock (_gate)
            {
                if (!PublicationTakeSnapshotAgrees(snapshot) ||
                    !ReferenceEquals(_validatedFence, snapshot.Fence) ||
                    _validated ||
                    !CurrentCacheStateAgrees(snapshot))
                {
                    return false;
                }

                _validated = true;
                return true;
            }
        }

        internal bool IsTreatmentPublicationRearmed(
            ValidatedPublicationTakeSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            lock (_gate)
            {
                return PublicationTakeSnapshotAgrees(snapshot) &&
                       ReferenceEquals(_validatedFence, snapshot.Fence) &&
                       _validated &&
                       CurrentCacheStateAgrees(snapshot);
            }
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

        internal bool TryCaptureNormalizationSnapshot(
            string sessionId,
            string snapshotToken,
            int turn,
            out MortalItemAcceptedTurnNormalizationSnapshot snapshot)
        {
            lock (_gate)
            {
                if (Matches(sessionId, snapshotToken))
                {
                    snapshot = new MortalItemAcceptedTurnNormalizationSnapshot(
                        sessionId,
                        snapshotToken,
                        turn,
                        _itemIdsByCreationRef);
                    return true;
                }

                snapshot = null!;
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

        internal IReadOnlyList<MortalItemAcceptedTurnOwner> GetOwners(
            string sessionId,
            string snapshotToken)
        {
            lock (_gate)
            {
                if (!Matches(sessionId, snapshotToken))
                    return Array.Empty<MortalItemAcceptedTurnOwner>();
                return _owners.Select(static owner => owner with
                {
                    Carrier = CloneCarrier(owner.Carrier),
                    Item = owner.Item.DeepClone().AsObject()
                }).ToArray();
            }
        }

        internal IReadOnlySet<string> GetMissingGovernedItemIds(
            string sessionId,
            string snapshotToken)
        {
            lock (_gate)
            {
                return Matches(sessionId, snapshotToken)
                    ? new HashSet<string>(_missingGovernedItemIds, StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);
            }
        }

        private bool Matches(string sessionId, string snapshotToken) =>
            _validated &&
            string.Equals(_sessionId, sessionId, StringComparison.Ordinal) &&
            string.Equals(_snapshotToken, snapshotToken, StringComparison.Ordinal);

        private bool PublicationTakeSnapshotAgrees(
            ValidatedPublicationTakeSnapshot snapshot) =>
            ReferenceEquals(snapshot.CacheAuthority, _cacheAuthority) &&
            string.Equals(
                snapshot.PublicationFingerprint,
                snapshot.NormalizationSnapshot.ComputePublicationFingerprint(
                    snapshot.CacheFingerprint),
                StringComparison.Ordinal);

        private bool CurrentCacheStateAgrees(
            ValidatedPublicationTakeSnapshot snapshot) =>
            string.Equals(
                _fingerprint,
                snapshot.CacheFingerprint,
                StringComparison.Ordinal) &&
            snapshot.NormalizationSnapshot.MatchesExactCacheState(
                _sessionId,
                _snapshotToken,
                _itemIdsByCreationRef);
    }

    internal sealed record NewCandidate(
        string CreationRef,
        JsonObject Item,
        string FilePath,
        string JsonPath,
        MortalItemCarrierCoordinate Carrier,
        bool EffectEligible,
        bool Equipped);

    internal sealed record StableCandidate(
        string ItemId,
        JsonObject Item,
        string FilePath,
        string JsonPath,
        MortalItemCarrierCoordinate Carrier,
        bool EffectEligible,
        bool Equipped);
}
