using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class CombatantIdentityStateTests
{
    [Fact]
    public void BuildNew_AllocatesAndConsumesEveryCombatantAndMemberRefWithoutCommandTargeting()
    {
        var factory = new CountingIdentityFactory();
        var combatants = new JsonArray(
            new JsonObject { ["combatantRef"] = "combatant_ref_alpha" },
            new JsonObject
            {
                ["combatantRef"] = "combatant_ref_named",
                ["NPCId"] = "npc_alpha"
            });
        var members = new JsonArray(
            new JsonObject { ["memberRef"] = "member_ref_alpha", ["name"] = "Первый" },
            new JsonObject { ["memberRef"] = "member_ref_beta", ["name"] = "Второй" });

        var result = CombatantIdentityState.BuildNew(combatants, members, factory);

        Assert.Empty(result.Issues);
        Assert.Equal(2, factory.CombatantCalls);
        Assert.Equal(2, factory.MemberCalls);
        Assert.All(result.RewrittenCombatants, node =>
        {
            Assert.NotNull(node!["combatantId"]);
            Assert.Null(node["combatantRef"]);
        });
        Assert.All(result.RewrittenMembers, node =>
        {
            Assert.NotNull(node!["memberId"]);
            Assert.Null(node["memberRef"]);
        });
        Assert.True(result.State!.TryResolveCombatant("combatant_ref_alpha", out _));
        Assert.True(result.State.TryResolveMember("member_ref_beta", out _));
        Assert.True(result.State.TryGetBoundNpcId("combatant_ref_named", out var npcId));
        Assert.Equal("npc_alpha", npcId);
        Assert.Null(combatants[0]!["combatantId"]);
        Assert.Null(members[0]!["memberId"]);
    }

    [Fact]
    public void BuildNew_PreservesExistingMemberIdentityAcrossReorder()
    {
        var firstOrder = new JsonArray(
            new JsonObject { ["memberId"] = "member_alpha", ["name"] = "Первый" },
            new JsonObject { ["memberId"] = "member_beta", ["name"] = "Второй" });
        var secondOrder = new JsonArray(
            firstOrder[1]!.DeepClone(),
            firstOrder[0]!.DeepClone());

        var first = CombatantIdentityState.BuildNew(
            new JsonArray(),
            firstOrder,
            new CountingIdentityFactory());
        var second = CombatantIdentityState.BuildNew(
            new JsonArray(),
            secondOrder,
            new CountingIdentityFactory());

        Assert.Empty(first.Issues);
        Assert.Empty(second.Issues);
        Assert.Equal(first.State!.Fingerprint, second.State!.Fingerprint);
        Assert.Equal("member_beta", second.RewrittenMembers[0]!["memberId"]!.GetValue<string>());
        Assert.Equal("member_alpha", second.RewrittenMembers[1]!["memberId"]!.GetValue<string>());
    }

    [Fact]
    public void BuildNew_RejectsSubmittedIdsDuplicateConfusableAndCrossKindRefs()
    {
        var combatants = new JsonArray(
            new JsonObject
            {
                ["combatantRef"] = "identity_ref_alpha",
                ["combatantId"] = "combatant_forged"
            },
            new JsonObject { ["combatantRef"] = "IDENTITY_REF_BETA" });
        var members = new JsonArray(
            new JsonObject { ["memberRef"] = "identity_ref_beta" },
            new JsonObject
            {
                ["memberRef"] = "member_ref_forged",
                ["memberId"] = "member_forged"
            });

        var result = CombatantIdentityState.BuildNew(
            combatants,
            members,
            new CountingIdentityFactory());

        Assert.Null(result.State);
        Assert.Contains(result.Issues, issue => issue.Code == "mechanics_combatant_id_forbidden");
        Assert.Contains(result.Issues, issue => issue.Code == "mechanics_member_id_forbidden");
        Assert.Contains(result.Issues, issue => issue.Code == "mechanics_identity_ref_confusable");
    }

    [Fact]
    public void BuildNew_RejectsInvalidNpcBindingAndDuplicateExistingIds()
    {
        var combatants = new JsonArray(
            new JsonObject { ["combatantId"] = "combatant_alpha", ["NPCId"] = " npc_alpha " },
            new JsonObject { ["combatantId"] = "COMBATANT_ALPHA" });
        var members = new JsonArray(
            new JsonObject { ["memberId"] = "member_alpha" },
            new JsonObject { ["memberId"] = "MEMBER_ALPHA" });

        var result = CombatantIdentityState.BuildNew(
            combatants,
            members,
            new CountingIdentityFactory());

        Assert.Contains(result.Issues, issue => issue.Code == "mechanics_combatant_npc_binding_invalid");
        Assert.Contains(result.Issues, issue => issue.Code == "mechanics_combatant_id_confusable");
        Assert.Contains(result.Issues, issue => issue.Code == "mechanics_member_id_confusable");
    }

    [Fact]
    public void ValidateCanonical_RejectsResidualRefsAndMissingStableMemberIdentity()
    {
        var issues = CombatantIdentityState.ValidateCanonical(
            new JsonArray(new JsonObject { ["combatantRef"] = "combatant_ref_alpha" }),
            new JsonArray(
                new JsonObject { ["memberRef"] = "member_ref_alpha" },
                new JsonObject { ["name"] = "Без идентичности" }));

        Assert.Contains(issues, issue => issue.Code == "mechanics_combatant_ref_residual");
        Assert.Contains(issues, issue => issue.Code == "mechanics_member_ref_residual");
        Assert.Contains(issues, issue => issue.Code == "mechanics_member_id_missing");
    }

    [Fact]
    public void State_ExposesReadOnlyReferenceAndIdentityCollections()
    {
        var result = CombatantIdentityState.BuildNew(
            new JsonArray(new JsonObject { ["combatantRef"] = "combatant_ref_alpha" }),
            new JsonArray(new JsonObject { ["memberRef"] = "member_ref_alpha" }),
            new CountingIdentityFactory());

        Assert.True(Assert.IsAssignableFrom<IDictionary<string, string>>(
            result.State!.CombatantIdsByRef).IsReadOnly);
        Assert.True(Assert.IsAssignableFrom<IDictionary<string, string>>(
            result.State.MemberIdsByRef).IsReadOnly);
        Assert.True(Assert.IsAssignableFrom<ISet<string>>(
            result.State.CombatantIds).IsReadOnly);
        Assert.True(Assert.IsAssignableFrom<ISet<string>>(
            result.State.MemberIds).IsReadOnly);
    }

    private sealed class CountingIdentityFactory : CombatantIdentityFactory
    {
        internal int CombatantCalls { get; private set; }

        internal int MemberCalls { get; private set; }

        internal override string CreateCombatantId()
        {
            CombatantCalls++;
            return $"combatant_test_{CombatantCalls}";
        }

        internal override string CreateMemberId()
        {
            MemberCalls++;
            return $"member_test_{MemberCalls}";
        }
    }
}
