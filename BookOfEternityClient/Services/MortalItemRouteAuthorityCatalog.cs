using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record MortalItemRouteAuthority(
    string Route,
    string AuthorityKind,
    string AuthorityId,
    MortalItemCarrierCoordinate Destination,
    IReadOnlyList<string> SourceItemIds);

internal sealed record MortalItemRouteAuthorityIssue(
    string Code,
    string Path,
    string Message,
    string Expected,
    string Actual,
    string FilePath,
    string? CreationRef = null);

/// <summary>
/// Resolves every raw Mortal item creation to one exact route-specific
/// authority before the normalizer assigns client-owned identity evidence.
/// </summary>
internal sealed class MortalItemRouteAuthorityCatalog
{
    private const string PlayerInventoryPath = "game_state/inventory/items.json";
    private const string NpcCorePath = "game_state/npcs/npc_core.json";
    private const string NpcCommandsPath = "game_state/npcs/npc_inventory.json";
    private const string CurrentLocationPath = "game_state/world/current_location.json";
    private const string OffscreenLocationStoragePath =
        "game_state/world/location_storage_contents.json";
    private const string VehiclesPath = "game_state/misc/vehicles.json";
    private const string QuestHistoryPath = "game_state/quests/quest_history.json";
    private const string TurnRequestPath = "input/turn_request.json";
    private const string PendingSnapshotPath =
        "game_state/control/pending_turn_snapshot.json";

    private MortalItemRouteAuthorityCatalog(
        Dictionary<string, MortalItemRouteAuthority> byCreationRef,
        List<MortalItemRouteAuthorityIssue> issues)
    {
        ByCreationRef = byCreationRef;
        Issues = issues.ToArray();
    }

    internal IReadOnlyDictionary<string, MortalItemRouteAuthority> ByCreationRef { get; }

    internal IReadOnlyList<MortalItemRouteAuthorityIssue> Issues { get; }

    internal static int MeasureTradeAuthorityWork(
        JsonObject? requestsRoot,
        JsonObject? npcRoot)
    {
        var work = new TradeAuthorityWorkCounter();
        var owners = BuildNpcOwners(npcRoot, work);
        _ = BuildTradeAuthorities(requestsRoot, owners, npcRoot, work);
        return work.TotalVisited;
    }

