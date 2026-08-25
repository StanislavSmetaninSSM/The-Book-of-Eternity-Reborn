using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectTargetAuthorityTests
{
    [Theory]
    [InlineData("mortal_world", "player", "player_current")]
    [InlineData("mortal_world", "npc", "npc_healer")]
    [InlineData("mortal_world", "combatant", "combatant_raider")]
    [InlineData("chaos_sea", "guardian", "guardian_mirror")]
    [InlineData("shining_abode", "resident", "resident_lumen")]
    [InlineData("shining_abode", "afterlife_actor", "shining_faction_head_council")]
    [InlineData("shining_abode", "radiant_actor", "radiant_actor_iris")]
    [InlineData("chaos_sea", "afterlife_actor", "afterlife_actor_echo")]
    [InlineData("chaos_sea", "spiritual_conflict_side", "conflict_test:player")]
    public void Resolve_EachSupportedExistingTargetUsesExactAuthority(
        string realm,
        string kind,
        string targetId)
    {
        var authority = Build(Target(realm, kind, targetId));

        var result = authority.Resolve(new JsonObject
        {
            ["kind"] = kind,
            ["targetId"] = targetId
        }, realm);

        Assert.Empty(authority.Issues);
        Assert.True(result.Success);
        Assert.Equal(new EffectTargetKey(realm, kind, targetId), result.Target);
    }

    [Fact]
    public void CombatantState_AllocatesStableClientIdFromExactSameTurnRef()
    {
        var factory = new CountingEffectIdentityFactory();
        var raw = new JsonArray(new JsonObject
        {
            ["combatantRef"] = "combatant_ref_raider",
            ["name"] = "Налётчик"
        });

        var result = CombatantIdentityState.BuildNew(raw, factory);

        Assert.Empty(result.Issues);
        Assert.Equal(1, factory.CombatantCalls);
        Assert.True(result.State!.TryResolveCombatant("combatant_ref_raider", out var combatantId));
        Assert.StartsWith("combatant_", combatantId, StringComparison.Ordinal);
        Assert.Equal(combatantId, result.RewrittenCombatants[0]!["combatantId"]!.GetValue<string>());
        Assert.Null(result.RewrittenCombatants[0]!["combatantRef"]);
        Assert.Null(raw[0]!["combatantId"]);
    }

    [Fact]
    public void CombatantState_ExposesReadOnlyReferenceMapping()
    {
        var result = CombatantIdentityState.BuildNew(
            new JsonArray(new JsonObject { ["combatantRef"] = "combatant_ref_raider" }),
            new CountingEffectIdentityFactory());

        Assert.True(
            Assert.IsAssignableFrom<IDictionary<string, string>>(result.State!.CombatantIdsByRef)
                .IsReadOnly);
    }

    [Fact]
    public void Resolve_SameTurnCombatantRefStoresOnlyPermanentTargetId()
    {
        var combatants = CombatantIdentityState.BuildNew(
            new JsonArray(new JsonObject { ["combatantRef"] = "combatant_ref_raider" }),
            new CountingEffectIdentityFactory());
        var authority = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            Array.Empty<EffectTargetExport>(),
            Array.Empty<EffectTargetExport>(),
            EmptySet(),
            combatants.State));

        var result = authority.Resolve(new JsonObject
        {
            ["kind"] = "combatant",
            ["targetRef"] = "combatant_ref_raider"
        }, "mortal_world");

        Assert.True(result.Success);
        Assert.StartsWith("combatant_", result.Target!.TargetId, StringComparison.Ordinal);
        Assert.DoesNotContain("combatant_ref", result.Target.TargetId, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_SameTurnPermanentTargetIdCannotReplaceRequiredTargetRef()
    {
        var combatants = CombatantIdentityState.BuildNew(
            new JsonArray(new JsonObject { ["combatantRef"] = "combatant_ref_raider" }),
            new CountingEffectIdentityFactory());
        Assert.True(combatants.State!.TryResolveCombatant("combatant_ref_raider", out var combatantId));
        var authority = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            Array.Empty<EffectTargetExport>(),
            Array.Empty<EffectTargetExport>(),
            EmptySet(),
            combatants.State));

        var result = authority.Resolve(new JsonObject
        {
            ["kind"] = "combatant",
            ["targetId"] = combatantId
        }, "mortal_world");

        Assert.Contains(result.Issues, issue => issue.Code == "effect_target_same_turn_id_forbidden");
    }

    [Fact]
    public void CombatantState_RejectsSubmittedPermanentIdAndDuplicateConfusableRefs()
    {
        var raw = new JsonArray(
            new JsonObject
            {
                ["combatantRef"] = "combatant_ref_raider",
                ["combatantId"] = "forged"
            },
            new JsonObject { ["combatantRef"] = "COMBATANT_REF_RAIDER" });

        var result = CombatantIdentityState.BuildNew(raw, new CountingEffectIdentityFactory());

        Assert.Null(result.State);
        Assert.Contains(result.Issues, issue => issue.Code == "mechanics_combatant_id_forbidden");
        Assert.Contains(result.Issues, issue => issue.Code == "mechanics_identity_ref_confusable");
    }

    [Theory]
    [InlineData("NPC_HEALER", "effect_target_selector_confusable")]
    [InlineData("npc_heаler", "effect_target_selector_confusable")]
    [InlineData("npc_retired", "effect_target_selector_historical")]
    [InlineData("npc_unknown", "effect_target_selector_unresolved")]
    public void Resolve_AliasHistoricalAndUnknownTargetFailsClosed(string targetId, string expectedCode)
    {
        var authority = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            new[] { Target("mortal_world", "npc", "npc_healer") },
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal) { "npc_retired" },
            null));

        var result = authority.Resolve(new JsonObject
        {
            ["kind"] = "npc",
            ["targetId"] = targetId
        }, "mortal_world");

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public void Resolve_CrossRealmAndDualSelectorFailClosed()
    {
        var authority = Build(Target("chaos_sea", "guardian", "guardian_mirror"));

        Assert.Contains(
            authority.Resolve(new JsonObject
            {
                ["kind"] = "guardian",
                ["targetId"] = "guardian_mirror"
            }, "mortal_world").Issues,
            issue => issue.Code == "effect_target_realm_mismatch");
        Assert.Contains(
            authority.Resolve(new JsonObject
            {
                ["kind"] = "guardian",
                ["targetId"] = "guardian_mirror",
                ["targetRef"] = "guardian_ref"
            }, "chaos_sea").Issues,
            issue => issue.Code == "effect_target_selector_invalid");
    }

    [Fact]
    public void Build_DuplicateConfusableTargetExportsFailClosed()
    {
        var authority = Build(
            Target("mortal_world", "npc", "npc_healer"),
            Target("mortal_world", "npc", "NPC_HEALER"));

        Assert.Contains(authority.Issues, issue => issue.Code == "effect_target_authority_confusable_target");
    }

    [Theory]
    [InlineData("npc_ref", "npc_ref")]
    [InlineData("npc_ref", "NPC_REF")]
    public void Resolve_DuplicateOrConfusableSameTurnRefsFailClosed(string firstRef, string secondRef)
    {
        var authority = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            Array.Empty<EffectTargetExport>(),
            new[]
            {
                new EffectTargetExport("mortal_world", "npc", "npc_one", true, firstRef),
                new EffectTargetExport("mortal_world", "npc", "npc_two", true, secondRef)
            },
            EmptySet(),
            null));

        var result = authority.Resolve(new JsonObject
        {
            ["kind"] = "npc",
            ["targetRef"] = firstRef
        }, "mortal_world");

        Assert.Contains(result.Issues, issue => issue.Code == "effect_target_selector_ambiguous");
    }

    [Fact]
    public void Build_SameTurnTargetWithoutTemporaryReferenceFailsClosed()
    {
        var authority = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            Array.Empty<EffectTargetExport>(),
            new[] { new EffectTargetExport("mortal_world", "npc", "npc_new", true) },
            EmptySet(),
            null));

        Assert.Contains(authority.Issues, issue => issue.Code == "effect_target_authority_ref_required");
    }

    private static EffectTargetAuthority Build(params EffectTargetExport[] targets) =>
        EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            targets,
            Array.Empty<EffectTargetExport>(),
            EmptySet(),
            null));

    private static EffectTargetExport Target(string realm, string kind, string targetId) =>
        new(realm, kind, targetId, SameTurn: false);

    private static IReadOnlySet<string> EmptySet() => new HashSet<string>(StringComparer.Ordinal);

    private sealed class CountingEffectIdentityFactory : EffectIdentityFactory
    {
        internal int CombatantCalls { get; private set; }

        internal override string CreateCombatantId()
        {
            CombatantCalls++;
            return base.CreateCombatantId();
        }
    }
}
