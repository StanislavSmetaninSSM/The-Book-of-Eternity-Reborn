using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record AfterlifeConflictActionPointProjection(
    string ConflictId,
    ResourceStateEntry Player,
    ResourceStateEntry Opposition);

internal sealed record AfterlifeConflictActionPointProjectionResult(
    AfterlifeConflictActionPointProjection? Projection,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Projection != null && Issues.Count == 0;
}

internal static class AfterlifeConflictActionPointProjectionService
{
    private const string ResourceKey = "spiritual_action_points";

    internal static AfterlifeConflictActionPointProjectionResult Resolve(
        string? definitionsJson,
        string? stateJson,
        JsonObject activeConflict)
    {
        ArgumentNullException.ThrowIfNull(activeConflict);
        var issues = new List<ValidationIssue>();
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            definitionsJson,
            allowMissingPristine: false);
        issues.AddRange(definitions.Issues);
        if (definitions.Catalog == null)
            return new AfterlifeConflictActionPointProjectionResult(null, issues);
        var state = ResourceStateContract.ParseCanonical(
            stateJson,
            definitions.Catalog,
            allowMissingPristine: false);
        issues.AddRange(state.Issues);
        if (state.Ledger == null)
            return new AfterlifeConflictActionPointProjectionResult(null, issues);

        var conflictId = ReadExact(activeConflict["conflictId"]);
        var rawRealm = AfterlifeSpiritualConflictState.GetNodeString(
            activeConflict["realm"]);
        var oppositionOwnerId = ReadExact(
            activeConflict[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]?
                ["opposition"]?["resourceOwnerId"]);
        if (conflictId == null ||
            !AfterlifeEntityProfileState.TryNormalizeEffectRealm(rawRealm, out var realm) ||
            oppositionOwnerId == null)
        {
            Add(
                issues,
                AfterlifeSpiritualConflictState.StatePath + ".activeConflict",
                "afterlife_conflict_resource_projection_authority_invalid",
                "exact conflictId/realm and client-owned opposition resource binding",
                activeConflict.ToJsonString());
            return new AfterlifeConflictActionPointProjectionResult(null, issues);
        }

        var playerCoordinate = new ResourceCoordinate(
            realm,
            ResourceOwnerKind.AfterlifeActor,
            "player_soul",
            ResourceKey);
        var oppositionCoordinate = new ResourceCoordinate(
            realm,
            ResourceOwnerKind.AfterlifeConflictSide,
            oppositionOwnerId,
            ResourceKey);
        if (!TryResolveActive(state.Ledger, playerCoordinate, out var player, issues) ||
            !TryResolveActive(state.Ledger, oppositionCoordinate, out var opposition, issues))
        {
            return new AfterlifeConflictActionPointProjectionResult(null, issues);
        }

        return new AfterlifeConflictActionPointProjectionResult(
            new AfterlifeConflictActionPointProjection(
                conflictId,
                player!,
                opposition!),
            Array.Empty<ValidationIssue>());
    }

    internal static JsonObject ToPreviewJson(ResourceStateEntry entry) =>
        new()
        {
            ["resourceKey"] = entry.Coordinate.ResourceKey,
            ["realm"] = entry.Coordinate.Realm,
            ["ownerKind"] = ResourceDefinitionCatalog.GetOwnerKindToken(
                entry.Coordinate.OwnerKind),
            ["resourceOwnerId"] = entry.Coordinate.ResourceOwnerId,
            ["current"] = entry.Current,
            ["maximum"] = entry.Maximum,
            ["state"] = entry.State == ResourceLifecycleState.Active
                ? "active"
                : "suspended",
            ["capacityAuthorityFingerprint"] =
                entry.CapacityBinding.AuthorityFingerprint
        };

    private static bool TryResolveActive(
        ResourceStateLedger state,
        ResourceCoordinate coordinate,
        out ResourceStateEntry? entry,
        List<ValidationIssue> issues)
    {
        if (state.TryResolveExact(coordinate, out entry) &&
            entry != null &&
            entry.State == ResourceLifecycleState.Active)
        {
            return true;
        }
        Add(
            issues,
            ResourceMaterializationContract.StatePath,
            "afterlife_conflict_resource_projection_coordinate_missing",
            $"active exact coordinate {coordinate.Realm}/{coordinate.OwnerKind}/{coordinate.ResourceOwnerId}/{coordinate.ResourceKey}",
            entry == null ? "missing" : entry.State.ToString());
        return false;
    }

    private static string? ReadExact(JsonNode? node)
    {
        var value = AfterlifeSpiritualConflictState.GetNodeString(node);
        return ResourceMaterializationContract.IsExactIdentifier(value)
            ? value
            : null;
    }

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        ResourceMaterializationContract.AddIssue(
            issues,
            path,
            code,
            expected,
            actual);
}