    internal static async Task<MortalItemRouteAuthorityCatalog> BuildAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease = null,
        IReadOnlyList<MortalLocationStorageCoordinate>? acceptedStorageCoordinates = null)
    {
        ArgumentNullException.ThrowIfNull(fs);

        var player = ParseObject(await ReadAsync(fs, writeLease, PlayerInventoryPath));
        var npcCore = ParseObject(await ReadAsync(fs, writeLease, NpcCorePath));
        var npcCommands = ParseObject(await ReadAsync(fs, writeLease, NpcCommandsPath));
        var currentLocation = ParseObject(await ReadAsync(fs, writeLease, CurrentLocationPath));
        var offscreenLocationStorage = ParseObject(await ReadAsync(
            fs,
            writeLease,
            OffscreenLocationStoragePath));
        var vehicles = ParseVehicles(await ReadAsync(fs, writeLease, VehiclesPath));
        var turnRequest = ParseObject(await ReadAsync(fs, writeLease, TurnRequestPath));
        var questHistory = ParseObject(await ReadAsync(fs, writeLease, QuestHistoryPath));
        var snapshotManifest = ParseObject(await ReadAsync(
            fs,
            writeLease,
            PendingSnapshotPath));
        var baselineNpcCore = ParseObject(await ReadSnapshotFileAsync(
            fs,
            writeLease,
            snapshotManifest,
            NpcCorePath));
        var baselineLocation = ParseObject(await ReadSnapshotFileAsync(
            fs,
            writeLease,
            snapshotManifest,
            CurrentLocationPath));
        var baselineCraftRequest = ParseObject(await ReadSnapshotFileAsync(
            fs,
            writeLease,
            snapshotManifest,
            CraftRequestState.PendingRequestPath));
        var baselineTradeRequests = ParseObject(await ReadSnapshotFileAsync(
            fs,
            writeLease,
            snapshotManifest,
            NpcTradeRequestState.PendingRequestPath));

        var carrierCatalog = MortalItemCarrierCatalog.Build(
            new MortalItemCarrierCatalogInput(
                player,
                npcCore,
                npcCommands,
                currentLocation,
                vehicles,
                new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                offscreenLocationStorage));
        var builder = new Builder(
            turnRequest,
            npcCore,
            npcCommands,
            baselineCraftRequest,
            baselineTradeRequests,
            questHistory,
            baselineNpcCore,
            baselineLocation,
            acceptedStorageCoordinates);
        foreach (var occurrence in carrierCatalog.Occurrences)
        {
            if (IsRawCreation(occurrence.Item))
                builder.Add(occurrence);
        }

        return new MortalItemRouteAuthorityCatalog(
            builder.ByCreationRef,
            builder.Issues);
    }

    private static Task<string?> ReadAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path) =>
        writeLease == null
            ? fs.ReadFileAsync(path)
            : fs.ReadFileAsync(writeLease, path);

    private static async Task<string?> ReadSnapshotFileAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        JsonObject? manifest,
        string canonicalPath)
    {
        if (manifest?["files"] is not JsonObject files ||
            ReadExactString(files[canonicalPath]) is not { } snapshotPath)
        {
            return null;
        }

        var path = snapshotPath;
        if (Path.IsPathFullyQualified(path))
        {
            var relative = Path.GetRelativePath(
                Path.GetFullPath(fs.BasePath),
                Path.GetFullPath(path));
            if (Path.IsPathRooted(relative) ||
                string.Equals(relative, "..", StringComparison.Ordinal) ||
                relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
            {
                return null;
            }

            path = relative.Replace('\\', '/');
        }

        return await ReadAsync(fs, writeLease, path);
    }

    private static JsonObject? ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception exception) when (
            exception is System.Text.Json.JsonException or
                InvalidOperationException or
                ArgumentException)
        {
            return null;
        }
    }

    private static JsonObject? ParseVehicles(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var node = JsonNode.Parse(json);
            return node switch
            {
                JsonObject root => root,
                JsonArray vehicles => new JsonObject { ["vehicles"] = vehicles.DeepClone() },
                _ => null
            };
        }
        catch (Exception exception) when (
            exception is System.Text.Json.JsonException or
                InvalidOperationException or
                ArgumentException)
        {
            return null;
        }
    }

    private static bool IsRawCreation(JsonObject item) =>
        item.ContainsKey("creationRef") ||
        item.TryGetPropertyValue("existedId", out var existedId) && existedId == null;

    private sealed class Builder
    {
        private readonly int _turn;
        private readonly HashSet<string> _lootAuthorities;
        private readonly Dictionary<string, NpcAddAuthorityCandidate>
            _npcAddAuthoritiesByCreationRef;
        private readonly Dictionary<string, string> _newNpcAuthoritiesByCreationRef;
        private readonly JsonObject? _craftRequest;
        private readonly Dictionary<string, TradeOfferAuthorityCandidate>
            _tradeAuthoritiesByCreationRef;
        private readonly Dictionary<string, string> _questAuthorityByCreationRef;
        private readonly HashSet<string> _baselineStorageAuthorities;
        private readonly Dictionary<string, SameTurnStorageAuthority>
            _sameTurnStorageAuthoritiesByCarrier;

        internal Builder(
            JsonObject? turnRequest,
            JsonObject? npcCore,
            JsonObject? npcCommands,
            JsonObject? craftRequest,
            JsonObject? tradeRequests,
            JsonObject? questHistory,
            JsonObject? baselineNpcCore,
            JsonObject? baselineLocation,
            IReadOnlyList<MortalLocationStorageCoordinate>? acceptedStorageCoordinates)
        {
            _turn = ReadInt(turnRequest?["turnNumber"]);
            _lootAuthorities = BuildLootAuthorities(turnRequest, _turn);
            var currentNpcOwners = BuildNpcOwners(npcCore);
            var baselineNpcOwners = BuildNpcOwners(baselineNpcCore);
            _npcAddAuthoritiesByCreationRef = BuildNpcAddAuthorities(
                npcCommands,
                _turn,
                currentNpcOwners,
                baselineNpcOwners);
            _newNpcAuthoritiesByCreationRef = BuildNewNpcAuthorities(
                npcCore,
                baselineNpcOwners);
            _craftRequest = craftRequest;
            _tradeAuthoritiesByCreationRef = BuildTradeAuthorities(
                tradeRequests,
                currentNpcOwners,
                npcCore);
            _questAuthorityByCreationRef = BuildQuestAuthorities(questHistory);
            _baselineStorageAuthorities = BuildStorageAuthorities(baselineLocation);
            _sameTurnStorageAuthoritiesByCarrier = BuildSameTurnStorageAuthorities(
                acceptedStorageCoordinates);
        }

        internal Dictionary<string, MortalItemRouteAuthority> ByCreationRef { get; } =
            new(StringComparer.Ordinal);

        internal List<MortalItemRouteAuthorityIssue> Issues { get; } = new();

        internal void Add(MortalItemCarrierOccurrence occurrence)
        {
            var creationRef = ReadExactString(occurrence.Item["creationRef"]);
            var envelope = occurrence.Item["materialization"] as JsonObject;
            var route = ReadExactString(envelope?["route"]);
            var authority = envelope?["sourceAuthority"] as JsonObject;
            var authorityKind = ReadExactString(authority?["kind"]);
            var actualAuthorityId = ReadExactString(authority?["authorityId"]);
            if (creationRef == null || route == null || authorityKind == null)
            {
                AddIssue(
                    occurrence,
                    "mortal_item_materialization_route_authority_missing",
                    "A raw Mortal item route requires exact creationRef, route, and source authority fields.",
                    "complete route authority",
                    actualAuthorityId ?? "missing",
                    creationRef);
                return;
            }

            if (ByCreationRef.ContainsKey(creationRef))
            {
                AddIssue(
                    occurrence,
                    "mortal_item_materialization_duplicate_creation_ref",
                    "A route authority may bind one raw creationRef only once.",
                    "one exact route occurrence",
                    creationRef,
                    creationRef);
                return;
            }

            var derived = Derive(occurrence, route, authorityKind, creationRef);
            if (derived == null)
                return;
            if (!string.Equals(actualAuthorityId, derived.AuthorityId, StringComparison.Ordinal))
            {
                AddIssue(
                    occurrence,
                    "mortal_item_materialization_route_authority_mismatch",
                    "The raw item source authority does not match its exact route authority.",
                    derived.AuthorityId,
                    actualAuthorityId ?? "missing",
                    creationRef);
                return;
            }

            ByCreationRef.Add(creationRef, derived);
        }

        private MortalItemRouteAuthority? Derive(
            MortalItemCarrierOccurrence occurrence,
            string route,
            string authorityKind,
            string creationRef)
        {
            string? expectedAuthorityId;
            IReadOnlyList<string> sourceItemIds = Array.Empty<string>();
            var destination = occurrence.Carrier;
            if (string.Equals(
                    occurrence.Carrier.Kind,
                    "player_inventory",
                    StringComparison.Ordinal) &&
                !occurrence.JsonPath.StartsWith(
                    $"{PlayerInventoryPath}.UpdateInventory[",
                    StringComparison.Ordinal))
            {
                AddIssue(
                    occurrence,
                    "mortal_item_materialization_route_authority_mismatch",
                    "A raw Mortal item entering the player carrier must use the UpdateInventory route surface.",
                    $"{PlayerInventoryPath}.UpdateInventory[]",
                    occurrence.JsonPath,
                    creationRef);
                return null;
            }

            if (string.Equals(
                    occurrence.Carrier.Kind,
                    "location_storage",
                    StringComparison.Ordinal) &&
                !IsAcceptedStorageCarrier(occurrence.Carrier))
            {
                AddIssue(
                    occurrence,
                    "mortal_item_materialization_route_authority_missing",
                    "A Mortal item route may target only an exact validated pre-turn or accepted same-turn current-location storage.",
                    "exact accepted location/storage carrier authority",
                    $"{occurrence.Carrier.OwnerId}:{occurrence.Carrier.ContainerId ?? "missing"}",
                    creationRef);
                return null;
            }

            switch (route)
            {
                case "player_acquisition":
                    expectedAuthorityId = _turn > 0 ? $"turn_{_turn}" : null;
                    break;
                case "npc_acquisition":
                    expectedAuthorityId = DeriveNpcAddAuthority(
                        occurrence,
                        creationRef,
                        out destination);
                    break;
                case "new_npc_inventory":
                    expectedAuthorityId = DeriveNewNpcAuthority(occurrence, creationRef);
                    break;
                case "loot_acquisition":
                {
                    var actual = ReadExactString(
                        occurrence.Item["materialization"]?["sourceAuthority"]?["authorityId"]);
                    expectedAuthorityId = ResolveExpectedAuthority(
                        _lootAuthorities,
                        actual);
                    break;
                }
                case "craft_output":
                    expectedAuthorityId = string.Equals(
                        ReadExactString(_craftRequest?["status"]),
                        "pending_gm_resolution",
                        StringComparison.Ordinal)
                        ? ReadExactString(_craftRequest?["requestId"])
                        : null;
                    if (expectedAuthorityId != null)
                        sourceItemIds = ReadExactStringArray(_craftRequest?["sourceItemIds"]);
                    break;
                case "trade_output":
                {
                    expectedAuthorityId =
                        _tradeAuthoritiesByCreationRef.TryGetValue(
                            creationRef,
                            out var tradeAuthority) &&
                        TradeItemMatchesOffer(
                            tradeAuthority,
                            occurrence.Item)
                            ? tradeAuthority.RequestId
                            : null;
                    break;
                }
                case "quest_reward":
                    expectedAuthorityId = _questAuthorityByCreationRef.GetValueOrDefault(creationRef);
                    break;
                case "storage_placement":
                {
                    var candidate = occurrence.Carrier.ContainerId == null
                        ? null
                        : $"{occurrence.Carrier.OwnerId}:{occurrence.Carrier.ContainerId}";
                    if (candidate != null &&
                        _baselineStorageAuthorities.Contains(candidate))
                    {
                        expectedAuthorityId = candidate;
                    }
                    else if (candidate != null &&
                             _sameTurnStorageAuthoritiesByCarrier.TryGetValue(
                                 candidate,
                                 out var sameTurnAuthority))
                    {
                        expectedAuthorityId = sameTurnAuthority.AuthorityId;
                        destination = sameTurnAuthority.Destination;
                    }
                    else
                    {
                        expectedAuthorityId = null;
                    }
                    break;
                }
                default:
                    AddIssue(
                        occurrence,
                        "mortal_item_materialization_route_authority_mismatch",
                        "The raw item uses an unsupported Mortal creation route.",
                        "supported Mortal item route",
                        route,
                        creationRef);
                    return null;
            }

            if (expectedAuthorityId == null)
            {
                AddIssue(
                    occurrence,
                    "mortal_item_materialization_route_authority_missing",
                    "The route-specific current authority could not be resolved.",
                    $"one current {route} authority",
                    "missing or unresolved",
                    creationRef);
                return null;
            }

            if (!CarrierAllowed(route, occurrence.Carrier))
            {
                AddIssue(
                    occurrence,
                    "mortal_item_materialization_route_authority_mismatch",
                    "The route does not authorize this destination carrier.",
                    $"carrier allowed by {route}",
                    occurrence.Carrier.Kind,
                    creationRef);
                return null;
            }

            return new MortalItemRouteAuthority(
                route,
                authorityKind,
                expectedAuthorityId,
                destination,
                sourceItemIds);
        }

        private static bool TradeItemMatchesOffer(
            TradeOfferAuthorityCandidate authority,
            JsonObject item)
        {
            var offerProjection = CreateTradeItemSemanticProjection(authority.ItemData);
            var rawProjection = CreateTradeItemSemanticProjection(item);
            return JsonNode.DeepEquals(offerProjection, rawProjection);
        }

        private static JsonObject CreateTradeItemSemanticProjection(JsonObject item)
        {
            var projection = item.DeepClone().AsObject();
            foreach (var property in new[]
                     {
                         "itemId",
                         "id",
                         "initialId",
                         "existedId",
                         "creationRef",
                         "materialization",
                         MortalItemMaterializationContract.ReceiptProperty
                     })
            {
                projection.Remove(property);
            }

            return projection;
        }

        private bool IsAcceptedStorageCarrier(MortalItemCarrierCoordinate carrier)
        {
            if (carrier.ContainerId == null)
                return false;
            var key = $"{carrier.OwnerId}:{carrier.ContainerId}";
            return _baselineStorageAuthorities.Contains(key) ||
                   _sameTurnStorageAuthoritiesByCarrier.ContainsKey(key);
        }

        private static Dictionary<string, SameTurnStorageAuthority>
            BuildSameTurnStorageAuthorities(
                IReadOnlyList<MortalLocationStorageCoordinate>? coordinates)
        {
            var result = new Dictionary<string, SameTurnStorageAuthority>(
                StringComparer.Ordinal);
            if (coordinates == null)
                return result;

            foreach (var coordinate in coordinates)
            {
                if (coordinate.InitialLocationId == null)
                    continue;
                var authorityId =
                    $"{coordinate.InitialLocationId}:{coordinate.StorageId}";
                var authority = new SameTurnStorageAuthority(
                    authorityId,
                    new MortalItemCarrierCoordinate(
                        "location_storage",
                        coordinate.LocationId,
                        coordinate.StorageId,
                        Array.Empty<string>()));
                result.TryAdd(authorityId, authority);
                result.TryAdd(
                    $"{coordinate.LocationId}:{coordinate.StorageId}",
                    authority);
            }

            return result;
        }

        private sealed record SameTurnStorageAuthority(
            string AuthorityId,
            MortalItemCarrierCoordinate Destination);

        private static string? ResolveExpectedAuthority(
            IReadOnlySet<string> candidates,
            string? actual)
        {
            if (actual != null && candidates.Contains(actual))
                return actual;
            return candidates.Count == 1 ? candidates.Single() : null;
        }

        private string? DeriveNpcAddAuthority(
            MortalItemCarrierOccurrence occurrence,
            string creationRef,
            out MortalItemCarrierCoordinate destination)
        {
            destination = occurrence.Carrier;
            if (!_npcAddAuthoritiesByCreationRef.TryGetValue(
                    creationRef,
                    out var candidate) ||
                !string.Equals(
                    candidate.NpcId,
                    occurrence.Carrier.OwnerId,
                    StringComparison.Ordinal))
            {
                return null;
            }

            if (candidate.DestinationContainerId != null)
            {
                if (occurrence.Carrier.ContainerPath.Count > 0 &&
                    (occurrence.Carrier.ContainerPath.Count != 1 ||
                     !string.Equals(
                         occurrence.Carrier.ContainerPath[0],
                         candidate.DestinationContainerId,
                         StringComparison.Ordinal)))
                {
                    return null;
                }

                destination = occurrence.Carrier with
                {
                    ContainerPath = new[] { candidate.DestinationContainerId }
                };
            }

            return candidate.AuthorityId;
        }

        private string? DeriveNewNpcAuthority(
            MortalItemCarrierOccurrence occurrence,
            string creationRef)
        {
            return _newNpcAuthoritiesByCreationRef.TryGetValue(
                       creationRef,
                       out var authorityId) &&
                   string.Equals(
                       authorityId,
                       occurrence.Carrier.OwnerId,
                       StringComparison.Ordinal)
                ? authorityId
                : null;
        }

        private void AddIssue(
            MortalItemCarrierOccurrence occurrence,
            string code,
            string message,
            string expected,
            string actual,
            string? creationRef)
        {
            Issues.Add(new MortalItemRouteAuthorityIssue(
                code,
                occurrence.JsonPath,
                message,
                expected,
                actual,
                occurrence.FilePath,
                creationRef));
        }

        private static bool CarrierAllowed(
            string route,
            MortalItemCarrierCoordinate carrier) =>
            route switch
            {
                "player_acquisition" => carrier.Kind == "player_inventory",
                "npc_acquisition" or "new_npc_inventory" =>
                    carrier.Kind == "npc_inventory",
                "storage_placement" => carrier.Kind == "location_storage",
                "trade_output" => carrier.Kind == "player_inventory",
                "loot_acquisition" or "craft_output" or "quest_reward" => carrier.Kind is
                        "player_inventory" or "npc_inventory" or "location_storage",
                _ => false
            };
    }

    private sealed record NpcAddAuthorityCandidate(
        string NpcId,
        string AuthorityId,
        string? DestinationContainerId);

    private sealed record TradeOfferAuthorityCandidate(
        string RequestId,
        string SlotId,
        JsonObject ItemData);

    private sealed record TradeOfferAuthorityTemplate(
        string SlotId,
        JsonObject ItemData);

    private sealed record ValidatedTradeAuthorityRequest(
        string RequestId,
        TradeNpcAuthoritySurface Surface);

    private readonly record struct TradeReceiptKey(
        string RequestId,
        string NpcId,
        string TradeCycleId,
        string MerchantProfile,
        int ItemCount);

    private sealed record TradeNpcAuthoritySurface(
        string TradeCycleId,
        string MerchantProfile,
        int RefreshAfterWorldDate,
        IReadOnlyList<TradeOfferAuthorityTemplate> Offers);

    private sealed record TradeReceiptAuthorityIndex(
        IReadOnlyDictionary<TradeReceiptKey, int> Counts,
        IReadOnlyDictionary<string, int> RequestIdCounts);

    private sealed class TradeAuthorityWorkCounter
    {
        internal int TotalVisited { get; private set; }

        internal void Visit() => TotalVisited++;
    }

    private static Dictionary<string, IReadOnlyList<JsonObject>> BuildNpcOwners(
        JsonObject? root,
        TradeAuthorityWorkCounter? work = null)
    {
        var mutable = new Dictionary<string, List<JsonObject>>(StringComparer.Ordinal);
        if (root != null)
        {
            foreach (var npc in EnumerateNpcObjects(root))
            {
                work?.Visit();
                var npcId = ReadNpcIdentity(npc);
                if (npcId == null)
                    continue;
                if (!mutable.TryGetValue(npcId, out var owners))
                {
                    owners = new List<JsonObject>();
                    mutable.Add(npcId, owners);
                }
                owners.Add(npc);
            }
        }

        return mutable.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<JsonObject>)pair.Value.ToArray(),
            StringComparer.Ordinal);
    }

    private static Dictionary<string, NpcAddAuthorityCandidate>
        BuildNpcAddAuthorities(
            JsonObject? commandsRoot,
            int turn,
            IReadOnlyDictionary<string, IReadOnlyList<JsonObject>> currentNpcOwners,
            IReadOnlyDictionary<string, IReadOnlyList<JsonObject>> baselineNpcOwners)
    {
        var result = new Dictionary<string, NpcAddAuthorityCandidate>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        if (turn < 1 || commandsRoot?["NPCInventoryAdds"] is not JsonArray adds)
            return result;

        for (var index = 0; index < adds.Count; index++)
        {
            if (adds[index] is not JsonObject command ||
                command["item"] is not JsonObject item ||
                ReadExactString(item["creationRef"]) is not { } creationRef ||
                ReadNpcIdentity(command) is not { } npcId ||
                !currentNpcOwners.TryGetValue(npcId, out var currentOwners) ||
                currentOwners.Count != 1 ||
                !baselineNpcOwners.TryGetValue(npcId, out var baselineOwners) ||
                baselineOwners.Count != 1)
            {
                continue;
            }

            var destinationNode = command["destinationContainerId"];
            var destinationContainerId = ReadExactString(destinationNode);
            if (destinationNode != null && destinationContainerId == null)
                continue;

            var candidate = new NpcAddAuthorityCandidate(
                npcId,
                $"npc_inventory_add:{turn}:{index}:{npcId}",
                destinationContainerId);
            if (duplicates.Contains(creationRef))
                continue;
            if (!result.TryAdd(creationRef, candidate))
            {
                result.Remove(creationRef);
                duplicates.Add(creationRef);
            }
        }

        return result;
    }

    private static Dictionary<string, string> BuildNewNpcAuthorities(
        JsonObject? npcCore,
        IReadOnlyDictionary<string, IReadOnlyList<JsonObject>> baselineNpcOwners)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        if (npcCore == null)
            return result;

        foreach (var npc in EnumerateNpcObjects(npcCore))
        {
            var permanentNode = npc["NPCId"];
            var permanentId = ReadExactString(permanentNode);
            var initialId = ReadExactString(npc["initialId"]);
            if (permanentId != null ||
                permanentNode != null && permanentId == null ||
                initialId == null ||
                baselineNpcOwners.ContainsKey(initialId) ||
                npc["inventory"] is not JsonArray inventory)
            {
                continue;
            }

            foreach (var item in inventory.OfType<JsonObject>())
            {
                var creationRef = ReadExactString(item["creationRef"]);
                if (creationRef == null || duplicates.Contains(creationRef))
                    continue;
                if (!result.TryAdd(creationRef, initialId))
                {
                    result.Remove(creationRef);
                    duplicates.Add(creationRef);
                }
            }
        }

        return result;
    }

    private static HashSet<string> BuildLootAuthorities(
        JsonObject? turnRequest,
        int turn)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (turn < 1 ||
            turnRequest?["additionalContext"]?["lootForCurrentTurn"] is not JsonArray templates)
        {
            return result;
        }

        for (var index = 0; index < templates.Count; index++)
        {
            var baseName = templates[index] switch
            {
                JsonObject obj =>
                    ReadExactString(obj["baseName"]) ?? ReadExactString(obj["name"]),
                JsonValue value when value.TryGetValue<string>(out var text) =>
                    IsExactString(text) ? text : null,
                _ => null
            };
            if (baseName != null)
                result.Add($"loot_template:{turn}:{index}:{baseName}");
        }

        return result;
    }

    private static Dictionary<string, TradeOfferAuthorityCandidate>
        BuildTradeAuthorities(
        JsonObject? requestsRoot,
        IReadOnlyDictionary<string, IReadOnlyList<JsonObject>> npcOwners,
        JsonObject? npcRoot,
        TradeAuthorityWorkCounter? work = null)
    {
        var result = new Dictionary<string, TradeOfferAuthorityCandidate>(
            StringComparer.Ordinal);
        if (requestsRoot?[NpcTradeRequestState.RequestsProperty] is not JsonArray requests)
            return result;

        var requestsById = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var duplicateRequestIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in requests)
        {
            work?.Visit();
            if (node is not JsonObject request ||
                ReadExactString(request["requestId"]) is not { } requestId ||
                duplicateRequestIds.Contains(requestId))
            {
                continue;
            }

            if (!requestsById.TryAdd(requestId, request))
            {
                requestsById.Remove(requestId);
                duplicateRequestIds.Add(requestId);
            }
        }

        var surfaces = BuildTradeNpcAuthoritySurfaces(npcOwners, work);
        var receiptIndex = BuildTradeReceiptAuthorityIndex(npcRoot, work);
        var validRequestsByNpcId = new Dictionary<string, ValidatedTradeAuthorityRequest>(
            StringComparer.Ordinal);
        var ambiguousNpcIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in requestsById)
        {
            var request = pair.Value;
            var requestId = ReadExactString(request["requestId"]);
            var npcId = ReadExactString(request["npcId"]);
            var merchantProfile = ReadExactString(request["merchantProfile"]);
            var tradeCycleId = ReadExactString(request["tradeCycleId"]);
            var derivedTradeSlotCount = ReadInt(request["derivedTradeSlotCount"]);
            var refreshAfterWorldDate = ReadInt(request["refreshAfterWorldDate"]);
            if (requestId == null || npcId == null || merchantProfile == null ||
                tradeCycleId == null || derivedTradeSlotCount < 1 ||
                refreshAfterWorldDate < 1)
            {
                continue;
            }

            if (!surfaces.TryGetValue(npcId, out var surface) ||
                !string.Equals(
                    surface.TradeCycleId,
                    tradeCycleId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    surface.MerchantProfile,
                    merchantProfile,
                    StringComparison.Ordinal) ||
                surface.RefreshAfterWorldDate != refreshAfterWorldDate ||
                surface.Offers.Count != derivedTradeSlotCount)
            {
                continue;
            }

            var receiptKey = new TradeReceiptKey(
                requestId,
                npcId,
                tradeCycleId,
                merchantProfile,
                derivedTradeSlotCount);
            if (!receiptIndex.Counts.TryGetValue(receiptKey, out var receiptCount) ||
                receiptCount != 1 ||
                receiptIndex.RequestIdCounts.GetValueOrDefault(requestId) != 1 ||
                ambiguousNpcIds.Contains(npcId))
            {
                continue;
            }

            var validated = new ValidatedTradeAuthorityRequest(requestId, surface);
            if (!validRequestsByNpcId.TryAdd(npcId, validated))
            {
                validRequestsByNpcId.Remove(npcId);
                ambiguousNpcIds.Add(npcId);
            }
        }

        var duplicateCreationRefs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var validated in validRequestsByNpcId.Values)
        {
            foreach (var offer in validated.Surface.Offers)
            {
                work?.Visit();
                var creationRef = offer.SlotId;
                if (duplicateCreationRefs.Contains(creationRef))
                    continue;
                var candidate = new TradeOfferAuthorityCandidate(
                    validated.RequestId,
                    offer.SlotId,
                    offer.ItemData);
                if (!result.TryAdd(creationRef, candidate))
                {
                    result.Remove(creationRef);
                    duplicateCreationRefs.Add(creationRef);
                }
            }
        }

        return result;
    }

    private static Dictionary<string, TradeNpcAuthoritySurface>
        BuildTradeNpcAuthoritySurfaces(
            IReadOnlyDictionary<string, IReadOnlyList<JsonObject>> npcOwners,
            TradeAuthorityWorkCounter? work)
    {
        var result = new Dictionary<string, TradeNpcAuthoritySurface>(
            StringComparer.Ordinal);
        foreach (var pair in npcOwners)
        {
            work?.Visit();
            if (pair.Value.Count != 1 ||
                pair.Value[0]["tradeInventory"] is not JsonObject tradeInventory ||
                ReadExactString(tradeInventory["tradeCycleId"]) is not { } tradeCycleId ||
                tradeInventory["items"] is not JsonArray tradeItems ||
                tradeItems.Count < 1)
            {
                continue;
            }

            var refreshAfterWorldDate = ReadInt(tradeInventory["refreshAfterWorldDate"]);
            if (refreshAfterWorldDate < 1)
                continue;

            var offers = new List<TradeOfferAuthorityTemplate>(tradeItems.Count);
            var slotIds = new HashSet<string>(StringComparer.Ordinal);
            string? merchantProfile = null;
            var valid = true;
            foreach (var node in tradeItems)
            {
                work?.Visit();
                if (node is not JsonObject tradeItem ||
                    ReadExactString(tradeItem["merchantProfile"]) is not { } itemProfile ||
                    ReadExactString(tradeItem["slotId"]) is not { } slotId ||
                    !slotIds.Add(slotId) ||
                    ReadExactString(tradeItem["itemId"]) is not { } itemId ||
                    tradeItem["itemData"] is not JsonObject itemData ||
                    !string.Equals(
                        ReadExactString(itemData["itemId"]),
                        itemId,
                        StringComparison.Ordinal) ||
                    merchantProfile != null && !string.Equals(
                        merchantProfile,
                        itemProfile,
                        StringComparison.Ordinal))
                {
                    valid = false;
                    break;
                }

                merchantProfile ??= itemProfile;
                offers.Add(new TradeOfferAuthorityTemplate(
                    slotId,
                    itemData));
            }

            if (!valid || merchantProfile == null)
                continue;

            result.Add(pair.Key, new TradeNpcAuthoritySurface(
                tradeCycleId,
                merchantProfile,
                refreshAfterWorldDate,
                offers));
        }

        return result;
    }

    private static TradeReceiptAuthorityIndex BuildTradeReceiptAuthorityIndex(
        JsonObject? npcRoot,
        TradeAuthorityWorkCounter? work)
    {
        var counts = new Dictionary<TradeReceiptKey, int>();
        var requestIdCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (npcRoot?[NpcTradeRequestState.UpdateReceiptsProperty] is not JsonArray receipts)
            return new TradeReceiptAuthorityIndex(counts, requestIdCounts);

        foreach (var node in receipts)
        {
            work?.Visit();
            if (node is not JsonObject receipt)
                continue;

            var itemCount = ReadInt(receipt["itemCount"]);
            if (ReadExactString(receipt["requestId"]) is not { } requestId ||
                ReadExactString(receipt["npcId"]) is not { } npcId ||
                ReadExactString(receipt["tradeCycleId"]) is not { } tradeCycleId ||
                ReadExactString(receipt["merchantProfile"]) is not { } merchantProfile ||
                !string.Equals(
                    ReadExactString(receipt["status"]),
                    NpcTradeRequestState.ReceiptStatusReady,
                    StringComparison.Ordinal) ||
                itemCount < 0 ||
                ReadInt(receipt["resolvedAtTurn"]) < 1 ||
                ReadExactString(receipt["resolvedAtUtc"]) == null)
            {
                continue;
            }

            var key = new TradeReceiptKey(
                requestId,
                npcId,
                tradeCycleId,
                merchantProfile,
                itemCount);
            counts[key] = counts.GetValueOrDefault(key) + 1;
            requestIdCounts[requestId] = requestIdCounts.GetValueOrDefault(requestId) + 1;
        }

        return new TradeReceiptAuthorityIndex(counts, requestIdCounts);
    }

    private static Dictionary<string, string> BuildQuestAuthorities(JsonObject? root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var duplicateCreationRefs = new HashSet<string>(StringComparer.Ordinal);
        if (root?["questRewards"] is not JsonArray rewards)
            return result;

        foreach (var reward in rewards.OfType<JsonObject>())
        {
            var authorityId = ReadExactString(reward["rewardId"]);
            if (authorityId == null || reward["itemsReceived"] is not JsonArray items)
                continue;
            foreach (var item in items.OfType<JsonObject>())
            {
                if (QuestRewardAuthority.IsExplicitlyUnavailableReward(item))
                    continue;

                var creationRef = ReadExactString(item["creationRef"]);
                if (creationRef == null || duplicateCreationRefs.Contains(creationRef))
                    continue;
                if (!result.TryAdd(creationRef, authorityId))
                {
                    result.Remove(creationRef);
                    duplicateCreationRefs.Add(creationRef);
                }
            }
        }

        return result;
    }

    private static IEnumerable<JsonObject> EnumerateNpcObjects(JsonObject root)
    {
        foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
        {
            if (root[section] is not JsonArray npcs)
                continue;
            foreach (var npc in npcs.OfType<JsonObject>())
                yield return npc;
        }
    }

    private static HashSet<string> BuildStorageAuthorities(JsonObject? root)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (root == null)
            return result;
        var location = root["currentLocationData"] as JsonObject ?? root;
        var locationId = ReadExactString(location["locationId"]);
        if (locationId == null ||
            location["locationStorages"] is not JsonArray storages)
        {
            return result;
        }

        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var storage in storages.OfType<JsonObject>())
        {
            var storageId = ReadExactString(storage["storageId"]);
            if (storageId == null || duplicates.Contains(storageId))
                continue;
            var authorityId = $"{locationId}:{storageId}";
            if (!result.Add(authorityId))
            {
                result.Remove(authorityId);
                duplicates.Add(storageId);
            }
        }

        return result;
    }

    private static string? ReadNpcIdentity(JsonObject obj) =>
        ReadExactString(obj["NPCId"]) ??
        ReadExactString(obj["npcId"]) ??
        ReadExactString(obj["id"]) ??
        ReadExactString(obj["initialId"]);

    private static IReadOnlyList<string> ReadExactStringArray(JsonNode? node)
    {
        if (node is not JsonArray array)
            return Array.Empty<string>();

        var result = new List<string>(array.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in array)
        {
            var text = ReadExactString(value);
            if (text != null && seen.Add(text))
                result.Add(text);
        }

        return result;
    }

    private static int ReadInt(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<int>(out var number)
            ? number
            : 0;

    private static string? ReadExactString(JsonNode? node)
    {
        if (node is not JsonValue value ||
            !value.TryGetValue<string>(out var text) ||
            !IsExactString(text))
        {
            return null;
        }

        return text;
    }

    private static bool IsExactString(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
