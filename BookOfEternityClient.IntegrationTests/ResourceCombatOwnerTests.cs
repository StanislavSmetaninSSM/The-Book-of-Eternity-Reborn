using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceCombatOwnerTests
{
    [Fact]
    public void Compose_RejectsTwoCombatRowsBoundToTheSameNamedNpc()
    {
        var npcCore = new JsonObject
        {
            ["NPCsInScene"] = new JsonArray(
                new JsonObject
                {
                    ["NPCId"] = "npc_guard_captain",
                    ["name"] = "Капитан стражи"
                })
        };
        var enemies = new JsonObject
        {
            ["enemiesData"] = new JsonArray(
                new JsonObject
                {
                    ["NPCId"] = "npc_guard_captain",
                    ["name"] = "Капитан стражи",
                    ["isGroup"] = false
                })
        };
        var allies = new JsonObject
        {
            ["alliesData"] = new JsonArray(
                new JsonObject
                {
                    ["NPCId"] = "npc_guard_captain",
                    ["name"] = "Капитан стражи",
                    ["isGroup"] = false
                })
        };

        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                new MortalResourceOwnerRoots(
                    npcCore,
                    new JsonObject { ["enemiesData"] = new JsonArray() },
                    new JsonObject { ["alliesData"] = new JsonArray() },
                    new JsonObject { ["vehicles"] = new JsonArray() }),
                new MortalResourceOwnerRoots(
                    npcCore,
                    enemies,
                    allies,
                    new JsonObject { ["vehicles"] = new JsonArray() })));

        Assert.Null(result.Authority);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_owner_named_combatant_binding_duplicate");
        Assert.Empty(result.OwnerCompanionAfterImages);
    }

    [Fact]
    public void Compose_ReorderedGroupMembersKeepIdsAndRemovedMemberBecomesHistorical()
    {
        var preTurnEnemies = Group("member_alpha", "member_beta", "member_gamma");
        var acceptedEnemies = Group("member_gamma", "member_alpha");

        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                Roots(preTurnEnemies),
                Roots(acceptedEnemies)));

        Assert.Empty(result.Issues);
        var authority = Assert.IsType<ResourceOwnerAuthority>(result.Authority);
        var memberIds = authority.Entries.Values
            .Where(entry => entry.Key.OwnerKind == ResourceOwnerKind.CombatGroupMember)
            .Select(entry => entry.Key.ResourceOwnerId)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "member_alpha", "member_gamma" }, memberIds);
        Assert.All(
            authority.Entries.Values.Where(entry =>
                entry.Key.OwnerKind == ResourceOwnerKind.CombatGroupMember),
            entry => Assert.False(entry.SameTurn));

        var removed = authority.Resolve(new ResourceOwnerRequest(
            "mortal_world",
            ResourceOwnerKind.CombatGroupMember,
            "health",
            ResourceOwnerId: "member_beta",
            OwnerRef: null));
        Assert.Contains(removed.Issues, issue =>
            issue.Code == "resource_owner_historical");

        var afterImage = result.OwnerCompanionAfterImages[EffectCarrierCatalog.EnemiesPath];
        var members = afterImage["enemiesData"]![0]!["members"]!.AsArray();
        Assert.Equal(
            new[] { "member_gamma", "member_alpha" },
            members.Select(member => member!["memberId"]!.GetValue<string>()));
    }

    private static MortalResourceOwnerRoots Roots(JsonObject enemies) =>
        new(
            new JsonObject { ["NPCsInScene"] = new JsonArray() },
            enemies,
            new JsonObject { ["alliesData"] = new JsonArray() },
            new JsonObject { ["vehicles"] = new JsonArray() });

    private static JsonObject Group(params string[] memberIds) =>
        new()
        {
            ["enemiesData"] = new JsonArray(
                new JsonObject
                {
                    ["name"] = "Дозор",
                    ["isGroup"] = true,
                    ["members"] = new JsonArray(memberIds
                        .Select(memberId => (JsonNode)new JsonObject
                        {
                            ["memberId"] = memberId,
                            ["name"] = memberId
                        })
                        .ToArray())
                })
        };
}
