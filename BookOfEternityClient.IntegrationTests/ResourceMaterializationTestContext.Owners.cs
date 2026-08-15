using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

internal sealed record ResourceOwnerFixture(
    string Realm,
    string OwnerKind,
    string ResourceOwnerId,
    bool SameTurn,
    string? OwnerRef,
    JsonObject RawTarget,
    JsonObject CanonicalExport);

internal sealed partial class ResourceMaterializationTestContext
{
    internal static IReadOnlyList<ResourceOwnerFixture> CreateOwnerFixtures() =>
        new[]
        {
            ExistingOwner("mortal_world", "player", "player_current", "health", "energy", "poise"),
            SameTurnOwner("mortal_world", "npc", "npc_ref_resource_test", "npc_resource_test", "health"),
            SameTurnOwner("mortal_world", "combatant", "combatant_ref_resource_test", "combatant_resource_test", "health", "energy", "poise"),
            SameTurnOwner("mortal_world", "combat_group_member", "member_ref_resource_test", "member_resource_test", "health", "poise"),
            SameTurnOwner("mortal_world", "vehicle", "vehicle_ref_resource_test", "vehicle_resource_test", "health"),
            SameTurnOwner("mortal_world", "item", "item_ref_resource_test", "item_resource_test", "durability", "charges", "ammunition"),
            SameTurnOwner("chaos_sea", "afterlife_actor", "afterlife_actor_ref_resource_test", "afterlife_actor_resource_test", "spiritual_action_points", "gacha_attempts", "blessing_rerolls"),
            SameTurnOwner("chaos_sea", "afterlife_conflict_side", "conflict_side_ref_resource_test", "conflict_side_resource_test", "spiritual_action_points"),
            SameTurnOwner("shining_abode", "afterlife_scope", "afterlife_scope_ref_resource_test", "afterlife_scope_resource_test", "gacha_attempts")
        };

    internal static IReadOnlyDictionary<string, JsonObject> CreateEmptyResourceRoots() =>
        new Dictionary<string, JsonObject>(StringComparer.Ordinal)
        {
            [DefinitionsPath] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["definitions"] = new JsonArray()
            },
            [StatePath] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            },
            [HistoryPath] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            },
            [CommandsPath] = new JsonObject
            {
                ["resourceDefinitionCreations"] = new JsonArray(),
                ["resourceCapacityChanges"] = new JsonArray(),
                ["resourceChanges"] = new JsonArray()
            }
        };

    private static ResourceOwnerFixture ExistingOwner(
        string realm,
        string ownerKind,
        string resourceOwnerId,
        params string[] capabilities) =>
        CreateOwnerFixture(
            realm,
            ownerKind,
            resourceOwnerId,
            sameTurn: false,
            ownerRef: null,
            capabilities);

    private static ResourceOwnerFixture SameTurnOwner(
        string realm,
        string ownerKind,
        string ownerRef,
        string resourceOwnerId,
        params string[] capabilities) =>
        CreateOwnerFixture(
            realm,
            ownerKind,
            resourceOwnerId,
            sameTurn: true,
            ownerRef,
            capabilities);

    private static ResourceOwnerFixture CreateOwnerFixture(
        string realm,
        string ownerKind,
        string resourceOwnerId,
        bool sameTurn,
        string? ownerRef,
        IReadOnlyCollection<string> capabilities)
    {
        var rawTarget = new JsonObject
        {
            ["kind"] = ownerKind,
            [sameTurn ? "targetRef" : "targetId"] = sameTurn
                ? ownerRef
                : resourceOwnerId
        };
        var capabilityArray = new JsonArray();
        foreach (var capability in capabilities)
            capabilityArray.Add(capability);

        var canonicalExport = new JsonObject
        {
            ["realm"] = realm,
            ["ownerKind"] = ownerKind,
            ["resourceOwnerId"] = resourceOwnerId,
            ["state"] = "active",
            ["sameTurn"] = sameTurn,
            ["ownerRef"] = ownerRef,
            ["capabilities"] = capabilityArray
        };

        return new ResourceOwnerFixture(
            realm,
            ownerKind,
            resourceOwnerId,
            sameTurn,
            ownerRef,
            rawTarget,
            canonicalExport);
    }
}
