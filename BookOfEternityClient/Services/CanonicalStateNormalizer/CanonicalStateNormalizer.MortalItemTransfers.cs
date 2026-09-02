using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private Task<MortalItemRouteAuthorityCatalog>
        BuildOrdinaryMortalItemRouteAuthorityCatalogAsync(
            IReadOnlyList<MortalLocationStorageCoordinate>? acceptedStorageCoordinates) =>
        MortalItemRouteAuthorityCatalog.BuildAsync(
            _fs,
            _writeLease,
            acceptedStorageCoordinates);

    private async Task NormalizeOrdinaryMortalItemTransfersAsync(
        IReadOnlyDictionary<string, string>? backups)
    {
        var currentRoots = await ReadOrdinaryMortalItemProjectionRootsAsync(
            backups: null);
        var backupRoots = await ReadOrdinaryMortalItemProjectionRootsAsync(backups);
        var previousCatalog = BuildOrdinaryPhysicalMortalItemCatalog(backupRoots);
        var currentCatalog = BuildOrdinaryPhysicalMortalItemCatalog(currentRoots);
        if (previousCatalog.Issues.Count > 0 || currentCatalog.Issues.Count > 0)
        {
            var issue = previousCatalog.Issues.FirstOrDefault() ?? currentCatalog.Issues[0];
            throw new InvalidDataException(
                $"Mortal item transfer carrier authority failed: {issue.Code}.");
        }

        var acceptedTurn = await TryReadCurrentTurnNumberAsync();
        var accepted = await MortalItemAcceptedTransferCatalog.BuildAsync(
            _fs,
            _writeLease,
            previousCatalog,
            currentCatalog,
            acceptedTurn);
        if (accepted.Issues.Count > 0)
        {
            throw new InvalidDataException(
                $"Mortal item transfer authority failed: {accepted.Issues[0].Code}.");
        }
        if (accepted.Transfers.Count == 0)
            return;

        if (currentRoots[MortalItemIdentityState.StatePath] is not JsonObject indexRoot)
            throw new InvalidDataException("Mortal item identity index is absent.");
        var transitionIds = accepted.Transfers.ToDictionary(
            static transfer => transfer.ItemId,
            static _ => "mitrn_" + Guid.NewGuid().ToString("N"),
            StringComparer.Ordinal);
        var projected = MortalItemTransferPlanner.Plan(
            currentRoots,
            MortalItemIdentityState.Parse(indexRoot.DeepClone()),
            accepted.Transfers,
            transitionIds);
        if (!projected.IsValid)
        {
            throw new InvalidDataException(
                $"Mortal item transfer failed: {projected.Issues[0].Code}.");
        }

        foreach (var path in MortalItemCanonicalProjectionPlanner.ProjectionRootPaths)
        {
            var after = projected.Roots[path];
            if (JsonNode.DeepEquals(currentRoots[path], after))
                continue;
            if (after == null)
                throw new InvalidDataException(
                    $"Mortal item transfer cannot remove canonical root '{path}'.");
            await WriteCanonicalFileAtomicAsync(path, after.ToJsonString(JsonOpts));
        }
    }

    private async Task<Dictionary<string, JsonNode?>>
        ReadOrdinaryMortalItemProjectionRootsAsync(
            IReadOnlyDictionary<string, string>? backups)
    {
        var result = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var path in MortalItemCanonicalProjectionPlanner.ProjectionRootPaths)
        {
            JsonNode? node;
            if (backups == null)
            {
                var json = await ReadCanonicalFileAsync(path);
                node = ParseOrdinaryProjectionNode(json, path);
            }
            else
            {
                node = await ReadBackupNodeAsync(path, backups);
            }
            result.Add(path, node?.DeepClone());
        }
        return result;
    }

    private static JsonNode? ParseOrdinaryProjectionNode(string? json, string path)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonNode.Parse(json) ??
                   throw new InvalidDataException($"Canonical root '{path}' is JSON null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Canonical root '{path}' is malformed.", exception);
        }
    }

    private static MortalItemCarrierCatalog BuildOrdinaryPhysicalMortalItemCatalog(
        IReadOnlyDictionary<string, JsonNode?> roots)
    {
        var companions = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var path in MortalItemCanonicalProjectionPlanner.ProjectionRootPaths.Skip(8))
        {
            if (roots[path] is JsonObject root)
                companions.Add(path, root);
        }
        var vehicles = roots[StorageTransportMoveService.VehiclesPath] switch
        {
            JsonObject root => root,
            JsonArray array => new JsonObject { ["vehicles"] = array.DeepClone() },
            _ => null
        };
        return MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            roots[InventoryEquipmentService.ItemsPath] as JsonObject,
            roots[NpcCoreChangesContract.NpcCorePath] as JsonObject,
            null,
            roots[StorageTransportMoveService.CurrentLocationPath] as JsonObject,
            vehicles,
            companions,
            roots[MortalLocationStorageContentsState.StatePath] as JsonObject));
    }
}
